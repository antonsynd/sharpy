namespace Sharpy.Generators
{
    /// <summary>Read-only description of a field visible to a <see cref="SourceGenerator"/>.</summary>
    public sealed class FieldInfo
    {
        public string Name { get; }
        public string? TypeName { get; }
        public bool HasDefault { get; }
        public string? DefaultValue { get; }

        public FieldInfo(string name, string? typeName, bool hasDefault, string? defaultValue)
        {
            Name = name;
            TypeName = typeName;
            HasDefault = hasDefault;
            DefaultValue = defaultValue;
        }
    }
}
