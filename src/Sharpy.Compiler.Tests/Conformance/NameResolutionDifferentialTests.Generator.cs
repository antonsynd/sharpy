using System.Globalization;
using Sharpy.TestInfrastructure.Oracles;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The generator (P77a D3): cells are the product of the rosters, computed here and never written by hand. One
/// program per cell, its Sharpy tag from the rule table in <see cref="NameResolutionDifferentialTests"/>'s Expect
/// functions and its python prediction from a small model of python's scoping, both keyed on axes and never on a cell.
/// </summary>
public partial class NameResolutionDifferentialTests
{
    internal enum Order
    {
        Earlier,
        Later,
        Self,
        Cycle,
    }

    internal enum Spelling
    {
        Bare,
        Qualified,
        Alias,
        Call,
        SelfDot,
    }


    /// <summary>The Block / FunctionLike nestings a position may ride (D3.c).</summary>
    internal enum Nesting
    {
        Direct,
        ComprehensionIterable,
        ComprehensionElement,
        Lambda,
    }

    /// <summary>How a binding is read so the read is a name verdict and never a type mismatch (D3.b).</summary>
    internal enum Form
    {
        Value,
        Call,
        Ctor,
        EnumValue,
    }

    private static readonly Lazy<IReadOnlyList<NameCell>> Pool = new(() =>
    {
        var cells = new List<NameCell>();
        cells.AddRange(ReadClassCells());
        cells.AddRange(ReadModuleCells());
        cells.AddRange(StrValueCells());
        cells.AddRange(ReadBuiltinModuleCells());
        cells.AddRange(ReadDeclaringCells());
        cells.AddRange(GenericReceiverCells());
        cells.AddRange(TypePositionCells());
        cells.AddRange(StoreCells());
        cells.AddRange(GlobalCreatesBindingCells());
        cells.AddRange(TypeMemberCells());
        cells.AddRange(LocalCells());
        cells.AddRange(ImportedOuterCells());
        cells.AddRange(ComprehensionCells());
        cells.AddRange(CycleCells());
        cells.AddRange(OrderObservationCells());
        cells.AddRange(ReadTimingCells());
        cells.AddRange(LetInBinderCells());
        cells.AddRange(LoopBodyClosureCells());
        cells.AddRange(SiblingNestedTypeCells());
        cells.AddRange(AliasTypePositionCells());
        cells.AddRange(ResultValueFormCells());
        cells.AddRange(DeclarationShapeCells());
        cells.AddRange(SeedCells());
        return cells.Select(WithModuleAccessorTwin).ToList();
    });

    /// <summary>Python has no module-level property, and the twin table has no row for one: a body hosted in a module
    /// accessor is twinned as the same body in a function, read by a call.</summary>
    private static NameCell WithModuleAccessorTwin(NameCell c) =>
        c.Position == ReadPosition.ModuleAccessorBody && c.TwinSource == null && c.Source.Contains("property get f() -> int:", StringComparison.Ordinal)
            ? c with
            {
                TwinSource = c.Source
                    .Replace("property get f() -> int:", "def f() -> int:", StringComparison.Ordinal)
                    .Replace("v0: int = f\n", "v0: int = f()\n", StringComparison.Ordinal),
            }
            : c;

    internal static IReadOnlyList<NameCell> GeneratedCells() => Pool.Value;

