using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CloudWatch
{
    class S3Manager
    {
        public static void UploadMetricFile()
        {
            string bucketName = "cloudiqcwmetrics";
            string keyName    = "";
            string filePath = string.Format("CWMetrics{0}.csv", DateTime.Now.ToString("MMddyyyy"));
            IAmazonS3 client;
            using (client = new AmazonS3Client(Amazon.RegionEndpoint.USWest2))
            {
                try
                {                    
                    PutObjectRequest putRequest2 = new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = keyName,
                        FilePath = filePath,
                        ContentType = "text/csv"
                    };
                    putRequest2.Metadata.Add("x-amz-meta-title", "cwmetrics");

                    PutObjectResponse response2 = client.PutObject(putRequest2);

                }
                catch (AmazonS3Exception amazonS3Exception)
                {
                    if (amazonS3Exception.ErrorCode != null &&
                        (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
                        ||
                        amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                    {
                        Console.WriteLine("Check the provided AWS Credentials.");
                        Console.WriteLine(
                            "For service sign up go to http://aws.amazon.com/s3");
                    }
                    else
                    {
                        Console.WriteLine(
                            "Error occurred. Message:'{0}' when writing an object"
                            , amazonS3Exception.Message);
                    }
                }
            }
            
        }
    }
}
