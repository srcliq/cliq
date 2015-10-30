using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TopologyReader.Data
{
    public class Instance : Amazon.EC2.Model.Instance
    {
        //public string instanceId;
        //public string publicIp;
        //public string privateIp;
        //public string vpcId;
        //public string subnetId;
        //public string imageId;
        //public string privateDNSName;
        //public string publicDNSName;
        //public string keyName;
        public int Size { get; set; }
        //public string instanceType;
        //public string state;
        //public DateTime launchTime;
        public string AsgKeyName { get; set; }
        public string ElbKeyName { get; set; }
        //public List<Amazon.EC2.Model.GroupIdentifier> SecurityGroups;
    }
}
