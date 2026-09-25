using System;

namespace Sharpy
{
    /// <summary>
    /// Records the Python-facing name of a generated type when its CLR name differs — a class
    /// declared <c>my_thing</c> is emitted as the CLR type <c>MyThing</c>, and the runtime names
    /// it <c>my_thing</c> in messages (<see cref="PyFormat.PyTypeName(System.Type)"/>) and in the
    /// instance fallback <c>&lt;__main__.my_thing object&gt;</c>
    /// (<see cref="PyFormat.PyQualifiedName(System.Type)"/>) (#2006, R-CF).
    /// </summary>
    /// <remarks>
    /// Emitted ONLY when the names differ (the <see cref="SharpyFieldNameAttribute"/> precedent,
    /// #1607): a PascalCase type carries no attribute, and the runtime reads its CLR name.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum
        | AttributeTargets.Interface | AttributeTargets.Delegate, AllowMultiple = false, Inherited = false)]
    public sealed class SharpyNameAttribute : Attribute
    {
        /// <summary>The Python-facing type name.</summary>
        public string PythonName { get; }

        /// <summary>Create the attribute with the Python-facing type name.</summary>
        public SharpyNameAttribute(string pythonName)
        {
            PythonName = pythonName;
        }
    }
}
