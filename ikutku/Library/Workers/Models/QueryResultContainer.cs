using LinqToTwitter;

namespace ikutku.Library.Workers.Models
{
    public class QueryResultContainer
    {
        public User User { get; set; }
        public string IdOrName { get; set; }
        public bool Valid { get; set; }


        public QueryResultContainer(User usr)
        {
            User = usr;
            Valid = true;
        }

        public QueryResultContainer(string id)
        {
            IdOrName = id;
            Valid = false;
        }
    }
}