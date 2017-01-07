using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CloudIQ.ES.API.Data
{
    public class Instance
    {
        public string InstanceId { get; set; }
        public string PublicIpAddress { get; set; }
        public string PrivateIpAddress { get; set; }
    }
}