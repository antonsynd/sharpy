using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for the PrimitiveCatalog class which provides exhaustive primitive type information,
/// numeric promotion rules, and conversion checking.
/// </summary>
public class PrimitiveCatalogTests
{
    // ==================== 1.6.2 Test all primitives are registered ====================

    [Theory]
    [InlineData("int", typeof(int))]
    [InlineData("long", typeof(long))]
    [InlineData("float", typeof(double))]      // Per spec: Sharpy 'float' -> C# 'double'
    [InlineData("float32", typeof(float))]     // Per spec: Sharpy 'float32' -> C# 'float'
    [InlineData("float64", typeof(double))]    // Per spec: Sharpy 'float64' -> C# 'double'
    [InlineData("double", typeof(double))]
    [InlineData("bool", typeof(bool))]
    [InlineData("str", typeof(string))]
    [InlineData("string", typeof(string))]
    [InlineData("sbyte", typeof(sbyte))]
    [InlineData("byte", typeof(byte))]
    [InlineData("short", typeof(short))]
    [InlineData("ushort", typeof(ushort))]
    [InlineData("uint", typeof(uint))]
    [InlineData("ulong", typeof(ulong))]
    [InlineData("char", typeof(char))]
    [InlineData("decimal", typeof(decimal))]
    public void GetByName_ReturnsCorrectClrType(string name, Type expectedClrType)
    {
        var info = PrimitiveCatalog.GetByName(name);
        info.Should().NotBeNull();
        info!.ClrType.Should().Be(expectedClrType);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("void")]
    public void GetByName_ReturnsVoidClrTypeForVoid(string name)
    {
        var info = PrimitiveCatalog.GetByName(name);
        info.Should().NotBeNull();
        info!.ClrType.Should().Be(typeof(void));
        info.CSharpName.Should().Be("void");
    }

