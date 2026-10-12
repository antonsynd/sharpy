using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Lsp.Tests.Conformance;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Refactoring;

/// <summary>
/// The movers' regressions by direction (R1–R5, found by the code-action shape sweep after P47 Phase 2
/// Task 2) and the parse cells no task named (plan-e39946 P2.T5): no code action turns a parsing document
/// into one that does not parse (P47, #2232). Driven through the real code-action handler
/// (<see cref="LspCodeActionDriver"/>); an offered action is judged on its byte-exact APPLIED text, and where
/// no legal text exists the action is not offered — each such "not offered" fact is paired with a positive
/// control on the same document shape where the action IS offered.
/// </summary>
public sealed class ParseSafetyTests : IDisposable
{
    private readonly LspCodeActionDriver _driver = new(new CompilerApi(Sharpy.Compiler.Logging.NullLogger.Instance, StdlibReferences.Paths()));

    public void Dispose() => _driver.Dispose();

    private (IReadOnlyList<string> Titles, string? Applied) Request(
        string text, int sl, int sc, int el, int ec, string titlePrefix, string? diagnosticCode = null)
    {
        using var session = _driver.Open(text);
        var context = diagnosticCode is null
            ? Array.Empty<Diagnostic>()
            : session.Diagnostics.Where(d => d.Code?.String == diagnosticCode).ToArray();
        var actions = session.Request(new LspRange(new Position(sl, sc), new Position(el, ec)), context);
        var action = actions.FirstOrDefault(a => a.Title.StartsWith(titlePrefix, StringComparison.Ordinal));
        return (actions.Select(a => a.Title).ToList(), action is null ? null : session.Apply(action));
    }

    private string Applied(string text, int sl, int sc, int el, int ec, string titlePrefix, string? diagnosticCode = null)
    {
        var (titles, applied) = Request(text, sl, sc, el, ec, titlePrefix, diagnosticCode);
        applied.Should().NotBeNull($"'{titlePrefix}' is offered; offered: {string.Join(" | ", titles)}");
        return applied!;
    }

    private void NotOffered(string text, int sl, int sc, int el, int ec, string titlePrefix)
    {
        var (titles, _) = Request(text, sl, sc, el, ec, titlePrefix);
        titles.Should().NotContain(t => t.StartsWith(titlePrefix, StringComparison.Ordinal));
    }

    // ---- R6: the indent unit is a BLOCK's indent, never a continuation line's ----

    /// <summary>
    /// A continuation line inside brackets at column 1 (` b: int) -> int:`) is not block indentation; the
    /// conversion moves its bodies by the document's real unit. 87f371b00 read the unit from the first indented
    /// line (1 space: SPY0013); the base doubled the header's indent inside the def.
    /// </summary>
    [Fact]
    public void R6_ConvertToMatch_ContinuationLineAtColumnOne_IsNotTheIndentUnit()
    {
        Applied("def gcd(a: int,\n b: int) -> int:\n    if b == 0:  # z\n        return a\n    return gcd(b, a % b)\n\ndef main():\n    print(gcd(4, 6))\n",
                2, 4, 2, 4, "Convert to match")
            .Should().Be("def gcd(a: int,\n b: int) -> int:\n    match b:\n        case 0:  # z\n            return a\n    return gcd(b, a % b)\n\n"
                + "def main():\n    print(gcd(4, 6))\n");
    }

    /// <summary>
    /// The scrutinee's text is written into every `if`/`elif` header, so a comment inside it would be written once
    /// per arm: with two arms, not offered; with one arm it travels inside the scrutinee, written once.
    /// </summary>
    [Fact]
    public void MatchToIf_CommentInTheScrutinee_NotOfferedWithTwoArms_CarriedOnceWithOne()
    {
        NotOffered("def f() -> int:\n    return 5\n\ndef main():\n    match f(  # c\n    ):\n        case 1:\n            print(1)\n        case 2:\n            print(2)\n",
            4, 4, 4, 4, "Convert to if");
        Applied("def f() -> int:\n    return 5\n\ndef main():\n    match f(  # c\n    ):\n        case 1:\n            print(1)\n        case _:\n            print(2)\n",
                4, 4, 4, 4, "Convert to if")
            .Should().Be("def f() -> int:\n    return 5\n\ndef main():\n    if f(  # c\n    ) == 1:\n        print(1)\n    else:\n        print(2)\n");
    }

