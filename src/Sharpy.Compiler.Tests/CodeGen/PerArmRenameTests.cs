using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// plan-775c50 D6: the spill arm's copy of a call emitted once per arm renames each name the copy
/// declares, and only the declaration and its reads: a member of the same name (<c>box.v</c>,
/// <c>o?.v</c>), a named argument (<c>v:</c>), a property-pattern member and a type keep their names.
/// </summary>
public class PerArmRenameTests
{
    [Fact]
    public void RenameDeclaredNames_RenamesTheDeclarationAndItsReads_Only()
    {
        var copy = SyntaxFactory.ParseExpression(
            "F(o is var v ? v + 1 : 0, box.v, o?.v, G(v: v), p is { v: 1 }, (v)o, w is v)");
        var renamed = RoslynEmitter.RenameDeclaredNames(copy, declared => declared + "__arm7");
        Assert.Equal(
            "F(o is var v__arm7 ? v__arm7 + 1 : 0, box.v, o?.v, G(v: v__arm7), p is { v: 1 }, (v)o, w is v)",
            renamed.ToFullString());
    }

    [Fact]
    public void RenameDeclaredNames_WithNoDeclaration_IsTheCopy()
    {
        var copy = SyntaxFactory.ParseExpression("F(box.v, v)");
        Assert.Same(copy, RoslynEmitter.RenameDeclaredNames(copy, declared => declared + "__arm7"));
    }
}
