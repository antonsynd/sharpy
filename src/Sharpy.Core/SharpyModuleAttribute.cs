using System;

namespace Sharpy
{
    /// <summary>
    /// Marks a class as a Sharpy module container for discovery by the compiler.
    /// The module name corresponds to the Python-style import name (e.g., "builtins", "math").
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class SharpyModuleAttribute : Attribute
    {
        /// <summary>The module name (e.g., "math", "os").</summary>
        public string ModuleName { get; }

        /// <summary>Create a SharpyModuleAttribute with the specified module name.</summary>
        public SharpyModuleAttribute(string moduleName)
        {
            ModuleName = moduleName;
        }
    }
}