    // ---- inline variable / function: where no legal text exists, not offered ----

    /// <summary>
    /// A comment inside the initializer travels with its text to the one reference, written once (the base,
    /// 7f0502de3, applied exactly this text; e4a24985f withheld the action — a regression by direction); with two
    /// references it would be written twice, so there it is not offered. A declaration with NO reference is not
    /// offered at all (measured: the symbol is found through a reference identifier), with or without a comment —
    /// the zero-reference row pins that the comment is never dropped by a deletion. A declaration
    /// alone in its block cannot be deleted without emptying the block; a name BOUND elsewhere (`let pair,
    /// other = …`) cannot take a value. The positive control is the same module variable with no such binding.
    /// </summary>
    [Fact]
    public void InlineVariable_NoLegalText_NotOffered_PlainModuleVariable_Inlines()
    {
        Applied("def main():\n    k: int = (1 +  # one\n              2)\n    print(k)\n", 1, 4, 1, 4, "Inline variable")
            .Should().Be("def main():\n    print((1 +  # one\n              2))\n");
        NotOffered("def main():\n    k: int = (1 +  # one\n              2)\n    print(k)\n    print(k)\n", 1, 4, 1, 4, "Inline variable");
        NotOffered("def main():\n    k: int = (1 +  # one\n              2)\n    print(1)\n", 1, 4, 1, 4, "Inline variable");
        NotOffered("def main():\n    if True:\n        k: int = 1\n    print(k)\n", 2, 8, 2, 8, "Inline variable");
        NotOffered("pair: int = 1\n\ndef f() -> None:\n    let pair, other = 6, 0\n    print(pair + other)\n\ndef main():\n    print(pair)\n    f()\n",
            0, 0, 0, 0, "Inline variable");
        Applied("pair: int = 1\n\ndef main():\n    print(pair)\n", 0, 0, 0, 0, "Inline variable")
            .Should().Be("\ndef main():\n    print(1)\n");
    }

    /// <summary>
    /// A parameter is substituted at its USES only (the base also rewrote the same-spelled lambda parameter — `lambda
    /// 10: …` — and the argument text already substituted), and a non-atomic argument goes in parentheses.
    /// </summary>
    [Fact]
    public void InlineFunction_LambdaArgument_SubstitutesOnlyTheParametersUses()
    {
        Applied("def apply_twice(f: (int) -> int, x: int) -> int:\n    return f(f(x))\n\ndef main():\n    print(apply_twice(lambda x: int: x + 3, 10))\n",
                4, 10, 4, 10, "Inline function")
            .Should().Be("def apply_twice(f: (int) -> int, x: int) -> int:\n    return f(f(x))\n\n"
                + "def main():\n    print(((lambda x: int: x + 3)((lambda x: int: x + 3)(10))))\n");
    }

    /// <summary>
    /// A comprehension that rebinds the parameter's name: only the uses that resolve to the PARAMETER take the
    /// argument (`range(x)`); the comprehension's own `x` stays — a textual replace wrote `[3 for 3 in …]`.
    /// </summary>
    [Fact]
    public void InlineFunction_NameReboundInsideTheExpression_KeepsItsOwnBinding()
    {
        Applied("def g(x: int) -> list[int]:\n    return [x for x in range(x)]\n\ndef main():\n    print(g(3))\n", 4, 10, 4, 10, "Inline function")
            .Should().Be("def g(x: int) -> list[int]:\n    return [x for x in range(x)]\n\ndef main():\n    print(([x for x in range(3)]))\n");
    }

    /// <summary>A comment inside the call's brackets would be dropped with the call: not offered; without it, offered.</summary>
    [Fact]
    public void InlineFunction_CommentInsideTheCall_NotOffered_ButWithoutIt_Inlines()
    {
        NotOffered("def add(a: int, b: int) -> int:\n    return a + b\n\ndef main():\n    print(add(  # one\n        1, 2))\n", 4, 10, 4, 10, "Inline function");
        Applied("def add(a: int, b: int) -> int:\n    return a + b\n\ndef main():\n    print(add(1, 2))\n", 4, 10, 4, 10, "Inline function")
            .Should().Be("def add(a: int, b: int) -> int:\n    return a + b\n\ndef main():\n    print((1 + 2))\n");
    }

