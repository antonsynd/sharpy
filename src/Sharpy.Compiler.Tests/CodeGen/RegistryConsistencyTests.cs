using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.CodeGen;

namespace Sharpy.Compiler.Tests.CodeGen;

public class RegistryConsistencyTests
{
    [Theory]
    [InlineData("__str__", "ToString")]
    [InlineData("__hash__", "GetHashCode")]
    [InlineData("__iter__", "GetEnumerator")]
    [InlineData("__contains__", "Contains")]
    [InlineData("__bool__", "ToBoolean")]
    public void NameMangler_TransformsProtocolDunderToExpectedName(string dunder, string expectedName)
    {
        var mangled = NameMangler.Transform(dunder, NameContext.Method);
        mangled.Should().Be(expectedName,
            $"Protocol dunder '{dunder}' should transform to '{expectedName}'");
    }

    [Fact]
    public void NameMangler_AllProtocolDundersWithClrMappingHaveTransformations()
    {
        foreach (var protocol in ProtocolRegistry.GetAllProtocols())
        {
            if (protocol.ClrMethodName != null && protocol.DunderName != "__init__")
            {
                // NameMangler should recognize this dunder
                var mangled = NameMangler.Transform(protocol.DunderName, NameContext.Method);

                // Should not just preserve the dunder name unchanged (except operators)
                if (!OperatorSignatureValidator.IsOperatorDunder(protocol.DunderName))
                {
                    // Verify it's actually transformed (not just returned as-is with capitalization)
                    mangled.Should().NotBeNull(
                        $"Protocol '{protocol.DunderName}' should be handled by NameMangler");
                    mangled.Should().NotStartWith("__",
                        $"Protocol '{protocol.DunderName}' should be transformed to a C# name, not preserved as dunder");
                }
            }
        }
    }

    [Fact]
    public void AllDundersAreRecognizedByRegistry()
    {
        // List of all dunders that appear in codegen
        var codegenDunders = new[]
        {
            "__init__", "__str__", "__repr__", "__hash__",
            "__len__", "__contains__", "__getitem__", "__setitem__", "__delitem__",
            "__iter__", "__bool__", "__eq__"
        };

        foreach (var dunder in codegenDunders)
        {
            var isProtocol = ProtocolRegistry.IsProtocolDunder(dunder);
            var isOperator = OperatorSignatureValidator.IsOperatorDunder(dunder);

            (isProtocol || isOperator).Should().BeTrue(
                $"Dunder '{dunder}' should be recognized by ProtocolRegistry or OperatorSignatureValidator");
        }
    }
}
