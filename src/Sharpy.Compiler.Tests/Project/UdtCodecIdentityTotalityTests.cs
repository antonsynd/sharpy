using System.Collections;
using System.Reflection;
using FluentAssertions;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// A <see cref="UserDefinedType"/> restored from the incremental cache carries the identity a cold
/// build gives it (#2027): wherever it sits inside a cached signature type, it comes back out of
/// serialize → decode → <see cref="RestoredTypeRelinker"/> bound to its symbol.
///
/// <para>Two censuses hold the class, not the cells. Every concrete <see cref="SemanticType"/> is
/// classified LEAF or COMPOSITE here; a composite brings one specimen per slot that can hold a type,
/// so a new composite record — or a slot the walker forgets — fails loudly instead of leaving a
/// cached type symbol-less. The UDTs in a relinked result are found by REFLECTION over the result's
/// SemanticType-typed members, independently of <see cref="SemanticTypeWalker"/>, so a slot the
/// walker skips shows up as an unbound type rather than as nothing.</para>
///
/// <para>Positive controls: decode alone leaves every symbol null (the relink is what binds), and
/// the pre-#2027 payload — the bare name — relinks to nothing.</para>
/// </summary>
public class UdtCodecIdentityTotalityTests
{
    private const string LibPath = "/proj/lib.spy";

    /// <summary>A project type declared in lib.spy.</summary>
    private static readonly TypeSymbol UserClass = new()
    {
        Name = "Point",
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Class,
        DefiningFilePath = LibPath,
        DeclaringFilePath = LibPath,
    };

    /// <summary>A registry-shaped type: CLR-backed, no file, no module.</summary>
    private static readonly TypeSymbol RegistryType = new()
    {
        Name = "bytes",
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Struct,
        ClrType = typeof(System.Text.StringBuilder),
    };

    /// <summary>A stdlib-module-exported type.</summary>
    private static readonly TypeSymbol ModuleType = new()
    {
        Name = "date",
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Class,
        ClrType = typeof(DateTime),
        DefiningModule = "datetime",
    };

    /// <summary>An escape-declared user <c>class `bytes`</c> — a FILE origin, never the registry's.</summary>
    private static readonly TypeSymbol EscapedUserBytes = new()
    {
        Name = "bytes",
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Class,
        IsNameBacktickEscaped = true,
        DefiningFilePath = LibPath,
        DeclaringFilePath = LibPath,
    };

    private static readonly Dictionary<string, TypeSymbol> SymbolsByOrigin = new()
    {
        ["user class"] = UserClass,
        ["registry type"] = RegistryType,
        ["module type"] = ModuleType,
        ["escaped user bytes"] = EscapedUserBytes,
    };

    public static IEnumerable<object[]> Origins() => SymbolsByOrigin.Keys.Select(o => new object[] { o });

    private static UserDefinedType Udt(TypeSymbol symbol) => new() { Name = symbol.Name, Symbol = symbol };

    /// <summary>
    /// Every concrete SemanticType, classified. A COMPOSITE entry builds one specimen per type slot
    /// around a given inner type; a LEAF entry holds no type. Unclassified or ghost entries fail.
    /// </summary>
    private static readonly Dictionary<string, Func<SemanticType, IEnumerable<(string Slot, SemanticType Specimen)>>?> Census = new()
    {
        ["UnknownType"] = null,
        ["VoidType"] = null,
        ["BuiltinType"] = null,
        ["UserDefinedType"] = t => new[] { ("bare", t) },
        ["UnmappedClrType"] = null,
        ["TypeParameterType"] = null,
        ["LiteralStringType"] = null,
        // Symbol-carrying leaves with their own (documented-lossy) channels — not UDT slots.
        ["ModuleType"] = null,
        ["SelfType"] = null,
        ["ConstructorReferenceType"] = null,
        ["GenericType"] = t => new[] { ("generic arg", (SemanticType)new GenericType { Name = "list", TypeArguments = { t } }) },
        ["GenericFunctionType"] = t => new[]
        {
            ("gfunc arg", (SemanticType)new GenericFunctionType
            {
                FunctionSymbol = new FunctionSymbol { Name = "identity", Kind = SymbolKind.Function },
                TypeArguments = { t },
            }),
        },
        ["NullableType"] = t => new[] { ("nullable", (SemanticType)new NullableType { UnderlyingType = t }) },
        ["OptionalType"] = t => new[] { ("optional", (SemanticType)new OptionalType { UnderlyingType = t }) },
        ["ResultType"] = t => new[]
        {
            ("result ok", (SemanticType)new ResultType { OkType = t, ErrorType = SemanticType.Str }),
            ("result err", new ResultType { OkType = SemanticType.Int, ErrorType = t }),
        },
        ["FunctionType"] = t => new[]
        {
            ("function param", (SemanticType)new FunctionType { ParameterTypes = { SemanticType.Int, t }, ReturnType = SemanticType.Void }),
            ("function return", new FunctionType { ParameterTypes = { SemanticType.Int }, ReturnType = t }),
        },
        ["TupleType"] = t => new[] { ("tuple element", (SemanticType)new TupleType { ElementTypes = { SemanticType.Int, t } }) },
        ["UnionType"] = t => new[] { ("union case", (SemanticType)new UnionType { Name = "U", CaseTypes = { t } }) },
        ["TaskType"] = t => new[] { ("task result", (SemanticType)new TaskType { ResultType = t }) },
    };

