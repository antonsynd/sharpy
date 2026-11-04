using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Core.Tests;

/// <summary>
/// #2404 (plan P76, D5): every Sharpy.Core element accessor the compiler emits on a hot path must
/// inline into its caller — only the negative-index wraparound and the exception construction may
/// live behind a call. The JIT cannot be observed from the gate (the test run loads the Debug Core,
/// whose <see cref="DebuggableAttribute.IsJITOptimizerDisabled"/> turns inlining off), so this is a
/// static IL-shape scan. Per roster method it asserts
/// <list type="bullet">
/// <item>(a) no exception-handling clause (an EH clause blocks inlining outright);</item>
/// <item>(b) no <c>newobj</c> of an <see cref="Exception"/>-derived type, no <c>ldstr</c>, no call into
/// <see cref="DefaultInterpolatedStringHandler"/> / <c>String.Concat</c> / <c>String.Format</c>
/// (the cold-path construction that defeats the inline budget);</item>
/// <item>(c) every <c>call</c>/<c>callvirt</c> target is in the literal BCL <see cref="BclAllowSet"/>, or a
/// Sharpy.Core method that is itself conformant (recursive, cycle-safe), or a Core method carrying
/// <see cref="MethodImplOptions.NoInlining"/> (the throw helpers);</item>
/// <item>(d) the IL body is at most 100 B against a Release Core (the JIT's
/// <c>DEFAULT_MAX_INLINE_SIZE</c>) and at most 128 B against a Debug Core, chosen at run time from the
/// Core assembly's <see cref="DebuggableAttribute"/>.</item>
/// </list>
/// The rule is the IL shape alone: <c>AggressiveInlining</c> earns nothing here, because the attribute
/// raises the JIT's willingness without removing the out-of-line construction this class is about.
/// <para>
/// The <see cref="Roster"/> is closed and literal and is totality-anchored three ways — every Core
/// method that (i) references a <c>Sharpy.Index</c> member, (ii) is a public key indexer (<c>get_Item</c>
/// with one parameter, <c>set_Item</c> with two) on an exported Core type, or (iii) holds
/// <c>newobj IndexError::.ctor</c> must be a roster row, a literal <see cref="NotApplicable"/> row, or
/// (for (iii) only) a <see cref="HelperLiteral"/> throw helper on <c>Sharpy.Index</c>. There is no
/// allowlist: a new <c>Normalize</c> caller, a new public indexer or a new <c>IndexError</c> site is red
/// until it is placed. Positive controls prove each clause and each anchor part reports what it must.
/// </para>
/// </summary>
public class ElementAccessInlinabilityConformanceTests
{
    private readonly ITestOutputHelper _output;

    public ElementAccessInlinabilityConformanceTests(ITestOutputHelper output) => _output = output;

    private static readonly Assembly CoreAssembly = typeof(global::Sharpy.List<>).Assembly;

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance |
        BindingFlags.DeclaredOnly;

    // ------------------------------------------------------------------------------------------------
    // The literals.
    // ------------------------------------------------------------------------------------------------

    /// <summary>The roster (plan P76, Defect Class rows 1–15): (row, method key).</summary>
    private static readonly (string Row, string Key)[] Roster =
    {
        ("1", "Sharpy.List`1::get_Item(System.Int32)"),
        ("2", "Sharpy.List`1::set_Item(System.Int32, T)"),
        ("3", "Sharpy.List`1::GetItemUnchecked(System.Int32)"),
        ("4", "Sharpy.Index::Normalize(System.Int32, System.Int32)"),
        ("4", "Sharpy.Index::Clamp(System.Int32, System.Int32)"),
        ("5", "Sharpy.IndexExtensions::ToNormalizedInt32(System.Index, System.Int32, System.Boolean, System.Boolean)"),
        ("6", "Sharpy.Slice::Normalize(System.Int32, System.Int32, System.Int32)"),
        ("7", "Sharpy.List`1::Pop(System.Int32)"),
        ("8", "Sharpy.List`1::Insert(System.Int32, T)"),
        ("9", "Sharpy.List`1::DeleteAt(System.Int32)"),
        ("10", "Sharpy.List`1::Index(T, System.Int32, System.Int32)"),
        ("11", "Sharpy.ArrayHelpers::GetItem<T>(T[], System.Int32)"),
        ("11", "Sharpy.ArrayHelpers::SetItem<T>(T[], System.Int32, T)"),
        ("12", "Sharpy.StringHelpers::GetItem(System.String, System.Int32)"),
        ("13", "Sharpy.Dict`2::get_Item(K)"),
        ("13", "Sharpy.Dict`2::set_Item(K, V)"),
        ("14", "Sharpy.Bytes::get_Item(System.Int32)"),
        ("15", "Sharpy.FrozenDict`2::get_Item(TKey)"),
    };

