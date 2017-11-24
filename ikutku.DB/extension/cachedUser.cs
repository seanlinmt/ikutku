using System;
using System.Diagnostics;
using ikutku.Models.user;

namespace ikutku.DB
{
    public partial class cachedUser
    {
        public cachedUser(LinqToTwitter.User usr)
        {
            Debug.Assert(!string.IsNullOrEmpty(usr.Identifier.ScreenName) && !string.IsNullOrEmpty(usr.Identifier.ID));

            twitterid = usr.Identifier.ID;
            screenName = usr.Identifier.ScreenName;
            profileImageUrl = usr.ProfileImageUrl;
            followersCount = usr.FollowersCount;
            followingsCount = usr.FriendsCount;
            if (!string.IsNullOrEmpty(usr.Status.StatusID))
            {
                lastTweet = usr.Status.CreatedAt;
            }

            ratio = User.CalculateRatio(followersCount, followingsCount);
            updated = DateTime.UtcNow;
        }

    }
}