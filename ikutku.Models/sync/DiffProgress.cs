using System;

namespace ikutku.Models.sync
{
    public class DiffProgress : Progress
    {
        public int Followers { get; set; }
        public int TotalFollowers { get; set; }
        public int UserLists { get; set; }
        public int TotalUserLists { get; set; }

        public void EstimateTimeLeft(DateTime startTime, int startingTotal, int totalLeft)
        {
            if (startingTotal == 0)
            {
                // prevents divide by zero
                TimeLeft = " ...";
            }
            else
            {
                // timeleft = timeElapsed x startingTotal/(startingTotal-totalLeft)
                var duration = new TimeSpan((DateTime.UtcNow - startTime).Ticks * (startingTotal - totalLeft) / startingTotal);
                TimeLeft = GetDisplayString(duration);
            }
        }
    }
}
