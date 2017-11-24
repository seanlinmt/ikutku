using System;
using System.Collections.Generic;
using System.Linq;

namespace ikutku.Models.user.lists
{
    public class UserList : IEquatable<UserList>
    {
        public string id { get; set; }
        public string slug { get; set; }
        public bool exclude { get; set; }

        public bool isNew { get; set; }
        public int memberCount { get; set; }
        public string name { get; set; }
        
        public IEnumerable<long> twitterids { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as UserList);
        }

        public bool Equals(UserList other)
        {
            return other != null && other.slug == slug;
        }

        public override int GetHashCode()
        {
            return slug.GetHashCode();
        }

        public UserList()
        {
            twitterids = Enumerable.Empty<long>();
        }

    }
}