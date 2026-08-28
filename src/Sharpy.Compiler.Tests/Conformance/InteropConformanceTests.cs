using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// A1 (#1034) — generated interop conformance sweep.
///
/// <para>
/// Enumerates <b>every public member of every stdlib assembly</b> through the same
/// <see cref="OverloadIndex"/> the compiler builds from <c>Discovery/ClrTypeBridge</c>
/// (via <see cref="OverloadIndexBuilder"/>), then emits a small <c>.spy</c> snippet per
/// member per applicable <i>usage position</i> — annotate a type, call a function with
/// defaulted/synthesized args, reference a field, invoke an instance method or read a
/// property (receiver supplied by a typed parameter so no construction is needed), and
/// subclass an unsealed concrete class. Each snippet is compiled <b>in-process</b> via
/// <see cref="CompilerApi"/> (Sharpy → C#) and the generated C# is bound through Roslyn
/// to catch codegen/mangling asymmetries the Sharpy phase accepts.
/// </para>
///
/// <para>
/// This is a <b>sweep</b>, not 10,000 xUnit cases (Design Decision #10): every failure
/// aggregates into ONE <c>Category=GapDiscovery</c> JSON report under <c>.claude/tmp/</c>.
/// The test is traited out of the default fast suite. When a reviewed allowlist file is
/// present (added in the baseline commit) the test becomes a <b>ratchet</b>: any failure
/// or crash whose key is not allowlisted fails CI. Until then it is report-only and only
/// asserts that enumeration actually produced members.
/// </para>
///
/// <para>
/// A "failure" is a snippet that <i>should</i> compile if the member is usable but did
/// not — a real bridge regression, a known-unsupported member (allowlisted with a
/// justification), or a generator limitation. The single reviewed baseline draws that
/// line; the ratchet holds it.
/// </para>
///
/// <para>
/// A snippet whose only errors are SPY0215 (CLR-only member refused on a builtin
/// exception, #1515) is a <b>refused-by-design</b> verdict, not a failure: the refusal
/// IS the contract, mirroring the re-representation matrix's "every cell either compiles
/// or is refused by a deliberate Sharpy diagnostic". Refusals are reported separately and
/// never enter the allowlist — the allowlist stays reserved for genuine gaps that drain
/// on fix.
/// </para>
/// </summary>
[Trait("Category", "GapDiscovery")]
public class InteropConformanceTests
{
    private readonly ITestOutputHelper _output;

    public InteropConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // Usage positions. index/match join the original six (#1094): index reads x[key] for types
    // exposing a gettable indexer (key synthesized from the indexer parameter, or the closed
    // receiver's type-arg substitution); match scrutinizes the annotate-position variable with a
    // wildcard `case _:` arm, exhaustive by construction.
    private const string PosAnnotate = "annotate";
    private const string PosCall = "call";
    private const string PosReference = "reference";
    private const string PosMethod = "method";
    private const string PosProperty = "property";
    private const string PosSubclass = "subclass";
    private const string PosIndex = "index";
    private const string PosMatch = "match";