    [Fact]
    public void GetByName_ReturnsNullForUnknownType()
    {
        var info = PrimitiveCatalog.GetByName("unknown_type");
        info.Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(long), "long")]
    [InlineData(typeof(float), "float32")]      // C# float -> Sharpy 'float32'
    [InlineData(typeof(double), "double")]      // C# double -> Sharpy 'double' (last registered canonical name)
    [InlineData(typeof(bool), "bool")]
    [InlineData(typeof(sbyte), "sbyte")]
    [InlineData(typeof(byte), "byte")]
    [InlineData(typeof(short), "short")]
    [InlineData(typeof(ushort), "ushort")]
    [InlineData(typeof(uint), "uint")]
    [InlineData(typeof(ulong), "ulong")]
    [InlineData(typeof(char), "char")]
    [InlineData(typeof(decimal), "decimal")]
    public void GetByClrType_ReturnsCorrectSharpyName(Type clrType, string expectedName)
    {
        var info = PrimitiveCatalog.GetByClrType(clrType);
        info.Should().NotBeNull();
        info!.SharpyName.Should().Be(expectedName);
    }

    [Fact]
    public void GetByClrType_ReturnsInfoForString()
    {
        // string has two aliases: "str" and "string"
        // The first registered ("str") wins in the CLR type lookup
        var info = PrimitiveCatalog.GetByClrType(typeof(string));
        info.Should().NotBeNull();
        info!.ClrType.Should().Be(typeof(string));
        // Both "str" and "string" are valid Sharpy names for string
        info.SharpyName.Should().BeOneOf("str", "string");
    }

    // ==================== 1.6.3 Test numeric classification ====================

    [Fact]
    public void IsNumeric_ReturnsTrueForNumericTypes()
    {
        PrimitiveCatalog.IsNumeric(SemanticType.Int).Should().BeTrue();
        PrimitiveCatalog.IsNumeric(SemanticType.Long).Should().BeTrue();
        PrimitiveCatalog.IsNumeric(SemanticType.Float).Should().BeTrue();
        PrimitiveCatalog.IsNumeric(SemanticType.Double).Should().BeTrue();
    }

    [Fact]
    public void IsNumeric_ReturnsFalseForNonNumericTypes()
    {
        PrimitiveCatalog.IsNumeric(SemanticType.Bool).Should().BeFalse();
        PrimitiveCatalog.IsNumeric(SemanticType.Str).Should().BeFalse();
        PrimitiveCatalog.IsNumeric(SemanticType.Void).Should().BeFalse();
    }

    [Fact]
    public void IsInteger_CorrectlyClassifiesTypes()
    {
        PrimitiveCatalog.IsInteger(SemanticType.Int).Should().BeTrue();
        PrimitiveCatalog.IsInteger(SemanticType.Long).Should().BeTrue();
        PrimitiveCatalog.IsInteger(SemanticType.Float).Should().BeFalse();
        PrimitiveCatalog.IsInteger(SemanticType.Double).Should().BeFalse();
    }

    [Fact]
    public void IsFloatingPoint_CorrectlyClassifiesTypes()
    {
        PrimitiveCatalog.IsFloatingPoint(SemanticType.Float).Should().BeTrue();
        PrimitiveCatalog.IsFloatingPoint(SemanticType.Double).Should().BeTrue();
        PrimitiveCatalog.IsFloatingPoint(SemanticType.Int).Should().BeFalse();
        PrimitiveCatalog.IsFloatingPoint(SemanticType.Long).Should().BeFalse();
    }

    [Fact]
    public void IsDecimal_CorrectlyClassifiesTypes()
    {
        var decimalType = new BuiltinType { Name = "decimal", ClrType = typeof(decimal) };
        PrimitiveCatalog.IsDecimal(decimalType).Should().BeTrue();
        PrimitiveCatalog.IsDecimal(SemanticType.Float).Should().BeFalse();
        PrimitiveCatalog.IsDecimal(SemanticType.Double).Should().BeFalse();
        PrimitiveCatalog.IsDecimal(SemanticType.Int).Should().BeFalse();
    }

    [Fact]
    public void IsNumeric_ReturnsFalseForNonBuiltinTypes()
    {
        var userType = new UserDefinedType { Name = "MyClass" };
        PrimitiveCatalog.IsNumeric(userType).Should().BeFalse();
    }

    // ==================== 1.6.4 Test promotion rules ====================

    [Theory]
    [InlineData("int", "int", "int")]
    [InlineData("int", "long", "long")]
    [InlineData("int", "float", "float")]      // int + float(double) -> float(double)
    [InlineData("float", "double", "float")]   // float(double) + double -> float(double), both are C# double
    [InlineData("long", "double", "double")]   // long + double -> double
    [InlineData("byte", "int", "int")]
    [InlineData("int", "uint", "long")]        // Mixed signed/unsigned promotes to larger signed
    [InlineData("short", "ushort", "int")]     // 16-bit mixed promotes to 32-bit signed
    [InlineData("sbyte", "byte", "short")]     // 8-bit mixed promotes to 16-bit signed
    [InlineData("int", "float32", "float32")]  // int + float32 -> float32
    [InlineData("float32", "float", "float")]  // float32 + float(double) -> float(double)
    public void GetPromotedType_ReturnsCorrectType(string left, string right, string expected)
    {
        var leftInfo = PrimitiveCatalog.GetByName(left)!;
        var rightInfo = PrimitiveCatalog.GetByName(right)!;
        var expectedInfo = PrimitiveCatalog.GetByName(expected)!;

        var result = PrimitiveCatalog.GetPromotedType(leftInfo, rightInfo);
        result.Should().NotBeNull();
        result!.SharpyName.Should().Be(expectedInfo.SharpyName);
    }

    [Fact]
    public void GetPromotedType_ReturnsNullForIncompatibleTypes()
    {
        var decimalInfo = PrimitiveCatalog.GetByName("decimal")!;
        var floatInfo = PrimitiveCatalog.GetByName("float")!;

        PrimitiveCatalog.GetPromotedType(decimalInfo, floatInfo).Should().BeNull();
    }

    [Fact]
    public void GetPromotedType_ReturnsNullForNonNumericTypes()
    {
        var boolInfo = PrimitiveCatalog.GetByName("bool")!;
        var intInfo = PrimitiveCatalog.GetByName("int")!;

        PrimitiveCatalog.GetPromotedType(boolInfo, intInfo).Should().BeNull();
    }

    [Fact]
    public void GetPromotedType_ReturnsNullForLongUlongMixedTypes()
    {
        // Per C# spec, long + ulong has no implicit common type and should be an error
        var longInfo = PrimitiveCatalog.GetByName("long")!;
        var ulongInfo = PrimitiveCatalog.GetByName("ulong")!;

        PrimitiveCatalog.GetPromotedType(longInfo, ulongInfo).Should().BeNull();
    }

    [Fact]
    public void GetPromotedType_SemanticType_ReturnsCorrectType()
    {
        var result = PrimitiveCatalog.GetPromotedType(SemanticType.Int, SemanticType.Double);
        result.Should().Be(SemanticType.Double);
    }

    [Fact]
    public void GetPromotedType_SemanticType_ReturnsNullForNonNumeric()
    {
        var result = PrimitiveCatalog.GetPromotedType(SemanticType.Bool, SemanticType.Int);
        result.Should().BeNull();
    }

    // ==================== 1.6.5 Test implicit conversion ====================

    [Theory]
    [InlineData("int", "long", true)]
    [InlineData("int", "float", true)]
    [InlineData("float", "double", true)]
    [InlineData("long", "int", false)]       // Narrowing
    [InlineData("float", "int", false)]      // Float to int
    [InlineData("int", "uint", false)]       // Signed to unsigned
    [InlineData("byte", "short", true)]      // Unsigned widening
    [InlineData("byte", "int", true)]        // Unsigned to larger signed
    [InlineData("int", "decimal", true)]     // Integer to decimal
    [InlineData("float", "decimal", false)]  // Float to decimal not allowed
    [InlineData("decimal", "double", false)] // Decimal to double not allowed
    public void CanImplicitlyConvert_ReturnsExpectedResult(string from, string to, bool expected)
    {
        var fromInfo = PrimitiveCatalog.GetByName(from)!;
        var toInfo = PrimitiveCatalog.GetByName(to)!;

        PrimitiveCatalog.CanImplicitlyConvert(fromInfo, toInfo).Should().Be(expected);
    }

    [Theory]
    [InlineData("float", "int", true)]
    [InlineData("double", "float", true)]
    [InlineData("long", "int", true)]
    [InlineData("int", "short", true)]
    [InlineData("char", "int", true)]        // char to integer
    [InlineData("int", "char", true)]        // integer to char
    public void CanExplicitlyConvert_ReturnsExpectedResult(string from, string to, bool expected)
    {
        var fromInfo = PrimitiveCatalog.GetByName(from)!;
        var toInfo = PrimitiveCatalog.GetByName(to)!;

        PrimitiveCatalog.CanExplicitlyConvert(fromInfo, toInfo).Should().Be(expected);
    }

    // ==================== Additional Tests ====================

    [Fact]
    public void GetAllPrimitives_ContainsExpectedCount()
    {
        var primitives = PrimitiveCatalog.GetAllPrimitives().ToList();
        // 17 primitives registered by name (including aliases)
        primitives.Count.Should().BeGreaterThanOrEqualTo(17);
    }

    [Fact]
    public void IsPrimitive_ReturnsCorrectResult()
    {
        PrimitiveCatalog.IsPrimitive("int").Should().BeTrue();
        PrimitiveCatalog.IsPrimitive("str").Should().BeTrue();
        PrimitiveCatalog.IsPrimitive("bool").Should().BeTrue();
        PrimitiveCatalog.IsPrimitive("MyClass").Should().BeFalse();
    }

    [Fact]
    public void GetPrimitiveInfo_ReturnsNullForNonPrimitive()
    {
        var userType = new UserDefinedType { Name = "MyClass" };
        PrimitiveCatalog.GetPrimitiveInfo(userType).Should().BeNull();
    }

    [Fact]
    public void GetPrimitiveInfo_ReturnsInfoForBuiltinType()
    {
        var info = PrimitiveCatalog.GetPrimitiveInfo(SemanticType.Int);
        info.Should().NotBeNull();
        info!.SharpyName.Should().Be("int");
        info.ClrType.Should().Be(typeof(int));
    }

    [Theory]
    [InlineData("sbyte", PrimitiveCatalog.NumericKind.SignedInteger)]
    [InlineData("short", PrimitiveCatalog.NumericKind.SignedInteger)]
    [InlineData("int", PrimitiveCatalog.NumericKind.SignedInteger)]
    [InlineData("long", PrimitiveCatalog.NumericKind.SignedInteger)]
    [InlineData("byte", PrimitiveCatalog.NumericKind.UnsignedInteger)]
    [InlineData("ushort", PrimitiveCatalog.NumericKind.UnsignedInteger)]
    [InlineData("uint", PrimitiveCatalog.NumericKind.UnsignedInteger)]
    [InlineData("ulong", PrimitiveCatalog.NumericKind.UnsignedInteger)]
    [InlineData("float", PrimitiveCatalog.NumericKind.FloatingPoint)]
    [InlineData("double", PrimitiveCatalog.NumericKind.FloatingPoint)]
    [InlineData("decimal", PrimitiveCatalog.NumericKind.Decimal)]
    [InlineData("bool", PrimitiveCatalog.NumericKind.None)]
    [InlineData("char", PrimitiveCatalog.NumericKind.None)]
    [InlineData("str", PrimitiveCatalog.NumericKind.None)]
    public void GetByName_ReturnsCorrectNumericKind(string name, PrimitiveCatalog.NumericKind expectedKind)
    {
        var info = PrimitiveCatalog.GetByName(name);
        info.Should().NotBeNull();
        info!.Kind.Should().Be(expectedKind);
    }
}
