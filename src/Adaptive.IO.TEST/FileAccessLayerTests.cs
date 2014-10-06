using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Threading;
using System.IO;
using System.Linq;

namespace Adaptive.IO.TEST
{
    [TestClass]
    public class FileAccessLayerTests
    {
        [TestInitialize]
        public void SetCulture()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        [TestMethod]
        public void FileAccessLayerTest()
        {
            IMyFAL fal = FileAccessLayer.Create<IMyFAL>();

            string filename = Path.GetTempFileName();
            fal.ReadAllText(filename);
            fal.Delete(filename);
        }

        [TestMethod]
        public void DirectoryAccessLayerTest()
        {
            IMyDAL dal = DirectoryAccessLayer.Create<IMyDAL>();

            Assert.AreNotEqual(0, dal.GetFilesInFolder(".").Length);
        }
    }

    public interface IMyFAL
    {
        [File(FileMethod.ReadAllText)]
        string ReadAllText(string filename);

        [File(FileMethod.Delete)]
        void Delete(string filename);
    }

    public interface IMyDAL
    {
        [Directory(DirectoryMethod.GetFiles)]
        string[] GetFilesInFolder(string folder);
    }
}
