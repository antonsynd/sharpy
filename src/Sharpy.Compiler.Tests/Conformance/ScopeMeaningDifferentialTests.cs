using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Tests.Properties.Differential;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;
using Harness = Sharpy.Compiler.Tests.Conformance.NameResolutionDifferentialTests;
using TypeKind = Sharpy.Compiler.Semantic.TypeKind;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>Whether a declaration says its first value: R-QI's axis. Closed: a declaration has an initializer, has
/// none, or (a property) computes its value in accessor bodies.</summary>
internal enum ScopeInitializer
{
    Present,
    Absent,
    AccessorBodies,
}

/// <summary>Where a declaration's type comes from. Closed: written, or inferred from the initializer.</summary>
internal enum ScopeAnnotation
{
    Annotated,
    Inferred,
}

/// <summary>What a constant position is handed (#2432's expression-kind axis; charter §3.20l calls 43 and 45; the
/// owner's ruling 7 of 2026-11-20, #2423 comment 7042723494): every kind of member a constant position's answer is
/// asked about, and the two chains. Each member kind is the image of what the compiler records for the member:
/// <list type="bullet">
/// <item>a member of a referenced MODULE: the collection of discovery's <c>ModuleOverloads</c> that holds it, a field refined
/// by <c>FieldSignature.IsConst</c> × <c>IsGuarded</c> and a function by its overload count
/// (<see cref="ScopeMeaningDifferentialTests.ModuleMemberShapes"/>; ruling 13, comment 7046304965);</item>
/// <item>a member of a referenced TYPE: the subclasses of <c>ClrMemberResolution</c>, a field refined by what C# reads as a
/// constant from metadata (<c>FieldInfo.IsLiteral</c>, or a <c>DecimalConstantAttribute</c>: the owner's ruling 17, #2423
/// comment 7049561049) and whether its type is an enum (<see cref="ScopeMeaningDifferentialTests.ClrTypeMemberShapes"/>);</item>
/// <item>a member of an enum the program declares: <c>EnumMemberAccessKind</c> and the member itself, per enum shape
/// (<see cref="ScopeMeaningDifferentialTests.EnumReadKinds"/>);</item>
/// <item>any other name the program declares: <see cref="OwnMember"/>, refined by <see cref="BindingKind"/>
/// (<see cref="ScopeMeaningDifferentialTests.OwnReads"/>).</item>
/// </list>
/// The two chains are closed by call 45. How each member is SPELLED and where its reader stands is a second axis, per where
/// the member lives (ruling 12; <c>ScopeMeaningDifferentialTests.Axes.cs</c>).</summary>
internal enum ScopeConstantKind
{
    /// <summary>A constant of a referenced assembly: a CLR literal field read through a Stdlib module.</summary>
    ClrLiteralField,

    /// <summary>A module variable of the <c>.spy</c> stdlib, whose storage is guarded (N3): no constant.</summary>
    StdlibModuleVariable,

    /// <summary>A static of a referenced assembly's module that is neither literal nor guarded (a static property, a
    /// static readonly field): no constant.</summary>
    ClrStaticMember,

    /// <summary>A function of a referenced module with one overload, read uncalled (<c>math.sqrt</c>): a function value, no constant.</summary>
    ModuleFunction,

    /// <summary>A function of a referenced module with several overloads, read uncalled (<c>math.log</c>): the slot's type picks
    /// one, and the value is no constant.</summary>
    ModuleFunctionGroup,

    /// <summary>A literal field of a referenced type (<c>int.max_value</c>, <c>Math.PI</c>): a C# constant.</summary>
    ClrTypeLiteralField,

    /// <summary>A <c>const decimal</c> of a referenced type (<c>decimal.one</c>): in metadata a <c>static readonly</c> field
    /// carrying <c>DecimalConstantAttribute</c>, which C# reads back as a constant wherever a <c>decimal</c> can be one: a
    /// default and a <c>const</c>, never an attribute argument (ruling 17).</summary>
    ClrTypeDecimalConstant,

    /// <summary>A member of a referenced enum (<c>DayOfWeek.Monday</c>): a C# constant.</summary>
    ClrTypeEnumMember,

    /// <summary>A static field of a referenced type that is no literal (<c>static readonly</c>): no constant.</summary>
    ClrTypeStaticField,

    /// <summary>A static property of a referenced type: no constant.</summary>
    ClrTypeStaticProperty,

    /// <summary>A static method of a referenced type with one overload, read uncalled: a function value, no constant.</summary>
    ClrTypeMethod,

    /// <summary>A static method of a referenced type whose signature the checker's resolver declines to type (a <c>char</c>
    /// parameter: <c>Char.is_ascii_digit</c>), read uncalled: the checker types it <c>object</c>, and the value is no constant.</summary>
    ClrTypeDeclinedMethod,

    /// <summary>A static method group of a referenced type, read uncalled. Where the slot has a function type, that type
    /// selects the overload and the read is a function value, no constant (the owner's ruling 29, #2423 comment 7052893050;
    /// SPY0336 today: #2277). Where it has none (an attribute argument) the group is no value at all (SPY0336).</summary>
    ClrTypeMethodGroup,

    /// <summary>A member of an int enum of the program: a C# constant.</summary>
    IntEnumMember,

    /// <summary>An int enum member's <c>.value</c>: the underlying integer, a C# constant (ruling 2).</summary>
    IntEnumValue,

    /// <summary>An int enum member's <c>.name</c>: a call of the name channel, no constant.</summary>
    IntEnumName,

    /// <summary>A member of a string enum of the program: a singleton object, no C# constant.</summary>
    StringEnumMember,

    /// <summary>A string enum member's <c>.value</c>: the singleton's property, no constant.</summary>
    StringEnumValue,

    /// <summary>A string enum member's <c>.name</c>: the singleton's property, no constant.</summary>
    StringEnumName,

    /// <summary>A name the program declares, read under each spelling and from each placement the typed-read set has a
    /// reader for (<c>OwnSpellings</c>): the cell's target is the kind.</summary>
    OwnMember,

    /// <summary>A comparison chain whose every link is a constant.</summary>
    ComparisonChain,

    /// <summary>A comparison chain with a link that is no constant.</summary>
    ChainWithNonConstantLink,
}

/// <summary>What a type alias's target names: a type that exists, nothing, or the alias itself, directly or
/// through a ring of aliases. Closed.</summary>
internal enum ScopeAliasTarget
{
    Declared,
    Missing,
    Itself,
}

/// <summary>Whether anything names a type alias. Closed.</summary>
internal enum ScopeAliasUse
{
    Used,
    Unused,
}

/// <summary>Who calls a constructor overload. Closed: another constructor of the type, a method of it, an initializer
/// of it, a function outside it.</summary>
internal enum ScopeOverloadCaller
{
    InitDelegation,
    MethodBody,
    StaticInitializer,
    ModuleFunction,
}

/// <summary>Whose parameter default reads a member of the class its declaration stands in: a method's (the control: a
/// <c>def</c>'s default is evaluated in the class body, and reads the member today), a <c>lambda</c>'s, an arrow lambda's. The
/// two lambdas are the image of <c>LambdaExpression.IsArrowSyntax</c>; a default has no fourth owner in a class body that
/// plan-777361 holds (a <c>delegate</c>'s and a signature-only method's are #2779's).</summary>
internal enum ScopeDefaultOwner
{
    Def,
    Lambda,
    ArrowLambda,
}

/// <summary>How a program reads the <c>__init__</c> a <c>@dataclass</c> synthesizes (the owner's ruling 30, #2423 comment
/// 7179278898). Closed: a call that constructs the type; the type's name as a function value; <c>super().__init__</c> in a
/// class deriving from it; <c>self.__init__</c> in the type itself; a deriving class that declares no <c>__init__</c> and is
/// constructed with its base's; a deriving <c>@dataclass</c>, whose own <c>__init__</c> takes the base's fields first.</summary>
internal enum ScopeInitReader
{
    Construction,
    ConstructorReference,
    SuperInit,
    SelfInit,
    Forwarding,
    InheritedFields,
}

/// <summary>Whether what a reader hands an initializer is what it takes. Closed.</summary>
internal enum ScopeInitFit
{
    Fits,
    Misfits,
}

/// <summary>What the dataclass of a cell derives from (the owner's ruling 35, #2423 comment 7181238911, and the second review
/// of PR #2798). A literal set, the shapes measured: nothing; a class that is no dataclass and declares no <c>__init__</c>,
/// one whose <c>__init__</c> takes no argument, one whose <c>__init__</c> takes one; a dataclass, which stands above the
/// reader at both orders while the dataclass deriving from it moves; an interface; an <c>@abstract</c> class; a constructed
/// generic class. The compiler has no enumeration of what a class derives from, so a base named nowhere here has no cell.
/// <see cref="ScopeInitReader.InheritedFields"/> is the other half of a dataclass over a dataclass: there the generated
/// dataclass is the base, and the deriving one is the reader.</summary>
internal enum ScopeDataclassBase
{
    None,
    Plain,
    PlainWithInit,
    PlainWithInitParameters,
    Dataclass,
    Interface,
    AbstractClass,
    ConstructedGeneric,
}

/// <summary>Whether the dataclass's field has a default, and what a read does about it (ruling 35). Closed: no default; a
/// default the read overrides by handing a value; a default the read takes by handing none.</summary>
internal enum ScopeFieldDefault
{
    None,
    Overridden,
    Taken,
}

/// <summary>The typed slot a constructor reference is handed to (ruling 35: the two reviews of PR #2798 ran each). A
/// literal set and an open one, since the compiler has no enumeration of the places a function type stands at; the values
/// are the slots measured, and a slot named nowhere here has no cell. A function's parameter (the base matrix), a keyword
/// argument, an annotated local, a module variable, an instance field, a return, a list element, a tuple element, a
/// parameter whose function type returns the callee's own type parameter, and the same of a builtin (<c>map</c>); from the
/// second review, a method's parameter, a constructor's, a local typed by an alias of the function type, a dict value, a
/// <c>@static</c> field, a conditional's branch, a lambda's return and a re-assignment.</summary>
internal enum ScopeReferenceSlot
{
    Parameter,
    KeywordArgument,
    AnnotatedLocal,
    ModuleVariable,
    InstanceField,
    Return,
    ListElement,
    TupleElement,
    GenericParameter,
    BuiltinGenericParameter,
    MethodParameter,
    ConstructorParameter,
    AliasTypedLocal,
    DictValue,
    StaticField,
    ConditionalBranch,
    LambdaReturn,
    Reassignment,
}

/// <summary>The type a reader names (ruling 35's reader compositions). Closed: the dataclass itself; a class deriving from
/// it that declares no <c>__init__</c> (<see cref="ScopeInitReader.Forwarding"/>'s class); a dataclass deriving from it
/// (<see cref="ScopeInitReader.InheritedFields"/>'s). A class that writes its <c>__init__</c> has one of its own to read.</summary>
internal enum ScopeInitThrough
{
    Itself,
    ForwardingClass,
    DerivingDataclass,
}

/// <summary>What names the dataclass where it is read (ruling 35). Closed: the name its site gives it (bare at module level,
/// qualified where it is nested); a type alias of it; from inside its enclosing type, its bare name.</summary>
internal enum ScopeDataclassName
{
    Declared,
    Alias,
    BareSibling,
}

/// <summary>Where a reader stands against the dataclass (ruling 35). Closed: apart from it and from its enclosing type (at
/// module level or in a reader class of its own: the base matrix); in the type the dataclass is nested in; in a method of
/// another dataclass, which the dataclass reads back in a ring (<c>Order.Cycle</c>), by a read of its own or by deriving
/// from it; in the dataclass's own body (<c>Order.Self</c>).</summary>
internal enum ScopeInitPlacement
{
    Apart,
    EnclosingType,
    OtherDataclass,
    Itself,
}

/// <summary>What a read hands a hand-written <c>__init__</c> (the owner's ruling 36, #2423 comment 7183929991). A literal
/// set, the shapes measured: a value whose type stands alone (the base cell); and the two whose type the parameter gives,
/// an empty list literal and a value that fixes a generic class's type parameter.</summary>
internal enum ScopeInitArgument
{
    Typed,
    EmptyList,
    TypeParameter,
}

/// <summary>A member <c>@dataclass</c> generates (the owner's ruling 37, #2423 comment 7183929991): the image of the names
/// <c>DataclassSynthesis.SynthesizeMembers</c> looks for among the class's own methods, read from its source
/// (<see cref="ScopeMeaningDifferentialTests.SynthesizedMemberNames"/>).</summary>
internal enum ScopeDataclassMember
{
    Init,
    Eq,
    Hash,
    Repr,
}

