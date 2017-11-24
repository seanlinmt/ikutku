using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using clearpixels.Helpers.authentication;
using clearpixels.Helpers.datetime;
using clearpixels.Logging;
using ikutku.Constants;
using ikutku.Models.sync;
using ikutku.Models.twitter;
using ikutku.Models.user;

namespace ikutku.DB.extension
{
    public static class UserExtensions
    {
        public static User ToModel(this cachedUser row)
        {
            return new User()
                {
                    twitterUserid = row.twitterid,
                    screenName = row.screenName,
                    lastTweet = row.lastTweet,
                    ratio = row.ratio,
                    profileImageUrl = row.profileImageUrl,
                    followers = row.followersCount,
                    followings = row.followingsCount
                };
        }

        public static Progress GetFollowingsProgressModel(this user row, string[] queuedFollowingsUserIDs)
        {
            var progress = new Progress
                {
                    UserCached =
                        !row.uncachedFollowingTotal.HasValue
                            ? 0
                            : (row.uncachedFollowingTotal.Value - row.uncachedFollowingCount.Value),
                    TotalUserCache = row.uncachedFollowingTotal.HasValue ? row.uncachedFollowingTotal.Value : 0,
                    AuthFailure = row.authFailCount,
                    Completed = row.uncachedCount == 0 && !queuedFollowingsUserIDs.Contains(row.id),
                    PositionInQueue = queuedFollowingsUserIDs.GetQueuePosition(x => x == row.id)
                };

            progress.EstimateRebuildTime();

            return progress;
        }

        private static string GetQueuePosition(this string[] ids, Predicate<string> indexMatch)
        {
            var pos = Array.FindIndex(ids, indexMatch);

            if (pos == 0)
            {
                return StringsResource.QUEUE_PROCESSING;
            }

            if (pos == -1)
            {
                return "-";
            }

            return pos.ToString();
        }

        public static DiffProgress GetDiffProgressModel(this user row, string[] queuedUserIDs)
        {
            

            var progress = new DiffProgress
                {
                    Followers = (row.followingCountSync ?? 0) + (row.followerCountSync ?? 0),
                    TotalFollowers = (row.followerCountTotal ?? 0) + (row.followingCountTotal ?? 0),
                    UserCached =
                        !row.uncachedTotal.HasValue
                            ? 0
                            : (row.uncachedTotal.Value - row.uncachedCount.Value),
                    TotalUserCache = row.uncachedTotal.HasValue ? row.uncachedTotal.Value : 0,
                    UserLists = row.usersLists.Count(x => x.listCursor.HasValue && x.listCursor.Value == 0),
                    TotalUserLists = row.usersLists.Count,
                    AuthFailure = row.authFailCount,
                    Completed = row.uncachedCount.HasValue && row.uncachedCount == 0 && (row.settings & (long)Settings.RESET) == 0,
                    PositionInQueue = queuedUserIDs.GetQueuePosition(x => x == row.id),
                    NextRefresh = "-"
                };

            if (row.apiNextRetry != null)
            {
                var duration = row.apiNextRetry.Value.FromUnixTime() - DateTime.UtcNow;
                progress.NextRefresh = Progress.GetDisplayString(duration);
            }

            progress.EstimateRebuildTime();

            return progress;
        }

        public static IEnumerable<User> ToModel(this IQueryable<cachedUser> rows)
        {
            foreach (var row in rows)
            {
                yield return row.ToModel();
            }
        }

        public static IEnumerable<cachedUser> ToDbModel(this IEnumerable<LinqToTwitter.User> rows)
        {
            foreach (var row in rows)
            {
                yield return new cachedUser(row);
            }
        }

        public static IEnumerable<AuthInfo> ToAuthInfo(this IEnumerable<user> rows)
        {
            foreach (var row in rows)
            {
                yield return row.ToAuthInfo();
            }
        }

        public static string ClearAuthTokensAndExecuteFormsSignOut(this user row, TwitterErrorCode errorcode)
        {
            string message = null;

            switch (errorcode)
            {
                case TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS:
                    message = "You were signed out because your token was invalid or has expired. Please sign in again.";
                    if (row != null)
                    {
                        row.authFailCount = 0; // because we auto logout the person on auth failure
                        row.apiNextRetry = null;
                        row.oauthSecret = null;
                        row.oauthToken = null;
                    }
                    break;
                case TwitterErrorCode.RATE_LIMIT_EXCEEDED:
                    message = "We are currently unable to sign you in. Please try again after 15 minutes.";
                    break;
                case TwitterErrorCode.NO_ERROR:
                    // do nothing
                    break;
                default:
                    message = "Something went wrong while trying to sign you in. Please try again in a few minutes.";
                    Syslog.Write("Errcode:{0}", errorcode);
                    break;
            }

            HttpContext.Current.SignOut();

            return message;
        }

        public static AuthInfo ToAuthInfo(this user row)
        {
            return new AuthInfo()
            {
                oauth_token = row.oauthToken,
                secret_token = row.oauthSecret,

                twitterUserid = row.id,
                twitterUsername = row.username,

                settings = (Settings)row.settings
            };
        }
    }
}