    [Fact]
    public void InteropSweep_AllPublicStdlibMembers_CompileClean()
    {
        var (corePath, stdlibPath) = ResolveStdlibAssemblyPaths();
        Assert.True(File.Exists(corePath), $"Sharpy.Core.dll not found at {corePath}");
        Assert.True(File.Exists(stdlibPath), $"Sharpy.Stdlib.dll not found at {stdlibPath}");

        var references = new[] { corePath, stdlibPath };
        var api = new CompilerApi(NullLogger.Instance, references);

        // Reusable Roslyn base compilation for binding generated C# (references are shared
        // and metadata is cached across the derived per-snippet compilations, so each bind
        // is just the cost of binding one small tree).
        var csharpBase = BuildCSharpBaseCompilation();

        var builder = new OverloadIndexBuilder(NullLogger.Instance);
        var snippets = new List<Snippet>();
        var notAttempted = new List<object>();
        var notAttemptedByReason = new Dictionary<(string Position, string Reason), int>();
        var skippedModules = new List<object>();
        var membersEnumerated = 0;

        // Records a notAttempted member for the flat report list and, in parallel, keeps a full
        // (uncapped) per-position/per-reason tally so bucket deltas stay measurable even though
        // the flat list is capped in the report.
        void RecordNotAttempted(string module, string member, string kind, string position, string reason)
        {
            notAttempted.Add(new { module, member, kind, position, reason });
            var key = (position, reason);
            notAttemptedByReason[key] = notAttemptedByReason.GetValueOrDefault(key) + 1;
        }

        foreach (var (asmName, asmPath) in new[] { ("Sharpy.Core", corePath), ("Sharpy.Stdlib", stdlibPath) })
        {
            var assembly = Assembly.LoadFrom(asmPath);
            var index = builder.BuildFromAssembly(assembly);

            foreach (var (moduleName, module) in index.Modules)
            {
                var isBuiltins = moduleName == "builtins";
                var importLine = isBuiltins ? "" : $"import {moduleName}\n";
                var qualifier = isBuiltins ? "" : moduleName + ".";

                // Skip whole modules that aren't user-importable so a non-importable
                // namespace-derived pseudo-module doesn't fail every one of its members.
                if (!isBuiltins && !ImportsClean(api, moduleName))
                {
                    skippedModules.Add(new { assembly = asmName, module = moduleName, reason = "module not importable" });
                    continue;
                }

                // --- Module functions -> call ---
                foreach (var (funcName, sigs) in module.Functions)
                {
                    membersEnumerated++;
                    var chosen = PickCallableOverload(sigs, allowGenericTypeArgs: true, typeSubst: null, out var args, out var typeArgs, out var reason);
                    if (chosen != null)
                    {
                        var callExpr = typeArgs != null
                            ? $"{qualifier}{funcName}[{typeArgs}]({args})"
                            : $"{qualifier}{funcName}({args})";
                        var body = CallStatement(callExpr, chosen.ReturnType);
                        var src = $"{importLine}def _use() -> None:\n    {body}\n";
                        snippets.Add(new Snippet(asmName, moduleName, "function", funcName, PosCall, src));
                    }
                    else
                    {
                        RecordNotAttempted(moduleName, funcName, "function", PosCall, reason);
                    }
                }

                // --- Module fields / static properties -> reference ---
                foreach (var (fieldName, _) in module.Fields)
                {
                    membersEnumerated++;
                    // Fields are exported under their verbatim discovered name (the key
                    // ImportResolver registers in ExportedSymbols), so reference it as-is.
                    var src = $"{importLine}def _use() -> None:\n    _x = {qualifier}{fieldName}\n";
                    snippets.Add(new Snippet(asmName, moduleName, "field", fieldName, PosReference, src));
                }

                // --- Types -> annotate (+ subclass, methods, properties) ---
                foreach (var typeInfo in module.Types)
                {
                    var clrType = ResolveClrType(typeInfo.ClrTypeName);
                    var isGeneric = clrType is { IsGenericType: true } || clrType is { IsGenericTypeDefinition: true }
                                    || typeInfo.Name.Contains('`', StringComparison.Ordinal);

                    string typeRef;
                    IReadOnlyDictionary<string, string>? typeSubst = null;
                    if (isGeneric)
                    {
                        // Render a CLOSED construction (SequenceMatcher[int], List[int], Deque[int])
                        // so the annotation binds instead of tripping CS0305 on the open definition.
                        // Every type parameter is filled with `int`; where the CLR type resolves we
                        // also build a subst map so receiver-position methods with T-typed parameters
                        // can be exercised against the closed receiver.
                        var arity = GenericArity(typeInfo, clrType);
                        var cleanName = CleanGenericName(typeInfo.Name);
                        if (arity is not > 0 || !IsUsableTypeName(cleanName))
                        {
                            RecordNotAttempted(moduleName, typeInfo.Name, "type", PosAnnotate, "generic type: arity/name not synthesizable");
                            continue;
                        }
                        typeRef = $"{qualifier}{cleanName}[{string.Join(", ", Enumerable.Repeat("int", arity.Value))}]";

                        // Core-internal generic types (List, Set, iterators, views) are not
                        // annotatable under their discovered CamelCase names — the user-facing
                        // spelling is `list`/`set`/…, so `List[int]` yields SPY0202. Probe the
                        // closed annotation once and skip the whole type when it doesn't resolve,
                        // so its methods/properties/subclass don't each fail downstream.
                        var probe = $"{importLine}def _probe(x: {typeRef}) -> None:\n    pass\n";
                        if (!CompilesClean(api, probe))
                        {
                            RecordNotAttempted(moduleName, typeInfo.Name, "type", PosAnnotate, "generic type not user-annotatable under discovered name");
                            continue;
                        }

                        if (clrType != null)
                            typeSubst = clrType.GetGenericArguments()
                                .ToDictionary(a => a.Name, _ => "int", StringComparer.Ordinal);
                    }
                    else
                    {
                        if (!IsUsableTypeName(typeInfo.Name))
                        {
                            RecordNotAttempted(moduleName, typeInfo.Name, "type", PosAnnotate, "unrenderable type name");
                            continue;
                        }
                        typeRef = qualifier + typeInfo.Name;
                    }

                    membersEnumerated++;

                    snippets.Add(new Snippet(asmName, moduleName, "type", typeInfo.Name, PosAnnotate,
                        $"{importLine}def _use(x: {typeRef}) -> None:\n    pass\n"));

                    // Match position: scrutinize the annotate-position variable with a wildcard arm
                    // (exhaustive by construction, so no per-type pattern synthesis is needed).
                    snippets.Add(new Snippet(asmName, moduleName, "type", typeInfo.Name, PosMatch,
                        $"{importLine}def _use(x: {typeRef}) -> None:\n    match x:\n        case _:\n            pass\n"));

                    if (IsSubclassable(typeInfo, clrType))
                    {
                        snippets.Add(new Snippet(asmName, moduleName, "type", typeInfo.Name, PosSubclass,
                            $"{importLine}class _Sub({typeRef}):\n    pass\n"));
                    }

                    // Index position: a read x[key] for types exposing a gettable indexer. The key
                    // comes from the indexer's parameter type — synthesized directly, or via the
                    // closed receiver's type-arg substitution for a generic key (Dict this[TKey]).
                    if (clrType != null)
                    {
                        var indexer = clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(p => p.GetIndexParameters().Length > 0 && p.GetGetMethod() != null);
                        if (indexer != null)
                        {
                            var key = TrySynthesizeKeyLiteral(indexer.GetIndexParameters()[0].ParameterType, typeSubst);
                            if (key != null)
                                snippets.Add(new Snippet(asmName, moduleName, "type", typeInfo.Name, PosIndex,
                                    $"{importLine}def _use(recv: {typeRef}) -> None:\n    _x = recv[{key}]\n"));
                            else
                                RecordNotAttempted(moduleName, typeInfo.Name, "type", PosIndex, "non-synthesizable indexer key type");
                        }
                    }

                    // Instance methods (receiver supplied by a typed parameter — no ctor needed).
                    foreach (var group in typeInfo.Methods.GroupBy(m => m.Name))
                    {
                        // Compiler-generated members (records' `<Clone>$`, backing fields) are not
                        // a real public surface; skip anything that isn't a plain identifier.
                        if (!IsIdentifier(group.Key))
                        {
                            RecordNotAttempted(moduleName, $"{typeInfo.Name}.{group.Key}", "method", PosMethod, "non-identifier / compiler-generated member");
                            continue;
                        }

                        // Dunder methods are invoked through operators/protocols, not directly
                        // (SPY0427); the sweep exercises them via their protocol surface instead.
                        if (group.Key.StartsWith("__", StringComparison.Ordinal) && group.Key.EndsWith("__", StringComparison.Ordinal))
                        {
                            RecordNotAttempted(moduleName, $"{typeInfo.Name}.{group.Key}", "method", PosMethod, "dunder invoked via protocol, not directly");
                            continue;
                        }

                        membersEnumerated++;
                        // Instance methods now accept explicit type args (recv.m[T](…) →
                        // recv.M<T>(…), #1133), so a generic instance method renders and is
                        // enforced exactly like a module-level generic function.
                        var chosen = PickCallableOverload(group.ToList(), allowGenericTypeArgs: true, typeSubst, out var margs, out var mTypeArgs, out var mReason);
                        if (chosen != null)
                        {
                            var callExpr = mTypeArgs != null
                                ? $"recv.{group.Key}[{mTypeArgs}]({margs})"
                                : $"recv.{group.Key}({margs})";
                            var body = CallStatement(callExpr, chosen.ReturnType);
                            var src = $"{importLine}def _use(recv: {typeRef}) -> None:\n    {body}\n";
                            snippets.Add(new Snippet(asmName, moduleName, "method", $"{typeInfo.Name}.{group.Key}", PosMethod, src));
                        }
                        else
                        {
                            RecordNotAttempted(moduleName, $"{typeInfo.Name}.{group.Key}", "method", PosMethod, mReason);
                        }
                    }

                    // Instance properties (Sharpy name is the reverse-mangled CLR name).
                    foreach (var prop in typeInfo.Properties)
                    {
                        var sharpyProp = NameMangler.ToSharpyName(prop.Name, ReverseNameContext.Property);
                        if (!IsIdentifier(sharpyProp))
                        {
                            RecordNotAttempted(moduleName, $"{typeInfo.Name}.{sharpyProp}", "property", PosProperty, "non-identifier / compiler-generated member");
                            continue;
                        }
                        membersEnumerated++;
                        var src = $"{importLine}def _use(recv: {typeRef}) -> None:\n    _x = recv.{sharpyProp}\n";
                        snippets.Add(new Snippet(asmName, moduleName, "property", $"{typeInfo.Name}.{sharpyProp}", PosProperty, src));
                    }
                }
            }
        }

        // Optional cap for local iteration (0 = all).
        var limit = ReadIntEnv("INTEROP_SWEEP_LIMIT", 0);
        if (limit > 0 && snippets.Count > limit)
            snippets = snippets.Take(limit).ToList();

        var doCSharpBind = ReadIntEnv("INTEROP_SWEEP_NO_CSHARP", 0) == 0;
        var dop = Math.Max(1, ReadIntEnv("INTEROP_SWEEP_DOP", Math.Min(4, Environment.ProcessorCount)));

        // Each CompilerApi.Compile builds its own Compiler/ModuleRegistry (stateless across
        // calls); the only shared mutable state is the process-safe on-disk OverloadIndex
        // cache, and derived CSharpCompilations off the immutable base are thread-safe — so
        // the sweep parallelizes safely to keep the CI-only baseline run tractable.
        var evaluated = new ConcurrentBag<FailureRecord>();
        Parallel.ForEach(snippets, new ParallelOptions { MaxDegreeOfParallelism = dop }, snippet =>
        {
            var record = Evaluate(api, csharpBase, snippet, doCSharpBind);
            if (record != null)
                evaluated.Add(record);
        });

        var refusals = evaluated.Where(r => r.Stage == "refused").ToList();
        var failures = evaluated.Where(r => r.Stage != "crash" && r.Stage != "refused").ToList();
        var crashes = evaluated.Where(r => r.Stage == "crash").ToList();
        var byPosition = snippets.GroupBy(s => s.Position).ToDictionary(g => g.Key, g => new PositionStats { Generated = g.Count() });
        foreach (var record in evaluated)
        {
            if (byPosition.TryGetValue(record.Snippet.Position, out var st))
            {
                if (record.Stage == "crash")
                    st.Crash++;
                else if (record.Stage == "refused")
                    st.Refused++;
                else
                    st.Fail++;
            }
        }
        foreach (var st in byPosition.Values)
            st.Pass = st.Generated - st.Fail - st.Crash - st.Refused;
        // Fold the notAttempted tally into the per-position breakdown so each position reports
        // pass/fail/crash AND notAttempted (a position may be entirely notAttempted with no
        // generated snippet, so create the entry if absent).
        foreach (var ((position, _), count) in notAttemptedByReason)
        {
            if (!byPosition.TryGetValue(position, out var st))
            {
                st = new PositionStats();
                byPosition[position] = st;
            }
            st.NotAttempted += count;
        }
        var passCount = snippets.Count - failures.Count - crashes.Count - refusals.Count;

        // Ratchet: any non-allowlisted failure/crash fails CI once an allowlist exists.
        var allowlist = LoadAllowlist();
        var offenders = failures.Concat(crashes)
            .Where(f => !allowlist.Matches(f.Snippet.Key))
            .ToList();
        var failingKeys = new HashSet<string>(
            failures.Concat(crashes).Select(f => f.Snippet.Key), StringComparer.Ordinal);
        var stale = allowlist.ExactKeys
            .Where(k => !failingKeys.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        WriteReport(new
        {
            summaryStats = new
            {
                assembliesSwept = 2,
                modulesSwept = snippets.Select(s => s.Module).Distinct().Count(),
                membersEnumerated,
                snippetsGenerated = snippets.Count,
                pass = passCount,
                fail = failures.Count,
                crash = crashes.Count,
                refusedByDesign = refusals.Count,
                notAttempted = notAttempted.Count,
                skippedModules = skippedModules.Count,
                allowlistSize = allowlist.Count,
                nonAllowlistedFailures = offenders.Count,
                staleAllowlistEntries = stale.Count,
            },
            ratchetMode = AllowlistFileExists(),
            byPosition = byPosition.ToDictionary(kv => kv.Key, kv => new { kv.Value.Generated, kv.Value.Pass, kv.Value.Fail, kv.Value.Crash, kv.Value.Refused, kv.Value.NotAttempted }),
            // Full (uncapped) per-position/per-reason notAttempted tally — the flat list below is
            // capped, so this is the authoritative source for bucket deltas.
            notAttemptedByReason = notAttemptedByReason
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new { position = kv.Key.Position, reason = kv.Key.Reason, count = kv.Value }),
            scopeNotes = new[]
            {
                "Usage positions: annotate, call, reference, method, property, subclass, index, match.",
                "Index reads x[key] for types exposing a gettable indexer (key synthesized from the indexer parameter, or the closed receiver's type-arg substitution); non-synthesizable key types are notAttempted.",
                "Match scrutinizes the annotate-position variable with a wildcard `case _:` arm (exhaustive by construction), so every rendered type gets a match snippet.",
                "Instance methods/properties use a typed parameter as the receiver, so no constructor is exercised.",
                "Generic functions/methods are called with synthesized type arguments (unconstrained/struct/new() -> int, class -> str); constraint-blocked overloads stay notAttempted (constraint synthesis future scope).",
                "Generic types are annotated/subclassed as a closed construction with every type parameter filled with int (List[int], Deque[int]); receiver-position methods substitute those args for T-typed parameters.",
                "Members with non-synthesizable required parameters are counted as notAttempted, not failures.",
            },
            crashes = crashes.Take(100).Select(f => f.ToReport(allowlist)),
            failures = failures.Take(500).Select(f => f.ToReport(allowlist)),
            refusedByDesign = refusals.Take(500).Select(f => f.ToReport(allowlist)),
            staleAllowlistEntries = stale,
            notAttempted = notAttempted.Take(200),
            skippedModules,
        });

        // A flat list of every failing key, to seed / audit the allowlist.
        WriteFailureKeys(failures.Concat(crashes).Select(f => f.Snippet.Key).Distinct().OrderBy(k => k, StringComparer.Ordinal));

        _output.WriteLine($"Members enumerated: {membersEnumerated}");
        _output.WriteLine($"Snippets generated: {snippets.Count}");
        _output.WriteLine($"Pass: {passCount}  Fail: {failures.Count}  Crash: {crashes.Count}  Refused-by-design: {refusals.Count}  NotAttempted: {notAttempted.Count}");
        _output.WriteLine($"Allowlist size: {allowlist.Count}  Non-allowlisted failures: {offenders.Count}  Stale allowlist entries: {stale.Count}");

        // Enumeration sanity always holds (a bridge that discovers nothing is itself a bug).
        Assert.True(membersEnumerated > 0, "Interop sweep enumerated zero members — discovery is broken.");
        Assert.True(snippets.Count > 0, "Interop sweep generated zero snippets.");

        // Ratchet only engages once a reviewed allowlist is committed (the baseline commit).
        if (AllowlistFileExists())
        {
            Assert.True(offenders.Count == 0,
                "Interop conformance ratchet: the following (member, position) snippets fail to compile and are " +
                "not on the reviewed allowlist. Either fix the bridge / file an issue, or add a justified " +
                "allowlist entry.\n" +
                string.Join("\n", offenders.Take(50).Select(o => $"  {o.Snippet.Key} [{o.Stage}] {o.Diagnostics.FirstOrDefault()}")) +
                $"\nFull list: .claude/tmp/interop-conformance-report.json");

            Assert.True(stale.Count == 0,
                $"{stale.Count} allowlist entr{(stale.Count == 1 ? "y" : "ies")} no longer " +
                "fail — whatever fixed them must also delete the entries " +
                "(docs/design/gap-discovery-contracts.md). Delete these lines from " +
                "Conformance/interop-allowlist.txt:\n  " +
                string.Join("\n  ", stale));
        }
    }

