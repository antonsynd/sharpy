using Xunit;

// Collection membership for spy-generated test classes. The classes under Spy/generated/ are
// generated from .spy sources and never hand-edited, so each one that touches process-global state
// gets a partial twin here carrying its [Collection]. This file must NOT live under Spy/generated/:
// regenerate_spy_tests.sh removes every generated/*.cs without a matching .spy stem.
//
// xUnit v2 reads [CollectionDefinition] only from the test assembly itself, so the definitions are
// here too (ConsoleCapture's is in UnittestCapturedOutputTests.cs, HostTimeZone's in
// StrftimeDirectiveTests.cs). ProcessGlobalStateConformanceTests (Sharpy.Compiler.Tests) enforces
// both the membership and the in-project definition (#2179).

namespace Sharpy.Stdlib.Tests.Spy
{
    /// <summary>
    /// Tests that change the process's current directory (os.chdir). Stdlib getcwd, Path.cwd and
    /// every relative path read the current directory, so these run alone.
    /// </summary>
    [CollectionDefinition("ProcessCwd", DisableParallelization = true)]
    public sealed class ProcessCwdCollection
    {
    }

    /// <summary>
    /// Tests that seed the random module and compare draws. The module's generator is one shared
    /// static, so these must not interleave with each other; nothing else reads it, so they need
    /// not run alone.
    /// </summary>
    [CollectionDefinition("PrngState")]
    public sealed class PrngStateCollection
    {
    }

    /// <summary>
    /// Tests that assert something happens inside a fixed wall-clock window. The threading
    /// Timer callback runs on a thread-pool timer (Sharpy.Stdlib Threading/Timer.cs:31), so under a
    /// saturated pool it misses the window: test_timer_fires_after_interval
    /// (threading/threading_module_tests.spy:323-330) failed at MaxParallelThreads=8. These run alone.
    /// </summary>
    [CollectionDefinition("WallClockWindow", DisableParallelization = true)]
    public sealed class WallClockWindowCollection
    {
    }
}

namespace Sharpy.Stdlib.Tests.Spy.Os.OsModuleTests
{
    // os.chdir (cwd) and os.putenv (environment).
    [Collection("ProcessCwd")]
    public partial class OsModuleTestsModuleTests
    {
    }
}

namespace Sharpy.Stdlib.Tests.Spy.Logging.LoggingModuleTests
{
    // unittest.CapturedStderr swaps Console.Error; logging's root level is module state.
    [Collection("ConsoleCapture")]
    public partial class LoggingModuleTestsModuleTests
    {
    }
}

namespace Sharpy.Stdlib.Tests.Spy.Logging.LoggingCompleteTests
{
    // unittest.CapturedStderr swaps Console.Error; logging's root level is module state.
    [Collection("ConsoleCapture")]
    public partial class LoggingCompleteTestsModuleTests
    {
    }
}

namespace Sharpy.Stdlib.Tests.Spy.Random.RandomTests
{
    [Collection("PrngState")]
    public partial class RandomTestsModuleTests
    {
    }
}

namespace Sharpy.Stdlib.Tests.Spy.Random.RandomAdditionalTests
{
    [Collection("PrngState")]
    public partial class RandomAdditionalTestsModuleTests
    {
    }
}

namespace Sharpy.Stdlib.Tests.Spy.Random.RandomAdditional2Tests
{
    [Collection("PrngState")]
    public partial class RandomAdditional2TestsModuleTests
    {
    }
}

namespace Sharpy.Stdlib.Tests.Spy.Cpython.CpythonBisectTests
{
    [Collection("PrngState")]
    public partial class CpythonBisectTestsModuleTests
    {
    }
}

namespace Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests
{
    // test_timer_fires_after_interval gives a 50 ms Timer a fixed 200 ms window.
    [Collection("WallClockWindow")]
    public partial class ThreadingModuleTestsModuleTests
    {
    }
}
