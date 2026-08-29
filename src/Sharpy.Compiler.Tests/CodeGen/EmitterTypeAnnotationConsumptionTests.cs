using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Critical Rule 2, annotation-consumption ratchet: expression/pattern generator methods in the
/// emitter must read materialized semantic types, never raw <c>TypeAnnotation</c> AST nodes.
/// A <c>MapType(TypeAnnotation)</c> call in such a method means the emitter is deriving a C# type
/// from the written syntax rather than from the decision semantic analysis already recorded.
///
/// <para>
/// <b>Scope.</b> Scans <c>RoslynEmitter.Expressions*.cs</c> and <c>RoslynEmitter.Patterns.cs</c>
/// for invocations of <c>MapType</c> inside methods returning <c>ExpressionSyntax</c> or
/// <c>PatternSyntax</c>. The exempt method is <see cref="MapClassifiedTypeOperandExemption"/>:
/// <c>MapClassifiedTypeOperand</c> is the MODEL for fact-first reading and its fallback call is
/// legitimate.
/// </para>
///
/// <para>
/// <b>Ratchet.</b> Per-file call counts are compared against
/// <c>emitter-annotation-consumption-allowlist.txt</c>. A new call exceeding the budget fails;
/// a file dropping below its entry is stale and also fails (drain on fix).
/// </para>
/// </summary>
public class EmitterTypeAnnotationConsumptionTests
{
    private readonly ITestOutputHelper _output;

    public EmitterTypeAnnotationConsumptionTests(ITestOutputHelper output) => _output = output;

    private const string ExemptMethod = "MapClassifiedTypeOperand";

    private static readonly string[] TargetReturnTypes =
    {
        "ExpressionSyntax",
        "PatternSyntax",
    };

    private static readonly string[] TargetFilePatterns =
    {
        "RoslynEmitter.Expressions",
        "RoslynEmitter.Patterns",
    };

    internal readonly record struct CallSite(string File, int Line, string EnclosingMethod, string Source)
    {
        public override string ToString() => $"{File}:{Line} in {EnclosingMethod} — {Source}";
    }

    // ---- the guard ---------------------------------------------------------------------------

    [Fact]
    public void ExpressionPatternGenerators_RespectAnnotationConsumptionBudget()
    {
        var sites = ScanEmitterSources();
        var allowlist = LoadAllowlist();

        _output.WriteLine($"MapType calls in expression/pattern generators: {sites.Count}");
        foreach (var site in sites)
            _output.WriteLine($"  {site}");

        var grouped = sites.GroupBy(s => s.File).ToDictionary(g => g.Key, g => g.Count());

        var overBudget = new List<string>();
        foreach (var (file, count) in grouped)
        {
            var budget = allowlist.GetValueOrDefault(file, 0);
            if (count > budget)
            {
                overBudget.Add(
                    $"{file}: {count} calls, budget {budget}"
                    + (budget == 0 ? " (no allowlist entry)" : ""));
            }
        }

        overBudget.Should().BeEmpty(
            "expression/pattern generator methods must read materialized semantic types, not raw "
            + "TypeAnnotation AST nodes (Critical Rule 2). A MapType(TypeAnnotation) call means "
            + "the emitter is deriving a C# type from the written syntax instead of from the "
            + "decision semantic analysis recorded. Move the decision into semantic analysis and "
            + "materialize it onto SemanticInfo. If a call cannot be fixed now, file an issue and "
            + "add an allowlist entry.\nOver budget:\n  " + string.Join("\n  ", overBudget));
    }

    [Fact]
    public void Allowlist_HasNoStaleEntries()
    {
        var sites = ScanEmitterSources();
        var grouped = sites.GroupBy(s => s.File).ToDictionary(g => g.Key, g => g.Count());
        var allowlist = LoadAllowlist();

        var stale = new List<string>();
        foreach (var (file, budget) in allowlist)
        {
            var actual = grouped.GetValueOrDefault(file, 0);
            if (actual < budget)
            {
                stale.Add($"{file}: budget {budget}, actual {actual} — lower or remove the entry");
            }
        }

        stale.Should().BeEmpty(
            "allowlist entries must be updated in the same commit that removes calls "
            + "(drain on fix).\nStale:\n  " + string.Join("\n  ", stale));
    }

    // ---- mutation cells ----------------------------------------------------------------------

