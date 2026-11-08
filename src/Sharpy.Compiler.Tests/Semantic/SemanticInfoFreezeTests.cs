using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #2443 (E2 contract item 3): <see cref="SemanticInfo.Freeze"/> closes the node-keyed side-tables
/// to writes, and the project compiler freezes the merged instance before the lowering pass. The
/// symbol-keyed analogue is <c>SemanticBinding.FreezeVariableTypes</c>.
/// </summary>
public class SemanticInfoFreezeTests
{
    /// <summary>
    /// Every mutator on <see cref="SemanticInfo"/> (every public instance <c>void</c> method but
    /// <see cref="SemanticInfo.Freeze"/>, plus the <c>CurrentFilePath</c> setter) throws
    /// <see cref="PhaseViolationException"/> once the instance is frozen. The roster is reflected, so
    /// a setter added without the guard fails here. Arguments are defaults: the guard runs before
    /// any other work, so a null key still reaches it.
    /// </summary>
    [Fact]
    public void EveryMutator_ThrowsPhaseViolation_AfterFreeze()
    {
        var mutators = Mutators().ToList();
        // 89 @ P82 tranche 1 (#2443): three setters were deleted with their tables
        // (SetAugmentedAssignMutation, SetLambdaBodyLowering, SetOptionalStoreWrap); 88 @ P82
        // tranche 2 (SetMatchScrutineeLowering). Re-measure and lower the floor only when a setter
        // is deleted by design, never because the filter changed.
        mutators.Should().HaveCountGreaterThanOrEqualTo(88,
            "the reflected roster must see every setter; a shrunk count means the filter changed");

        var frozen = new SemanticInfo();
        frozen.Freeze();
        frozen.IsFrozen.Should().BeTrue();

        var unguarded = mutators
            .Where(m => !(Invoke(frozen, m) is PhaseViolationException))
            .Select(m => m.Name)
            .ToList();

        unguarded.Should().BeEmpty(
            "every SemanticInfo mutator must refuse writes after Freeze() (#2443); these did not:\n"
            + string.Join("\n", unguarded));
    }

    /// <summary>
    /// Positive control for the sweep above: on an unfrozen instance no mutator throws
    /// <see cref="PhaseViolationException"/>, so the throws above come from the freeze and not
    /// from the fabricated arguments.
    /// </summary>
    [Fact]
    public void NoMutator_ThrowsPhaseViolation_BeforeFreeze()
    {
        var violating = Mutators()
            .Where(m => Invoke(new SemanticInfo(), m) is PhaseViolationException)
            .Select(m => m.Name)
            .ToList();

        violating.Should().BeEmpty("an unfrozen SemanticInfo accepts writes");
    }

    /// <summary>
    /// The real phase boundary: after a project compile, the merged project-level SemanticInfo that
    /// lowering and code generation read is frozen, so a write after validation throws instead of
    /// silently changing a fact the emitted program was already built from.
    /// </summary>
    [Fact]
    public void ProjectCompile_FreezesMergedSemanticInfo_BeforeLowering()
    {
        using var helper = new ProjectCompilationHelper();
        helper.WithRootNamespace("SemanticInfoFreeze")
            .AddSourceFile("lib.spy", """
                def helper() -> int:
                    return 41
                """)
            .AddSourceFile("main.spy", """
                from lib import helper

                def main() -> None:
                    print(helper() + 1)
                """)
            .CreateProjectFile();

        var result = helper.Compile();
        result.Success.Should().BeTrue(string.Join("\n", result.Diagnostics.GetErrors().Select(e => e.Message)));

        var merged = result.ProjectModel?.SemanticInfo;
        merged.Should().NotBeNull();

        var lateWrite = () => merged!.SetExpressionType(new Identifier { Name = "late" }, SemanticType.Int);
        lateWrite.Should().Throw<PhaseViolationException>(
                "a write to the merged SemanticInfo after validation must not pass silently (#2443)")
            .Which.Operation.Should().Be("write _expressionTypes");
        merged!.IsFrozen.Should().BeTrue("ProjectCompiler freezes the merged SemanticInfo before lowering");
    }

    private static IEnumerable<MethodInfo> Mutators()
    {
        var methods = typeof(SemanticInfo)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(void) && !m.IsSpecialName && m.Name != nameof(SemanticInfo.Freeze));
        var currentFilePathSetter = typeof(SemanticInfo)
            .GetProperty(nameof(SemanticInfo.CurrentFilePath))!
            .GetSetMethod(nonPublic: true)!;
        return methods.Append(currentFilePathSetter);
    }

    /// <summary>Invokes <paramref name="method"/> with default arguments and returns what it threw, if anything.</summary>
    private static Exception? Invoke(SemanticInfo target, MethodInfo method)
    {
        var args = method.GetParameters()
            .Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
            .ToArray();
        try
        {
            method.Invoke(target, args);
            return null;
        }
        catch (TargetInvocationException ex)
        {
            return ex.InnerException;
        }
    }
}
