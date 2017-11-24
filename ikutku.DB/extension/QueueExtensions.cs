using System.Collections.Generic;
using System.Linq;
using clearpixels.Helpers;
using ikutku.Models.queue;
using ikutku.Models.user;

namespace ikutku.DB.extension
{
    public static class QueueExtensions
    {
        public static QueueInfo ToModel(this queuedUser row)
        {
            return new QueueInfo()
                {
                    auth = row.user.ToAuthInfo(),
                    settings = row.settings.ToEnum<QueueSettings>(),
                    reset = (row.user.settings & (long)Settings.RESET) != 0
                };
        }

        public static IEnumerable<QueueInfo> ToModel(this IQueryable<queuedUser> rows)
        {
            foreach (var row in rows)
            {
                yield return row.ToModel();
            }
        }

        public static IEnumerable<AuthInfo> ToModel(this IQueryable<queuedFollowingUser> rows)
        {
            foreach (var row in rows)
            {
                yield return row.user.ToAuthInfo();
            }
        }
    }
}
