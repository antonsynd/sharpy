using System.Reflection;
using FluentAssertions;
using Sharpy.Compiler.CodeGen;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Guards that <see cref="CodeGenContext.Ir"/> is non-nullable (#1646). Every construction site
/// must run <c>LoweringPass</c> before building the context; the <c>required</c> keyword and the
/// non-nullable annotation are the compile-time guard — this test is the runtime backstop.
/// <para><b>Mutation:</b> change the property type to <c>IrCompilation?</c> (remove <c>required</c>,
/// add <c>?</c>) → this test goes red; restore → green. Recorded in the commit body.</para>
/// </summary>
public class CodeGenContextIrNonNullableTests
{
    [Fact]
    public void Ir_PropertyHasNoNullableAnnotation()
    {
        var property = typeof(CodeGenContext).GetProperty(
            nameof(CodeGenContext.Ir),
            BindingFlags.Public | BindingFlags.Instance);

        property.Should().NotBeNull("CodeGenContext must have a public Ir property");

        var nullabilityContext = new NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(property!);

        nullabilityInfo.WriteState.Should().NotBe(
            NullabilityState.Nullable,
            "CodeGenContext.Ir must be non-nullable — every construction site runs LoweringPass first (#1646)");
        nullabilityInfo.ReadState.Should().NotBe(
            NullabilityState.Nullable,
            "CodeGenContext.Ir must be non-nullable — every construction site runs LoweringPass first (#1646)");
    }
}
