using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Mail;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;

namespace Adaptive.Mail.TEST
{
    [TestClass]
    public class MailAccessLayerTests
    {
        [TestInitialize]
        public void SetCulture()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        [TestMethod]
        public void SendMailTest()
        {
            // Create layer that logs messages in list instead of actually sending them
            List<MailMessage> messages = new List<MailMessage>();
            ITestMail mail = MailAccessLayer.Create<ITestMail>(messages.Add);

            mail.SendAlert("TEST PASSED", "manager@compary.com");
            Assert.AreEqual(1, messages.Count);

            mail.SendNewsletter("all@internet.com", "Buy more goods!");
            Assert.AreEqual(2, messages.Count);

            Assert.AreSame(mail.GetType(), MailAccessLayer.Implement(typeof(ITestMail)));
        }
    }

    public interface ITestMail
    {
        [Send(Subject="ALERT: {0}", To="{1}", From="myself@email.com", Body="Please be aware of alert {0}.")]
        void SendAlert(string alert, string to);

        [Send(Subject = "Company News", To = "{0}", From = "noreply@company.com", Body = "{1}")]
        void SendNewsletter(string to, string body);
    }
}
