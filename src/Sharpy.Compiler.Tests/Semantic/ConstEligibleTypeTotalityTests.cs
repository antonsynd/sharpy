using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for <see cref="ConstEligibility.IsConstEligibleSemanticType"/> (#1791, #1782).
///
/// <para><b>Contract.</b> The const-eligible type set is every <c>PrimitiveCatalog</c> primitive
/// except <c>object</c> and <c>void</c>, plus enum types. This test enumerates the catalog, asserts
/// the eligible/excluded sets by name, and verifies that enum types pass.</para>
///
/// <para><b>Exclusion set.</b> The two excluded primitives (<c>object</c>, <c>void</c>/<c>None</c>)
/// are named explicitly so adding a primitive to the catalog without classifying it here fails the
/// test -- the new name appears in neither the eligible nor the excluded set.</para>
///
/// <para><b>Walk totality.</b> The analysis is ONE descendant walk over every const host, so its
/// statement dispatch (<c>Collect</c>) is scanned against the whole <see cref="Statement"/> universe:
/// a new statement kind must be classified as host-opening, const-declaring or transparent, and the
/// scan is cross-checked against the production switch so the roster cannot drift from the code
/// (the DispatchSiteInventoryTests "guarded-by" claim for this class).</para>
///
/// <para><b>Cross-module operator roster.</b> The route that reads another module's AST has no
/// recorded lowerings, so it admits only operators whose C# form is constant for EVERY operand type.
/// Every <see cref="BinaryOperator"/> member is classified admitted or excluded, anchored to
/// literals.</para>
/// </summary>
public class ConstEligibleTypeTotalityTests
{
    // Anchored counts so the test is not "the enum against itself".
    // Distinct canonical primitives (not counting aliases):
    //   int8, int16, int32, int64, uint8, uint16, uint32, uint64,
    //   float32, float64, decimal, bool, char, str, object, void = 16
    // The catalog registers aliases (int, long, sbyte, short, byte, ushort, uint, ulong,
    // float, double, string, None) that map to the same CLR types -- those are NOT
    // double-counted here because the predicate is on SemanticType, not on catalog names.
    private const int EligibleCanonicalCount = 14; // 16 - object - void
    private const int ExcludedCanonicalCount = 2;  // object, void

    /// <summary>
    /// The excluded names: object and void/None. Every catalog entry whose CLR type is one of
    /// these must NOT be const-eligible; every other catalog entry MUST be.
    /// </summary>
    private static readonly HashSet<Type> ExcludedClrTypes = new()
    {
        typeof(object),
        typeof(void),
    };

    [Fact]
    public void EligibleSet_CoversEveryPrimitiveCatalogEntryExceptObjectAndVoid()
    {
        // Group by CLR type to deduplicate aliases.
        var grouped = PrimitiveCatalog.GetAllPrimitives()
            .GroupBy(p => p.Info.ClrType)
            .Select(g => (ClrType: g.Key, Rep: g.First()))
            .ToList();

        var eligible = grouped.Where(g => !ExcludedClrTypes.Contains(g.ClrType)).ToList();
        var excluded = grouped.Where(g => ExcludedClrTypes.Contains(g.ClrType)).ToList();

        eligible.Count.Should().Be(EligibleCanonicalCount,
            "the eligible count is anchored to a literal, not derived from the catalog");
        excluded.Count.Should().Be(ExcludedCanonicalCount,
            "the excluded count is anchored to a literal");
        (eligible.Count + excluded.Count).Should().Be(grouped.Count,
            "every catalog entry is in exactly one set");

        foreach (var (_, (name, info)) in eligible)
        {
            var builtinType = new BuiltinType { Name = name };
            ConstEligibility.IsConstEligibleSemanticType(builtinType).Should().BeTrue(
                $"'{name}' (CLR: {info.ClrType.Name}) is a const-eligible primitive");
        }

        foreach (var (_, (name, _)) in excluded)
        {
            var builtinType = new BuiltinType { Name = name };
            ConstEligibility.IsConstEligibleSemanticType(builtinType).Should().BeFalse(
                $"'{name}' is excluded from const eligibility");
        }
    }

    [Fact]
    public void EnumTypes_AreConstEligible()
    {
        var enumSymbol = new TypeSymbol { Name = "TestEnum", TypeKind = TypeKind.Enum };
        var enumType = new UserDefinedType { Name = "TestEnum", Symbol = enumSymbol };

        ConstEligibility.IsConstEligibleSemanticType(enumType).Should().BeTrue(
            "enum types are const-eligible (C# admits const for enums)");
    }

    [Fact]
    public void NonPrimitiveNonEnumTypes_AreNotConstEligible()
    {
        var classSymbol = new TypeSymbol { Name = "MyClass", TypeKind = TypeKind.Class };
        var classType = new UserDefinedType { Name = "MyClass", Symbol = classSymbol };

        ConstEligibility.IsConstEligibleSemanticType(classType).Should().BeFalse(
            "class types are not const-eligible");

        var structSymbol = new TypeSymbol { Name = "MyStruct", TypeKind = TypeKind.Struct };
        var structType = new UserDefinedType { Name = "MyStruct", Symbol = structSymbol };

        ConstEligibility.IsConstEligibleSemanticType(structType).Should().BeFalse(
            "struct types are not const-eligible");

        ConstEligibility.IsConstEligibleSemanticType(SemanticType.Unknown).Should().BeFalse(
            "UnknownType is not const-eligible");
    }

    // ══ The walk: every statement kind is classified ═════════════════════════════════════════

    private const string SourceFile = "src/Sharpy.Compiler/Semantic/ConstEligibility.cs";

    /// <summary>
    /// Statement kinds the walk names explicitly: the const declaration itself, the three type
    /// bodies that own field consts (a nested one included), and the function body that owns local
    /// consts. Every other kind is transparent — its child statements are collected in the SAME
    /// scope by the default arm, which is why no kind can be missed by omission.
    /// </summary>
    private static readonly HashSet<string> Collect_Handled = new()
    {
        nameof(VariableDeclaration),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(FunctionDef),
    };

    private static readonly HashSet<string> Collect_Transparent = new()
    {
        nameof(ExpressionStatement),
        nameof(DecoratedStatement),
        nameof(Assignment),
        nameof(AssertStatement),
        nameof(PassStatement),
        nameof(BreakStatement),
        nameof(BreakWithFlagStatement),
        nameof(ContinueStatement),
        nameof(ReturnStatement),
        nameof(YieldStatement),
        nameof(RaiseStatement),
        nameof(IfStatement),
        nameof(WhileStatement),
        nameof(ForStatement),
        nameof(TryStatement),
        nameof(WithStatement),
        nameof(DeferStatement),
        nameof(MatchStatement),
        nameof(EnumDef),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(EventDef),
        nameof(PropertyDef),
        nameof(TypeAlias),
        nameof(ImportStatement),
        nameof(FromImportStatement),
    };

    [Fact]
    public void Collect_AllStatementSubtypes_AreClassified()
    {
        var all = typeof(Statement).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Statement)) && !t.IsAbstract && t.IsPublic)
            .Select(t => t.Name)
            .ToList();

        var classified = new HashSet<string>(Collect_Handled);
        classified.UnionWith(Collect_Transparent);

        all.Where(n => !classified.Contains(n)).Should().BeEmpty(
            "a new statement kind must be classified host-opening, const-declaring or transparent");
        classified.Where(n => !all.Contains(n)).Should().BeEmpty(
            "a roster entry that names no statement kind is stale");
    }

    [Fact]
    public void Collect_SwitchArms_MatchClassification()
    {
        var arms = SwitchArmScan.CaseTypeNames(SourceFile, "Collect");

        arms.Should().BeEquivalentTo(Collect_Handled,
            "the walk's own switch names exactly the host-opening and const-declaring kinds; "
            + "everything else reaches the transparent default arm");
    }

    // ══ The cross-module operator roster ═════════════════════════════════════════════════════

    /// <summary>
    /// Binary operators whose C# form is a constant expression whatever the operand types are, so a
    /// route without recorded lowerings may admit them.
    /// </summary>
    private static readonly HashSet<BinaryOperator> OperandTypeIndependentOperators = new()
    {
        BinaryOperator.Add,
        BinaryOperator.Subtract,
        BinaryOperator.Divide,
        BinaryOperator.Equal,
        BinaryOperator.NotEqual,
        BinaryOperator.And,
        BinaryOperator.Or,
        BinaryOperator.BitwiseAnd,
        BinaryOperator.BitwiseOr,
        BinaryOperator.BitwiseXor,
        BinaryOperator.LeftShift,
        BinaryOperator.RightShift,
    };

    /// <summary>
    /// Operators the cross-module route must refuse, each with the lowering that makes it a call for
    /// at least one operand type it cannot see.
    /// </summary>
    private static readonly Dictionary<BinaryOperator, string> OperandTypeDependentOperators = new()
    {
        [BinaryOperator.Multiply] = "str * int lowers to Builtins.Repeat",
        [BinaryOperator.FloorDivide] = "// lowers to Builtins.FloorDiv",
        [BinaryOperator.Modulo] = "% lowers to Builtins.FloorMod",
        [BinaryOperator.Power] = "float ** lowers to Math.Pow",
        [BinaryOperator.MatMul] = "@ lowers to a call",
        [BinaryOperator.LessThan] = "str ordering lowers to string.CompareOrdinal",
        [BinaryOperator.LessThanOrEqual] = "str ordering lowers to string.CompareOrdinal",
        [BinaryOperator.GreaterThan] = "str ordering lowers to string.CompareOrdinal",
        [BinaryOperator.GreaterThanOrEqual] = "str ordering lowers to string.CompareOrdinal",
        [BinaryOperator.In] = "in lowers to a containment call",
        [BinaryOperator.NotIn] = "not in lowers to a containment call",
        [BinaryOperator.Is] = "Optional identity lowers to an OptionalNoneTest call",
        [BinaryOperator.IsNot] = "Optional identity lowers to an OptionalNoneTest call",
        [BinaryOperator.NullCoalesce] = "?? over Optional lowers to a call",
        [BinaryOperator.PipeForward] = "|> is a call by construction",
    };

    // Anchored to literals, not to the enum this test classifies.
    private const int BinaryOperatorCount = 27;
    private const int OperandTypeIndependentCount = 12;
    private const int OperandTypeDependentCount = 15;

    /// <summary>
    /// The production roster is a type switch over the three operator node kinds; scanning it keeps
    /// this class's guarded-by claim for
    /// <c>ConstEligibility.IsOperandTypeIndependentNativeOperator</c> honest.
    /// </summary>
    [Fact]
    public void CrossModuleOperatorRoster_DispatchesOnEveryOperatorNodeKind()
    {
        var arms = SwitchArmScan.CaseTypeNames(SourceFile, "IsOperandTypeIndependentNativeOperator");

        arms.Should().BeEquivalentTo(
            new[] { nameof(BinaryOp), nameof(UnaryOp), nameof(ConditionalExpression) },
            "the three node kinds the classifier hands the hook are exactly the ones it answers for");
    }

    [Fact]
    public void CrossModuleOperatorRoster_ClassifiesEveryBinaryOperator()
    {
        var all = Enum.GetValues<BinaryOperator>().ToList();
        all.Should().HaveCount(BinaryOperatorCount,
            "the operator axis is anchored to a literal, not to the enum under test");
        OperandTypeIndependentOperators.Should().HaveCount(OperandTypeIndependentCount);
        OperandTypeDependentOperators.Should().HaveCount(OperandTypeDependentCount);
        (OperandTypeIndependentCount + OperandTypeDependentCount).Should().Be(BinaryOperatorCount,
            "admitted + excluded must be the whole enum");

        foreach (var op in all)
        {
            var classified = OperandTypeIndependentOperators.Contains(op)
                || OperandTypeDependentOperators.ContainsKey(op);
            classified.Should().BeTrue($"'{op}' must be classified for the cross-module route");
        }
    }

    [Theory]
    [InlineData(BinaryOperator.Add, true)]
    [InlineData(BinaryOperator.Subtract, true)]
    [InlineData(BinaryOperator.Divide, true)]
    [InlineData(BinaryOperator.Equal, true)]
    [InlineData(BinaryOperator.NotEqual, true)]
    [InlineData(BinaryOperator.And, true)]
    [InlineData(BinaryOperator.Or, true)]
    [InlineData(BinaryOperator.BitwiseAnd, true)]
    [InlineData(BinaryOperator.BitwiseOr, true)]
    [InlineData(BinaryOperator.BitwiseXor, true)]
    [InlineData(BinaryOperator.LeftShift, true)]
    [InlineData(BinaryOperator.RightShift, true)]
    [InlineData(BinaryOperator.Multiply, false)]
    [InlineData(BinaryOperator.FloorDivide, false)]
    [InlineData(BinaryOperator.Modulo, false)]
    [InlineData(BinaryOperator.Power, false)]
    [InlineData(BinaryOperator.MatMul, false)]
    [InlineData(BinaryOperator.LessThan, false)]
    [InlineData(BinaryOperator.LessThanOrEqual, false)]
    [InlineData(BinaryOperator.GreaterThan, false)]
    [InlineData(BinaryOperator.GreaterThanOrEqual, false)]
    [InlineData(BinaryOperator.In, false)]
    [InlineData(BinaryOperator.NotIn, false)]
    [InlineData(BinaryOperator.Is, false)]
    [InlineData(BinaryOperator.IsNot, false)]
    [InlineData(BinaryOperator.NullCoalesce, false)]
    [InlineData(BinaryOperator.PipeForward, false)]
    public void CrossModuleOperatorRoster_MatchesTheProductionAnswer(BinaryOperator op, bool admitted)
    {
        var node = new BinaryOp
        {
            Operator = op,
            Left = new IntegerLiteral { Value = "1" },
            Right = new IntegerLiteral { Value = "2" },
        };

        ConstEligibility.IsExportedConstCompileTime(
                SemanticType.Double, node, foldedValue: null, _ => false)
            .Should().Be(admitted,
                $"the cross-module route admits '{op}' iff its C# form is constant for every operand type");
    }
}
