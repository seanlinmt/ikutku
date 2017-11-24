namespace ikutku.Models.twitter
{
    // https://dev.twitter.com/docs/error-codes-responses
    // https://support.twitter.com/articles/15364-about-twitter-limits-update-api-dm-and-following
    public enum TwitterErrorCode : int
    {
        NO_ERROR                            = -1,
        NO_REPLY                            = 0,
        NO_MATCH                            = 17,
        FAIL_AUTHENTICATION                 = 32,
        PAGE_DOES_NOT_EXIST                 = 34,
        USER_SUSPENDED                      = 63,
        ACCOUNT_SUSPENDED                   = 64,
        RATE_LIMIT_EXCEEDED                 = 88,
        INVALID_OR_EXPIRED_CREDENTIALS      = 89,
        LIST_NOT_A_MEMBER                   = 110,
        OVERCAPACITY                        = 130,
        INTERNAL_ERROR                      = 131,
        DM_FAIL_NOT_FOLLOWING               = 150,
        DUPLICATE_MESSAGE                   = 151,
        ACCOUNT_SUSPENDED2                  = 159,
        UNFOLLOW_ALREADY_REQUESTED          = 160,
        FOLLOW_RATE_LIMIT_EXCEEDED          = 161,
        FOLLOW_BLOCKED                      = 162,
        ACCESS_DENIED                       = 220,
        UNKNOWN_ERROR                       = int.MaxValue
    }
}
