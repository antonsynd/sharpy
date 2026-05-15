using System.Collections.Generic;

namespace Sharpy.Generators
{
    /// <summary>The result of running a <see cref="SourceGenerator"/>.</summary>
    public sealed class GeneratorOutput
    {
        /// <summary>Generated Sharpy source code, or empty string if nothing was generated.</summary>
        public string Source { get; }

        /// <summary>Diagnostics produced during generation.</summary>
        public List<GeneratorDiagnostic> Diagnostics { get; }

        public GeneratorOutput(string source, List<GeneratorDiagnostic>? diagnostics = null)
        {
            Source = source;
            Diagnostics = diagnostics ?? new List<GeneratorDiagnostic>();
        }

        /// <summary>An empty output with no source and no diagnostics.</summary>
        public static GeneratorOutput Empty { get; } = new GeneratorOutput(string.Empty);
    }
}
