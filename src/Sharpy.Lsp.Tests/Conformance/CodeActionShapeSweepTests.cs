using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;
using Sharpy.Lsp.Refactoring;
using Sharpy.TestInfrastructure.Formatting;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
// The enclosing `Sharpy` namespace exposes Core's Pythonic collections, which shadow System's; SCG-qualify.
using SCG = System.Collections.Generic;
using CompilerNullLogger = Sharpy.Compiler.Logging.NullLogger;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using RoslynSyntax = Microsoft.CodeAnalysis.CSharp.Syntax;
using SModule = Sharpy.Compiler.Parser.Ast.Module;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// The code-action shape sweep of P47 (#2232): <b>a code action that moves or re-emits existing source text keeps
/// the source's shape.</b> It drives the REAL <c>textDocument/codeAction</c> handler with the seven production
/// providers through <see cref="LspCodeActionDriver"/>, applies each action's edits the way a client must
/// (<see cref="LspFormattingDriver.ApplyStrict"/>), and judges the APPLIED text — never the edits alone.
///
/// <para><b>Actions</b> (<see cref="Actions"/>): the fourteen title prefixes the seven providers can offer, each with
/// a SHAPE class that fixes which buckets judge it — <c>Move</c> (re-emits a region of lines: every bucket),
/// <c>Insert</c> (adds lines: no <c>line</c>/<c>blank</c>), <c>Delete</c> (removes lines and writes no text: no
/// <c>line</c>/<c>blank</c>/<c>lineBreak</c>) and <c>Rewrite</c> (changes one line's text: no <c>line</c>/<c>blank</c>).
/// The roster is total over the title literals of <c>src/Sharpy.Lsp/Refactoring/*.cs</c>
/// (<see cref="Actions_AreTotalOverTheTitleLiteralsOfRefactoring"/>, a Roslyn scan that also resolves
/// <c>Title = local</c> to the local's initializer and takes both arms of a conditional).</para>
///
/// <para><b>Requests</b> (<see cref="BuildRequests"/>, every one the AST admits, 0-based LSP positions):
/// <c>whole</c> — a cursor at (0,0); <c>if</c>/<c>match</c> — a cursor at each <c>if</c>/<c>match</c> statement's
/// start; <c>decl</c> — a cursor at each variable declaration's name; <c>class</c> — at each class/struct name;
/// <c>call</c> — at each call's start; <c>body</c> — every statement body selected whole, from its first statement's
/// line start to its last statement's TRUE last line end (<see cref="SweepText.EndLine"/>: a multi-line literal's tail
/// included); <c>first</c> — each multi-statement body's first statement alone; <c>expr</c> — each declaration
/// initializer and assignment value selected exactly; <c>diag</c> — each published diagnostic whose code has a quick
/// fix (<see cref="QuickFixCodes"/>), handed to the handler as the request's context. An action is JUDGED on the
/// request kinds that own it (<see cref="ActionSpec.Owners"/>) — the kind the sweep built for that action's target,
/// so the facts a bucket exempts (a converted statement's re-spelled header lines, a deleted statement's lines) are
/// computed by the sweep from its own request, never read from the provider. The same action offered on a request
/// it does not own (Organize Imports on every request of an importing document) is counted, not re-judged; equal
/// (action, edits) pairs within one document are judged once.</para>
///
/// <para><b>Twins</b>: <c>identity</c>, <c>crlf</c> (<see cref="FormatterTwins.Crlf"/>), <c>cr</c>
/// (<see cref="FormatterTwins.Cr"/>, the line break a <c>\n</c>-only split misses), <c>comment</c>
/// (<see cref="FormatterTwins.CommentInjected"/>: comments at every anchor kind — own-line, block end, before a
/// clause keyword, inside brackets, line-trailing).</para>
///
/// <para><b>Buckets</b>, <c>S</c> the document and <c>T</c> the applied text:
/// <list type="bullet">
/// <item><c>edits</c> — the strict applier threw (the edits are not a legal LSP edit list for S);</item>
/// <item><c>offer</c> — on the <c>crlf</c>/<c>cr</c> twin, the action set offered for a request differs from the
/// <c>identity</c> twin's for the same request (a line break must not change what is offered). Judged on those two
/// twins ONLY: an injected comment legitimately changes what a request covers, so offer changes on the
/// <c>comment</c> twin are deliberately unjudged;</item>
/// <item><c>parse</c> — S lexes and parses clean and T does not;</item>
/// <item><c>diagnostics</c> — the error-code multiset of <see cref="CompilerApi.Analyze(string, CancellationToken)"/>
/// on T differs from S's, judged when T parses (a T that does not is <c>parse</c>'s verdict; sampled in regular mode, see
/// the theories);</item>
/// <item><c>comment</c> — the comment TRIVIA of T (re-lexed, never a substring) lacks a comment of S, or holds one S
/// does not, other than the comments on the lines the action deletes as whole statements (the declared removals,
/// D5: the unused import a quick fix or Organize Imports deletes, the declaration Inline variable deletes) — those
/// may go;</item>
/// <item><c>line</c> (<c>Move</c>) — a non-blank line of the replaced region of S (the whole lines the edits touch),
/// other than a header the action re-spells and a declared removal, does not reappear in T's new region with the
/// region's ONE indentation offset δ (clamped at column 0), or a line that starts inside a multi-line literal does
/// not reappear byte-identical;</item>
/// <item><c>blank</c> (<c>Move</c>) — the region's blank lines decrease (Organize Imports' own block exempt, D4), or
/// an edit reaches outside the whole lines of the moved statements (from the first one's line start to the line
/// after the last one's true last line), swallowing an edge blank line;</item>
/// <item><c>lineBreak</c> — a line break of T is not S's line break. Sound only on a source whose breaks are
/// uniform; every corpus fixture is <c>\n</c>-only, so every twin is uniform by construction, and a non-uniform
/// source is a test error (<see cref="SweepText.UniformBreak"/>), not a verdict.</item>
/// </list></para>
///
/// <para><b>Sweeps and pins.</b> One fact per twin sweeps every stem of the corpus (<see cref="SweepCorpus"/>, shared with the
/// formatting route sweep) and aggregates the FAILING STEMS per pin key (action, twin, bucket, cite — the cite is the
/// cell's cause by contract clause, <see cref="CiteOf"/>). <c>Conformance/code-action-shape-allowlist.txt</c> pins each key
/// as <c>action twin bucket fullCount fullDigest regularCount regularDigest # #issue reason</c>, the digest being the first
/// 16 hex characters of SHA-256 over the sorted, <c>\n</c>-joined stems (the <c>KnownLimitPin</c> precedent of the formatting
/// sweep: a long-lived red class is a pinned count, not a per-stem row). A key's count AND digest must equal its pin
/// (<see cref="ComparePins"/>): a new failing stem, a fixed one, or a fix swapped for a regression is red; an unpinned
/// failing key is red; a pinned key that holds everywhere is red until its row is deleted (drain on fix). On a mismatch the
/// actual stem lists go to <c>.claude/tmp/code-action-sweep/</c> (the path is in the message) and up to 20 stems inline.</para>
///
/// <para><b>Budget and the two pairs.</b> Regular mode runs <c>identity</c> on every stem and the other three twins on the
/// stems whose <see cref="SweepCorpus.StableBucket"/> under <see cref="TwinStride"/> is 0, and judges <c>diagnostics</c> (one
/// extra semantic analysis per cell) on the cells whose bucket under <see cref="DiagnosticsStride"/> is 0. The sample is
/// deterministic, so each pin carries a second pair: the failing stems regular mode judges. A FULL run records for every
/// failing cell whether regular mode judges it (its stem runs in regular mode, and a <c>diagnostics</c> cell is sampled), so
/// one FULL run measures both pairs and checks both; regular mode checks the regular pair, and is red on a regression in a
/// sampled stem. <c>SHARPY_CODEACTION_SWEEP_FULL=1</c> runs everything and writes every failing cell and the measured pin
/// rows to <c>.claude/tmp/code-action-sweep/</c> (how the allowlist is populated). Stems run in parallel within a twin
/// (<c>SHARPY_CODEACTION_SWEEP_DOP</c>, default min(4, processors)); the driver retains no document after a twin.</para>
/// </summary>
[TestCaseOrderer("Sharpy.Lsp.Tests.Conformance.CodeActionShapeSweepOrderer", "Sharpy.Lsp.Tests")]
public sealed class CodeActionShapeSweepTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public CodeActionShapeSweepTests(ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
    }

    // ================================================================
    // Actions, shapes, buckets, twins
    // ================================================================

    /// <summary>What an action does to the document's lines — fixes the buckets that judge it.</summary>
    internal enum ShapeClass
    {
        /// <summary>Re-emits a region of existing lines (every bucket).</summary>
        Move,

        /// <summary>Inserts new lines at a computed position.</summary>
        Insert,

        /// <summary>Deletes whole statements and writes no text.</summary>
        Delete,

        /// <summary>Changes the text of one line (or one expression).</summary>
        Rewrite,
    }

    // ---- request kinds ----
    internal const string WholeRequest = "whole";
    internal const string IfRequest = "if";
    internal const string MatchRequest = "match";
    internal const string DeclRequest = "decl";
    internal const string ClassRequest = "class";
    internal const string CallRequest = "call";
    internal const string BodyRequest = "body";
    internal const string FirstRequest = "first";
    internal const string ExprRequest = "expr";
    internal const string DiagRequest = "diag";

    /// <summary>One roster row: the row name (no spaces — it is an allowlist field), the title prefix, the shape class and the request kinds that own it.</summary>
    internal sealed record ActionSpec(string Name, string Prefix, ShapeClass Shape, string[] Owners);

    /// <summary>
    /// The fourteen title prefixes of the seven providers. <c>Implement</c> covers both
    /// <c>Implement interface '…'</c> and <c>Implement all interfaces</c> (D9).
    /// </summary>
    internal static readonly ActionSpec[] Actions =
    {
        new("organize-imports", "Organize Imports", ShapeClass.Move, new[] { WholeRequest }),
        new("wrap-try", "Wrap in try/except", ShapeClass.Move, new[] { BodyRequest, FirstRequest }),
        new("to-match", "Convert to match statement", ShapeClass.Move, new[] { IfRequest }),
        new("to-if", "Convert to if/elif/else", ShapeClass.Move, new[] { MatchRequest }),
        new("add-annotation", "Add type annotation", ShapeClass.Rewrite, new[] { DeclRequest }),
        new("remove-annotation", "Remove type annotation", ShapeClass.Rewrite, new[] { DeclRequest }),
        new("extract-method", "Extract method", ShapeClass.Move, new[] { BodyRequest, FirstRequest }),
        new("extract-variable", "Extract variable", ShapeClass.Rewrite, new[] { ExprRequest }),
        new("inline-variable", "Inline variable", ShapeClass.Rewrite, new[] { DeclRequest }),
        new("inline-function", "Inline function", ShapeClass.Rewrite, new[] { CallRequest }),
        new("implement", "Implement", ShapeClass.Insert, new[] { ClassRequest }),
        new("remove-import", "Remove unused import", ShapeClass.Delete, new[] { DiagRequest }),
        new("prefix-underscore", "Prefix with '_'", ShapeClass.Rewrite, new[] { DiagRequest }),
        new("rename", "Rename to", ShapeClass.Rewrite, new[] { DiagRequest }),
    };

    /// <summary>The roster row whose prefix starts <paramref name="title"/>, or null.</summary>
    internal static ActionSpec? ActionOf(string title)
    {
        ActionSpec? found = null;
        foreach (var spec in Actions)
        {
            if (!title.StartsWith(spec.Prefix, StringComparison.Ordinal))
                continue;
            if (found is not null)
                throw new InvalidOperationException($"title '{title}' matches two roster prefixes: '{found.Prefix}' and '{spec.Prefix}'");
            found = spec;
        }

        return found;
    }

    // ---- buckets ----
    internal const string Edits = "edits";
    internal const string Offer = "offer";
    internal const string Parse = "parse";
    internal const string Diagnostics = "diagnostics";
    internal const string CommentBucket = "comment";
    internal const string Line = "line";
    internal const string Blank = "blank";
    internal const string LineBreak = "lineBreak";
    internal static readonly string[] Buckets = { Edits, Offer, Parse, Diagnostics, CommentBucket, Line, Blank, LineBreak };

    /// <summary>The buckets that judge an action of each shape class.</summary>
    internal static readonly SCG.IReadOnlyDictionary<ShapeClass, string[]> BucketsOf = new SCG.Dictionary<ShapeClass, string[]>
    {
        [ShapeClass.Move] = new[] { Edits, Offer, Parse, Diagnostics, CommentBucket, Line, Blank, LineBreak },
        [ShapeClass.Insert] = new[] { Edits, Offer, Parse, Diagnostics, LineBreak, CommentBucket },
        [ShapeClass.Delete] = new[] { Edits, Offer, Parse, Diagnostics, CommentBucket },
        [ShapeClass.Rewrite] = new[] { Edits, Offer, Parse, Diagnostics, LineBreak, CommentBucket },
    };

    // ---- twins ----
    internal const string Identity = "identity";
    internal const string Crlf = "crlf";
    internal const string Cr = "cr";
    internal const string CommentTwin = "comment";
    internal static readonly string[] Twins = { Identity, Crlf, Cr, CommentTwin };

    /// <summary>The twins whose <c>offer</c> parity against <see cref="Identity"/> is judged.</summary>
    internal static readonly string[] OfferTwins = { Crlf, Cr };

    internal static string TwinOf(string source, string twin) => twin switch
    {
        Identity => source,
        Crlf => FormatterTwins.Crlf(source),
        Cr => FormatterTwins.Cr(source),
        CommentTwin => FormatterTwins.CommentInjected(source).Text,
        _ => throw new ArgumentOutOfRangeException(nameof(twin), twin, "unknown twin"),
    };

    /// <summary>
    /// The diagnostic codes <c>DiagnosticQuickFixProvider</c> has a fix for — read from its private
    /// <c>FixFactories</c> registry, so a code added there gets <c>diag</c> requests without touching the sweep.
    /// </summary>
    internal static readonly Lazy<SCG.IReadOnlySet<string>> QuickFixCodes = new(() =>
    {
        var field = typeof(DiagnosticQuickFixProvider).GetField("FixFactories", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DiagnosticQuickFixProvider.FixFactories is gone; the diag requests read it");
        var registry = (SCG.IReadOnlyDictionary<string, Func<CodeActionProviderContext, Diagnostic, CodeAction?>>)field.GetValue(null)!;
        return new SCG.HashSet<string>(registry.Keys, StringComparer.Ordinal);
    });

    // ================================================================
    // The document facts the buckets read
    // ================================================================

    /// <summary>
    /// A text as the buckets read it: its lines and breaks (<see cref="LineDiff.Split"/> — <c>\r\n</c>, <c>\n</c> and a
    /// lone <c>\r</c> each end a line), its trivia-preserving tokens, its AST when it parses, its comments, and the
    /// 1-based lines that start inside a multi-line literal (<see cref="LiteralSpans"/>).
    /// </summary>
    internal sealed class SweepText
    {
        private SCG.List<string>? _errorCodes;

        private SweepText(string text)
        {
            Text = text;
            (Lines, Breaks) = LineDiff.Split(text);
            LineStarts = new int[Lines.Count];
            var offset = 0;
            for (var i = 0; i < Lines.Count; i++)
            {
                LineStarts[i] = offset;
                offset += Lines[i].Length + (i < Breaks.Count ? Breaks[i].Length : 0);
            }

            var lex = FileCompilationPipeline.Lex(new SourceText(text, "<code-action-sweep>"), CompilerNullLogger.Instance, preserveTrivia: true);
            Tokens = lex.Tokens;
            if (!lex.HasErrors)
            {
                var parse = FileCompilationPipeline.Parse(lex.Tokens, CompilerNullLogger.Instance);
                Module = parse.HasErrors ? null : parse.Module;
                var error = parse.HasErrors ? parse.Diagnostics.GetErrors().FirstOrDefault() : null;
                (FirstError, FirstErrorLine, FirstErrorColumn) = (error?.ToString(), error?.Line, error?.Column);
            }
            else
            {
                var error = lex.Diagnostics.GetErrors().FirstOrDefault();
                (FirstError, FirstErrorLine, FirstErrorColumn) = (error?.ToString(), error?.Line, error?.Column);
            }

            Comments = CommentsOf(Tokens);
            LiteralLines = LiteralSpans.LinesStartingInside(text, LiteralSpans.Of(Tokens));
        }

        public static SweepText Of(string text) => new(text);

        public string Text { get; }

        public SCG.List<string> Lines { get; }

        public SCG.List<string> Breaks { get; }

        /// <summary>The offset of each line's first character.</summary>
        public int[] LineStarts { get; }

        public IReadOnlyList<Token> Tokens { get; }

        /// <summary>The AST when the text lexes and parses clean; null otherwise.</summary>
        public SModule? Module { get; }

        public bool Parses => Module is not null;

        public string? FirstError { get; }

        /// <summary>The first error's 1-based line and column, when it has them.</summary>
        public int? FirstErrorLine { get; }

        public int? FirstErrorColumn { get; }

        /// <summary>Every comment (trivia kind <c>Comment</c>), text without trailing spaces/tabs, with its 1-based line.</summary>
        public SCG.IReadOnlyList<(string Text, int Line)> Comments { get; }

        /// <summary>1-based numbers of the lines whose first character lies inside a literal.</summary>
        public SCG.HashSet<int> LiteralLines { get; }

        /// <summary>
        /// The one line break of the text (<c>\n</c> when it has none), or null when its breaks are mixed — on which
        /// the <c>lineBreak</c> oracle has no answer (<see cref="LineDiff.DocumentLineBreak(string)"/> is the first).
        /// </summary>
        public string? UniformBreak => Breaks.Distinct(StringComparer.Ordinal).Count() switch
        {
            0 => "\n",
            1 => Breaks[0],
            _ => null,
        };

        public static bool IsBlankLine(string line) => line.All(c => c is ' ' or '\t' or '\f');

        public static string IndentOf(string line)
        {
            var i = 0;
            while (i < line.Length && line[i] is ' ' or '\t')
                i++;
            return line[..i];
        }

        /// <summary>The offset of an LSP position (assumed legal for this text).</summary>
        public int Offset(Position position) => LineStarts[position.Line] + position.Character;

        /// <summary>The 0-based line holding <paramref name="offset"/> (the line whose content or break it lies in).</summary>
        public int LineOfOffset(int offset)
        {
            var index = Array.BinarySearch(LineStarts, offset);
            return index >= 0 ? index : ~index - 1;
        }

        /// <summary>
        /// A statement's TRUE last line (1-based): <c>max(stmt.LineEnd, …)</c> over the end line of every token of the
        /// statement — from its start offset, starting on or before <c>LineEnd</c> — whose span ends on a later line. The
        /// parser records a node's <c>LineEnd</c> as its last token's START line (#2295), so a statement ending in a
        /// multi-line literal "ends" on the literal's first line; the lexer's span (<see cref="Token.Length"/>) has the
        /// end. Monotone, so it is an identity once the parser records the end.
        /// </summary>
        public int EndLine(Statement statement)
        {
            var end = statement.LineEnd;
            if (statement.LineStart < 1 || statement.LineStart > Lines.Count)
                return end;
            var start = LineStarts[statement.LineStart - 1] + Math.Max(0, statement.ColumnStart - 1);
            foreach (var token in Tokens)
            {
                if (token.Position < start || token.Length <= 0
                    || token.Type is TokenType.Newline or TokenType.Indent or TokenType.Dedent or TokenType.Eof)
                {
                    continue;
                }

                if (token.Line > statement.LineEnd)
                    break;
                var last = LineOfOffset(token.Position + token.Length - 1) + 1;
                if (last > end)
                    end = last;
            }

            return end;
        }

        /// <summary>The error-severity codes of <see cref="CompilerApi.Analyze(string, CancellationToken)"/> on this text, sorted (computed once).</summary>
        public SCG.List<string> ErrorCodes(CompilerApi api)
        {
            if (_errorCodes is null)
            {
                var analysis = api.Analyze(Text, CancellationToken.None);
                _errorCodes = analysis.Diagnostics.Where(d => d.IsError).Select(d => d.Code ?? "(no code)")
                    .OrderBy(c => c, StringComparer.Ordinal).ToList();
            }

            return _errorCodes;
        }

        private static SCG.List<(string Text, int Line)> CommentsOf(IReadOnlyList<Token> tokens)
        {
            // FormatterService.CommentsOf's reading: every comment trivia, leading or trailing, text TrimEnd'ed.
            var result = new SCG.List<(string, int)>();
            foreach (var token in tokens)
            {
                Add(token.LeadingTrivia);
                Add(token.TrailingTrivia);
            }

            return result;

            void Add(IReadOnlyList<Trivia>? trivia)
            {
                if (trivia is null)
                    return;
                foreach (var t in trivia)
                {
                    if (t.Kind == TriviaKind.Comment)
                        result.Add((t.Text.TrimEnd(' ', '\t'), t.Line));
                }
            }
        }
    }

    // ================================================================
    // Requests
    // ================================================================

    /// <summary>
    /// One request the sweep sends: its kind, range and quick-fix context, the statement it targets (the converted
    /// <c>if</c>/<c>match</c>, the declaration), the 1-based line span of the statements a selection covers, and a key
    /// that names it identically on every line-break twin.
    /// </summary>
    internal sealed record SweepRequest(
        string Kind, LspRange Range, Diagnostic? Diagnostic, Statement? Target, (int First, int Last)? Statements, string Key);

    /// <summary>Every request the document's AST admits (see the class summary), de-duplicated by key, in a stable order.</summary>
    internal static SCG.List<SweepRequest> BuildRequests(SweepText doc, IReadOnlyList<Diagnostic> published)
    {
        var requests = new SCG.List<SweepRequest>();
        var keys = new SCG.HashSet<string>(StringComparer.Ordinal);
        void Add(string kind, LspRange range, Diagnostic? diagnostic = null, Statement? target = null, (int, int)? statements = null)
        {
            var key = $"{kind}@{range.Start.Line}:{range.Start.Character}-{range.End.Line}:{range.End.Character}"
                + (diagnostic is null ? "" : $"#{diagnostic.Code?.String}");
            if (keys.Add(key))
                requests.Add(new SweepRequest(kind, range, diagnostic, target, statements, key));
        }

        Add(WholeRequest, Cursor(0, 0));
        if (doc.Module is { } module)
        {
            var (nodes, bodies) = Walk(module);
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case IfStatement s when s.LineStart > 0:
                        Add(IfRequest, Cursor(s.LineStart - 1, s.ColumnStart - 1), target: s);
                        break;
                    case MatchStatement s when s.LineStart > 0:
                        Add(MatchRequest, Cursor(s.LineStart - 1, s.ColumnStart - 1), target: s);
                        break;
                    case VariableDeclaration s when s.NameLineStart > 0:
                        Add(DeclRequest, Cursor(s.NameLineStart - 1, s.NameColumnStart - 1), target: s);
                        break;
                    case ClassDef s when s.NameLineStart > 0:
                        Add(ClassRequest, Cursor(s.NameLineStart - 1, s.NameColumnStart - 1), target: s);
                        break;
                    case StructDef s when s.NameLineStart > 0:
                        Add(ClassRequest, Cursor(s.NameLineStart - 1, s.NameColumnStart - 1), target: s);
                        break;
                    case FunctionCall c when c.LineStart > 0:
                        Add(CallRequest, Cursor(c.LineStart - 1, c.ColumnStart - 1));
                        break;
                }
            }

            foreach (var body in bodies)
            {
                if (body.Length == 0 || body[0].LineStart < 1)
                    continue;
                Add(BodyRequest, Lines(doc, body[0], body[^1]), statements: (body[0].LineStart, doc.EndLine(body[^1])));
                if (body.Length > 1)
                    Add(FirstRequest, Lines(doc, body[0], body[0]), statements: (body[0].LineStart, doc.EndLine(body[0])));
            }

            foreach (var node in nodes)
            {
                var expression = node switch
                {
                    VariableDeclaration { InitialValue: { } value } => value,
                    Assignment { Value: { } value } => value,
                    _ => null,
                };
                if (expression is { LineStart: > 0 })
                {
                    Add(ExprRequest, new LspRange(new Position(expression.LineStart - 1, expression.ColumnStart - 1),
                        new Position(expression.LineEnd - 1, expression.ColumnEnd - 1)));
                }
            }
        }

        foreach (var diagnostic in published)
        {
            if (diagnostic.Code?.String is { } code && QuickFixCodes.Value.Contains(code))
                Add(DiagRequest, diagnostic.Range, diagnostic);
        }

        return requests;
    }

    private static LspRange Cursor(int line, int character) => new(new Position(line, character), new Position(line, character));

    /// <summary>Whole lines from <paramref name="first"/>'s line start to the end of <paramref name="last"/>'s true last line.</summary>
    private static LspRange Lines(SweepText doc, Statement first, Statement last)
    {
        var lastLine = Math.Min(doc.EndLine(last), doc.Lines.Count) - 1;
        return new LspRange(new Position(first.LineStart - 1, 0), new Position(lastLine, doc.Lines[lastLine].Length));
    }

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> s_astProperties = new();

    /// <summary>
    /// Every AST object under <paramref name="module"/> (nodes and the clause records — <c>ElifClause</c>,
    /// <c>MatchCase</c>, <c>ExceptHandler</c> — that are not <see cref="Node"/>s) and every statement body
    /// (<c>ImmutableArray&lt;Statement&gt;</c>-typed property), in source-walk order. Reflection over the AST
    /// namespace's types, so a new compound statement's bodies are swept without touching the sweep.
    /// </summary>
    internal static (SCG.List<object> Nodes, SCG.List<ImmutableArray<Statement>> Bodies) Walk(SModule module)
    {
        var nodes = new SCG.List<object>();
        var bodies = new SCG.List<ImmutableArray<Statement>> { module.Body };
        foreach (var statement in module.Body)
            Visit(statement);
        return (nodes, bodies);

        void Visit(object value)
        {
            nodes.Add(value);
            foreach (var property in s_astProperties.GetOrAdd(value.GetType(), AstProperties))
            {
                var child = property.GetValue(value);
                switch (child)
                {
                    case null:
                        break;
                    case ImmutableArray<Statement> statements:
                        if (!statements.IsDefault)
                        {
                            bodies.Add(statements);
                            foreach (var s in statements)
                                Visit(s);
                        }

                        break;
                    case System.Collections.IEnumerable sequence when child is not string:
                        foreach (var item in sequence)
                        {
                            if (item is not null && IsAstType(item.GetType()))
                                Visit(item);
                        }

                        break;
                    default:
                        if (IsAstType(child.GetType()))
                            Visit(child);
                        break;
                }
            }
        }
    }

    private static readonly string AstNamespace = typeof(Node).Namespace!;

    private static bool IsAstType(Type type) => type.IsClass && type.Namespace == AstNamespace;

    private static PropertyInfo[] AstProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && IsTraversed(p.PropertyType))
            .ToArray();

    private static bool IsTraversed(Type type)
    {
        if (IsAstType(type) || (type.IsAbstract && type.Namespace == AstNamespace))
            return true;
        if (!type.IsGenericType)
            return false;
        var argument = type.GetGenericArguments()[0];
        return argument.Namespace == AstNamespace && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
    }

    // ================================================================
    // Judging one applied action
    // ================================================================

    /// <summary>
    /// What a bucket exempts for one judged action, computed by the sweep from its request: the 1-based lines of the
    /// statements the action deletes whole (D5), the header lines it re-spells (D6), and the line span its edits may
    /// replace (the moved statements' whole lines, for <c>blank</c>'s edge rule).
    /// </summary>
    internal sealed record Exemptions(SCG.IReadOnlySet<int> RemovedLines, SCG.IReadOnlySet<int> HeaderLines, (int First, int Last)? MovedStatements, bool BlankInsideExempt)
    {
        public static readonly Exemptions None = new(new SCG.HashSet<int>(), new SCG.HashSet<int>(), null, false);
    }

    /// <summary>The buckets <paramref name="spec"/>'s shape class runs on one applied action, given its edits, each failing verdict with its cite
    /// (<see cref="CiteOf"/>); <c>offer</c> is judged per request elsewhere.</summary>
    internal static SCG.List<(string Bucket, string Detail, string Cite)> Judge(
        SweepText source, ActionSpec spec, IReadOnlyList<TextEdit> edits, Exemptions exemptions, CompilerApi? diagnosticsApi)
    {
        var verdicts = new SCG.List<(string, string, string)>();
        var applies = BucketsOf[spec.Shape];
        string applied;
        try
        {
            applied = LspFormattingDriver.ApplyStrict(source.Text, edits);
        }
        catch (StrictEditApplicationException ex)
        {
            verdicts.Add((Edits, ex.Message, ShapeIssue));
            return verdicts;
        }

        var after = SweepText.Of(applied);
        void Add(string bucket, string? detail)
        {
            if (detail is not null && applies.Contains(bucket))
                verdicts.Add((bucket, detail, CiteOf(spec, bucket, after)));
        }

        Add(Parse, ParseVerdict(source, after));
        Add(CommentBucket, CommentVerdict(source, after, exemptions.RemovedLines));
        Add(LineBreak, LineBreakVerdict(source, after));
        if (spec.Shape == ShapeClass.Move)
        {
            Add(Line, LineVerdict(source, edits, exemptions));
            Add(Blank, BlankVerdict(source, edits, exemptions));
        }

        // A text that does not parse is parse's verdict; diagnostics judges the semantic change of a text that does.
        if (diagnosticsApi is not null && after.Parses)
            Add(Diagnostics, DiagnosticsVerdict(diagnosticsApi, source, after));
        return verdicts;
    }

    /// <summary><c>parse</c>: S parses and T does not.</summary>
    internal static string? ParseVerdict(SweepText source, SweepText applied)
        => source.Parses && !applied.Parses ? $"the applied text does not parse: {applied.FirstError}" : null;

    /// <summary><c>diagnostics</c>: the error-code multisets of S and T differ.</summary>
    internal static string? DiagnosticsVerdict(CompilerApi api, SweepText source, SweepText applied)
    {
        var before = source.ErrorCodes(api);
        var after = applied.ErrorCodes(api);
        if (before.SequenceEqual(after, StringComparer.Ordinal))
            return null;
        var (added, removed) = MultisetDiff(before, after);
        return $"error codes changed: +[{string.Join(", ", added)}] -[{string.Join(", ", removed)}]";
    }

    /// <summary>
    /// <c>comment</c>: every comment of S survives in T's comment trivia, and T holds no comment S lacks — except that
    /// a comment on a declared-removal line may go.
    /// </summary>
    internal static string? CommentVerdict(SweepText source, SweepText applied, SCG.IReadOnlySet<int> removedLines)
    {
        var required = new SCG.Dictionary<string, int>(StringComparer.Ordinal);
        var optional = new SCG.Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (text, line) in source.Comments)
        {
            var into = removedLines.Contains(line) ? optional : required;
            into[text] = into.GetValueOrDefault(text) + 1;
        }

        var have = new SCG.Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (text, _) in applied.Comments)
            have[text] = have.GetValueOrDefault(text) + 1;

        foreach (var (text, n) in required)
        {
            if (have.GetValueOrDefault(text) < n)
                return $"comment lost: '{Clip(text)}' ({have.GetValueOrDefault(text)} of {n} remain as comments)";
        }

        foreach (var (text, n) in have)
        {
            if (n > required.GetValueOrDefault(text) + optional.GetValueOrDefault(text))
                return $"comment gained: '{Clip(text)}' ({n} in the applied text, {required.GetValueOrDefault(text) + optional.GetValueOrDefault(text)} in the source)";
        }

        return null;
    }

    /// <summary><c>lineBreak</c>: every break of T is S's (uniform) break.</summary>
    internal static string? LineBreakVerdict(SweepText source, SweepText applied)
    {
        var expected = source.UniformBreak
            ?? throw new InvalidOperationException("the lineBreak oracle needs a source whose line breaks are uniform; this one mixes them (a test error, not a verdict)");
        for (var i = 0; i < applied.Breaks.Count; i++)
        {
            if (!string.Equals(applied.Breaks[i], expected, StringComparison.Ordinal))
                return $"applied line {i} ends with {Escape(applied.Breaks[i])}, the document's break is {Escape(expected)}";
        }

        return null;
    }

    /// <summary>
    /// One replaced region: the edits whose whole-line extents overlap, the source lines they cover
    /// (<c>[First, End)</c>, 0-based) and the lines the composed replacement writes there.
    /// </summary>
    internal sealed record Region(int First, int End, SCG.IReadOnlyList<string> NewLines);

    /// <summary>
    /// The replaced regions of <paramref name="edits"/> (legal for <paramref name="source"/>): each edit widened to the
    /// whole lines it touches (an insert at a column-0 position touches none), overlapping widenings merged, and each
    /// region's new lines read from the edits applied to its source lines.
    /// </summary>
    internal static SCG.List<Region> Regions(SweepText source, IReadOnlyList<TextEdit> edits)
    {
        var widened = edits.Select((edit, index) =>
        {
            var start = edit.Range.Start;
            var end = edit.Range.End;
            var from = source.LineStarts[start.Line];
            int to;
            if (end.Character == 0 && (end.Line > start.Line || start.Character == 0))
                to = source.LineStarts[end.Line];
            else
                to = end.Line + 1 < source.LineStarts.Length ? source.LineStarts[end.Line + 1] : source.Text.Length;
            return (From: from, To: to, Index: index);
        }).OrderBy(w => w.From).ThenBy(w => w.To).ThenBy(w => w.Index).ToList();

        var regions = new SCG.List<Region>();
        var k = 0;
        while (k < widened.Count)
        {
            var from = widened[k].From;
            var to = widened[k].To;
            var members = new SCG.List<int> { widened[k].Index };
            k++;
            while (k < widened.Count && widened[k].From < to)
            {
                to = Math.Max(to, widened[k].To);
                members.Add(widened[k].Index);
                k++;
            }

            var slice = source.Text[from..to];
            var local = members.Select(i => edits[i]).Select(e => Shift(source, e, from)).ToArray();
            var composed = LspFormattingDriver.ApplyStrict(slice, local);
            var (lines, breaks) = LineDiff.Split(composed);
            if (breaks.Count > 0 && lines[^1].Length == 0)
                lines.RemoveAt(lines.Count - 1);
            if (composed.Length == 0)
                lines.Clear();
            var firstLine = source.LineOfOffset(from);
            var endLine = to >= source.Text.Length && (source.Text.Length == 0 || !source.Text.EndsWith('\n') && !source.Text.EndsWith('\r'))
                ? source.Lines.Count
                : source.LineOfOffset(to);
            if (to == from)
                endLine = firstLine;
            regions.Add(new Region(firstLine, endLine, lines));
        }

        return regions;
    }

    /// <summary>An edit re-addressed to a slice of the source starting at <paramref name="sliceStart"/> (a line start).</summary>
    private static TextEdit Shift(SweepText source, TextEdit edit, int sliceStart)
    {
        var baseLine = source.LineOfOffset(sliceStart);
        Position Move(Position p) => new(p.Line - baseLine, p.Character);
        return new TextEdit { Range = new LspRange(Move(edit.Range.Start), Move(edit.Range.End)), NewText = edit.NewText };
    }

    /// <summary>
    /// <c>line</c>: every non-blank source line of the replaced regions — other than a re-spelled header or a declared
    /// removal — reappears among the regions' new lines with ONE offset δ (its indentation plus δ, clamped at column
    /// 0), each as many times as it occurs; a line that starts inside a multi-line literal reappears byte-identical
    /// (δ = 0 for it). δ is the offset that leaves the fewest lines unmatched, over every offset some moved line
    /// reappears at.
    /// </summary>
    internal static string? LineVerdict(SweepText source, IReadOnlyList<TextEdit> edits, Exemptions exemptions)
    {
        var regions = Regions(source, edits);
        var moved = new SCG.List<(string Indent, string Text, bool Literal, int Line)>();
        var fresh = new SCG.Dictionary<string, SCG.List<string>>(StringComparer.Ordinal);
        foreach (var region in regions)
        {
            for (var i = region.First; i < region.End; i++)
            {
                var text = source.Lines[i];
                var line = i + 1;
                if (SweepText.IsBlankLine(text) || exemptions.HeaderLines.Contains(line) || exemptions.RemovedLines.Contains(line))
                    continue;
                var indent = SweepText.IndentOf(text);
                moved.Add((indent, text[indent.Length..], source.LiteralLines.Contains(line), line));
            }

            foreach (var text in region.NewLines)
            {
                if (SweepText.IsBlankLine(text))
                    continue;
                var indent = SweepText.IndentOf(text);
                var rest = text[indent.Length..];
                if (!fresh.TryGetValue(rest, out var indents))
                    fresh[rest] = indents = new SCG.List<string>();
                indents.Add(indent);
            }
        }

        if (moved.Count == 0)
            return null;

        // Candidate offsets: every offset some non-literal moved line reappears at (0 when none does).
        var candidates = moved.Where(m => !m.Literal && fresh.ContainsKey(m.Text))
            .SelectMany(m => fresh[m.Text].Select(i => i.Length - m.Indent.Length))
            .Append(0).Distinct().OrderBy(d => d).ToArray();

        (int Delta, (string Indent, string Text, bool Literal, int Line)? Unmatched, int Count) best = (0, null, int.MaxValue);
        foreach (var delta in candidates)
        {
            var pool = fresh.ToDictionary(p => p.Key, p => new SCG.List<string>(p.Value), StringComparer.Ordinal);
            (string, string, bool, int)? firstUnmatched = null;
            var unmatched = 0;
            foreach (var m in moved)
            {
                var found = -1;
                if (pool.TryGetValue(m.Text, out var indents))
                {
                    found = m.Literal
                        ? indents.FindIndex(i => string.Equals(i, m.Indent, StringComparison.Ordinal))
                        : indents.FindIndex(i => i.Length == Math.Max(0, m.Indent.Length + delta));
                }

                if (found >= 0)
                {
                    indents!.RemoveAt(found);
                }
                else
                {
                    unmatched++;
                    firstUnmatched ??= m;
                }
            }

            if (unmatched < best.Count)
                best = (delta, firstUnmatched, unmatched);
        }

        if (best.Count == 0)
            return null;
        var (indentText, restText, literal, lineNumber) = best.Unmatched!.Value;
        return $"{best.Count} of {moved.Count} moved line(s) not kept at offset {best.Delta:+0;-0;0}; first: line {lineNumber} "
            + (literal ? "(inside a multi-line literal, must stay byte-identical) " : "")
            + $"'{Clip(indentText + restText)}'";
    }

    /// <summary>
    /// <c>blank</c>: the regions' blank lines do not decrease (unless the action's own block is exempt, D4), and no edit
    /// reaches outside the moved statements' whole lines (<see cref="Exemptions.MovedStatements"/>).
    /// </summary>
    internal static string? BlankVerdict(SweepText source, IReadOnlyList<TextEdit> edits, Exemptions exemptions)
    {
        if (exemptions.MovedStatements is { } moved)
        {
            var allowedStart = source.LineStarts[Math.Clamp(moved.First - 1, 0, source.Lines.Count - 1)];
            var allowedEnd = moved.Last < source.Lines.Count ? source.LineStarts[moved.Last] : source.Text.Length;
            foreach (var edit in edits)
            {
                var start = source.Offset(edit.Range.Start);
                var end = source.Offset(edit.Range.End);
                if (start == end)
                    continue;
                if (start < allowedStart || end > allowedEnd)
                {
                    return $"edit [{edit.Range.Start.Line}:{edit.Range.Start.Character}-{edit.Range.End.Line}:{edit.Range.End.Character}] "
                        + $"reaches outside the moved statements' lines {moved.First}-{moved.Last} (1-based)";
                }
            }
        }

        if (exemptions.BlankInsideExempt)
            return null;
        var regions = Regions(source, edits);
        var before = regions.Sum(r => Enumerable.Range(r.First, r.End - r.First).Count(i => SweepText.IsBlankLine(source.Lines[i])));
        var after = regions.Sum(r => r.NewLines.Count(SweepText.IsBlankLine));
        return after < before ? $"blank lines inside the region: {before} in the source, {after} after" : null;
    }

    /// <summary><c>offer</c>: the actions offered on a twin for one request against the identity twin's.</summary>
    internal static SCG.List<(string Action, string Detail)> OfferVerdicts(SCG.IReadOnlySet<string> identity, SCG.IReadOnlySet<string> twin)
    {
        var verdicts = new SCG.List<(string, string)>();
        foreach (var action in identity.Except(twin).OrderBy(a => a, StringComparer.Ordinal))
            verdicts.Add((action, "offered on the identity twin, not on this one"));
        foreach (var action in twin.Except(identity).OrderBy(a => a, StringComparer.Ordinal))
            verdicts.Add((action, "offered on this twin, not on the identity twin"));
        return verdicts;
    }

    private static (SCG.List<string> Added, SCG.List<string> Removed) MultisetDiff(SCG.IEnumerable<string> before, SCG.IEnumerable<string> after)
    {
        var counts = new SCG.Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var c in before)
            counts[c] = counts.GetValueOrDefault(c) - 1;
        foreach (var c in after)
            counts[c] = counts.GetValueOrDefault(c) + 1;
        var added = counts.Where(p => p.Value > 0).SelectMany(p => Enumerable.Repeat(p.Key, p.Value)).OrderBy(c => c, StringComparer.Ordinal).ToList();
        var removed = counts.Where(p => p.Value < 0).SelectMany(p => Enumerable.Repeat(p.Key, -p.Value)).OrderBy(c => c, StringComparer.Ordinal).ToList();
        return (added, removed);
    }

    private static string Escape(string lineBreak) => lineBreak.Replace("\r", "\\r").Replace("\n", "\\n");

    private static string Clip(string text) => text.Length > 100 ? text[..100] + "…" : text;

    // ================================================================
    // Exemptions, computed from the request
    // ================================================================

    /// <summary>
    /// The exemptions of <paramref name="spec"/> on <paramref name="request"/> over <paramref name="doc"/>:
    /// <list type="bullet">
    /// <item><c>to-match</c>/<c>to-if</c> — the target's header lines (<c>LineStart..HeaderLineEnd</c>, each clause's
    /// <c>LineStart..HeaderLineEnd</c>, the <c>else</c> header line) are re-spelled; the moved span is the target;</item>
    /// <item><c>wrap-try</c>/<c>extract-method</c> — the moved span is the request's selected statements;</item>
    /// <item><c>organize-imports</c> — the moved span is the module's first through last import; the unused imports
    /// (published SPY0452) are declared removals; blank lines inside the block are the action's layout (D4);</item>
    /// <item><c>remove-import</c> — the import statement at the diagnostic's line is a declared removal;</item>
    /// <item><c>inline-variable</c> — the target declaration is a declared removal.</item>
    /// </list>
    /// </summary>
    internal static Exemptions ExemptionsOf(ActionSpec spec, SweepRequest request, SweepText doc, IReadOnlyList<Diagnostic> published)
    {
        var removed = new SCG.HashSet<int>();
        var headers = new SCG.HashSet<int>();
        (int, int)? moved = null;
        var blankInsideExempt = false;
        void Span(SCG.HashSet<int> into, int first, int last)
        {
            for (var line = first; line <= last; line++)
                into.Add(line);
        }

        switch (spec.Name)
        {
            case "to-match" when request.Target is IfStatement s:
                Span(headers, s.LineStart, Math.Max(s.LineStart, s.HeaderLineEnd));
                foreach (var elif in s.ElifClauses)
                    Span(headers, elif.LineStart, Math.Max(elif.LineStart, elif.HeaderLineEnd));
                if (s.ElseHeaderLine > 0)
                    headers.Add(s.ElseHeaderLine);
                moved = (s.LineStart, doc.EndLine(s));
                break;
            case "to-if" when request.Target is MatchStatement s:
                Span(headers, s.LineStart, Math.Max(s.LineStart, s.HeaderLineEnd));
                foreach (var @case in s.Cases)
                    Span(headers, @case.LineStart, Math.Max(@case.LineStart, @case.HeaderLineEnd));
                moved = (s.LineStart, doc.EndLine(s));
                break;
            case "wrap-try" or "extract-method":
                moved = request.Statements;
                break;
            case "organize-imports" when doc.Module is { } module:
                var imports = module.Body.Where(st => st.UnwrapDecorated() is ImportStatement or FromImportStatement).ToList();
                if (imports.Count > 0)
                    moved = (imports[0].LineStart, doc.EndLine(imports[^1]));
                var unused = published.Where(d => d.Code?.String == Sharpy.Compiler.Diagnostics.DiagnosticCodes.Validation.UnusedImport)
                    .Select(d => d.Range.Start.Line + 1).ToHashSet();
                foreach (var import in imports.Where(st => unused.Contains(st.LineStart)))
                    Span(removed, import.LineStart, doc.EndLine(import));
                blankInsideExempt = true;
                break;
            case "remove-import" when request.Diagnostic is { } diagnostic && doc.Module is { } module:
                var line = diagnostic.Range.Start.Line + 1;
                var statement = Walk(module).Nodes.OfType<Statement>().FirstOrDefault(st => st.LineStart == line && st.UnwrapDecorated() is ImportStatement or FromImportStatement);
                if (statement is not null)
                    Span(removed, statement.LineStart, doc.EndLine(statement));
                break;
            case "inline-variable" when request.Target is VariableDeclaration declaration:
                Span(removed, declaration.LineStart, doc.EndLine(declaration));
                break;
        }

        return new Exemptions(removed, headers, moved, blankInsideExempt);
    }

    // ================================================================
    // Roster — the title scan (D9)
    // ================================================================

    /// <summary>The directory the providers live in (the title scan's subject).</summary>
    internal static string RefactoringDirectory()
    {
        var dir = Path.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Lsp", "Refactoring");
        if (!Directory.Exists(dir))
            throw new InvalidOperationException($"{dir} is missing: the title scan is only meaningful against the real sources.");
        return dir;
    }

    /// <summary>
    /// Every code-action title head under <paramref name="directory"/>: for each <c>Title = e</c> member initializer in
    /// a <c>*.cs</c> file, the heads of <c>e</c> — a string literal's text, an interpolated string's text up to its
    /// first hole, both arms of a conditional, and for an identifier, the heads of every value the enclosing member
    /// assigns it (initializer and plain assignments). A shape the scan cannot read is a null head (the test refuses it).
    /// </summary>
    internal static SCG.List<(string File, int Line, string? Head)> ScanTitleHeads(string directory)
    {
        var result = new SCG.List<(string, int, string?)>();
        foreach (var file in Directory.GetFiles(directory, "*.cs").OrderBy(f => f, StringComparer.Ordinal))
        {
            var root = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            foreach (var assignment in root.DescendantNodes().OfType<RoslynSyntax.AssignmentExpressionSyntax>())
            {
                if (assignment.Left is not RoslynSyntax.IdentifierNameSyntax { Identifier.ValueText: "Title" }
                    || assignment.Parent is not RoslynSyntax.InitializerExpressionSyntax)
                {
                    continue;
                }

                var line = assignment.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                foreach (var head in Heads(assignment.Right, depth: 0))
                    result.Add((Path.GetFileName(file), line, head));
            }
        }

        return result;
    }

    private static SCG.IEnumerable<string?> Heads(RoslynSyntax.ExpressionSyntax expression, int depth)
    {
        if (depth > 8)
        {
            yield return null;
            yield break;
        }

        switch (expression)
        {
            case RoslynSyntax.ParenthesizedExpressionSyntax p:
                foreach (var h in Heads(p.Expression, depth + 1))
                    yield return h;
                break;
            case RoslynSyntax.LiteralExpressionSyntax { Token.Value: string text }:
                yield return text;
                break;
            case RoslynSyntax.InterpolatedStringExpressionSyntax interpolated:
                yield return string.Concat(interpolated.Contents
                    .TakeWhile(c => c is RoslynSyntax.InterpolatedStringTextSyntax)
                    .Cast<RoslynSyntax.InterpolatedStringTextSyntax>()
                    .Select(t => t.TextToken.ValueText));
                break;
            case RoslynSyntax.ConditionalExpressionSyntax conditional:
                foreach (var h in Heads(conditional.WhenTrue, depth + 1))
                    yield return h;
                foreach (var h in Heads(conditional.WhenFalse, depth + 1))
                    yield return h;
                break;
            case RoslynSyntax.IdentifierNameSyntax identifier:
                var member = identifier.Ancestors().FirstOrDefault(a => a is RoslynSyntax.BaseMethodDeclarationSyntax
                    or RoslynSyntax.LocalFunctionStatementSyntax or RoslynSyntax.AccessorDeclarationSyntax
                    or RoslynSyntax.PropertyDeclarationSyntax or RoslynSyntax.FieldDeclarationSyntax);
                var name = identifier.Identifier.ValueText;
                var values = member is null
                    ? new SCG.List<RoslynSyntax.ExpressionSyntax>()
                    : member.DescendantNodes().OfType<RoslynSyntax.VariableDeclaratorSyntax>()
                        .Where(v => v.Identifier.ValueText == name && v.Initializer is not null)
                        .Select(v => v.Initializer!.Value)
                        .Concat(member.DescendantNodes().OfType<RoslynSyntax.AssignmentExpressionSyntax>()
                            .Where(a => a.Left is RoslynSyntax.IdentifierNameSyntax l && l.Identifier.ValueText == name
                                && a.Parent is not RoslynSyntax.InitializerExpressionSyntax)
                            .Select(a => a.Right))
                        .ToList();
                if (values.Count == 0)
                {
                    yield return null;
                    break;
                }

                foreach (var value in values)
                {
                    foreach (var h in Heads(value, depth + 1))
                        yield return h;
                }

                break;
            default:
                yield return null;
                break;
        }
    }

    /// <summary>The scan's problems: unreadable title shapes, heads no prefix matches, and prefixes no head uses (stale).</summary>
    internal static SCG.List<string> TitleRosterProblems(SCG.IReadOnlyList<(string File, int Line, string? Head)> heads, SCG.IReadOnlyList<ActionSpec> actions)
    {
        var problems = new SCG.List<string>();
        foreach (var (file, line, head) in heads)
        {
            if (head is null)
            {
                problems.Add($"{file}:{line}: a Title the scan cannot read (string, interpolation, conditional or a local assigned one)");
                continue;
            }

            var matches = actions.Where(a => head.StartsWith(a.Prefix, StringComparison.Ordinal)).ToList();
            if (matches.Count != 1)
                problems.Add($"{file}:{line}: title head '{head}' matches {matches.Count} roster prefixes (need exactly 1)");
        }

        foreach (var action in actions)
        {
            if (!heads.Any(h => h.Head is not null && h.Head.StartsWith(action.Prefix, StringComparison.Ordinal)))
                problems.Add($"roster prefix '{action.Prefix}' ({action.Name}) matches no title in Refactoring/ — stale");
        }

        return problems;
    }

    [Fact]
    public void Actions_AreTotalOverTheTitleLiteralsOfRefactoring()
    {
        var heads = ScanTitleHeads(RefactoringDirectory());
        foreach (var (file, line, head) in heads)
            _output.WriteLine($"CODEACTION-TITLE {file}:{line} '{head}'");
        TitleRosterProblems(heads, Actions).Should().BeEmpty("every code-action title in src/Sharpy.Lsp/Refactoring/ is in the sweep's roster, and only those");
        Actions.Select(a => a.Name).Should().OnlyHaveUniqueItems();
        Actions.Length.Should().Be(14);
    }

    /// <summary>Copies the Refactoring sources to a temp directory and appends <paramref name="plant"/> as a new file.</summary>
    private static string PlantedCopy(string plant)
    {
        var dir = Path.Combine(Path.GetTempPath(), "p47-title-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var file in Directory.GetFiles(RefactoringDirectory(), "*.cs"))
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));
        File.WriteAllText(Path.Combine(dir, "Planted.cs"), plant);
        return dir;
    }

    /// <summary>Positive control of the scan's literal arm: a planted <c>Title = "Frobnicate"</c> is reported.</summary>
    [Fact]
    public void TitleScan_PlantedLiteral_IsReported()
    {
        var dir = PlantedCopy("class P { object M() => new X { Title = \"Frobnicate\" }; }");
        try
        {
            TitleRosterProblems(ScanTitleHeads(dir), Actions).Should().ContainSingle().Which.Should().Contain("'Frobnicate'");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Positive control of the scan's identifier arm (D9 correction): <c>var frob = "Frobnicate"; … Title = frob</c> is reported.</summary>
    [Fact]
    public void TitleScan_PlantedLocal_IsReported()
    {
        var dir = PlantedCopy("class P { object M() { var frob = \"Frobnicate\"; return new X { Title = frob }; } }");
        try
        {
            TitleRosterProblems(ScanTitleHeads(dir), Actions).Should().ContainSingle().Which.Should().Contain("'Frobnicate'");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The scan reads both arms of a conditional local — the two shapes of Extract variable and Implement.</summary>
    [Fact]
    public void TitleScan_ConditionalLocal_YieldsBothArms()
    {
        var heads = ScanTitleHeads(RefactoringDirectory());
        heads.Where(h => h.File == "ImplementInterfaceProvider.cs").Select(h => h.Head)
            .Should().BeEquivalentTo(new[] { "Implement interface '", "Implement all interfaces" });
        heads.Where(h => h.File == "ExtractVariableProvider.cs").Select(h => h.Head)
            .Should().BeEquivalentTo(new[] { "Extract variable '", "Extract variable '" });
    }

    // ================================================================
    // Request builders
    // ================================================================

    private const string Nested = "def main():\n    x = 1\n    if x == 1:  # one\n        if x > 0:\n            print(x)\n    elif x == 2:\n        print(2)\n\n    s = \"\"\"a\n  b\"\"\"\n    print(s)\n";

    [Fact]
    public void BuildRequests_HandDocument_CoversEveryKindItAdmits()
    {
        var doc = SweepText.Of(Nested);
        doc.Parses.Should().BeTrue();
        var requests = BuildRequests(doc, Array.Empty<Diagnostic>());
        var byKind = requests.GroupBy(r => r.Kind).ToDictionary(g => g.Key, g => g.Count());
        byKind.Should().Contain(WholeRequest, 1);
        byKind.Should().Contain(IfRequest, 2);
        byKind.Should().Contain(CallRequest, 3);
        byKind.Should().Contain(ExprRequest, 2, "x = 1 and the triple-quoted value");
        byKind.Should().ContainKey(BodyRequest);
        byKind.Should().ContainKey(FirstRequest);

        // The def body selected whole reaches the string's TAIL line (`  b"""`, line 9 0-based): the parser's LineEnd
        // for the assignment is its first line (#2295); the sweep's extent is the lexer's.
        var defBody = requests.Single(r => r.Kind == BodyRequest && r.Range.Start.Line == 1);
        defBody.Range.End.Should().Be(new Position(10, "    print(s)".Length));
        var stringStatement = doc.Module!.Body.OfType<FunctionDef>().Single().Body.OfType<Assignment>().Last();
        stringStatement.LineEnd.Should().Be(9, "the parser fact P47 works around (ledgered as #2295)");
        doc.EndLine(stringStatement).Should().Be(10);
    }

    [Fact]
    public void BuildRequests_PublishedQuickFixDiagnostic_IsADiagRequest()
    {
        var doc = SweepText.Of("import math\n");
        var diagnostic = new Diagnostic
        {
            Code = Sharpy.Compiler.Diagnostics.DiagnosticCodes.Validation.UnusedImport,
            Range = new LspRange(new Position(0, 0), new Position(0, 11)),
        };
        var other = new Diagnostic { Code = "SPY9999", Range = diagnostic.Range };
        var requests = BuildRequests(doc, new[] { diagnostic, other });
        requests.Where(r => r.Kind == DiagRequest).Should().ContainSingle().Which.Diagnostic.Should().BeSameAs(diagnostic);
        QuickFixCodes.Value.Should().HaveCount(4, "SPY0452, unused variable, naming convention, undefined variable @ 7f0502de3");
    }

    // ================================================================
    // Positive controls — each bucket fires on a damaged applied text and not on the clean one
    // ================================================================

    private static TextEdit E(int sl, int sc, int el, int ec, string text)
        => new() { Range = new LspRange(new Position(sl, sc), new Position(el, ec)), NewText = text };

    private static ActionSpec Spec(string name) => Actions.Single(a => a.Name == name);

    private const string WrapSource = "def main():\n    x = 1\n    if x:\n        print(x)\n\n    y = 2\n";

    private static Exemptions Moved(int first, int last) => new(new SCG.HashSet<int>(), new SCG.HashSet<int>(), (first, last), false);

    [Fact]
    public void Edits_PositionPastTheLine_Fires()
        => Judge(SweepText.Of("x = 1\n"), Spec("rename"), new[] { E(0, 9, 0, 9, "y") }, Exemptions.None, null)
            .Should().ContainSingle(v => v.Bucket == Edits);

    [Fact]
    public void Edits_LegalEdit_IsClean()
        => Judge(SweepText.Of("x = 1\n"), Spec("rename"), new[] { E(0, 0, 0, 1, "y") }, Exemptions.None, null)
            .Should().NotContain(v => v.Bucket == Edits);

    [Fact]
    public void Offer_MissingOnTheTwin_Fires()
        => OfferVerdicts(new SCG.HashSet<string> { "to-match", "wrap-try" }, new SCG.HashSet<string> { "wrap-try" })
            .Should().ContainSingle().Which.Action.Should().Be("to-match");

    [Fact]
    public void Offer_SameSet_IsClean()
        => OfferVerdicts(new SCG.HashSet<string> { "to-match" }, new SCG.HashSet<string> { "to-match" }).Should().BeEmpty();

    [Fact]
    public void Parse_IndentationLost_Fires()
        => ParseVerdict(SweepText.Of("def f():\n    pass\n"), SweepText.Of("def f():\npass\n")).Should().NotBeNull();

    [Fact]
    public void Parse_ParsingText_IsClean()
        => ParseVerdict(SweepText.Of("def f():\n    pass\n"), SweepText.Of("def f():\n    try:\n        pass\n    except Exception as e:\n        raise\n")).Should().BeNull();

    private static readonly Lazy<CompilerApi> s_api = new(() => new CompilerApi(CompilerNullLogger.Instance, StdlibReferences.Paths()));

    [Fact]
    public void Diagnostics_NameUnbound_Fires()
        => DiagnosticsVerdict(s_api.Value, SweepText.Of("def main():\n    x: int = 1\n    print(x)\n"), SweepText.Of("def main():\n    print(x)\n"))
            .Should().Contain("SPY0200");

    [Fact]
    public void Diagnostics_SameCodes_IsClean()
        => DiagnosticsVerdict(s_api.Value, SweepText.Of("def main():\n    x: int = 1\n    print(x)\n"), SweepText.Of("def main():\n    y: int = 1\n    print(y)\n"))
            .Should().BeNull();

    /// <summary>The comment oracle reads TRIVIA: a comment moved into a string literal is lost, though its text is still a substring.</summary>
    [Fact]
    public void Comment_MovedIntoAString_Fires()
    {
        var applied = "x = \"# note\"\n";
        applied.Should().Contain("# note", "the substring a text oracle would accept");
        CommentVerdict(SweepText.Of("x = 1  # note\n"), SweepText.Of(applied), new SCG.HashSet<int>()).Should().Contain("comment lost");
    }

    [Fact]
    public void Comment_KeptOnAMovedLine_IsClean()
        => CommentVerdict(SweepText.Of("x = 1  # note\n"), SweepText.Of("try:\n    x = 1  # note\nexcept Exception as e:\n    raise\n"), new SCG.HashSet<int>())
            .Should().BeNull();

    [Fact]
    public void Comment_OnADeclaredRemoval_MayGo()
    {
        var source = SweepText.Of("import sys  # a\nprint(1)\n");
        CommentVerdict(source, SweepText.Of("print(1)\n"), new SCG.HashSet<int> { 1 }).Should().BeNull();
        CommentVerdict(source, SweepText.Of("print(1)\n"), new SCG.HashSet<int>()).Should().Contain("comment lost", "the same deletion without the declared removal");
    }

    /// <summary>The flattened nested line of cell 3: <c>print(x)</c> moved to the <c>if</c> column under a +4 wrap.</summary>
    [Fact]
    public void Line_FlattenedNestedLine_Fires()
    {
        var edit = E(1, 0, 4, 0, "    try:\n        x = 1\n        if x:\n        print(x)\n    except Exception as e:\n        raise\n");
        LineVerdict(SweepText.Of(WrapSource), new[] { edit }, Moved(2, 4)).Should().Contain("print(x)");
    }

    [Fact]
    public void Line_UniformOffset_IsClean()
    {
        var edit = E(1, 0, 4, 0, "    try:\n        x = 1\n        if x:\n            print(x)\n    except Exception as e:\n        raise\n");
        LineVerdict(SweepText.Of(WrapSource), new[] { edit }, Moved(2, 4)).Should().BeNull();
    }

    private const string LiteralSource = "def main():\n    s = \"\"\"a\n  b\"\"\"\n    print(s)\n";

    /// <summary>A literal's tail line shifted with the region changes the string's value.</summary>
    [Fact]
    public void Line_LiteralTailShifted_Fires()
    {
        var edit = E(1, 0, 3, 0, "    try:\n        s = \"\"\"a\n      b\"\"\"\n    except Exception as e:\n        raise\n");
        LineVerdict(SweepText.Of(LiteralSource), new[] { edit }, Moved(2, 3)).Should().Contain("multi-line literal");
    }

    [Fact]
    public void Line_LiteralTailKeptAtOffsetZero_IsClean()
    {
        var edit = E(1, 0, 3, 0, "    try:\n        s = \"\"\"a\n  b\"\"\"\n    except Exception as e:\n        raise\n");
        LineVerdict(SweepText.Of(LiteralSource), new[] { edit }, Moved(2, 3)).Should().BeNull();
    }

    [Fact]
    public void Line_ReSpelledHeader_IsExempt()
    {
        var source = SweepText.Of("if x == 1:\n    print(1)\n");
        var edit = E(0, 0, 2, 0, "match x:\n    case 1:\n        print(1)\n");
        LineVerdict(source, new[] { edit }, new Exemptions(new SCG.HashSet<int>(), new SCG.HashSet<int> { 1 }, (1, 2), false)).Should().BeNull();
        LineVerdict(source, new[] { edit }, Moved(1, 2)).Should().Contain("if x == 1:", "the same edit with the header not exempt");
    }

    /// <summary>The swallowed edge blank of cell 22: the edit reaches past the moved statements' last line.</summary>
    [Fact]
    public void Blank_SwallowedEdgeBlank_Fires()
    {
        var source = SweepText.Of("import sys\nimport math\n\n\ndef main():\n    pass\n");
        var edit = E(0, 0, 4, 0, "import math\nimport sys\n");
        BlankVerdict(source, new[] { edit }, new Exemptions(new SCG.HashSet<int>(), new SCG.HashSet<int>(), (1, 2), true)).Should().Contain("reaches outside");
    }

    [Fact]
    public void Blank_DroppedInnerBlank_Fires()
    {
        var edit = E(1, 0, 6, 0, "    try:\n        x = 1\n        if x:\n            print(x)\n        y = 2\n    except Exception as e:\n        raise\n");
        BlankVerdict(SweepText.Of(WrapSource), new[] { edit }, Moved(2, 6)).Should().Contain("blank lines inside the region");
    }

    [Fact]
    public void Blank_EdgesAndInnerBlankKept_IsClean()
    {
        var edit = E(1, 0, 6, 0, "    try:\n        x = 1\n        if x:\n            print(x)\n\n        y = 2\n    except Exception as e:\n        raise\n");
        BlankVerdict(SweepText.Of(WrapSource), new[] { edit }, Moved(2, 6)).Should().BeNull();
    }

    [Fact]
    public void LineBreak_LfWrittenIntoACrlfDocument_Fires()
        => LineBreakVerdict(SweepText.Of("a = 1\r\nb = 2\r\n"), SweepText.Of("a = 1\r\nc = 3\nb = 2\r\n")).Should().StartWith("applied line 1 ends with \\n,");

    [Fact]
    public void LineBreak_DocumentBreakKept_IsClean()
        => LineBreakVerdict(SweepText.Of("a = 1\r\nb = 2\r\n"), SweepText.Of("a = 1\r\nc = 3\r\nb = 2\r\n")).Should().BeNull();

    [Fact]
    public void LineBreak_MixedSource_IsATestError()
        => FluentActions.Invoking(() => LineBreakVerdict(SweepText.Of("a\r\nb\n"), SweepText.Of("a\r\nb\n")))
            .Should().Throw<InvalidOperationException>().WithMessage("*uniform*");

    [Fact]
    public void Regions_InsertAndReplace_AreSeparateRegionsWithTheirNewLines()
    {
        var source = SweepText.Of("a\nb\nc\n");
        var regions = Regions(source, new[] { E(0, 0, 0, 0, "x\n"), E(1, 0, 2, 0, "B\n") });
        regions.Should().HaveCount(2);
        regions[0].Should().Match<Region>(r => r.First == 0 && r.End == 0 && r.NewLines.SequenceEqual(new[] { "x" }));
        regions[1].Should().Match<Region>(r => r.First == 1 && r.End == 2 && r.NewLines.SequenceEqual(new[] { "B" }));
    }

    [Fact]
    public void Regions_MidLineReplacement_WidensToTheWholeLine()
    {
        var source = SweepText.Of("    if x == 1:  # one\n        pass\n");
        var regions = Regions(source, new[] { E(0, 4, 1, 12, "match x:") });
        regions.Should().ContainSingle().Which.NewLines.Should().Equal("    match x:");
    }

    // ================================================================
    // Cites — the cause of a failing cell, by contract clause
    // ================================================================

    /// <summary>The source-shape contract (this plan's class; drained by Phase 2).</summary>
    internal const string ShapeIssue = "#2232";

    /// <summary>Synthesized type spellings: Extract method's parameter inference and Extract variable's annotation.</summary>
    internal const string SpellingIssue = "#2294";

    /// <summary>Code actions that are not meaning-preserving (the applied text parses and its error codes change).</summary>
    internal const string MeaningIssue = "#2296";

    internal static readonly string[] Cites = { ShapeIssue, SpellingIssue, MeaningIssue };

    /// <summary>
    /// The issue a failing cell is pinned under (lead ruling, P47 Task 3): <c>diagnostics</c> of Extract method / Extract
    /// variable → #2294 and of every other action → #2296; <c>parse</c> of Extract variable → #2294, of Extract method
    /// when the first error lies inside the synthesized <c>def extracted_method(…)</c> parameter list (a type spelling)
    /// → #2294; every other cell — every <c>edits</c>/<c>offer</c>/<c>lineBreak</c>/<c>line</c>/<c>blank</c>/<c>comment</c>
    /// cell and every other <c>parse</c> cell — → #2232 ("never turns a parsing document into one that does not parse").
    /// </summary>
    internal static string CiteOf(ActionSpec spec, string bucket, SweepText? applied)
    {
        if (bucket == Diagnostics)
            return spec.Name is "extract-method" or "extract-variable" ? SpellingIssue : MeaningIssue;
        if (bucket != Parse || applied is null)
            return ShapeIssue;
        if (spec.Name == "extract-variable")
            return SpellingIssue;
        if (spec.Name == "extract-method" && InsideSynthesizedParameterList(applied))
            return SpellingIssue;
        return ShapeIssue;
    }

    private const string SynthesizedDef = "def extracted_method(";

    private static bool InsideSynthesizedParameterList(SweepText applied)
    {
        if (applied.FirstErrorLine is not { } line || applied.FirstErrorColumn is not { } column || line < 1 || line > applied.Lines.Count)
            return false;
        var text = applied.Lines[line - 1];
        var open = text.IndexOf(SynthesizedDef, StringComparison.Ordinal);
        return open >= 0 && text[..open].Trim().Length == 0 && column > open + SynthesizedDef.Length;
    }

    [Fact]
    public void Cite_ErrorInsideTheSynthesizedParameterList_IsASpelling()
    {
        var spec = Actions.Single(a => a.Name == "extract-method");
        // The spelling Extract method writes for a builtin it takes as a parameter (#2294, recorded by the stdio probe).
        var spelled = SweepText.Of("def extracted_method(search: Callable[[list[int32], int32], int32]):\n    pass\n");
        spelled.Parses.Should().BeFalse();
        CiteOf(spec, Parse, spelled).Should().Be(SpellingIssue);
        var misplaced = SweepText.Of("class A:\ndef extracted_method(x: int):\n    pass\n");
        misplaced.Parses.Should().BeFalse();
        CiteOf(spec, Parse, misplaced).Should().Be(ShapeIssue, "the error is the def's position, not its spelling");
        CiteOf(spec, Diagnostics, null).Should().Be(SpellingIssue);
        CiteOf(Actions.Single(a => a.Name == "wrap-try"), Diagnostics, null).Should().Be(MeaningIssue);
        CiteOf(Actions.Single(a => a.Name == "extract-variable"), Parse, misplaced).Should().Be(SpellingIssue);
        CiteOf(Actions.Single(a => a.Name == "prefix-underscore"), Parse, misplaced).Should().Be(ShapeIssue);
    }

    // ================================================================
    // Corpus, modes and strides
    // ================================================================

    private const string AllowlistFileName = "code-action-shape-allowlist.txt";
    private const string FullModeVariable = "SHARPY_CODEACTION_SWEEP_FULL";

    /// <summary>Regular mode runs the <c>crlf</c>/<c>cr</c>/<c>comment</c> twins on the stems whose <see cref="SweepCorpus.StableBucket"/> under this stride is 0.</summary>
    internal const int TwinStride = 8;

    /// <summary>Regular mode judges <c>diagnostics</c> (one extra semantic analysis per cell) on the cells whose bucket under this stride is 0.</summary>
    internal const int DiagnosticsStride = 8;

    internal static bool FullMode => Environment.GetEnvironmentVariable(FullModeVariable) == "1";

    /// <summary>Whether (stem, twin) runs in the given mode.</summary>
    internal static bool Runs(string stem, string twin, bool full)
        => full || twin == Identity || SweepCorpus.StableBucket(stem, TwinStride) == 0;

    /// <summary>Whether a judged cell's <c>diagnostics</c> bucket runs in the given mode.</summary>
    internal static bool JudgesDiagnostics(string stem, string twin, string requestKey, string action, bool full)
        => full || SweepCorpus.StableBucket($"{stem}|{twin}|{requestKey}|{action}", DiagnosticsStride) == 0;

    private static readonly Lazy<CompilerApi> s_sweepApi = new(() => new CompilerApi(CompilerNullLogger.Instance, StdlibReferences.Paths()));

    private static readonly Lazy<LspCodeActionDriver> s_driver = new(() => new LspCodeActionDriver(s_sweepApi.Value));

    private static int Parallelism => int.TryParse(Environment.GetEnvironmentVariable("SHARPY_CODEACTION_SWEEP_DOP"), out var n) && n > 0
        ? n
        : Math.Min(4, Environment.ProcessorCount);

    // ================================================================
    // The twin sweeps
    // ================================================================

    [Fact]
    public void Identity_EveryCodeActionKeepsTheSourceShape() => SweepTwin(Identity);

    [Fact]
    public void Crlf_EveryCodeActionKeepsTheSourceShape() => SweepTwin(Crlf);

    [Fact]
    public void Cr_EveryCodeActionKeepsTheSourceShape() => SweepTwin(Cr);

    [Fact]
    public void CommentInjected_EveryCodeActionKeepsTheSourceShape() => SweepTwin(CommentTwin);

    /// <summary>A pin's key: one (action, bucket, cite) of one twin.</summary>
    internal readonly record struct PinKey(string Action, string Bucket, string Cite)
    {
        public override string ToString() => $"{Action} {Bucket} {Cite}";
    }

    /// <summary>The verdicts of one action on one (stem, twin).</summary>
    internal sealed class Group
    {
        public int Cells;
        public int OfferObservations;

        /// <summary>Per failing (bucket, cite): the cells, the first detail, and whether any failing cell is one regular mode judges.</summary>
        public readonly SortedDictionary<(string Bucket, string Cite), (int Count, string First, bool Regular)> Failures = new();

        public readonly SCG.List<string> Details = new();

        public void Fail(string bucket, string cite, string detail, bool regular)
        {
            Failures[(bucket, cite)] = Failures.TryGetValue((bucket, cite), out var f)
                ? (f.Count + 1, f.First, f.Regular || regular)
                : (1, detail, regular);
            Details.Add($"{bucket} {cite} {(regular ? "R" : "-")} {detail}");
        }
    }

    // ---- what the census reads from the sweeps that ran in this process ----
    private static readonly ConcurrentDictionary<(string Action, string Twin), int> s_cells = new();
    private static readonly ConcurrentDictionary<(string Twin, PinKey Key), int> s_failingStems = new();
    private static readonly ConcurrentDictionary<string, int> s_stemsRun = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, long> s_requests = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, double> s_seconds = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> s_unparseableTwins = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> s_unownedOffers = new(StringComparer.Ordinal);

    /// <summary>The identity twin's offers per request key, kept for the stems the offer twins run on (read by <c>crlf</c>/<c>cr</c>).</summary>
    private static readonly ConcurrentDictionary<string, SCG.Dictionary<string, SCG.HashSet<string>>> s_identityOffers = new(StringComparer.Ordinal);

    /// <summary>
    /// Sweeps every stem of <paramref name="twin"/> that runs in this mode, aggregates the failing stems per pin key, and
    /// compares them with the pinned (count, digest) pairs — the FULL pair and the regular pair in FULL mode, the regular
    /// pair in regular mode (<see cref="ComparePins"/>).
    /// </summary>
    private void SweepTwin(string twin)
    {
        var full = FullMode;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var stems = SweepCorpus.Corpus.Value.Corpus.Keys.Where(stem => Runs(stem, twin, full)).ToArray();
        var fullSets = new ConcurrentDictionary<PinKey, ConcurrentDictionary<string, byte>>();
        var regularSets = new ConcurrentDictionary<PinKey, ConcurrentDictionary<string, byte>>();
        var cellLines = new ConcurrentBag<string>();
        long requests = 0;
        Parallel.ForEach(stems, new ParallelOptions { MaxDegreeOfParallelism = Parallelism }, stem =>
        {
            var groups = RunCells(stem, twin, full, out var requestCount, out var unparseable);
            Interlocked.Add(ref requests, requestCount);
            if (unparseable)
                s_unparseableTwins.AddOrUpdate(twin, 1, (_, n) => n + 1);
            foreach (var (action, group) in groups)
            {
                s_cells.AddOrUpdate((action, twin), group.Cells, (_, n) => n + group.Cells);
                foreach (var ((bucket, cite), (_, _, regular)) in group.Failures)
                {
                    var key = new PinKey(action, bucket, cite);
                    fullSets.GetOrAdd(key, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))[stem] = 0;
                    if (regular)
                        regularSets.GetOrAdd(key, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))[stem] = 0;
                }

                if (full)
                {
                    foreach (var detail in group.Details)
                        cellLines.Add($"{stem} {twin} {action} {detail}");
                }
            }
        });

        s_driver.Value.OpenDocumentCount.Should().Be(0, "the driver closes every document it opens");
        s_stemsRun[twin] = stems.Length;
        s_requests[twin] = requests;
        s_seconds[twin] = clock.Elapsed.TotalSeconds;

        static SCG.Dictionary<PinKey, SortedSet<string>> Freeze(ConcurrentDictionary<PinKey, ConcurrentDictionary<string, byte>> sets)
            => sets.ToDictionary(p => p.Key, p => new SortedSet<string>(p.Value.Keys, StringComparer.Ordinal));

        var actualFull = Freeze(fullSets);
        var actualRegular = Freeze(regularSets);
        foreach (var (key, set) in actualFull)
            s_failingStems[(twin, key)] = set.Count;

        var rows = Allowlist.Value.Where(r => r.Twin == twin).ToList();
        var problems = new SCG.List<string>();
        var mismatched = new SCG.Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var dumpDir = Path.Combine(FixtureRoots.RepositoryRoot, ".claude", "tmp", "code-action-sweep");
        Directory.CreateDirectory(dumpDir);
        if (full)
        {
            problems.AddRange(ComparePins(twin, "full", actualFull, rows, r => (r.FullCount, r.FullDigest), mismatched));
            File.WriteAllLines(Path.Combine(dumpDir, $"{twin}-cells.txt"), cellLines.OrderBy(l => l, StringComparer.Ordinal));
            File.WriteAllLines(Path.Combine(dumpDir, $"{twin}-actual-pins.txt"), ActualPinRows(twin, actualFull, actualRegular, rows));
        }

        problems.AddRange(ComparePins(twin, "regular", actualRegular, rows, r => (r.RegularCount, r.RegularDigest), mismatched));
        var stemFile = Path.Combine(dumpDir, $"{twin}-{(full ? "full" : "regular")}-mismatched-stems.txt");
        File.WriteAllLines(stemFile, mismatched.SelectMany(m => new[] { $"## {m.Key} ({m.Value.Count} failing stems)" }.Concat(m.Value)));

        _output.WriteLine($"CODEACTION-TWIN {twin} mode={(full ? "full" : "regular")} stems={stems.Length} requests={requests} "
            + $"seconds={clock.Elapsed.TotalSeconds:F1} keys={actualFull.Count} problems={problems.Count}");
        if (problems.Count > 0)
        {
            Assert.Fail($"{twin} twin: every code action must keep the source shape; the failing stems must equal the pins in "
                + $"Conformance/{AllowlistFileName}. Actual stem lists of the mismatched keys: {stemFile}"
                + (full ? $"; actual pin rows: {Path.Combine(dumpDir, $"{twin}-actual-pins.txt")}" : "")
                + "\n  " + string.Join("\n  ", problems));
        }
    }

    /// <summary>
    /// The pin ratchet of one twin and one pair (FULL or regular): every key with failing stems must be pinned, and its
    /// (count, digest) must equal the pin; a pinned key whose stems now all pass reads (0, "-") and mismatches — drain:
    /// delete the row (FULL) or zero its regular pair. A mismatched key's actual stems go to <paramref name="mismatched"/>.
    /// </summary>
    internal static SCG.List<string> ComparePins(string twin, string pair, SCG.IReadOnlyDictionary<PinKey, SortedSet<string>> actual,
        SCG.IReadOnlyList<Row> rows, Func<Row, (int Count, string Digest)> expectedOf, SCG.IDictionary<string, SortedSet<string>> mismatched)
    {
        var problems = new SCG.List<string>();
        var pinned = rows.ToDictionary(r => new PinKey(r.Action, r.Bucket, r.Cite), r => expectedOf(r));
        foreach (var key in actual.Keys.Union(pinned.Keys).OrderBy(k => k.ToString(), StringComparer.Ordinal))
        {
            var stems = actual.TryGetValue(key, out var set) ? set : new SortedSet<string>(StringComparer.Ordinal);
            var (count, digest) = (stems.Count, Digest(stems));
            if (!pinned.TryGetValue(key, out var expected))
            {
                if (count == 0)
                    continue;
                problems.Add($"{pair}: {twin} {key} fails on {count} stem(s) and has no pin row (digest {digest}): {Sample(stems)}");
            }
            else if (expected.Count != count || !string.Equals(expected.Digest, digest, StringComparison.Ordinal))
            {
                problems.Add($"{pair}: {twin} {key} pinned count={expected.Count} digest={expected.Digest}, actual count={count} digest={digest}"
                    + (count == 0 ? " — every stem holds now: drain the pin" : $": {Sample(stems)}"));
            }
            else
            {
                continue;
            }

            mismatched[$"{pair} {twin} {key}"] = stems;
        }

        return problems;

        static string Sample(SortedSet<string> stems) => string.Join(" ", stems.Take(20)) + (stems.Count > 20 ? " …" : "");
    }

    /// <summary>First 16 hex characters of SHA-256 over the sorted, <c>\n</c>-joined stems; <c>-</c> for none.</summary>
    internal static string Digest(SCG.IEnumerable<string> stems)
    {
        var sorted = stems.OrderBy(s => s, StringComparer.Ordinal).ToArray();
        if (sorted.Length == 0)
            return "-";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", sorted)));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    /// <summary>The allowlist rows the FULL run measured for <paramref name="twin"/> (keeping an existing row's reason).</summary>
    private static SCG.IEnumerable<string> ActualPinRows(string twin, SCG.IReadOnlyDictionary<PinKey, SortedSet<string>> full,
        SCG.IReadOnlyDictionary<PinKey, SortedSet<string>> regular, SCG.IReadOnlyList<Row> rows)
    {
        var reasons = rows.ToDictionary(r => new PinKey(r.Action, r.Bucket, r.Cite), r => r.Reason);
        foreach (var (key, stems) in full.OrderBy(p => p.Key.ToString(), StringComparer.Ordinal))
        {
            var sampled = regular.TryGetValue(key, out var r) ? r : new SortedSet<string>(StringComparer.Ordinal);
            var reason = reasons.TryGetValue(key, out var kept) ? kept : "TODO reason";
            yield return $"{key.Action} {twin} {key.Bucket} {stems.Count} {Digest(stems)} {sampled.Count} {Digest(sampled)} # {key.Cite} {reason}";
        }
    }

    /// <summary>The identity twin's offered action set per request key for <paramref name="stem"/>.</summary>
    private static SCG.Dictionary<string, SCG.HashSet<string>> IdentityOffers(string stem)
    {
        if (s_identityOffers.TryGetValue(stem, out var cached))
            return cached;
        var text = SweepCorpus.Corpus.Value.Corpus[stem].Source;
        var doc = SweepText.Of(text);
        var offers = new SCG.Dictionary<string, SCG.HashSet<string>>(StringComparer.Ordinal);
        using var session = s_driver.Value.Open(text);
        foreach (var request in BuildRequests(doc, session.Diagnostics))
            offers[request.Key] = OfferedNames(session.Request(request.Range, ContextOf(request)), out _);
        return offers;
    }

    private static Diagnostic[] ContextOf(SweepRequest request)
        => request.Diagnostic is null ? Array.Empty<Diagnostic>() : new[] { request.Diagnostic };

    private static SCG.HashSet<string> OfferedNames(IReadOnlyList<CodeAction> actions, out SCG.List<string> unknown)
    {
        var names = new SCG.HashSet<string>(StringComparer.Ordinal);
        unknown = new SCG.List<string>();
        foreach (var action in actions)
        {
            var spec = ActionOf(action.Title);
            if (spec is null)
                unknown.Add(action.Title);
            else
                names.Add(spec.Name);
        }

        return names;
    }

    /// <summary>
    /// Runs every request of (stem, twin) and judges every owned action, grouped by action row name. Each failing cell
    /// records whether regular mode judges it (the stem runs in regular mode, and a <c>diagnostics</c> cell is sampled),
    /// so one FULL run measures both pin pairs.
    /// </summary>
    private static SCG.SortedDictionary<string, Group> RunCells(string stem, string twin, bool full, out int requestCount, out bool unparseable)
    {
        var groups = new SCG.SortedDictionary<string, Group>(StringComparer.Ordinal);
        Group G(string action) => groups.TryGetValue(action, out var g) ? g : groups[action] = new Group();
        requestCount = 0;
        var source = SweepCorpus.Corpus.Value.Corpus[stem].Source;
        var text = TwinOf(source, twin);
        var doc = SweepText.Of(text);
        unparseable = !doc.Parses;
        if (unparseable)
            return groups;
        if (doc.UniformBreak is null)
            throw new InvalidOperationException($"{stem} ({twin}): the source mixes line breaks; the lineBreak oracle has no answer (a test error)");

        var stemRunsRegular = Runs(stem, twin, full: false);
        var identityOffers = OfferTwins.Contains(twin) ? IdentityOffers(stem) : null;
        var judgedSignatures = new SCG.HashSet<string>(StringComparer.Ordinal);
        using var session = s_driver.Value.Open(text);
        var published = session.Diagnostics;
        var requests = BuildRequests(doc, published);
        requestCount = requests.Count;
        var keepIdentity = twin == Identity && Runs(stem, Crlf, full) ? new SCG.Dictionary<string, SCG.HashSet<string>>(StringComparer.Ordinal) : null;
        foreach (var request in requests)
        {
            var actions = session.Request(request.Range, ContextOf(request));
            var offered = OfferedNames(actions, out var unknown);
            if (unknown.Count > 0)
                throw new InvalidOperationException($"{stem} ({twin}) {request.Key}: titles outside the roster: {string.Join(" | ", unknown)} — add them to Actions");
            keepIdentity?.TryAdd(request.Key, offered);

            if (identityOffers is not null && identityOffers.TryGetValue(request.Key, out var expected))
            {
                foreach (var name in expected.Union(offered))
                    G(name).OfferObservations++;
                foreach (var (name, detail) in OfferVerdicts(expected, offered))
                    G(name).Fail(Offer, ShapeIssue, $"{request.Key}: {detail}", stemRunsRegular);
            }

            foreach (var action in actions)
            {
                var spec = ActionOf(action.Title)!;
                if (!spec.Owners.Contains(request.Kind))
                {
                    s_unownedOffers.AddOrUpdate(spec.Name, 1, (_, n) => n + 1);
                    continue;
                }

                IReadOnlyList<TextEdit> edits;
                try
                {
                    edits = LspCodeActionDriver.EditsOf(action, session.Uri);
                }
                catch (InvalidOperationException ex)
                {
                    G(spec.Name).Cells++;
                    G(spec.Name).Fail(Edits, ShapeIssue, $"{request.Key}: {ex.Message}", stemRunsRegular);
                    continue;
                }

                var signature = spec.Name + "|" + string.Join("|", edits.Select(e =>
                    $"{e.Range.Start.Line}:{e.Range.Start.Character}-{e.Range.End.Line}:{e.Range.End.Character}={e.NewText}"));
                if (!judgedSignatures.Add(signature))
                    continue;

                var group = G(spec.Name);
                group.Cells++;
                var regularDiagnostics = JudgesDiagnostics(stem, twin, request.Key, spec.Name, full: false);
                var judgeDiagnostics = full || regularDiagnostics;
                var exemptions = ExemptionsOf(spec, request, doc, published);
                foreach (var (bucket, detail, cite) in Judge(doc, spec, edits, exemptions, judgeDiagnostics ? s_sweepApi.Value : null))
                {
                    var regular = stemRunsRegular && (bucket != Diagnostics || regularDiagnostics);
                    group.Fail(bucket, cite, $"{request.Key}: {detail}", regular);
                }
            }
        }

        if (keepIdentity is not null)
            s_identityOffers[stem] = keepIdentity;
        return groups;
    }

    // ================================================================
    // Allowlist (pins)
    // ================================================================

    /// <summary>One pin: the failing stems of (action, twin, bucket, cite), as a count and a digest, for FULL and regular mode.</summary>
    internal sealed record Row(string Action, string Twin, string Bucket, int FullCount, string FullDigest, int RegularCount, string RegularDigest, string Cite, string Reason);

    private static readonly Lazy<IReadOnlyList<Row>> Allowlist = new(ReadAllowlist);

    private static readonly System.Text.RegularExpressions.Regex DigestShape = new("^[0-9a-f]{16}$");

    /// <summary>
    /// <c>action twin bucket fullCount fullDigest regularCount regularDigest # #issue reason</c>. Refused: an unknown action,
    /// twin or bucket; a bucket the action's shape class never runs; an <c>offer</c> pin off the offer twins; a cite
    /// outside <see cref="Cites"/> or none; a malformed count or digest (16 lowercase hex characters, or <c>-</c> exactly
    /// when the count is 0); a FULL count of 0 (a drained pin is deleted); a regular count above the FULL count; a
    /// duplicate (action, twin, bucket, cite).
    /// </summary>
    internal static IReadOnlyList<Row> ParseAllowlist(IEnumerable<string> lines)
    {
        var rows = new SCG.List<Row>();
        var keys = new SCG.HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var split = line.IndexOf(" #", StringComparison.Ordinal);
            var fields = (split < 0 ? line : line[..split]).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var tail = split < 0 ? "" : line[(split + 2)..].Trim();
            var citeMatch = System.Text.RegularExpressions.Regex.Match(tail, @"^(#\d+)\b\s*(.*)$");
            string Refuse(string why) => $"Conformance/{AllowlistFileName}: '{line}' — {why}";
            var spec = fields.Length == 7 ? Actions.FirstOrDefault(a => a.Name == fields[0]) : null;
            if (fields.Length != 7 || spec is null || !Twins.Contains(fields[1]) || !Buckets.Contains(fields[2]))
            {
                throw new InvalidOperationException(Refuse("must read `action twin bucket fullCount fullDigest regularCount regularDigest # #issue reason` "
                    + $"with action in {{{string.Join(", ", Actions.Select(a => a.Name))}}}, twin in {{{string.Join(", ", Twins)}}} "
                    + $"and bucket in {{{string.Join(", ", Buckets)}}}."));
            }

            if (!BucketsOf[spec.Shape].Contains(fields[2]))
                throw new InvalidOperationException(Refuse($"{spec.Name} is a {spec.Shape} action, which {fields[2]} never judges."));
            if (fields[2] == Offer && !OfferTwins.Contains(fields[1]))
                throw new InvalidOperationException(Refuse($"offer is judged on {string.Join("/", OfferTwins)} only."));
            if (!citeMatch.Success || !Cites.Contains(citeMatch.Groups[1].Value))
                throw new InvalidOperationException(Refuse($"must cite one of {string.Join(", ", Cites)} first (# #N reason)."));
            if (!int.TryParse(fields[3], out var fullCount) || !int.TryParse(fields[5], out var regularCount) || fullCount < 0 || regularCount < 0)
                throw new InvalidOperationException(Refuse("counts must be non-negative integers."));
            foreach (var (count, digest) in new[] { (fullCount, fields[4]), (regularCount, fields[6]) })
            {
                if (count == 0 ? digest != "-" : !DigestShape.IsMatch(digest))
                    throw new InvalidOperationException(Refuse($"digest '{digest}' is malformed for count {count} (16 lowercase hex, or - for 0)."));
            }

            if (fullCount == 0)
                throw new InvalidOperationException(Refuse("a FULL count of 0 is a drained pin: delete the row."));
            if (regularCount > fullCount)
                throw new InvalidOperationException(Refuse("the regular count cannot exceed the FULL count (regular mode judges a subset)."));
            var cite = citeMatch.Groups[1].Value;
            if (!keys.Add($"{fields[0]} {fields[1]} {fields[2]} {cite}"))
                throw new InvalidOperationException(Refuse("is listed twice."));
            rows.Add(new Row(fields[0], fields[1], fields[2], fullCount, fields[4], regularCount, fields[6], cite, citeMatch.Groups[2].Value));
        }

        return rows;
    }

    private static IReadOnlyList<Row> ReadAllowlist()
    {
        var path = Path.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Lsp.Tests", "Conformance", AllowlistFileName);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Conformance/{AllowlistFileName} is missing. Its presence is what arms the ratchet.");
        return ParseAllowlist(File.ReadAllLines(path));
    }

    private static readonly string D1 = Digest(new[] { "a/b" });

    [Fact]
    public void Allowlist_Grammar_RefusesMalformedRows()
    {
        ParseAllowlist(new[] { $"wrap-try identity line 1 {D1} 1 {D1} # #2232 nested body flattened" }).Should().ContainSingle()
            .Which.Should().Be(new Row("wrap-try", Identity, Line, 1, D1, 1, D1, ShapeIssue, "nested body flattened"));
        ParseAllowlist(new[] { $"wrap-try identity line 1 {D1} 0 - # #2232" }).Should().ContainSingle();
        foreach (var bad in new[]
                 {
                     $"wrap-try identity line 1 {D1} 1 {D1}",                   // no cite
                     $"wrap-try identity line 1 {D1} 1 {D1} # no issue",        // cite without #N
                     $"wrap-try identity line 1 {D1} 1 {D1} # #1234",           // a cite outside the three
                     $"wrap-try wide line 1 {D1} 1 {D1} # #2232",               // unknown twin
                     $"frobnicate identity line 1 {D1} 1 {D1} # #2232",         // unknown action
                     $"wrap-try identity depth 1 {D1} 1 {D1} # #2232",          // unknown bucket
                     $"rename identity line 1 {D1} 1 {D1} # #2232",             // Rewrite never judges line
                     $"wrap-try identity offer 1 {D1} 1 {D1} # #2232",          // offer off the offer twins
                     $"wrap-try identity line 1 {D1[..15]} 1 {D1} # #2232",     // digest too short
                     $"wrap-try identity line 1 {D1.ToUpperInvariant()} 1 {D1} # #2232", // digest not lowercase
                     $"wrap-try identity line 1 - 1 {D1} # #2232",              // - with a non-zero count
                     $"wrap-try identity line 0 - 0 - # #2232",                 // drained pin
                     $"wrap-try identity line 1 {D1} 2 {D1} # #2232",           // regular above FULL
                     $"wrap-try identity line x {D1} 1 {D1} # #2232",           // count not a number
                 })
        {
            FluentActions.Invoking(() => ParseAllowlist(new[] { bad })).Should().Throw<InvalidOperationException>(bad);
        }

        FluentActions.Invoking(() => ParseAllowlist(new[] { $"wrap-try cr line 1 {D1} 1 {D1} # #2232", $"wrap-try cr line 1 {D1} 1 {D1} # #2232 again" }))
            .Should().Throw<InvalidOperationException>().WithMessage("*twice*");
    }

    /// <summary>The pin ratchet: a matching pin holds; an unpinned key, a changed count, a swapped stem (same count) and a drained key all fail.</summary>
    [Fact]
    public void ComparePins_CountAndDigestMustBothMatch()
    {
        var key = new PinKey("wrap-try", Line, ShapeIssue);
        SCG.Dictionary<PinKey, SortedSet<string>> Actual(params string[] stems)
            => new() { [key] = new SortedSet<string>(stems, StringComparer.Ordinal) };
        var pin = new Row("wrap-try", Identity, Line, 2, Digest(new[] { "a", "b" }), 2, Digest(new[] { "a", "b" }), ShapeIssue, "");
        var sink = new SCG.Dictionary<string, SortedSet<string>>();

        ComparePins(Identity, "full", Actual("b", "a"), new[] { pin }, r => (r.FullCount, r.FullDigest), sink).Should().BeEmpty();
        ComparePins(Identity, "full", Actual("a", "b"), Array.Empty<Row>(), r => (r.FullCount, r.FullDigest), sink)
            .Should().ContainSingle().Which.Should().Contain("no pin row");
        ComparePins(Identity, "full", Actual("a", "b", "c"), new[] { pin }, r => (r.FullCount, r.FullDigest), sink)
            .Should().ContainSingle().Which.Should().Contain("actual count=3");
        ComparePins(Identity, "full", Actual("a", "c"), new[] { pin }, r => (r.FullCount, r.FullDigest), sink)
            .Should().ContainSingle().Which.Should().Contain("actual count=2", "a fix plus a regression keeps the count and moves the digest");
        ComparePins(Identity, "full", new SCG.Dictionary<PinKey, SortedSet<string>>(), new[] { pin }, r => (r.FullCount, r.FullDigest), sink)
            .Should().ContainSingle().Which.Should().Contain("drain the pin");
        sink.Should().ContainKey($"full {Identity} {key}").WhoseValue.Should().BeEmpty();
        Digest(Array.Empty<string>()).Should().Be("-");
        Digest(new[] { "b", "a" }).Should().Be(Digest(new[] { "a", "b" })).And.MatchRegex("^[0-9a-f]{16}$");
    }

    // ================================================================
    // Census
    // ================================================================

    /// <summary>
    /// <c>CODEACTION-CENSUS</c> lines: the corpus, mode and strides; per twin the stems run, requests, requests per stem,
    /// wall time and unparseable twin documents; judged cells per (action, twin) and failing stems per (action, twin,
    /// bucket, cite); offers on requests an action does not own; the pins per cite and the pinned failing-stem totals
    /// per cite. Floors (when every twin's sweep ran in this process): ≥ 1 judged cell per (Move action, twin). The pin
    /// count is anchored.
    /// </summary>
    [Fact]
    public void Census_CellsPerActionTwinAndBucket_AreStated()
    {
        var corpus = SweepCorpus.Corpus.Value;
        _output.WriteLine($"CODEACTION-CENSUS corpus={corpus.Corpus.Count} single-file={corpus.SingleFile} excluded-unparseable={corpus.Unparseable.Count} "
            + $"mode={(FullMode ? "full" : "regular")} twin-stride={TwinStride} diagnostics-stride={DiagnosticsStride} dop={Parallelism} "
            + $"twin-stems={corpus.Corpus.Keys.Count(k => SweepCorpus.StableBucket(k, TwinStride) == 0)}");
        foreach (var twin in Twins)
        {
            var stems = s_stemsRun.GetValueOrDefault(twin);
            var requests = s_requests.GetValueOrDefault(twin);
            _output.WriteLine($"CODEACTION-CENSUS twin {twin} stems={stems} requests={requests} "
                + $"requests-per-stem={(stems == 0 ? "-" : (requests / (double)stems).ToString("F1", System.Globalization.CultureInfo.InvariantCulture))} "
                + $"seconds={s_seconds.GetValueOrDefault(twin):F1} unparseable-twin={s_unparseableTwins.GetValueOrDefault(twin)}");
        }

        foreach (var spec in Actions)
        {
            foreach (var twin in Twins)
            {
                var failing = s_failingStems.Where(p => p.Key.Twin == twin && p.Key.Key.Action == spec.Name)
                    .OrderBy(p => p.Key.Key.ToString(), StringComparer.Ordinal)
                    .Select(p => $"{p.Key.Key.Bucket}/{p.Key.Key.Cite}={p.Value}");
                _output.WriteLine($"CODEACTION-CENSUS cells {spec.Name} {twin}={s_cells.GetValueOrDefault((spec.Name, twin))} failing-stems: {string.Join(" ", failing)}");
            }

            _output.WriteLine($"CODEACTION-CENSUS unowned-offers {spec.Name}={s_unownedOffers.GetValueOrDefault(spec.Name)}");
        }

        var rows = Allowlist.Value;
        foreach (var cite in Cites)
        {
            var pins = rows.Where(r => r.Cite == cite).ToList();
            _output.WriteLine($"CODEACTION-CENSUS pins {cite}={pins.Count} pinned-failing-stems full={pins.Sum(r => r.FullCount)} regular={pins.Sum(r => r.RegularCount)}");
        }

        foreach (var bucket in Buckets)
            _output.WriteLine($"CODEACTION-CENSUS pins bucket {bucket}={rows.Count(r => r.Bucket == bucket)}");

        var everyTwinRan = Twins.All(t => s_stemsRun.ContainsKey(t));
        if (everyTwinRan)
        {
            foreach (var spec in Actions.Where(a => a.Shape == ShapeClass.Move))
            {
                foreach (var twin in Twins)
                    s_cells.GetValueOrDefault((spec.Name, twin)).Should().BeGreaterThan(0, $"the sweep judges ≥ 1 {spec.Name} cell on the {twin} twin");
            }
        }
        else
        {
            _output.WriteLine("CODEACTION-CENSUS floors skipped: not every twin's sweep ran in this process");
        }

        rows.Count.Should().Be(109, "P47 Phase 1 Task 3 populated 182 pins from one FULL run (132 cite #2232, 15 #2294, 35 #2296); Phase 2 Task 1 (one line table, the document's break) left 158 (106 #2232, 16 #2294, 36 #2296); Phase 2 Task 2 (wrap, extract method, inline, remove unused import move whole regions) left 140 (88 #2232, 16 #2294, 36 #2296); Phase 2 Task 3 (Organize Imports moves units) left 124 (72 #2232, 16 #2294, 36 #2296); Phase 2 Task 4 (convert forms re-spell headers, move bodies) left 109 (53 #2232, 16 #2294, 40 #2296); a commit that changes the count says why");
    }
}

/// <summary>
/// Orders <see cref="CodeActionShapeSweepTests"/> (one collection, so its cases run in this order): the identity
/// theory first — it keeps the identity offers the <c>crlf</c>/<c>cr</c> theories compare against — then the other
/// twins, then the facts, then the census, which reads what the theories ran.
/// </summary>
public sealed class CodeActionShapeSweepOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
        => testCases.OrderBy(t => Rank(t.TestMethod.Method.Name)).ThenBy(t => t.DisplayName, StringComparer.Ordinal);

    private static int Rank(string method) => method switch
    {
        _ when method.StartsWith("Identity_", StringComparison.Ordinal) => 0,
        _ when method.StartsWith("Crlf_", StringComparison.Ordinal) => 1,
        _ when method.StartsWith("Cr_", StringComparison.Ordinal) => 2,
        _ when method.StartsWith("CommentInjected_", StringComparison.Ordinal) => 3,
        _ when method.StartsWith("Census_", StringComparison.Ordinal) => 5,
        _ => 4,
    };
}
