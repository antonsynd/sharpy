using System.Reflection;

namespace Sharpy.TestInfrastructure;

/// <summary>
/// The coverage-on test mode (#2512, plan-88736c D1): <c>dotnet test --collect "Code Coverage"
/// --settings build_tools/coverage.runsettings</c> statically instruments each included product
/// assembly (Sharpy.Compiler, .Core, .Stdlib, .Lsp, sharpyc) before the tests load it. The assembly a
/// reflection or IL roster then reads is not the shipped one: the collector injects a tracker type
/// (<c>Microsoft.CodeCoverage.Instrumentation.Static.Tracker.StaticManagedTrackerTemplate_&lt;guid&gt;</c>,
/// 16 mutable statics) and a hit probe (<c>ldsfld</c> of the tracker's buffer) into every method body.
/// A roster over the running assembly either excludes the injected type by
/// <see cref="IsInjected(Type)"/> or, when the probes change what it measures (an IL body's size or
/// shape), skips under <see cref="IsInstrumented(Assembly)"/>.
/// </summary>
public static class CoverageInstrumentation
{
    /// <summary>The namespace the collector's static instrumentation injects its types under.</summary>
    public const string InjectedNamespacePrefix = "Microsoft.CodeCoverage.Instrumentation";

    /// <summary>True for a type the collector injected; never true for Sharpy code.</summary>
    public static bool IsInjected(Type type) =>
        type.Namespace != null && type.Namespace.StartsWith(InjectedNamespacePrefix, StringComparison.Ordinal);

    /// <summary>True when <paramref name="assembly"/> was statically instrumented by the collector.</summary>
    public static bool IsInstrumented(Assembly assembly) => assembly.GetTypes().Any(IsInjected);
}
