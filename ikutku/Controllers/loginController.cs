using System;
using System.Threading;
using System.Web.Mvc;
using clearpixels.Helpers;
using ikutku.DB.extension;
using ikutku.Models.twitter;
using LinqToTwitter;
using ikutku.Constants;
using clearpixels.Logging;
using ikutku.DB;
using ikutku.Library.ActionFilters;
using ikutku.Library.Workers;
using ikutku.Models.queue;
using ikutku.Models.user;
using user = ikutku.DB.user;

namespace ikutku.Controllers
{
    public class loginController : baseController
    {
        IOAuthCredentials _credentials;
        private readonly IUnitOfWork _unitOfWork;
        MvcAuthorizer _auth;

        public loginController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        protected override void Dispose(bool disposing)
        {
            _unitOfWork.Dispose();
            base.Dispose(disposing);
        }

        public ActionResult Index()
        {
            // clear error messagre
            Session[General.SESSION_ERRORMESSAGE] = null;

            _credentials = new SessionStateCredentials() { ConsumerKey = General.OAUTH_CONSUMER_KEY, ConsumerSecret = General.OAUTH_CONSUMER_SECRET };
            _auth = new MvcAuthorizer
            {
                Credentials = _credentials
            };

            // internally, this doesn't execute if BeginAuthorization hasn't been called yet
            //  but it will execute after the user authorizes your application
            _auth.CompleteAuthorization(Request.Url);

            // this will only execute if we don't have all 4 keys, which is what IsAuthorized checks
            if (!_auth.IsAuthorized)
            {
                // url param is optional, it lets you specify the page Twitter redirects to.
                // You can use it to complete the OAuth process on another action/controller - in which
                // case you would move auth.CompleteAuthorization to that action/controller.
                return _auth.BeginAuthorization(new Uri(General.OAUTH_CALLBACK_URL));
            }

            user usr;
            var loginFailureCode = TwitterErrorCode.UNKNOWN_ERROR;

            try
            {
                var twitterWorker = new TwitterWorker(new TwitterContext(_auth));

                // make an oauth-authenticated call with the access token
                var identity = twitterWorker.VerifyCredentials();
                var twitterid = identity.User.Identifier.ID;

                usr = _unitOfWork.Repository<user>().FindById(twitterid);
                if (usr == null)
                {
                    usr = new user
                    {
                        id = twitterid,
                        followerCountTotal = identity.User.FollowersCount,
                        followingCountTotal = identity.User.FriendsCount
                    };
                    _unitOfWork.Repository<user>().Insert(usr);
                    usr.TouchLastLoginAndOtherThings(_unitOfWork);
                    usr.AddToDiffQueue((int)QueueSettings.ALL_BUILDS_USER);
                }

                usr.username = identity.User.Identifier.ScreenName;
                usr.photoUrl = identity.User.ProfileImageUrl;
                usr.updated = DateTime.UtcNow;
                usr.oauthToken = _auth.Credentials.OAuthToken;
                usr.oauthSecret = _auth.Credentials.AccessToken;
                _unitOfWork.SaveChanges();

                // add cookie
                usr.SetLoginCookie();

                var siteAuth = Thread.CurrentPrincipal.ToAuthInfo();

                var dbworker = new DatabaseWorker(siteAuth);
                var resetSettings = dbworker.IsAccountUpToDate();

                // existing accounts
                if (resetSettings != QueueSettings.NONE)
                {
                    dbworker.ResetQueue(resetSettings | QueueSettings.USER_TRIGGERED);
                }

                return Redirect("/dashboard");
            }
            catch (TwitterQueryException ex)
            {
                loginFailureCode = ex.ErrorCode.ToEnum<TwitterErrorCode>();

                Syslog.Write(ex, "Login failed for {0} {1} {2}", _credentials.AccessToken, _credentials.OAuthToken, _credentials.UserId);
            }
            catch (Exception ex)
            {
                Syslog.Write(ex, "Login failed for {0} {1} {2}", _credentials.AccessToken, _credentials.OAuthToken, _credentials.UserId);
            }

            usr = _unitOfWork.Repository<user>().FindById(_credentials.UserId);
            Session[General.SESSION_ERRORMESSAGE] = usr.ClearAuthTokensAndExecuteFormsSignOut(loginFailureCode);

            _unitOfWork.SaveChanges();

            return Redirect("/");
        }

        [MvcAuthorize]
        public ActionResult SignOut()
        {
            var auth = Thread.CurrentPrincipal.ToAuthInfo();

            var usr = _unitOfWork.Repository<user>().FindById(auth.twitterUserid);
            Session[General.SESSION_ERRORMESSAGE] = usr.ClearAuthTokensAndExecuteFormsSignOut(TwitterErrorCode.NO_ERROR);

            _unitOfWork.SaveChanges();

            return RedirectToAction("Index", "Home");
        }
    }
}
