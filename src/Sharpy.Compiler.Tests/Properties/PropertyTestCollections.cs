using Xunit;

namespace Sharpy.Compiler.Tests.Properties;

/// <summary>
/// Single serialized collection for all tests that create Roslyn CSharpCompilation objects
/// (via Analyze(), Compile(), or CompileAndExecute()). Running these in parallel causes
/// 30-60GB memory usage because each compilation holds full BCL symbol tables.
/// </summary>
[CollectionDefinition("HeavyCompilation", DisableParallelization = true)]
public class HeavyCompilationCollection;
