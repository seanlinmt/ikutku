using System;

namespace ikutku.Models.user
{
    public class User : IEquatable<User>
    {
        const string DATEFORMAT_STANDARD = "dd MMM yyyy";

        public string lastTweetDateString { get
        {
            if (lastTweet.HasValue)
            {
                return string.Format("last tweet: {0}", lastTweet.Value.ToString(DATEFORMAT_STANDARD));
            }
            return "<span class='font_red'>private</span>";
        }
        }
        
        public bool @protected { get { return !lastTweet.HasValue; } }
        public string twitterUserid { get; set; }
        public string screenName { get; set; }

        public DateTime? lastTweet { get; set; }
        public decimal ratio { get; set; }
        public int followings { get; set; }
        public int followers { get; set; }
        public bool excluded { get; set; }

        public string profileImageUrl { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as User);
        }

        public bool Equals(User other)
        {
            return other != null && other.twitterUserid == twitterUserid;
        }

        public override int GetHashCode()
        {
            return twitterUserid.GetHashCode();
        }

        public static decimal CalculateRatio(int followercount, int friendscount)
        {
            if (friendscount < 1)
            {
                return followercount;
            }

            if (followercount < 1)
            {
                return 0;
            }
            
            return Decimal.Round((decimal)(1f * followercount / friendscount), 2);
        }
    }
}