using System;
using System.Linq;
using clearpixels.Logging;
using ikutku.DB;

namespace ikutku.Library.Workers.Helpers
{
    public static class DbWorkerHelper
    {
        public static int AddFollowersByIdFromDatabase(this string[] twitterids, string ownerid, bool isFollower)
        {
            try
            {
                using (var scoped = new UnitOfWork(false))
                {
                    if (isFollower)
                    {
                        var inserts =
                        twitterids.Select(
                            x => new follower { twitterid = x, ownerid = ownerid });

                        scoped.Repository<follower>().BulkInsert(inserts, 1000);
                    }
                    else
                    {
                        var inserts =
                        twitterids.Select(
                            x => new following { twitterid = x, ownerid = ownerid });

                        scoped.Repository<following>().BulkInsert(inserts, 1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Syslog.Write(ex);
                return 0;
            }

            return twitterids.Length;
        }

        public static int RemoveFollowersByIdFromDatabase(this string[] twitterids, string ownerid, bool removeFollowings)
        {
            //int affectedRows;
            using (var scoped = new UnitOfWork(false))
            {
                var followers =
                    scoped.Repository<follower>()
                          .FindAll(x => x.ownerid == ownerid && twitterids.Contains(x.twitterid));
                scoped.Repository<follower>().DeleteRange(followers);

                try
                {
                    scoped.SaveChanges();
                }
                catch (Exception ex)
                {
                    Syslog.Write(ex);
                    return 0;
                }

                if (removeFollowings)
                {
                    var followings =
                    scoped.Repository<following>()
                          .FindAll(x => x.ownerid == ownerid && twitterids.Contains(x.twitterid));

                    scoped.Repository<following>().DeleteRange(followings);

                    try
                    {
                        scoped.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        Syslog.Write(ex);
                    }
                }

                var usersInList =
                    scoped.Repository<usersInList>()
                          .FindAll(x => x.ownerid == ownerid && twitterids.Contains(x.twitterid));
                scoped.Repository<usersInList>().DeleteRange(usersInList);

                try
                {
                    scoped.SaveChanges();
                }
                catch (Exception ex)
                {
                    Syslog.Write(ex);
                }
            }

            return twitterids.Length;
        }
    }
}