using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Collection definition to ensure tests that use NameMangler static state run sequentially
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollection
{
}
