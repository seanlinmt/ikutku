using ikutku.Models.queue;

namespace ikutku.Models.user
{
    public class QueueInfo
    {
        public AuthInfo auth { get; set; }
        public QueueSettings settings { get; set; }
        public bool reset { get; set; }
    }
}
