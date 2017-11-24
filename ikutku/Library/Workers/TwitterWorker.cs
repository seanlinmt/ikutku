using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using clearpixels.Helpers.debugging;
using LinqToTwitter;
using ikutku.Constants;
using clearpixels.Logging;
using ikutku.Models.user;
using User = LinqToTwitter.User;

namespace ikutku.Library.Workers
{
    // DO NOT USE SCREEN NAME TO QUERY TWITTER BECAUSE SCREEN NAME CHANGES
    // DO NOT USE SCREEN NAME TO QUERY TWITTER BECAUSE SCREEN NAME CHANGES
    // DO NOT USE SCREEN NAME TO QUERY TWITTER BECAUSE SCREEN NAME CHANGES
        
    public sealed class TwitterWorker : TwitterWorkerBase
    {
        private readonly string _screenName;
        private readonly string _twitterid;

        private TwitterWorker(bool applicationOnly)
        {
            _twitterContext = GetIkutkuService(applicationOnly);
            _screenName = General.IKUTKU_SCREENNAME;
            _twitterid = General.IKUTKU_USERID;
            _twitterContext.Log = Console.Out;
        }

        public TwitterWorker(AuthInfo auth)
            : this(new TwitterContext(new SingleUserAuthorizer()
                {
                    Credentials = auth.ToCredentials()
                }), auth)
        {
            
        }

        public TwitterWorker(TwitterContext ctx)
        {
            _twitterContext = ctx;
            _twitterContext.Log = new DebugTextWriter();
        }

        private TwitterWorker(TwitterContext ctx, AuthInfo auth) 
        {
            Debug.Assert(!string.IsNullOrEmpty(auth.twitterUserid) &&
                !string.IsNullOrEmpty(auth.twitterUsername));

            _screenName = auth.twitterUsername;
            _twitterid = auth.twitterUserid;

            _twitterContext = ctx;
            _twitterContext.Log = new DebugTextWriter();
        }

        public static TwitterWorker GetIkutkuWorker(bool applicationOnly)
        {
            return new TwitterWorker(applicationOnly);
        }

        /*
        public User[] ListUserDIffv1(ConcurrentDictionary<long, User> cache, int pageno, int row, string method, string dir, bool hide_excluded)
        {
            var usrs = cache.Values.Where(x => !x.invalid).AsQueryable();

            if (hide_excluded)
            {
                usrs = usrs.Where(x => !x.excluded);
            }

            // the default fast way
            if (string.IsNullOrEmpty(method) ||
                (method == "follow" && dir == "desc"))
            {
                // default order

                var temp = usrs.Skip(pageno * row).Take(row);
                ResolveUserProfiles(temp, cache);
                return temp.OrderBy(y => y.order).ToArray();
            }

            // slow slow
            ResolveUserProfiles(cache);

            switch (method)
            {
                case "follow":
                    if (dir == "asc")
                    {
                        usrs = usrs.OrderByDescending(y => y.order);
                    }
                    else
                    {
                        throw new Exception("should not not get here");
                    }
                    break;
                case "ratio":
                    if (dir == "asc")
                    {
                        usrs = usrs.OrderBy(y => y.ratio);
                    }
                    else
                    {
                        usrs = usrs.OrderByDescending(y => y.ratio);
                    }
                    break;
                case "activity":
                    if (dir == "asc")
                    {
                        usrs = usrs.OrderBy(y => y.lastTweet);
                    }
                    else
                    {
                        usrs = usrs.OrderByDescending(y => y.lastTweet);
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }

            return usrs.Skip(row * pageno).Take(row).ToArray();
        }

        */

        public int[] GetApiStats()
        {
            return new[] 
                {
                    _twitterContext.MediaRateLimitRemaining, 
                    _twitterContext.RateLimitRemaining,
                    _twitterContext.RateLimitCurrent, 
                    _twitterContext.MediaRateLimitReset,
                    _twitterContext.RateLimitReset
                };
        }
        
        public void FollowBothParties(string twitteruserid)
        {
            try
            {
                GetIkutkuService(false).CreateFriendship(twitteruserid, null, false);
            }
            catch (Exception ex)
            {
                Syslog.Write(ex);
            }

            try
            {
                _twitterContext.CreateFriendship(General.IKUTKU_USERID, null, false);
            }
            catch (Exception ex)
            {
                Syslog.Write(ex);
            }
        }

        public User FollowByTwitterUserId(string twitterUserid)
        {
            return _twitterContext.CreateFriendship(twitterUserid, null, false);
        }

        public User UnfollowByTwitterId(string twitterUserid)
        {
            return _twitterContext.DestroyFriendship(twitterUserid, null);
        }

        public bool IsFollowing(string twitterUserid)
        {
            var friendship = _twitterContext.Friendship.First(x => x.Type == FriendshipType.Show &&
                                                                  x.SourceUserID == _twitterid &&
                                                                  x.TargetUserID == twitterUserid);

            return friendship.TargetRelationship.FollowedBy;
        }