    /// <summary>A literal N/A row of the Defect Class table: its reason and the Core members it covers.</summary>
    private sealed record NotApplicableRow(string Name, string Reason, string[] Keys);

    /// <summary>The six N/A rows of the Defect Class table, each with its reason.</summary>
    private static readonly NotApplicableRow[] NotApplicable =
    {
        new("tuple t[i]",
            "compile-time .ItemN (collection_types.md:363): no runtime accessor exists, so no Core member " +
            "carries it; R-KA's \"tuple\" resolves to this row (owner-acknowledged 2026-11-03)",
            Array.Empty<string>()),
        new("explicit interface indexer implementations",
            "private CLR members reached only by interface dispatch, never a direct hot call; excluded " +
            "from anchor part (ii) by construction (not public)",
            new[]
            {
                "Sharpy.Dict`2::Sharpy.IDict.get_Item(System.Object)",
                "Sharpy.Dict`2::Sharpy.IDict.set_Item(System.Object, System.Object)",
                "Sharpy.Dict`2::System.Collections.IDictionary.get_Item(System.Object)",
                "Sharpy.Dict`2::System.Collections.IDictionary.set_Item(System.Object, System.Object)",
                "Sharpy.List`1::System.Collections.Generic.IList<T>.get_Item(System.Int32)",
                "Sharpy.List`1::System.Collections.Generic.IList<T>.set_Item(System.Int32, T)",
                "Sharpy.List`1::System.Collections.Generic.IReadOnlyList<T>.get_Item(System.Int32)",
                "Sharpy.List`1::System.Collections.IList.get_Item(System.Int32)",
                "Sharpy.List`1::System.Collections.IList.set_Item(System.Int32, System.Object)",
                "Sharpy.List`1::Sharpy.IList.get_Item(System.Object)",
                "Sharpy.List`1::Sharpy.IList.set_Item(System.Object, System.Object)",
            }),
        new("abstract interface indexer declarations",
            "abstract members have no body to inline; anchor part (ii) skips IsAbstract members and the " +
            "skipped set must equal AbstractIndexerLiteral, so the skip is not an open exemption",
            AbstractIndexerLiteral()),
        new("str.format item access",
            "StringExtensions.ResolveItem / ResolveFieldValue are str.format's runtime field access " +
            "(\"{0[5]}\".format(...)), not an element-access emit route",
            new[]
            {
                "Sharpy.StringExtensions::ResolveItem(System.Object, System.String)",
                "Sharpy.StringExtensions::ResolveFieldValue(System.String, System.Object[], System.Boolean, " +
                "Sharpy.Dict`2[System.String, System.Object], System.Int32&, System.Boolean&, System.Boolean&)",
            }),
        new("C# list-pattern indexers",
            "List<T>.this[System.Index] / this[System.Range] are the C# list-pattern surface: GetOffset + " +
            "the BCL indexer (ArgumentOutOfRangeException, never IndexError); no Python route",
            new[]
            {
                "Sharpy.List`1::get_Item(System.Index)",
                "Sharpy.List`1::get_Item(System.Range)",
            }),
        new("slice accessors",
            "slice access, not element access: they reach Slice.Normalize (row 6) and are outside anchor " +
            "part (ii) (two/three parameters) by construction; listed so a future one-parameter overload is red",
            new[]
            {
                "Sharpy.List`1::get_Item(System.Int32, System.Int32)",
                "Sharpy.List`1::set_Item(System.Int32, System.Int32, Sharpy.List`1)",
                "Sharpy.List`1::get_Item(System.Int32, System.Int32, System.Int32)",
                "Sharpy.List`1::set_Item(System.Int32, System.Int32, System.Int32, Sharpy.List`1)",
                "Sharpy.List`1::GetSlice(Sharpy.Slice)",
                "Sharpy.List`1::SetSlice(Sharpy.Slice, System.Collections.Generic.IEnumerable`1[T])",
                "Sharpy.List`1::SetSlice(Sharpy.Slice, Sharpy.List`1)",
                "Sharpy.List`1::DeleteSlice(Sharpy.Slice)",
            }),
    };

    /// <summary>The abstract public indexers anchor part (ii) skips (<c>IDict.cs:33</c>, <c>IList.cs:32</c>).</summary>
    private static string[] AbstractIndexerLiteral() => new[]
    {
        "Sharpy.IDict::get_Item(System.Object)",
        "Sharpy.IDict::set_Item(System.Object, System.Object)",
        "Sharpy.IList::get_Item(System.Object)",
        "Sharpy.IList::set_Item(System.Object, System.Object)",
    };