    /// <summary>An argument passed with a modifier (`in v`) is a passing convention, not an expression to substitute: not offered.</summary>
    [Fact]
    public void InlineFunction_ModifiedArgument_NotOffered()
    {
        NotOffered("def compute(x: in int) -> int:\n    return x * 2\n\ndef main():\n    v = 21\n    print(compute(in v))\n", 5, 10, 5, 10, "Inline function");
    }

    // ---- generated names are spelled so they parse ----

    /// <summary>
    /// A generated binding named by a keyword keeps its backticks (`` `let` ``); the base wrote `return let` and
    /// `let = extracted_method()`, which do not parse.
    /// </summary>
    [Fact]
    public void ExtractMethod_KeywordNamedReturnValue_IsWrittenInBackticks()
    {
        Applied("def main():\n    `let` = \"a\"\n    print(`let`)\n", 1, 0, 1, 15, "Extract method")
            .Should().Be("def extracted_method() -> str:\n    `let` = \"a\"\n    return `let`\n\ndef main():\n    `let` = extracted_method()\n    print(`let`)\n");
    }

    // ---- a statement sharing its line with its header has no whole lines of its own ----

    [Fact]
    public void Wrap_TheStubBodyOfABodilessDef_NotOffered()
    {
        // The stub body `...` sits on the header's closing line `    ) -> float: ...`: the base wrapped that line,
        // putting `try:` inside the def's parameter list.
        NotOffered("interface IShape:\n    def area(\n        self\n    ) -> float: ...\n\ndef main():\n    print(1)\n", 3, 0, 3, 19, "Wrap in try/except");
    }

    // ---- a block-ending bodiless def's LineEnd runs on to the next statement ----

    /// <summary>
    /// The class body ends in a bodiless `def other(self) -> int: ...` whose recorded LineEnd is the next
    /// statement's line; the stubs go after the def's own line, never inside `class Impl`.
    /// </summary>
    [Fact]
    public void ImplementInterface_AfterABodilessDefEndingTheClass_InsertsInsideTheClass()
    {
        Applied("interface ISized2:\n    def size(self) -> int: ...\n\n@abstract\nclass Collection(ISized2):\n    def other(self) -> int: ...\nclass Impl:\n    pass\n\n"
                + "def main():\n    print(1)\n", 4, 6, 4, 6, "Implement")
            .Should().Be("interface ISized2:\n    def size(self) -> int: ...\n\n@abstract\nclass Collection(ISized2):\n    def other(self) -> int: ...\n\n"
                + "    def size(self) -> int32:\n        raise NotImplementedError()\nclass Impl:\n    pass\n\ndef main():\n    print(1)\n");
    }

    // ---- R1: wrap in try/except where no try block can stand ----

    /// <summary>
    /// Selecting a whole interface and class from their first column selects THEM (not the interface's
    /// members); module-level declarations inside a try stop being declarations (SPY0340), so no legal text
    /// exists. The base selected the interface's members and wrapped them inside the interface body.
    /// </summary>
    [Fact]
    public void R1_Wrap_ModuleLevelDeclarations_NotOffered_ButInsideAFunctionBody_Offered()
    {
        var text = "interface I:\n    def g(self) -> int: ...\n\nclass C(I):\n    def g(self) -> int:\n        return 1\n\n"
            + "def main():\n    x = C().g()\n    print(x)\n";

        NotOffered(text, 0, 0, 6, 0, "Wrap in try/except");
        Applied(text, 8, 0, 9, 12, "Wrap in try/except").Should().Be(
            "interface I:\n    def g(self) -> int: ...\n\nclass C(I):\n    def g(self) -> int:\n        return 1\n\n"
            + "def main():\n    try:\n        x = C().g()\n        print(x)\n    except Exception as e:\n        raise\n");
    }