    // ------------------------------------------------------------------------------------------- //
    // D1 / D3.b: the distinct value of every kind and how it is declared and read
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The distinct value per kind, so a wrong binding, a CLR default or a skipped initializer print differently.</summary>
    internal static int ValueOf(BindingKind kind) => kind switch
    {
        BindingKind.ModuleVar => 5,
        BindingKind.ModuleLet => 5,
        BindingKind.ModuleLiteralConst => 9,
        BindingKind.ModuleComputedConst => 11,
        BindingKind.ModuleProperty => 8,
        BindingKind.ModuleDef => 12,
        BindingKind.ModuleClass => 14,
        BindingKind.ModuleStruct => 19,
        BindingKind.ModuleEnum => 16,
        BindingKind.ModuleUnion => 17,
        BindingKind.ModuleInterface => 18,
        BindingKind.ModuleTypeAlias => 14,
        BindingKind.StaticField => 2,
        BindingKind.StaticProperty => 4,
        BindingKind.StaticMethod => 6,
        BindingKind.ClassLiteralConst => 1,
        BindingKind.ClassComputedConst => 7,
        BindingKind.NestedType => 20,
        BindingKind.InstanceField => 3,
        BindingKind.InstanceProperty => 24,
        BindingKind.InstanceMethod => 26,
        BindingKind.EnclosingLocal => 13,
        BindingKind.Parameter => 21,
        BindingKind.Inner => 22,
        BindingKind.LocalVar => 15,
        BindingKind.LocalLet => 15,
        BindingKind.LocalLiteralConst => 27,
        BindingKind.LocalComputedConst => 28,
        BindingKind.LocalDef => 7,
        BindingKind.LambdaTypedLocal => 7,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "has no value"),
    };

    private static Form FormOf(BindingKind kind) => kind switch
    {
        BindingKind.ModuleDef or BindingKind.StaticMethod or BindingKind.InstanceMethod
            or BindingKind.LocalDef or BindingKind.LambdaTypedLocal or BindingKind.Builtin => Form.Call,
        BindingKind.ModuleClass or BindingKind.ModuleStruct or BindingKind.ModuleTypeAlias or BindingKind.NestedType => Form.Ctor,
        BindingKind.ModuleEnum => Form.EnumValue,
        _ => Form.Value,
    };

    private static bool IsCallableKind(BindingKind kind) => FormOf(kind) == Form.Call;

    /// <summary>The builtins of the <c>Builtin</c> outer value: functions with different CLR overload sets and arities (R-KP restored
    /// <c>max</c>), and a type name read as a call (<c>int</c>).</summary>
    private static readonly (string Name, string Args, string Params, int Result)[] Builtins =
    {
        ("abs", "-3", "x: int", 3),
        ("len", "\"ab\"", "s: str", 2),
        ("max", "1, 2", "a: int, b: int", 2),
        ("int", "\"5\"", "s: str", 5),
    };

    /// <summary>The value a builtin-named binding carries, so the builtin's result and the binding never coincide.</summary>
    private const int BuiltinBindingValue = 7;

    private readonly record struct ReadName(string Name, string CallArgs, string Params, int? BuiltinResult)
    {
        public static readonly ReadName Plain = new("x", "", "", null);
        public bool IsBuiltin => BuiltinResult != null;
    }

    private static string ReadText(Form form, string prefix, ReadName n) => form switch
    {
        Form.Value => prefix + n.Name,
        Form.Call => $"{prefix}{n.Name}({n.CallArgs})",
        Form.Ctor => $"{prefix}{n.Name}({n.CallArgs}).v",
        Form.EnumValue => $"{prefix}{n.Name}.RED.value",
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    /// <summary>What reading a binding of <paramref name="kind"/> through <paramref name="form"/> does in python, the
    /// form having been fixed by the highest of the two candidate kinds. A form the kind does not admit is a type
    /// error in both languages.</summary>
    private static PyOutcome EvalPython(BindingKind kind, Form form, int value)
    {
        var own = FormOf(kind);
        if (own == form)
            return PyOutcome.Ran(value);
        return (own, form) switch
        {
            (Form.Value, Form.Call or Form.Ctor) => PyOutcome.Other("TypeError"),
            (Form.Value, Form.EnumValue) => PyOutcome.Other("AttributeError"),
            (Form.Call, Form.Ctor or Form.EnumValue) => PyOutcome.Other("AttributeError"),
            (Form.Ctor, Form.EnumValue) => PyOutcome.Other("AttributeError"),
            (Form.Ctor, Form.Call) => PyOutcome.Other("AttributeError"),
            (Form.EnumValue, Form.Ctor or Form.Call) => PyOutcome.Other("TypeError"),
            _ => PyOutcome.Other("TypeError"),
        };
    }

    private static int BindingValue(BindingKind kind, ReadName n)
        => n.IsBuiltin ? BuiltinBindingValue : ValueOf(kind);

    /// <summary>The module-level declaration of a kind, with the helper it needs. <paramref name="initOverride"/> is the
    /// initializer of a self / cycle cell.</summary>
    private static List<string> ModuleDecl(BindingKind kind, ReadName n, string? initOverride = null)
    {
        var name = n.Name;
        var value = BindingValue(kind, n);
        var init = initOverride ?? value.ToString(CultureInfo.InvariantCulture);
        switch (kind)
        {
            case BindingKind.ModuleVar:
                return new() { $"{name}: int = {init}" };
            case BindingKind.ModuleLet:
                return new() { $"let {name} = {init}" };
            case BindingKind.ModuleLiteralConst:
                return new() { $"const {name}: int = {init}" };
            case BindingKind.ModuleComputedConst:
                return new() { $"const {name}: int = {initOverride ?? "eleven()"}" };
            case BindingKind.ModuleProperty:
                return new() { $"property {name}: int = {init}" };
            case BindingKind.ModuleDef:
                return new() { $"def {name}({n.Params}) -> int:", $"    return {value}" };
            case BindingKind.ModuleClass:
                return new() { $"class {name}:", $"    v: int = {value}" };
            case BindingKind.ModuleStruct:
                return new() { $"struct {name}:", $"    v: int = {value}" };
            case BindingKind.ModuleEnum:
                return new() { $"enum {name}:", $"    RED = {value}" };
            case BindingKind.ModuleTypeAlias:
                return new() { "class Tgt:", $"    v: int = {value}", $"type {name} = Tgt" };
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a module kind with an expression read");
        }
    }

    /// <summary>The class-body declaration of a member kind. <paramref name="init"/> overrides the initializer (self / cycle).</summary>
    private static List<string> MemberDecl(BindingKind kind, ReadName n, string? init = null)
    {
        var name = n.Name;
        var value = BindingValue(kind, n).ToString(CultureInfo.InvariantCulture);
        var e = init ?? value;
        switch (kind)
        {
            case BindingKind.StaticField:
                return new() { "@static", $"{name}: int = {e}" };
            case BindingKind.StaticProperty:
                return new() { "@static", $"property {name}: int = {e}" };
            case BindingKind.StaticMethod:
                return new() { "@static", $"def {name}({n.Params}) -> int:", $"    return {value}" };
            case BindingKind.ClassLiteralConst:
                return new() { $"const {name}: int = {e}" };
            case BindingKind.ClassComputedConst:
                return new() { $"const {name}: int = {init ?? "seven()"}" };
            case BindingKind.NestedType:
                return new() { $"class {name}:", $"    v: int = {value}" };
            case BindingKind.InstanceField:
                return new() { $"{name}: int = {e}" };
            case BindingKind.InstanceProperty:
                return new() { $"property {name}: int = {e}" };
            case BindingKind.InstanceMethod:
                return new()
                {
                    n.IsBuiltin ? $"def {name}(self, {n.Params}) -> int:" : $"def {name}(self) -> int:",
                    $"    return {value}",
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a member kind");
        }
    }

    private static IEnumerable<string> Indent(IEnumerable<string> lines, int by = 4)
    {
        var pad = new string(' ', by);
        return lines.Select(l => pad + l);
    }

    private static string Join(IEnumerable<string> lines) => string.Join("\n", lines) + "\n";

    private static string MainBlock(params string[] body)
        => Join(new[] { "def main() -> None:" }.Concat(Indent(body)));

    /// <summary>The helpers a program needs, first in the file: declared at most once and only when referenced.</summary>
    private static List<string> Helpers(string text)
    {
        var helpers = new List<string>();
        if (text.Contains("seven()", StringComparison.Ordinal))
            helpers.AddRange(new[] { "def seven() -> int:", "    return 7" });
        if (text.Contains("eleven()", StringComparison.Ordinal))
            helpers.AddRange(new[] { "def eleven() -> int:", "    return 11" });
        if (text.Contains("ap(lambda", StringComparison.Ordinal))
            helpers.AddRange(new[] { "def ap(f: () -> int) -> int:", "    return f()" });
        return helpers;
    }

    private static string WithHelpers(string program) => Join(Helpers(program)) is var h && h.Length > 1 ? h + program : program;

    // ------------------------------------------------------------------------------------------- //
    // D3.c positions
    // ------------------------------------------------------------------------------------------- //

    /// <summary>Where a reader sits inside a type body and how <c>main</c> reaches it.</summary>
    private sealed record Placed(IReadOnlyList<string> Members, string Access, string[]? Setup = null, IReadOnlyList<string>? Inits = null);

    /// <summary>The class-body positions of roster 1 × the host only where the dispatch routes on it.</summary>
    private static readonly ReadPosition[] ClassBodyInitializers =
    {
        ReadPosition.StaticInitializer,
        ReadPosition.InstanceFieldInitializer,
        ReadPosition.DataclassFieldInitializer,
        ReadPosition.DataclassInitFieldInitializer,
        ReadPosition.StructFieldInitializer,
        ReadPosition.StructInitFieldInitializer,
        ReadPosition.ConstInitializer,
        ReadPosition.StaticPropertyDefault,
        ReadPosition.InstancePropertyDefault,
        ReadPosition.ParameterDefault,
    };

    /// <summary>The body positions of roster 4 hosted in a type.</summary>
    private static readonly ReadPosition[] ClassBodies =
    {
        ReadPosition.MethodBody,
        ReadPosition.StaticMethodBody,
        ReadPosition.InitBody,
        ReadPosition.AccessorGetBody,
        ReadPosition.AccessorSetBody,
        ReadPosition.NestedClassMethodBody,
        ReadPosition.NestedDefInMethod,
        ReadPosition.GeneratorBody,
        ReadPosition.LambdaStored,
        ReadPosition.LambdaImmediate,
        ReadPosition.LambdaPassed,
        ReadPosition.ComprehensionElement,
        ReadPosition.ComprehensionIterable,
    };

    /// <summary>Positions where <c>self</c> is in scope.</summary>
    private static bool IsSelfBearing(ReadPosition p) => p is ReadPosition.MethodBody or ReadPosition.InitBody
        or ReadPosition.AccessorGetBody or ReadPosition.AccessorSetBody or ReadPosition.NestedDefInMethod
        or ReadPosition.GeneratorBody or ReadPosition.LambdaStored or ReadPosition.LambdaImmediate
        or ReadPosition.LambdaPassed or ReadPosition.ComprehensionElement or ReadPosition.ComprehensionIterable;

    private static bool IsInitializer(ReadPosition p) => ClassBodyInitializers.Contains(p);

    /// <summary>The SPY0401 class: a position whose value must be a compile-time constant, because the C# target is an
    /// optional parameter (a parameter default, or a dataclass / struct field through the synthesized constructor).</summary>
    private static bool IsConstantOnly(ReadPosition p) => p is ReadPosition.ParameterDefault
        or ReadPosition.DataclassFieldInitializer or ReadPosition.StructFieldInitializer;

    private static string HostHeader(ReadPosition p) => p switch
    {
        ReadPosition.DataclassFieldInitializer or ReadPosition.DataclassInitFieldInitializer => "@dataclass\nclass H:",
        ReadPosition.StructFieldInitializer or ReadPosition.StructInitFieldInitializer => "struct H:",
        _ => "class H:",
    };

    private static Placed PlaceInClass(ReadPosition p, Nesting nesting, string e)
    {
        switch (p)
        {
            case ReadPosition.StaticInitializer:
                return nesting switch
                {
                    Nesting.Direct => new(new[] { "@static", $"r: int = {e}" }, "H.r"),
                    Nesting.ComprehensionIterable => new(new[] { "@static", $"r: list[int] = [v0 for v0 in [{e}]]" }, "H.r[0]"),
                    Nesting.ComprehensionElement => new(new[] { "@static", $"r: list[int] = [{e} for v0 in [1]]" }, "H.r[0]"),
                    Nesting.Lambda => new(new[] { "@static", $"r: () -> int = lambda: {e}" }, "H.r()"),
                    _ => throw new ArgumentOutOfRangeException(nameof(nesting)),
                };
            case ReadPosition.InstanceFieldInitializer:
            case ReadPosition.DataclassFieldInitializer:
            case ReadPosition.StructFieldInitializer:
                return new(new[] { $"r: int = {e}" }, "H().r");
            case ReadPosition.DataclassInitFieldInitializer:
            case ReadPosition.StructInitFieldInitializer:
                return new(new[] { $"r: int = {e}", "def __init__(self) -> None:", "    pass" }, "H().r");
            case ReadPosition.ConstInitializer:
                return new(new[] { $"const r: int = {e}" }, "H.r");
            case ReadPosition.StaticPropertyDefault:
                return new(new[] { "@static", $"property r: int = {e}" }, "H.r");
            case ReadPosition.InstancePropertyDefault:
                return new(new[] { $"property r: int = {e}" }, "H().r");
            case ReadPosition.ParameterDefault:
                return new(new[] { $"def r(self, p0: int = {e}) -> int:", "    return p0" }, "H().r()");
            case ReadPosition.MethodBody:
                return new(new[] { "def r(self) -> int:", $"    return {e}" }, "H().r()");
            case ReadPosition.StaticMethodBody:
                return new(new[] { "@static", "def r() -> int:", $"    return {e}" }, "H.r()");
            case ReadPosition.InitBody:
                return new(new[] { "r: int = 0", "def __init__(self) -> None:", $"    self.r = {e}" }, "H().r");
            case ReadPosition.AccessorGetBody:
                return new(new[] { "property get r(self) -> int:", $"    return {e}" }, "H().r");
            case ReadPosition.AccessorSetBody:
                return new(
                    new[] { "q0: int = 0", "property get r(self) -> int:", "    return self.q0", "property set r(self, v0: int):", $"    self.q0 = {e}" },
                    "h0.r",
                    new[] { "h0: H = H()", "h0.r = 0" });
            case ReadPosition.NestedClassMethodBody:
                return new(new[] { "class I:", "    def r(self) -> int:", $"        return {e}" }, "H.I().r()");
            case ReadPosition.NestedDefInMethod:
                return new(new[] { "def r(self) -> int:", "    def g() -> int:", $"        return {e}", "    return g()" }, "H().r()");
            case ReadPosition.GeneratorBody:
                return new(new[] { "def r(self) -> int:", $"    yield {e}" }, "[g0 for g0 in H().r()][0]");
            case ReadPosition.LambdaStored:
                return new(new[] { "def r(self) -> int:", $"    f: () -> int = lambda: {e}", "    return f()" }, "H().r()");
            case ReadPosition.LambdaImmediate:
                return new(new[] { "def r(self) -> int:", $"    return (lambda: {e})()" }, "H().r()");
            case ReadPosition.LambdaPassed:
                return new(new[] { "def r(self) -> int:", $"    return ap(lambda: {e})" }, "H().r()");
            case ReadPosition.ComprehensionElement:
                return new(new[] { "def r(self) -> int:", $"    return [{e} for v0 in [1]][0]" }, "H().r()");
            case ReadPosition.ComprehensionIterable:
                return new(new[] { "def r(self) -> int:", $"    return [v0 for v0 in [{e}]][0]" }, "H().r()");
            default:
                throw new ArgumentOutOfRangeException(nameof(p), p, "not a class position");
        }
    }

    /// <summary>The positions of roster 5 (module level) and the function hosted body positions: reader lines at module
    /// level and how <c>main</c> reaches them.</summary>
    private static (List<string> Lines, string Access) PlaceAtModule(ReadPosition p, Nesting nesting, string e)
    {
        switch (p)
        {
            case ReadPosition.ModuleInitializer:
                return nesting switch
                {
                    Nesting.Direct => (new() { $"r: int = {e}" }, "r"),
                    Nesting.ComprehensionIterable => (new() { $"r: list[int] = [v0 for v0 in [{e}]]" }, "r[0]"),
                    Nesting.ComprehensionElement => (new() { $"r: list[int] = [{e} for v0 in [1]]" }, "r[0]"),
                    Nesting.Lambda => (new() { $"r: () -> int = lambda: {e}" }, "r()"),
                    _ => throw new ArgumentOutOfRangeException(nameof(nesting)),
                };
            case ReadPosition.ModulePropertyInitializer:
                return (new() { $"property r: int = {e}" }, "r");
            case ReadPosition.ModuleAccessorBody:
                return (new() { "property get r() -> int:", $"    return {e}" }, "r");
            case ReadPosition.EnumMemberValue:
                return (new() { "enum R0:", $"    RED = {e}" }, "R0.RED.value");
            case ReadPosition.FunctionBody:
                return (new() { "def r() -> int:", $"    return {e}" }, "r()");
            case ReadPosition.NestedDefInFunction:
                return (new() { "def r() -> int:", "    def g() -> int:", $"        return {e}", "    return g()" }, "r()");
            case ReadPosition.GeneratorBody:
                return (new() { "def r() -> int:", $"    yield {e}" }, "[g0 for g0 in r()][0]");
            case ReadPosition.LambdaStored:
                return (new() { "def r() -> int:", $"    f: () -> int = lambda: {e}", "    return f()" }, "r()");
            case ReadPosition.LambdaImmediate:
                return (new() { "def r() -> int:", $"    return (lambda: {e})()" }, "r()");
            case ReadPosition.LambdaPassed:
                return (new() { "def r() -> int:", $"    return ap(lambda: {e})" }, "r()");
            case ReadPosition.ComprehensionElement:
                return (new() { "def r() -> int:", $"    return [{e} for v0 in [1]][0]" }, "r()");
            case ReadPosition.ComprehensionIterable:
                return (new() { "def r() -> int:", $"    return [v0 for v0 in [{e}]][0]" }, "r()");
            default:
                throw new ArgumentOutOfRangeException(nameof(p), p, "not a module position");
        }
    }

    private static string CellId(params object?[] parts)
        => string.Join("/", parts.Where(p => p != null).Select(p => Convert.ToString(p, CultureInfo.InvariantCulture)!.Replace(' ', '_')));

    private static string MainPrinting(string access, string[]? setup)
        => MainBlock((setup ?? Array.Empty<string>()).Append($"print({access})").ToArray());
}
