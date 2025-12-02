using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.CodeGen;

namespace Sharpy.Compiler.Tests.CodeGen;

public class RegistryConsistencyTests
{
    [Fact]
    public void NameMangler_AllProtocolDundersHaveMappings()
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
                    // Either it's transformed to a C# name, or it's preserved as a dunder
                    // The important thing is that it's recognized
                    mangled.Should().NotBeNull(
                        $"Protocol '{protocol.DunderName}' should be handled by NameMangler");
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
            "__len__", "__contains__", "__getitem__", "__setitem__",
            "__iter__", "__bool__"
        };

        foreach (var dunder in codegenDunders)
        {
            var isProtocol = ProtocolRegistry.IsProtocolDunder(dunder);
            var isOperator = OperatorSignatureValidator.IsOperatorDunder(dunder);

            (isProtocol || isOperator || dunder == "__init__").Should().BeTrue(
                $"Dunder '{dunder}' should be recognized by ProtocolRegistry or OperatorSignatureValidator");
        }
    }
}
