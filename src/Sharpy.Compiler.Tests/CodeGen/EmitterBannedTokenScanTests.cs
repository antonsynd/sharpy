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

    // ---- scoped guard: operator / statement / access lowering reads facts, not types (#1623, #1618) ----

    /// <summary>
    /// The Batch-6 class guard (plan-c6ae1b D7, #1618 census, #1623 umbrella): every operator, statement
    /// and multi-axis-access lowering decision is a fact recorded by semantic analysis
    /// (<c>OperatorLowering</c>, <c>IterationLowering</c>, <c>StatementLowering</c>,
    /// <c>MultiAxisAccessLowering</c>, <c>BinaryOpLowering</c>), so the emitter methods that consume
    /// them may switch on a tag but may never inspect a semantic type, a CLR type, or an AST shape to
    /// pick a lowering. Each root below is walked transitively over its same-file callees exactly like
    /// <see cref="GenericReferenceDispatch_LowersFromTheFact_WithoutCallTargetRederivation"/>, and every
    /// token in <see cref="TypeDispatchTokens"/> / member access in <see cref="TypeDispatchMemberAccesses"/>
    /// / <c>typeof(...)</c> found in a body is a violation. No root is exempt and there is no allowlist:
    /// a new generator joins by being LISTED here, so an unlisted one is a loud review gap rather than a
    /// silent pass (the exemption must never be the subject).
    ///
    /// <para><b>Mutation procedure</b> (executed once at authoring time; record the observation in the commit
    /// body): re-introduce one dispatch in a copy of the production file, e.g. in
    /// <c>GenerateBinaryOp</c> add <c>if (GetExpressionSemanticType(binOp) is UserDefinedType) { }</c>,
    /// run this theory — the <c>GenerateBinaryOp</c> row must go red naming that line — then restore the
    /// file from the copy. The positive control below
    /// (<see cref="TypeDispatchDetector_FlagsEachBannedShape_PositiveControl"/>) proves the detector itself
    /// recognises every banned shape, so an all-green run is not vacuous.</para>
    ///
    /// <para><b>The walk is same-file.</b> A callee that lives in a different <c>RoslynEmitter.*.cs</c>
    /// partial is not reached from a root in another one, so a cross-partial callee joins this table as a
    /// root of its OWN file — the precedent is the <c>//</c> / <c>%</c> routing seams
    /// <c>GenerateFloorDivideValue</c> / <c>GenerateModuloValue</c> (<c>RoslynEmitter.Operators.cs</c>),
    /// called from <c>GenerateBinaryOp</c> and <c>GenerateAugmentedValue</c> and listed below since #1658
    /// (their rows were red against the pre-#1658 emitter: <c>IsDecimalOperand</c> / <c>IsFloatExpression</c>
    /// at the roots and <c>SemanticType.Int</c> / <c>SemanticType.Long</c> in the reached
    /// <c>IsFlooredNumericOperand</c>).</para>
    /// </summary>
    [Theory]
    [InlineData("RoslynEmitter.Expressions.Operators.cs", "GenerateBinaryOp")]
    [InlineData("RoslynEmitter.Expressions.Operators.cs", "GenerateComparisonChain")]
    [InlineData("RoslynEmitter.Expressions.Operators.cs", "GenerateUnaryOp")]
    [InlineData("RoslynEmitter.Statements.Assignments.cs", "GenerateAugmentedValue")]
    [InlineData("RoslynEmitter.Statements.Assignments.cs", "GenerateNullCoalesceValue")]
    [InlineData("RoslynEmitter.Operators.cs", "GeneratePowerValue")]
    [InlineData("RoslynEmitter.Operators.cs", "GenerateFloorDivideValue")]
    [InlineData("RoslynEmitter.Operators.cs", "GenerateModuloValue")]
    [InlineData("RoslynEmitter.Expressions.Access.cs", "GenerateMultiAxisAccess")]
    [InlineData("RoslynEmitter.Statements.cs", "GenerateExpressionStatement")]
    [InlineData("RoslynEmitter.Expressions.Comprehensions.cs", "GenerateComprehensionIterator")]
    [InlineData("RoslynEmitter.Statements.ControlFlow.cs", "GenerateFor")]
    [InlineData("RoslynEmitter.TypeDeclarations.cs", "GenerateEnumValuesIterator")]
    public void OperatorDispatchRoots_LowerFromTheRecordedFact_WithoutTypeDispatch(string fileName, string rootMethod)
    {
        var file = Path.Combine(FindCodeGenSourceDirectory(), fileName);
        File.Exists(file).Should().BeTrue(
            $"{rootMethod} is expected in {fileName}; if it moved, update this guard's root table");

        var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
        var methodsByName = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .GroupBy(m => m.Identifier.Text, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        methodsByName.Should().ContainKey(rootMethod,
            $"{rootMethod} is a lowering root this guard protects; a rename must update the root table, " +
            "not silently drop the root");

        var scope = CollectTransitiveCallScope(methodsByName, rootMethod);
        scope.Should().Contain(rootMethod);

        var violations = new List<string>();
        foreach (var methodName in scope.OrderBy(n => n, StringComparer.Ordinal))
        {
            foreach (var method in methodsByName[methodName])
            {
                foreach (var (line, what) in FindTypeDispatch(method))
                    violations.Add($"{fileName}:{line} — {methodName} (reached from {rootMethod}) decides by {what}");
            }
        }

        violations.Should().BeEmpty(
            "operator/statement/access lowering must switch on the RECORDED fact (OperatorLowering, " +
            "IterationLowering, StatementLowering, MultiAxisAccessLowering, BinaryOpLowering strategy) " +
            "and never re-derive the decision from a semantic type, a CLR type, or an AST shape " +
            "(Critical Rule 2 pattern (b); #1618 census, #1623 umbrella, plan-c6ae1b D4–D7). If an " +
            "emission body needs something the fact does not carry, that is a MISSING TAG: add it to the " +
            "enum, record it in the TypeChecker, and read it here.\nViolations:\n" +
            string.Join("\n", violations));
    }

    /// <summary>
    /// Identifier spellings that mark an emitter-side type/shape decision inside the operator roots:
    /// the <c>SemanticType</c> subclasses (allowed as carriers elsewhere — <see cref="EmitterCarrierOnlyConformanceTests"/>
    /// permits them file-wide, which is exactly why this scan is method-scoped), the CLR-type accessor,
    /// and the deleted emitter-side predicates. <c>IsStringEnumSymbol</c> is deliberately absent: it is a
    /// symbol-keyed materialized fact (#1284) the plan allows the iteration arm to read.
    /// </summary>
    private static readonly string[] TypeDispatchTokens =
    {
        "UserDefinedType", "GenericType", "OptionalType", "TypeParameterType", "BuiltinType",
        "ClrType", "HasComparableConstraint", "IsFloatExpression", "IsDecimalExpression", "IsDecimalOperand",
        "IsFlooredNumericOperand",
    };

    /// <summary>
    /// Qualified member accesses that are type dispatch: <c>SemanticType.Str/Long/Int</c> comparisons and
    /// <c>TypeKind.Enum</c>. Stored as (qualifier, member) pairs so <c>Semantic.TypeKind.Enum</c> is caught too.
    /// </summary>
    private static readonly (string Qualifier, string Member)[] TypeDispatchMemberAccesses =
    {
        ("SemanticType", "Str"), ("SemanticType", "Long"), ("SemanticType", "Int"), ("TypeKind", "Enum"),
    };

    /// <summary>
    /// Every banned shape in one method body, as (line, description). Bodies only — a doc comment naming
    /// a token is not a violation.
    /// </summary>
    private static IEnumerable<(int Line, string What)> FindTypeDispatch(MethodDeclarationSyntax method)
    {
        var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null)
            yield break;

        foreach (var node in body.DescendantNodes())
        {
            switch (node)
            {
                case IdentifierNameSyntax id when Array.IndexOf(TypeDispatchTokens, id.Identifier.Text) >= 0:
                    yield return (LineOf(id), $"'{id.Identifier.Text}'");
                    break;
                case MemberAccessExpressionSyntax { Name: SimpleNameSyntax member } access
                    when TypeDispatchMemberAccesses.Any(p =>
                        string.Equals(p.Member, member.Identifier.Text, StringComparison.Ordinal)
                        && string.Equals(p.Qualifier, RightmostName(access.Expression), StringComparison.Ordinal)):
                    yield return (LineOf(access), $"'{RightmostName(access.Expression)}.{member.Identifier.Text}'");
                    break;
                case TypeOfExpressionSyntax typeOf:
                    yield return (LineOf(typeOf), "'typeof(...)'");
                    break;
            }
        }
    }

    private static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static string? RightmostName(ExpressionSyntax expr) => expr switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax { Name: SimpleNameSyntax name } => name.Identifier.Text,
        _ => null,
    };

    /// <summary>
    /// Positive control for <see cref="FindTypeDispatch"/>: each banned shape, planted in a synthetic method,
    /// must be reported — so an all-green <see cref="OperatorDispatchRoots_LowerFromTheRecordedFact_WithoutTypeDispatch"/>
    /// is evidence about the emitter, not about a detector that matches nothing.
    /// </summary>
    [Theory]
    [InlineData("if (t is UserDefinedType u) { }", "'UserDefinedType'")]
    [InlineData("var b = t is GenericType;", "'GenericType'")]
    [InlineData("var b = t is OptionalType;", "'OptionalType'")]
    [InlineData("var b = t is TypeParameterType;", "'TypeParameterType'")]
    [InlineData("var b = t is BuiltinType;", "'BuiltinType'")]
    [InlineData("var c = bt.ClrType;", "'ClrType'")]
    [InlineData("var c = bt.ClrType == typeof(int);", "'typeof(...)'")]
    [InlineData("var b = HasComparableConstraint(t);", "'HasComparableConstraint'")]
    [InlineData("var b = IsFloatExpression(e);", "'IsFloatExpression'")]
    [InlineData("var b = IsDecimalExpression(e);", "'IsDecimalExpression'")]
    [InlineData("var b = IsDecimalOperand(e);", "'IsDecimalOperand'")]
    [InlineData("var b = IsFlooredNumericOperand(e);", "'IsFlooredNumericOperand'")]
    [InlineData("var b = t == SemanticType.Str;", "'SemanticType.Str'")]
    [InlineData("var b = t == SemanticType.Long;", "'SemanticType.Long'")]
    [InlineData("var b = t == SemanticType.Int;", "'SemanticType.Int'")]
    [InlineData("var b = k == Semantic.TypeKind.Enum;", "'TypeKind.Enum'")]
    public void TypeDispatchDetector_FlagsEachBannedShape_PositiveControl(string statement, string expected)
    {
        var source = "class C { void M() { " + statement + " } }";
        var method = CSharpSyntaxTree.ParseText(source).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

        FindTypeDispatch(method).Select(v => v.What).Should().Contain(expected,
            "the detector must recognise this shape, otherwise the scoped scan passes vacuously");

        var clean = CSharpSyntaxTree.ParseText("class C { void M() { var k = lowering?.Kind; } }").GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        FindTypeDispatch(clean).Should().BeEmpty("a fact read is not type dispatch");
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
