using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// The parser has two line terminators and they are not interchangeable.
/// <c>ExpectStatementEnd()</c> accepts Newline, <c>Dedent</c>, EOF and post-<c>Dedent</c>;
/// <c>ExpectNewline()</c> accepts Newline and tolerates EOF, and is therefore only correct
/// <em>mid-construct</em> — where more input is required on a later line, so a <c>Dedent</c> really
/// is an error.
///
/// <para>#1233 was reported as "an indented <c>const</c> at end of file fails to parse", but the
/// narrow contract sat on fourteen statement-terminal productions, not one: <c>const</c>,
/// <c>raise</c>, <c>assert</c>, <c>import</c>, <c>from ... import</c>, enum members, union cases,
/// type aliases, inline stubs, and all four body-less declaration forms. Each failed as the last
/// line of an indented block in a file with no trailing newline (token stream <c>Dedent, EOF</c>),
/// and each also mis-parsed a block-consuming initializer such as <c>const Y = match x: ...</c> at
/// any position in the file. Nothing about the type system stops the fifteenth from being written
/// the same way — this test does.</para>
///
/// <para><b>Scan:</b> every <c>ExpectNewline()</c> invocation in the parser must be immediately
/// followed, in its own block, by <c>Expect(TokenType.Indent)</c>. That is the mechanical signature
/// of a mid-construct terminator: the newline exists so that an indented suite can begin. Anything
/// else is either a statement terminator wearing the wrong contract, or a mid-construct site of a
/// different shape that must be named in <see cref="Allowlist"/> with its reason.</para>
///
/// <para><b>Scope (deliberate):</b> the converse — that a statement production actually calls a
/// terminator at all — is not checked, since a production may legitimately delegate its terminator
/// to a helper. The behavioural half lives in <c>UnterminatedFinalLineParserTests</c>, which parses
/// real byte-exact sources; this is the source guard that keeps that matrix from silently going out
/// of date as productions are added.</para>
/// </summary>
public class StatementTerminatorConformanceTests
{
    /// <summary>
    /// Mid-construct <c>ExpectNewline()</c> sites that are <em>not</em> followed by an
    /// <c>Indent</c>. Key = "<c>{fileName} :: {memberName}</c>"; every entry states what the newline
    /// is waiting for, which is never "nothing".
    /// </summary>
    private static readonly Dictionary<string, string> Allowlist = new(StringComparer.Ordinal)
    {
        ["Parser.cs :: ParseDecoratedStatement"] =
            "Both decorator arms (@name and @[attribute]). A decorator is a prefix: the statement it "
            + "decorates must follow on a later line, so Dedent/EOF here is a decorator with no target.",
    };

    [Fact]
    public void EveryExpectNewlineSite_IsMidConstruct()
    {
        var violations = new List<string>();

        foreach (var (fileName, root) in ParserSyntaxTrees())
        {
            foreach (var (statement, memberName) in ExpectNewlineStatements(root))
            {
                if (IsFollowedByIndentExpectation(statement))
                    continue;

                var key = $"{fileName} :: {memberName}";
                if (Allowlist.ContainsKey(key))
                    continue;

                var line = statement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add($"{key}  [{fileName}:{line}]");
            }
        }

        violations.Should().BeEmpty(
            "ExpectNewline() is the mid-construct terminator: it rejects Dedent, so a statement "
            + "production that ends with it cannot be the last line of an indented block in a file "
            + "with no trailing newline (#1233). Use ExpectStatementEnd() to end a statement, or add "
            + "the printed key to the Allowlist with the reason more input is genuinely required.\n"
            + "Violations:\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// The allowlist must not rot: an entry naming a member that no longer has a bare
    /// <c>ExpectNewline()</c> is a blind spot with nothing behind it.
    /// </summary>
    [Fact]
    public void Allowlist_HasNoStaleEntries()
    {
        var live = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (fileName, root) in ParserSyntaxTrees())
        {
            foreach (var (statement, memberName) in ExpectNewlineStatements(root))
            {
                if (!IsFollowedByIndentExpectation(statement))
                    live.Add($"{fileName} :: {memberName}");
            }
        }

        Allowlist.Keys.Where(k => !live.Contains(k)).Should().BeEmpty(
            "delete allowlist entries whose site is gone or now precedes an Indent expectation");
    }

    // --- Scan -----------------------------------------------------------------------------------

    private static IEnumerable<(ExpressionStatementSyntax Statement, string MemberName)>
        ExpectNewlineStatements(CompilationUnitSyntax root)
    {
        foreach (var statement in root.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            if (statement.Expression is not InvocationExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "ExpectNewline" },
                    ArgumentList.Arguments.Count: 0
                })
                continue;

            var member = statement.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            yield return (statement, member?.Identifier.ValueText ?? "<unknown>");
        }
    }

    /// <summary>
    /// True when the next statement in the same block is <c>Expect(TokenType.Indent)</c> — the
    /// mid-construct signature. Intervening statements are not tolerated: a newline that something
    /// else happens after is exactly the shape that hides a statement terminator.
    /// </summary>
    private static bool IsFollowedByIndentExpectation(ExpressionStatementSyntax statement)
    {
        if (statement.Parent is not BlockSyntax block)
            return false;

        var index = block.Statements.IndexOf(statement);
        if (index < 0 || index + 1 >= block.Statements.Count)
            return false;

        return block.Statements[index + 1] is ExpressionStatementSyntax
        {
            Expression: InvocationExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "Expect" },
                ArgumentList.Arguments.Count: 1
            } invocation
        } && invocation.ArgumentList.Arguments[0].ToString() == "TokenType.Indent";
    }

    private static IEnumerable<(string FileName, CompilationUnitSyntax Root)> ParserSyntaxTrees()
    {
        var dir = Path.Combine(FindSourceDir("Sharpy.Compiler"), "Parser");
        Directory.Exists(dir).Should().BeTrue($"the parser source directory must be locatable (looked in {dir})");

        var files = Directory.GetFiles(dir, "Parser*.cs", SearchOption.TopDirectoryOnly);
        files.Should().NotBeEmpty("the scan is vacuous if it finds no parser sources");

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            yield return (Path.GetFileName(file), (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text).GetRoot());
        }
    }

    private static string FindSourceDir(string project)
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", project);
            if (Directory.Exists(path))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", project));
    }
}