/// <summary>How a program reads a generated member that is no <c>__init__</c> (ruling 37). A literal set, the shapes
/// measured: through the operator or builtin that calls it (<c>==</c>, <c>hash()</c>, <c>repr()</c>); by its own name; and
/// redefined in a deriving class, with <c>@override</c> and without.</summary>
internal enum ScopeMemberRead
{
    Used,
    CalledByName,
    RedefinedWithOverride,
    RedefinedWithoutOverride,
}

/// <summary>
/// Stage 0 of plan-777361 (P78; verification contract §11), the owner's rulings of 2026-11-20 (#2423 comments
/// 7030074628 item 1, 7039834146 item 5, 7042723494 items 7 to 9, 7046304965 items 12 and 13, 7049561049 items 17, 19 and 20,
/// and 7050154806 item 22) and of 2026-11-26 (comments 7179278898, item 30, 7181238911, item 35, and 7183929991, items 36 and 37): the cells of rules N1, N2, N4 and N7 that the P77a pool
/// (<see cref="NameResolutionDifferentialTests"/>) has no program for, as a generated test type of their own. A cell is
/// a <see cref="NameCell"/>, and its tag and rule are READ from the rule table (<c>name-resolution-rules.txt</c>, #2487):
/// the generator writes the program, what it prints when it runs and the codes a refusal may carry, and never a verdict.
///
/// Ten cell sets. Every axis is the image of a compiler enumeration read by reflection, a census of the compiler's own
/// output, the P77a pool, or a set named closed; every value is generated or excluded with its reason
/// (<see cref="Axes_EveryValueIsGeneratedOrExcluded"/>), and an exclusion that rests on what the compiler does today
/// has a probe that goes red when it stops (<see cref="Exclusions_RestingOnTheCompiler_GoRedWhenItChanges"/>). The
/// axes and their sources are in <c>ScopeMeaningDifferentialTests.Axes.cs</c>:
/// <list type="bullet">
/// <item><c>no-initializer</c> (R-QI): <see cref="BindingKind"/> × <see cref="ScopeInitializer"/> × host
/// (<c>TypeKind</c>) × declared type (<c>SemanticType</c>'s subclasses);</item>
/// <item><c>typed-read</c>: a read handed to a <c>bool</c> slot, <see cref="BindingKind"/> × <see cref="ScopeAnnotation"/>
/// × order × <c>Spelling</c> × <see cref="ReadPosition"/>, the last two as the P77a pool runs them;</item>
/// <item><c>callee</c>: a binder's name called under an outer binding of the name, binder (<see cref="LocalTarget"/> and
/// the parameter) × outer (<see cref="BindingKind"/>);</item>
/// <item><c>constant-kind</c> (#2432): host (the call sites of <c>ConstantDefaultClassifier.IsAdmitted</c>, each with its
/// <c>AdmissionTable</c>, and the enum member's value; the constant slots no classifier call judges besides are #2779's, by
/// the owner's decision: <see cref="EnumMemberHost"/>) × <see cref="ScopeConstantKind"/> × the read's spelling and the
/// reader's placement (an import's forms, a type's alias, and for the program's own names <c>Spelling</c> × inside or outside
/// the member's type, as the typed-read set's readers have them);</item>
/// <item><c>init-overload</c>: <see cref="ScopeOverloadCaller"/> × order;</item>
/// <item><c>dataclass-init</c> (D2; rulings 30 and 35): <see cref="ScopeInitReader"/> × <see cref="ScopeInitFit"/> × order × the
/// site the dataclass is declared at (<c>ScopeKind</c>) × where a reader that is an expression stands (the positions at which
/// the typed-read set reads a module function), and, each varied alone from that matrix's first value, the dataclass's base
/// (<see cref="ScopeDataclassBase"/>), its field's default (<see cref="ScopeFieldDefault"/>), the slot a reference is handed
/// to (<see cref="ScopeReferenceSlot"/>), the type a reader names (<see cref="ScopeInitThrough"/>), the name it is read by
/// (<see cref="ScopeDataclassName"/>) and where the reader stands (<see cref="ScopeInitPlacement"/>);</item>
/// <item><c>written-init</c> (D2; ruling 36): a hand-written <c>__init__</c>, <see cref="ScopeInitReader"/> ×
/// <see cref="ScopeInitFit"/> × order, and, varied alone, what a read that fits hands (<see cref="ScopeInitArgument"/>);</item>
/// <item><c>dataclass-member</c> (D2; ruling 37): <see cref="ScopeDataclassMember"/> × <see cref="ScopeMemberRead"/> × order;</item>
/// <item><c>lambda-default</c> (D4; ruling 19): <see cref="ScopeDefaultOwner"/> × the class-level initializer that holds the
/// lambda (the positions at which the P77a pool runs a bare read of the member from its own type) × order × a module
/// binding of the name;</item>
/// <item><c>alias-target</c>: declaration site (<c>ScopeKind</c> × <c>TypeKind</c>) × target shape (the properties of the
/// alias's AST records) × <see cref="ScopeAliasTarget"/> × <see cref="ScopeAliasUse"/> × order. An alias that reaches
/// itself has its unused cells, which must run, and no used one (#2769).</item>
/// </list>
/// Known-red cells are rows of <c>scope-meaning-differential-allowlist.txt</c> (a missing file reads as empty), each citing
/// the issue whose fix turns it green, by the cell's class (<see cref="ProposedCite"/>). A must-refuse cell refused under a code its cell does not admit is listed as
/// <see cref="VerdictClass.FalseRefusal"/> with the lane <c>wrong-code</c>: the refusal is another rule's.
/// </summary>
[Trait("Category", "GapDiscovery")]
public sealed partial class ScopeMeaningDifferentialTests : IntegrationTestBase
{
    private const string AllowlistFileName = "scope-meaning-differential-allowlist.txt";
    private const int SharpyExecTimeoutMs = 12_000;
    private const int PythonBatchTimeoutMs = 600_000;