        public void ListAddUser(string twitteruserid, string listid)
        {
            _twitterContext.AddMemberToList(twitteruserid, null, listid, null,
                                    _twitterid, null);
        }

        public void ListDeleteUser(string twitteruserid, string listid)
        {
            _twitterContext.DeleteMemberFromList(twitteruserid, null, listid, null,
                                                    _twitterid, null);
        }

        public User GetUserById(string twitterid)
        {
            return _twitterContext.User.SingleOrDefault(u => u.Type == UserType.Show && u.UserID == twitterid);
        }

        public List<User> GetUsersById(string[] ids)
        {
            if (ids.Length == 0)
            {
                return new List<User>();
            }
            var idstring = string.Join(",", ids);

            return _twitterContext.User.Where(x => x.Type == UserType.Lookup && x.UserID == idstring).ToList();
        }

        public List CreateList(string listName)
        {
            List list = null;

            try
            {
                list = _twitterContext.CreateList(listName, "private", "");
            }
            catch (Exception ex)
            {
                Syslog.Write(ex);
            }

            return list;
        }

        public SocialGraph GetFollowers(string cursor)
        {
            return _twitterContext.SocialGraph.SingleOrDefault(x => x.Type == SocialGraphType.Followers &&
                                                                 x.ScreenName == _screenName &&
                                                                 x.Cursor == cursor &&
                                                                 x.Count == ENTRIES_PER_PAGE);
        }

        public SocialGraph GetFriends(string cursor)
        {
            return _twitterContext.SocialGraph.SingleOrDefault(x => x.Type == SocialGraphType.Friends &&
                                                                 x.ScreenName == _screenName &&
                                                                 x.Cursor == cursor &&
                                                                 x.Count == ENTRIES_PER_PAGE);
        }

        public List GetList(string listid)
        {
            return _twitterContext.List.Single(x => x.Type == ListType.Show && x.ListID == listid);
        }

        public List<List> GetLists()
        {
            var lists = _twitterContext.List.Where(x =>
                                             x.Type == ListType.Ownerships &&
                                             x.ScreenName == _screenName &&
                                             x.Count == 1000)
                                 .ToList();

            if (lists.Any(x => x.CursorMovement.Next != "0"))
            {
                throw new NotSupportedException("More than 1000 lists");
            }

            return lists;
        }

        public List GetListMembers(string listid, string cursor)
        {
            return _twitterContext.List.FirstOrDefault(x => x.Type == ListType.Members &&
                                                      x.ListID == listid &&
                                                      x.OwnerScreenName == _screenName &&
                                                      x.Cursor == cursor &&
                                                      x.SkipStatus == true);
        }

        public void DeleteList(string listid)
        {
            _twitterContext.DeleteList(listid, null, _twitterid, null);
        }

        public void SendDirectMessage(string twitterUserId, string message)
        {
            _twitterContext.NewDirectMessage(twitterUserId, message);
        }

        public List UpdateList(string listid, string listname)
        {
            List list = null;
            try
            {
                list = _twitterContext.UpdateList(listid, null, listname, null, null, null, null);
            }
            catch (Exception ex)
            {
                Syslog.Write(ex);
            }

            return list;
        }

        public Account VerifyCredentials()
        {
            return _twitterContext.Account.Single(x => x.Type == AccountType.VerifyCredentials &&
                x.SkipStatus == true &&
                x.IncludeEntities == false);
        }

        private TwitterContext GetIkutkuService(bool applicationOnly)
        {
            if (applicationOnly)
            {
                // https://dev.twitter.com/docs/auth/application-only-auth
                var auth = new ApplicationOnlyAuthorizer()
                {
                    Credentials = new InMemoryCredentials()
                    {
                        ConsumerKey = General.OAUTH_CONSUMER_KEY,
                        ConsumerSecret = General.OAUTH_CONSUMER_SECRET
                    }
                };

                auth.Authorize();
                return new TwitterContext(auth);
            }

#if DEBUG
            return new TwitterContext(new SingleUserAuthorizer()
                {
                    Credentials = new SingleUserInMemoryCredentials()
                        {
                            ConsumerKey = General.OAUTH_CONSUMER_KEY,
                            ConsumerSecret = General.OAUTH_CONSUMER_SECRET,
                            OAuthToken = "441515800-TItmzvFHJB7o8UaMhOmIdLCUyL36l55wtzv4j4bl",
                            AccessToken = "TCmueb5kHJanYN8XXgSymeDsynz8wyFJI5y3OaQM"
                        }
                });
#else
                return new TwitterContext(new SingleUserAuthorizer()
                {
                    Credentials = new SingleUserInMemoryCredentials()
                    {
                        ConsumerKey = General.OAUTH_CONSUMER_KEY,
                        ConsumerSecret = General.OAUTH_CONSUMER_SECRET,
                        OAuthToken = "441515800-cVyX9QeHSgSkoUn2M8b420GSDROpiE103oSVq5X4",
                        AccessToken = "MjnoHwgcooCRCWSd67yk0e8gEvUeIxXKtw9JF8nzgU"
                    }
                });
#endif
        }

    }
}