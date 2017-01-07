using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using CloudIQ.ES.API.Helpers;

namespace CloudIQ.ES.API.Controllers
{
    public class TopologyController : ApiController
    {
        public string GetDummyData()
        {           
            var json = Common.ReadFile("awsdata.json");
            return json;
        }

        public string GetDummyData(string filename)
        {
            if(string.IsNullOrWhiteSpace(filename))
            {
                filename = "awsdata.json";
            }
            var json = Common.ReadFile(filename);
            return json;
        }
    }
}
