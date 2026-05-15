using System.Collections.Generic;

namespace Sharpy.Generators
{
    /// <summary>Read-only description of a method visible to a <see cref="SourceGenerator"/>.</summary>
    public sealed class MethodInfo
    {
        public string Name { get; }
        public List<ParameterInfo> Parameters { get; }
        public string? ReturnType { get; }
        public bool IsStatic { get; }
        public bool IsAbstract { get; }
        public bool IsVirtual { get; }
        public bool IsAsync { get; }

        public MethodInfo(
            string name,
            List<ParameterInfo> parameters,
            string? returnType,
            bool isStatic,
            bool isAbstract,
            bool isVirtual,
            bool isAsync)
        {
            Name = name;
            Parameters = parameters;
            ReturnType = returnType;
            IsStatic = isStatic;
            IsAbstract = isAbstract;
            IsVirtual = isVirtual;
            IsAsync = isAsync;
        }
    }
}
