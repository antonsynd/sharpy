using System;

namespace Sharpy
{
    /// <summary>
    /// Marks a type as belonging to a specific Sharpy module for discovery by the compiler.
    /// Used for types that live in the Sharpy namespace but belong to a specific module
    /// (e.g., ArgumentParser belongs to "argparse", Path belongs to "pathlib").
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, AllowMultiple = false)]
    public sealed class SharpyModuleTypeAttribute : Attribute
    {
        /// <summary>The module name this type belongs to.</summary>
        public string ModuleName { get; }

        /// <summary>Create a SharpyModuleTypeAttribute with the specified module name.</summary>
        public SharpyModuleTypeAttribute(string moduleName)
        {
            ModuleName = moduleName;
        }
    }
}
