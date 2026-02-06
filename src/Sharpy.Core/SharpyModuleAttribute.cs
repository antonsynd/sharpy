using System;

namespace Sharpy.Core
{
    /// <summary>
    /// Marks a class as a Sharpy module container for discovery by the compiler.
    /// The module name corresponds to the Python-style import name (e.g., "builtins", "math").
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class SharpyModuleAttribute : Attribute
    {
        public string ModuleName { get; }

        public SharpyModuleAttribute(string moduleName)
        {
            ModuleName = moduleName;
        }
    }
}
