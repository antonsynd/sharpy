namespace Sharpy.Generators
{
    /// <summary>A diagnostic reported by a <see cref="SourceGenerator"/>.</summary>
    [SharpyModuleType("sharpy.generators")]
    public sealed class GeneratorDiagnostic
    {
        public string Message { get; }
        public GeneratorDiagnosticSeverity Severity { get; }

        public GeneratorDiagnostic(string message, GeneratorDiagnosticSeverity severity)
        {
            Message = message;
            Severity = severity;
        }
    }
}
