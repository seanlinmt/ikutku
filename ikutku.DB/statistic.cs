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
    
    public partial class statistic
    {
        public long id { get; set; }
        public long uncachedCount { get; set; }
        public long uncachedElapsed { get; set; }
        public long staleCount { get; set; }
        public long staleElapsed { get; set; }
        public int insertCount { get; set; }
        public long insertElapsed { get; set; }
        public long ticksSince { get; set; }
        public long ticks { get; set; }
    }
}