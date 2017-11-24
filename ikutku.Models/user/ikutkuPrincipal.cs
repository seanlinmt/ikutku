using System;
using System.Security.Principal;
using System.Threading;
using ikutku.Models.twitter;

namespace ikutku.Models.user
{
    public class ikutkuPrincipal : IPrincipal
    {
        public AuthInfo User { get; private set; }
        public IIdentity Identity { get; private set; }

        public ikutkuPrincipal(IIdentity identity, AuthInfo usr)
        {
            User = usr;
            Identity = identity;
        }

        public bool IsInRole(string role)
        {
            if (role == "Admin" && (User.twitterUsername == "ikutku" || User.twitterUsername == "seanlinmt"))
            {
                return true;
            }
            return false;
        }
    }


    public static class ikutkuPrincipalHelper
    {
        public static AuthInfo ToAuthInfo(this IPrincipal principal)
        {
            if (principal as ikutkuPrincipal == null)
            {
                return null;
            }

            return ((ikutkuPrincipal) principal).User;
        }

    }
}