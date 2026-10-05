// No xUnit collections are defined here, on purpose. xUnit v2 reads [CollectionDefinition] only
// from the test assembly itself, so a definition in this shared library is inert: HeavyCompilation,
// defined here from #679 until #2179, never disabled parallelization for anyone. Define a
// collection in the test project whose classes name it; ProcessGlobalStateConformanceTests
// (Sharpy.Compiler.Tests) enforces that for every process-global mutation.
