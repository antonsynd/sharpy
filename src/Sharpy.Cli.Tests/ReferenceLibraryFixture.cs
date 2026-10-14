namespace Sharpy.Cli.Tests;

/// <summary>
/// A Sharpy library a reference can resolve to: <c>Tablegreeter.dll</c> (namespace
/// <c>Tablegreeter</c>, class <c>Greeter</c> whose <c>greet()</c> returns <c>"hello from lib"</c>).
/// Usable as an <c>IClassFixture</c> or, through a <c>[CollectionDefinition]</c>, an
/// <c>ICollectionFixture</c>.
/// </summary>
/// <remarks>
/// The library is built ONCE per test process and every fixture instance hands out the same file.
/// The compiler loads a referenced assembly into the process by identity, so a second
/// <c>Tablegreeter.dll</c> at another path (one per test class) would be a same-name load that
/// fails with SPY0306 and leaves the test depending on which class ran first. The build directory
/// is deleted when the process exits.
/// </remarks>
public sealed class ReferenceLibraryFixture
{
    static readonly Lazy<(TempWorkspace Workspace, string Directory, string DllPath)> Library =
        new(Build, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The directory holding <see cref="DllPath"/>.</summary>
    public string Directory => Library.Value.Directory;

    /// <summary>The full path of <c>Tablegreeter.dll</c>.</summary>
    public string DllPath => Library.Value.DllPath;

    public ReferenceLibraryFixture()
    {
        // Build (or fail) when the fixture is created, as a class fixture's constructor did.
        _ = Library.Value;
    }

    static (TempWorkspace, string, string) Build()
    {
        var ws = new TempWorkspace();
        var source = ws.WriteSpy(
            "class Greeter:\n    def greet(self) -> str:\n        return \"hello from lib\"\n",
            "tablegreeter.spy");
        var directory = ws.PathFor("lib");
        var dllPath = Path.Combine(directory, "Tablegreeter.dll");
        var invocation = CliTestHarness.Invoke($"compile \"{source}\" -t library -o \"{dllPath}\" --no-deps");
        if (invocation.ExitCode != 0 || !File.Exists(dllPath))
        {
            ws.Dispose();
            throw new InvalidOperationException(
                $"could not build the reference library: exit {invocation.ExitCode}\n{invocation.StdOut}\n{invocation.StdErr}");
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => ws.Dispose();
        return (ws, directory, dllPath);
    }
}