    /// <summary>
    /// A selection that starts at the first column of a compound statement and holds it whole selects THAT
    /// statement — the base descended into its body and wrapped only the nested statement.
    /// </summary>
    [Fact]
    public void Wrap_SelectionHoldingACompoundStatementWhole_WrapsTheStatementNotItsBody()
    {
        Applied("x = 1\nif x > 0:\n    print(x)\nprint(2)\n", 1, 0, 3, 8, "Wrap in try/except").Should().Be(
            "x = 1\ntry:\n    if x > 0:\n        print(x)\n    print(2)\nexcept Exception as e:\n    raise\n");
    }

    // ---- P2.T5c F1: an expression's replaced range never takes the line break after its last token ----

    /// <summary>
    /// A block-form match whose last arm is a nested match records its end at the next line's column 1; selected
    /// exactly by that range, the replaced span used to swallow the break (`v: str = result    print(v)`).
    /// </summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void ExtractVariable_BlockMatchEndingAtTheNextLine_KeepsTheBreak(string lineBreak)
    {
        var text = ("def main():\n    a: int = 1\n    v: str = match a:\n        case 1: \"x\"\n        case _: match a:\n"
            + "            case 2: \"y\"\n            case _: \"z\"\n    print(v)\n").Replace("\n", lineBreak, StringComparison.Ordinal);

        Applied(text, 2, 13, 7, 0, "Extract variable").Should().Be(
            ("def main():\n    a: int = 1\n    result: str = match a:\n        case 1: \"x\"\n        case _: match a:\n"
            + "            case 2: \"y\"\n            case _: \"z\"\n    v: str = result\n    print(v)\n").Replace("\n", lineBreak, StringComparison.Ordinal));
    }

    /// <summary>
    /// A bare tuple on the right of a destructuring `let` is not an initializer on its own: the synthesized
    /// declaration gives it its parentheses (the reclassified #2232 cells: `result: … = "x", "y"` does not parse).
    /// </summary>
    [Fact]
    public void ExtractVariable_BareTuple_IsParenthesizedInTheDeclaration()
    {
        Applied("def main():\n    let a, b = \"x\", \"y\"\n    print(a + b)\n", 1, 15, 1, 23, "Extract variable")
            .Should().Be("def main():\n    result: tuple[str, str] = (\"x\", \"y\")\n    let a, b = result\n    print(a + b)\n");
    }

    // ---- P2.T5c F2: Extract method never inserts between a decorator and its definition ----

    [Fact]
    public void ExtractMethod_InsideADecoratedTopLevelDef_InsertsAboveTheDecorator()
    {
        Applied("@deprecated(\"x\")\ndef main():\n    a = \"x\"\n    b = \"y\"\n", 2, 0, 3, 11, "Extract method")
            .Should().Be("def extracted_method():\n    a = \"x\"\n    b = \"y\"\n\n@deprecated(\"x\")\ndef main():\n    extracted_method()\n");
    }

    [Fact]
    public void ExtractMethod_InsideTwiceDecoratedDef_InsertsAboveBothDecorators()
    {
        Applied("@deprecated(\"x\")\n@static\ndef main():\n    a = \"x\"\n    b = \"y\"\n", 3, 0, 4, 11, "Extract method")
            .Should().Be("def extracted_method():\n    a = \"x\"\n    b = \"y\"\n\n@deprecated(\"x\")\n@static\ndef main():\n    extracted_method()\n");
    }

    [Fact]
    public void ExtractMethod_InsideAMethodOfADecoratedClass_InsertsAboveTheClassDecorator()
    {
        Applied("@dataclass\nclass A:\n    def m(self) -> None:\n        a = \"x\"\n        b = \"y\"\n", 3, 0, 4, 15, "Extract method")
            .Should().Be("def extracted_method():\n    a = \"x\"\n    b = \"y\"\n\n@dataclass\nclass A:\n    def m(self) -> None:\n        extracted_method()\n");
    }

    // ---- P2.T5b: the statements an action acts on are exactly the statements the selection covers ----

    private const string Describe = "def describe(n: int) -> str:\n    result: str = \"a\" if n == 1 else \"b\"\n    return result\n";

    private const string DescribeBodyWrapped = "def describe(n: int) -> str:\n    try:\n        result: str = \"a\" if n == 1 else \"b\"\n"
        + "        return result\n    except Exception as e:\n        raise\n";

