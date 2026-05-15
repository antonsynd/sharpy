namespace Sharpy.Generators
{
    /// <summary>The result of running a <see cref="SourceGenerator"/>.</summary>
    [SharpyModuleType("sharpy.generators")]
    public sealed class GeneratorOutput
    {
        public string Source { get; }
        public System.Collections.Generic.List<GeneratorDiagnostic> Diagnostics { get; }

        public GeneratorOutput(string source, System.Collections.Generic.List<GeneratorDiagnostic>? diagnostics = null)
        {
            Source = source;
            Diagnostics = diagnostics ?? new System.Collections.Generic.List<GeneratorDiagnostic>();
        }

        public static GeneratorOutput Empty { get; } = new GeneratorOutput(string.Empty);
    }
}
