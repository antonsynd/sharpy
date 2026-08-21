using System;

namespace Sharpy
{
    /// <summary>
    /// Records the Python-facing name of a generated module-level field or property when
    /// the reverse name-mangle cannot recover it from the CLR name alone.
    /// </summary>
    /// <remarks>
    /// The forward mangle is not injective for single-character names: Sharpy <c>e</c>
    /// PascalCases to CLR <c>E</c>, while Sharpy <c>I</c> (a constant-case name) keeps its
    /// spelling — so from the CLR side <c>E</c> and <c>I</c> are indistinguishable in form
    /// yet must reverse differently (<c>math.e</c> vs <c>re.I</c>, #1607). Discovery prefers
    /// this attribute over inference; codegen emits it only where inference would go wrong.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class SharpyFieldNameAttribute : Attribute
    {
        /// <summary>The Python-facing member name.</summary>
        public string PythonName { get; }

        /// <summary>Create the attribute with the Python-facing member name.</summary>
        public SharpyFieldNameAttribute(string pythonName)
        {
            PythonName = pythonName;
        }
    }
}