    /// <summary>
    /// The [NoInlining] throw helpers on <c>Sharpy.Index</c>, by name. Checked both ways: each named
    /// helper exists and carries NoInlining, and every NoInlining Core method holding
    /// <c>newobj IndexError</c> is one of them. <c>NotInList</c> / <c>KeyNotFound</c> hold
    /// ValueError / KeyError and trip no anchor; they are listed for completeness.
    /// </summary>
    private static readonly string[] HelperLiteral =
    {
        "WrapOrThrow", "OutOfRange", "PopOutOfRange", "PopFromEmpty", "ArrayOutOfRange",
        "StringOutOfRange", "BytesOutOfRange", "NotInList", "KeyNotFound",
    };

    /// <summary>
    /// The complete BCL call set of the roster bodies and their conformant Core callees (D5(c)).
    /// An entry no scanned body reaches is red (delete it).
    /// </summary>
    private static readonly string[] BclAllowSet =
    {
        "System.Collections.Generic.List`1::get_Item(System.Int32)",
        "System.Collections.Generic.List`1::set_Item(System.Int32, T)",
        "System.Collections.Generic.List`1::get_Count()",
        "System.Collections.Generic.List`1::RemoveAt(System.Int32)",
        "System.Collections.Generic.List`1::Insert(System.Int32, T)",
        "System.Collections.Generic.List`1::IndexOf(T, System.Int32, System.Int32)",
        "System.String::get_Length()",
        "System.String::get_Chars(System.Int32)",
        "System.Char::ToString()",
        "System.Collections.Generic.Dictionary`2::TryGetValue(TKey, TValue&)",
        "System.Collections.Generic.Dictionary`2::set_Item(TKey, TValue)",
        "System.Collections.Generic.Dictionary`2::get_Count()",
        "System.Collections.Immutable.ImmutableDictionary`2::TryGetValue(TKey, TValue&)",
        "System.Index::get_IsFromEnd()",
        "System.Index::get_Value()",
        "System.Array::Empty<T>()",
        "System.Math::Min(System.Int32, System.Int32)",
        "System.Math::Max(System.Int32, System.Int32)",
    };

