using System.Collections.Generic;

namespace Sharpy.Generators
{
    /// <summary>Read-only description of a function visible to a <see cref="SourceGenerator"/>.</summary>
    public sealed class FunctionInfo
    {
        public string Name { get; }
        public List<ParameterInfo> Parameters { get; }
        public string? ReturnType { get; }
        public List<DecoratorInfo> Decorators { get; }
        public bool IsStatic { get; }
        public bool IsAsync { get; }

        public FunctionInfo(
            string name,
            List<ParameterInfo> parameters,
            string? returnType,
            List<DecoratorInfo> decorators,
            bool isStatic,
            bool isAsync)
        {
            Name = name;
            Parameters = parameters;
            ReturnType = returnType;
            Decorators = decorators;
            IsStatic = isStatic;
            IsAsync = isAsync;
        }
    }
}
