namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Canonical names for compilation phases tracked by <see cref="CompilationMetrics"/>.
/// </summary>
/// <remarks>
/// These constants are the single source of truth shared between phase producers
/// (<c>Compiler</c>, <c>ProjectCompiler</c>, <c>AssemblyCompiler</c>) that call
/// <see cref="CompilationMetrics.StartPhase(string)"/> and consumers such as the
/// <see cref="CompilationMetrics"/> shortcut properties that key off the phase name.
/// </remarks>
public static class CompilerPhaseNames
{
    /// <summary>Module discovery: loading and indexing referenced assemblies/stdlib modules.</summary>
    public const string ModuleDiscovery = "Module Discovery";

    /// <summary>Lexical analysis (tokenization).</summary>
    public const string LexicalAnalysis = "Lexical Analysis";

    /// <summary>Syntax analysis (parsing).</summary>
    public const string SyntaxAnalysis = "Syntax Analysis";

    /// <summary>Name resolution (symbol table construction).</summary>
    public const string NameResolution = "Name Resolution";

    /// <summary>Import resolution (module loading).</summary>
    public const string ImportResolution = "Import Resolution";

    /// <summary>Type resolution (resolving type annotations to concrete types).</summary>
    public const string TypeResolution = "Type Resolution";

    /// <summary>Type checking (inference and validation).</summary>
    public const string TypeChecking = "Type Checking";

    /// <summary>Code generation (emitting C# via Roslyn SyntaxFactory).</summary>
    public const string CodeGeneration = "Code Generation";

    /// <summary>Parsing the generated C# source into a Roslyn syntax tree.</summary>
    public const string CSharpParsing = "C# Parsing";

    /// <summary>Resolving Roslyn metadata references for the assembly compilation.</summary>
    public const string ReferenceResolution = "Reference Resolution";

    /// <summary>Roslyn C# compilation of the generated source.</summary>
    public const string RoslynCompilation = "Roslyn Compilation";

    /// <summary>IL emission (writing the assembly).</summary>
    public const string IlEmission = "IL Emission";
}
