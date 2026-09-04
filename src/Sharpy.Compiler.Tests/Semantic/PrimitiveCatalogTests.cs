using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;

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
    [InlineData(typeof(int), "int32")]
    [InlineData(typeof(long), "int64")]
    [InlineData(typeof(float), "float32")]
    [InlineData(typeof(double), "float64")]
    [InlineData(typeof(bool), "bool")]
    [InlineData(typeof(sbyte), "int8")]
    [InlineData(typeof(byte), "uint8")]
    [InlineData(typeof(short), "int16")]
    [InlineData(typeof(ushort), "uint16")]
    [InlineData(typeof(uint), "uint32")]
    [InlineData(typeof(ulong), "uint64")]
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
        // "str" is canonical; "string" is an alias that lives in _bySharpyName only (#1356)
        var info = PrimitiveCatalog.GetByClrType(typeof(string));
        info.Should().NotBeNull();
        info!.ClrType.Should().Be(typeof(string));
        info.SharpyName.Should().Be("str");
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
    public void IsSharpyInteger_ReturnsTrueForLanguageLevelIntegers()
    {
        PrimitiveCatalog.IsSharpyInteger(SemanticType.Int).Should().BeTrue();
        PrimitiveCatalog.IsSharpyInteger(SemanticType.Long).Should().BeTrue();
    }

    [Fact]
    public void IsSharpyInteger_ReturnsFalseForNonIntegers()
    {
        PrimitiveCatalog.IsSharpyInteger(SemanticType.Float).Should().BeFalse();
        PrimitiveCatalog.IsSharpyInteger(SemanticType.Double).Should().BeFalse();
        PrimitiveCatalog.IsSharpyInteger(SemanticType.Str).Should().BeFalse();
        PrimitiveCatalog.IsSharpyInteger(SemanticType.Bool).Should().BeFalse();
    }

    [Fact]
    public void IsSharpyInteger_ReturnsFalseForClrOnlyIntegerTypes()
    {
        PrimitiveCatalog.IsSharpyInteger(SemanticType.SByte).Should().BeFalse();
        PrimitiveCatalog.IsSharpyInteger(SemanticType.Byte).Should().BeFalse();
        PrimitiveCatalog.IsSharpyInteger(SemanticType.Short).Should().BeFalse();
        PrimitiveCatalog.IsSharpyInteger(SemanticType.UShort).Should().BeFalse();
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
    [InlineData("int", "uint", "long")]        // C# §12.4.7: uint + signed → long
    [InlineData("uint", "int", "long")]        // commutative
    [InlineData("short", "ushort", "int")]     // C# §12.4.7: remaining mixed-sign → int
    [InlineData("ushort", "short", "int")]     // commutative
    [InlineData("sbyte", "byte", "int")]       // C# §12.4.7: remaining mixed-sign → int
    [InlineData("byte", "sbyte", "int")]       // commutative
    [InlineData("uint", "short", "long")]      // C# §12.4.7: uint + signed → long
    [InlineData("short", "uint", "long")]      // commutative
    [InlineData("uint", "sbyte", "long")]      // uint + sbyte → long
    [InlineData("sbyte", "uint", "long")]      // commutative
    [InlineData("long", "uint", "long")]       // long + unsigned → long
    [InlineData("uint", "long", "long")]       // commutative
    [InlineData("long", "byte", "long")]       // long + byte → long
    [InlineData("byte", "long", "long")]       // commutative
    [InlineData("long", "ushort", "long")]     // long + ushort → long
    [InlineData("ushort", "long", "long")]     // commutative
    [InlineData("uint", "ushort", "uint")]     // same-sign: priority
    [InlineData("ushort", "uint", "uint")]     // commutative
    [InlineData("ulong", "uint", "ulong")]    // same-sign: priority
    [InlineData("uint", "ulong", "ulong")]    // commutative
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

    // Decimal mixes with every integer kind, promoting to decimal — CPython's Decimal rule
    // (`Decimal(7) // 3` is `Decimal('2')`), and the spec's "decimal + any integer -> decimal"
    // row. Both operand orders (#1188).
    [Theory]
    [InlineData("int")]
    [InlineData("long")]
    [InlineData("sbyte")]
    [InlineData("short")]
    [InlineData("byte")]
    [InlineData("ushort")]
    [InlineData("uint")]
    [InlineData("ulong")]
    [InlineData("int8")]
    [InlineData("int16")]
    [InlineData("int32")]
    [InlineData("int64")]
    [InlineData("uint8")]
    [InlineData("uint16")]
    [InlineData("uint32")]
    [InlineData("uint64")]
    public void GetPromotedType_DecimalWithIntegerKind_PromotesToDecimal(string integerName)
    {
        var decimalInfo = PrimitiveCatalog.GetByName("decimal")!;
        var integerInfo = PrimitiveCatalog.GetByName(integerName)!;

        PrimitiveCatalog.GetPromotedType(decimalInfo, integerInfo)!.SharpyName.Should().Be("decimal");
        PrimitiveCatalog.GetPromotedType(integerInfo, decimalInfo)!.SharpyName.Should().Be("decimal");
    }

    // The one restriction that survives: decimal never mixes with a float kind, in either
    // order — CPython raises TypeError for `Decimal(7) + 1.5`, Sharpy gives SPY0222 (#1188).
    [Theory]
    [InlineData("float")]
    [InlineData("float32")]
    [InlineData("float64")]
    [InlineData("double")]
    public void GetPromotedType_DecimalWithFloatKind_ReturnsNull(string floatName)
    {
        var decimalInfo = PrimitiveCatalog.GetByName("decimal")!;
        var floatInfo = PrimitiveCatalog.GetByName(floatName)!;

        PrimitiveCatalog.GetPromotedType(decimalInfo, floatInfo).Should().BeNull();
        PrimitiveCatalog.GetPromotedType(floatInfo, decimalInfo).Should().BeNull();
    }

    [Fact]
    public void GetPromotedType_DecimalWithDecimal_IsDecimal()
    {
        var decimalInfo = PrimitiveCatalog.GetByName("decimal")!;
        PrimitiveCatalog.GetPromotedType(decimalInfo, decimalInfo)!.SharpyName.Should().Be("decimal");
    }

    [Fact]
    public void GetPromotedType_SemanticType_DecimalAndInteger_IsDecimal()
    {
        PrimitiveCatalog.GetPromotedType(SemanticType.Decimal, SemanticType.Int)
            .Should().Be(SemanticType.Decimal);
        PrimitiveCatalog.GetPromotedType(SemanticType.Long, SemanticType.Decimal)
            .Should().Be(SemanticType.Decimal);
        PrimitiveCatalog.GetPromotedType(SemanticType.Decimal, SemanticType.Double)
            .Should().BeNull();
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

    [Theory]
    [InlineData("ulong", "sbyte")]
    [InlineData("sbyte", "ulong")]
    [InlineData("ulong", "short")]
    [InlineData("short", "ulong")]
    [InlineData("ulong", "int")]
    [InlineData("int", "ulong")]
    [InlineData("ulong", "long")]
    [InlineData("long", "ulong")]
    public void GetPromotedType_UlongWithSigned_ReturnsNull(string left, string right)
    {
        var leftInfo = PrimitiveCatalog.GetByName(left)!;
        var rightInfo = PrimitiveCatalog.GetByName(right)!;

        PrimitiveCatalog.GetPromotedType(leftInfo, rightInfo).Should().BeNull();
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

    // Note: Sharpy `float`/`float64`/`double` are all C# `double` (typeof(double)); the 32-bit
    // IEEE float is `float32` (typeof(float)). The widening lattice levels are: {8-bit int}=0,
    // {16-bit}=1, {32-bit int}=2, {64-bit int}=3, float32=4, double=5, decimal=6.
    [Theory]
    // Exact match — cost 0.
    [InlineData("int", "int", 0)]
    [InlineData("int32", "int", 0)]           // alias of the same CLR type
    [InlineData("float", "double", 0)]        // Sharpy float IS C# double — same CLR type
    // Numeric widening — 2 for one lattice step, +1 per additional step (spec cost ranking).
    [InlineData("int", "long", 2)]            // 32→64-bit int: one step
    [InlineData("byte", "short", 2)]          // 8→16-bit: one step
    [InlineData("ushort", "int", 2)]          // unsigned 16 → signed 32: one step
    [InlineData("float32", "double", 2)]      // float32(4) → double(5): one step
    [InlineData("byte", "int", 3)]            // 8→32-bit int: two steps
    [InlineData("int", "float32", 3)]         // int(2) → float32(4): two steps
    [InlineData("long", "double", 3)]         // long(3) → double(5): two steps
    [InlineData("int", "double", 4)]          // int(2) → double(5): three steps
    [InlineData("int", "decimal", 5)]         // int(2) → decimal(6)
    // No implicit conversion — sentinel.
    [InlineData("long", "int", PrimitiveCatalog.NoImplicitConversion)]      // narrowing
    [InlineData("float32", "int", PrimitiveCatalog.NoImplicitConversion)]   // float → int
    [InlineData("int", "uint", PrimitiveCatalog.NoImplicitConversion)]      // signed → unsigned
    [InlineData("double", "float32", PrimitiveCatalog.NoImplicitConversion)]// double → float narrowing
    [InlineData("float32", "decimal", PrimitiveCatalog.NoImplicitConversion)]
    [InlineData("decimal", "double", PrimitiveCatalog.NoImplicitConversion)]
    [InlineData("bool", "int", PrimitiveCatalog.NoImplicitConversion)]      // Axiom 1: not Python's bool≤int
    [InlineData("str", "int", PrimitiveCatalog.NoImplicitConversion)]
    public void ImplicitConversionCost_ReturnsExpectedRank(string from, string to, int expected)
    {
        var fromInfo = PrimitiveCatalog.GetByName(from)!;
        var toInfo = PrimitiveCatalog.GetByName(to)!;

        PrimitiveCatalog.ImplicitConversionCost(fromInfo, toInfo).Should().Be(expected);
    }

    [Fact]
    public void ImplicitConversionCost_PrefersCloserWideningTarget()
    {
        // C#'s "better conversion target": int→long beats int→float32 beats int→double, because
        // long→float32→double each implicitly convert onward.
        var i = PrimitiveCatalog.GetByName("int")!;
        var l = PrimitiveCatalog.GetByName("long")!;
        var f32 = PrimitiveCatalog.GetByName("float32")!;
        var d = PrimitiveCatalog.GetByName("double")!;

        PrimitiveCatalog.ImplicitConversionCost(i, l)
            .Should().BeLessThan(PrimitiveCatalog.ImplicitConversionCost(i, f32));
        PrimitiveCatalog.ImplicitConversionCost(i, f32)
            .Should().BeLessThan(PrimitiveCatalog.ImplicitConversionCost(i, d));
    }

    [Fact]
    public void ImplicitConversionCost_IsTotalAndConsistentWithCanImplicitlyConvert()
    {
        // Guard the ranking's invariants across every primitive pair: exact is the unique best (0),
        // every other implicit conversion is a strictly positive widening cost, non-conversions use
        // the sentinel, and the boolean view agrees exactly with rank != NoImplicitConversion.
        var infos = PrimitiveCatalog.GetAllPrimitives().Select(p => p.Info).ToList();

        foreach (var from in infos)
        {
            foreach (var to in infos)
            {
                var cost = PrimitiveCatalog.ImplicitConversionCost(from, to);

                // void participates in no conversion, not even void→void (matches prior behavior).
                if (from.ClrType == typeof(void) || to.ClrType == typeof(void))
                {
                    cost.Should().Be(PrimitiveCatalog.NoImplicitConversion, "void has no conversions");
                }
                else if (from.ClrType == to.ClrType)
                {
                    cost.Should().Be(0, "an exact match is the best conversion");
                }
                else if (cost != PrimitiveCatalog.NoImplicitConversion)
                {
                    cost.Should().BeGreaterThan(0,
                        $"a widening conversion {from.SharpyName}->{to.SharpyName} must cost more than an exact match");
                }
                else
                {
                    cost.Should().Be(PrimitiveCatalog.NoImplicitConversion);
                }

                // Boolean view must agree with the rank exactly (zero-behavior-change contract).
                PrimitiveCatalog.CanImplicitlyConvert(from, to)
                    .Should().Be(cost != PrimitiveCatalog.NoImplicitConversion,
                        $"CanImplicitlyConvert must mirror the rank for {from.SharpyName}->{to.SharpyName}");
            }
        }
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
        info!.SharpyName.Should().Be("int32");
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
