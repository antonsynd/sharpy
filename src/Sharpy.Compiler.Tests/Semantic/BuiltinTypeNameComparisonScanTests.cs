using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Scan guard: identity reads on <c>BuiltinType</c> compare <c>ClrType</c>, never <c>Name</c>
/// (#1356, the #1304 failure class).
///
/// <para>
/// After the singleton collapse, <c>Name</c> is the canonical display spelling
/// (<c>int32</c>, <c>float64</c>) and identity is one singleton per CLR type. A
/// <c>bt.Name == "int32"</c> comparison silently inverts when a spelling changes — that is how
/// three emitter sites broke twice (#1304) — so name-string matches on <c>BuiltinType</c> are
/// banned outside the spelling layer (<c>PrimitiveCatalog</c>, <c>BuiltinNames</c>, the symbol
/// serializer), which this scan does not cover.
/// </para>
///
/// <para>
/// <b>How it scans.</b> Every source file under <c>src/Sharpy.Compiler/</c> except the spelling
/// layer is parsed with Roslyn. Two shapes are flagged:
/// (a) a recursive pattern <c>BuiltinType { Name: ... }</c>;
/// (b) an <c>==</c>/<c>!=</c> comparing <c>&lt;id&gt;.Name</c> to a string literal or a
/// <c>BuiltinNames</c> constant, where <c>&lt;id&gt;</c> is bound as a <c>BuiltinType</c> in the
/// same method (<c>is BuiltinType bt</c> pattern, <c>BuiltinType bt</c> local or parameter) —
/// the binding is routinely statements away from the comparison, so receivers are resolved per
/// method. Hits are allowlisted by <c>fileName::containingMethod</c>; every entry cites why the
/// read is a legitimate alias hedge or spelling read, and a stale entry fails the scan (drain
/// on fix).
/// </para>
///
/// <para>
/// <b>Named limitation:</b> a receiver whose <c>BuiltinType</c> binding lives outside the
/// comparing method (a field, or a helper's return), a chained access
/// (<c>x.Type.Name == ...</c>), or a comparand that is neither a literal nor a
/// <c>BuiltinNames</c> constant is invisible to shape (b). It is a tripwire for the dominant
/// idiom, not a proof; all #1304 sites and every site in the 2026-08 census carried the idiom.
/// </para>
/// </summary>
public class BuiltinTypeNameComparisonScanTests
{
    /// <summary>Files whose whole purpose is name spelling — out of scan scope.</summary>
    private static readonly string[] SpellingLayerFiles =
    {
        "PrimitiveCatalog.cs",
        "BuiltinNames.cs",
        "SymbolSerializer.cs",
    };

    /// <summary>
    /// Verified-legitimate hits, keyed <c>fileName::containingMethod</c>. Each entry must say
    /// why the name read is correct. Delete the entry when the site goes — stale entries fail.
    /// </summary>
    private static readonly Dictionary<string, string> Allowlist = new(StringComparer.Ordinal)
    {
        // bytes/array[uint8] interop hedge: distinguishes the uint8/byte spelling pair by design
        // (pre-ruled by the #1356 plan as a surviving alias hedge).
        ["RoslynEmitter.Expressions.Access.Calls.cs::ApplyArrayBridge"] =
            "alias hedge on the uint8/byte spelling pair (bytes interop)",
        // float32 literal suffix follows the narrowing recorded in SemanticInfo (#1301);
        // float32 has exactly one spelling, and the read is of a materialized fact.
        ["RoslynEmitter.Expressions.cs::GenerateFloatLiteral"] =
            "float32 suffix decision from the recorded narrowing (#1301); single-spelling type",
        // bytes ↔ array[uint8] assignability + LiteralStringType's str-assignability arm
        // (pre-ruled). NOTE: the method-name key covers both IsAssignableTo overloads in this file.
        ["SemanticType.cs::IsAssignableTo"] =
            "alias hedge on the uint8/byte spelling pair + literal-string assignability",
        // str/string and bool spelling reads (pre-ruled: TypeUtils.cs:66).
        ["TypeUtils.cs::IsString"] = "str/string spelling hedge",
        ["TypeUtils.cs::IsBool"] = "bool spelling read; single-spelling type",
        // Discovery collapses generic params to object; the read undoes that collapse (#889).
        ["TypeChecker.Expressions.Access.Calls.cs::NormalizeExpectedParamType"] =
            "object-collapse normalization (#889); single-spelling type",
        // StrEnum members compare equal to their backing str (#1284) — a str-spelling read.
        ["TypeChecker.Expressions.Operators.cs::CheckBinaryOp"] =
            "StrEnum == str comparison arm (#1284)",
        // str/string spelling hedge, TypeChecker's operator arm (pre-ruled: Operators.cs:1210).
        ["TypeChecker.Expressions.Operators.cs::IsStringType"] = "str/string spelling hedge",
        ["TypeChecker.Expressions.Operators.cs::IsObjectType"] =
            "object spelling read; single-spelling type",
        // StrEnum IS-a str assignability arm (#1284).
        ["TypeChecker.Utilities.cs::IsAssignable"] = "StrEnum → str assignability arm (#1284)",
        // RangeIterator is a non-primitive internal BuiltinType with no ClrType mapping and a
        // single spelling — the name IS its identity (pre-ruled by the #1356 plan).
        ["LoweringPass.Comprehensions.cs::IsSizedComprehensionSource"] =
            "non-primitive RangeIterator read; single-spelling internal type",
    };

