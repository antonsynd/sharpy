using System.Collections.Generic;

namespace Sharpy.Generators
{
    /// <summary>
    /// Context supplied to a <see cref="SourceGenerator"/>. Either <see cref="TargetClass"/>
    /// or <see cref="TargetFunction"/> is non-null depending on whether the decorator was
    /// applied to a class or a function.
    /// </summary>
    public sealed class GeneratorContext
    {
        public ClassInfo? TargetClass { get; }
        public FunctionInfo? TargetFunction { get; }
        public List<object> Arguments { get; }
        public Dictionary<string, object> KeywordArguments { get; }
        public string ModuleName { get; }

        public GeneratorContext(
            ClassInfo? targetClass,
            FunctionInfo? targetFunction,
            List<object> arguments,
            Dictionary<string, object> keywordArguments,
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
