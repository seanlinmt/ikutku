namespace ikutku.Constants
{
    public static class General
    {
#if DEBUG
        public const int AUTH_MAX_FAILURES = 5;
#else
        public const int AUTH_MAX_FAILURES = 10;
#endif

        public const int DB_COMMAND_TIMEOUT = 180; // seconds

        public const int FORMS_AUTH_VERSION = 4;

        public const string IKUTKU_SCREENNAME = "ikutku";
        public const string IKUTKU_USERID = "441515800";

        // database
        public const int DB_CACHERESOLVER_WORKINGSIZE = 1500; // limit is 1200 but we add extra to trigger api limit
        public const int DB_CACHE_VALID_DAYS = 3; // when entries become stale
        public const int DB_DELETE_BATCHSIZE = 100;
#if DEBUG
        public const int DB_ACCOUNT_VALID_DAYS = 5;
#else
        public const int DB_ACCOUNT_VALID_DAYS = 14;
#endif
        public const string SESSION_ERRORMESSAGE = "SessionErrorMessage";

#if DEBUG
        public const string IKUTKU_DOMAIN_URI = "http://localhost:8888";
        public const string OAUTH_CONSUMER_KEY = "OAUTH_CONSUMER_KEY";
        public const string OAUTH_CONSUMER_SECRET = "OAUTH_CONSUMER_SECRET";
#else
        public const string IKUTKU_DOMAIN_URI = "http://ikutku.com";
        public const string OAUTH_CONSUMER_KEY = "OAUTH_CONSUMER_KEY";
        public const string OAUTH_CONSUMER_SECRET = "OAUTH_CONSUMER_SECRET";
#endif
        public const string HTTP_CACHEURL = IKUTKU_DOMAIN_URI + "/dummy";
        public const string OAUTH_CALLBACK_URL = IKUTKU_DOMAIN_URI + "/login";
    }
}