    public ScopeMeaningDifferentialTests(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    /// <summary>A generated case: the cell, and whether python runs the same program with the same meaning (no twin, no vote).</summary>
    internal sealed record Case(NameCell Cell, bool Twin);

    private const string Main = "def main() -> None:\n";

    private static CellKey Key(params object?[] parts)
    {
        var kept = parts.Where(p => p != null).Select(p => p!).ToArray();
        return new CellKey(string.Join("/", kept.Select(p => Convert.ToString(p, System.Globalization.CultureInfo.InvariantCulture))), kept);
    }

    /// <summary>A cell before the table has said what it must do. <paramref name="model"/> is what it prints if it runs;
    /// <paramref name="refusal"/> the codes a refusal of it may carry.</summary>
    private static Case Draft(CellKey key, Family family, string source, string? model, string[] refusal, bool twin,
        BindingKind target = BindingKind.None, BindingKind outer = BindingKind.None, ReadPosition? position = null,
        LocalTarget? construct = null, bool stdlib = false, DeclaringRelation? relation = null)
        => new(
            new NameCell
            {
                Key = key,
                Family = family,
                Source = source,
                Tag = SharpyTag.Unruled,
                Model = model,
                Python = model == null ? new PyOutcome(PyKind.OtherError, "no twin") : PyOutcome.Ran(model),
                Rule = "",
                Target = target,
                Outer = outer,
                Position = position,
                Construct = construct,
                Relation = relation,
                SeedRefusalCodes = new HashSet<string>(refusal, StringComparer.Ordinal),
                NeedsStdlib = stdlib,
            },
            twin);

    private static string Indent(string text) => string.Join("\n", text.Split('\n').Select(l => "    " + l));

    private static string Lines(IEnumerable<string> lines) => string.Join("\n", lines) + "\n";

    // ------------------------------------------------------------------------------------------- //
    // no-initializer (R-QI)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The code R-QI's refusal carries: plan-777361's D7 and D9 introduce it with the refusal itself.</summary>
    internal const string NoInitializerRefusal = "SPY0620";

    private static IEnumerable<Case> NoInitializerCases()
    {
        foreach (var (kind, d) in Declarations.OrderBy(e => e.Key))
        {
            foreach (var host in d.InType ? DeclarationHosts.OrderBy(h => h.Key).SelectMany(h => h.Value).ToArray() : new DeclarationHost?[] { null })
            {
                foreach (var type in ValueTypes.OrderBy(e => e.Key.Name, StringComparer.Ordinal).SelectMany(e => e.Value))
                {
                    foreach (var initializer in Enum.GetValues<ScopeInitializer>())
                    {
                        if (initializer == ScopeInitializer.AccessorBodies && d.Accessor == null)
                            continue;
                        // A struct's instance field initializer is a constant position (the 8951 pool's StructFieldInitializer):
                        // a declared type whose value is no constant has no initializer to write there.
                        if (initializer == ScopeInitializer.Present && kind == BindingKind.InstanceField && host?.Kind == TypeKind.Struct && !type.Constant)
                            continue;
                        string F(string text) => text.Replace("{T}", type.Spelling, StringComparison.Ordinal).Replace("{L}", type.Literal, StringComparison.Ordinal);
                        string decl = initializer switch
                        {
                            ScopeInitializer.Present => F(d.Text) + " = " + type.Literal,
                            ScopeInitializer.Absent => F(d.Text),
                            _ => F(d.Accessor!),
                        };
                        var lines = new List<string>();
                        if (type.Prelude != null)
                            lines.Add(type.Prelude);
                        // The read lands in a local of the declared type before it is shown: a member of an untyped read is #2417's.
                        var print = $"    v: {type.Spelling} = {d.Read}\n    print({string.Format(System.Globalization.CultureInfo.InvariantCulture, type.Show, "v")})";
                        if (kind == BindingKind.LocalVar)
                        {
                            lines.Add(Main.TrimEnd());
                            lines.Add(Indent(decl));
                            if (initializer == ScopeInitializer.Absent)
                                lines.AddRange(d.Stored!.Select(s => Indent(F(s))));
                            lines.Add(print);
                        }
                        else
                        {
                            if (d.InType)
                            {
                                lines.Add($"{host!.Keyword} H:");
                                lines.Add(Indent(decl));
                                if (initializer == ScopeInitializer.Absent && d.Stored != null)
                                    lines.AddRange(d.Stored.Select(s => Indent(F(s))));
                            }
                            else
                                lines.Add(decl);
                            lines.Add(Main.TrimEnd());
                            lines.Add(print);
                        }
                        // Read where R-QI refuses; the kinds it leaves alone store first, so every cell that runs prints the stored value.
                        bool twin = d.Twin && type.Twin && host is null or { Kind: TypeKind.Class }
                            && (initializer == ScopeInitializer.Present || (initializer == ScopeInitializer.Absent && d.Stored != null));
                        // R-QI's refusal is SPY0620, the code plan-777361 introduces for it (D7, D9): no compiler says it yet, so a
                        // cell that must be refused is red until then, and a refusal under any other code is another rule's.
                        yield return Draft(Key("read", "no-initializer", kind, initializer, host, type.Label), Family.Read, Lines(lines), "5",
                            new[] { NoInitializerRefusal }, twin, target: kind);
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // typed-read: a read handed to a bool slot is the type mismatch, whatever the order, spelling and position
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The probe: a function that takes a <c>bool</c>. Handed an <c>int</c> the checker has typed, the call is
    /// SPY0220; handed a read it has not typed, it is C#'s CS1503 behind SPY0908 (measured @ 55846bb3f).</summary>
    private const string Probe = "def nb(v: bool) -> int:\n    return 1";

    private const string Five = "def five() -> int:\n    return 5";

    /// <summary>The reader of a constant-only position: its slot is declared <c>bool</c>, since a call is no constant there.</summary>
    private static string[] BoolSlot(ReadPosition p, string read) => p switch
    {
        ReadPosition.ParameterDefault => new[] { $"def r(self, p0: bool = {read}) -> int:", "    return 1" },
        ReadPosition.DataclassFieldInitializer or ReadPosition.StructFieldInitializer => new[] { $"r: bool = {read}" },
        _ => throw new ArgumentOutOfRangeException(nameof(p), p, "not a constant-only position"),
    };

    private static IEnumerable<Case> TypedReadCases()
    {
        foreach (var (kind, t) in TypedReads.OrderBy(e => e.Key))
        {
            foreach (var annotation in Enum.GetValues<ScopeAnnotation>())
            {
                var text = annotation == ScopeAnnotation.Annotated ? t.Annotated : t.Inferred;
                if (text == null)
                    continue;
                foreach (var reader in TypedReaders.Where(r => r.Kind == kind))
                {
                    foreach (var order in new[] { Harness.Order.Earlier, Harness.Order.Later })
                    {
                        string receiver = reader.Spelling switch
                        {
                            Harness.Spelling.Bare => "",
                            Harness.Spelling.SelfDot => "self.",
                            Harness.Spelling.Alias => t.Level == MemberLevel.Instance ? "A()." : "A.",
                            Harness.Spelling.Qualified => t.Level == MemberLevel.Instance ? "H()." : "H.",
                            _ => throw new ArgumentOutOfRangeException(nameof(reader), reader.Spelling, "no receiver"),
                        };
                        string read = receiver + t.Member;
                        bool constantOnly = Harness.IsConstantOnly(reader.Position);
                        string probed = constantOnly ? read : $"nb({read})";
                        var prelude = new List<string> { Probe };
                        if (t.NeedsFive)
                            prelude.Add(Five);
                        var body = new List<string>();
                        if (reader.Inside)
                        {
                            // The reader is a sibling of the member, in the member's own type.
                            var members = constantOnly ? BoolSlot(reader.Position, probed) : Harness.PlaceInClass(reader.Position, reader.Nesting, probed).Members.ToArray();
                            var member = text.Split('\n');
                            body.Add(Harness.HostHeader(reader.Position));
                            body.AddRange((order == Harness.Order.Earlier ? member.Concat(members) : members.Concat(member)).Select(l => "    " + l));
                            if (reader.Spelling == Harness.Spelling.Alias)
                                body.Add("type A = H");
                        }
                        else
                        {
                            var target = new List<string>();
                            if (t.Level == MemberLevel.Module)
                                target.Add(text);
                            else
                            {
                                target.Add("class H:\n" + Indent(text));
                                if (reader.Spelling == Harness.Spelling.Alias)
                                    target.Add("type A = H");
                            }
                            var host = new List<string>();
                            if (constantOnly)
                            {
                                host.Add(Harness.HostHeader(reader.Position).Replace("H:", "Rd:", StringComparison.Ordinal));
                                host.AddRange(BoolSlot(reader.Position, probed).Select(l => "    " + l));
                            }
                            else if (ModulePlacedPositions.Contains(reader.Position))
                                host.AddRange(Harness.PlaceAtModule(reader.Position, reader.Nesting, probed).Lines);
                            else
                            {
                                host.Add(Harness.HostHeader(reader.Position).Replace("H:", "Rd:", StringComparison.Ordinal));
                                host.AddRange(Harness.PlaceInClass(reader.Position, reader.Nesting, probed).Members.Select(l => "    " + l));
                            }
                            body.AddRange(order == Harness.Order.Earlier ? target.Concat(host) : host.Concat(target));
                        }
                        var lines = prelude.Concat(body).ToList();
                        if (string.Join("\n", body).Contains("ap(lambda", StringComparison.Ordinal))
                            lines.Insert(0, "def ap(f: () -> int) -> int:\n    return f()");
                        lines.Add(Main + "    print(1)");
                        yield return Draft(Key("read", "typed-read", kind, annotation, order, reader.Spelling, reader.Position, reader.Nesting), Family.Read, Lines(lines), null,
                            new[] { "SPY0220" }, twin: false, target: kind, position: reader.Position);
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // callee: the innermost binding of a name is what a call of it calls
    // ------------------------------------------------------------------------------------------- //

    private const string One = "def one() -> int:\n    return 1";
    private const string Twelve = "def twelve() -> int:\n    return 12";
    private const string CalleeCm = "class CM:\n    def __enter__(self) -> () -> int:\n        return one\n    def __exit__(self) -> None:\n        pass";

    private static IEnumerable<Case> CalleeCases()
    {
        foreach (var (outer, o) in CalleeOuters.OrderBy(e => e.Key))
        {
            // The binders: a parameter, which is no LocalTarget, and each construct target.
            foreach (var binder in new object[] { BindingKind.Parameter }.Concat(CalleeBinders.Keys.OrderBy(k => k).Cast<object>()))
            {
                string n = o.Name;
                bool parameter = binder is BindingKind;
                // The statements that bind n to `one` and print its call; a parameter binder's function is declared below.
                string[] stmts = parameter ? new[] { "print(f(one))" } : CalleeBinders[(LocalTarget)binder].Select(s => s.Replace("{n}", n, StringComparison.Ordinal)).ToArray();
                var lines = new List<string> { One };
                if (o.Text.Contains("twelve()", StringComparison.Ordinal))
                    lines.Add(Twelve);
                if (binder is LocalTarget.WithAs)
                    lines.Add(CalleeCm);
                string call;
                switch (o.Scope)
                {
                    case OuterScope.Module:
                        if (o.Text.Length > 0)
                            lines.Insert(0, o.Text);
                        if (parameter)
                            lines.Add($"def f({n}: () -> int) -> int:\n    return {n}()");
                        lines.Add(Main + string.Join("\n", stmts.Select(s => "    " + s)));
                        break;
                    case OuterScope.Member:
                        // The outer binding is a member of H, and the binder sits in a method of H.
                        lines.Add("class H:\n" + Indent(o.Text) + "\n"
                            + (parameter
                                ? $"    def m(self, {n}: () -> int) -> int:\n        return {n}()"
                                : "    def m(self) -> None:\n" + string.Join("\n", stmts.Select(s => "        " + s))));
                        call = parameter ? "print(H().m(one))" : "H().m()";
                        lines.Add(Main + "    " + call);
                        break;
                    default:
                        // The outer binding is a local of the enclosing function, and the binder sits in a nested def.
                        string signature = outer == BindingKind.Parameter ? $"def outer0({n}: int) -> None:" : "def outer0() -> None:";
                        var enclosing = new List<string> { signature };
                        if (outer != BindingKind.Parameter)
                            enclosing.Add(Indent(o.Text));
                        enclosing.Add(parameter
                            ? $"    def g({n}: () -> int) -> int:\n        return {n}()\n    print(g(one))"
                            : "    def g() -> None:\n" + string.Join("\n", stmts.Select(s => "        " + s)) + "\n    g()");
                        lines.Add(string.Join("\n", enclosing));
                        lines.Add(Main + (outer == BindingKind.Parameter ? "    outer0(12)" : "    outer0()"));
                        break;
                }
                var construct = binder as LocalTarget?;
                yield return Draft(Key("local", "callee", binder, $"outer={outer}"), Family.Local, Lines(lines), "1", Array.Empty<string>(),
                    twin: o.Twin && binder is BindingKind or LocalTarget.For or LocalTarget.Comprehension,
                    target: parameter ? BindingKind.Parameter : construct == LocalTarget.Comprehension ? BindingKind.Inner : BindingKind.LocalVar,
                    outer: outer, position: ReadPosition.FunctionBody, construct: construct, stdlib: o.Stdlib);
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // constant-kind (#2432)
    // ------------------------------------------------------------------------------------------- //

    private static IEnumerable<Case> ConstantKindCases()
    {
        foreach (var (table, host) in AllConstantHosts())
        {
            foreach (var (kind, expressions) in ConstantExpressions.OrderBy(e => e.Key))
            {
                foreach (var e in expressions)
                {
                    if (HostSkips(host, e.Type) != null)
                        continue;
                    // Python has the module-qualified read of a literal and the chain; every other spelling is Sharpy's own cell.
                    bool twin = host.Twin && (kind == ScopeConstantKind.ComparisonChain || (kind == ScopeConstantKind.ClrLiteralField && e.Spelling == ImportSpellings[0].Label));
                    yield return ConstantCase(Key("read", "constant-kind", table, host.Label, kind, e.Form, e.Label, e.Spelling), table, host, kind, e.Text, e.Type, e.Prints, e.Show,
                        imports: e.Import, prelude: e.Prelude, refusals: e.Refusals, target: e.Target, stdlib: e.Stdlib, twin: twin);
                }
            }
            foreach (var own in OwnReadsOf(host))
            {
                if (HostSkips(host, own.Type) != null)
                    continue;
                bool inside = own.Relation == DeclaringRelation.Self;
                yield return ConstantCase(
                    Key("read", "constant-kind", table, host.Label, ScopeConstantKind.OwnMember, own.Kind, own.Annotation, own.Spelling, own.Relation, own.Order,
                        own.Outer == BindingKind.None ? null : $"outer={own.Outer}", own.Form,
                        inside && own.Spelling == Harness.Spelling.Bare ? BareSibling : null, inside && host.InMethod ? InMethod : null),
                    table, host, ScopeConstantKind.OwnMember, own.Text, own.Type, own.Prints, own.Show, imports: null, prelude: own.Prelude, refusals: own.Refusals, target: own.Kind,
                    stdlib: false, twin: false, own: own);
            }
        }
    }

    /// <summary>One cell: the expression's imports and declarations, then the host's program around it. What <c>main</c> shows is
    /// the slot's value read into a local of the slot's type first, since a call on a function-typed property is #2770's. A
    /// read from the member's own type takes the host's <c>Inside</c> program, with the member above or below the slot.</summary>
    private static Case ConstantCase(CellKey key, AdmissionTable? table, ConstantHost host, ScopeConstantKind kind, string text, string type, string prints, string show,
        string? imports, string? prelude, string[]? refusals, BindingKind target, bool stdlib, bool twin, OwnRead? own = null)
    {
        var lines = new List<string>();
        if (host.Imports != null)
            lines.Add(host.Imports);
        if (imports != null)
            lines.Add(imports);
        if (prelude != null)
            lines.Add(prelude);
        string program = host.Program;
        if (own?.Relation == DeclaringRelation.Self)
        {
            bool below = own.Order == Harness.Order.Later;
            program = host.Inside!
                .Replace("{above}", below ? "" : own.Member, StringComparison.Ordinal)
                .Replace("{below}", below ? own.Member : "", StringComparison.Ordinal)
                .Replace("{after}", own.After ?? "", StringComparison.Ordinal);
        }
        lines.Add(program.Replace("{T}", type, StringComparison.Ordinal).Replace("{e}", text, StringComparison.Ordinal)
            .Replace("{s}", string.Format(System.Globalization.CultureInfo.InvariantCulture, show, "v"), StringComparison.Ordinal));
        return Draft(key, Family.Read, Lines(lines), host.PrintsOne ? "1" : prints, own?.Only ?? new[] { host.Refusal }.Concat(refusals ?? Array.Empty<string>()).ToArray(), twin,
            target: target, outer: own?.Outer ?? BindingKind.None, position: host.Position, stdlib: stdlib, relation: own?.Relation == DeclaringRelation.Self ? DeclaringRelation.Self : null);
    }

    // ------------------------------------------------------------------------------------------- //
    // init-overload: a constructor's overload set is the whole body's
    // ------------------------------------------------------------------------------------------- //

    private static IEnumerable<Case> InitOverloadCases()
    {
        const string Target = "def __init__(self, x: int):\n    self.x = x";
        foreach (var caller in Enum.GetValues<ScopeOverloadCaller>())
        {
            foreach (var order in new[] { Harness.Order.Earlier, Harness.Order.Later })
            {
                (string? member, string? function, string read) = caller switch
                {
                    ScopeOverloadCaller.InitDelegation => ("def __init__(self):\n    self.__init__(7)", null, "P().x"),
                    ScopeOverloadCaller.MethodBody => ("def mk(self) -> int:\n    return P(7).x", null, "P(1).mk()"),
                    ScopeOverloadCaller.StaticInitializer => ("@static\nd: int = P(7).x", null, "P.d"),
                    _ => ((string?)null, "def mk() -> int:\n    return P(7).x", "mk()"),
                };
                var members = new List<string> { "x: int = 0" };
                if (member == null)
                    members.Add(Target);
                else
                    members.AddRange(order == Harness.Order.Earlier ? new[] { Target, member } : new[] { member, Target });
                var type = "class P:\n" + string.Join("\n", members.Select(Indent));
                var lines = function == null ? new[] { type } : order == Harness.Order.Earlier ? new[] { type, function } : new[] { function, type };
                yield return Draft(Key("read", "init-overload", caller, order), Family.Read, string.Join("\n", lines) + $"\n{Main}    print({read})\n", "7",
                    Array.Empty<string>(), twin: false, target: BindingKind.Constructor);
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // dataclass-init: the __init__ a @dataclass synthesizes is read whatever the order (D2; ruling 30)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>
    /// A read of the <c>__init__</c> a <c>@dataclass</c> synthesizes, with the dataclass above its reader and below it (the
    /// owner's ruling 30, #2423 comment 7179278898, which folds the cells of comment 7171020757 into plan-777361). The checker
    /// adds the synthesized <c>__init__</c> to the type when it reaches the class, so a reader above the class sees none
    /// (measured @ 28f10cd51: a constructor reference that fits is SPY0342 there, one that does not is CS7036 behind SPY0908,
    /// <c>super().__init__</c> is SPY0203, and a deriving dataclass gets no parameter for an inherited field).
    ///
    /// No verdict is written here but by the order-free rule (N1; D2's predicate): a cell does what its CONTROL does, the same
    /// program with the dataclass declared first. A read that fits runs and prints what the field holds (<see cref="InitShape.Prints"/>);
    /// one that does not is refused under the code the control carries (<see cref="InitRead.Refusal"/>,
    /// <see cref="InitShape.ReferenceRefusal"/>). A reader that is an expression stands at every slot
    /// <see cref="InitReaderSlots"/> derives, and hands what it makes (the constructed value, the reference) to a helper
    /// declared below the dataclass at both orders, so the slot holds no read of the dataclass's members (an untyped forward
    /// read of a member is the typed-read set's). The construction cell is the slot's own control: it runs at both orders today,
    /// so a red cell beside it is red for what it reads of the initializer.
    ///
    /// Three parts (the owner's ruling 35, #2423 comment 7181238911: every shape the review of PR #2798 found is a cell). The
    /// MATRIX is reader × fit × order × site × slot over the two sites that are not projected. A PROJECTION is one value of an
    /// axis the matrix holds at its first value (<see cref="InitProjections"/>), under every reader and fit, at both orders,
    /// with an expression at <see cref="ProjectedSlots"/>: each new value is varied alone, and the product of two new values,
    /// or of one with the matrix's 26 slots, is not taken. A PLACEMENT moves the reader: into the type the dataclass is nested
    /// in, under the qualified name, and into a method of another dataclass, where <c>Order.Cycle</c> is the ring in which the
    /// dataclass reads that one back: by a read of its own, or (<see cref="OtherDataclassRings"/>) by deriving from it.
    ///
    /// Python runs a cell's program with the same meaning wherever it has the shape and the read (<see cref="InitShape.Twin"/>,
    /// <see cref="InitRead.Python"/>) and binds the dataclass's name before it reads it. Python binds a name when the
    /// statement that reads it runs, so a reader in a function's or a method's body, which <c>main</c> calls, is python's
    /// program at every order (<see cref="CalledSlots"/>, <see cref="InitRead.WhenCalled"/>); a reader that is a declaration, or
    /// one a module or a type body evaluates as it loads, is python's with the dataclass above it alone.
    ///
    /// Three must-refuse cells do not vary with order today: <c>ConstructorReference/Misfits/Later</c> under
    /// <c>Overridden</c> and under <c>DerivingDataclass</c>, and <c>SuperInit/Misfits/Later</c> under <c>DerivingDataclass</c>,
    /// are refused under their control's code with the synthesized <c>__init__</c> absent as with it present, so each is green
    /// at both orders and says nothing of order until the <c>__init__</c> is there to read (the second review of PR #2798).
    /// </summary>
    private static IEnumerable<Case> DataclassInitCases()
    {
        var earlierAndLater = new[] { Harness.Order.Earlier, Harness.Order.Later };
        foreach (var site in AllDataclassSites().Where(s => !s.Projected))
        {
            foreach (var (reader, r) in InitReaders.OrderBy(e => e.Key))
            {
                foreach (var fit in InitFitsOf(reader, r))
                {
                    foreach (var slot in r.Shows == null ? InitReaderSlots.Select(s => ((ReadPosition Position, Harness.Nesting Nesting)?)s).ToArray() : new (ReadPosition Position, Harness.Nesting Nesting)?[] { null })
                    {
                        foreach (var order in earlierAndLater)
                            yield return InitCell(reader, r, fit, order, site.Label, slot, site.Shape, null);
                    }
                }
            }
        }
        foreach (var p in InitProjections)
        {
            foreach (var (reader, r) in p.Readers.OrderBy(e => e.Key))
            {
                foreach (var fit in InitFitsOf(reader, r).Where(f => !ExcludedProjections.ContainsKey((p.Value, reader, f))))
                {
                    foreach (var slot in r.Shows == null ? ProjectedSlots.Select(s => ((ReadPosition Position, Harness.Nesting Nesting)?)s).ToArray() : new (ReadPosition Position, Harness.Nesting Nesting)?[] { null })
                    {
                        foreach (var order in earlierAndLater)
                            yield return InitCell(reader, r, fit, order, p.Site, slot, p.Shape, p.Variant);
                    }
                }
            }
        }
        foreach (var (reader, r) in InitReaders.OrderBy(e => e.Key))
        {
            foreach (var fit in InitFitsOf(reader, r))
            {
                foreach (var slot in r.Shows == null ? EnclosedSlots.Select(s => ((ReadPosition Position, Harness.Nesting Nesting)?)s).ToArray() : new (ReadPosition Position, Harness.Nesting Nesting)?[] { null })
                {
                    foreach (var order in earlierAndLater)
                        yield return EnclosedInitCell(reader, r, fit, order, slot);
                }
                if (ExcludedPlacementReaders.ContainsKey((ScopeInitPlacement.OtherDataclass, reader)))
                    continue;
                foreach (var ring in OtherDataclassRings)
                {
                    foreach (var order in OtherDataclassOrders)
                        yield return OtherDataclassInitCell(reader, r, fit, order, ring);
                }
            }
        }
    }

    /// <summary>The fits a reader has a program for: both, less the pairs <see cref="ExcludedInitFits"/> gives a reason.</summary>
    private static IEnumerable<ScopeInitFit> InitFitsOf(ScopeInitReader reader, InitRead r)
        => Enum.GetValues<ScopeInitFit>().Where(fit => !ExcludedInitFits.ContainsKey((reader, fit)) && (fit == ScopeInitFit.Fits || r.Misfits != null));

    /// <summary>The verdict-free parts of a cell: what it prints if it runs, and the one code its refusal carries if it must not.</summary>
    private static Case InitDraft(CellKey key, string source, ScopeInitFit fit, InitRead r, InitShape shape, string prints, bool twin, ReadPosition? position)
    {
        bool fits = fit == ScopeInitFit.Fits;
        string[] refusal = fits ? Array.Empty<string>()
            : new[] { r.Refusal ?? shape.ReferenceRefusal ?? throw new InvalidOperationException($"{key}: a read that does not fit names the code its control carries") };
        return Draft(key, Family.Read, source, fits ? prints : null, refusal, twin && fits, target: BindingKind.Constructor, position: position);
    }

    /// <summary>Whether python runs a cell's program with the same meaning: it has the shape and the read, and it binds the
    /// type's name before the read runs. A reader that is an expression is read when <c>main</c> calls its slot, or (an
    /// initializer) as the module loads, where the helper the reader hands its value to, which stands last, is not bound yet at
    /// either order. A reader that is a declaration is read as the module loads unless it says otherwise. Measured, not
    /// assumed: with python asked about every cell that must run, it ran these and no other (the sweep's python arm holds the
    /// first half, <see cref="AxisProblems"/> the second).</summary>
    private static bool InitTwin(InitShape shape, InitRead r, Harness.Order order, (ReadPosition Position, Harness.Nesting Nesting)? slot)
        => shape.Twin && r.Python && (slot is not { } at || CalledSlots.Contains(at))
            // Above the type, every read of its name has to wait for `main`: a type that derives from it and stands with the
            // reader (`Above`) names it in a base list, which python evaluates as the module loads.
            && (order == Harness.Order.Earlier || (shape.Above == null && (slot != null || r.WhenCalled)));

    /// <summary>A cell whose reader stands apart from the dataclass: a declaration of its own at module level, or an expression
    /// at the slot. <paramref name="variant"/> is the projected axis's value, which the matrix's cells do not carry.</summary>
    private static Case InitCell(ScopeInitReader reader, InitRead r, ScopeInitFit fit, Harness.Order order, string? site, (ReadPosition Position, Harness.Nesting Nesting)? slot, InitShape shape, object? variant,
        string route = DataclassInitRoute)
    {
        bool fits = fit == ScopeInitFit.Fits;
        string read = shape.Fill(fits ? r.Fits : r.Misfits!);
        string? helper = fits ? r.FitsHelper : r.MisfitsHelper;
        var declared = new List<string>();
        if (shape.Above != null)
            declared.Add(shape.Above);
        string main;
        if (slot is not { } at)
        {
            declared.Add(read);
            main = Harness.MainPrinting(shape.Fill(r.Shows!), null);
        }
        else if (ModulePlacedPositions.Contains(at.Position))
        {
            var placed = Harness.PlaceAtModule(at.Position, at.Nesting, read);
            declared.AddRange(placed.Lines);
            main = Harness.MainPrinting(placed.Access, null);
        }
        else
        {
            var placed = Harness.PlaceInClass(at.Position, at.Nesting, read);
            declared.Add(Harness.HostHeader(at.Position));
            declared.AddRange(placed.Members.Select(l => "    " + l));
            main = Harness.MainPrinting(placed.Access, placed.Setup);
        }
        var lines = new List<string>();
        if (declared.Any(l => l.Contains("ap(lambda", StringComparison.Ordinal)))
            lines.Add("def ap(f: () -> int) -> int:\n    return f()");
        // What the type derives from stands first at both orders: the one declaration that moves is the type's own.
        if (shape.First != null)
            lines.Add(shape.First);
        lines.AddRange(order == Harness.Order.Earlier ? declared.Prepend(shape.Declaration) : declared.Append(shape.Declaration));
        // Last, so below the dataclass at both orders: what the helper reads of the dataclass is no forward read.
        if (helper != null)
            lines.Add(shape.Fill(helper));
        return InitDraft(Key("read", route, reader, fit, order, site, slot?.Position, slot?.Nesting, variant), Lines(lines) + main, fit, r, shape, shape.Prints,
            InitTwin(shape, r, order, slot), slot?.Position);
    }

    /// <summary>A cell whose reader stands in the type the dataclass is nested in: a member of <c>H</c> beside the nested
    /// dataclass, above it or below it, reading it by its qualified name. A reader that is a declaration is a sibling class.</summary>
    private static Case EnclosedInitCell(ScopeInitReader reader, InitRead r, ScopeInitFit fit, Harness.Order order, (ReadPosition Position, Harness.Nesting Nesting)? slot)
    {
        bool fits = fit == ScopeInitFit.Fits;
        string read = EnclosedShape.Fill(fits ? r.Fits : r.Misfits!);
        string? helper = fits ? r.FitsHelper : r.MisfitsHelper;
        IEnumerable<string> members;
        string main;
        if (slot is not { } at)
        {
            members = read.Split('\n');
            main = Harness.MainPrinting("H." + EnclosedShape.Fill(r.Shows!), null);
        }
        else
        {
            var placed = Harness.PlaceInClass(at.Position, at.Nesting, read);
            members = placed.Members;
            main = Harness.MainPrinting(placed.Access, placed.Setup);
        }
        var nested = EnclosedShape.Declaration.Split('\n');
        var lines = new List<string> { "class H:" };
        lines.AddRange((order == Harness.Order.Earlier ? nested.Concat(members) : members.Concat(nested)).Select(l => "    " + l));
        if (helper != null)
            lines.Add(EnclosedShape.Fill(helper));
        // Python has the read from a method's body alone, and there at both orders: at the type body's level `H` is not bound
        // yet, so an initializer and a sibling class that name `H.DC` are no python program wherever the dataclass stands.
        bool twin = slot is { } placedAt && CalledSlots.Contains(placedAt);
        return InitDraft(Key("read", DataclassInitRoute, reader, fit, order, EnclosedSite, slot?.Position, slot?.Nesting, ScopeInitPlacement.EnclosingType), Lines(lines) + main, fit, r,
            EnclosedShape, EnclosedShape.Prints, twin, slot?.Position);
    }

    /// <summary>A cell whose reader stands in a method of another dataclass, <c>Rd</c>. At <c>Order.Cycle</c> the dataclass
    /// stands below the reader's dataclass and reads it back: a ring of two, where one of the two is read above its declaration
    /// whichever stands first. The cell's own read is the forward one. With <paramref name="ring"/> none the dataclass reads
    /// <c>Rd</c> back from a method of its own, by a read that fits. With a base (<see cref="OtherDataclassRings"/>) the
    /// dataclass derives from a dataclass <c>G</c>, which stands first at every order: the reader's dataclass is <c>Rd</c> at the
    /// two plain orders and <c>G</c> itself in the ring, where deriving from it is the reading back.</summary>
    private static Case OtherDataclassInitCell(ScopeInitReader reader, InitRead r, ScopeInitFit fit, Harness.Order order, ScopeDataclassBase ring)
    {
        bool fits = fit == ScopeInitFit.Fits;
        bool derived = ring != ScopeDataclassBase.None;
        var shape = derived ? DataclassBases[ring] : OtherDataclassShape;
        string? helper = fits ? r.FitsHelper : r.MisfitsHelper;
        bool cycle = order == Harness.Order.Cycle;
        string method = "\n    def t(self) -> int:\n        return ";
        string other = "@dataclass\nclass Rd:\n    w: int" + method + shape.Fill(fits ? r.Fits : r.Misfits!);
        var lines = new List<string>();
        string shows;
        if (derived)
        {
            lines.Add(cycle ? shape.First! + method + shape.Fill(fits ? r.Fits : r.Misfits!) : shape.First!);
            lines.AddRange(cycle ? new[] { shape.Declaration } : order == Harness.Order.Earlier ? new[] { shape.Declaration, other } : new[] { other, shape.Declaration });
            shows = cycle ? "G(1).t()" : "Rd(1).t()";
        }
        else
        {
            string dataclass = shape.Declaration + (cycle ? method + RingReads[reader].Read : "");
            lines.AddRange(order == Harness.Order.Earlier ? new[] { dataclass, other } : new[] { other, dataclass });
            shows = cycle ? "Rd(1).t() + DC(1).t()" : "Rd(1).t()";
        }
        if (helper != null)
            lines.Add(shape.Fill(helper));
        if (cycle && !derived)
            lines.Add(RingReads[reader].Helper);
        // Python binds a method's names when it runs: the same program at every order, the ring included.
        return InitDraft(Key("read", DataclassInitRoute, reader, fit, order, OtherDataclassSite, OtherDataclassSlot.Position, OtherDataclassSlot.Nesting, ScopeInitPlacement.OtherDataclass, derived ? ring : null),
            Lines(lines) + Harness.MainPrinting(shows, null), fit, r, shape, cycle && !derived ? "10" : shape.Prints, twin: true, OtherDataclassSlot.Position);
    }

    // ------------------------------------------------------------------------------------------- //
    // written-init: a hand-written __init__ is read whatever the order (D2; ruling 36)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>
    /// A read of an <c>__init__</c> the class WRITES, with the class above its reader and below it (the owner's ruling 36,
    /// #2423 comment 7183929991; the cells are measured on comment 7183781537). A reader above the class has the
    /// <c>__init__</c>'s arity and not its parameter types (measured @ 28f10cd51): a construction, a constructor reference and
    /// <c>super().__init__</c> handed a <c>str</c> for an <c>int</c> are SPY0908 CS1503 there, and an argument whose type the
    /// parameter gives (an empty list, a generic class's type parameter) is refused. The set is the <c>dataclass-init</c>
    /// set's sibling, on the same rule (N1; D2's predicate: a cell does what its control does) and under a route of its own,
    /// because that set's reasons are a dataclass's: there a construction that does not fit has no refusing control
    /// (<see cref="ExcludedInitFits"/>) and a misfit is one of arity, where here every reader's control refuses a misfit of type.
    ///
    /// A star, as the owner said: the base cell is reader × fit × order over <see cref="ScopeInitArgument.Typed"/>, an
    /// expression at <see cref="ProjectedSlots"/>; an argument shape is varied alone, under the reads that fit.
    /// </summary>
    private static IEnumerable<Case> WrittenInitCases()
    {
        foreach (var (argument, shape) in WrittenInits.OrderBy(e => e.Key))
        {
            foreach (var (reader, r) in WrittenInitReaders.OrderBy(e => e.Key))
            {
                foreach (var fit in Enum.GetValues<ScopeInitFit>().Where(f => (f == ScopeInitFit.Fits || r.Misfits != null) && !ExcludedWrittenInits.ContainsKey((argument, reader, f))))
                {
                    foreach (var slot in r.Shows == null ? ProjectedSlots.Select(s => ((ReadPosition Position, Harness.Nesting Nesting)?)s).ToArray() : new (ReadPosition Position, Harness.Nesting Nesting)?[] { null })
                    {
                        foreach (var order in new[] { Harness.Order.Earlier, Harness.Order.Later })
                            yield return InitCell(reader, r, fit, order, null, slot, shape, argument, WrittenInitRoute);
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // dataclass-member: a dataclass's other generated members are read whatever the order (D2; ruling 37)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>
    /// A read of a member <c>@dataclass</c> generates beside <c>__init__</c>, with the dataclass above its reader and below it
    /// (the owner's ruling 37, #2423 comment 7183929991, which widens D2's clause to the generated members). The checker adds
    /// them where it adds the <c>__init__</c>, so a reader above the dataclass sees none (measured @ 28f10cd51: a deriving
    /// class that redefines <c>__repr__</c> without <c>@override</c> is SPY0248 below the dataclass and runs above it; a member
    /// called by its name is SPY0427 below it and SPY0203 beside SPY0427 above it). The rule is the <c>dataclass-init</c>
    /// set's: a cell does what its control does, the same program with the dataclass declared first.
    /// </summary>
    private static IEnumerable<Case> DataclassMemberCases()
    {
        foreach (var ((member, read), m) in MemberReads.OrderBy(e => e.Key))
        {
            foreach (var order in new[] { Harness.Order.Earlier, Harness.Order.Later })
            {
                var lines = order == Harness.Order.Earlier ? new[] { ModuleDataclass, m.Reader } : new[] { m.Reader, ModuleDataclass };
                bool fits = m.Refusal == null;
                // A reader in a function's body is python's program at both orders; python has no `@override`, and its
                // dataclass is unhashable unless frozen (MemberRead.Python).
                yield return Draft(Key("read", DataclassMemberRoute, member, read, fits ? ScopeInitFit.Fits : ScopeInitFit.Misfits, order), Family.Read,
                    Lines(lines) + Harness.MainPrinting(m.Shows, null), fits ? m.Prints : null, fits ? Array.Empty<string>() : new[] { m.Refusal! },
                    twin: fits && m.Python && (order == Harness.Order.Earlier || m.WhenCalled), target: BindingKind.InstanceMethod);
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // lambda-default: a lambda's parameter default at a type body's level is evaluated in the type body (D4; ruling 19)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>
    /// A parameter default that reads a member of its own class by its bare name, per owner of the default
    /// (<see cref="DefaultOwners"/>). The owner's ruling 19 (#2423 comment 7049561049; unchanged by item 22 of 7050154806): a
    /// lambda's default in a class-level initializer "reads the member", as a <c>def</c>'s does, and the planning session reads
    /// that as one rule for both lambda spellings. So the class-body rule decides the read (D3): the member where it stands
    /// above the initializer or no module binding holds the name, N2's refusal at-or-after under one (SPY0619, which lands
    /// with plan-777361's T8). The member's value is 5 and a module binding's 12, so a default that reads the wrong one prints
    /// it. The lambda is called where it stands (<see cref="ExcludedLambdaUses"/>), at each position
    /// <see cref="LambdaDefaultPositions"/> derives; each owner and position has its control, the same slot with a LITERAL
    /// default, which runs today, so a red cell beside it is red for what its default reads.
    /// </summary>
    private static IEnumerable<Case> LambdaDefaultCases()
    {
        foreach (var (owner, o) in DefaultOwners.OrderBy(e => e.Key))
        {
            foreach (var position in o.Arrow == null ? new[] { ReadPosition.ParameterDefault } : LambdaDefaultKinds.SelectMany(LambdaDefaultPositions).Distinct().OrderBy(p => p).ToArray())
            {
                var placed = Harness.PlaceInClass(position, Harness.Nesting.Direct, o.Text.Replace("{d}", "5", StringComparison.Ordinal));
                var lines = new[] { Harness.HostHeader(position) }.Concat(placed.Members.Select(l => "    " + l));
                yield return Draft(Key("read", "lambda-default", owner, position, LiteralDefault), Family.Read, Lines(lines) + Harness.MainPrinting(placed.Access, placed.Setup), "5",
                    Array.Empty<string>(), twin: Harness.HostHeader(position) == "class H:", position: position);
            }
        }
        foreach (var kind in LambdaDefaultKinds.OrderBy(k => k))
        {
            var t = TypedReads[kind];
            foreach (var (owner, o) in DefaultOwners.OrderBy(e => e.Key))
            {
                foreach (var position in o.Arrow == null ? new[] { ReadPosition.ParameterDefault } : LambdaDefaultPositions(kind).ToArray())
                {
                    foreach (var annotation in Enum.GetValues<ScopeAnnotation>())
                    {
                        var declaration = annotation == ScopeAnnotation.Annotated ? t.Annotated : t.Inferred;
                        if (declaration == null)
                            continue;
                        foreach (var order in new[] { Harness.Order.Earlier, Harness.Order.Later })
                        {
                            foreach (var outer in new[] { BindingKind.None, SiblingOuter(callable: false) })
                            {
                                var placed = Harness.PlaceInClass(position, Harness.Nesting.Direct, o.Text.Replace("{d}", t.Member, StringComparison.Ordinal));
                                var member = declaration.Split('\n');
                                var lines = new List<string>();
                                if (outer != BindingKind.None)
                                    lines.Add("const x: int = 12");
                                lines.Add(Harness.HostHeader(position));
                                lines.AddRange((order == Harness.Order.Earlier ? member.Concat(placed.Members) : placed.Members.Concat(member)).Select(l => "    " + l));
                                bool refused = order == Harness.Order.Later && outer != BindingKind.None;
                                // Python evaluates a default where its `def` or `lambda` stands, top to bottom: it is the same program
                                // where the member stands above, in a plain class.
                                bool twin = order == Harness.Order.Earlier && Harness.HostHeader(position) == "class H:";
                                yield return Draft(Key("read", "lambda-default", owner, kind, annotation, position, order, outer == BindingKind.None ? null : $"outer={outer}"),
                                    Family.Read, Lines(lines) + Harness.MainPrinting(placed.Access, placed.Setup), "5", refused ? new[] { "SPY0619" } : Array.Empty<string>(), twin,
                                    target: kind, outer: outer, position: position, relation: DeclaringRelation.Self);
                            }
                        }
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // alias-target: an alias names a type that exists, used or not, wherever it is declared and whatever its shape
    // ------------------------------------------------------------------------------------------- //

    private const string AliasTargetClass = "class C0:\n    v: int = 1";

    /// <summary>The orders an alias has against what its target names: a declared type stands above or below it, a missing one
    /// has no declaration to stand anywhere, and the alias itself is reached directly or through a ring.</summary>
    internal static Harness.Order[] AliasOrders(ScopeAliasTarget target) => target switch
    {
        ScopeAliasTarget.Declared => new[] { Harness.Order.Earlier, Harness.Order.Later },
        ScopeAliasTarget.Missing => new[] { Harness.Order.Earlier },
        _ => new[] { Harness.Order.Self, Harness.Order.Cycle },
    };

    private static IEnumerable<Case> AliasTargetCases()
    {
        foreach (var site in AllAliasSites())
        {
            foreach (var shape in AliasShapes.Values.DistinctBy(s => s.Label).OrderBy(s => s.Label, StringComparer.Ordinal))
            {
                foreach (var target in Enum.GetValues<ScopeAliasTarget>())
                {
                    // A name a module qualifies is never the alias (ExcludedSelfReachingShapes says so, and the axes fact holds the two together).
                    if (target == ScopeAliasTarget.Itself && shape.Import != null)
                        continue;
                    foreach (var use in Enum.GetValues<ScopeAliasUse>())
                    {
                        // Named by an annotation, a self-reaching alias kills the compiler's process (#2769; ExcludedAliasUses).
                        if (target == ScopeAliasTarget.Itself && use == ScopeAliasUse.Used)
                            continue;
                        foreach (var order in AliasOrders(target))
                        {
                            string generic = shape.Generic ? "[T]" : "";
                            // What the target names: the program's class, nothing, the alias itself, or the next alias of a ring of two.
                            string named = target switch
                            {
                                ScopeAliasTarget.Declared => shape.Declared,
                                ScopeAliasTarget.Missing => "nosuch",
                                _ when order == Harness.Order.Self => "K0" + generic,
                                _ => "K1",
                            };
                            var aliases = new List<string> { $"type K0{generic} = " + shape.Target.Replace("{N}", named, StringComparison.Ordinal) };
                            if (target == ScopeAliasTarget.Itself && order == Harness.Order.Cycle)
                                aliases.Add("type K1 = K0" + (shape.Generic ? "[int]" : ""));
                            var lines = new List<string>();
                            if (shape.Import != null)
                                lines.Add($"import {shape.Import}");
                            if (target == ScopeAliasTarget.Declared && order == Harness.Order.Earlier)
                                lines.Add(AliasTargetClass);
                            // Every alias of the site stands at the site's own indentation.
                            var slot = site.Program.Split('\n').First(l => l.Contains("{alias}", StringComparison.Ordinal));
                            var indent = slot[..slot.IndexOf("{alias}", StringComparison.Ordinal)];
                            lines.Add(site.Program
                                .Replace("{alias}", string.Join("\n" + indent, aliases), StringComparison.Ordinal)
                                .Replace("{use}", use == ScopeAliasUse.Used ? site.Use.Replace("{K}", shape.Generic ? "K0[int]" : "K0", StringComparison.Ordinal) : site.NoUse, StringComparison.Ordinal));
                            if (target == ScopeAliasTarget.Declared && order == Harness.Order.Later)
                                lines.Add(AliasTargetClass);
                            lines.Add(Main + "    print(1)");
                            yield return Draft(Key("read", "alias-target", target, use, order, site.Label, shape.Label), Family.Read, Lines(lines), "1",
                                new[] { "SPY0202" }, twin: false, target: BindingKind.ModuleTypeAlias, position: ReadPosition.TypeAliasTarget, stdlib: shape.Import != null);
                        }
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // The pool: every case, with the tag and rule its one row of the table gives it
    // ------------------------------------------------------------------------------------------- //

    private static readonly Lazy<IReadOnlyList<Case>> Drafts = new(() =>
        NoInitializerCases().Concat(TypedReadCases()).Concat(CalleeCases()).Concat(ConstantKindCases()).Concat(InitOverloadCases()).Concat(DataclassInitCases())
            .Concat(WrittenInitCases()).Concat(DataclassMemberCases()).Concat(LambdaDefaultCases()).Concat(AliasTargetCases()).ToList());

    /// <summary>The cells as generated, before the table is read: what <see cref="Harness.RuleTablePools"/> counts a row's cells over.</summary>
    internal static IReadOnlyList<NameCell> DraftCells() => Drafts.Value.Select(c => c.Cell).ToList();

    private static readonly Lazy<IReadOnlyList<Case>> Pool = new(() => Drafts.Value.Select(c =>
    {
        var rows = Harness.RowsOf(Harness.RuleTable(), c.Cell);
        return rows.Count == 1 && rows[0].Tag is { } tag ? c with { Cell = c.Cell with { Tag = tag, Rule = rows[0].Rule } } : c;
    }).ToList());

    internal static IReadOnlyList<Case> GeneratedCases() => Pool.Value;

    /// <summary>The pool's totality: ids are unique, every cell has its one row and took its verdict from it, a cell that
    /// must run says what it prints, and a twin is a program that runs.</summary>
    internal static List<string> TotalityFailures(IReadOnlyList<Case> pool)
    {
        var failures = new List<string>();
        foreach (var dup in pool.GroupBy(c => c.Cell.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
            failures.Add($"duplicate cell id {dup.Key}");
        var rows = Harness.RuleTable();
        foreach (var c in pool)
        {
            var matched = Harness.RowsOf(rows, c.Cell);
            if (matched.Count != 1)
                failures.Add($"RuleTable: {c.Cell.Id} matches {(matched.Count == 0 ? "no row" : "rows L" + string.Join(", L", matched.Select(r => r.Line)))}");
            else if (matched[0].Tag != c.Cell.Tag || matched[0].Rule != c.Cell.Rule)
                failures.Add($"RuleTable: {c.Cell.Id} carries {c.Cell.Tag} under '{c.Cell.Rule}', and its row L{matched[0].Line} says {matched[0].Tag?.ToString() ?? "*"} under {matched[0].Rule}");
            if (c.Cell.Tag is not (SharpyTag.MustRun or SharpyTag.MustRefuse))
                failures.Add($"{c.Cell.Id}: a cell of this pool must run or must be refused, and its row says {c.Cell.Tag}");
            if (c.Cell.Tag == SharpyTag.MustRun && c.Cell.Model == null)
                failures.Add($"{c.Cell.Id}: its row says must-run and the generator gives it nothing to print");
            if (c.Twin && c.Cell.Tag != SharpyTag.MustRun)
                failures.Add($"{c.Cell.Id}: a twin is python running the same program, and the cell does not run");
            // A must-refuse cell that admits no code can only ever be judged a false refusal: no refusal could turn it green,
            // and nothing would say which refusal is the rule's (the review of PR #2767, F6).
            if (c.Cell.Tag == SharpyTag.MustRefuse && c.Cell.SeedRefusalCodes is not { Count: > 0 })
                failures.Add($"{c.Cell.Id}: its row says must-refuse under {c.Cell.Rule}, and the generator names no code its refusal may carry");
            if (c.Cell.Id.Any(char.IsWhiteSpace))
                failures.Add($"'{c.Cell.Id}': an id is one word, since an allowlist row is `<id> <class> # <cite>`");
        }
        failures.AddRange(Harness.RuleTableRowFailures(rows, Harness.RuleTablePools.SelectMany(p => p.Cells()).ToList()));
        return failures.Count > 60 ? failures.Take(60).Append($"… {failures.Count} failures in all").ToList() : failures;
    }

    [Fact]
    public void RuleTable_EveryCaseHasOneRow_AndTakesItsVerdictFromIt()
    {
        var failures = TotalityFailures(GeneratedCases());
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // ------------------------------------------------------------------------------------------- //
    // Judge, route, ratchet
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The issue whose fix turns a red cell of a route green: #2432 for a constant position's verdict, #2423 for the rest
    /// (R-QI's cells, the untyped forward read, the callee lookup, a constructor's overload set, a later dataclass's synthesized
    /// <c>__init__</c> and its other generated members, a later class's written <c>__init__</c>, a lambda's default, the alias
    /// refusal: charter §3.20l). A typed read that is
    /// red at the EARLIER order is no order defect, and it and its later twin are #2417's; a read of an <c>__init__</c> or of
    /// a generated member (<see cref="OrderFreeRoutes"/>) that is red with the type declared first is no order defect either,
    /// and it and the cells it is the control of are no row's (<see cref="NoOrderAtFault"/>); an alias that reaches itself is
    /// #2769's; in a constant position, N2's cells are #2423's, as is a bare sibling read red at the later order alone, a
    /// member that dies of an instance receiver is #2147's, and a method group refused where its slot's function type selects
    /// an overload is #2277's (<see cref="ProposedCite"/>).</summary>
    internal static readonly IReadOnlyDictionary<string, string> RouteIssues = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["no-initializer"] = "#2423",
        ["typed-read"] = "#2423",
        ["callee"] = "#2423",
        ["constant-kind"] = "#2432",
        ["init-overload"] = "#2423",
        [DataclassInitRoute] = "#2423",
        [WrittenInitRoute] = "#2423",
        [DataclassMemberRoute] = "#2423",
        ["lambda-default"] = "#2423",
        ["alias-target"] = "#2423",
    };

    /// <summary>The class of an untyped member-access read (#2417): typed at neither order.</summary>
    internal const string UntypedAtEitherOrder = "#2417";

    /// <summary>The class of an alias whose target reaches the alias itself (#2769): unruled, so the unused cells keep running
    /// and a red one is that tracker's.</summary>
    internal const string SelfReachingAlias = "#2769";

    internal const string DataclassInitRoute = "dataclass-init";
    internal const string WrittenInitRoute = "written-init";
    internal const string DataclassMemberRoute = "dataclass-member";

    /// <summary>The routes whose one rule is the order-free one (N1; D2's predicate): a cell does what its control does, the
    /// same read with the type declared first.</summary>
    internal static readonly IReadOnlySet<string> OrderFreeRoutes = new HashSet<string>(StringComparer.Ordinal) { DataclassInitRoute, WrittenInitRoute, DataclassMemberRoute };

    /// <summary>What a cell of <see cref="OrderFreeRoutes"/> red at the EARLIER order is routed to, and with it every cell it
    /// is the control of (the same read at another order): no issue. With the type above its reader no order is at fault, so
    /// the red is another class's, and no row can hold it under #2423 (an allowlist row cites an issue): the generator owes the
    /// value an exclusion with its reason and probe first.</summary>
    internal const string NoOrderAtFault = "UNROUTED";

    /// <summary>The id of the control of a cell of <see cref="OrderFreeRoutes"/>, the same read with the type declared first, or
    /// null for a cell that is its own control or is no such cell.</summary>
    internal static string? InitControlId(NameCell cell)
    {
        if (cell.Key is not { Route: { } route } key || !OrderFreeRoutes.Contains(route) || key.Part<Harness.Order>() is not { } order || order == Harness.Order.Earlier)
            return null;
        return Key(key.Parts.Select(p => p is Harness.Order ? Harness.Order.Earlier : p).ToArray()).Text;
    }

    /// <summary>The class of a bare sibling read at-or-after its member under a module binding of the name (N2): the class-body
    /// rule's refusal is #2423's whatever the position, a constant one included.</summary>
    internal const string ClassBodyRule = "#2423";

    /// <summary>The class of a constant or static member read through an instance (#2147): C# refuses the emitted instance
    /// access (CS0176) at every position, so a constant position's cell that dies of it waits on that tracker.</summary>
    internal const string StaticThroughInstance = "#2147";

    /// <summary>The C# error that is #2147's symptom.</summary>
    internal const string StaticThroughInstanceCs = "CS0176";

    /// <summary>The class of an overloaded method group of a referenced type read where a function type stands at the slot
    /// (#2277; the owner's ruling 29, #2423 comment 7052893050): the slot's type selects the overload and the read must run,
    /// and today the checker refuses every read of the group. No constant position's fix cures it.</summary>
    internal const string OverloadByTargetType = "#2277";

    /// <summary>The refusal that is #2277's symptom there.</summary>
    internal const string MethodGroupRefusal = "SPY0336";

    internal sealed record Result(Case Case, Harness.SharpyRun Run, SharpyKind Kind, VerdictClass Class, bool WrongCode, string? PythonFailure);

    /// <summary>The harness's table, and one more red: a must-refuse cell refused under a code it does not admit.</summary>
    internal static (VerdictClass Class, bool WrongCode) Judge(NameCell cell, SharpyKind kind, Harness.SharpyRun run)
    {
        var verdict = Harness.Judge(cell, kind, run.Stdout);
        bool wrongCode = verdict == VerdictClass.Match && cell.Tag == SharpyTag.MustRefuse && Harness.RefusalReasonFailure(cell, run) != null;
        return (wrongCode ? VerdictClass.FalseRefusal : verdict, wrongCode);
    }

    /// <summary>The id of a typed-read cell's twin at the earlier order, or null for a cell that has none.</summary>
    internal static string? EarlierTwinId(NameCell cell)
    {
        if (cell.Key is not { Route: "typed-read" } key || key.Part<Harness.Order>() is not { } order)
            return null;
        return order == Harness.Order.Earlier ? cell.Id : Key(key.Parts.Select(p => p is Harness.Order ? Harness.Order.Earlier : p).ToArray()).Text;
    }

    /// <summary>The id of a bare sibling read's twin above the reader in a constant position, or null for a cell that is no
    /// such read at the later order.</summary>
    internal static string? SiblingAboveTwinId(NameCell cell)
    {
        if (cell.Key is not { Route: "constant-kind" } key || !key.Marks.Contains(BareSibling) || key.Part<Harness.Order>() != Harness.Order.Later)
            return null;
        return Key(key.Parts.Select(p => p is Harness.Order ? Harness.Order.Earlier : p).ToArray()).Text;
    }

    /// <summary>The cite a red cell's row must carry. <paramref name="redIds"/> are the ids red in the same run: a typed read
    /// whose earlier twin is red is untyped whatever the order, so declaring before reading does not cure it and it is #2417's.</summary>
    internal static string ProposedCite(Result r, IReadOnlySet<string> redIds)
    {
        var issue = RouteIssues.GetValueOrDefault(r.Case.Cell.Key!.Route ?? "", "UNROUTED");
        if (EarlierTwinId(r.Case.Cell) is { } earlier && redIds.Contains(earlier))
            issue = UntypedAtEitherOrder;
        if (r.Case.Cell.Key.Part<ScopeAliasTarget>() == ScopeAliasTarget.Itself)
            issue = SelfReachingAlias;
        // A type above its reader: the `__init__` or the generated member is there to read, so the red is not the order's.
        // Nor is the red of a cell whose control is red: the same read fails with the type declared first.
        if (OrderFreeRoutes.Contains(r.Case.Cell.Key.Route ?? "")
            && (r.Case.Cell.Key.Part<Harness.Order>() == Harness.Order.Earlier || (InitControlId(r.Case.Cell) is { } control && redIds.Contains(control))))
            issue = NoOrderAtFault;
        // N2's refusal, and a sibling read that is red BELOW its member alone (above it the same position gives the same
        // member its verdict): the class-body rule's order, whatever the position.
        if (r.Case.Cell.Key.Route == "constant-kind" && r.Case.Cell.Rule == "N2")
            issue = ClassBodyRule;
        if (SiblingAboveTwinId(r.Case.Cell) is { } above && !redIds.Contains(above))
            issue = ClassBodyRule;
        if (r.Case.Cell.Key.Route == "constant-kind" && r.Case.Cell.Key.Part<Harness.Spelling>() == Harness.Spelling.SelfDot && r.Run.CsCode == StaticThroughInstanceCs)
            issue = StaticThroughInstance;
        // A method group that must run and is refused as a group: no overload was selected, whatever the position.
        if (r.Case.Cell.Key.Part<ScopeConstantKind>() == ScopeConstantKind.ClrTypeMethodGroup && r.Case.Cell.Tag == SharpyTag.MustRun && r.Run.ErrorCodes.Contains(MethodGroupRefusal))
            issue = OverloadByTargetType;
        string lane = r.WrongCode ? "wrong-code" : r.Class switch
        {
            VerdictClass.Ice => "ice",
            VerdictClass.FalseRefusal => "false-refusal",
            VerdictClass.RuntimeThrow => "runtime-throw",
            VerdictClass.SilentWrong when r.Case.Cell.Tag == SharpyTag.MustRefuse => "missing-refusal",
            _ => "silent-wrong",
        };
        return $"{issue} {lane}";
    }

    private static bool IsRed(VerdictClass c) => c is VerdictClass.Ice or VerdictClass.FalseRefusal or VerdictClass.SilentWrong or VerdictClass.RuntimeThrow;

    /// <summary>The ratchet: a red cell has its row, a row has its red cell of that class and cite, and python or the harness
    /// failing to judge a cell is never a row's to hold.</summary>
    internal static List<string> RatchetFailures(IReadOnlyList<Result> results, IReadOnlyDictionary<string, InitializerDependencyDifferentialTests.AllowRow> rows)
    {
        var failures = new List<string>();
        var redIds = results.Where(r => IsRed(r.Class)).Select(r => r.Case.Cell.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var r in results)
        {
            rows.TryGetValue(r.Case.Cell.Id, out var row);
            if (r.PythonFailure != null)
                failures.Add($"GeneratorBug {r.Case.Cell.Id}: {r.PythonFailure}{(row == null ? "" : " (and a row lists it: not allowlistable)")}");
            else if (r.Class == VerdictClass.Harness)
                failures.Add($"Harness {r.Case.Cell.Id}: Sharpy {Describe(r)}");
            else if (IsRed(r.Class) && row == null)
                failures.Add($"{r.Class} {r.Case.Cell.Id} ({r.Case.Cell.Tag}, {r.Case.Cell.Rule}): Sharpy {Describe(r)}; model '{r.Case.Cell.Model}'");
            else if (IsRed(r.Class) && row!.Class != r.Class)
                failures.Add($"{r.Case.Cell.Id}: listed as {row.Class}, now {r.Class}: MOVED, update or delete the row");
            else if (!IsRed(r.Class) && row != null)
                failures.Add($"{r.Case.Cell.Id}: listed as {row.Class}, now {r.Class}: STALE, delete the row");
            else if (IsRed(r.Class) && row!.Cite != ProposedCite(r, redIds))
                failures.Add($"{r.Case.Cell.Id}: the row cites '{row.Cite}' and the route says '{ProposedCite(r, redIds)}'");
        }
        return failures;
    }

    private static string Describe(Result r)
        => r.Kind == SharpyKind.Ran
            ? $"Ran '{Harness.Normalize(r.Run.Stdout)}'"
            : $"{r.Kind} [{string.Join(",", r.Run.AllCodes.Distinct())}{(r.Run.CsCode != null ? " " + r.Run.CsCode : "")}]"
                + (r.Kind == SharpyKind.RuntimeThrow ? $" stderr '{r.Run.Stderr.TrimStart().Split('\n', 2)[0].TrimEnd()}'" : "");

    private bool _currentCellNeedsStdlib;

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
    {
        if (_currentCellNeedsStdlib)
            yield return SharpyStdlibReference.Location;
    }

    private Harness.SharpyRun RunSharpy(string source, bool needsStdlib, int timeoutMs)
    {
        _currentCellNeedsStdlib = needsStdlib;
        var r = CompileAndExecute(source, "scope_meaning.spy", executionTimeoutMs: timeoutMs);
        var diagnostics = r.RawDiagnostics;
        var all = diagnostics.Select(d => d.Code).Where(c => c != null).Select(c => c!).ToList();
        var errors = diagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).Select(d => d.Code).Where(c => c != null).Select(c => c!).ToList();
        var cs = diagnostics.Where(d => d.Code == "SPY0908").Select(d => Regex.Match(d.Message, @"\bCS\d{4}\b")).Where(m => m.Success).Select(m => m.Value).FirstOrDefault();
        return new Harness.SharpyRun(r.Success, r.ExitCode, r.Exception != null, r.TimedOut, r.StandardOutput, r.StandardError, errors, all, cs);
    }

    [SkippableFact]
    public void ScopeMeaning_Sweep()
    {
        // The oracle is the pinned CPython (`.python-version`, 3.15): an older minor is no verdict, so it skips where it would answer.
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 15));
        var wall = Stopwatch.StartNew();
        var pool = GeneratedCases();
        var failures = TotalityFailures(pool);

        // --- Python arm: one batch over the cells python runs with the same meaning. ---
        var requests = new List<PyRequest>();
        var twinFailures = new Dictionary<int, string>();
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].Twin)
                continue;
            try
            {
                requests.Add(new PyRequest(i, TwinRenderer.Render(pool[i].Cell.Source)));
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
            {
                twinFailures[i] = $"renderer: {ex.Message}";
            }
        }
        var pythonById = oracle.RunBatch(requests, PythonBatchTimeoutMs);

        // --- Sharpy arm: production compile + execute, `run` and never `emit`. ---
        var results = new List<Result>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            var c = pool[i];
            string? pythonFailure = twinFailures.GetValueOrDefault(i);
            if (pythonFailure == null && c.Twin)
            {
                var python = pythonById.TryGetValue(i, out var verdict) ? Harness.ClassifyPython(verdict) : new PyOutcome(PyKind.SyntaxError, "no verdict from the runner");
                if (python != c.Cell.Python)
                    pythonFailure = $"the cell is modelled to print '{c.Cell.Model}' and python {python}";
            }
            var run = RunSharpy(c.Cell.Source, c.Cell.NeedsStdlib, SharpyExecTimeoutMs);
            if (run.TimedOut)
                run = RunSharpy(c.Cell.Source, c.Cell.NeedsStdlib, SharpyExecTimeoutMs * 2);
            var kind = Harness.ClassifySharpy(run);
            var (cls, wrongCode) = Judge(c.Cell, kind, run);
            results.Add(new Result(c, run, kind, cls, wrongCode, pythonFailure));
        }

        // --- Ratchet. ---
        var universe = pool.Select(c => c.Cell.Id).ToHashSet(StringComparer.Ordinal);
        var path = PythonInterpreter.FindRepoFile("src", "Sharpy.Compiler.Tests", "Conformance", AllowlistFileName);
        var rows = InitializerDependencyDifferentialTests.ParseAllowlist(path == null ? Array.Empty<string>() : File.ReadAllLines(path), universe, failures);
        failures.AddRange(RatchetFailures(results, rows));

        wall.Stop();
        var red = results.Where(r => IsRed(r.Class)).ToList();
        var redIds = red.Select(r => r.Case.Cell.Id).ToHashSet(StringComparer.Ordinal);
        var dir = ReportDirectory();
        using (var writer = new StreamWriter(Path.Combine(dir, "scope-meaning-differential-cells.jsonl")))
        {
            foreach (var r in results)
            {
                writer.WriteLine(JsonSerializer.Serialize(new
                {
                    id = r.Case.Cell.Id,
                    tag = r.Case.Cell.Tag.ToString(),
                    rule = r.Case.Cell.Rule,
                    verdict = r.Class.ToString(),
                    wrongCode = r.WrongCode,
                    twin = r.Case.Twin,
                    python = r.PythonFailure,
                    sharpy = Describe(r),
                    source = r.Case.Cell.Source,
                }));
            }
        }
        File.WriteAllLines(
            Path.Combine(dir, "scope-meaning-differential-proposed-allowlist.txt"),
            red.OrderBy(r => r.Case.Cell.Id, StringComparer.Ordinal).Select(r => $"{r.Case.Cell.Id} {r.Class} # {ProposedCite(r, redIds)}"));

        var report = $"scope-meaning-differential: pool {pool.Count} ({pool.Count(c => c.Cell.Tag == SharpyTag.MustRun)} must-run, {pool.Count(c => c.Cell.Tag == SharpyTag.MustRefuse)} must-refuse), "
            + $"python agreed {results.Count(r => r.Case.Twin && r.PythonFailure == null)}/{pool.Count(c => c.Twin)}, red {red.Count}, allowlist {rows.Count}, wall {wall.Elapsed.TotalSeconds:F1}s. "
            + string.Join(" ", results.GroupBy(r => r.Case.Cell.Key!.Route).Select(g => $"{g.Key}={g.Count()}/red {g.Count(r => IsRed(r.Class))}"))
            + " cites: " + string.Join(" ", rows.Values.GroupBy(r => r.Cite.Split(' ')[0]).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key}={g.Count()}"));
        Output.WriteLine(report);
        Assert.True(failures.Count == 0, $"{failures.Count} failures. {report}\n{string.Join("\n", failures.Take(120))}");
    }

    private static string ReportDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
            dir = dir.Parent;
        var tmp = Path.Combine(dir?.FullName ?? Path.GetTempPath(), ".claude", "tmp");
        Directory.CreateDirectory(tmp);
        return tmp;
    }

    // ------------------------------------------------------------------------------------------- //
    // The ratchet's own fact: plain data in, no compile
    // ------------------------------------------------------------------------------------------- //

    [Fact]
    public void Ratchet_ARedCell_NeedsItsRow_AndARowNeedsItsRedCell()
    {
        var pool = GeneratedCases();
        var running = pool.First(c => c.Cell.Tag == SharpyTag.MustRun && c.Cell.Key!.Route == "callee");
        var later = pool.First(c => c.Cell.Tag == SharpyTag.MustRefuse && c.Cell.Key!.Route == "typed-read" && c.Cell.Key.Part<Harness.Order>() == Harness.Order.Later);
        var earlier = pool.Single(c => c.Cell.Id == EarlierTwinId(later.Cell));
        static Harness.SharpyRun Ran(string stdout) => new(true, 0, false, false, stdout, "", Array.Empty<string>(), Array.Empty<string>(), null);
        static Harness.SharpyRun Refused(string code) => new(false, null, false, false, "", "", new[] { code }, new[] { code }, null);
        Result Of(Case c, Harness.SharpyRun run, string? pythonFailure = null)
        {
            var kind = Harness.ClassifySharpy(run);
            var (cls, wrongCode) = Judge(c.Cell, kind, run);
            return new Result(c, run, kind, cls, wrongCode, pythonFailure);
        }
        static Dictionary<string, InitializerDependencyDifferentialTests.AllowRow> Rows(params (Case Case, VerdictClass Class, string Cite)[] rows)
            => rows.ToDictionary(r => r.Case.Cell.Id, r => new InitializerDependencyDifferentialTests.AllowRow(r.Class, r.Cite), StringComparer.Ordinal);
        var none = Rows();
        var listed = Rows((running, VerdictClass.SilentWrong, "#2423 silent-wrong"));
        var wrong = Rows((later, VerdictClass.FalseRefusal, "#2423 wrong-code"));

        Assert.Empty(RatchetFailures(new[] { Of(running, Ran("1")) }, none));
        Assert.Empty(RatchetFailures(new[] { Of(running, Ran("12")) }, listed));
        Assert.Empty(RatchetFailures(new[] { Of(later, Refused("SPY0220")) }, none));
        Assert.Empty(RatchetFailures(new[] { Of(later, Refused("SPY0200")) }, wrong));

        Assert.Contains("model", Assert.Single(RatchetFailures(new[] { Of(running, Ran("12")) }, none)));
        Assert.Contains("STALE", Assert.Single(RatchetFailures(new[] { Of(running, Ran("1")) }, listed)));
        Assert.Contains("MOVED", Assert.Single(RatchetFailures(new[] { Of(running, Refused("SPY0200")) }, listed)));
        Assert.Contains("FalseRefusal", Assert.Single(RatchetFailures(new[] { Of(later, Refused("SPY0200")) }, none)));
        Assert.Contains("STALE", Assert.Single(RatchetFailures(new[] { Of(later, Refused("SPY0220")) }, wrong)));
        Assert.Contains("the route says", Assert.Single(RatchetFailures(new[] { Of(running, Ran("12")) }, Rows((running, VerdictClass.SilentWrong, "#2432 silent-wrong")))));
        Assert.Contains("GeneratorBug", Assert.Single(RatchetFailures(new[] { Of(running, Ran("1"), "python 4") }, none)));

        // By class (contract §10): a typed read red at BOTH orders is no order defect, so both rows are #2417's; red at the
        // later order alone it is #2423's, and a row citing the other issue is refused.
        var both = new[] { Of(earlier, Refused("SPY0200")), Of(later, Refused("SPY0200")) };
        Assert.Empty(RatchetFailures(both, Rows((earlier, VerdictClass.FalseRefusal, "#2417 wrong-code"), (later, VerdictClass.FalseRefusal, "#2417 wrong-code"))));
        Assert.Contains("the route says '#2417", Assert.Single(RatchetFailures(both, Rows((earlier, VerdictClass.FalseRefusal, "#2417 wrong-code"), (later, VerdictClass.FalseRefusal, "#2423 wrong-code")))));
        var laterOnly = new[] { Of(earlier, Refused("SPY0220")), Of(later, Refused("SPY0200")) };
        Assert.Contains("the route says '#2423", Assert.Single(RatchetFailures(laterOnly, Rows((later, VerdictClass.FalseRefusal, "#2417 wrong-code")))));

        // An alias whose target reaches the alias itself is unruled (#2769): its unused cell must run, and one that stops
        // running is that tracker's row, never the alias refusal's (#2423).
        var itself = pool.First(c => c.Cell.Key!.Part<ScopeAliasTarget>() == ScopeAliasTarget.Itself);
        Assert.Equal(SharpyTag.MustRun, itself.Cell.Tag);
        var refusedItself = new[] { Of(itself, Refused("SPY0202")) };
        Assert.Empty(RatchetFailures(refusedItself, Rows((itself, VerdictClass.FalseRefusal, "#2769 false-refusal"))));
        Assert.Contains("the route says '#2769", Assert.Single(RatchetFailures(refusedItself, Rows((itself, VerdictClass.FalseRefusal, "#2423 false-refusal")))));

        // A dataclass's synthesized `__init__` read from above the dataclass is the order's cell (#2423); red with the dataclass
        // ABOVE its reader it is no order's, and nothing routes it, so no row holds it.
        var initLater = pool.First(c => c.Cell.Key!.Route == "dataclass-init" && c.Cell.Tag == SharpyTag.MustRun && c.Cell.Key.Part<Harness.Order>() == Harness.Order.Later);
        var initEarlier = pool.Single(c => c.Cell.Id == Key(initLater.Cell.Key!.Parts.Select(p => p is Harness.Order ? Harness.Order.Earlier : p).ToArray()).Text);
        Assert.Equal(SharpyTag.MustRun, initEarlier.Cell.Tag);
        Assert.Empty(RatchetFailures(new[] { Of(initLater, Refused("SPY0342")) }, Rows((initLater, VerdictClass.FalseRefusal, "#2423 false-refusal"))));
        Assert.Contains($"the route says '{NoOrderAtFault}", Assert.Single(RatchetFailures(new[] { Of(initEarlier, Refused("SPY0342")) }, Rows((initEarlier, VerdictClass.FalseRefusal, "#2423 false-refusal")))));
        // Red at both orders, the later cell is no order's either: its control fails with the dataclass declared first.
        var initBoth = new[] { Of(initEarlier, Refused("SPY0342")), Of(initLater, Refused("SPY0342")) };
        Assert.Equal(2, RatchetFailures(initBoth, Rows((initEarlier, VerdictClass.FalseRefusal, "#2423 false-refusal"), (initLater, VerdictClass.FalseRefusal, "#2423 false-refusal"))).Count(f => f.Contains($"the route says '{NoOrderAtFault}", StringComparison.Ordinal)));
        // A ring of two dataclasses (ruling 35): red where one reads the other above its declaration, with a control that
        // runs, it is the order's cell; with a red control it is no row's.
        var initRing = pool.First(c => c.Cell.Key!.Route == "dataclass-init" && c.Cell.Tag == SharpyTag.MustRun && c.Cell.Key.Part<Harness.Order>() == Harness.Order.Cycle);
        var initRingControl = pool.Single(c => c.Cell.Id == InitControlId(initRing.Cell));
        Assert.Equal(Harness.Order.Earlier, initRingControl.Cell.Key!.Part<Harness.Order>());
        Assert.Null(InitControlId(initRingControl.Cell));
        Assert.Empty(RatchetFailures(new[] { Of(initRingControl, Ran(initRingControl.Cell.Model!)), Of(initRing, Refused("SPY0342")) }, Rows((initRing, VerdictClass.FalseRefusal, "#2423 false-refusal"))));
        var initRingBoth = new[] { Of(initRingControl, Refused("SPY0342")), Of(initRing, Refused("SPY0342")) };
        Assert.Equal(2, RatchetFailures(initRingBoth, Rows((initRingControl, VerdictClass.FalseRefusal, "#2423 false-refusal"), (initRing, VerdictClass.FalseRefusal, "#2423 false-refusal"))).Count(f => f.Contains($"the route says '{NoOrderAtFault}", StringComparison.Ordinal)));
        var initMisfit = pool.First(c => c.Cell.Key!.Route == "dataclass-init" && c.Cell.Tag == SharpyTag.MustRefuse && c.Cell.Key.Part<Harness.Order>() == Harness.Order.Later);
        Assert.Empty(RatchetFailures(new[] { Of(initMisfit, Refused(initMisfit.Cell.SeedRefusalCodes!.Single())) }, none));
        Assert.Empty(RatchetFailures(new[] { Of(initMisfit, Ice("CS7036")) }, Rows((initMisfit, VerdictClass.Ice, "#2423 ice"))));
        Assert.Empty(RatchetFailures(new[] { Of(initMisfit, Refused("SPY0203")) }, Rows((initMisfit, VerdictClass.FalseRefusal, "#2423 wrong-code"))));
        // The sibling sets on the same rule (rulings 36 and 37): a written `__init__` and a generated member read from above
        // the type are the order's cells while the control passes, and no row's once it is red.
        foreach (var route in new[] { WrittenInitRoute, DataclassMemberRoute })
        {
            var above = pool.First(c => c.Cell.Key!.Route == route && c.Cell.Tag == SharpyTag.MustRefuse && c.Cell.Key.Part<Harness.Order>() == Harness.Order.Later);
            var control = pool.Single(c => c.Cell.Id == InitControlId(above.Cell));
            Assert.Equal(Harness.Order.Earlier, control.Cell.Key!.Part<Harness.Order>());
            string code = above.Cell.SeedRefusalCodes!.Single();
            Assert.Empty(RatchetFailures(new[] { Of(control, Refused(code)), Of(above, Ice("CS1503")) }, Rows((above, VerdictClass.Ice, "#2423 ice"))));
            Assert.Equal(2, RatchetFailures(new[] { Of(control, Ice("CS1503")), Of(above, Ice("CS1503")) }, Rows((control, VerdictClass.Ice, "#2423 ice"), (above, VerdictClass.Ice, "#2423 ice")))
                .Count(f => f.Contains($"the route says '{NoOrderAtFault}", StringComparison.Ordinal)));
        }

        // A lambda's default that does not read the member is the class-body rule's cell (#2423), never a constant position's.
        var lambdaDefault = pool.First(c => c.Cell.Key!.Route == "lambda-default" && c.Cell.Key.Part<ScopeDefaultOwner>() == ScopeDefaultOwner.Lambda && c.Cell.Key.Part<Harness.Order>() == Harness.Order.Earlier);
        Assert.Equal(SharpyTag.MustRun, lambdaDefault.Cell.Tag);
        Assert.Empty(RatchetFailures(new[] { Of(lambdaDefault, Refused("SPY0200")) }, Rows((lambdaDefault, VerdictClass.FalseRefusal, "#2423 false-refusal"))));
        Assert.Contains("the route says '#2423", Assert.Single(RatchetFailures(new[] { Of(lambdaDefault, Refused("SPY0200")) }, Rows((lambdaDefault, VerdictClass.FalseRefusal, "#2432 false-refusal")))));

        // A constant position's cell is #2432's, with three classes that are another tracker's whatever the position.
        static Harness.SharpyRun Ice(string cs) => new(false, null, false, false, "", "", new[] { "SPY0908" }, new[] { "SPY0908" }, cs);
        bool Constant(Case c) => c.Cell.Key!.Route == "constant-kind";
        var member = pool.First(c => Constant(c) && c.Cell.Tag == SharpyTag.MustRun && c.Cell.Key!.Part<Harness.Spelling>() == Harness.Spelling.Qualified);
        Assert.Contains("the route says '#2432", Assert.Single(RatchetFailures(new[] { Of(member, Ice("CS0133")) }, Rows((member, VerdictClass.Ice, "#2423 ice")))));
        // N2: a bare sibling at-or-after its reader under a module binding of the name is the class-body rule's refusal (#2423).
        // It is so whatever its twin above the member does: red there too, the twin is the position's own and N2 is still N2.
        var shadowed = pool.First(c => Constant(c) && c.Cell.Rule == "N2");
        var shadowedAbove = pool.Single(c => c.Cell.Id == SiblingAboveTwinId(shadowed.Cell));
        Assert.Equal(SharpyTag.MustRefuse, shadowed.Cell.Tag);
        var shadowedBoth = new[] { Of(shadowedAbove, Ice("CS0133")), Of(shadowed, Ran("12")) };
        Assert.Empty(RatchetFailures(shadowedBoth, Rows((shadowedAbove, VerdictClass.Ice, "#2432 ice"), (shadowed, VerdictClass.SilentWrong, "#2423 missing-refusal"))));
        Assert.Contains("the route says '#2423", Assert.Single(RatchetFailures(shadowedBoth, Rows((shadowedAbove, VerdictClass.Ice, "#2432 ice"), (shadowed, VerdictClass.SilentWrong, "#2432 missing-refusal")))));
        // A member read through `self` that dies of CS0176 is #2147's; dying of anything else it is the position's own.
        var throughSelf = pool.First(c => Constant(c) && c.Cell.Tag == SharpyTag.MustRun && c.Cell.Key!.Part<Harness.Spelling>() == Harness.Spelling.SelfDot);
        Assert.Empty(RatchetFailures(new[] { Of(throughSelf, Ice(StaticThroughInstanceCs)) }, Rows((throughSelf, VerdictClass.Ice, "#2147 ice"))));
        Assert.Contains("the route says '#2147", Assert.Single(RatchetFailures(new[] { Of(throughSelf, Ice(StaticThroughInstanceCs)) }, Rows((throughSelf, VerdictClass.Ice, "#2432 ice")))));
        Assert.Contains("the route says '#2432", Assert.Single(RatchetFailures(new[] { Of(throughSelf, Ice("CS1736")) }, Rows((throughSelf, VerdictClass.Ice, "#2147 ice")))));
        // A method group that must run (a function type stands at its slot) and is refused as a group is #2277's; red any other
        // way it is the position's own.
        var group = pool.First(c => Constant(c) && c.Cell.Tag == SharpyTag.MustRun && c.Cell.Key!.Part<ScopeConstantKind>() == ScopeConstantKind.ClrTypeMethodGroup);
        Assert.Empty(RatchetFailures(new[] { Of(group, Refused(MethodGroupRefusal)) }, Rows((group, VerdictClass.FalseRefusal, "#2277 false-refusal"))));
        Assert.Contains("the route says '#2277", Assert.Single(RatchetFailures(new[] { Of(group, Refused(MethodGroupRefusal)) }, Rows((group, VerdictClass.FalseRefusal, "#2432 false-refusal")))));
        Assert.Contains("the route says '#2432", Assert.Single(RatchetFailures(new[] { Of(group, Ice("CS1736")) }, Rows((group, VerdictClass.Ice, "#2277 ice")))));
        // A bare sibling read red BELOW its member alone is the class-body rule's order (#2423); red above it too, no order
        // cures it and it is the position's own (#2432).
        var below = pool.First(c => Constant(c) && c.Cell.Tag == SharpyTag.MustRun && c.Cell.Rule != "N2" && SiblingAboveTwinId(c.Cell) != null);
        var aboveTwin = pool.Single(c => c.Cell.Id == SiblingAboveTwinId(below.Cell));
        Assert.Equal(SharpyTag.MustRun, aboveTwin.Cell.Tag);
        var belowOnly = new[] { Of(aboveTwin, Ran(aboveTwin.Cell.Model)), Of(below, Refused("SPY0200")) };
        Assert.Empty(RatchetFailures(belowOnly, Rows((below, VerdictClass.FalseRefusal, "#2423 false-refusal"))));
        Assert.Contains("the route says '#2423", Assert.Single(RatchetFailures(belowOnly, Rows((below, VerdictClass.FalseRefusal, "#2432 false-refusal")))));
        var atBoth = new[] { Of(aboveTwin, Refused("SPY0425")), Of(below, Refused("SPY0425")) };
        Assert.Empty(RatchetFailures(atBoth, Rows((aboveTwin, VerdictClass.FalseRefusal, "#2432 false-refusal"), (below, VerdictClass.FalseRefusal, "#2432 false-refusal"))));
        Assert.Contains("the route says '#2432", Assert.Single(RatchetFailures(atBoth, Rows((aboveTwin, VerdictClass.FalseRefusal, "#2432 false-refusal"), (below, VerdictClass.FalseRefusal, "#2423 false-refusal")))));
    }
}
