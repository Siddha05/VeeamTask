using Microsoft.VisualStudio.TestTools.UnitTesting;
using VeeamTask;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamTask.Tests
{
    [TestClass()]
    public class SyncDirectoryTests
    {
        [TestMethod()]
        public void ClearFolderTest()
        {
            var sd = new SyncDirectory(@"d:\TestFolder\");
            sd.ClearDestinationFolder();
            var files = sd.GetAllFiles(new System.IO.DirectoryInfo(sd.DestinationDirectory)).ToList();
            var dir = new System.IO.DirectoryInfo(sd.DestinationDirectory).GetDirectories().ToList();

            Assert.AreEqual(0, files.Count);
            Assert.AreEqual(0, dir.Count);
        }

        [TestMethod()]
        public void ToSourceFilePathTest()
        {
            var sd = new SyncDirectory(@"d:\Games", @"d:\SyncFolder\Test");
            System.IO.FileInfo fi = new System.IO.FileInfo(@"d:\SyncFolder\Test\myfile.txt");

            var res = sd.ToSourceFilePath(fi);

            Assert.AreEqual(@"d:\Games\myfile.txt", res);
        }

        [TestMethod()]
        public void ToDestinationPathTest()
        {
            var sd = new SyncDirectory(@"d:\Games", @"d:\SyncFolder\Test");
            System.IO.FileInfo fi = new System.IO.FileInfo(@"d:\Games\Strategy\Favorite\myfile.txt");

            var res = sd.ToDestinationFilePath(fi);

            Assert.AreEqual(@"d:\SyncFolder\Test\Strategy\Favorite\myfile.txt", res);
        }

        [TestMethod()]
        public void HasFileInSourceTest()
        {
            var sd = new SyncDirectory(@"d:\Games", @"d:\SyncFolder\Test");

            var res1 = sd.HasFileInSource(new System.IO.FileInfo(@"d:\SyncFolder\Test\WoT\version.xml"));
            var res2 = sd.HasFileInSource(new System.IO.FileInfo(@"d:\SyncFolder\Test\WoT\unexisting.xml"));

            Assert.IsTrue(res1);
            Assert.IsFalse(res2);
        }

        [TestMethod()]
        public void HasDirInSourceTest()
        {
            var sd = new SyncDirectory(@"d:\Games", @"d:\SyncFolder\Test");

            var res1 = sd.HasDirInSource(new System.IO.DirectoryInfo(@"d:\SyncFolder\Test\WoT\Win32"));
            var res2 = sd.HasDirInSource(new System.IO.DirectoryInfo(@"d:\SyncFolder\Test\WoT\Win128"));

            Assert.IsTrue(res1);
            Assert.IsFalse(res2);
        }

        [TestMethod()]
        public void CreateDirectoryStructureTest()
        {
            var sd = new SyncDirectory(@"d:\Games", @"d:\SyncFolder\Test");

            

            
        }
    }
}