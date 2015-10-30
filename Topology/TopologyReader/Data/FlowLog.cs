using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopologyReader.Data
{
    public class FlowLog
    {
        public int protocol { get; set; }
        public string account_id { get; set; }
        public string interface_id { get; set; }
        public int packets { get; set; }
        public int dstport { get; set; }
        public int srcport { get; set; }
        public string log_status { get; set; }
        public int version { get; set; }
        public string action { get; set; }
        public string dstaddr { get; set; }
        public string start { get; set; }
        public string end { get; set; }
        public string srcaddr { get; set; }

        public override string ToString()
        {
            return string.Format("protocol:{0}\naccount_Id:{1}\npackets:{2}\ndstport:{3}\nsrcport:{4}\nlog_status:{5}\nversion:{6}\naction:{7}\ndstaddr:{8}\nstart:{9}\nend:{10}\nsrcaddr:{11}",
                protocol, account_id, packets, dstport, srcport, log_status, version, action, dstaddr, start, end, srcaddr);
        }
    }
}
