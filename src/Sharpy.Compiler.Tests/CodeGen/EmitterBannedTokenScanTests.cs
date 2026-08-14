using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// A <b>five-substring scan</b> of every source file under <c>src/Sharpy.Compiler/CodeGen/</c>,
/// plus one method-scoped token scan of the generic-reference dispatch (#1039, #1041, #1175).
///
/// <para>
/// <b>What this class checks — and, since #1475, what it says it checks.</b> It searches
/// comment-stripped CodeGen source text for five literal substrings and fails if any occurs:
/// </para>
///
/// <list type="number">
///   <item><description>
///     <c>TypeInferenceService</c> (also matching <c>GenericTypeInferenceService</c>) — the two
///     inference engines, by name.
///   </description></item>
///   <item><description>
///     <c>System.Reflection</c>, <c>BindingFlags</c>, <c>GetCustomAttribute</c>,
///     <c>GetIndexParameters</c> — four spellings that appear in most reflection code. CLR
///     inspection belongs to <c>Discovery</c>/semantic (<c>Discovery.ClrTypeBridge</c>,
///     <c>Discovery.ClrTypeHelper</c>), materialized for the emitter.
///   </description></item>
/// </list>
///
/// <para>
/// <b>This is NOT Critical Rule 2 enforcement.</b> Rule 2 says the emitter makes no type or
/// lowering decisions; the central violation it describes — a type decision taken emitter-side —
/// carries none of these five substrings, so a green result here is evidence about five names and
/// nothing more. Reading it as purity evidence is exactly the false assurance #1475 was filed for
/// (the 2026-08-13 verification round cited this suite's name twice and had to re-verify by hand
/// both times). Rule 2 is enforced structurally by
/// <see cref="EmitterCarrierOnlyConformanceTests"/>, which inverts the question: CodeGen may name
/// only the materialized-fact carriers, and every other <c>Semantic</c> type — enumerated by
/// reflection, so the list cannot go stale — is denied. This scan is kept as a cheap backstop for
/// the five spellings it does know, not as the guard the rule names.
/// </para>
///
/// <para>
/// Comments are stripped before scanning so a doc-comment that merely mentions a banned type by
/// name (as historical context) is not a violation; only real code references are flagged. The
/// emitter's sanctioned API is Roslyn's <c>SyntaxFactory</c> (the
/// <c>Microsoft.CodeAnalysis.CSharp</c> namespaces), none of whose construction spellings contain
/// a banned token, so no per-token allowlist is needed.
/// </para>
///
/// <para>
/// The third scan is <b>method-scoped</b> rather than file-wide (#1175): within the
/// generic-reference dispatch — <c>GenerateGenericReferenceCall</c> and every method in its file it
/// transitively calls — <c>LookupSymbol</c> and <c>GetCallTarget</c> are banned, because that
/// lowering must read the materialized <c>GenericReference</c> fact and nothing else. Those two are
/// legitimate in the ordinary call arms of the same file, so the flat whole-file scan above cannot
/// express the rule; the scoped guard walks the call graph with Roslyn instead (the
/// <c>WrapperNodeUnwrapConformanceTests</c> idiom). See
/// <see cref="GenericReferenceDispatch_LowersFromTheFact_WithoutCallTargetRederivation"/>.
/// </para>
/// </summary>
public class EmitterBannedTokenScanTests
{
    /// <summary>Substrings banned from CodeGen source (matched against comment-stripped code).</summary>
    private static readonly string[] BannedTokens =
    {
        // Inference engines — decisions belong in semantic analysis.
        "TypeInferenceService",       // also matches GenericTypeInferenceService (superstring)
        // Reflection — CLR inspection belongs in Discovery/semantic.
        "System.Reflection",
        "BindingFlags",
        "GetCustomAttribute",
        "GetIndexParameters",
    };

