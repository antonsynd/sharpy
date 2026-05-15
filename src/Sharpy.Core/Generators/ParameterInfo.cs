namespace Sharpy.Generators
{
    /// <summary>Read-only description of a parameter visible to a <see cref="SourceGenerator"/>.</summary>
    public sealed class ParameterInfo
    {
        public string Name { get; }
        public string? TypeName { get; }
        public bool HasDefault { get; }

        public ParameterInfo(string name, string? typeName, bool hasDefault)
        {
            Name = name;
            TypeName = typeName;
            HasDefault = hasDefault;
        }
    }
}
