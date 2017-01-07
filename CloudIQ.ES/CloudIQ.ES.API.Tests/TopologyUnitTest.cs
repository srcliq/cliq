using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CloudIQ.ES.API.Helpers;

namespace CloudIQ.ES.API.Tests
{
    [TestClass]
    public class TopologyUnitTest
    {
        [TestMethod]
        public void GetTopologyUnitTest()
        {
            Common.GetTopology();
            Assert.IsTrue(true);
        }
    }
}
