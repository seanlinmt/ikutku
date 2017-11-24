using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Transactions;
using System.Web.Mvc;
using clearpixels.Helpers;
using clearpixels.Helpers.database;
using clearpixels.Helpers.datetime;
using clearpixels.Logging;
using ikutku.Constants;
using ikutku.DB;
using ikutku.DB.extension;
using ikutku.Library.Workers;
using ikutku.Models.queue;
using ikutku.Models.user;

namespace ikutku.Controllers
{
    public class testController : baseController
    {
#if DEBUG

        private readonly IUnitOfWork _unitOfWork;

        public testController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        protected override void Dispose(bool disposing)
        {
            _unitOfWork.Dispose();
            base.Dispose(disposing);
        }

        public ActionResult TestMath()
        {
            int? a = null;
            int? b = 1;
            return Content((a + b).ToString());
        }

        public ActionResult TestException()
        {
            try
            {
                throw new NotImplementedException("new exception");
            }
            catch (Exception ex)
            {

                Syslog.Write(ex);
            }
            try
            {
                throw new NotImplementedException("new exception");
            }
            catch (Exception ex)
            {

                Syslog.Write(ex," with a message");
            }
            try
            {
                throw new NotImplementedException("new exception");
            }
            catch (Exception ex)
            {

                Syslog.Write(ex, " with a message and {0}", "parameters");
            }
            try
            {
                throw new NotImplementedException("new exception");
            }
            catch (Exception)
            {
                Syslog.Write("Just a message with {0}", "parameters");
            }
            
            return Content("done");
        }

        public ActionResult TestGetList(string id)
        {
            var auth = Thread.CurrentPrincipal.ToAuthInfo();

            var service = new TwitterWorker(auth);

            var list = service.GetList(id);

            return Content(list.Name);
        }

        public ActionResult TestSQL()
        {
            var auth = Thread.CurrentPrincipal.ToAuthInfo();
            if (auth == null)
            {
                return Content("not signed in");
            }

            var ids = new[] {"2", "1", "3", "4", "5", "6", "7"};
            _unitOfWork.Repository<cachedUser>().DeleteAll("twitterid", ids);
            _unitOfWork.Repository<following>().DeleteAll("twitterid", "7");
            _unitOfWork.SaveChanges();

            // add 5,6,7
            _unitOfWork.Repository<cachedUser>().Insert(new cachedUser() { twitterid = "5", screenName = "", profileImageUrl = "", ratio = 0, updated = DateTime.Now});
           
            _unitOfWork.Repository<cachedUser>().Insert(new cachedUser() { twitterid = "7", screenName = "", profileImageUrl = "", ratio = 0, updated = DateTime.Now });

            _unitOfWork.Repository<following>().Insert(new following() {ownerid = auth.twitterUserid, twitterid = "7"});

            var no6 = new cachedUser() { twitterid = "6", screenName = "", profileImageUrl = "", ratio = 0, updated = DateTime.Now };
            _unitOfWork.Repository<cachedUser>().Insert(no6);

            _unitOfWork.SaveChanges();

            using (var u = new UnitOfWork())
            {
                try
                {
                    using (var scope = TransactionHelper.CreateTransactionScope(TransactionScopeOption.Suppress))
                    {
                        // containsDuplicate
                        var oneTwoFive = new[]
                            {
                                new cachedUser() { twitterid = "2", screenName = "", profileImageUrl = "", ratio = 0, updated = DateTime.Now }
                            , new cachedUser() { twitterid = "5", screenName = "", profileImageUrl = "", ratio = 0, updated = DateTime.Now }
                            ,new cachedUser() { twitterid = "1", screenName = "", profileImageUrl = "", ratio = 0, updated = DateTime.Now }
                            };
                        var threeFour = new[]
                            {
                                new cachedUser() { twitterid = "3", screenName = "", profileImageUrl = "", ratio = 0, updated = DateTime.Now },
                                new cachedUser() { twitterid = "4", screenName = "", profileImageUrl = "", ratio = 0, updated = DateTime.Now }
                            };

                        u.Repository<cachedUser>().BulkInsert(threeFour, 10);
                        //u.Repository<cachedUser>().BulkInsert(oneTwoFive, 10);
                        
                        //u.Repository<cachedUser>().DeleteAll("id", "5");
                        //u.Repository<cachedUser>().Delete(no6.id);
                        
                        var working = u.Repository<cachedUser>().FindAll(x => ids.Contains(x.twitterid))
                                       .Take(3);

                        u.Repository<cachedUser>().DeleteRange(x => ids.Contains(x.twitterid));
                        u.SaveChanges(false);

                        scope.Complete();
                    }
                }
                catch (Exception ex)
                {
                    Syslog.Write(ex);
                }
            }
                    
            

            return
                Content("done");
        }

        public ActionResult CompareActions()
        {
            var auth = Thread.CurrentPrincipal.ToAuthInfo();

            var builder = new DatabaseWorker(auth);

            Action a = () => builder.DebugMethod1(-1);
            Action b = () => builder.DebugMethod1(-1);
            Action c = () => builder.DebugMethod2();

            if (a.Method.Name == b.Method.Name)
            {
                return Content("match");
            }

            return Content("diff");
        }

        public ActionResult TestWorkerTimers()
        {

            var usrs = _unitOfWork.Repository<user>().FindAll();

            foreach (var usr in usrs)
            {
                usr.AddToDiffQueue(QueueSettings.ALL_BUILDS_USER.ToInt());
                _unitOfWork.SaveChanges();
                var builder = new DatabaseWorker(usr.ToAuthInfo(), QueueSettings.ALL_BUILDS_USER);
                try
                {
                    builder.StartTest();
                }
                catch (Exception ex)
                {
                    Syslog.Write(ex);
                }
            }

            return Content("done. now we wait");

        }


        public ActionResult TestContextMemoryLeak()
        {
            for (int i = 0; i < 1000; i++)
            {
                using (var scoped = new UnitOfWork())
                {
                    var cachedUsers = scoped.Repository<cachedUser>().FindAll().ToArray();
                }
            }

            return Content("done");
        }
#endif
    }
}
