using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Shared;

public class CSharpTypeNamesTests
{
    // Sharpy-name -> C# type-name resolution moved to ClrTypeBridge (single owner of the
    // CLR<->Sharpy<->C# name mapping); the former FromSharpyName coverage now lives in
    // ClrTypeBridgeTests (wrapper-collection + reverse-name-mapping theories).

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        Assert.Equal("Sharpy.List", CSharpTypeNames.SharpyList);
        Assert.Equal("Sharpy.Dict", CSharpTypeNames.SharpyDict);
        Assert.Equal("Sharpy.Set", CSharpTypeNames.SharpySet);
        Assert.Equal("Sharpy.Optional", CSharpTypeNames.SharpyOptional);
        Assert.Equal("Sharpy.Result", CSharpTypeNames.SharpyResult);
        Assert.Equal("IEnumerable", CSharpTypeNames.IEnumerable);
        Assert.Equal("IAsyncEnumerable", CSharpTypeNames.IAsyncEnumerable);
    }
}