    /// <summary>
    /// A selection ending exactly at the end of `    return result` covers it. The parser's LineEnd for a block's
    /// last statement runs on to the next token, so the base and 2abf1e5d2 left `return result` outside the try
    /// (SPY0200). A selection holding the whole def at module level means its body (a def cannot stand in a try).
    /// </summary>
    [Theory]
    [InlineData(0, 0, 2, 17)] // the whole def, ending exactly at the end of its last line
    [InlineData(1, 0, 2, 17)] // the body only
    [InlineData(1, 0, 3, 0)]  // ending at the next line's column 0
    public void Wrap_SelectionCoveringTheLastStatementExactly_WrapsIt(int sl, int sc, int el, int ec)
    {
        Applied(Describe, sl, sc, el, ec, "Wrap in try/except").Should().Be(DescribeBodyWrapped);
    }

    /// <summary>
    /// A bodiless def (`def greet(self) -> str` in an interface, an `@abstract` method) has no body of its own
    /// lines to fall back to: selecting it whole offers neither wrap nor extract method.
    /// </summary>
    [Fact]
    public void WrapAndExtract_BodilessDefSelectedWhole_NotOffered()
    {
        var text = "interface IGreeter:\n    def greet(self) -> str\n\ndef main():\n    print(1)\n";

        NotOffered(text, 1, 0, 1, 26, "Wrap in try/except");
        NotOffered(text, 1, 0, 1, 26, "Extract method");
    }

    // ---- chore R-GP (#2296): a nested declaration moved into a block is scoped to it ----

    /// <summary>
    /// A def or a type alias declared inside a function and moved into a <c>try:</c> body or a new function is
    /// scoped to that block: a read of its name after the region is undefined (SPY0200 / SPY0202). Not offered @
    /// 7f0502de3, offered with that damage @ e4a24985f (a not-offered → offered-damaged direction, measured on
    /// both binaries). The read may be a call, a value, a closure's body, an f-string hole or a type annotation,
    /// and the owner may be an <c>if</c> body. A decorated nested def is withheld even without a later read: its
    /// decorator sits above the def's first line and would stay behind (SPY0105).
    /// </summary>
    [Theory]
    [InlineData("def main():\n    def helper() -> int:\n        return 1\n    print(helper())\n", 1, 0, 2, 16)]
    [InlineData("def main():\n    def helper() -> int:\n        return 1\n    f = helper\n    print(f())\n", 1, 0, 2, 16)]
    [InlineData("def main():\n    def helper() -> int:\n        return 1\n    def other() -> int:\n        return helper()\n    print(other())\n", 1, 0, 2, 16)]
    [InlineData("def main():\n    if True:\n        def helper() -> int:\n            return 1\n        print(helper())\n", 2, 0, 3, 20)]
    [InlineData("def main():\n    def helper() -> int:\n        return 1\n    print(f\"{helper()}\")\n", 1, 0, 2, 16)]
    [InlineData("def main():\n    type T = int\n    v: T = 1\n    print(v)\n", 1, 0, 1, 16)]
    [InlineData("def main():\n    x: int = 1\n    def helper() -> int:\n        return 1\n    print(helper())\n", 1, 0, 3, 16)]
    [InlineData("def main():\n    @deprecated(\"x\")\n    def helper() -> int:\n        return 1\n    print(helper())\n", 1, 0, 3, 16)]
    [InlineData("def main():\n    @deprecated(\"x\")\n    def helper() -> int:\n        return 1\n    print(2)\n", 1, 0, 3, 16)]
    public void WrapAndExtract_NestedDeclarationReadAfterTheRegion_NotOffered(string text, int sl, int sc, int el, int ec)
    {
        NotOffered(text, sl, sc, el, ec, "Wrap in try/except");
        NotOffered(text, sl, sc, el, ec, "Extract method");
    }