    // ================================================================================
    // Re-representation matrix (#1146 class)
    // ================================================================================

    /// <summary>
    /// One cell of the re-representation matrix: a spelling, the verdict it must draw, and — when
    /// the verdict is a refusal — a substring of the diagnostic that names the rule.
    /// </summary>
    /// <param name="Row">The type family and the direction it crosses in.</param>
    /// <param name="Seam">Where the value crosses: argument-in, return-out, element, scalar.</param>
    private sealed record ReRepresentationCell(
        string Row, string Seam, string Source, bool Compiles, string? Refusal = null)
    {
        public string Key => $"{Row}::{Seam}";
    }

    /// <summary>
    /// The two type families whose Sharpy spelling and CLR representation DIFFER — a CLR
    /// <c>char</c> against Sharpy <c>str</c> (Sharpy has no char), and <c>Nullable&lt;T&gt;</c>
    /// against <c>T | None</c> — swept across every seam a value crosses: an argument going IN to
    /// .NET, a value coming OUT of it, a collection ELEMENT, and a bare SCALAR.
    ///
    /// <para>
    /// The contract is #1146's, in full: every cell either compiles or is refused by a deliberate
    /// Sharpy diagnostic. Neither verdict is "unknown", and <b>SPY0908 is a failure whichever way the
    /// cell is supposed to go</b> — an ICE is the compiler reporting its own bug for a spelling it
    /// was asked about, which is the class this matrix exists to keep drained.
    /// </para>
    ///
    /// <para>
    /// Curated rather than generated, and deliberately so: the generated sweep above enumerates
    /// members, and a re-representation is a property of the TYPE at a POSITION, which no member
    /// enumeration reaches. The two are complementary and share the same compile-or-refuse bar; this
    /// one asserts per cell rather than ratcheting, so it needs no allowlist and must never grow one.
    /// </para>
    /// </summary>
    private static IEnumerable<ReRepresentationCell> ReRepresentationCells()
    {
        // ── char -> str: a CLR char coming OUT of .NET is the one-character str Sharpy means (#1291).
        yield return new ReRepresentationCell("char->str", "scalar",
            "def _use() -> None:\n    s = \"abc\"\n    x: str = s.element_at(1)\n    _n = len(x)\n", true);

        yield return new ReRepresentationCell("char->str", "return-out",
            "def _use() -> None:\n    cs: array[str] = \"hello\".to_char_array()\n    _n = len(cs)\n", true);

        // The element seam: an IEnumerable<char> materializing into a list[str] slot (#1401).
        yield return new ReRepresentationCell("char->str", "element",
            "def _use() -> None:\n    s = \"abcdef\"\n    xs: list[str] = list(s.take(3))\n    _n = len(xs)\n", true);

        yield return new ReRepresentationCell("char->str", "argument-in",
            "def _take(v: str) -> None:\n    pass\n\n\n"
            + "def _use() -> None:\n    cs = \"hello\".to_char_array()\n    _take(cs[1])\n", true);

        // ── str -> char: the reverse direction, decided at the call seam on the parameter (#1402).
        yield return new ReRepresentationCell("str->char", "argument-in",
            "from system import Char\n\n\ndef _use() -> None:\n    _x = Char.to_upper(\"a\")\n", true);

        yield return new ReRepresentationCell("str->char", "argument-in-multichar",
            "from system import Char\n\n\ndef _use() -> None:\n    _x = Char.to_upper(\"abc\")\n",
            false, "takes a CLR 'char'");

        yield return new ReRepresentationCell("str->char", "argument-in-computed",
            "from system import Char\n\n\ndef _use() -> None:\n    s = \"a\" + \"b\"\n    _x = Char.to_upper(s)\n",
            false, "takes a CLR 'char'");

        yield return new ReRepresentationCell("str->char", "return-out",
            "from system import Char\n\n\ndef _use() -> None:\n    x: str = Char.to_upper(\"a\")\n    _n = len(x)\n", true);

        // ── Nullable<T> -> T | None: a CLR nullable coming OUT of .NET.
        yield return new ReRepresentationCell("nullable->union", "return-out",
            "from sharpy import SliceSpec\n\n\ndef _use() -> None:\n    s = SliceSpec(1, 5, 2)\n    _n: int | None = s.stop\n", true);

        // A value payload into a plain-T formal is refused: `Nullable<int>` has no implicit
        // conversion to `int`, and accepting it was CS1503 behind SPY0908 (#1399).
        yield return new ReRepresentationCell("nullable->union", "scalar-value-payload",
            "def _twice(v: int) -> int:\n    return v * 2\n\n\n"
            + "def _use() -> None:\n    x: int | None = 5\n    _y = _twice(x)\n",
            false, "Cannot pass argument of type 'int32?' to parameter of type 'int32'");

        // A REFERENCE payload stays loose on purpose: `string?` IS `string` at runtime, and
        // tagged_unions_optional.md documents the looseness. This cell must never flip.
        yield return new ReRepresentationCell("nullable->union", "scalar-reference-payload",
            "def _shout(s: str) -> str:\n    return s.upper()\n\n\n"
            + "def _use() -> None:\n    v: str | None = \"hi\"\n    _y = _shout(v)\n", true);

        yield return new ReRepresentationCell("nullable->union", "element",
            "def _use() -> None:\n    xs: list[int | None] = [1, None, 3]\n    _n = len(xs)\n", true);

        // ── T | None -> Nullable<T>: the union going IN to a .NET nullable formal.
        yield return new ReRepresentationCell("union->nullable", "argument-in",
            "import collections\n\n\ndef _use() -> None:\n"
            + "    c = collections.Counter[str]([\"a\", \"b\", \"a\"])\n"
            + "    n: int | None = 1\n    _x = c.most_common(n)\n", true);

        yield return new ReRepresentationCell("union->nullable", "argument-in-plain",
            "import collections\n\n\ndef _use() -> None:\n"
            + "    c = collections.Counter[str]([\"a\", \"b\", \"a\"])\n"
            + "    _x = c.most_common(1)\n", true);

        // Boxing is a conversion .NET really performs, so it stays accepted.
        yield return new ReRepresentationCell("union->nullable", "scalar-boxing",
            "def _show(v: object) -> str:\n    return str(v)\n\n\n"
            + "def _use() -> None:\n    n: int | None = 5\n    _y = _show(n)\n", true);

        yield return new ReRepresentationCell("union->nullable", "return-out",
            "def _pick(flag: bool) -> int | None:\n    if flag:\n        return 5\n    return None\n\n\n"
            + "def _use() -> None:\n    _x: int | None = _pick(True)\n", true);
    }

