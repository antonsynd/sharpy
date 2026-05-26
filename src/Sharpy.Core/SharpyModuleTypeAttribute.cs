using System;

namespace Sharpy
{
    /// <summary>
    /// Marks a type as belonging to a specific Sharpy module for discovery by the compiler.
    /// Used for types that live in the Sharpy namespace but belong to a specific module
    /// (e.g., ArgumentParser belongs to "argparse", Path belongs to "pathlib").
    /// </summary>
    /// <remarks>
    /// An optional <see cref="PythonName"/> lets a type expose a Python-facing identifier
    /// that differs from its CLR name — e.g., <c>DateTime</c> appears as <c>datetime</c> in
    /// user code. When omitted, the CLR type name is used verbatim.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class SharpyModuleTypeAttribute : Attribute
    {
        /// <summary>The module name this type belongs to.</summary>
        public string ModuleName { get; }

        /// <summary>
        /// Optional Python-facing type name. When null, the CLR type name is used as-is.
        /// </summary>
        public string? PythonName { get; }

        /// <summary>Create a SharpyModuleTypeAttribute with the specified module name.</summary>
        public SharpyModuleTypeAttribute(string moduleName)
        {
            ModuleName = moduleName;
            PythonName = null;
        }

        /// <summary>
        /// Create a SharpyModuleTypeAttribute with a module name and a Python-facing alias.
        /// </summary>
        public SharpyModuleTypeAttribute(string moduleName, string pythonName)
        {
            ModuleName = moduleName;
            PythonName = pythonName;
        }
    }
}
