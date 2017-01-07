using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CloudIQ.ES.API.Data
{
    public class Account
    {
        public string AccountId {get; set;}
        public string AccountName { get; set; }
        public int RunningInstances { get; set; }
        public int StoppedInstances { get; set; }
        public int ReservedInstances { get; set; }
        public int InUseVolumes { get; set; }
        public int AvailableVolumes { get; set; }
        public float MtdCost { get; set; }
        public float L2mCost { get; set; }
        public int TAWarnings { get; set; }
        public int CustomWarnings { get; set; }
        public string CommonContact { get; set; }
        public string BillingContact { get; set; }
        public string OperationsContact { get; set; }
        public string SecurityContact { get; set; }
    }
}