    // ------------------------------------------------------------------------------------------------
    // The Facts.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void CoreElementAccessRoster_IsInlinableShaped_AndTotal()
    {
        var violations = new System.Collections.Generic.List<string>();
        int ceiling = CeilingFor(CoreAssembly, out bool jitOptimizerDisabled);
        var scanner = new ShapeScanner(CoreAssembly, ceiling);

        _output.WriteLine($"Sharpy.Core IsJITOptimizerDisabled={jitOptimizerDisabled} -> clause (d) ceiling {ceiling} B");

        var coreTypes = CoreAssembly.GetTypes();
        var byKey = new Dictionary<string, System.Collections.Generic.List<MethodBase>>(StringComparer.Ordinal);
        foreach (var method in Members(coreTypes))
        {
            string key = Key(method);
            if (!byKey.TryGetValue(key, out var list))
            {
                byKey[key] = list = new System.Collections.Generic.List<MethodBase>();
            }

            list.Add(method);
        }

        // The roster: every row resolves, and every row's body is inlinable-shaped.
        _output.WriteLine("row  IL B  EH  method");
        foreach (var (row, key) in Roster)
        {
            if (!byKey.TryGetValue(key, out var found))
            {
                violations.Add($"row {row} {key}: [roster] no such method in Sharpy.Core — a renamed or " +
                    "re-signatured accessor must move its roster row");
                continue;
            }

            foreach (var method in found)
            {
                var body = method.GetMethodBody();
                _output.WriteLine($"{row,-4} {body?.GetILAsByteArray()?.Length ?? -1,4}  {body?.ExceptionHandlingClauses.Count ?? -1,2}  {key}");
                foreach (var finding in scanner.Clauses(method))
                {
                    violations.Add($"row {row} {key}: ({finding.Clause}) {finding.Detail}");
                }
            }
        }

        // The N/A literal is not stale.
        foreach (var row in NotApplicable)
        {
            foreach (var key in row.Keys)
            {
                if (!byKey.ContainsKey(key))
                {
                    violations.Add($"N/A row '{row.Name}' {key}: [stale N/A] no such member in Sharpy.Core — delete " +
                        "or re-key the literal entry");
                }
            }
        }

        var rosterKeys = Roster.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        var naKeys = NotApplicable.SelectMany(r => r.Keys).ToHashSet(StringComparer.Ordinal);
        bool Placed(MethodBase m) => rosterKeys.Contains(Key(m)) || naKeys.Contains(Key(m));

        // Anchor part (i): Sharpy.Index referrers, over every Core type.
        foreach (var (method, detail) in AnchorIndexReferrers(coreTypes, violations))
        {
            if (!Placed(method))
            {
                violations.Add($"{Key(method)}: [anchor (i)] references {detail} but is neither a roster row " +
                    "nor an N/A row");
            }
        }

        // Anchor part (ii): public key indexers, over the exported Core types.
        var exportedTypes = CoreAssembly.GetExportedTypes();
        var publicKeyIndexers = AnchorPublicKeyIndexers(exportedTypes, out var skippedAbstract);
        foreach (var method in publicKeyIndexers)
        {
            if (!Placed(method))
            {
                violations.Add($"{Key(method)}: [anchor (ii)] a public key indexer on an exported Core type that " +
                    "is neither a roster row nor an N/A row");
            }
        }

        var abstractLiteral = AbstractIndexerLiteral().ToHashSet(StringComparer.Ordinal);
        var skippedKeys = skippedAbstract.Select(Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in skippedKeys.Where(k => !abstractLiteral.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
        {
            violations.Add($"{key}: [anchor (ii) abstract skip] an abstract public indexer the scan skipped that " +
                "the abstract literal does not name — place it");
        }

        foreach (var key in abstractLiteral.Where(k => !skippedKeys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
        {
            violations.Add($"{key}: [anchor (ii) abstract skip] named by the abstract literal but not skipped by the " +
                "scan — delete the entry");
        }

        // Anchor part (iii): IndexError construction sites, over every Core type.
        var helperNames = HelperLiteral.ToHashSet(StringComparer.Ordinal);
        bool IsNamedHelper(MethodBase m) =>
            m.DeclaringType == typeof(global::Sharpy.Index) && helperNames.Contains(m.Name) && HasNoInlining(m);

        var indexErrorSites = AnchorIndexErrorSites(coreTypes, violations);
        foreach (var method in indexErrorSites)
        {
            if (!Placed(method) && !IsNamedHelper(method))
            {
                violations.Add($"{Key(method)}: [anchor (iii)] holds newobj IndexError::.ctor but is neither a roster " +
                    "row, an N/A row, nor a [NoInlining] helper named in the helper literal");
            }
        }

        // The helper literal, both ways.
        var indexMembers = typeof(global::Sharpy.Index).GetMethods(AllDeclared);
        foreach (var name in HelperLiteral)
        {
            var named = indexMembers.Where(m => m.Name == name).ToList();
            if (named.Count == 0)
            {
                violations.Add($"Sharpy.Index::{name}: [helper literal] named helper does not exist on Sharpy.Index");
            }

            foreach (var helper in named.Where(m => !HasNoInlining(m)))
            {
                violations.Add($"{Key(helper)}: [helper literal] named helper lacks [MethodImpl(MethodImplOptions.NoInlining)]");
            }

            foreach (var helper in named)
            {
                var body = helper.GetMethodBody();
                _output.WriteLine($"helper {body?.GetILAsByteArray()?.Length ?? -1,4}  {body?.ExceptionHandlingClauses.Count ?? -1,2}  {Key(helper)}");
            }
        }

        foreach (var method in indexErrorSites.Where(m => HasNoInlining(m) && !IsNamedHelper(m)))
        {
            violations.Add($"{Key(method)}: [helper literal] a [NoInlining] Core method holding newobj IndexError " +
                "that the helper literal does not name");
        }

        // The allow-set has no entry that no scanned body reaches.
        foreach (var key in BclAllowSet.Where(k => !scanner.UsedAllowSetEntries.Contains(k)))
        {
            violations.Add($"{key}: [allow-set] no roster body or conformant Core callee calls it — delete the entry");
        }

        Assert.True(violations.Count == 0,
            $"{violations.Count} element-access inlinability violation(s) (#2404). Fix the accessor (fast path + " +
            "[NoInlining] helper on Sharpy.Index) or place the member in the roster / N/A literal:\n  " +
            string.Join("\n  ", violations));
    }

    [Fact]
    public void ShapePredicate_ReportsEachBadControlByItsOwnClause_AndPassesTheGoodOne()
    {
        int ceiling = CeilingFor(CoreAssembly, out _);
        var scanner = new ShapeScanner(CoreAssembly, ceiling);
        var problems = new System.Collections.Generic.List<string>();

        void Expect(string name, params char[] expected)
        {
            var method = typeof(ElementAccessInlinabilityConformanceTests).GetMethod(name, AllDeclared)!;
            var findings = scanner.Clauses(method);
            var clauses = findings.Select(f => f.Clause).Distinct().OrderBy(c => c).ToArray();
            var want = expected.OrderBy(c => c).ToArray();
            int size = method.GetMethodBody()!.GetILAsByteArray()!.Length;
            _output.WriteLine($"{name}: IL {size} B, clauses [{string.Join(",", clauses)}]");
            foreach (var f in findings)
            {
                _output.WriteLine($"    ({f.Clause}) {f.Detail}");
            }

            if (!clauses.SequenceEqual(want))
            {
                problems.Add($"{name}: expected clauses [{string.Join(",", want)}], reported [{string.Join(",", clauses)}]: " +
                    string.Join("; ", findings.Select(f => $"({f.Clause}) {f.Detail}")));
            }
        }

        Expect(nameof(BadShapeControl), 'b', 'c');
        Expect(nameof(BigShapeControl), 'd');
        Expect(nameof(EhShapeControl), 'a');
        Expect(nameof(CallShapeControl), 'c');
        Expect(nameof(GoodShapeControl));

        Assert.True(problems.Count == 0, "The shape predicate mis-reports its controls:\n  " + string.Join("\n  ", problems));
    }

    [Fact]
    public void TotalityAnchor_ReportsEachPlantedMember_ByItsOwnPart()
    {
        var universe = new[] { typeof(PlantedHolder) };
        var scanErrors = new System.Collections.Generic.List<string>();

        var partI = AnchorIndexReferrers(universe, scanErrors).Select(t => t.Method.Name).ToArray();
        var partII = AnchorPublicKeyIndexers(universe, out var skippedAbstract).Select(m => m.Name).ToArray();
        var partIII = AnchorIndexErrorSites(universe, scanErrors).Select(m => m.Name).ToArray();

        Assert.Empty(scanErrors);
        Assert.Equal(new[] { "PlantedIndexReferrer" }, partI);
        Assert.Equal(new[] { "get_Item" }, partII);
        Assert.Empty(skippedAbstract);
        Assert.Equal(new[] { "PlantedIndexErrorSite" }, partIII);

        // The planted (i)/(iii) members are private: the member flags must include NonPublic.
        Assert.False(typeof(PlantedHolder).GetMethod("PlantedIndexReferrer", AllDeclared)!.IsPublic);
        Assert.False(typeof(PlantedHolder).GetMethod("PlantedIndexErrorSite", AllDeclared)!.IsPublic);
        // (ii) filters on the member, never on Type.IsPublic (false for a public nested type).
        Assert.False(typeof(PlantedHolder).IsPublic);
        Assert.True(typeof(PlantedHolder).IsVisible);
    }

    [Fact]
    public void TotalityAnchor_TypeUniverses_ArePinned()
    {
        var all = CoreAssembly.GetTypes();
        var exported = CoreAssembly.GetExportedTypes();

        Assert.Contains(typeof(global::Sharpy.Index), all);
        Assert.Contains(typeof(global::Sharpy.List<>), exported);
        Assert.DoesNotContain(typeof(global::Sharpy.Index), exported);
    }

    // ------------------------------------------------------------------------------------------------
    // Positive controls (never called; their IL is the probe).
    // ------------------------------------------------------------------------------------------------

    private static readonly int[] ControlData = { 1, 2, 3 };

    /// <summary>An inline interpolated throw: reported by (b) (newobj IndexError, ldstr, handler calls) and (c).</summary>
    public static int BadShapeControl(int i)
    {
        var d = ControlData;
        if ((uint)i < (uint)d.Length)
        {
            return d[i];
        }

        throw new IndexError($"list index {i} out of range");
    }

    /// <summary>40 live straight-line integer ops on the parameter: no newobj/ldstr/calls, reported by (d) alone.</summary>
    public static int BigShapeControl(int i)
    {
        i += 3;
        i *= 5;
        i ^= 7;
        i -= 11;
        i += 13;
        i *= 17;
        i ^= 19;
        i -= 23;
        i += 29;
        i *= 31;
        i ^= 37;
        i -= 41;
        i += 43;
        i *= 47;
        i ^= 53;
        i -= 59;
        i += 61;
        i *= 67;
        i ^= 71;
        i -= 73;
        i += 79;
        i *= 83;
        i ^= 89;
        i -= 97;
        i += 101;
        i *= 103;
        i ^= 107;
        i -= 109;
        i += 113;
        i *= 127;
        i ^= 3;
        i -= 5;
        i += 7;
        i *= 11;
        i ^= 13;
        i -= 17;
        i += 19;
        i *= 23;
        i ^= 29;
        i -= 31;
        return i;
    }

    /// <summary>The good shape inside <c>try { } catch (IndexError) { throw; }</c>: reported by (a) alone.</summary>
    public static int EhShapeControl(int i)
    {
        try
        {
            var d = ControlData;
            return (uint)i < (uint)d.Length ? d[i] : d[global::Sharpy.Index.WrapOrThrow(i, d.Length)];
        }
        catch (IndexError)
        {
            throw;
        }
    }

    /// <summary>The good shape plus an unlisted BCL call: reported by (c) alone.</summary>
    public static int CallShapeControl(int i)
    {
        System.Console.Out.Flush();
        var d = ControlData;
        return (uint)i < (uint)d.Length ? d[i] : d[global::Sharpy.Index.WrapOrThrow(i, d.Length)];
    }

    /// <summary>The D2 shape, slow path through the [NoInlining] <c>Index.WrapOrThrow</c>: reported by no clause.</summary>
    public static int GoodShapeControl(int i)
    {
        var d = ControlData;
        return (uint)i < (uint)d.Length ? d[i] : d[global::Sharpy.Index.WrapOrThrow(i, d.Length)];
    }

    /// <summary>
    /// The anchor positive control: public (so an exported-type universe can see it) with one planted
    /// member per anchor part — a private <c>Sharpy.Index</c> referrer (i), a public one-parameter
    /// indexer (ii) and a private <c>newobj IndexError</c> site (iii).
    /// </summary>
    public sealed class PlantedHolder
    {
        public int this[int i] => i;

        private static int PlantedIndexReferrer(int i, int n) => global::Sharpy.Index.Normalize(i, n);

        private static IndexError PlantedIndexErrorSite() => new IndexError("planted");
    }

    // ------------------------------------------------------------------------------------------------
    // The anchors — each a function of an explicit type universe; the member flags are applied here.
    // ------------------------------------------------------------------------------------------------

    private static IEnumerable<MethodBase> Members(IEnumerable<Type> universe) =>
        universe.SelectMany(t => t.GetMethods(AllDeclared).Cast<MethodBase>().Concat(t.GetConstructors(AllDeclared)));

    /// <summary>Anchor part (i): methods whose IL references a member of <c>Sharpy.Index</c>.</summary>
    private static System.Collections.Generic.List<(MethodBase Method, string Detail)> AnchorIndexReferrers(IEnumerable<Type> universe, System.Collections.Generic.List<string> scanErrors)
    {
        var hits = new System.Collections.Generic.List<(MethodBase, string)>();
        foreach (var method in Members(universe))
        {
            var decoded = TryDecode(method, scanErrors);
            if (decoded is null)
            {
                continue;
            }

            var target = decoded.Value.Instructions
                .Where(ins => ins.OpCode.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineTok)
                .Select(ins => TryResolveMember(method, ins.Token, scanErrors))
                .FirstOrDefault(m => m?.DeclaringType == typeof(global::Sharpy.Index));
            if (target is not null)
            {
                hits.Add((method, $"Sharpy.Index::{target.Name}"));
            }
        }

        return hits;
    }

    /// <summary>
    /// Anchor part (ii): public, non-abstract <c>get_Item</c> with one parameter or <c>set_Item</c> with two.
    /// Visibility is the MEMBER's; the abstract members skipped are returned for the literal check.
    /// </summary>
    private static System.Collections.Generic.List<MethodBase> AnchorPublicKeyIndexers(IEnumerable<Type> universe, out System.Collections.Generic.List<MethodBase> skippedAbstract)
    {
        var hits = new System.Collections.Generic.List<MethodBase>();
        skippedAbstract = new System.Collections.Generic.List<MethodBase>();
        foreach (var method in universe.SelectMany(t => t.GetMethods(AllDeclared)))
        {
            int arity = method.GetParameters().Length;
            bool keyIndexer = (method.Name == "get_Item" && arity == 1) || (method.Name == "set_Item" && arity == 2);
            if (!keyIndexer || !method.IsPublic)
            {
                continue;
            }

            (method.IsAbstract ? skippedAbstract : hits).Add(method);
        }

        return hits;
    }

    /// <summary>Anchor part (iii): methods whose IL holds <c>newobj IndexError::.ctor</c>.</summary>
    private static System.Collections.Generic.List<MethodBase> AnchorIndexErrorSites(IEnumerable<Type> universe, System.Collections.Generic.List<string> scanErrors)
    {
        var hits = new System.Collections.Generic.List<MethodBase>();
        foreach (var method in Members(universe))
        {
            var decoded = TryDecode(method, scanErrors);
            if (decoded is null)
            {
                continue;
            }

            if (decoded.Value.Instructions.Any(ins => ins.OpCode == OpCodes.Newobj &&
                    TryResolveMember(method, ins.Token, scanErrors)?.DeclaringType == typeof(IndexError)))
            {
                hits.Add(method);
            }
        }

        return hits;
    }

    // ------------------------------------------------------------------------------------------------
    // The shape predicate (clauses (a)–(d)).
    // ------------------------------------------------------------------------------------------------

    private readonly record struct Finding(char Clause, string Detail);

    private sealed class ShapeScanner
    {
        private readonly Assembly _core;
        private readonly int _ceiling;
        private readonly HashSet<string> _allowSet = BclAllowSet.ToHashSet(StringComparer.Ordinal);
        private readonly Dictionary<MethodBase, bool?> _calleeConformance = new();

        public ShapeScanner(Assembly core, int ceiling)
        {
            _core = core;
            _ceiling = ceiling;
        }

        public HashSet<string> UsedAllowSetEntries { get; } = new(StringComparer.Ordinal);

        public System.Collections.Generic.List<Finding> Clauses(MethodBase method)
        {
            var findings = new System.Collections.Generic.List<Finding>();
            var errors = new System.Collections.Generic.List<string>();
            var decoded = TryDecode(method, errors);
            if (decoded is null)
            {
                foreach (var error in errors)
                {
                    findings.Add(new Finding('c', $"cannot be scanned: {error}"));
                }

                if (errors.Count == 0)
                {
                    findings.Add(new Finding('c', "has no IL body to inline"));
                }

                return findings;
            }

            var (body, il, instructions) = decoded.Value;

            // (a) exception-handling clauses.
            foreach (var clause in body.ExceptionHandlingClauses)
            {
                findings.Add(new Finding('a', $"EH clause {clause.Flags} (try IL_{clause.TryOffset:X4}, handler IL_{clause.HandlerOffset:X4})"));
            }

            foreach (var ins in instructions)
            {
                // (b) cold-path construction inline.
                if (ins.OpCode == OpCodes.Ldstr)
                {
                    findings.Add(new Finding('b', $"ldstr \"{method.Module.ResolveString(ins.Token)}\" at IL_{ins.Offset:X4}"));
                    continue;
                }

                bool isCall = ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt;
                if (ins.OpCode == OpCodes.Calli)
                {
                    findings.Add(new Finding('c', $"calli (an indirect call) at IL_{ins.Offset:X4}"));
                    continue;
                }

                if (!isCall && ins.OpCode != OpCodes.Newobj)
                {
                    continue;
                }

                var target = TryResolveMember(method, ins.Token, errors) as MethodBase;
                if (target is null)
                {
                    findings.Add(new Finding('c', $"unresolvable {ins.OpCode.Name} target 0x{ins.Token:X8} at IL_{ins.Offset:X4}"));
                    continue;
                }

                var declaring = target.DeclaringType;
                if (ins.OpCode == OpCodes.Newobj && declaring is not null && typeof(Exception).IsAssignableFrom(declaring))
                {
                    findings.Add(new Finding('b', $"newobj {declaring.FullName}::.ctor at IL_{ins.Offset:X4}"));
                }

                if (declaring == typeof(DefaultInterpolatedStringHandler) ||
                    (declaring == typeof(string) && target.Name is "Concat" or "Format"))
                {
                    findings.Add(new Finding('b', $"{ins.OpCode.Name} {Key(Definition(target))} at IL_{ins.Offset:X4} (string construction)"));
                }

                if (!isCall)
                {
                    continue;
                }

                // (c) every call target: BCL allow-set, conformant Core callee, or [NoInlining] Core method.
                var definition = Definition(target);
                string key = Key(definition);
                if (_allowSet.Contains(key))
                {
                    UsedAllowSetEntries.Add(key);
                    continue;
                }

                if (definition.DeclaringType?.Assembly == _core)
                {
                    if (HasNoInlining(definition))
                    {
                        continue;
                    }

                    var calleeClauses = CalleeClauses(definition);
                    if (calleeClauses is null)
                    {
                        continue;
                    }

                    findings.Add(new Finding('c', $"calls {key} at IL_{ins.Offset:X4} — a Core callee that is neither " +
                        $"[NoInlining] nor conformant ({calleeClauses})"));
                    continue;
                }

                findings.Add(new Finding('c', $"calls {key} at IL_{ins.Offset:X4} — not in the BCL allow-set"));
            }

            // (d) the inline-candidate size ceiling.
            if (il.Length > _ceiling)
            {
                findings.Add(new Finding('d', $"IL body {il.Length} B > {_ceiling} B ceiling"));
            }

            return findings;
        }

        /// <summary>Null when the Core callee is conformant; otherwise its clause letters. Cycle-safe (in progress = conformant).</summary>
        private string? CalleeClauses(MethodBase callee)
        {
            if (_calleeConformance.TryGetValue(callee, out var known))
            {
                return known is null || known.Value ? null : string.Join(",", Clauses(callee).Select(f => f.Clause).Distinct());
            }

            _calleeConformance[callee] = null;
            var findings = Clauses(callee);
            _calleeConformance[callee] = findings.Count == 0;
            return findings.Count == 0 ? null : string.Join(",", findings.Select(f => f.Clause).Distinct().OrderBy(c => c));
        }
    }

    // ------------------------------------------------------------------------------------------------
    // The IL reader — an opcode table built from System.Reflection.Emit.OpCodes.
    // ------------------------------------------------------------------------------------------------

    private readonly record struct Instruction(int Offset, OpCode OpCode, int Token);

    private static readonly OpCode?[] OneByteOpCodes = new OpCode?[0x100];
    private static readonly OpCode?[] TwoByteOpCodes = new OpCode?[0x100];

    static ElementAccessInlinabilityConformanceTests()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode op)
            {
                continue;
            }

            if (op.Size == 1)
            {
                OneByteOpCodes[op.Value & 0xFF] = op;
            }
            else
            {
                TwoByteOpCodes[op.Value & 0xFF] = op;
            }
        }
    }

    private static System.Collections.Generic.List<Instruction> Decode(byte[] il)
    {
        var instructions = new System.Collections.Generic.List<Instruction>();
        int pos = 0;
        while (pos < il.Length)
        {
            int start = pos;
            byte b = il[pos++];
            OpCode? op;
            if (b == 0xFE)
            {
                if (pos >= il.Length)
                {
                    throw new InvalidDataException($"truncated two-byte opcode at IL_{start:X4}");
                }

                op = TwoByteOpCodes[il[pos++]];
            }
            else
            {
                op = OneByteOpCodes[b];
            }

            if (op is null)
            {
                throw new InvalidDataException($"unknown opcode 0x{b:X2} at IL_{start:X4}");
            }

            int token = 0;
            switch (op.Value.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    pos += 1;
                    break;
                case OperandType.InlineVar:
                    pos += 2;
                    break;
                case OperandType.InlineBrTarget:
                case OperandType.InlineI:
                case OperandType.ShortInlineR:
                    pos += 4;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    pos += 8;
                    break;
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    token = BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(pos, 4));
                    pos += 4;
                    break;
                case OperandType.InlineSwitch:
                    int targets = BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(pos, 4));
                    pos += 4 + (4 * targets);
                    break;
                default:
                    throw new InvalidDataException($"unsupported operand type {op.Value.OperandType} at IL_{start:X4}");
            }

            if (pos > il.Length)
            {
                throw new InvalidDataException($"operand of {op.Value.Name} at IL_{start:X4} runs past the body");
            }

            instructions.Add(new Instruction(start, op.Value, token));
        }

