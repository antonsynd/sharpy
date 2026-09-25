using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// One mistake, one diagnostic (#2075): a declaration whose annotation FAILED to resolve (and was
/// reported) is annotated-but-unknown — error recovery — not unannotated. Its initializer used to be
/// checked as if the target had no annotation, so a <c>None</c>, <c>None()</c> or empty-collection
/// initializer added SPY0227 "cannot infer a type" (sorted first, telling the user to annotate what
/// they had annotated), and a declaration with no initializer added SPY0240 ("declared with 'auto'
/// must have an initializer"). The resolver marks such an annotation
/// (<c>SemanticInfo.MarkAnnotationErrorRecovery</c>) and the declaration check suppresses the
/// cascade; a genuinely unannotated target still reports SPY0227 (the positive controls).
/// </summary>
[Collection("HeavyCompilation")]
public class AnnotationErrorRecoveryCascadeTests : IntegrationTestBase
{
    public AnnotationErrorRecoveryCascadeTests(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> ErroredAnnotations() => new[]
    {
        new object[] { "unknown_type_none", "def main() -> None:\n    x: Foo = None\n    print(1)\n", DiagnosticCodes.Semantic.UndefinedType },
        new object[] { "unknown_type_none_ctor", "def main() -> None:\n    x: Foo = None()\n    print(1)\n", DiagnosticCodes.Semantic.UndefinedType },
        new object[] { "unknown_type_empty_list", "def main() -> None:\n    x: Foo = []\n    print(1)\n", DiagnosticCodes.Semantic.UndefinedType },
        new object[] { "unknown_type_module", "X: Foo = None\n\ndef main() -> None:\n    print(1)\n", DiagnosticCodes.Semantic.UndefinedType },
        new object[] { "unknown_type_field", "class C:\n    x: Foo = None\n\ndef main() -> None:\n    print(1)\n", DiagnosticCodes.Semantic.UndefinedType },
        // No initializer: the errored annotation is not an initializer-less `auto` (SPY0240).
        new object[] { "unknown_type_no_initializer", "class C:\n    v: Foo\n\ndef main() -> None:\n    print(1)\n", DiagnosticCodes.Semantic.UndefinedType },
        new object[] { "none_local", "def main() -> None:\n    x: None = None\n    print(1)\n", DiagnosticCodes.SemanticOverflow.NoneAnnotationInValuePosition },
        new object[] { "none_field", "class C:\n    x: None = None\n\ndef main() -> None:\n    print(1)\n", DiagnosticCodes.SemanticOverflow.NoneAnnotationInValuePosition },
        new object[] { "none_optional_ctor", "def main() -> None:\n    x: None? = None()\n    print(1)\n", DiagnosticCodes.SemanticOverflow.NoneAnnotationInValuePosition },
        new object[] { "none_alias", "type Unit = None\n\ndef main() -> None:\n    x: Unit = None\n    print(1)\n", DiagnosticCodes.SemanticOverflow.NoneAnnotationInValuePosition },
    };

    [Theory]
    [MemberData(nameof(ErroredAnnotations))]
    public void AnErroredAnnotation_IsReportedOnce_WithNoCannotInferCascade(string cell, string source, string code)
    {
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{cell}]");
        var errors = result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).ToList();
        errors.Should().ContainSingle($"[{cell}] {string.Join(" | ", errors.Select(d => d.Code + " " + d.Message))}");
        errors[0].Code.Should().Be(code, $"[{cell}] the annotation's own diagnostic is the one report");
    }

    /// <summary>
    /// Positive controls: an UNANNOTATED target with nothing to infer from still reports SPY0227 —
    /// the suppression keys on an annotation that errored, not on the initializer's shape.
    /// </summary>
    [Theory]
    [InlineData("none", "def main() -> None:\n    x = None\n    print(1)\n")]
    [InlineData("none_ctor", "def main() -> None:\n    x = None()\n    print(1)\n")]
    [InlineData("empty_list", "def main() -> None:\n    x = []\n    print(1)\n")]
    public void AnUnannotatedUninferableTarget_StillReportsSpy0227(string cell, string source)
    {
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{cell}]");
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.CannotInferType,
            $"[{cell}] {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
    }
}
