using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web.Http;
using clearpixels.Helpers.generics;
using ikutku.Library.Workers.Helpers;
using LinqToTwitter;
using clearpixels.Helpers;
using clearpixels.Helpers.concurrency;
using clearpixels.Logging;
using clearpixels.Models;
using ikutku.DB;
using ikutku.Library.Workers;
using ikutku.Models.json;
using ikutku.Models.twitter;
using ikutku.Models.user;
using ikutku.Models.user.followers;
using Settings = ikutku.Models.user.Settings;

namespace ikutku.Controllers.API
{
    public class FollowersController : baseApiController
    {
        public FollowersController(IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
        }

        // unfollow
        public ApiResult DeleteById([FromBody] string[] id)
        {
            if (id == null)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            var auth = Thread.CurrentPrincipal.ToAuthInfo();

            var duplicates = id.GroupBy(x => x)
                               .Where(x => x.Count() > 1)
                               .ToDictionary(g => g.Key, g => g.Count());

            if (duplicates.Count != 0)
            {
                Syslog.Write("UnFollow Duplicates => {0}", string.Join(", ", duplicates.Select(x => string.Format("{0}:{1}", x.Key, x.Value))));
            }

            var batches = new HashSet<string>(id).InSetsOf(10);

            var errorModel = new ApiError();

            var parallel = new ParallelHelper(TwitterWorkerBase.TASK_TIMEOUT);
            var successes = parallel.ProcessData(batches, p =>
            {
                var service = new TwitterWorker(auth);
                var result = new List<IdName>();

                foreach (var entry in p)
                {
                    parallel.GetCancellationToken().ThrowIfCancellationRequested();
                
                    var twitterid = entry;
                    try
                    {
                        var unfollowed = service.UnfollowByTwitterId(twitterid);
                        result.Add(new IdName(unfollowed.Identifier.ID, unfollowed.Identifier.ScreenName));
                    }
                    catch (TwitterQueryException ex)
                    {
                        var errorcode = ex.ErrorCode.ToEnum<TwitterErrorCode>();
                        switch (errorcode)
                        {
                            case TwitterErrorCode.ACCESS_DENIED:
                            case TwitterErrorCode.ACCOUNT_SUSPENDED:
                            case TwitterErrorCode.FAIL_AUTHENTICATION:
                            case TwitterErrorCode.NO_REPLY:
                                errorModel.Add(errorcode);
                                break;
                            case TwitterErrorCode.OVERCAPACITY:
                                errorModel.Add(errorcode);
                                parallel.Cancel();
                               break;
                            case TwitterErrorCode.PAGE_DOES_NOT_EXIST:
                            case TwitterErrorCode.UNFOLLOW_ALREADY_REQUESTED:
                                result.Add(new IdName(twitterid, ""));
                                break;
                            case TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS:
                                errorModel.Add(errorcode);
                                parallel.Cancel();
                                break;
                            default:
                                errorModel.Add(TwitterErrorCode.UNKNOWN_ERROR);
                                Syslog.Write(ex, "DeleteFollowers: {0} {1}", ex.ErrorCode, ex.Message);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorModel.Add(TwitterErrorCode.UNKNOWN_ERROR);
                        Syslog.Write(ex);
                    }
                }
                return result;

            })
            .Distinct2((x, y) => x.id == y.id)
            .ToArray();

            var deletedIDs = successes.Select(y => y.id).ToArray();

            if (deletedIDs.RemoveFollowersByIdFromDatabase(auth.twitterUserid, true) != 0)
            {
                var usr = _unitOfWork.Repository<user>().FindById(auth.twitterUserid);
                usr.followingCountTotal -= deletedIDs.Length;
                _unitOfWork.SaveChanges();
            }

            return new ApiResult() { response = successes.Select(y => y.id).ToArray(), error = errorModel.ToString() };
        }

        public FollowersListing GetFollowers(FollowersListingType type, string m, OrderByType method, string dir, int page, int rows = 100)
        {
            var more = !string.IsNullOrEmpty(m);
            var finito = false;

            var auth = Thread.CurrentPrincipal.ToAuthInfo();

            var service = new DatabaseWorker(auth);

            var total = 0;
            var users = service.ListUserDIffv2(
                type,
                page++,
                rows,
                method,
                dir,
                auth.settings.HasFlag(Settings.EXCLUDED_HIDE),
                out total);

            if (users.Length < rows)
            {
                finito = true;
            }

            return new FollowersListing()
            {
                followers = users,
                hasMore = !finito,
                showHeader = !more,
                count = total,
                page = page
            };
        }

        // follow
        public ApiResult PostById([FromBody] string[] id)
        {
            if (id == null)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            var auth = Thread.CurrentPrincipal.ToAuthInfo();

            var duplicates = id.GroupBy(x => x)
                               .Where(x => x.Count() > 1)
                               .ToDictionary(g => g.Key, g => g.Count());

            if (duplicates.Count != 0)
            {
                Syslog.Write("Follow Duplicates => {0}", string.Join(", ", duplicates.Select(x => string.Format("{0}:{1}", x.Key, x.Value))));
            }

            var batches = new HashSet<string>(id).InSetsOf(10);
            var errorModel = new ApiError();

            var parallel = new ParallelHelper(TwitterWorkerBase.TASK_TIMEOUT);
            var successes = parallel.ProcessData(batches, p =>
            {
                var service = new TwitterWorker(auth);

                var result = new List<IdName>();

                foreach (var entry in p)
                {
                    parallel.GetCancellationToken().ThrowIfCancellationRequested();

                    var twitterid = entry;
                    try
                    {
                        var followed = service.FollowByTwitterUserId(twitterid);

                        result.Add(new IdName(followed.Identifier.ID, followed.Identifier.ScreenName));
                    }
                    catch (TwitterQueryException ex)
                    {
                        var errorcode = ex.ErrorCode.ToEnum<TwitterErrorCode>();
                        switch (errorcode)
                        {
                            //case TwitterErrorCode.NO_REPLY:
                            case TwitterErrorCode.ACCOUNT_SUSPENDED2:
                            case TwitterErrorCode.FOLLOW_BLOCKED:
                                errorModel.Add(errorcode, twitterid);
                                break;
                            case TwitterErrorCode.OVERCAPACITY:
                            case TwitterErrorCode.FOLLOW_RATE_LIMIT_EXCEEDED:
                                errorModel.Add(errorcode);
                                parallel.Cancel();
                                break;
                            case TwitterErrorCode.PAGE_DOES_NOT_EXIST:
                            case TwitterErrorCode.UNFOLLOW_ALREADY_REQUESTED:
                                result.Add(new IdName(twitterid,""));
                                break;
                            case TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS:
                                errorModel.Add(TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS);
                                parallel.Cancel();
                                break;
                            default:
                                errorModel.Add(TwitterErrorCode.UNKNOWN_ERROR);
                                Syslog.Write("FollowFollowers: {0} {1}", ex.ErrorCode, ex.Message);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Syslog.Write(ex);
                    }
                }
                return result;
            })
            .Distinct2((x,y) => x.id == y.id)
            .ToArray();

            // instead of adding, we should delete because we check totals using the counter and not by enumerating the database
            var insertIds = successes.Select(y => y.id).ToArray();

            if (insertIds.AddFollowersByIdFromDatabase(auth.twitterUserid, false) != 0)
            {
                var usr = _unitOfWork.Repository<user>().FindById(auth.twitterUserid);
                usr.followingCountTotal += successes.Length;
                _unitOfWork.SaveChanges();
            }

            return new ApiResult() { response = successes.Select(y => y.id).ToArray(), error = errorModel.ToString() };
        }
    }
}
