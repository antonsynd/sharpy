extern alias SharpyRT;

using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Unit tests for the #890 root-cause fix: two <see cref="UserDefinedType"/> values backed by the
/// same CLR type are mutually assignable even when their Sharpy names differ — e.g. the builtin
/// <c>bytes</c> (registered with ClrType = Sharpy.Bytes) versus the CLR-discovered <c>Bytes</c>
/// struct that is the underlying type of a <c>Bytes?</c> parameter. This is what lets
/// <c>hmac.new(bytes, bytes?, str)</c> resolve a plain <c>bytes</c> argument against the nullable
/// parameter.
/// </summary>
public class NullableStructAssignabilityTests
{
    private static UserDefinedType MakeUdt(string sharpyName, System.Type clrType)
        => new UserDefinedType
        {
            Name = sharpyName,
            Symbol = new TypeSymbol
            {
                Name = sharpyName,
                TypeKind = TypeKind.Struct,
                ClrType = clrType,
            },
        };

    [Fact]
    public void SameClrType_DifferentSharpyNames_AreMutuallyAssignable()
    {
        var bytesClr = typeof(SharpyRT::Sharpy.Bytes);

        // "bytes" is how the builtin registers it; "Bytes" is how discovery names the struct
        // that backs a `Bytes?` parameter. Both wrap the identical CLR type.
        var builtinBytes = MakeUdt("bytes", bytesClr);
        var discoveredBytes = MakeUdt("Bytes", bytesClr);

        Assert.True(builtinBytes.IsAssignableTo(discoveredBytes));
        Assert.True(discoveredBytes.IsAssignableTo(builtinBytes));
    }

    [Fact]
    public void DifferentClrTypes_AreNotAssignable()
    {
        // Sanity: the same-ClrType shortcut must not make unrelated structs assignable.
        var bytes = MakeUdt("bytes", typeof(SharpyRT::Sharpy.Bytes));
        var guid = MakeUdt("guid", typeof(System.Guid));

        Assert.False(bytes.IsAssignableTo(guid));
        Assert.False(guid.IsAssignableTo(bytes));
    }
}
