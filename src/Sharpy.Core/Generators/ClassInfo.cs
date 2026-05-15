namespace Sharpy.Generators
{
    /// <summary>Read-only description of a class visible to a <see cref="SourceGenerator"/>.</summary>
    public sealed class ClassInfo
    {
        public string Name { get; }
        public System.Collections.Generic.List<FieldInfo> Fields { get; }
        public System.Collections.Generic.List<MethodInfo> Methods { get; }
        public System.Collections.Generic.List<string> BaseClasses { get; }
        public System.Collections.Generic.List<DecoratorInfo> Decorators { get; }
        public bool IsDataclass { get; }

        public ClassInfo(
            string name,
            System.Collections.Generic.List<FieldInfo> fields,
            System.Collections.Generic.List<MethodInfo> methods,
            System.Collections.Generic.List<string> baseClasses,
            System.Collections.Generic.List<DecoratorInfo> decorators,
            bool isDataclass)
        {
            Name = name;
            Fields = fields;
            Methods = methods;
            BaseClasses = baseClasses;
            Decorators = decorators;
            IsDataclass = isDataclass;
        }
    }
}
