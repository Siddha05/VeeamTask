using Microsoft.VisualStudio.TestTools.UnitTesting;
using VeeamTask;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamTask.Tests
{
    public class SyncDirectoryTests
    {
        [TestMethod()]
        public void GetFilesTest()
        {
            var sd = new SyncDirectory(@"d:\TestFolder\");
            var files = sd.GetFiles().ToList();

            Assert.AreEqual(4, files.Count);
        }
    }
}