using System.Collections.Generic;
using System.Linq;

namespace ikutku.Models.user
{
    public class UserContentViewModel
    {
        public IEnumerable<User> people { get; set; }
        public bool showHeader { get; set; }
        public int count { get; set; }

        public bool hasMore { get; set; }

        public UserContentViewModel()
        {
            people = Enumerable.Empty<User>();
        }
    }
}