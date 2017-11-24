using System.Collections.Generic;
using System.Web.Mvc;
using ikutku.Models.user.lists;

namespace ikutku.ViewModels
{
    public class UserListViewModel
    {
        public IEnumerable<UserList> lists { get; set; }
        public IEnumerable<SelectListItem> displaySettings { get; set; }

        public UserListViewModel(bool hideExcluded)
        {
            displaySettings = new[]
                                  {
                                      new SelectListItem()
                                          {
                                              Text = "Show excluded people as greyed out entries",
                                              Value = "False",
                                              Selected = hideExcluded
                                          },
                                      new SelectListItem()
                                          {
                                              Text = "Don't show excluded people in results",
                                              Value = "True",
                                              Selected = hideExcluded
                                          },
                                  };
        }
    }
}