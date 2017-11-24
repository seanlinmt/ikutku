using System;

namespace ikutku.Models.sync
{
    [Flags]
    public enum AccountStatus
    {
        DIFFQUEUE,
        FOLLOWINGSQUEUE
    }
}
