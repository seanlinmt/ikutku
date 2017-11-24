using System;
using LinqToTwitter;

namespace ikutku.Library.Workers
{
    public abstract class TwitterWorkerBase
    {
        public const int TASK_TIMEOUT = 60000;
        public const int ENTRIES_PER_PAGE = 5000;
        private const int RESOLVE_MAX_RETRIES = 3;

#if DEBUG
        public const double TWITTER_API_WAIT_SECONDS = 60;
#else
        public const double TWITTER_API_WAIT_SECONDS = 900;
#endif

        protected TwitterContext _twitterContext { get; set; }
        
        public string GetLastUrl()
        {
            return _twitterContext.LastUrl;
        }
        
        public void Dispose()
        {
            if (_twitterContext != null)
            {
                _twitterContext.Dispose();
            }
        }
         
    }
}