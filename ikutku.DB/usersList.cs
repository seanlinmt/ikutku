//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ikutku.DB
{
    using System;
    using System.Collections.Generic;
    
    public partial class usersList
    {
        public usersList()
        {
            this.usersInLists = new HashSet<usersInList>();
        }
    
        public string id { get; set; }
        public string listname { get; set; }
        public string slug { get; set; }
        public bool exclude { get; set; }
        public string ownerid { get; set; }
        public System.DateTime updated { get; set; }
        public Nullable<long> listCursor { get; set; }
        public int memberCount { get; set; }
    
        public virtual ICollection<usersInList> usersInLists { get; set; }
        public virtual user user { get; set; }
    }
}
