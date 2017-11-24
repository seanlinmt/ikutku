using System.Diagnostics;
using LinqToTwitter;
using ikutku.Constants;

namespace ikutku.Models.user
{
    public class AuthInfo
    {
        public string oauth_token { get; set; }
        public string secret_token { get; set; }

        public string twitterUserid { get; set; }
        public string twitterUsername { get; set; }

        public Settings settings { get; set; }
    }

    public static class TwitterAuthInfoHelper
    {
        public static SingleUserInMemoryCredentials ToCredentials(this AuthInfo row)
        {
            Debug.Assert(!string.IsNullOrEmpty(row.secret_token) && 
                !string.IsNullOrEmpty(row.oauth_token));

            return new SingleUserInMemoryCredentials()
                {
                    ConsumerKey = General.OAUTH_CONSUMER_KEY,
                    ConsumerSecret = General.OAUTH_CONSUMER_SECRET,
                    AccessToken = row.secret_token,
                    OAuthToken = row.oauth_token
                };
        }
    }
}