    [Fact]
    public void ReRepresentationMatrix_CharAndNullableSeams_CompileOrRefuseDeliberately()
    {
        var (corePath, stdlibPath) = ResolveStdlibAssemblyPaths();
        Assert.True(File.Exists(corePath), $"Sharpy.Core.dll not found at {corePath}");
        Assert.True(File.Exists(stdlibPath), $"Sharpy.Stdlib.dll not found at {stdlibPath}");

        var api = new CompilerApi(NullLogger.Instance, new[] { corePath, stdlibPath });
        var csharpBase = BuildCSharpBaseCompilation();

        var failures = new List<string>();
        var cells = ReRepresentationCells().ToList();

        foreach (var cell in cells)
        {
            CompileResult result;
            try
            {
                result = api.Compile(cell.Source, new CompilerOptions { OutputType = "library" });
            }
            catch (Exception ex)
            {
                failures.Add($"{cell.Key}: crashed — {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var errors = result.Diagnostics
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
                .ToList();

            // An ICE fails the cell whichever verdict it was supposed to draw.
            if (errors.Any(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError))
            {
                failures.Add($"{cell.Key}: SPY0908 — {errors[0].Message}");
                continue;
            }

            if (cell.Compiles)
            {
                if (errors.Count > 0)
                {
                    failures.Add($"{cell.Key}: expected to compile but drew {errors[0].Code}: {errors[0].Message}");
                    continue;
                }

                // The Sharpy phase being clean is only half the contract: the emitted C# must bind,
                // or the same disagreement is an SPY0908 waiting for a `run`.
                if (result.GeneratedCSharp != null)
                {
                    var csErrors = BindGeneratedCSharp(csharpBase, result.GeneratedCSharp);
                    if (csErrors.Count > 0)
                        failures.Add($"{cell.Key}: generated C# does not bind — {csErrors[0]}");
                }

                continue;
            }

            if (errors.Count == 0)
            {
                failures.Add($"{cell.Key}: expected a deliberate refusal naming '{cell.Refusal}' but it compiled");
                continue;
            }

            if (cell.Refusal != null
                && !errors.Any(d => d.Message.Contains(cell.Refusal, StringComparison.Ordinal)))
            {
                failures.Add(
                    $"{cell.Key}: refused, but not by the expected rule — wanted '{cell.Refusal}', "
                    + $"got {errors[0].Code}: {errors[0].Message}");
            }
        }

        _output.WriteLine($"Re-representation cells: {cells.Count}  Failures: {failures.Count}");

        Assert.True(failures.Count == 0,
            "Re-representation matrix (#1146 class): every cell must compile or be refused by a "
            + "deliberate Sharpy diagnostic — never SPY0908.\n" + string.Join("\n", failures.Select(f => "  " + f)));
    }

    /// <summary>
    /// Compiles one snippet (Sharpy → C#) and, on success, binds the generated C# through
    /// Roslyn. Returns a <see cref="FailureRecord"/> on failure or crash, or null on a clean pass.
    /// </summary>
    private static FailureRecord? Evaluate(CompilerApi api, CSharpCompilation csharpBase, Snippet snippet, bool doCSharpBind)
    {
        CompileResult result;
        try
        {
            result = api.Compile(snippet.Source, new CompilerOptions { OutputType = "library" });
        }
        catch (Exception ex)
        {
            return new FailureRecord(snippet, "crash", new[] { $"{ex.GetType().Name}: {ex.Message}" });
        }

        var errors = result.Diagnostics
            .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
            .Select(d => $"{d.Code}: {d.Message}")
            .Distinct()
            .Take(5)
            .ToList();

        if (errors.Count > 0)
        {
            // An SPY0215-only error set is the deliberate builtin-exception surface refusal
            // (#1515) — a by-design verdict, not a bridge gap. Mixed error sets stay failures.
            var stage = errors.All(e => e.StartsWith(
                    DiagnosticCodes.Semantic.BuiltinExceptionClrMemberRefused + ":", StringComparison.Ordinal))
                ? "refused"
                : "sharpy";
            return new FailureRecord(snippet, stage, errors);
        }

        if (doCSharpBind && result.GeneratedCSharp != null)
        {
            var csErrors = BindGeneratedCSharp(csharpBase, result.GeneratedCSharp);
            if (csErrors.Count > 0)
                return new FailureRecord(snippet, "csharp", csErrors);
        }

        return null;
    }

    // ---- snippet generation helpers ----

    /// <summary>
    /// Picks the most-callable overload and builds a positional argument list: parameters
    /// with defaults or params-arrays are omitted (fewest-args strategy); each remaining
    /// required parameter gets a synthesized literal. Generic overloads are pinned via
    /// <see cref="TrySynthesizeTypeArguments"/> — <paramref name="typeArgs"/> then carries the
    /// rendered <c>T1, …</c> list for a <c>func[T1, …](…)</c> call, or null for a non-generic
    /// call. Returns the chosen signature (so the caller can honor its return type), or null if
    /// no overload is callable — in which case <paramref name="notAttemptedReason"/> is the
    /// narrowed reason to record.
    /// <para>
    /// Explicit type arguments are only rendered when <paramref name="allowGenericTypeArgs"/> is
    /// true AND the overload set is unambiguous: exactly one generic arity and no non-generic
    /// sibling. A non-generic sibling makes Sharpy prefer it and reject the type args (e.g.
    /// <c>len</c> binds <c>(object) -> int</c>); mixed arities make the count ambiguous (e.g.
    /// <c>itertools.product</c>). In those cases generic overloads are not rendered and the member
    /// stays notAttempted. Since #1133 both module-level functions and instance methods pass
    /// <see langword="true"/> — instance generic-method calls (<c>recv.m[T](…)</c> →
    /// <c>recv.M&lt;T&gt;(…)</c>) are rendered and enforced like any other generic call.
    /// </para>
    /// </summary>
    private static FunctionSignature? PickCallableOverload(
        IReadOnlyList<FunctionSignature> overloads,
        bool allowGenericTypeArgs,
        IReadOnlyDictionary<string, string>? typeSubst,
        out string args,
        out string? typeArgs,
        out string notAttemptedReason)
    {
        var genericArities = overloads.Where(s => s.TypeParameters.Count > 0)
            .Select(s => s.TypeParameters.Count)
            .Distinct()
            .ToList();
        var hasNonGeneric = overloads.Any(s => s.TypeParameters.Count == 0);
        var genericArgsSafe = allowGenericTypeArgs && genericArities.Count == 1 && !hasNonGeneric;

        FunctionSignature? best = null;
        string? bestArgs = null;
        string? bestTypeArgs = null;
        var bestCount = int.MaxValue;
        var sawParamFailure = false;
        var sawUnsynthesizableConstraint = false;
        var sawUnsafeGeneric = false;

        foreach (var sig in overloads)
        {
            // A closed-generic receiver pins the type's own parameters (T -> int); method-level
            // type args, when rendered, layer on top.
            IReadOnlyDictionary<string, string>? subst = typeSubst;
            string? renderedTypeArgs = null;
            if (sig.TypeParameters.Count > 0)
            {
                if (!genericArgsSafe)
                {
                    // Instance-method position, or an ambiguous generic overload set: rendering
                    // explicit type args here would not bind. Skip this overload.
                    sawUnsafeGeneric = true;
                    continue;
                }
                var synthesized = TrySynthesizeTypeArguments(sig.TypeParameters);
                if (synthesized == null)
                {
                    // Interface/base-constrained (or class+new()) type params: out of scope.
                    sawUnsynthesizableConstraint = true;
                    continue;
                }
                var merged = typeSubst == null
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    : new Dictionary<string, string>(typeSubst, StringComparer.Ordinal);
                for (var i = 0; i < sig.TypeParameters.Count; i++)
                    merged[sig.TypeParameters[i].Name] = synthesized[i];
                subst = merged;
                renderedTypeArgs = string.Join(", ", synthesized);
            }

            var parts = new List<string>();
            var ok = true;
            foreach (var p in sig.Parameters)
            {
                if (p.IsVariadic || p.HasDefault)
                    continue;
                var lit = TrySynthesizeLiteral(p.Type, subst);
                if (lit == null)
                { ok = false; break; }
                parts.Add(lit);
            }
            if (!ok)
            {
                sawParamFailure = true;
                continue;
            }
            if (parts.Count < bestCount)
            {
                bestCount = parts.Count;
                bestArgs = string.Join(", ", parts);
                bestTypeArgs = renderedTypeArgs;
                best = sig;
                // A zero-arg non-generic call is already optimal; a generic one still has to
                // render its type args, so keep scanning for a non-generic alternative.
                if (bestCount == 0 && renderedTypeArgs == null)
                    break;
            }
        }

        args = bestArgs ?? "";
        typeArgs = bestTypeArgs;
        // Precedence: a genuine non-synthesizable parameter dominates; then generic constraint
        // synthesis being out of scope; then an unsafe generic overload set (an ambiguous
        // arity / non-generic-sibling set — no longer the #1133 instance-method carve-out, which
        // now renders like any generic call).
        notAttemptedReason =
            sawParamFailure ? "non-synthesizable required parameter"
            : sawUnsynthesizableConstraint ? "constraint synthesis future scope"
            : sawUnsafeGeneric ? "generic overload set — type-argument synthesis future scope"
            : "non-synthesizable required parameter";
        return best;
    }

    /// <summary>
    /// Wraps a call expression as a statement body. A non-void result is assigned (some
    /// builtins like <c>len</c> lower to a property access, which is not a valid bare
    /// statement — CS0201); a void result is emitted as a bare call.
    /// </summary>
    private static string CallStatement(string call, TypeSignature returnType)
        => IsVoidReturn(returnType) ? call : $"_x = {call}";

    private static bool IsVoidReturn(TypeSignature returnType)
        => string.IsNullOrEmpty(returnType.Name) || returnType.Name is "None" or "void";

    private static readonly HashSet<string> IntegerTypeNames = new(StringComparer.Ordinal)
    {
        "int", "long", "int8", "int16", "int32", "int64",
        "uint8", "uint16", "uint32", "uint64", "byte", "sbyte", "short", "ushort", "uint", "ulong", "nint", "nuint",
    };

    /// <summary>
    /// Produces a Sharpy literal of the given type, or null when the type is not one we
    /// can safely construct a value for (in which case the enclosing call is not attempted).
    /// Collection literals are element-typed and non-empty so inference is pinned — an empty
    /// literal (<c>[]</c>/<c>set()</c>) would trip SPY0227 and masquerade as a bridge failure.
    /// </summary>
    private static string? TrySynthesizeLiteral(TypeSignature t, IReadOnlyDictionary<string, string>? subst = null)
    {
        // Generic-parameter substitution: when rendering a call to a generic function/method
        // the caller pins each type parameter to a concrete Sharpy type; a parameter typed as
        // (or nesting) `T` resolves to that pinned type before literal synthesis.
        if (subst != null && t.IsGenericParameter && subst.TryGetValue(t.Name, out var concrete))
            return TrySynthesizeLiteral(new TypeSignature { Name = concrete }, subst);

        if (t.Name == TypeSignature.NullableSentinel || t.Name == "Optional")
            return "None";

        var name = t.Name;
        var bracket = name.IndexOf('[', StringComparison.Ordinal);
        var baseName = (bracket >= 0 ? name.Substring(0, bracket) : name).Trim();

        if (IntegerTypeNames.Contains(baseName))
            return "0";

        switch (baseName)
        {
            case "float" or "double" or "float32" or "float64":
                return "0.0";
            case "bool":
                return "True";
            case "str":
                return "\"\"";
            case "bytes":
                return "b\"\"";
            case "list":
                return SynthElement(t, 0, subst) is { } le ? $"[{le}]" : null;
            case "set":
                return SynthElement(t, 0, subst) is { } se ? $"{{{se}}}" : null;
            case "dict":
                return t.TypeArguments.Count == 2
                       && SynthElement(t, 0, subst) is { } dk && SynthElement(t, 1, subst) is { } dv
                    ? $"{{{dk}: {dv}}}"
                    : null;
            case "tuple":
                if (t.TypeArguments.Count == 0)
                    return null;
                var elems = t.TypeArguments.Select(a => TrySynthesizeLiteral(a, subst)).ToList();
                return elems.All(e => e != null) ? "(" + string.Join(", ", elems) + ")" : null;
            default:
                return null;
        }
    }

    /// <summary>Synthesizes a literal for the <paramref name="index"/>th type argument, or null.</summary>
    private static string? SynthElement(TypeSignature t, int index, IReadOnlyDictionary<string, string>? subst = null)
        => index < t.TypeArguments.Count ? TrySynthesizeLiteral(t.TypeArguments[index], subst) : null;

    /// <summary>
    /// Produces a Sharpy literal for an indexer's key (parameter) CLR type, or null when it is
    /// not one we can construct. A generic key (e.g. Dict's <c>this[TKey]</c>) resolves through
    /// the closed receiver's type-arg substitution; concrete primitive key types map directly.
    /// </summary>
    private static string? TrySynthesizeKeyLiteral(Type keyType, IReadOnlyDictionary<string, string>? typeSubst)
    {
        if (keyType.IsGenericParameter)
            return typeSubst != null && typeSubst.TryGetValue(keyType.Name, out var concrete)
                ? TrySynthesizeLiteral(new TypeSignature { Name = concrete })
                : null;

        return keyType.FullName switch
        {
            "System.Int32" or "System.Int64" or "System.Int16" or "System.SByte" or "System.Byte"
                or "System.UInt16" or "System.UInt32" or "System.UInt64" or "System.IntPtr" or "System.UIntPtr" => "0",
            "System.String" => "\"\"",
            "System.Boolean" => "True",
            "System.Double" or "System.Single" => "0.0",
            _ => null,
        };
    }

    /// <summary>
    /// Synthesizes concrete Sharpy type-argument spellings for a generic function/method's
    /// type parameters so the sweep can render an explicit <c>func[T1, …](…)</c> call, or
    /// null when any parameter's constraints put synthesis out of scope (the caller then
    /// records the member <c>notAttempted</c> with reason "constraint synthesis future scope").
    /// <para>
    /// Minimal v1 (plan D4): an unconstrained parameter, a value-type constraint
    /// (<c>T : struct</c>), or a bare <c>new()</c> constraint → <c>int</c>; a reference-type
    /// constraint (<c>T : class</c>) → <c>str</c>; an interface or base-class constraint, or a
    /// <c>class</c>+<c>new()</c> combination (<see cref="string"/> has no parameterless ctor),
    /// fails the whole synthesis. Reads constraint metadata from <see cref="TypeParameterInfo"/>
    /// (materialized into the OverloadIndex, #976) rather than a parallel <c>MethodInfo</c>
    /// reflection path, so the sweep sees the same generic surface the compiler does.
    /// </para>
    /// </summary>
    internal static string[]? TrySynthesizeTypeArguments(IReadOnlyList<TypeParameterInfo> typeParameters)
    {
        if (typeParameters.Count == 0)
            return Array.Empty<string>();

        var args = new string[typeParameters.Count];
        for (var i = 0; i < typeParameters.Count; i++)
        {
            var synth = TrySynthesizeTypeArgument(typeParameters[i]);
            if (synth == null)
                return null;
            args[i] = synth;
        }
        return args;
    }

    /// <summary>Maps one type parameter's constraints to a synthesizable Sharpy type, or null.</summary>
    private static string? TrySynthesizeTypeArgument(TypeParameterInfo tp)
    {
        // Interface constraints require a type satisfying an arbitrary contract — future scope.
        // Note: OverloadIndexBuilder keeps only interface types in InterfaceConstraints
        // (.Where(c => c.IsInterface)); a base-class constraint (T : SomeBase) is not recorded on
        // TypeParameterInfo at all, so it would be treated as unconstrained here and synthesize
        // `int` — the snippet then fails to compile and trips the ratchet loudly. No base-class-
        // constrained generic exists in the swept assemblies today.
        if (tp.InterfaceConstraints.Count > 0)
            return null;

        // A reference-type constraint (`T : class`) rules out int; `str` (System.String) is a
        // reference type. But `class` + `new()` together need a reference type with a public
        // parameterless ctor, which String lacks — bail rather than emit code Roslyn rejects.
        if (tp.HasReferenceTypeConstraint)
            return tp.HasDefaultConstructorConstraint ? null : "str";

        // Value-type (`T : struct`), bare `new()`, or unconstrained: int satisfies all three.
        return "int";
    }

    /// <summary>Rejects open-generic / unrenderable type names for annotation positions.</summary>
    private static bool IsUsableTypeName(string name)
        => !string.IsNullOrEmpty(name)
           && name.IndexOfAny(new[] { '`', '<', '[', '+', '.' }) < 0;

    /// <summary>
    /// Number of type parameters to fill for a closed-generic construction, from the resolved
    /// CLR type when available (authoritative) or the discovered name's <c>`N</c> arity suffix,
    /// or null when neither yields a positive arity.
    /// </summary>
    private static int? GenericArity(DiscoveredTypeInfo typeInfo, Type? clrType)
    {
        if (clrType is { IsGenericType: true })
            return clrType.GetGenericArguments().Length;
        var tick = typeInfo.Name.IndexOf('`', StringComparison.Ordinal);
        if (tick >= 0 && int.TryParse(typeInfo.Name.AsSpan(tick + 1), out var n) && n > 0)
            return n;
        return null;
    }

    /// <summary>Strips the <c>`N</c> arity suffix so a generic name renders as a bare identifier.</summary>
    private static string CleanGenericName(string name)
    {
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick >= 0 ? name.Substring(0, tick) : name;
    }

    /// <summary>True when the name is a plain identifier (letters/digits/underscore, no leading digit).</summary>
    private static bool IsIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsDigit(name[0]))
            return false;
        foreach (var ch in name)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '_')
                return false;
        }
        return true;
    }

    private static bool IsSubclassable(DiscoveredTypeInfo typeInfo, Type? clrType)
    {
        if (typeInfo.TypeKind != "Class")
            return false;
        if (clrType == null)
            return false;
        if (clrType.IsSealed || clrType.IsAbstract)
            return false;
        // Only classes with an accessible parameterless constructor: `class _Sub(T): pass`
        // must not need to forward base-constructor arguments. Generic bases are subclassed
        // through their closed construction (`class _Sub(Deque[int]): pass`); the open
        // definition's parameterless ctor carries over to the closed type.
        return clrType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(c => (c.IsPublic || c.IsFamily) && c.GetParameters().Length == 0);
    }

    private static Type? ResolveClrType(string clrTypeName)
    {
        if (string.IsNullOrEmpty(clrTypeName))
            return null;
        var t = Type.GetType(clrTypeName);
        if (t != null)
            return t;
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(clrTypeName))
            .FirstOrDefault(x => x != null);
    }

    private static bool ImportsClean(CompilerApi api, string moduleName)
        => CompilesClean(api, $"import {moduleName}\n\ndef _p() -> None:\n    pass\n");

    /// <summary>True when <paramref name="source"/> compiles (Sharpy phase) with no errors.</summary>
    private static bool CompilesClean(CompilerApi api, string source)
    {
        try
        {
            var r = api.Compile(source, new CompilerOptions { OutputType = "library" });
            return r.Diagnostics.All(d => d.Severity != CompilerDiagnosticSeverity.Error);
        }
        catch
        {
            return false;
        }
    }

    // ---- Roslyn C# bind of the generated code ----

    private static CSharpCompilation BuildCSharpBaseCompilation()
    {
        var refs = new List<MetadataReference>(IntegrationTestBase.GetSharedReferences());
        var seen = refs.OfType<PortableExecutableReference>()
            .Select(r => Path.GetFileName(r.FilePath))
            .Where(n => n != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Add every DLL next to the test assembly (Sharpy.Stdlib + MathNet/Sqlite/Tomlyn/Yaml …)
        // so generated C# that touches any stdlib dependency binds without a missing-reference
        // masquerading as a codegen leak.
        var binDir = Path.GetDirectoryName(typeof(InteropConformanceTests).Assembly.Location)!;
        foreach (var dll in Directory.GetFiles(binDir, "*.dll"))
        {
            var fileName = Path.GetFileName(dll);
            if (!seen.Add(fileName))
                continue;
            try
            { refs.Add(MetadataReference.CreateFromFile(dll)); }
            catch { /* not a managed assembly */ }
        }

        return CSharpCompilation.Create(
            "InteropSweepBase",
            Array.Empty<SyntaxTree>(),
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static List<string> BindGeneratedCSharp(CSharpCompilation baseCompilation, string generatedCSharp)
    {
        var tree = CSharpSyntaxTree.ParseText(generatedCSharp);
        var compilation = baseCompilation.AddSyntaxTrees(tree);
        return compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .Distinct()
            .Take(5)
            .ToList();
    }

    // ---- allowlist + report I/O ----

    private static readonly Lazy<string?> AllowlistPath = new(FindAllowlistPath);

    private static bool AllowlistFileExists() => AllowlistPath.Value != null && File.Exists(AllowlistPath.Value);

    private static Allowlist LoadAllowlist()
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var wildcards = new List<string>();
        var path = AllowlistPath.Value;
        if (path == null || !File.Exists(path))
            return new Allowlist(exact, wildcards);

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw;
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
                line = line.Substring(0, hash);
            line = line.Trim();
            if (line.Length == 0)
                continue;
            if (line.Contains('*', StringComparison.Ordinal))
                wildcards.Add(line);
            else
                exact.Add(line);
        }
        return new Allowlist(exact, wildcards);
    }

    private static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var candidate = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance", "interop-allowlist.txt");
            if (File.Exists(candidate))
                return candidate;
            // Also accept the directory existing even if the file isn't there yet, so we know
            // where it should live (report-only mode).
            var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
            if (Directory.Exists(dir))
                return candidate;
            current = Directory.GetParent(current)?.FullName;
        }
        return null;
    }

    private void WriteReport(object report)
    {
        var reportDir = ReportDir();
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "interop-conformance-report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine($"Report written to: {reportPath}");
    }

    private static void WriteFailureKeys(IEnumerable<string> keys)
    {
        var reportDir = ReportDir();
        Directory.CreateDirectory(reportDir);
        File.WriteAllLines(Path.Combine(reportDir, "interop-conformance-failures.txt"), keys);
    }

    private static string ReportDir()
        => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(InteropConformanceTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", ".claude", "tmp"));

    private static int ReadIntEnv(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : fallback;

    private static (string CorePath, string StdlibPath) ResolveStdlibAssemblyPaths()
    {
        var baseDir = Path.GetDirectoryName(typeof(InteropConformanceTests).Assembly.Location)!;
        var core = Path.Combine(baseDir, "Sharpy.Core.dll");
        var stdlib = Path.Combine(baseDir, "Sharpy.Stdlib.dll");
        return (core, stdlib);
    }

    // ---- records ----

    private sealed record Snippet(string Assembly, string Module, string MemberKind, string Member, string Position, string Source)
    {
        public string Key => $"{Module}::{MemberKind}::{Member}::{Position}";
    }

    private sealed record FailureRecord(Snippet Snippet, string Stage, IReadOnlyList<string> Diagnostics)
    {
        public object ToReport(Allowlist allowlist) => new
        {
            module = Snippet.Module,
            member = Snippet.Member,
            kind = Snippet.MemberKind,
            position = Snippet.Position,
            key = Snippet.Key,
            stage = Stage,
            allowlisted = allowlist.Matches(Snippet.Key),
            diagnostics = Diagnostics,
            snippet = Snippet.Source,
        };
    }

    /// <summary>
    /// The reviewed conformance allowlist: exact <c>module::kind::member::position</c> keys plus
    /// simple <c>*</c>-glob patterns (used to cover a whole known-unsupported surface — e.g. the
    /// source-generator plugin API — compactly, each with a justification comment in the file).
    /// </summary>
    private sealed class Allowlist
    {
        private readonly HashSet<string> _exact;
        private readonly List<string> _wildcards;

        public Allowlist(HashSet<string> exact, List<string> wildcards)
        {
            _exact = exact;
            _wildcards = wildcards;
        }

        public int Count => _exact.Count + _wildcards.Count;

        public IReadOnlyCollection<string> ExactKeys => _exact;

        public bool Matches(string key)
            => _exact.Contains(key) || _wildcards.Any(w => GlobMatch(key, w));

        private static bool GlobMatch(string text, string pattern)
        {
            var parts = pattern.Split('*');
            var pos = 0;
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length == 0)
                    continue;
                if (i == 0)
                {
                    if (!text.StartsWith(part, StringComparison.Ordinal))
                        return false;
                    pos = part.Length;
                }
                else
                {
                    var idx = text.IndexOf(part, pos, StringComparison.Ordinal);
                    if (idx < 0)
                        return false;
                    pos = idx + part.Length;
                }
            }
            // A pattern not ending in '*' must match through the end of the text.
            return pattern.EndsWith("*", StringComparison.Ordinal) || pos == text.Length;
        }
    }

    private sealed class PositionStats
    {
        public int Generated;
        public int Pass;
        public int Fail;
        public int Crash;
        public int Refused;
        public int NotAttempted;
    }

    // ================================================================================
    // Semantic-type fidelity axis (#1640, #1645)
    // ================================================================================

    private sealed record FidelityCell(
        string Label, string Source, bool ExpectsError, string? ErrorSubstring = null);

    private static IEnumerable<FidelityCell> FidelityCells()
    {
        yield return new FidelityCell("Stack[int].peek-correct",
            "from system.collections.generic import Stack\n\ndef _use() -> None:\n    s = Stack[int]()\n    s.push(1)\n    n: int = s.peek()\n",
            false);

        yield return new FidelityCell("Stack[int].peek-wrong-type",
            "from system.collections.generic import Stack\n\ndef _use() -> None:\n    s = Stack[int]()\n    s.push(1)\n    x: str = s.peek()\n",
            true, "Cannot assign type 'int32'");

        yield return new FidelityCell("Queue[int].peek-wrong-type",
            "from system.collections.generic import Queue\n\ndef _use() -> None:\n    q = Queue[int]()\n    q.enqueue(1)\n    x: str = q.peek()\n",
            true, "Cannot assign type 'int32'");

        yield return new FidelityCell("Stack[int].count-correct",
            "from system.collections.generic import Stack\n\ndef _use() -> None:\n    s = Stack[int]()\n    n: int = s.count\n",
            false);

        yield return new FidelityCell("Stack[Queue[int]].peek-nested",
            "from system.collections.generic import Stack, Queue\n\ndef _use() -> None:\n    s = Stack[Queue[int]]()\n    s.push(Queue[int]())\n    _q = s.peek()\n",
            false);
    }

    [Fact]
    public void SemanticTypeFidelity_UnmappedBclGenerics_ReturnReflectedType()
    {
        var (corePath, stdlibPath) = ResolveStdlibAssemblyPaths();
        var api = new CompilerApi(NullLogger.Instance, new[] { corePath, stdlibPath });

        var failures = new List<string>();
        var cells = FidelityCells().ToList();

        foreach (var cell in cells)
        {
            CompileResult result;
            try
            {
                result = api.Compile(cell.Source, new CompilerOptions { OutputType = "library" });
            }
            catch (Exception ex)
            {
                failures.Add($"{cell.Label}: crashed — {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var errors = result.Diagnostics
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
                .ToList();

            if (errors.Any(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError))
            {
                failures.Add($"{cell.Label}: SPY0908 — the member resolved to Unknown instead of its reflected type");
                continue;
            }

            if (cell.ExpectsError)
            {
                if (errors.Count == 0)
                {
                    failures.Add($"{cell.Label}: expected SPY0220 but compiled clean — the member type is still Unknown");
                    continue;
                }

                if (cell.ErrorSubstring != null
                    && !errors.Any(d => d.Message.Contains(cell.ErrorSubstring, StringComparison.Ordinal)))
                {
                    failures.Add(
                        $"{cell.Label}: expected '{cell.ErrorSubstring}', got {errors[0].Code}: {errors[0].Message}");
                }
            }
            else
            {
                if (errors.Count > 0)
                    failures.Add($"{cell.Label}: expected to compile but drew {errors[0].Code}: {errors[0].Message}");
            }
        }

        _output.WriteLine($"Fidelity cells: {cells.Count}  Failures: {failures.Count}");

        Assert.True(failures.Count == 0,
            "Semantic-type fidelity (#1640, #1645): every member access on a CLR-origin receiver must " +
            "have the reflected semantic type, so a mistyped destination is SPY0220 — never Unknown " +
            "(SPY0908 / silent wrong type).\n" +
            string.Join("\n", failures.Select(f => "  " + f)));
    }
}
