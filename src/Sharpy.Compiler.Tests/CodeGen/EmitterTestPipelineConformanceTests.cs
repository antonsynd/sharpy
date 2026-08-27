using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Guards the test-side materialization boundary. A CodeGen unit test that builds its own
/// front end (<c>new TypeChecker(</c>) and hands the result to <c>new RoslynEmitter(</c>) must
/// cross the boundary the real pipeline crosses — <c>SemanticBinding.MaterializeCodeGenInfo()</c>
/// — or the emitter sees symbols with no <c>CodeGenInfo</c> and throws by contract. Five test
/// classes carried such a pipeline and passed only while the emitter still re-derived the facts;
/// they went red (25 tests) when the fallbacks were deleted (plan-c6ae1b verification @
/// 3bc6bc2a7). The cure is <see cref="EmitterTestPipeline"/>; this scan keeps a new copy from
/// re-introducing the gap. There is no exemption list: the shared helper itself contains all
/// three tokens and passes on its own merits.
/// <para><b>Mutation procedure</b> (executed once at authoring time; observation in the commit
/// body): delete the <c>MaterializeCodeGenInfo()</c> line from a copy of
/// <c>EmitterTestPipeline.cs</c> (or of any file listed by the failure) — this theory goes red
/// naming that file; restore the copy.</para>
/// </summary>
public class EmitterTestPipelineConformanceTests
{
    private const string TypeCheckerToken = "new TypeChecker(";
    private const string EmitterToken = "new RoslynEmitter(";
    private const string MaterializeToken = "MaterializeCodeGenInfo(";

    [Fact]
    public void EveryHandRolledEmitterPipeline_CrossesTheMaterializationBoundary()
    {
        var dir = FindCodeGenTestDirectory();
        var violations = Directory.EnumerateFiles(dir, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => IsHandRolledPipelineWithoutMaterialization(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        violations.Should().BeEmpty(
            "a CodeGen test that constructs TypeChecker + RoslynEmitter itself must call " +
            "SemanticBinding.MaterializeCodeGenInfo() before emitting (or use EmitterTestPipeline)");
    }

    /// <summary>Positive control: the detector flags exactly the hand-rolled-without-materialization shape.</summary>
    [Theory]
    [InlineData("var t = new TypeChecker(a, b, c, d); var e = new RoslynEmitter(ctx);", true)]
    [InlineData("var t = new TypeChecker(a, b, c, d); binding.MaterializeCodeGenInfo(); var e = new RoslynEmitter(ctx);", false)]
    [InlineData("var t = new TypeChecker(a, b, c, d);", false)]
    [InlineData("var e = new RoslynEmitter(ctx);", false)]
    [InlineData("// new TypeChecker( and new RoslynEmitter( only in a comment still count", true)]
    public void Detector_FlagsOnlyTheUnmaterializedShape(string text, bool expected)
        => IsHandRolledPipelineWithoutMaterialization(text).Should().Be(expected);

    private static bool IsHandRolledPipelineWithoutMaterialization(string text)
        => text.Contains(TypeCheckerToken, StringComparison.Ordinal)
            && text.Contains(EmitterToken, StringComparison.Ordinal)
            && !text.Contains(MaterializeToken, StringComparison.Ordinal);

    private static string FindCodeGenTestDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException("sharpy.sln not found above " + AppContext.BaseDirectory);
        var path = Path.Combine(dir.FullName, "src", "Sharpy.Compiler.Tests", "CodeGen");
        if (!Directory.Exists(path))
            throw new InvalidOperationException("CodeGen test directory not found: " + path);
        return path;
    }
}
