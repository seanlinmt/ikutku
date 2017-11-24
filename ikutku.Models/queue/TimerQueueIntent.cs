namespace ikutku.Models.queue
{
    public class TimerQueueIntent
    {
        public string Reason { get; set; }
        public double SecondsToWait { get; set; }

        public TimerQueueIntent(string reason, double secondsToWait)
        {
            Reason = reason;
            SecondsToWait = secondsToWait;
        }
    }
}
