using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Adaptive.Core
{
    /// <summary>
    /// Base class for all member handlers
    /// </summary>
    /// <typeparam name="TMember"></typeparam>
    public abstract class MemberHandler<TMember> where TMember : MemberInfo
    {
        /// <summary>
        /// Base type of implemented type
        /// </summary>
        public Type BaseClass { get; internal set; }

        /// <summary>
        /// Predicate for handler
        /// </summary>
        public Func<TMember, bool> Predicate { get; internal set; }

        /// <summary>
        /// Syntax checker for handler
        /// </summary>
        public Action<TMember> Checker { get; internal set; }
    }

    /// <summary>
    /// Holds general member handler methods
    /// </summary>
    public static class MemberHandlerExtensions
    {
        /// <summary>
        /// Adds method that is called once per property when type is first created. Checker must throw exception if check fails.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="checker"></param>
        /// <returns></returns>
        public static THandler WithSyntaxChecker<THandler, TInfo>(this THandler handler, Action<TInfo> checker) where THandler : MemberHandler<TInfo> where TInfo : MemberInfo
        {
            handler.Checker = checker;
            return handler;
        }
    }
}
