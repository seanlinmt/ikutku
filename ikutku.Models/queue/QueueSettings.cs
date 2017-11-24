using System;

namespace ikutku.Models.queue
{
    [Flags]
    public enum QueueSettings
    {
        NONE                = 0,
        BUILD_FOLLOWERS     = 1,
        BUILD_FOLLOWINGS    = 2,
        BUILD_LISTS         = 4,
        RESET               = 8,
        USER_TRIGGERED      = 16,
        ALL_BUILDS          = BUILD_FOLLOWERS | BUILD_FOLLOWINGS | BUILD_LISTS,
        ALL_BUILDS_USER     = ALL_BUILDS | USER_TRIGGERED
    }
}
