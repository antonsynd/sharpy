namespace Sharpy.Generators
{
    /// <summary>
    /// Context supplied to a <see cref="SourceGenerator"/>. Either <see cref="TargetClass"/>
    /// or <see cref="TargetFunction"/> is non-null depending on whether the decorator was
    /// applied to a class or a function.
    /// </summary>
    [SharpyModuleType("sharpy.generators")]
    public sealed class GeneratorContext
    {
        public ClassInfo? TargetClass { get; }
        public FunctionInfo? TargetFunction { get; }
        public System.Collections.Generic.List<object> Arguments { get; }
        public System.Collections.Generic.Dictionary<string, object> KeywordArguments { get; }
        public string ModuleName { get; }

        public GeneratorContext(
            ClassInfo? targetClass,
            FunctionInfo? targetFunction,
            System.Collections.Generic.List<object> arguments,
            System.Collections.Generic.Dictionary<string, object> keywordArguments,
            string moduleName)
        {
            TargetClass = targetClass;
            TargetFunction = targetFunction;
            Arguments = arguments;
            KeywordArguments = keywordArguments;
            ModuleName = moduleName;
        }
    }
}