    [Fact]
    public void EveryConcreteSemanticType_IsClassifiedLeafOrComposite()
    {
        var concrete = typeof(SemanticType).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(SemanticType).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();
        concrete.Should().NotBeEmpty();

        concrete.Where(n => !Census.ContainsKey(n)).Should().BeEmpty(
            "every concrete SemanticType must be classified: a composite that can hold a cached type "
            + "needs its slot specimens here, or a restored type inside it is never relinked");
        Census.Keys.Where(k => !concrete.Contains(k)).Should().BeEmpty("no ghost census entries");

        // A LEAF must really hold no SemanticType — else it is a composite classified away.
        var leavesWithTypeSlots = typeof(SemanticType).Assembly.GetTypes()
            .Where(t => Census.TryGetValue(t.Name, out var arms) && arms == null
                        && t.Name is not ("ModuleType" or "SelfType" or "ConstructorReferenceType"))
            .Where(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p => IsTypeSlot(p.PropertyType)))
            .Select(t => t.Name)
            .ToList();
        leavesWithTypeSlots.Should().BeEmpty("a type with a SemanticType-valued member is a composite");
    }

    public static IEnumerable<object[]> Slots()
    {
        foreach (var (_, arms) in Census)
        {
            if (arms == null)
                continue;
            foreach (var (slot, _) in arms(SemanticType.Unknown))
                yield return new object[] { slot };
        }
    }

    /// <summary>The specimen for <paramref name="slot"/> around <paramref name="inner"/>.</summary>
    private static SemanticType Specimen(string slot, SemanticType inner)
        => Census.Values.Where(a => a != null).SelectMany(a => a!(inner)).Single(s => s.Slot == slot).Specimen;

    /// <summary>
    /// Serialize → decode → relink every slot kind × origin kind: every UDT in the result is bound
    /// to the symbol the cold signature held.
    /// </summary>
    [Theory]
    [MemberData(nameof(SlotsTimesSymbols))]
    public void ACachedUdt_InEverySlot_IsRelinkedToItsSymbol(string slot, string origin)
    {
        var symbol = SymbolsByOrigin[origin];
        var restored = RoundTrip(Specimen(slot, Udt(symbol)));

        var relinked = new RestoredTypeRelinker(Resolver).Relink(restored);

        var found = UdtsIn(relinked);
        found.Should().NotBeEmpty($"the {slot} specimen holds a {origin}");
        found.Should().OnlyContain(u => ReferenceEquals(u.Symbol, symbol),
            $"a cached {origin} in slot '{slot}' must relink to its own symbol");
    }

    public static IEnumerable<object[]> SlotsTimesSymbols()
        => from s in Slots() from origin in SymbolsByOrigin.Keys select new[] { s[0], origin };

    /// <summary>Positive control: the decode itself binds nothing — the relink pass is what binds.</summary>
    [Theory]
    [MemberData(nameof(Slots))]
    public void DecodeAlone_LeavesTheSymbolUnbound(string slot)
    {
        var restored = RoundTrip(Specimen(slot, Udt(UserClass)));

        var found = UdtsIn(restored);
        found.Should().NotBeEmpty();
        found.Should().OnlyContain(u => u.Symbol == null && u.CacheOrigin != null,
            "decode is symbol-less and keeps the origin for the relink pass");
    }

    /// <summary>
    /// Positive control, the pre-#2027 codec: a payload carrying only the name decodes with no
    /// origin, and the relink binds nothing — the cell this suite would miss if origin never travelled.
    /// </summary>
    [Fact]
    public void ThePreFixPayload_BareName_RelinksToNothing()
    {
        var decoded = SymbolSerializerProbe.Decode("user:Point");

        var relinked = new RestoredTypeRelinker(Resolver).Relink(decoded);

        relinked.Should().BeOfType<UserDefinedType>().Which.Symbol.Should().BeNull();
    }

    /// <summary>
    /// A relink that cannot bind leaves the type as decoded, and re-encoding it writes the SAME
    /// origin — a failed relink is not a lossy re-save.
    /// </summary>
    [Fact]
    public void AnUnresolvableOrigin_StaysSymbolLess_AndReEncodesUnchanged()
    {
        var payload = SymbolSerializerProbe.Encode(Udt(UserClass));
        var decoded = SymbolSerializerProbe.Decode(payload);

        var relinked = new RestoredTypeRelinker(_ => null).Relink(decoded);

        relinked.Should().BeOfType<UserDefinedType>().Which.Symbol.Should().BeNull();
        SymbolSerializerProbe.Encode(relinked).Should().Be(payload);
    }

    [Theory]
    [MemberData(nameof(Origins))]
    public void TheOrigin_NamesWhereTheColdSymbolLives(string origin)
    {
        var written = CachedTypeOrigin.Of(Udt(SymbolsByOrigin[origin]));

        var expected = origin switch
        {
            "user class" or "escaped user bytes" => CachedTypeOrigin.FilePrefix + LibPath,
            "registry type" => CachedTypeOrigin.ClrPrefix + typeof(System.Text.StringBuilder).FullName,
            "module type" => CachedTypeOrigin.ModulePrefix + "datetime",
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        written.Should().Be(expected);
    }

    /// <summary>Restores through the real symbol channel: a function whose return type is the specimen.</summary>
    private static SemanticType RoundTrip(SemanticType type)
    {
        var cached = SymbolSerializer.Serialize(
            new FunctionSymbol { Name = "probe", Kind = SymbolKind.Function, ReturnType = type }, LibPath);
        var restored = (FunctionSymbol)SymbolSerializer.Deserialize(cached, new Dictionary<string, Symbol>(StringComparer.Ordinal));
        return restored.ReturnType;
    }

    /// <summary>
    /// The test's stand-in for ProjectCompiler's resolver: binds exactly the origin each specimen
    /// symbol writes. The escaped user <c>bytes</c> and the registry <c>bytes</c> share a name and
    /// are told apart ONLY by origin.
    /// </summary>
    private static TypeSymbol? Resolver(UserDefinedType udt)
        => SymbolsByOrigin.Values
            .FirstOrDefault(s => s.Name == udt.Name && CachedTypeOrigin.Of(Udt(s)) == udt.CacheOrigin);

    private static bool IsTypeSlot(Type t)
        => typeof(SemanticType).IsAssignableFrom(t)
           || (t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t)
               && t.IsGenericType && typeof(SemanticType).IsAssignableFrom(t.GetGenericArguments()[0]));

    /// <summary>Every UDT reachable through SemanticType-valued public members — by reflection, not the walker.</summary>
    private static List<UserDefinedType> UdtsIn(SemanticType type)
    {
        var found = new List<UserDefinedType>();
        void Visit(SemanticType t)
        {
            if (t is UserDefinedType udt)
            {
                found.Add(udt);
                return;
            }
            foreach (var p in t.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0 || !IsTypeSlot(p.PropertyType))
                    continue;
                switch (p.GetValue(t))
                {
                    case SemanticType child:
                        Visit(child);
                        break;
                    case IEnumerable children:
                        foreach (var c in children.OfType<SemanticType>())
                            Visit(c);
                        break;
                }
            }
        }
        Visit(type);
        return found;
    }

    /// <summary>The codec's two directions through the public symbol channel.</summary>
    private static class SymbolSerializerProbe
    {
        internal static string Encode(SemanticType type)
            => SymbolSerializer.Serialize(
                new FunctionSymbol { Name = "probe", Kind = SymbolKind.Function, ReturnType = type }, LibPath).ReturnTypeId!;

        internal static SemanticType Decode(string typeId)
        {
            var cached = SymbolSerializer.Serialize(
                new FunctionSymbol { Name = "probe", Kind = SymbolKind.Function, ReturnType = SemanticType.Void }, LibPath);
            return ((FunctionSymbol)SymbolSerializer.Deserialize(
                cached with { ReturnTypeId = typeId },
                new Dictionary<string, Symbol>(StringComparer.Ordinal))).ReturnType;
        }
    }
}
