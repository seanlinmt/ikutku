using System;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Web;
using System.Web.Caching;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using Elmah;
using clearpixels.Helpers;
using clearpixels.Helpers.authentication;
using clearpixels.Helpers.database;
using clearpixels.Helpers.scheduling;
using clearpixels.Logging;
using ikutku.App_Start;
using ikutku.Constants;
using ikutku.Controllers;
using ikutku.DB;
using ikutku.DB.extension;
using ikutku.Library.Scheduler;
using ikutku.Models.user;

namespace ikutku
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            // See http://blogs.msdn.com/b/tmarq/archive/2007/07/21/asp-net-thread-usage-on-iis-7-0-and-6-0.aspx
            /* 5.If your ASP.NET application is using web services (WFC or ASMX) or System.Net to communicate 
             * with a backend over HTTP you may need to increase connectionManagement/maxconnection.  
             * For ASP.NET applications, this is limited to 12 * #CPUs by the autoConfig feature.  
             * This means that on a quad-proc, you can have at most 12 * 4 = 48 concurrent 
             * connections to an IP end point.  Because this is tied to autoConfig, the easiest way
             * to increase maxconnection in an ASP.NET application is to 
             * set System.Net.ServicePointManager.DefaultConnectionLimit programatically,
             * from Application_Start, for example.  Set the value to the number of concurrent 
             * System.Net connections you expect your application to use.  I've set this to Int32.MaxValue
             * and not had any side effects, so you might try that--this is actually the default used 
             * in the native HTTP stack, WinHTTP.  If you're not able to set 
             * System.Net.ServicePointManager.DefaultConnectionLimit programmatically, you'll
             * need to disable autoConfig , but that means you also need to set maxWorkerThreads and maxIoThreads. 
             * You won't need to set minFreeThreads or minLocalRequestFreeThreads if you're not using classic/ISAPI mode.
             * */
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12;
            MvcHandler.DisableMvcResponseHeader = true;

            AreaRegistration.RegisterAllAreas();
            
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            GlobalConfiguration.Configure(WebApiConfig.Register);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            // http://msdn.microsoft.com/en-us/library/dn178463%28v=pandp.30%29.aspx
            InjectionConfig.Initialise();

            // start cache-based scheduler
            RegisterCacheEntry();
        }

        void Session_Start(object sender, EventArgs e)
        {
            //Ensure SessionID in order to prevent the following exception
	        //when the Application Pool Recycles
	        //[HttpException]: Session state has created a session id, but cannot
	        //    save it because the response was already flushed by  
            string id = Session.SessionID;
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception exception = Server.GetLastError();
            
            var httpException = exception as HttpException;

            // Clear the error on server.
            Response.Clear();
            Server.ClearError();

            // Avoid IIS7 getting in the middle
            Response.TrySkipIisCustomErrors = true;

            if (httpException == null)
            {
                return;
            }

            var routeData = new RouteData();
            IController errorController = new errorController();
            switch (httpException.GetHttpCode())
            {
                case 404:
                    routeData.Values["controller"] = "Error";
                    routeData.Values["action"] = "NotFound";
                    break;
                default:
                    routeData.Values["controller"] = "Error";
                    routeData.Values["action"] = "Index";
                    break;
            }

            // Call target Controller and pass the routeData.
            errorController.Execute(new RequestContext(
                 new HttpContextWrapper(Context), routeData));
        }

        // need to move this to an authorize attribute because it fires even for /dummy
        public void FormsAuthentication_OnAuthenticate(object sender, FormsAuthenticationEventArgs args)
        {
            if (HttpContext.Current != null && HttpContext.Current.Request.RawUrl.IndexOf(General.HTTP_CACHEURL) != -1)
            {
                return;
            }

            if (!Thread.CurrentPrincipal.Identity.IsAuthenticated)
            {
                if (!FormsAuthentication.CookiesSupported ||
                    args.Context.Request.Cookies[FormsAuthentication.FormsCookieName] == null)
                {
                    return;
                }

                var cookie = args.Context.Request.Cookies[FormsAuthentication.FormsCookieName];
                if (cookie != null)
                {
                    // decrypt cookie
                    try
                    {
                        FormsAuthenticationTicket ticket = FormsAuthentication.Decrypt(cookie.Value);
                        // renew ticket if old
                        var newTicket = FormsAuthentication.RenewTicketIfOld(ticket);
                        if (newTicket.Expiration != ticket.Expiration)
                        {
                            string encryptedTicket = FormsAuthentication.Encrypt(newTicket);

                            cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket);
                            cookie.Path = FormsAuthentication.FormsCookiePath;
                            args.Context.Response.Cookies.Add(cookie);
                        }

                        // set principal
                        AuthInfo auth = null;
                        using (DB.IRepository<user> userRepository = new Repository<user>())
                        {
                            userRepository.SetDeadlockPriority(DeadlockPriority.HIGH);

                            userRepository.AutoDetectChangesEnabled = false;

                            var usr = userRepository.FindOneNoTracking(x => x.id == ticket.UserData);

                            if (usr != null && usr.oauthSecret != null && usr.oauthToken != null)
                            {
                                auth = usr.ToAuthInfo();
                            }
                        }

                        if (auth != null)
                        {
                            Thread.CurrentPrincipal = new ikutkuPrincipal(new FormsIdentity(ticket), auth);
                            if (HttpContext.Current != null)
                            {
                                HttpContext.Current.User = Thread.CurrentPrincipal;
                            }
                        }
                        else
                        {
                            HttpContext.Current.SignOut();
                        }
                    }
                    catch (CryptographicException ex)
                    {
                        Syslog.Write(ex, "Cookie => {0}", cookie.Value);
                        HttpContext.Current.SignOut();
                    }
                    catch (FormatException ex)
                    {
                        Syslog.Write(ex);
                        HttpContext.Current.SignOut();
                    }
                    catch (Exception ex)
                    {
                        Syslog.Write(ex);
                    }
                }
            }
            else
            {
#if DEBUG
                Syslog.Write("authenticated");
#endif
            }
        }

        private void RegisterCacheEntry()
        {
            Debug.WriteLine("RegisterCacheEntry .....");
            // Prevent duplicate key addition
            if (HttpRuntime.Cache[CacheTimerType.Minute1.ToString()] == null)
            {
                HttpRuntime.Cache.Add(CacheTimerType.Minute1.ToString(),
                    1,
                    null,
                    DateTime.UtcNow.AddMinutes(1),
                    Cache.NoSlidingExpiration,
                    CacheItemPriority.NotRemovable,
                    CacheItemRemovedCallback);
            }

            if (HttpRuntime.Cache[CacheTimerType.Minute5.ToString()] == null)
            {
                HttpRuntime.Cache.Add(CacheTimerType.Minute5.ToString(),
                    5,
                    null,
#if DEBUG
                    DateTime.UtcNow.AddMinutes(1),
#else
                    DateTime.UtcNow.AddMinutes(5),
#endif
                    Cache.NoSlidingExpiration,
                    CacheItemPriority.NotRemovable,
                    CacheItemRemovedCallback);
            }

            if (HttpRuntime.Cache[CacheTimerType.Minute30.ToString()] == null)
            {
                HttpRuntime.Cache.Add(CacheTimerType.Minute30.ToString(),
                    30,
                    null,
#if DEBUG
                    DateTime.UtcNow.AddMinutes(1),
#else
                    DateTime.UtcNow.AddMinutes(30),
#endif
                    Cache.NoSlidingExpiration,
                    CacheItemPriority.NotRemovable,
                    CacheItemRemovedCallback);
            }

            if (HttpRuntime.Cache[CacheTimerType.Minute60.ToString()] == null)
            {
                HttpRuntime.Cache.Add(CacheTimerType.Minute60.ToString(),
                    60,
                    null,
#if DEBUG
                    DateTime.UtcNow.AddMinutes(1),
#else
                    DateTime.UtcNow.AddMinutes(60),
#endif

 Cache.NoSlidingExpiration,
                    CacheItemPriority.NotRemovable,
                    CacheItemRemovedCallback);
            }
        }

        private void CacheItemRemovedCallback(
            string key,
            object value,
            CacheItemRemovedReason reason
            )
        {
            //if (reason != CacheItemRemovedReason.Expired)
            //{
            //    eJException newex = new eJException();
            //    newex.logException("cacheExpired: " + key + " " + reason.ToString(), null);
            //}
            Debug.WriteLine("Cache Expired: " + key);

            try
            {
                switch (key.ToEnum<CacheTimerType>())
                {
                    case CacheTimerType.Minute1:
                        CacheScheduler.Instance.StartSystemThread(() =>
                        {
                            BackgroundScheduler.Instance.ProcessQueuedUsers();
                            BackgroundScheduler.Instance.ProcessQueuedFollowingUsers();
                        }, TaskType.REBUILD.ToInt());
                        break;
                    case CacheTimerType.Minute5:
                        CacheScheduler.Instance.StartSystemThread(
                            () => BackgroundCacheWorker.Instance.DeleteStaleCacheEveryFiveMinutes(),
                            TaskType.PURGE_CACHE.ToInt()); 
                        break;
                    case CacheTimerType.Minute30:
                        break;
                    case CacheTimerType.Minute60:
                        CacheScheduler.Instance.StartSystemThread(
                            () => BackgroundScheduler.Instance.FindAndDeleteStaleAccount(),
                            TaskType.PURGE_ACCOUNTS.ToInt());

                        /*
                        CacheScheduler.Instance.StartSystemThread(
                            () => BackgroundCacheWorker.Instance.Start(),
                            BackgroundScheduler.TaskType.RESYNC); 
                        */
                        break;
                    default:
                        Syslog.Write("CacheScheduler ERROR: " + key);
                        break;
                }
            }
            catch (Exception ex)
            {
                Syslog.Write(ex);
            }
            finally
            {
                RegisterCacheEntry();
            }
            
            HitPage();
        }

        private void HitPage()
        {
            var req = WebRequest.Create(General.HTTP_CACHEURL);
            req.Method = "HEAD";
            req.Timeout = 10000;
            WebResponse resp = null;
            try
            {
                resp = req.GetResponse();
            }
            catch (Exception)
            {
                
            }
            finally
            {
                if (resp != null)
                {
                    resp.Close();
                }
            }
        }

        // this is only hit when Raise error is used which only happens when the exception occurs with a HttpContext
        // http://code.google.com/p/elmah/wiki/ErrorFiltering
        protected void ErrorLog_OnFiltering(object sender, ExceptionFilterEventArgs e)
        {
            //Syslog.Write("ErrorLog_Filtering received {0}", e.Exception.GetBaseException().GetType().Name);

            if (e.Exception.GetBaseException() is ThreadAbortException)
            {
                e.Dismiss();
            }
        }
    }
}