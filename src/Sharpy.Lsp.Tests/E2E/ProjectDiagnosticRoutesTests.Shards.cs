using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

// One class per state of the project-diagnostics route matrix (ProjectDiagnosticRouteCells), in a NAMESPACE named
// ProjectDiagnosticRoutesTests so every row's FullyQualifiedName still starts
// `Sharpy.Lsp.Tests.E2E.ProjectDiagnosticRoutesTests.` — the FileBasedIntegrationTests precedent (#2179).
namespace Sharpy.Lsp.Tests.E2E.ProjectDiagnosticRoutesTests;

public sealed class Clean : ProjectDiagnosticRouteCells
{
    public Clean(ITestOutputHelper output) : base(output) { }

    protected override string State => "clean";
}

public sealed class Missing : ProjectDiagnosticRouteCells
{
    public Missing(ITestOutputHelper output) : base(output) { }

    protected override string State => "missing";
}

public sealed class Unlisted : ProjectDiagnosticRouteCells
{
    public Unlisted(ITestOutputHelper output) : base(output) { }

    protected override string State => "unlisted";
}

public sealed class DupClass : ProjectDiagnosticRouteCells
{
    public DupClass(ITestOutputHelper output) : base(output) { }

    protected override string State => "dupclass";
}

/// <summary>The partition is total: each state of <see cref="ProjectDiagnosticRouteCells.States"/> has exactly one class.</summary>
public sealed class Totality
{
    [Fact]
    public void EveryState_HasExactlyOnePartitionClass()
    {
        var states = typeof(ProjectDiagnosticRouteCells).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ProjectDiagnosticRouteCells)) && !t.IsAbstract)
            .Select(t => (string)t.GetProperty("State", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t))!)
            .ToList();
        states.Should().BeEquivalentTo(ProjectDiagnosticRouteCells.States, "one partition class per state, none twice");
        states.Should().OnlyHaveUniqueItems();
    }
}
