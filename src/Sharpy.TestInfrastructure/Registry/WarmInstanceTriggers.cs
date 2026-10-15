namespace Sharpy.TestInfrastructure.Registry;

/// <summary>
/// The earlier documents measured to change what ONE <see cref="Sharpy.Compiler.CompilerApi"/> answers for a later
/// document (P54 #2250 cells 2–3, measured @ 43aab2658 and still live @ 486c9a88a): two Wrap-in-try APPLIED texts the
/// P47 code-action sweep analysed on its shared instance — a builtin's name (a module-level <c>def len</c>, a method
/// <c>format</c>) defined inside a <c>try:</c> body. After either is analysed, a later document's builtin identifier
/// takes the user definition's signature (<c>len</c> <c>(object) -&gt; int32</c> becomes <c>(int32) -&gt; int32</c>;
/// <c>format</c> <c>(object | None, str) -&gt; str</c> becomes <c>(Formatter, int32) -&gt; str</c>, and the
/// IFormattable fixtures gain SPY0220), with no master dictionary key added. The P47 sweep stopped producing these
/// texts when Wrap in try stopped being offered over a declaration (e3c5cbdf6), which hid the cells from the sweep, not
/// the defect. Shared by <c>WarmInstanceParityTests</c> (Lsp.Tests) and <c>MasterRegistryImmutabilityTests</c>
/// (Compiler.Tests).
/// </summary>
public static class WarmInstanceTriggers
{
    /// <summary>(name, the corpus fixture whose Wrap-in-try applied text it is, the applied text).</summary>
    public static readonly (string Name, string Fixture, string Text)[] AppliedTexts =
    {
        ("wrap-try-shadowing-len", "builtins_qualified_escapes_shadow", string.Join("\n",
            "try:",
            "    import builtins",
            "",
            "",
            "    def len(x: int) -> int:",
            "        return x * 100",
            "",
            "",
            "    def main() -> None:",
            "        xs: list[int] = [1, 2, 3]",
            "        print(builtins.len(xs))",
            "        print(len(7))",
            "except Exception as e:",
            "    raise",
            "")),
        ("wrap-try-method-format", "classes/overload_by_type", string.Join("\n",
            "class Formatter:",
            "    try:",
            "        def __init__(self):",
            "            pass",
            "",
            "        def format(self, value: int) -> str:",
            "            return \"int:\" + str(value)",
            "    except Exception as e:",
            "        raise",
            "",
            "    def format(self, value: str) -> str:",
            "        return \"str:\" + value",
            "",
            "def main():",
            "    f: Formatter = Formatter()",
            "    print(f.format(42))",
            "    print(f.format(\"hello\"))",
            "")),
    };

    /// <summary>
    /// The TYPE-definition twins of <see cref="AppliedTexts"/> (P54 #2250 final verification, F1/F2 @ effba7776): a
    /// builtin type's name (<c>str</c>) defined by a class, struct, interface, union or delegate inside a block or a
    /// function body, where NameResolver declares no symbol. Before the type-definition seam the checker looked the name
    /// up and wrote whatever it found — the MASTER <c>str</c>: the mixed auto/setter property added a <c>_size</c>
    /// backing field to it (a later document's <c>s._size</c> lost its SPY0203 on the same instance, measured on one
    /// Sharpy.Lsp), and the dataclass text set <c>IsDataclass</c> on it (silently at 486c9a88a; a SPY0909
    /// PhaseViolationException ICE once the master's properties were frozen at cbabbf73a). Each is refused (SPY0202, and
    /// SPY0340 for a module-level block) and writes nothing it did not declare.
    /// </summary>
    public static readonly (string Name, string Text)[] TypeDefinitionTexts =
    {
        ("class-str-in-if-in-def", string.Join("\n",
            "def helper() -> None:",
            "    if True:",
            "        class str:",
            "            property size: int = 0",
            "",
            "            property set size(self, v: int) -> None:",
            "                self._size = v",
            "    print(\"helper\")",
            "",
            "def main() -> None:",
            "    helper()",
            "")),
        ("dataclass-str-in-module-try", string.Join("\n",
            "try:",
            "    @dataclass",
            "    class str:",
            "        x: int",
            "except Exception:",
            "    pass",
            "",
            "def main() -> None:",
            "    print(\"ok\")",
            "")),
        ("struct-str-in-def", string.Join("\n",
            "def helper() -> None:",
            "    struct str:",
            "        property size: int = 0",
            "",
            "        property set size(self, v: int) -> None:",
            "            self._size = v",
            "",
            "def main() -> None:",
            "    helper()",
            "")),
        ("interface-str-in-for-in-def", string.Join("\n",
            "def helper() -> None:",
            "    for i in range(1):",
            "        interface str:",
            "            def probe(self, value: int) -> int: ...",
            "",
            "def main() -> None:",
            "    helper()",
            "")),
        ("union-str-in-module-try", string.Join("\n",
            "try:",
            "    union str:",
            "        case Probe(value: int)",
            "except Exception:",
            "    pass",
            "",
            "def main() -> None:",
            "    print(\"ok\")",
            "")),
        ("delegate-str-in-def", string.Join("\n",
            "def helper() -> None:",
            "    delegate str(value: int) -> int",
            "",
            "def main() -> None:",
            "    helper()",
            "")),
    };

    /// <summary>
    /// Every trigger text — <see cref="AppliedTexts"/> then <see cref="TypeDefinitionTexts"/> — for the consumers that
    /// replay them by text (<c>WarmInstanceParityTests</c>; <c>MasterRegistryImmutabilityTests</c>).
    /// </summary>
    public static readonly (string Name, string Text)[] AllTexts =
        AppliedTexts.Select(t => (t.Name, t.Text)).Concat(TypeDefinitionTexts).ToArray();

    /// <summary>
    /// Later documents whose analysis reads what a <see cref="TypeDefinitionTexts"/> write would change: a member the
    /// master <c>str</c> does not have (<c>SPY0203</c> on a fresh instance — the verifier's <c>c5ctl.spy</c>), and a
    /// control that names no written member.
    /// </summary>
    public static readonly (string Name, string Text)[] LaterDocumentProbes =
    {
        ("str-has-no-_size", string.Join("\n",
            "def main() -> None:",
            "    s = \"abc\"",
            "    print(s._size)",
            "")),
        ("str-upper-control", string.Join("\n",
            "def main() -> None:",
            "    s = \"abc\"",
            "    print(s.upper())",
            "")),
    };
}
