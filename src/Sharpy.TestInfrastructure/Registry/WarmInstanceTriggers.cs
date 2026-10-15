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
}
