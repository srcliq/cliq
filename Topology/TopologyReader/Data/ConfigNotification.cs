using System;

namespace TopologyReader.Data
{
    class ConfigNotification
    {
        public string Type { get; set; }
        public string MessageId { get; set; }
        public string TopicArn { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string Timestamp { get; set; }
        public string SignatureVersion { get; set; }
        public string Signature { get; set; }
        public string SigningCertURL { get; set; }
        public string UnsubscribeURL { get; set; }
    }

    class ConfigMessage
    {
        public Object configurationItem { get; set; }
        public Object configurationItemDiff { get; set; }
        public string messageType { get; set; }
    }

    class ConfigurationItem
    {
        public Object configuration { get; set; }
        public string ResourceType { get; set; }
        public string AWSAccountId { get; set; }
        public string AWSRegion { get; set; }
        public string ConfigurationItemCaptureTime { get; set; }
    }

    class ConfigurationItemDiff
    {
        public string ChangeType { get; set; }
    }
    //class Configuration
    //{
    //    public string configData { get; set; }
    //}
}
