using System;

namespace ikutku.Models.sync
{
    public class Progress
    {
        public int UserCached { get; set; }
        public int TotalUserCache { get; set; }
        public string PositionInQueue { get; set; }
        public string NextRefresh { get; set; }
        
        public int AuthFailure { get; set; }
        public string TimeLeft { get; set; }
        public bool Completed { get; set; }

        public void EstimateRebuildTime()
        {
            var minutes = Math.Max((TotalUserCache - UserCached) * 15 / 18000, 1);

            var estimate = new TimeSpan(0, minutes, 0);

            TimeLeft = GetDisplayString(estimate);
        }

        public static string GetDisplayString(TimeSpan timespan)
        {
            if (timespan.Hours != 0)
            {
                if (timespan.Hours == 1)
                {
                    return "an hour.";
                }
                return string.Format("{0} hours.", timespan.Hours);
            }

            if (timespan.Minutes != 0)
            {
                if (timespan.Minutes == 1)
                {
                    return "a minute.";
                }
                return string.Format("{0} minutes.", timespan.Minutes);
            }

            if (timespan.Seconds == 1)
            {
                return "a second.";
            }
            return string.Format("{0} seconds.", timespan.Seconds);
        }

    }
}