    [Fact]
    public void BuiltinTypeIdentity_IsNeverComparedByNameString()
    {
        var compilerDir = FindCompilerSourceDirectory();
        Directory.Exists(compilerDir).Should().BeTrue(
            $"Compiler source directory should exist at {compilerDir}");

        var files = Directory.GetFiles(compilerDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !SpellingLayerFiles.Contains(Path.GetFileName(f), StringComparer.Ordinal))
            .ToList();
        files.Should().NotBeEmpty("Should find compiler source files");

        var violations = new List<string>();
        var matchedAllowlistKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();

            // Shape (a): BuiltinType { Name: ... } recursive patterns.
            foreach (var pattern in root.DescendantNodes().OfType<RecursivePatternSyntax>())
            {
                if (pattern.Type?.ToString() != "BuiltinType")
                    continue;
                var namesNameProperty = pattern.PropertyPatternClause?.Subpatterns
                    .Any(s => s.NameColon?.Name.Identifier.Text == "Name"
                              || (s.ExpressionColon?.Expression as IdentifierNameSyntax)?.Identifier.Text == "Name") ?? false;
                if (namesNameProperty)
                    Record(pattern, fileName, "BuiltinType { Name: ... } pattern");
            }

            // Shape (b): <id>.Name compared to a string literal or a BuiltinNames constant, where
            // <id> is bound as a BuiltinType somewhere in the same method (`is BuiltinType bt`,
            // `BuiltinType bt = ...`, or a BuiltinType parameter). The binding is routinely
            // statements away from the comparison, so the scan resolves receivers per method
            // rather than per statement.
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var builtinTypeIds = CollectBuiltinTypeBoundIdentifiers(method);
                if (builtinTypeIds.Count == 0)
                    continue;

                foreach (var binary in method.DescendantNodes().OfType<BinaryExpressionSyntax>())
                {
                    if (!binary.IsKind(SyntaxKind.EqualsExpression) && !binary.IsKind(SyntaxKind.NotEqualsExpression))
                        continue;
                    var comparesNameToSpelling =
                        (IsNameAccessOn(binary.Left, builtinTypeIds) && IsSpellingComparand(binary.Right))
                        || (IsNameAccessOn(binary.Right, builtinTypeIds) && IsSpellingComparand(binary.Left));
                    if (!comparesNameToSpelling)
                        continue;

                    Record(binary, fileName, $"'{binary}'");
                }
            }

            void Record(SyntaxNode node, string name, string description)
            {
                var method = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault()?.Identifier.Text ?? "<no-method>";
                var key = $"{name}::{method}";
                if (Allowlist.ContainsKey(key))
                {
                    matchedAllowlistKeys.Add(key);
                    return;
                }
                var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add($"{name}:{line} ({method}) — {description}");
            }
        }

        violations.Should().BeEmpty(
            "BuiltinType identity must be read from ClrType, never from the Name spelling — a " +
            "Name-string match inverts silently when a canonical spelling changes (#1304, #1356). " +
            "Compare bt.ClrType (or reference-compare against the SemanticType singletons); if the " +
            "read is genuinely about spelling, either move it into the spelling layer or add an " +
            "allowlist entry citing why.\nViolations:\n" + string.Join("\n", violations));

        var stale = Allowlist.Keys.Except(matchedAllowlistKeys, StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
        stale.Should().BeEmpty(
            "allowlist entries no longer matched by any scan hit — whatever removed the site must " +
            "also delete the entry (drain on fix):\n" + string.Join("\n", stale));
    }

    /// <summary>
    /// Identifiers bound as <c>BuiltinType</c> in this method: <c>is BuiltinType bt</c> patterns,
    /// <c>BuiltinType bt = ...</c> locals, and <c>BuiltinType bt</c> parameters.
    /// </summary>
    private static HashSet<string> CollectBuiltinTypeBoundIdentifiers(MethodDeclarationSyntax method)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pattern in method.DescendantNodes().OfType<DeclarationPatternSyntax>())
        {
            if (pattern.Type.ToString() == "BuiltinType"
                && pattern.Designation is SingleVariableDesignationSyntax single)
                ids.Add(single.Identifier.Text);
        }

        foreach (var pattern in method.DescendantNodes().OfType<RecursivePatternSyntax>())
        {
            if (pattern.Type?.ToString() == "BuiltinType"
                && pattern.Designation is SingleVariableDesignationSyntax single)
                ids.Add(single.Identifier.Text);
        }

        foreach (var declaration in method.DescendantNodes().OfType<VariableDeclarationSyntax>())
        {
            if (declaration.Type.ToString() == "BuiltinType")
                foreach (var variable in declaration.Variables)
                    ids.Add(variable.Identifier.Text);
        }

        foreach (var parameter in method.ParameterList.Parameters)
        {
            if (parameter.Type?.ToString() == "BuiltinType")
                ids.Add(parameter.Identifier.Text);
        }

        return ids;
    }

    /// <summary>A <c>Name</c> access on one of the BuiltinType-bound identifiers (<c>bt.Name</c>).</summary>
    private static bool IsNameAccessOn(ExpressionSyntax expr, HashSet<string> builtinTypeIds)
        => expr is MemberAccessExpressionSyntax
        {
            Name.Identifier.Text: "Name",
            Expression: IdentifierNameSyntax receiver,
        } && builtinTypeIds.Contains(receiver.Identifier.Text);

    /// <summary>A string literal or a <c>BuiltinNames.X</c> constant — the spelling comparands.</summary>
    private static bool IsSpellingComparand(ExpressionSyntax expr) => expr switch
    {
        LiteralExpressionSyntax lit => lit.IsKind(SyntaxKind.StringLiteralExpression),
        MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "BuiltinNames" } } => true,
        _ => false,
    };

    /// <summary>The <c>src/Sharpy.Compiler/</c> directory.</summary>
    private static string FindCompilerSourceDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var compilerPath = Path.Combine(current, "src", "Sharpy.Compiler");
            if (Directory.Exists(compilerPath))
                return compilerPath;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Sharpy.Compiler"));
    }
}
