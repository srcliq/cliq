using Amazon;
using Amazon.IdentityManagement;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TopologyReader.Helpers
{
    public static class Common
    {
        

        public static string GetAccountNumber()
        {
            var iamClient = new AmazonIdentityManagementServiceClient();
            var x = iamClient.GetUser();
            Regex regex = new Regex(@"\d+");
            Match match = regex.Match(x.User.Arn);
            if (match.Success)
            {
                return match.Value;
            }
            return string.Empty; //"arn:aws:iam::990008671661:user/xyz"
        }

        public static string GetDataKey(string accountNumber, RegionEndpoint regionEndPoint)
        {
            var dataKey = GetDataKey(accountNumber, regionEndPoint.SystemName);
            return dataKey;
        }

        public static string GetDataKey(string accountNumber, string regionName)
        {
            return GetDataKey(DateTime.UtcNow, accountNumber, regionName);
        }

        public static string GetDataKey(DateTime date, string accountNumber, string regionName)
        {
            var dateString = date.ToString("yyyyMMddHHmmss");
            var dataKey = string.Format("{0}-{1}-{2}", dateString, accountNumber, regionName);
            return dataKey;
        }

        public static string GetDataKey(string dateString, string accountNumber, string regionName)
        {            
            var dataKey = string.Format("{0}-{1}-{2}", dateString, accountNumber, regionName);
            return dataKey;
        }
    }
}
