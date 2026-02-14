using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.CodeGen;

namespace Sharpy.Compiler.Tests.CodeGen;

public class RegistryConsistencyTests
{
    [Theory]
    [InlineData("__str__", "ToString")]
    [InlineData("__hash__", "GetHashCode")]
    [InlineData("__iter__", "GetEnumerator")]
    [InlineData("__contains__", "Contains")]
    // __bool__ no longer has a DunderMapping entry — handled as special codegen (operator true/false)
    public void DunderMapping_TransformsProtocolDunderToExpectedName(string dunder, string expectedName)
    {
        var resolved = DunderMapping.GetCSharpName(dunder);
        resolved.Should().Be(expectedName,
            $"Protocol dunder '{dunder}' should map to '{expectedName}'");
    }

    [Fact]
    public void DunderMapping_AllProtocolDundersWithClrMappingHaveMappings()
    {
        foreach (var protocol in ProtocolRegistry.GetAllProtocols()
            .Where(protocol => protocol.ClrMethodName != null && protocol.DunderName != "__init__"))
        {
            // DunderMapping should recognize this dunder
            var resolved = DunderMapping.ResolveCSharpName(protocol.DunderName);

            // Should not just preserve the dunder name unchanged (except operators)
            if (!OperatorRegistry.IsOperatorDunder(protocol.DunderName))
            {
                resolved.Should().NotBeNull(
                    $"Protocol '{protocol.DunderName}' should be handled by DunderMapping");
                resolved.Should().NotStartWith("__",
                    $"Protocol '{protocol.DunderName}' should be transformed to a C# name, not preserved as dunder");
            }
        }
    }

    [Fact]
    public void DunderMapping_RecognizesAllProtocolDunders()
    {
        foreach (var protocol in ProtocolRegistry.GetAllProtocols()
            .Where(p => p.ClrMethodName != null))
        {
            DunderMapping.IsDunderMethod(protocol.DunderName).Should().BeTrue(
                $"DunderMapping should recognize protocol dunder '{protocol.DunderName}'");
        }
    }

    [Fact]
    public void AllDundersAreRecognizedByRegistry()
    {
        // List of all dunders that appear in codegen
        var codegenDunders = new[]
        {
            "__init__", "__str__", "__hash__",
            "__len__", "__contains__", "__getitem__", "__setitem__",
            "__iter__", "__bool__", "__eq__"
        };

        foreach (var dunder in codegenDunders)
        {
            var isProtocol = ProtocolRegistry.IsProtocolDunder(dunder);
            var isOperator = OperatorRegistry.IsOperatorDunder(dunder);

            (isProtocol || isOperator).Should().BeTrue(
                $"Dunder '{dunder}' should be recognized by ProtocolRegistry or OperatorRegistry");
        }
    }
}
