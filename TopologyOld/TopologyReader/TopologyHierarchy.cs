using System;
using Amazon.CloudSearch.Model;
using Newtonsoft.Json;

namespace TopologyReader
{
    public class TopologyHierarchy
    {
         public Company children;
    }

    public class Company
    {
        public string name;
        public Account[] children;
    }

    public class CompanySG
    {
        public string name;
        public AccountSG[] children;
    }

    public class Account
    {
        public string name;
        public VPC[] children;
    }

    public class AccountSG
    {
        public string name;
        public VPCSG[] children;
    }

    public class VPC
    {
        public string name;
        public Subnet[] children;
    }

    public class VPCSG
    {
        public string name;
        public SecurityGroup[] children;
    }


    public class Subnet
    {
        public string name;
        public Instance[] children;
    }

    public class SecurityGroup
    {
        public string name;
        public Instance[] children;
    }

    public class Instance
    {
        public string name;
        public int size;
        [JsonIgnore]
        public string instanceType;
        [JsonIgnore]
        public string instanceState;
        [JsonIgnore]
        public DateTime launchTime;
    }
}