    [Fact]
    public void SyntheticMapTypeCall_InExpressionGenerator_IsFlagged()
    {
        var source = @"
            partial class RoslynEmitter {
                private ExpressionSyntax GenerateSomething(ListLiteral list) {
                    var t = _typeMapper.MapType(list.ElementTypeHint);
                    return LiteralExpression(SyntaxKind.NullLiteralExpression);
                }
            }";

        var sites = ScanSource("Mutation.cs", source);
        sites.Should().NotBeEmpty(
            "a MapType call in an ExpressionSyntax-returning method is exactly what this "
            + "ratchet guards against");
    }

    [Fact]
    public void SyntheticMapTypeCall_InPatternGenerator_IsFlagged()
    {
        var source = @"
            partial class RoslynEmitter {
                private PatternSyntax GeneratePattern(TypePattern tp) {
                    var t = _typeMapper.MapType(tp.Type);
                    return ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression));
                }
            }";

        var sites = ScanSource("Mutation.cs", source);
        sites.Should().NotBeEmpty(
            "a MapType call in a PatternSyntax-returning method is exactly what this "
            + "ratchet guards against");
    }

    [Fact]
    public void CarrierOnlySource_IsNotFlagged()
    {
        var source = @"
            partial class RoslynEmitter {
                private MemberDeclarationSyntax GenerateDeclaration(ClassDefinition cls) {
                    var t = _typeMapper.MapType(cls.BaseType);
                    return ClassDeclaration(""C"");
                }
            }";

        var sites = ScanSource("Control.cs", source);
        sites.Should().BeEmpty(
            "a MapType call in a MemberDeclarationSyntax-returning method is a declaration "
            + "generator, not an expression/pattern generator — it must not be flagged.\n"
            + "Flagged:\n  " + string.Join("\n  ", sites));
    }

    [Fact]
    public void MapTypeCall_InExemptMethod_IsNotFlagged()
    {
        var source = @"
            partial class RoslynEmitter {
                private TypeSyntax MapClassifiedTypeOperand(TypeAnnotation annotation)
                    => _context.SemanticInfo?.GetTypeTestLowering(annotation) is { } lowering
                        ? MapTypeTestTarget(lowering)
                        : _typeMapper.MapType(annotation);
            }";

        var sites = ScanSource("Control.cs", source);
        sites.Should().BeEmpty(
            "MapClassifiedTypeOperand is exempt — it is the model for fact-first reading and "
            + "its MapType fallback is legitimate.\nFlagged:\n  " + string.Join("\n  ", sites));
    }

    // ---- scan infrastructure -----------------------------------------------------------------

    private static List<CallSite> ScanEmitterSources()
    {
        var directory = EmitterBannedTokenScanTests.FindCodeGenSourceDirectory();
        Directory.Exists(directory).Should().BeTrue($"CodeGen source directory should exist at {directory}");

        var files = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                return TargetFilePatterns.Any(p =>
                    name.StartsWith(p, StringComparison.Ordinal));
            })
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        files.Should().NotBeEmpty("should find emitter expression/pattern source files");

        return files.SelectMany(f => ScanSource(Path.GetFileName(f), File.ReadAllText(f))).ToList();
    }

    internal static List<CallSite> ScanSource(string fileName, string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var lines = source.Split('\n');
        var sites = new List<CallSite>();

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var methodName = invocation.Expression switch
            {
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                IdentifierNameSyntax id => id.Identifier.Text,
                _ => null,
            };

            if (methodName != "MapType")
                continue;

            var enclosingMethod = invocation.Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();
            if (enclosingMethod == null)
                continue;

            if (enclosingMethod.Identifier.Text == ExemptMethod)
                continue;

            var returnType = enclosingMethod.ReturnType.ToString();
            if (!TargetReturnTypes.Any(t => returnType.Contains(t, StringComparison.Ordinal)))
                continue;

            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var text = line - 1 < lines.Length ? lines[line - 1].Trim() : string.Empty;
            sites.Add(new CallSite(fileName, line, enclosingMethod.Identifier.Text, text));
        }

        return sites;
    }

    private static Dictionary<string, int> LoadAllowlist()
    {
        var directory = EmitterBannedTokenScanTests.FindCodeGenSourceDirectory();
        var path = Path.Combine(directory, "emitter-annotation-consumption-allowlist.txt");
        File.Exists(path).Should().BeTrue($"allowlist file should exist at {path}");

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            parts.Length.Should().BeGreaterThanOrEqualTo(2,
                $"allowlist line must be 'FileName Count #IssueRef', got: {line}");

            var file = parts[0];
            int.TryParse(parts[1], out var count).Should().BeTrue(
                $"allowlist count must be an integer, got '{parts[1]}' in: {line}");

            result[file] = count;
        }

        return result;
    }
}
