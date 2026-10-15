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

    // The module globals StdlibModuleGlobalConcurrencyTests (Conformance/) drives from several
    // threads (#2250): each module's cells share a collection with the spy tests that set and then
    // read the same global, so a cell never runs beside them. Like PrngState, none needs to run alone.

    /// <summary>Tests that set and read socket.setdefaulttimeout's process-wide default.</summary>
    [CollectionDefinition("SocketDefaultTimeout")]
    public sealed class SocketDefaultTimeoutCollection
    {
    }

    /// <summary>Tests that set calendar.setfirstweekday's module-wide first weekday and read it back.</summary>
    [CollectionDefinition("CalendarFirstweekday")]
    public sealed class CalendarFirstweekdayCollection
    {
    }

    /// <summary>Tests that write xml.register_namespace's process-wide registry.</summary>
    [CollectionDefinition("XmlRegisteredNamespaces")]
    public sealed class XmlRegisteredNamespacesCollection
    {
    }

    /// <summary>Tests that seed numpy.random and compare draws.</summary>
    [CollectionDefinition("NumpyRandomState")]
    public sealed class NumpyRandomStateCollection
    {
    }
}

namespace Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests
{
    [Collection("SocketDefaultTimeout")]
    public partial class SocketModuleTestsModuleTests
    {
    }
}

namespace Sharpy.Stdlib.Tests.Spy.Calendar.CalendarTests
{
    [Collection("CalendarFirstweekday")]
    public partial class CalendarTestsModuleTests
    {
    }
}

namespace Sharpy.Stdlib.Tests.Spy.XML.XmlModuleTests
{
    [Collection("XmlRegisteredNamespaces")]
    public partial class XmlModuleTestsModuleTests
    {
    }
}

namespace Sharpy.Stdlib.Tests.Spy.Numpy.NumpyRandomTests
{
    [Collection("NumpyRandomState")]
    public partial class NumpyRandomTestsModuleTests
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
