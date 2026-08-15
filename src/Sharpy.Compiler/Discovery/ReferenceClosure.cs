namespace Sharpy.Compiler.Discovery;

/// <summary>
/// The assemblies a project's <c>.spyproj</c> brings in, as seen from VALIDATION time (Phase 5).
///
/// <para>#1492: the SPY0495 absence proof consulted loaded assemblies and the shared framework
/// only. A project's references are consumed in Phase 7 (<c>AssemblyCompiler</c>), long after
/// Phase 5 has already refused — so a bracket attribute whose type lives only in a referenced
/// assembly was declared absent by a proof that had never looked where it lives.</para>
///
/// <para>Two kinds, kept apart because only one can be proved against:</para>
/// <list type="bullet">
///   <item><description><see cref="AssemblyPaths"/> — direct <c>&lt;Reference&gt;</c> entries that
///   resolve to an existing file right now. These are PROBED, so a name that is absent from them
///   is genuinely absent and refusing it is sound.</description></item>
///   <item><description><see cref="HasUnprobedReferences"/> — references that cannot be reduced to
///   a path before Phase 5 (an unresolved <c>PackageReference</c>, or a <c>&lt;Reference&gt;</c>
///   whose file is not there yet). Nothing can be proved about these, so a project that has any
///   must not have absence REFUSED on its behalf.</description></item>
/// </list>
///
/// <para>The distinction is the whole point. A blanket pass-through — "this project has
/// references, so stop refusing" — would reopen the #1146 leak for every project with any
/// reference at all, turning a clean SPY0495 back into a CS0246 behind SPY0908. The downgrade is
/// scoped to projects that actually carry something unprobeable.</para>
/// </summary>
internal sealed record ReferenceClosure(
    IReadOnlyList<string> AssemblyPaths,
    bool HasUnprobedReferences)
{
    /// <summary>
    /// No references and nothing unprobed — the single-file and REPL shapes, and the default
    /// everywhere the closure is not threaded. Behaves exactly as the pre-#1492 proof did.
    /// </summary>
    internal static readonly ReferenceClosure Empty =
        new(Array.Empty<string>(), HasUnprobedReferences: false);

    /// <summary>Whether there is anything here worth probing.</summary>
    internal bool IsEmpty => AssemblyPaths.Count == 0 && !HasUnprobedReferences;
}