    /// <summary>
    /// The positive controls of the cell above: the same nested def with no read after the region, its call
    /// moved WITH it, a same-named def in another function, and a member access <c>C().helper()</c> (a name after
    /// a <c>.</c> is a member, not a read) are all still offered, and each applied text compiles.
    /// </summary>
    [Fact]
    public void Wrap_NestedDeclarationNotReadAfterTheRegion_IsOffered()
    {
        Applied("def main():\n    def helper() -> int:\n        return 1\n    print(2)\n", 1, 0, 2, 16, "Wrap in try/except").Should().Be(
            "def main():\n    try:\n        def helper() -> int:\n            return 1\n    except Exception as e:\n        raise\n    print(2)\n");
        Applied("def main():\n    def helper() -> int:\n        return 1\n    print(helper())\n    print(3)\n", 1, 0, 3, 19, "Wrap in try/except").Should().Be(
            "def main():\n    try:\n        def helper() -> int:\n            return 1\n        print(helper())\n    except Exception as e:\n        raise\n    print(3)\n");
        Applied("def main():\n    def helper() -> int:\n        return 1\n    print(2)\n\ndef helper() -> int:\n    return 5\n\ndef g():\n    print(helper())\n",
                1, 0, 2, 16, "Wrap in try/except").Should().Be(
            "def main():\n    try:\n        def helper() -> int:\n            return 1\n    except Exception as e:\n        raise\n    print(2)\n\n"
            + "def helper() -> int:\n    return 5\n\ndef g():\n    print(helper())\n");
        Applied("class C:\n    def helper(self) -> int:\n        return 2\n\ndef main():\n    def helper() -> int:\n        return 1\n    print(C().helper())\n",
                5, 0, 6, 16, "Wrap in try/except").Should().Be(
            "class C:\n    def helper(self) -> int:\n        return 2\n\ndef main():\n    try:\n        def helper() -> int:\n            return 1\n"
            + "    except Exception as e:\n        raise\n    print(C().helper())\n");
        Applied("def main():\n    type T = int\n    v: int = 1\n    print(v)\n", 1, 0, 1, 16, "Wrap in try/except").Should().Be(
            "def main():\n    try:\n        type T = int\n    except Exception as e:\n        raise\n    v: int = 1\n    print(v)\n");
    }

    [Fact]
    public void ExtractMethod_NestedDeclarationNotReadAfterTheRegion_IsOffered()
    {
        Applied("def main():\n    def helper() -> int:\n        return 1\n    print(2)\n", 1, 0, 2, 16, "Extract method").Should().Be(
            "def extracted_method():\n    def helper() -> int:\n        return 1\n\ndef main():\n    extracted_method()\n    print(2)\n");
        Applied("class C:\n    def helper(self) -> int:\n        return 2\n\ndef main():\n    def helper() -> int:\n        return 1\n    print(C().helper())\n",
                5, 0, 6, 16, "Extract method").Should().Be(
            "class C:\n    def helper(self) -> int:\n        return 2\n\ndef extracted_method():\n    def helper() -> int:\n        return 1\n\n"
            + "def main():\n    extracted_method()\n    print(C().helper())\n");
    }

    [Fact]
    public void Wrap_SelectionEndingInsideAStatement_DoesNotCoverIt()
    {
        Applied(Describe, 1, 0, 2, 10, "Wrap in try/except").Should().Be(
            "def describe(n: int) -> str:\n    try:\n        result: str = \"a\" if n == 1 else \"b\"\n    except Exception as e:\n        raise\n"
            + "    return result\n");
    }

    [Fact]
    public void Wrap_LastStatementAtEndOfFileWithNoBreak_IsCovered()
    {
        Applied(Describe.TrimEnd('\n'), 1, 0, 2, 17, "Wrap in try/except").Should().Be(DescribeBodyWrapped.TrimEnd('\n'));
    }

    [Fact]
    public void Wrap_WholeDefWithAnIfBody_WrapsTheWholeBody()
    {
        Applied("def describe(n: int) -> str:\n    if n == 1:\n        return \"a\"\n    return \"r\"\n", 0, 0, 3, 14, "Wrap in try/except").Should().Be(
            "def describe(n: int) -> str:\n    try:\n        if n == 1:\n            return \"a\"\n        return \"r\"\n    except Exception as e:\n        raise\n");
    }

    /// <summary>
    /// Extract method on a selection that covers `return result` is not offered (a `return` cannot leave its
    /// function — the base wrote it twice); the statement before it alone is extracted.
    /// </summary>
    [Fact]
    public void ExtractMethod_SelectionCoveringTheReturn_NotOffered_TheStatementBeforeIt_Extracts()
    {
        var text = "def describe() -> str:\n    result: str = \"a\"\n    return result\n";

        NotOffered(text, 1, 0, 2, 17, "Extract method");
        Applied(text, 1, 0, 1, 21, "Extract method").Should().Be(
            "def extracted_method() -> str:\n    result: str = \"a\"\n    return result\n\ndef describe() -> str:\n    result = extracted_method()\n    return result\n");
    }

