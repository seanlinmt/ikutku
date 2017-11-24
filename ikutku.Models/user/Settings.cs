using System;

namespace ikutku.Models.user
{
    [Flags]
    public enum Settings : long
    {
        NONE                = 0,
        EXCLUDED_HIDE       = 1,
        NO_DIRECT_MSG       = 2,   
        RESET               = 4,    // in the process of being reset
        UPGRADE             = 8     // use to force reset only when there is a bug
    }
}