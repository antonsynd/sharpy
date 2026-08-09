namespace Sharpy.TestInfrastructure.Integration;

/// <summary>
/// One fixture corpus root, with the label its fixtures' test names carry.
/// </summary>
/// <remarks>
/// The label is what keeps two roots' fixtures distinguishable once a sweep covers both. The
/// primary root deliberately has an <em>empty</em> label so its test names — which are the keys in
/// the drain-on-fix allowlists (<c>differential-exec-allowlist.txt</c>,
/// <c>metamorphic-allowlist.txt</c>) — stay byte-identical to what they were before roots became
/// explicit. A widening must not silently rewrite every existing ratchet key.
/// </remarks>
public sealed record FixtureRoot
{
    public required string Path { get; init; }

    /// <summary>
    /// Prefixed to each discovered fixture's <see cref="TestFixtureInfo.TestName"/> (and recorded
    /// as <see cref="TestFixtureInfo.RootLabel"/>). Empty for the primary root.
    /// </summary>
    public string Label { get; init; } = "";

    public override string ToString() => Label.Length == 0 ? Path : $"{Label} ({Path})";
}

/// <summary>
/// The named fixture corpora, resolved from the repository root rather than from whichever test
/// assembly happens to be asking (#1338).
///
/// <para>
/// Before this existed, <see cref="FixtureDiscoveryHelper.FixturesPath"/> was anchored on the
/// calling assembly's location, so "which fixtures does this sweep see?" had no answer at the call
/// site — it depended on the host project. Two sweeps that should have covered both corpora
/// covered only their own, and nothing said so. Every consumer now names its roots; the ones that
/// deliberately stay single-root say that too, in the same spelling.
/// </para>
/// </summary>
public static class FixtureRoots
{
    /// <summary>The repository root — the directory containing <c>sharpy.sln</c>.</summary>
    public static readonly string RepositoryRoot = FindRepositoryRoot();

    /// <summary>
    /// <c>src/Sharpy.Compiler.Tests/Integration/TestFixtures</c> — the language corpus: every
    /// fixture that compiles against Sharpy.Core alone.
    /// </summary>
    public static readonly FixtureRoot CompilerTests = new()
    {
        Path = ProjectFixtures("Sharpy.Compiler.Tests"),
    };

    /// <summary>
    /// <c>src/Sharpy.Stdlib.Tests/Integration/TestFixtures</c> — fixtures that import stdlib
    /// modules, and so need <c>Sharpy.Stdlib.dll</c> referenced to compile.
    /// </summary>
    public static readonly FixtureRoot StdlibTests = new()
    {
        Path = ProjectFixtures("Sharpy.Stdlib.Tests"),
        Label = "stdlib-tests",
    };

    /// <summary>Both corpora, in the order a cross-corpus sweep should report them.</summary>
    public static readonly IReadOnlyList<FixtureRoot> All = new[] { CompilerTests, StdlibTests };

    private static string ProjectFixtures(string projectName) => Path.GetFullPath(
        Path.Combine(RepositoryRoot, "src", projectName, "Integration", "TestFixtures"));

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(typeof(FixtureRoots).Assembly.Location)!);

        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
            dir = dir.Parent;

        if (dir == null)
        {
            throw new InvalidOperationException(
                "Could not locate the repository root (no sharpy.sln above "
                + $"{Path.GetDirectoryName(typeof(FixtureRoots).Assembly.Location)}). Fixture roots "
                + "are repository-anchored so that a sweep's corpus does not depend on which test "
                + "project hosts it.");
        }

        return dir.FullName;
    }
}
