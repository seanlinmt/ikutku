using System.Collections.Generic;

namespace ikutku.ViewModels.action
{
    public class UserActionViewModel
    {
        public string id { get; set; }
        public string screenName { get; set; }
        public List<UserListMemberActionViewModel> lists { get; set; }

        public UserActionViewModel()
        {
            lists = new List<UserListMemberActionViewModel>();
        }
    }
}