    /// <summary>A class body holds members, not statements: a try block cannot stand there.</summary>
    [Fact]
    public void R1_Wrap_ClassBodyMembers_NotOffered()
    {
        NotOffered("class A:\n    x: int = 1\n    y: int = 2\n\ndef main():\n    print(A().x)\n", 1, 0, 2, 14, "Wrap in try/except");
    }

    // ---- R2: extract variable over a multi-line string ----

    /// <summary>
    /// The replaced range ends at the string's TRUE end (#2295: the node records ColumnEnd on the string's
    /// start line). The base did not offer the action; 0283e7d58 offered it with an edit past its line.
    /// </summary>
    [Fact]
    public void R2_ExtractVariable_MultiLineString_ReplacesThroughTheStringsTrueEnd()
    {
        Applied("def main():\n    x = \"\"\"hello\n  world\"\"\"\n    print(x)\n", 1, 8, 2, 10, "Extract variable").Should().Be(
            "def main():\n    result: str = \"\"\"hello\n  world\"\"\"\n    x = result\n    print(x)\n");
    }

    // ---- R3: inline function whose body is a multi-line match expression ----

    /// <summary>
    /// Direction guard (the base does not offer it either): a match expression cannot be written inside the
    /// call's brackets (SPY0102), and its node text is not read past its line.
    /// </summary>
    [Fact]
    public void R3_InlineFunction_MultiLineMatchExpressionBody_NotOffered()
    {
        NotOffered("def pick(n: int) -> str:\n    return match n:\n        case 1: \"one\"\n        case _: \"other\"\n\n"
            + "def main():\n    print(pick(1))\n", 6, 10, 6, 10, "Inline function");
    }

    // ---- R4: extract method where no function body can take the statements ----

    [Fact]
    public void R4_ExtractMethod_ClassConstAtEndOfFileWithNoBreak_NotOffered()
    {
        NotOffered("def main() -> None:\n    print(Config.LIMIT)\n\nclass Config:\n    const LIMIT: int = 42", 4, 0, 4, 25, "Extract method");
    }

    [Fact]
    public void R4_ExtractMethod_AnInterfaceSelectedWhole_NotOffered()
    {
        NotOffered("interface Measurable:\n    def __len__(self) -> int: ...\n", 0, 0, 2, 0, "Extract method");
    }

    // ---- R5: inline variable must not read a token's length past its line ----

    /// <summary>
    /// Direction guard (the base does not offer it either): `.5` lexes with a value longer than its source
    /// text, so its node end lies past the line; the node text is refused rather than read into the break.
    /// The positive control is the same declaration spelled `0.5`.
    /// </summary>
    [Fact]
    public void R5_InlineVariable_LeadingDotFloat_NotOffered_ButZeroPointFive_Inlines()
    {
        NotOffered("def main():\n    x: float = .5\n    print(x)\n", 1, 4, 1, 4, "Inline variable");
        Applied("def main():\n    x: float = 0.5\n    print(x)\n", 1, 4, 1, 4, "Inline variable").Should().Be(
            "def main():\n    print(0.5)\n");
    }

    // ---- P2.T5: extract method's insertion point ----

    [Fact]
    public void ExtractMethod_InsideAMethod_InsertsBeforeTheClass_AtColumnZero()
    {
        Applied("class A:\n    def m(self) -> None:\n        a = \"x\"\n        b = \"y\"\n\ndef main():\n    A().m()\n", 2, 0, 3, 15, "Extract method")
            .Should().Be("def extracted_method():\n    a = \"x\"\n    b = \"y\"\n\nclass A:\n    def m(self) -> None:\n"
                + "        extracted_method()\n\ndef main():\n    A().m()\n");
    }

