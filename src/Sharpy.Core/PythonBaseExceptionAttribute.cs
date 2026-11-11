using System;

namespace Sharpy
{
    /// <summary>
    /// Marks an exception type that CPython roots at <c>BaseException</c> rather than <c>Exception</c>
    /// (<c>SystemExit</c>). Sharpy has no <c>BaseException</c> (exception_handling.md "No
    /// <c>BaseException</c>"): every exception derives from <see cref="System.Exception"/>, so an
    /// <c>except Exception:</c> handler catches this type where CPython's does not. The compiler reads
    /// the marker to say so when such a handler follows <c>except Exception:</c> and never runs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PythonBaseExceptionAttribute : Attribute
    {
    }
}
