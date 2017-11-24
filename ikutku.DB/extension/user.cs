using System;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Security;
using ikutku.Constants;
using ikutku.DB.extension;
using ikutku.Models.user;

namespace ikutku.DB
{
    public partial class user
    {
        public queuedUser AddToDiffQueue(int queuesettings, bool isReQueue = false)
        {
            var q = queuedUsers.SingleOrDefault();
            if (q != null)
            {
                return q;
            }

            q = new queuedUser()
            {
                ownerid = id,
                settings = queuesettings
            };

            queuedUsers.Add(q);

            if (!isReQueue)
            {
                startTime = DateTime.UtcNow;
            }

            return q;
        }

        public queuedFollowingUser AddToFollowingsQueue()
        {
            var q = queuedFollowingUsers.SingleOrDefault();
            if (q != null)
            {
                return q;
            }

            q = new queuedFollowingUser()
            {
                ownerid = id
            };

            queuedFollowingUsers.Add(q);

            return q;
        }

        public void SetLoginCookie()
        {
            var ticket = new FormsAuthenticationTicket(General.FORMS_AUTH_VERSION,
                username,
                DateTime.UtcNow,
                DateTime.UtcNow.AddDays(10),
                true,
                id);

            string ticketString = FormsAuthentication.Encrypt(ticket);

            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, ticketString)
            {
                Expires = DateTime.UtcNow.AddDays(General.DB_ACCOUNT_VALID_DAYS), // synch with account validity
                HttpOnly = true
            };

            Thread.CurrentPrincipal = new ikutkuPrincipal(new FormsIdentity(ticket), this.ToAuthInfo());
            if (HttpContext.Current != null)
            {
                HttpContext.Current.User = Thread.CurrentPrincipal;
                HttpContext.Current.Response.Cookies.Add(cookie);
            }
        }

        public void TouchLastLoginAndOtherThings(IUnitOfWork uow)
        {
            // record last time user logged in
            if (lastLogin.HasValue)
            {
                var interval = new loginInterval()
                {
                    ownerid = id,
                    timeBetweenLogins = (DateTime.UtcNow - lastLogin.Value).Ticks
                };
                uow.Repository<loginInterval>().Insert(interval);
            }
            
            // set lastLogin
            lastLogin = DateTime.UtcNow;
        }
    }
}