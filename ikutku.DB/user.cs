//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ikutku.DB
{
    using System;
    using System.Collections.Generic;
    
    public partial class user
    {
        public user()
        {
            this.followers = new HashSet<follower>();
            this.followings = new HashSet<following>();
            this.loginIntervals = new HashSet<loginInterval>();
            this.queuedFollowingUsers = new HashSet<queuedFollowingUser>();
            this.queuedUsers = new HashSet<queuedUser>();
            this.usersLists = new HashSet<usersList>();
            this.usersInLists = new HashSet<usersInList>();
        }
    
        public string id { get; set; }
        public string username { get; set; }
        public Nullable<System.DateTime> startTime { get; set; }
        public long settings { get; set; }
        public string photoUrl { get; set; }
        public System.DateTime updated { get; set; }
        public string oauthToken { get; set; }
        public string oauthSecret { get; set; }
        public Nullable<long> followersCursor { get; set; }
        public Nullable<long> followingsCursor { get; set; }
        public Nullable<long> lastRebuildDuration { get; set; }
        public Nullable<int> followerCountSync { get; set; }
        public Nullable<int> followerCountTotal { get; set; }
        public Nullable<int> followingCountSync { get; set; }
        public Nullable<int> followingCountTotal { get; set; }
        public Nullable<int> uncachedTotal { get; set; }
        public Nullable<int> uncachedCount { get; set; }
        public Nullable<int> uncachedFollowingTotal { get; set; }
        public Nullable<int> uncachedFollowingCount { get; set; }
        public Nullable<int> userlistCount { get; set; }
        public Nullable<System.DateTime> lastLogin { get; set; }
        public int authFailCount { get; set; }
        public Nullable<double> apiNextRetry { get; set; }
    
        public virtual ICollection<follower> followers { get; set; }
        public virtual ICollection<following> followings { get; set; }
        public virtual ICollection<loginInterval> loginIntervals { get; set; }
        public virtual ICollection<queuedFollowingUser> queuedFollowingUsers { get; set; }
        public virtual ICollection<queuedUser> queuedUsers { get; set; }
        public virtual ICollection<usersList> usersLists { get; set; }
        public virtual ICollection<usersInList> usersInLists { get; set; }
    }
}
