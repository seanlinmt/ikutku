using System;
using System.Linq;
using System.Threading;
using System.Web.Http;
using LinqToTwitter;
using clearpixels.Helpers;
using clearpixels.Logging;
using ikutku.DB;
using ikutku.Library.Workers;
using ikutku.Models.twitter;
using ikutku.Models.user;
using ikutku.Models.user.lists;
using Settings = ikutku.Models.user.Settings;

namespace ikutku.Controllers.API
{
    public class ListsController : baseApiController
    {
        public ListsController(IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
        }

        // adds follower to list
        [HttpPost]
        [ActionName("followers")]
        public bool PostByFollowers(string listid, string followerid)
        {
            var auth = Thread.CurrentPrincipal.ToAuthInfo();
            var service = new TwitterWorker(auth);
            try
            {
                service.ListAddUser(followerid, listid);

                // add entry
                var list = _unitOfWork.Repository<usersList>().FindOne(x => x.id == listid);
                if (list != null)
                {
                    var existing = list.usersInLists.SingleOrDefault(x => x.twitterid == followerid);
                    if (existing == null)
                    {
                        var entry = new usersInList
                            {
                                twitterid = followerid,
                                userlistid = listid,
                                ownerid = auth.twitterUserid
                            };
                        _unitOfWork.Repository<usersInList>().Insert(entry);
                        list.memberCount++;
                        _unitOfWork.SaveChanges();
                    }
                }
            }
            catch (TwitterQueryException ex)
            {
                Syslog.Write(ex, "{0} {1}", ex.ErrorCode, ex.Message);
            }
           

            return true;
        }

        [HttpDelete]
        [ActionName("followers")]
        public bool DeleteByFollowers(string listid, string followerid)
        {
            var auth = Thread.CurrentPrincipal.ToAuthInfo();
            var service = new TwitterWorker(auth);

            try
            {
                service.ListDeleteUser(followerid, listid);
            }
            catch (TwitterQueryException ex)
            {
                var errorcode = ex.ErrorCode.ToEnum<TwitterErrorCode>();
                switch (errorcode)
                {
                    case TwitterErrorCode.LIST_NOT_A_MEMBER:
                        break;
                    default:
                        Syslog.Write(ex, "{0} {1}", ex.ErrorCode, ex.Message);
                        break;
                }
            }

            var list = _unitOfWork.Repository<usersList>().FindOne(x => x.id == listid);
            if (list != null)
            {
                var follower = list.usersInLists.SingleOrDefault(x => x.twitterid == followerid);

                if (follower != null)
                {
                    _unitOfWork.Repository<usersInList>().Delete(follower);
                    list.memberCount--;
                    _unitOfWork.SaveChanges();
                }
            }

            return true;
        }

        public bool DeleteById(string id)
        {
            var auth = Thread.CurrentPrincipal.ToAuthInfo();
            try
            {
                var existingList = _unitOfWork.Repository<usersList>().FindAll(x => x.id == id).First();
                var listid = existingList.id;

                var service = new TwitterWorker(auth);
                service.DeleteList(listid);
                
                existingList.user.userlistCount--;

                _unitOfWork.Repository<usersInList>().DeleteRange(existingList.usersInLists);
                _unitOfWork.Repository<usersList>().Delete(existingList);

                _unitOfWork.SaveChanges();
            }
            catch (Exception ex)
            {
                if (ex is TwitterQueryException)
                {
                    var tqex = ex as TwitterQueryException;
                    var errorcode = tqex.ErrorCode.ToEnum<TwitterErrorCode>();
                    switch (errorcode)
                    {
                        case TwitterErrorCode.OVERCAPACITY:
                            break;
                        default:
                            Syslog.Write(ex, "DeleteList: {0} {1}", tqex.ErrorCode, tqex.Message);
                            break;
                    }
                }
                else
                {
                    Syslog.Write(ex);
                }

                return false;
            }
            return true;
        }

        public string PutLists(ListUpdateJson update)
        {
            var auth = Thread.CurrentPrincipal.ToAuthInfo();
            if (update.lists == null)
            {
                return "Nothing to save";
            }

            // get user
            var existingUser = _unitOfWork.Repository<user>().FindOne(x => x.id == auth.twitterUserid);

            if (existingUser == null)
            {
                // should not get here
                return "Refresh: Unable to locate " + auth.twitterUsername;
            }

            var service = new TwitterWorker(auth);

            foreach (var row in update.lists)
            {
                
                if (!String.IsNullOrEmpty(row.id))
                {
                    // handle existing
                    var idSingle = row.id;
                    var statusSingle = row.status;
                    var listname = row.listname;

                    var existingList = existingUser.usersLists.Single(x => x.id == idSingle);

                    existingList.exclude = statusSingle;

                    // detect name changes
                    if (existingList.listname != listname)
                    {
                        var list = service.UpdateList(existingList.id, listname);
                        
                        if (list != null)
                        {
                            existingList.listname = listname;
                            existingList.slug = list.SlugResult;
                        }
                    }
                }
                else
                {
                    // handle new
                    var slugNew = row.listname;
                    var statusNew = row.liststatus;

                    var list = service.CreateList(slugNew);
                    if (list == null)
                    {
                        return "Failed to create new list: " + slugNew;
                    }

                    // add to local db
                    var entry = new usersList
                    {
                        listname = slugNew,
                        slug = list.SlugResult,
                        id = list.ListIDResult,
                        exclude = statusNew,
                        ownerid = auth.twitterUserid,
                        updated = DateTime.UtcNow,
                        listCursor = null
                    };
                    existingUser.usersLists.Add(entry);
                    existingUser.userlistCount++;
                }
            }


            // handle settings
            if (update.excluded_hide)
            {
                existingUser.settings |= (long)Settings.EXCLUDED_HIDE;
            }
            else
            {
                existingUser.settings &= ~((long)Settings.EXCLUDED_HIDE);
            }

            _unitOfWork.SaveChanges();

            return "Save successful";
        }

    }
}
