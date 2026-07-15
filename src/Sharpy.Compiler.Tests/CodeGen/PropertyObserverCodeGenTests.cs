using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Code generation + execution tests for property observers (#416). Observers lower to a
/// backing field plus an expanded setter; every store — including constructor assignments —
/// runs the observers in before → store → after order (Design Decision 9).
/// </summary>
[Collection("HeavyCompilation")]
public class PropertyObserverCodeGenTests : IntegrationTestBase
{
    public PropertyObserverCodeGenTests(ITestOutputHelper output) : base(output) { }

    private static readonly FeatureFlags Enabled = FeatureFlags.None.Enable("property_observers");

    [Fact]
    public void Observers_FireInBeforeStoreAfterOrder()
    {
        var result = CompileAndExecute(@"
class Box:
    property value: int = 0
        before_set(incoming):
            print(""before"")
        after_set(previous):
            print(""after"")

def main() -> None:
    b: Box = Box()
    b.value = 5
", features: Enabled);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("before\nafter\n", result.StandardOutput);
    }

    [Fact]
    public void Observers_SeeCorrectOldAndNewValues()
    {
        var result = CompileAndExecute(@"
class Box:
    property value: int = 10
        before_set(incoming):
            print(incoming)
        after_set(previous):
            print(previous)
            print(self.value)

def main() -> None:
    b: Box = Box()
    b.value = 42
", features: Enabled);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        // before_set sees incoming=42; after_set sees previous=10 and the stored new value 42.
        Assert.Equal("42\n10\n42\n", result.StandardOutput);
    }

    [Fact]
    public void ConstructorAssignment_FiresObservers()
    {
        var result = CompileAndExecute(@"
class Box:
    property value: int = 1
        after_set(previous):
            print(previous)

    def __init__(self):
        self.value = 99

def main() -> None:
    b: Box = Box()
    print(b.value)
", features: Enabled);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        // The ctor store fires after_set with previous=1 (the field default), then the getter reads 99.
        Assert.Equal("1\n99\n", result.StandardOutput);
    }

    [Fact]
    public void BeforeSetOnly_StoresValueWithoutCapturingOld()
    {
        var result = CompileAndExecute(@"
class Box:
    property value: int = 0
        before_set(incoming):
            if incoming < 0:
                print(""negative"")

def main() -> None:
    b: Box = Box()
    b.value = -3
    print(b.value)
", features: Enabled);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("negative\n-3\n", result.StandardOutput);
    }

    [Fact]
    public void Getter_ReturnsBackingFieldDefault_WithoutFiringObservers()
    {
        var result = CompileAndExecute(@"
class Box:
    property value: int = 7
        before_set(incoming):
            print(""should not fire on default"")

def main() -> None:
    b: Box = Box()
    print(b.value)
", features: Enabled);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        // The field-initializer default does not route through the setter, so no observer fires.
        Assert.Equal("7\n", result.StandardOutput);
    }
}
