using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Text;
using SModule = Sharpy.Compiler.Parser.Ast.Module;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The P40 oracle (#2166): <b>a backtick-escaped contextual keyword parses exactly as a fresh
/// identifier would.</b> The escaped twin wraps one token in backticks; the renamed twin replaces
/// the same token with a fresh identifier of the escaped spelling's length
/// (<see cref="RenamedSpelling"/>), so the two texts are byte-aligned and every diagnostic position
/// is comparable. The renamed twin is the ground truth for "this token is an identifier here".
/// <see cref="Outcome"/> is the parse-diagnostic multiset <c>(code, line, column)</c> when either
/// side fails, else the preorder node-type sequence of the AST — every reachable AST record's type
/// and enum-valued properties (accessor, modifier, variance, observer kind); names, flags,
/// positions and trivia excluded, since those are what the two twins legitimately differ in.
/// Shared by <see cref="ContextualKeywordEscapeSweepTests"/> and
/// <c>Parser/ContextualKeywordEscapeMatrixTests</c>.
/// </summary>
internal static class ContextualKeywordEscapeOracle
{
    private const string AstNamespace = "Sharpy.Compiler.Parser.Ast";

    /// <summary>One parse of a twin: the diagnostic multiset, or the AST shape when it parses clean.</summary>
    internal sealed record Outcome(bool HasErrors, IReadOnlyList<string> Diagnostics, string Shape, SModule? Module)
    {
        public bool SameAs(Outcome other)
            => HasErrors == other.HasErrors
                && (HasErrors ? Diagnostics.SequenceEqual(other.Diagnostics, StringComparer.Ordinal) : Shape == other.Shape);

        public override string ToString()
            => HasErrors ? "parse error [" + string.Join(", ", Diagnostics) + "]" : "parses";
    }

    /// <summary>Lexes and parses <paramref name="source"/> the way the compiler does (no trivia, no feature flags).</summary>
    public static Outcome Observe(string source)
    {
        var lex = FileCompilationPipeline.Lex(new SourceText(source, "<cke>"), NullLogger.Instance);
        if (lex.HasErrors)
            return new Outcome(true, Positions(lex.Diagnostics.GetAll()), "", null);
        var parse = FileCompilationPipeline.Parse(lex.Tokens, NullLogger.Instance);
        if (parse.HasErrors || parse.Module == null)
            return new Outcome(true, Positions(parse.Diagnostics.GetAll()), "", parse.Module);
        return new Outcome(false, Array.Empty<string>(), Shape(parse.Module), parse.Module);
    }

    private static IReadOnlyList<string> Positions(IEnumerable<global::Sharpy.Compiler.Diagnostics.CompilerDiagnostic> diagnostics)
        => diagnostics.Select(d => $"{d.Code}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();

    /// <summary>The escaped spelling of <paramref name="keyword"/>.</summary>
    public static string EscapedSpelling(string keyword) => $"`{keyword}`";

    /// <summary>
    /// A fresh identifier as long as <paramref name="keyword"/>'s escaped spelling (so the twins are
    /// byte-aligned): one letter repeated, the first letter whose run does not occur in
    /// <paramref name="source"/>.
    /// </summary>
    public static string RenamedSpelling(string keyword, string source)
    {
        foreach (var letter in "zqjxv")
        {
            var name = new string(letter, keyword.Length + 2);
            if (!source.Contains(name, StringComparison.Ordinal))
                return name;
        }

        throw new InvalidOperationException($"no fresh identifier of length {keyword.Length + 2} for '{keyword}'");
    }

    /// <summary>The escaped and renamed twins of <paramref name="source"/> at one identifier token.</summary>
    public static (string Escaped, string Renamed) Twins(string source, Token token)
    {
        var before = source[..token.Position];
        var after = source[(token.Position + token.Length)..];
        return (before + EscapedSpelling(token.Value) + after,
            before + RenamedSpelling(token.Value, source) + after);
    }

    /// <summary>Every AST record reachable from <paramref name="root"/>, preorder, each once.</summary>
    public static IEnumerable<object> Walk(object root)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<object>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!seen.Add(node))
                continue;
            yield return node;
            var children = Children(node).ToList();
            for (var i = children.Count - 1; i >= 0; i--)
                stack.Push(children[i]);
        }
    }

    /// <summary>Whether an AST record carries <c>Name == name</c> and <c>IsNameBacktickEscaped == escaped</c> (an <c>Identifier</c>, a type annotation, …).</summary>
    public static bool HasName(SModule module, string name, bool escaped)
        => Walk(module).Any(node =>
        {
            var props = Properties(node.GetType());
            var nameProp = props.FirstOrDefault(p => p.Name == "Name" && p.PropertyType == typeof(string));
            var flagProp = props.FirstOrDefault(p => p.Name == "IsNameBacktickEscaped" && p.PropertyType == typeof(bool));
            return nameProp != null && flagProp != null
                && (string?)nameProp.GetValue(node) == name && (bool)flagProp.GetValue(node)! == escaped;
        });

    /// <summary>The preorder node-type sequence: each record's type and its enum-valued properties.</summary>
    public static string Shape(SModule module)
    {
        var text = new StringBuilder();
        foreach (var node in Walk(module))
        {
            text.Append(node.GetType().Name);
            foreach (var prop in Properties(node.GetType()).Where(p => p.PropertyType.IsEnum))
                text.Append(' ').Append(prop.Name).Append('=').Append(prop.GetValue(node));
            text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>A one-line account of where two outcomes part.</summary>
    public static string Difference(Outcome escaped, Outcome renamed)
    {
        if (escaped.HasErrors || renamed.HasErrors)
            return $"escaped: {escaped}; renamed: {renamed}";
        var a = escaped.Shape.Split('\n');
        var b = renamed.Shape.Split('\n');
        var first = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], Math.Min(a.Length, b.Length));
        return $"both parse; shapes part at node {first}: escaped '{At(a, first)}' vs renamed '{At(b, first)}'";

        static string At(string[] lines, int i) => i < lines.Length ? lines[i] : "<end>";
    }

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    private static PropertyInfo[] Properties(Type type)
        => PropertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray());

    private static bool IsAstRecord(object value) => value.GetType().Namespace == AstNamespace && !value.GetType().IsEnum;

    private static IEnumerable<object> Children(object node)
    {
        foreach (var prop in Properties(node.GetType()))
        {
            var type = prop.PropertyType;
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum)
                continue;
            var value = prop.GetValue(node);
            if (value == null)
                continue;
            if (IsAstRecord(value))
            {
                yield return value;
                continue;
            }

            if (value is IEnumerable items && value is not string && !IsDefaultImmutableArray(value))
            {
                foreach (var item in items)
                {
                    if (item != null && IsAstRecord(item))
                        yield return item;
                }
            }
        }
    }

    private static bool IsDefaultImmutableArray(object value)
    {
        var type = value.GetType();
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>)
            && (bool)type.GetProperty(nameof(ImmutableArray<int>.IsDefault))!.GetValue(value)!;
    }
}
