using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;

namespace Sharpy.Compiler.Tests.Parser.Ast;

public class AstVisitorTotalityTests
{
    private static readonly string AstVisitorPath =
        "src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs";

    private static HashSet<string> GetConcreteNodeTypeNames()
    {
        var nodeType = typeof(Node);
        return nodeType.Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(nodeType) && !t.IsAbstract)
            .Select(t => t.Name)
            .ToHashSet();
    }

    [Fact]
    public void VoidVisitor_DispatchArms_EqualConcreteNodeUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(AstVisitorPath, "Visit", "AstVisitor");
        var universe = GetConcreteNodeTypeNames();

        var missingArms = universe.Except(arms).ToList();
        var phantomArms = arms.Except(universe).ToList();

        Assert.Empty(missingArms);
        Assert.Empty(phantomArms);
    }

    [Fact]
    public void GenericVisitor_DispatchArms_EqualConcreteNodeUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(AstVisitorPath, "Visit", "AstVisitor`1");
        var universe = GetConcreteNodeTypeNames();

        var missingArms = universe.Except(arms).ToList();
        var phantomArms = arms.Except(universe).ToList();

        Assert.Empty(missingArms);
        Assert.Empty(phantomArms);
    }

    [Fact]
    public void BothOverloads_HaveIdenticalArms()
    {
        var voidArms = SwitchArmScan.CaseTypeNames(AstVisitorPath, "Visit", "AstVisitor");
        var genericArms = SwitchArmScan.CaseTypeNames(AstVisitorPath, "Visit", "AstVisitor`1");

        Assert.True(voidArms.SetEquals(genericArms),
            $"Void-only: [{string.Join(", ", voidArms.Except(genericArms))}]; " +
            $"Generic-only: [{string.Join(", ", genericArms.Except(voidArms))}]");
    }
}
