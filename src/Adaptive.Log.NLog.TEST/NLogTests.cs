using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using NLog.Config;
using NLog.Targets;
using System.IO;

namespace Adaptive.Log.NLog.TEST
{
    [TestClass]
    public class NLogAccessLayerTests
    {
        [TestMethod]
        public void NLogAccessLayerTest()
        {
            string logFile = Path.GetTempFileName();

            LoggingConfiguration cfg = new LoggingConfiguration();
            FileTarget file = new FileTarget() { FileName = logFile, Layout="${message}", AutoFlush=true };
            cfg.AddTarget("FILE", file);
            cfg.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, file));
            LogManager.Configuration = cfg;

            Logger logger = LogManager.GetLogger("TEST");
            IMyLog log = NLogAccessLayer.Create<IMyLog>(logger);

            log.SomeError("Donald Duck", new Exception("Out of money."));

            string txt = File.ReadAllText(logFile);
            Assert.IsFalse(string.IsNullOrEmpty(txt));

            log.SomeOtherError("42");
            Assert.AreEqual(2, File.ReadAllLines(logFile).Length);
        }
    }

    public interface IMyLog
    {
        [LogWrite(Id=23, Message="Error processing user '{0}': {1}")]
        void SomeError(string user, Exception error);

        void SomeOtherError(string problem);
    }
}
