using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using SysList = System.Collections.Generic.List<System.Exception>;

namespace Sharpy
{
    /// <summary>
    /// An exception that contains a group of exceptions (PEP 654).
    /// Wraps <see cref="AggregateException"/> with Python-compatible Subgroup/Split API.
    /// </summary>
    public class ExceptionGroup : AggregateException
    {
        private readonly string _message;

        /// <summary>Create an ExceptionGroup with a message and list of exceptions.</summary>
        public ExceptionGroup(string message, SysList exceptions)
            : base(message, exceptions)
        {
            _message = message;
        }

        /// <summary>Create an ExceptionGroup with a message and enumerable of exceptions.</summary>
        public ExceptionGroup(string message, IEnumerable<Exception> exceptions)
            : base(message, exceptions)
        {
            _message = message;
        }

        /// <summary>The group message (without appended inner exception text).</summary>
        public override string Message => _message;

        /// <summary>The contained exceptions (delegates to InnerExceptions).</summary>
        public ReadOnlyCollection<Exception> Exceptions => InnerExceptions;

        /// <summary>Return a new ExceptionGroup containing only exceptions matching the predicate, or null if none match.</summary>
        public ExceptionGroup? Subgroup(Func<Exception, bool> predicate)
        {
            var matched = new SysList();
            foreach (var ex in InnerExceptions)
            {
                if (ex is ExceptionGroup nested)
                {
                    var sub = nested.Subgroup(predicate);
                    if (sub != null)
                        matched.Add(sub);
                }
                else if (predicate(ex))
                {
                    matched.Add(ex);
                }
            }

            if (matched.Count == 0)
                return null;

            return Derive(matched);
        }

        /// <summary>Split exceptions into (match, rest) groups based on the predicate. Either side may be null.</summary>
        public (ExceptionGroup?, ExceptionGroup?) Split(Func<Exception, bool> predicate)
        {
            var matchList = new SysList();
            var restList = new SysList();

            foreach (var ex in InnerExceptions)
            {
                if (ex is ExceptionGroup nested)
                {
                    var (sub, rem) = nested.Split(predicate);
                    if (sub != null)
                        matchList.Add(sub);
                    if (rem != null)
                        restList.Add(rem);
                }
                else if (predicate(ex))
                {
                    matchList.Add(ex);
                }
                else
                {
                    restList.Add(ex);
                }
            }

            ExceptionGroup? match = matchList.Count > 0 ? Derive(matchList) : null;
            ExceptionGroup? rest = restList.Count > 0 ? Derive(restList) : null;
            return (match, rest);
        }

        /// <summary>Create a new ExceptionGroup of the same type with the given exceptions. Override in subclasses.</summary>
        public virtual ExceptionGroup Derive(IEnumerable<Exception> exceptions)
        {
            return new ExceptionGroup(Message, exceptions);
        }

        /// <summary>Returns a string representation of the ExceptionGroup.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("ExceptionGroup('");
            sb.Append(Message);
            sb.Append("', [");

            bool first = true;
            foreach (var ex in InnerExceptions)
            {
                if (!first)
                    sb.Append(", ");
                first = false;
                sb.Append(ex.GetType().Name);
                sb.Append("('");
                sb.Append(ex.Message);
                sb.Append("')");
            }

            sb.Append("])");
            return sb.ToString();
        }
    }
}