        return instructions;
    }

    /// <summary>Decodes a method body; null for a method without IL. A decode failure is recorded, never skipped silently.</summary>
    private static (MethodBody Body, byte[] Il, System.Collections.Generic.List<Instruction> Instructions)? TryDecode(MethodBase method, System.Collections.Generic.List<string> errors)
    {
        var body = method.GetMethodBody();
        var il = body?.GetILAsByteArray();
        if (body is null || il is null)
        {
            return null;
        }

        try
        {
            return (body, il, Decode(il));
        }
        catch (InvalidDataException e)
        {
            errors.Add($"{Key(method)}: [scan] {e.Message}");
            return null;
        }
    }

    /// <summary>Resolves a member token in the method's own generic context; a failure is recorded, never skipped silently.</summary>
    private static MemberInfo? TryResolveMember(MethodBase method, int token, System.Collections.Generic.List<string> errors)
    {
        var typeArgs = method.DeclaringType is { IsGenericType: true } t ? t.GetGenericArguments() : null;
        var methodArgs = method is MethodInfo { IsGenericMethod: true } ? method.GetGenericArguments() : null;
        try
        {
            return method.Module.ResolveMember(token, typeArgs, methodArgs);
        }
        catch (Exception e) when (e is ArgumentException or BadImageFormatException or TypeLoadException or MissingMemberException)
        {
            errors.Add($"{Key(method)}: [scan] token 0x{token:X8} does not resolve: {e.GetType().Name}: {e.Message}");
            return null;
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Keys and attributes.
    // ------------------------------------------------------------------------------------------------

    /// <summary>The open definition of a (possibly constructed) method: its MethodDef token resolved without context.</summary>
    private static MethodBase Definition(MethodBase method) => method.Module.ResolveMethod(method.MetadataToken)!;

    private static string Key(MethodBase method)
    {
        string generic = method is MethodInfo { IsGenericMethod: true }
            ? "<" + string.Join(",", method.GetGenericArguments().Select(TypeName)) + ">"
            : "";
        return $"{TypeName(method.DeclaringType!)}::{method.Name}{generic}(" +
            string.Join(", ", method.GetParameters().Select(p => TypeName(p.ParameterType))) + ")";
    }

    private static string TypeName(Type type)
    {
        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (type.IsByRef)
        {
            return TypeName(type.GetElementType()!) + "&";
        }

        if (type.IsPointer)
        {
            return TypeName(type.GetElementType()!) + "*";
        }

        if (type.IsArray)
        {
            return TypeName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            return type.GetGenericTypeDefinition().FullName + "[" +
                string.Join(", ", type.GetGenericArguments().Select(TypeName)) + "]";
        }

        return type.FullName ?? type.Name;
    }

    private static bool HasNoInlining(MethodBase method) =>
        (method.MethodImplementationFlags & MethodImplAttributes.NoInlining) != 0;

    /// <summary>Clause (d)'s ceiling: 100 B against a Release Core, 128 B against a Debug one (MinOpts).</summary>
    private static int CeilingFor(Assembly core, out bool jitOptimizerDisabled)
    {
        jitOptimizerDisabled = core.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled ?? false;
        return jitOptimizerDisabled ? 128 : 100;
    }
}
