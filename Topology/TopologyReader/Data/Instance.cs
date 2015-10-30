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
        public int Size { get; set; }        
        public string AsgKeyName { get; set; }
        public string ElbKeyName { get; set; }        
    }
}
