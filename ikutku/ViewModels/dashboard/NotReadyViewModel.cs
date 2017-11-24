using ikutku.Models.sync;

namespace ikutku.ViewModels.dashboard
{
    public class NotReadyViewModel
    {
        public DiffProgress diffProgress { get; set; }
        public Progress followingProgress { get; set; }
        public bool NotificationOff { get; set; }
    }
}