using Adaptive.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mail;
using System.Reflection;
using System.Text;

namespace Adaptive.Mail
{
    /// <summary>
    /// Access layer for sending SMTP mails.
    /// </summary>
    public abstract class MailAccessLayer
    {
        /// <summary>
        /// The singleton access layer factory.
        /// </summary>
        private static readonly AdaptiveFactory<MailAccessLayer> s_Factory = new AdaptiveFactory<MailAccessLayer>();

        /// <summary>
        /// Configure factory once.
        /// </summary>
        static MailAccessLayer()
        {
            s_Factory.ImplementAttributedMethods<SendAttribute>()
                .WithSyntaxChecker(CheckSendMethod)
                .UsingSharedExecuter(InitSend, ExecSend);
        }

        /// <summary>
        /// Creates type implementing given mail interface.
        /// </summary>
        /// <param name="interfaceType"></param>
        /// <returns></returns>
        public static Type Implement(Type interfaceType)
        {
            return s_Factory.Implement(interfaceType);
        }

        /// <summary>
        /// Creates new instance of given mail interface.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="send"></param>
        /// <returns></returns>
        public static T Create<T>(Action<MailMessage> send)
        {
            return (T)Activator.CreateInstance(Implement(typeof(T)), send);
        }

        /// <summary>
        /// Holds all info needed to execute a [Send] method efficiently.
        /// </summary>
        protected class SendInfo
        {
            /// <summary>
            /// The reflected attribute of method.
            /// </summary>
            public SendAttribute Attribute { get; set; }
        }

        /// <summary>
        /// Validates method syntax.
        /// </summary>
        /// <param name="method"></param>
        protected static void CheckSendMethod(MethodInfo method)
        {
            if (method.ReturnType != typeof(void))
                throw new ArgumentException("[Send] methods must return void.");
        }

        /// <summary>
        /// Harvests attribute from method into info.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        protected static SendInfo InitSend(MailAccessLayer layer, MethodInfo method)
        {
            return new SendInfo() { Attribute = method.GetCustomAttributes(true).OfType<SendAttribute>().Single() };
        }

        /// <summary>
        /// Executes a single call to a [Send] method.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="info"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        protected static object ExecSend(MailAccessLayer layer, SendInfo info, object[] parameters)
        {
            string from = string.Format(info.Attribute.From, parameters);
            string to = string.Format(info.Attribute.To, parameters);
            string subject = string.Format(info.Attribute.Subject, parameters);
            string body = string.Format(info.Attribute.Body, parameters);

            using (MailMessage message = new MailMessage(from, to, subject, body))
            {
                layer._send(message);
            }

            return null;
        }

        private readonly Action<MailMessage> _send;

        /// <summary>
        /// Creates new instance around SMTP client.
        /// </summary>
        /// <param name="client"></param>
        public MailAccessLayer(SmtpClient client)
            : this (client.Send)
        {
        }

        /// <summary>
        /// Creates new instane around any mail send delegate.
        /// </summary>
        /// <param name="send"></param>
        public MailAccessLayer(Action<MailMessage> send)
        {
            _send = send;
        }
    }
}
