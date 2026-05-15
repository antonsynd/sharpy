namespace Sharpy.Generators
{
    /// <summary>Read-only description of a function visible to a <see cref="SourceGenerator"/>.</summary>
    public sealed class FunctionInfo
    {
        public string Name { get; }
        public System.Collections.Generic.List<ParameterInfo> Parameters { get; }
        public string? ReturnType { get; }
        public System.Collections.Generic.List<DecoratorInfo> Decorators { get; }
        public bool IsStatic { get; }
        public bool IsAsync { get; }

        public FunctionInfo(
            string name,
            System.Collections.Generic.List<ParameterInfo> parameters,
            string? returnType,
            System.Collections.Generic.List<DecoratorInfo> decorators,
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
