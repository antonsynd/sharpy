using System.Text;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// The block kinds of the #1560 matrix and one way to wrap a statement body in each of them.
/// Shared by <see cref="BlockScopeRedeclarationMatrixTests"/> (executing cells) and
/// <c>TargetBindingRecordingTests</c> (recorded facts), so both range over the same rows.
/// </summary>
internal static class BlockKinds
{
    /// <summary>A context manager for the <c>with</c> kind; harmless in every other program.</summary>
    public const string Prelude =
        "class Resource:\n" +
        "    name: str\n" +
        "    def __init__(self, name: str):\n" +
        "        self.name = name\n" +
        "    def __enter__(self) -> Self:\n" +
        "        return self\n" +
        "    def __exit__(self) -> None:\n" +
        "        pass\n\n";

    /// <summary>Kinds that take a statement body (the lambda-parameter and comprehension-target kinds do not).</summary>
    public static readonly string[] BodyKinds =
    {
        "if", "elif", "else", "while", "while-else", "for", "for-else",
        "try", "except", "try-else", "finally", "with", "match-arm", "defer", "nested-def",
    };

    /// <summary>A <c>defer</c> block runs at scope exit, after everything that follows it.</summary>
    public static bool RunsAtExit(string kind) => kind == "defer";

    public static FeatureFlags FeaturesFor(string kind)
        => kind == "defer" ? FeatureFlags.None.Enable("defer") : FeatureFlags.None;

    /// <summary>
    /// Wraps <paramref name="body"/> (lines at zero indentation, may contain its own nesting) in
    /// block kind <paramref name="kind"/>; <paramref name="i"/> makes the wrapper's own names
    /// unique so two wrappers can be siblings. The result is indented for a function body.
    /// </summary>
    public static string Wrap(string kind, int i, string body)
    {
        var (prefix, depth, suffix) = kind switch
        {
            "if" => (new[] { "if True:" }, 1, System.Array.Empty<string>()),
            "elif" => (new[] { "if False:", "    pass", "elif True:" }, 1, System.Array.Empty<string>()),
            "else" => (new[] { "if False:", "    pass", "else:" }, 1, System.Array.Empty<string>()),
            "while" => (new[] { $"d{i} = False", $"while not d{i}:" }, 1, new[] { $"    d{i} = True" }),
            "while-else" => (new[] { $"d{i} = False", $"while d{i}:", "    pass", "else:" }, 1, System.Array.Empty<string>()),
            "for" => (new[] { $"for i{i} in range(1):" }, 1, System.Array.Empty<string>()),
            "for-else" => (new[] { $"for i{i} in range(0):", "    pass", "else:" }, 1, System.Array.Empty<string>()),
            "try" => (new[] { "try:" }, 1, new[] { "except Exception:", "    pass" }),
            "except" => (new[] { "try:", $"    raise ValueError(\"e{i}\")", "except ValueError:" }, 1, System.Array.Empty<string>()),
            "try-else" => (new[] { "try:", "    pass", "except Exception:", "    pass", "else:" }, 1, System.Array.Empty<string>()),
            "finally" => (new[] { "try:", "    pass", "finally:" }, 1, System.Array.Empty<string>()),
            "with" => (new[] { $"with Resource(\"r{i}\") as r{i}:" }, 1, System.Array.Empty<string>()),
            "match-arm" => (new[] { $"match {i}:", $"    case {i}:" }, 2, new[] { "    case _:", "        pass" }),
            "defer" => (new[] { "defer:" }, 1, System.Array.Empty<string>()),
            "nested-def" => (new[] { $"def inner{i}() -> None:" }, 1, new[] { $"inner{i}()" }),
            _ => throw new System.ArgumentException($"unknown block kind '{kind}'", nameof(kind)),
        };

        var sb = new StringBuilder();
        foreach (var line in prefix)
            sb.Append("    ").Append(line).Append('\n');
        var pad = new string(' ', 4 * (depth + 1));
        foreach (var line in body.Split('\n'))
            sb.Append(pad).Append(line).Append('\n');
        foreach (var line in suffix)
            sb.Append("    ").Append(line).Append('\n');
        return sb.ToString();
    }

    /// <summary>A whole program: the prelude, <c>def main()</c>, then the given function-body text.</summary>
    public static string Program(string mainBody)
        => Prelude + "def main() -> None:\n" + mainBody;

    /// <summary>
    /// Ratcheted known-red cells (verification-contract §1: an allowlist entry cites an issue and is
    /// deleted when fixed). <c>for-else</c>/<c>while-else</c> bodies are never type-checked
    /// (#1656), so every cell that needs a symbol, a TargetBinding or a spelling there is red;
    /// definite assignment treats a <c>defer</c> body as executing in place (#1657).
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<(string Kind, string Cell), string> KnownRed = new()
    {
        [("for-else", "OuterRedeclareAfterBare")] = "#1656",
        [("for-else", "OuterRedeclareAfterAnnotated")] = "#1656",
        [("for-else", "WriteThrough")] = "#1656",
        [("for-else", "UseBeforeAssign")] = "#1656",
        [("for-else", "SiblingRedeclareRecorded")] = "#1656",
        [("for-else", "OuterReassignRecorded")] = "#1656",
        [("while-else", "OuterRedeclareAfterBare")] = "#1656",
        [("while-else", "OuterRedeclareAfterAnnotated")] = "#1656",
        [("while-else", "WriteThrough")] = "#1656",
        [("while-else", "UseBeforeAssign")] = "#1656",
        [("while-else", "SiblingRedeclareRecorded")] = "#1656",
        [("while-else", "OuterReassignRecorded")] = "#1656",
        [("defer", "UseBeforeAssign")] = "#1657",
    };

    /// <summary>
    /// Runs a cell's assertion. A cell not in <see cref="KnownRed"/> must pass. A known-red cell
    /// must still FAIL its assertion: the moment it passes, this fails loudly so the entry (and
    /// the issue) are drained — never a silent skip.
    /// </summary>
    public static void Cell(string kind, string cell, System.Action assertion)
    {
        if (!KnownRed.TryGetValue((kind, cell), out var issue))
        {
            assertion();
            return;
        }

        try
        {
            assertion();
        }
        catch (Xunit.Sdk.XunitException)
        {
            return; // still red, as the issue records
        }

        Xunit.Assert.Fail($"[{kind}/{cell}] now passes — {issue} is fixed for this cell: delete its KnownRed entry.");
    }
}
