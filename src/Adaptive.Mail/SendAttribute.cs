using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Adaptive.Mail
{
    /// <summary>
    /// Marks a method as message sending method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple=false)]
    public class SendAttribute : Attribute
    {
        /// <summary>
        /// Email of sender. Can include method parameters using string.Format syntax.
        /// </summary>
        public string From { get; set; }

        /// <summary>
        /// Recipients of mail. Can include method parameters using string.Format syntax.
        /// </summary>
        public string To { get; set; }

        /// <summary>
        /// Mail subject. Can include method parameters using string.Format syntax.
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Mail body. Can include method parameters using string.Format syntax.
        /// </summary>
        public string Body { get; set; }
    }
}
