namespace Sharpy
{
    /// <summary>
    /// Base class for unittest-style test classes. The Sharpy compiler detects
    /// inheritance from TestCase and synthesizes xUnit lifecycle code:
    /// <list type="bullet">
    ///   <item>setup() → constructor body that calls Setup()</item>
    ///   <item>teardown() → IDisposable.Dispose() that calls Teardown()</item>
    ///   <item>@test methods → [Fact] public methods</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// This is a marker type with no xUnit dependency. The compiler handles all
    /// xUnit integration during code generation. TestCase itself is a minimal
    /// base class that user test classes inherit from.
    /// </remarks>
    [SharpyModuleType("unittest")]
    public class TestCase
    {
    }
}
