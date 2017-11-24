namespace ikutku.Models.user.followers
{
    public class FollowersListing
    {
        public User[] followers { get; set; }
        public bool hasMore { get; set; }
        public bool showHeader { get; set; }
        public int count { get; set; }
        public int page { get; set; }
    }
}
