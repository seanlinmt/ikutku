using System;
using ikutku.Models.user;
using ikutku.Models.user.lists;

namespace ikutku.DB
{

    public partial class usersList
    {
        public UserList ToModel()
        {
            var model = new UserList();

            model.id = id;
            model.slug = slug;
            model.exclude = exclude;
            model.name = listname;

            return model;
        }


        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj != null && ((usersList)obj).id == id;
        }
    }
}