    [Fact]
    public void ExtractMethod_InsideADecoratedMethod_NeverLandsBetweenTheDecoratorAndItsDef()
    {
        Applied("class A:\n    @static\n    def m() -> None:\n        a = \"x\"\n        b = \"y\"\n\ndef main():\n    A.m()\n", 3, 0, 4, 15, "Extract method")
            .Should().Be("def extracted_method():\n    a = \"x\"\n    b = \"y\"\n\nclass A:\n    @static\n    def m() -> None:\n"
                + "        extracted_method()\n\ndef main():\n    A.m()\n");
    }

    // ---- P2.T5: the quick fixes edit the NAME's extent ----

    [Fact]
    public void PrefixUnderscore_LetBinding_PrefixesTheName()
    {
        Applied("def main():\n    let x = 1\n    print(2)\n", 1, 0, 1, 0, "Prefix with '_'", DiagnosticCodes.Validation.UnusedVariable)
            .Should().Be("def main():\n    let _x = 1\n    print(2)\n");
    }

    [Fact]
    public void PrefixUnderscore_ExceptBinding_PrefixesTheName()
    {
        Applied("def main():\n    try:\n        print(1)\n    except Exception as e:\n        print(2)\n", 3, 0, 3, 0, "Prefix with '_'",
                DiagnosticCodes.Validation.UnusedVariable)
            .Should().Be("def main():\n    try:\n        print(1)\n    except Exception as _e:\n        print(2)\n");
    }

    [Fact]
    public void RenameToSuggestion_DecoratedField_RenamesTheNameNotTheDecorator()
    {
        Applied("class MathHelper:\n    @static\n    PiValue: float = 3.14159\n\ndef main():\n    print(1)\n", 1, 0, 2, 0, "Rename to",
                DiagnosticCodes.Validation.NamingConventionWarning)
            .Should().Be("class MathHelper:\n    @static\n    pi_value: float = 3.14159\n\ndef main():\n    print(1)\n");
    }

    // ---- P2.T5: remove type annotation on a decorated field ----

    /// <summary>
    /// A decorated field needs its annotation (SPY0105 without it): not offered. The positive control is the
    /// same field undecorated, where the annotation is removable.
    /// </summary>
    [Fact]
    public void RemoveTypeAnnotation_DecoratedField_NotOffered_UndecoratedField_Offered()
    {
        NotOffered("class MathHelper:\n    @static\n    pi: float = 3.14159\n\ndef main():\n    print(MathHelper.pi)\n", 2, 4, 2, 4, "Remove type annotation");
        Applied("class MathHelper:\n    pi: float = 3.14159\n\ndef main():\n    print(MathHelper().pi)\n", 1, 4, 1, 4, "Remove type annotation")
            .Should().Be("class MathHelper:\n    pi = 3.14159\n\ndef main():\n    print(MathHelper().pi)\n");
    }

    // ---- P2.T5: implement interface writes Sharpy's property syntax ----

    [Fact]
    public void ImplementInterface_PropertyRequirement_WritesAPropertyGetStub()
    {
        Applied("interface INamed:\n    property get name: str\n\nclass A(INamed):\n    def other(self) -> int:\n        return 1\n\n"
                + "def main():\n    print(A().other())\n", 3, 6, 3, 6, "Implement")
            .Should().Be("interface INamed:\n    property get name: str\n\nclass A(INamed):\n    def other(self) -> int:\n        return 1\n\n"
                + "    property get name(self) -> str:\n        raise NotImplementedError()\n\ndef main():\n    print(A().other())\n");
    }

    // ---- convert to if/elif/else on a wildcard-only match ----

    /// <summary>A match of only `case _:` has no `if` to begin the chain (a bare `else:` does not parse): not offered.</summary>
    [Fact]
    public void ConvertToIf_WildcardOnlyMatch_NotOffered_ButWithALiteralCase_Offered()
    {
        NotOffered("def main():\n    value: int = 7\n    match value:\n        case _:\n            print(\"w\")\n", 2, 4, 2, 4, "Convert to if");
        Applied("def main():\n    value: int = 7\n    match value:\n        case 7:\n            print(\"s\")\n        case _:\n            print(\"w\")\n",
                2, 4, 2, 4, "Convert to if")
            .Should().Be("def main():\n    value: int = 7\n    if value == 7:\n        print(\"s\")\n    else:\n        print(\"w\")\n");
    }
}
