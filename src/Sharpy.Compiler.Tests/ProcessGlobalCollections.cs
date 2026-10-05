using Xunit;

namespace Sharpy.Compiler.Tests;

// xUnit v2 reads [CollectionDefinition] only from the test assembly itself: a definition placed in
// Sharpy.TestInfrastructure leaves DisableParallelization inert (#2179). Every collection a
// Compiler.Tests class names must therefore be defined here; ProcessGlobalStateConformanceTests
// enforces it.

/// <summary>
/// Tests that change the process's current directory. Every compile in this assembly reads the
/// current directory (ModuleResolver's fallback, SyntheticProject's project directory), so these
/// run alone.
/// </summary>
[CollectionDefinition("ProcessCwd", DisableParallelization = true)]
public class ProcessCwdCollection
{
}

/// <summary>
/// Tests that redirect Console.Out/Error without <c>TestHelpers.ConsoleLock</c> (ReplSession swaps
/// the console itself, on a thread-pool thread), so they run alone.
/// </summary>
[CollectionDefinition("ConsoleCapture", DisableParallelization = true)]
public class ConsoleCaptureCollection
{
}

/// <summary>
/// CsCheck property classes that compile programs. CsCheck's <c>Sample</c> already uses one thread
/// per core, so these run alone and keep their memory out of the parallel phase (#649, #2179).
/// </summary>
[CollectionDefinition("PropertySerial", DisableParallelization = true)]
public class PropertySerialCollection
{
}
