using CloudIQ.ES.API.Data;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace CloudIQ.ES.API.Controllers
{
    public class EsController : ApiController
    {
        // GET api/es
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/es/5
        public string Get(int id)
        {
            return "value";
        }

        // POST api/es
        public void Post([FromBody]string value)
        {
        }

        // PUT api/es/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/es/5
        public void Delete(int id)
        {
        }

        [System.Web.Http.AcceptVerbs("GET", "POST")]
        [System.Web.Http.HttpGet]
        public string SearchByIndex(int id)
        {
            var client = new RestClient("http://52.26.168.92:9200/");            

            var request = new RestRequest("_search", Method.GET);            
            RestResponse response = (RestResponse) client.Execute(request);
            var content = response.Content; // raw content as string

            return content;
        }

        [System.Web.Http.AcceptVerbs("GET")]
        [System.Web.Http.HttpGet]
        public string GetInstances()
        {
            MySql.Data.MySqlClient.MySqlConnection conn;
            string myConnectionString;
            string json = string.Empty;
            myConnectionString = "server=cloudiq.cpnxfxlqtuls.us-west-2.rds.amazonaws.com; port=3306; uid=CloudIQ; pwd=CloudIQ123; database=AWS_Metadata;";

            try
            {
                conn = new MySql.Data.MySqlClient.MySqlConnection();
                conn.ConnectionString = myConnectionString;
                conn.Open();
                var command = new MySqlCommand("Select * from EC2Instance", conn);
                var reader = command.ExecuteReader();
                List<Instance> instances = new List<Instance>();
                if(reader.HasRows)
                {
                    while(reader.Read())
                    {
                        instances.Add(new Instance { InstanceId = reader["instanceId"].ToString(), 
                                                        PrivateIpAddress = reader["privateipaddress"].ToString(), 
                                                        PublicIpAddress = reader["publicipaddress"].ToString() }
                                    );                        
                    }
                }
                json = JsonConvert.SerializeObject(instances, Formatting.Indented);
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                
            }
            return json;
        }
    }
}
