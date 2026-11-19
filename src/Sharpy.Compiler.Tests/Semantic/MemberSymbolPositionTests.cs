using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Infrastructure;
using Sharpy.TestInfrastructure;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// plan-775c50 (P77): a type member a use binds is the member the type holds, wherever the using
/// function is declared. The checker reads a function body in declaration order, so a function
/// declared ABOVE its type is checked before the type's body resolves its field types. That
/// resolution used to replace the field with a copy (<c>fieldSymbol with { Type = … }</c>), leaving
/// the use above bound to a symbol the type no longer held: <c>CodeGenInfoComputer</c> recorded no
/// <c>StaticStorage</c> on it, and the emitter wrote a plain store <c>H.T = 42</c> where the symbol
/// below the type gave <c>H.T__set(42)</c> (D4): <c>1 42</c> instead of <c>1 7</c>.
/// <para><b>Cells.</b> host {class, struct, generic class through a closed type} × use {plain store,
/// read, <c>out</c> operand} × function {above the type, below it}: every recorded member resolution
/// is the field the type holds, by reference. The generated sweep that runs these programs is
/// <c>InitializerDependencyDifferentialTests</c>' <c>FunctionPosition</c> axis.</para>
/// </summary>
public class MemberSymbolPositionTests
{
    public static IEnumerable<object[]> Cells() =>
        from host in new[] { "class", "struct", "generic" }
        from above in new[] { true, false }
        select new object[] { host, above };

    [Theory]
    [MemberData(nameof(Cells))]
    public void AUseAboveOrBelowItsType_BindsTheFieldTheTypeHolds(string host, bool above)
    {
        var h = host == "generic" ? "H[int]" : "H";
        var type = host switch
        {
            "class" => "class H:\n",
            "struct" => "struct H:\n",
            _ => "class H[E]:\n",
        } + "    @static\n    t: int = 7\n\n";
        var function =
            "def fill(a: out int) -> int:\n    a = 42\n    return 1\n\n"
            + "def use() -> int:\n"
            + $"    {h}.t = 5\n"
            + $"    r = {h}.t + fill(out {h}.t)\n"
            + "    return r\n\n";
        var source = (above ? function + type : type + function) + $"def main() -> None:\n    print(use(), {h}.t)\n";

        var result = new CompilerApi(null, new[] { SharpyCoreReference.Location }).Analyze(source);
        result.Diagnostics.Where(d => d.IsError).Select(d => $"{d.Code}: {d.Message}")
            .Should().BeEmpty("the probe program type-checks");
        var held = ((TypeSymbol)result.SymbolTable!.Lookup("H")!).Fields.Single(f => f.Name == "t");

        var uses = AstDescendants.SelfAndDescendants(result.Ast!).OfType<MemberAccess>()
            .Where(ma => ma.Member == "t").ToList();
        uses.Should().HaveCount(4, "a store, a read and an operand in use(), and main's read");
        foreach (var use in uses)
        {
            var bound = result.SemanticInfo!.GetMemberAccessResolution(use)?.Member;
            bound.Should().BeSameAs(held,
                $"the use at {use.LineStart}:{use.ColumnStart} binds the field the type holds (function {(above ? "above" : "below")} the {host})");
        }
    }
}
