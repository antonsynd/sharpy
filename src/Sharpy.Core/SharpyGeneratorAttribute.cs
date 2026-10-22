using System;

namespace Sharpy
{
    /// <summary>
    /// Marks a method the compiler emitted from a Sharpy generator function and records its python
    /// <c>__qualname__</c> (<c>gen</c>, <c>K.items</c>, <c>main.&lt;locals&gt;.inner</c>). The
    /// runtime finds the method from a generator object's state machine type (C#'s
    /// <see cref="System.Runtime.CompilerServices.IteratorStateMachineAttribute"/>) and prints the
    /// object as python does, <c>&lt;generator object K.items&gt;</c>, without running its body
    /// (#2057, R-IF; the <c>at 0x…</c> address is a recorded deviation, R-CI).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class SharpyGeneratorAttribute : Attribute
    {
        /// <summary>The generator function's python qualified name.</summary>
        public string PythonQualifiedName { get; }

        /// <summary>Create the attribute with the generator function's python qualified name.</summary>
        public SharpyGeneratorAttribute(string pythonQualifiedName)
        {
            PythonQualifiedName = pythonQualifiedName;
        }
    }
}
