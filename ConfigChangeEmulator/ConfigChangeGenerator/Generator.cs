/*******************************************************************************
* Copyright 2009-2013 Amazon.com, Inc. or its affiliates. All Rights Reserved.
* 
* Licensed under the Apache License, Version 2.0 (the "License"). You may
* not use this file except in compliance with the License. A copy of the
* License is located at
* 
* http://aws.amazon.com/apache2.0/
* 
* or in the "license" file accompanying this file. This file is
* distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
* KIND, either express or implied. See the License for the specific
* language governing permissions and limitations under the License.
*******************************************************************************/

using System;
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using System.Collections.Generic;

namespace ConfigChangeGenerator
{
    class Generator
    {
        public static void Main(string[] args)
        {
            //ChangeInstanceSecurityGroupTags(0);
            //ChangeInstanceTags(1);
            ManageSecurityGroups();
            ManageInstanceSecurityGroups();
            Console.WriteLine("Press Enter to continue...");
            Console.Read();
        }

        private static void ChangeInstanceTags(int changeType)
        {
            IAmazonEC2 ec2 = new Amazon.EC2.AmazonEC2Client();
            DescribeInstancesResponse instanceResponse = ec2.DescribeInstances();
            var resourceIdList = new List<string>();
            var tagsList = new List<Tag>();
            tagsList.Add(new Tag { Key = "Test1-AutoAdded", Value = "ToInduceConfigChages" });
            tagsList.Add(new Tag { Key = "Test2-AutoAdded", Value = "ToInduceConfigChages" });
            foreach (var reservation in instanceResponse.Reservations)
            {
                foreach (var rInstance in reservation.Instances)
                {
                    resourceIdList.Add(rInstance.InstanceId);                    
                }
            }               
            if (changeType == 0)
            {
                var createTagsRequest = new CreateTagsRequest(resourceIdList, tagsList);
                ec2.CreateTags(createTagsRequest);
            }                
            else if (changeType == 1)
            {
                var deleteTagsRequest = new DeleteTagsRequest();
                deleteTagsRequest.Resources = resourceIdList;
                deleteTagsRequest.Tags = tagsList;
                ec2.DeleteTags(deleteTagsRequest);
            }                
        }

        private static void ManageInstanceSecurityGroups()
        {
            IAmazonEC2 ec2 = new Amazon.EC2.AmazonEC2Client();
            DescribeInstancesResponse instanceResponse = ec2.DescribeInstances();
            foreach (var reservation in instanceResponse.Reservations)
            {
                foreach (var rInstance in reservation.Instances)
                {
                    var securityGroupList = new List<string>();
                    foreach(var groupIdentifier in rInstance.SecurityGroups){
                        securityGroupList.Add(groupIdentifier.GroupId);
                    }
                    if (!securityGroupList.Contains("sg-9cc4a3fb"))
                    {
                        securityGroupList.Add("sg-9cc4a3fb");
                    }
                    else
                    {
                        securityGroupList.Remove("sg-9cc4a3fb");
                    }
                    var modifyInstanceAttributeRequest = new ModifyInstanceAttributeRequest();
                    modifyInstanceAttributeRequest.InstanceId = rInstance.InstanceId;
                    modifyInstanceAttributeRequest.Groups = securityGroupList;
                    try
                    {
                        ec2.ModifyInstanceAttribute(modifyInstanceAttributeRequest);
                    }
                    catch (Exception)
                    {
                                                
                    }                    
                }
            }       
        }

        private static void ManageSecurityGroups()
        {
            IAmazonEC2 ec2 = new Amazon.EC2.AmazonEC2Client();
            var sgResponse = ec2.DescribeSecurityGroups();
            
            string ipRange = "22.22.22.22/0";
            List<string> ranges = new List<string>() { ipRange };

            var ipPermission = new IpPermission();
            ipPermission.IpProtocol = "tcp";
            ipPermission.FromPort = 3333;
            ipPermission.ToPort = 3333;
            ipPermission.IpRanges = ranges;

            var ingressRequest = new AuthorizeSecurityGroupIngressRequest();            
            ingressRequest.IpPermissions.Add(ipPermission);
            var revokeRequest = new RevokeSecurityGroupIngressRequest();
            revokeRequest.IpPermissions.Add(ipPermission);
            foreach (var sg in sgResponse.SecurityGroups)
            {
                try
                {
                    if (new Random().Next(2) == 1)
                    {
                        ingressRequest.GroupId = sg.GroupId;
                        var ingressResponse = ec2.AuthorizeSecurityGroupIngress(ingressRequest);
                    }
                    else
                    {
                        revokeRequest.GroupId = sg.GroupId;
                        ec2.RevokeSecurityGroupIngress(revokeRequest);
                    }                    
                    //Console.WriteLine("New RDP rule for: " + ipRange);
                }
                catch (AmazonEC2Exception ex)
                {
                    // Check the ErrorCode to see if the rule already exists.
                    if ("InvalidPermission.Duplicate" == ex.ErrorCode)
                    {
                        //Console.WriteLine("An RDP rule for: {0} already exists.", ipRange);
                    }
                    else
                    {
                        // The exception was thrown for another reason, so re-throw the exception.
                        //throw;
                    }
                }
            }
            
        }
    }
}