    [Fact]
    public void CodeGenSources_ContainNoBannedTokenSubstrings()
    {
        var codeGenDir = FindCodeGenSourceDirectory();
        Directory.Exists(codeGenDir).Should().BeTrue(
            $"CodeGen source directory should exist at {codeGenDir}");

        var files = Directory.GetFiles(codeGenDir, "*.cs", SearchOption.AllDirectories);
        files.Should().NotBeEmpty("Should find CodeGen source files");

        var violations = new List<string>();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var code = StripLineComment(lines[i]);
                foreach (var token in BannedTokens)
                {
                    if (code.Contains(token, StringComparison.Ordinal))
                        violations.Add($"{fileName}:{i + 1} — references banned '{token}': {lines[i].Trim()}");
                }
            }
        }

        violations.Should().BeEmpty(
            "CodeGen source may not contain these five substrings: the two inference engines by name, " +
            "and four spellings that appear in most reflection code. Move the decision into semantic " +
            "analysis and materialize it onto SemanticInfo/CodeGenInfo (and, for a new SemanticInfo " +
            "dictionary, add it to SemanticInfo.MergeFrom so it survives the per-file → project merge " +
            "codegen reads from); put CLR inspection in Discovery.\n" +
            "NOTE: this scan checks five substrings, not Critical Rule 2 — an emitter-side type " +
            "decision carries none of them. Rule 2 is enforced by EmitterCarrierOnlyConformanceTests " +
            "(#1475).\nViolations:\n" +
            string.Join("\n", violations));
    }

    // ---- scoped guard: the generic-reference dispatch (#1175) --------------------------------

    /// <summary>The file holding the generic-reference dispatch and its per-kind emission bodies.</summary>
    private const string DispatchFileName = "RoslynEmitter.Expressions.Access.cs";

    /// <summary>The single entry point every <c>callee[T, ...]</c> lowering goes through (#1143).</summary>
    private const string DispatchEntryMethod = "GenerateGenericReferenceCall";

    /// <summary>
    /// Call-target re-derivation the dispatch must never do: <c>LookupSymbol</c> re-resolves the callee
    /// through the symbol table and <c>GetCallTarget</c> re-reads the call's bound symbol, both of which
    /// the <c>GenericReference</c> fact already carries. Note these are legitimate elsewhere in the same
    /// file (the ordinary call arms use them), which is exactly why this guard is method-scoped rather
    /// than added to <see cref="BannedTokens"/>.
    /// </summary>
    private static readonly string[] CallTargetRederivationTokens = { "LookupSymbol", "GetCallTarget" };

    /// <summary>
    /// #1175: the generic-reference lowering must read the materialized <c>GenericReference</c> fact and
    /// nothing else. #1143 collapsed a five-helper cascade that re-derived callee shape emitter-side and
    /// #1164 removed the last fact-less arm, but nothing mechanically stopped a new arm — or a "small
    /// fix" inside an existing one — from reaching back for the symbol table. This does.
    ///
    /// <para>Scope is the dispatch method plus every method in the same file it transitively calls
    /// (the per-kind emission bodies and their same-class helpers), discovered syntactically with
    /// Roslyn. Bodies only, so a doc comment naming a token is not a violation. There is deliberately
    /// no exemption list: at the time of writing the scope is clean, and the correct response to a new
    /// need for a lookup is to add a field to the fact.</para>
    /// </summary>
    [Fact]
    public void GenericReferenceDispatch_LowersFromTheFact_WithoutCallTargetRederivation()
    {
        var dispatchFile = Path.Combine(FindCodeGenSourceDirectory(), DispatchFileName);
        File.Exists(dispatchFile).Should().BeTrue(
            $"the generic-reference dispatch is expected in {DispatchFileName}; if it moved, update this guard");

        var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText(dispatchFile)).GetRoot();
        var methodsByName = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .GroupBy(m => m.Identifier.Text, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        methodsByName.Should().ContainKey(DispatchEntryMethod,
            $"{DispatchEntryMethod} is the generic-reference lowering seam this guard protects");

        var scope = CollectTransitiveCallScope(methodsByName, DispatchEntryMethod);
        scope.Count.Should().BeGreaterThan(1,
            "the scope must reach the per-kind emission bodies, not just the dispatch switch — " +
            "a scope of one means the call-graph walk stopped working and the guard is vacuous");

        var violations = new List<string>();
        foreach (var methodName in scope.OrderBy(n => n, StringComparer.Ordinal))
        {
            foreach (var method in methodsByName[methodName])
            {
                var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
                if (body is null)
                    continue;

                foreach (var id in body.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    var token = Array.Find(CallTargetRederivationTokens,
                        t => string.Equals(id.Identifier.Text, t, StringComparison.Ordinal));
                    if (token == null)
                        continue;

                    var line = id.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    violations.Add($"{DispatchFileName}:{line} — {methodName} re-derives the call target via '{token}'");
                }
            }
        }

        violations.Should().BeEmpty(
            "generic-reference lowering reads the materialized fact — extend the resolver, not the emitter. " +
            $"Every method reachable from {DispatchEntryMethod} lowers a callee[T, ...] whose kind, target " +
            "symbol, receiver type and type arguments the GenericReferenceResolver already decided and " +
            "recorded in SemanticInfo (Critical Rule 2 pattern (b); #1143, #1164). If an emission body needs " +
            "something it cannot read from the GenericReference, that is a MISSING FIELD ON THE FACT: add it " +
            "in Semantic/GenericReferenceResolver.cs and record it there. Do not re-resolve the callee " +
            "emitter-side — that is the cascade #1143 deleted, and it silently diverges from what semantic " +
            "analysis decided.\nViolations:\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// The transitive set of same-file method names reachable from <paramref name="entry"/> through
    /// unqualified (same-class) invocations — <c>Foo(...)</c>, <c>Foo&lt;T&gt;(...)</c> and
    /// <c>this.Foo(...)</c>. Invocations on other receivers (<c>_typeMapper.MapType(...)</c>) are calls
    /// out of scope, and names not declared in this file (Roslyn's static-imported
    /// <c>SyntaxFactory</c> helpers) are simply absent from the method table.
    /// </summary>
    private static HashSet<string> CollectTransitiveCallScope(
        Dictionary<string, List<MethodDeclarationSyntax>> methodsByName, string entry)
    {
        var scope = new HashSet<string>(StringComparer.Ordinal) { entry };
        var queue = new Queue<string>();
        queue.Enqueue(entry);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var method in methodsByName[current])
            {
                var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
                if (body is null)
                    continue;

                foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var callee = CalleeName(invocation.Expression);
                    if (callee == null || !methodsByName.ContainsKey(callee) || !scope.Add(callee))
                        continue;
                    queue.Enqueue(callee);
                }
            }
        }

        return scope;
    }

    /// <summary>The simple name of an unqualified or <c>this.</c>-qualified call, else null.</summary>
    private static string? CalleeName(ExpressionSyntax invoked) => invoked switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        GenericNameSyntax generic => generic.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: SimpleNameSyntax name } =>
            name.Identifier.Text,
        _ => null,
    };

    /// <summary>
    /// Removes a single-line <c>//</c> comment from a line so that a banned type named only in a
    /// comment (historical context) is not treated as a code reference. Naive: does not attempt to
    /// honor <c>//</c> inside string literals — a banned token inside a CodeGen string literal would
    /// itself be suspicious and is intentionally still flagged.
    /// </summary>
    private static string StripLineComment(string line)
    {
        var idx = line.IndexOf("//", StringComparison.Ordinal);
        return idx >= 0 ? line.Substring(0, idx) : line;
    }

    /// <summary>
    /// The <c>src/Sharpy.Compiler/CodeGen/</c> directory. Shared with
    /// <c>EmitterCarrierOnlyConformanceTests</c> so the two purity guards agree on which sources
    /// they judge — one location, not two copies that can drift apart.
    /// </summary>
    internal static string FindCodeGenSourceDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var codeGenPath = Path.Combine(current, "src", "Sharpy.Compiler", "CodeGen");
            if (Directory.Exists(codeGenPath))
                return codeGenPath;
            current = Directory.GetParent(current)?.FullName;
        }

        // Fallback: relative from the test assembly location.
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Sharpy.Compiler", "CodeGen"));
    }
}
