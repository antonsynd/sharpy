using System.Collections.Generic;

namespace Sharpy.Generators
{
    /// <summary>Read-only description of a class visible to a <see cref="SourceGenerator"/>.</summary>
    public sealed class ClassInfo
    {
        public string Name { get; }
        public List<FieldInfo> Fields { get; }
        public List<MethodInfo> Methods { get; }
        public List<string> BaseClasses { get; }
        public List<DecoratorInfo> Decorators { get; }
        public bool IsDataclass { get; }

        public ClassInfo(
            string name,
            List<FieldInfo> fields,
            List<MethodInfo> methods,
            List<string> baseClasses,
            List<DecoratorInfo> decorators,
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
