using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CloudIQ.ES.API.Data
{
    public class Vpc
    {
        public string VpcId { get; set; }
        public string VpcName { get; set; }
        public string State { get; set; }
        public string Tenancy { get; set; }
        public string CIDR { get; set; }
        public string Region { get; set; }
        public bool InternetFacing { get; set; }
        public string Azs { get; set; }
        public int RunningInstances { get; set; }
        public int StoppedInstances { get; set; }
        public int ReservedInstances { get; set; }
        public int InUseVolumes { get; set; }
        public int AvailableVolumes { get; set; }
        public int TAWarnings { get; set; }
        public int CustomWarnings { get; set; }
    }
}