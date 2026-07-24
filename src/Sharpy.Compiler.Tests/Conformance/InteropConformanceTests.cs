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
/// </summary>
[Trait("Category", "GapDiscovery")]
public class InteropConformanceTests
{
    private readonly ITestOutputHelper _output;

    public InteropConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // Usage positions v1. index/match are recognized in the plan but deferred: a sound
    // scrutinee/key requires per-type synthesis that would otherwise generate false
    // failures. Tracked for a follow-up; see the sweep report's scopeNotes.
    private const string PosAnnotate = "annotate";
    private const string PosCall = "call";
    private const string PosReference = "reference";
    private const string PosMethod = "method";
    private const string PosProperty = "property";
    private const string PosSubclass = "subclass";

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
        var skippedModules = new List<object>();
        var membersEnumerated = 0;

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
                    var chosen = PickCallableOverload(sigs, allowGenericTypeArgs: true, out var args, out var typeArgs, out var reason);
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
                        notAttempted.Add(new { module = moduleName, member = funcName, kind = "function", position = PosCall, reason });
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

                    // Generic types need type-argument synthesis to render (annotating a bare
                    // `SequenceMatcher<T>` emits CS0305); that is future scope. The arity is
                    // often absent from the discovered Name, so also consult the CLR type.
                    if (!IsUsableTypeName(typeInfo.Name) || clrType is { IsGenericType: true }
                        || clrType is { IsGenericTypeDefinition: true })
                    {
                        notAttempted.Add(new { module = moduleName, member = typeInfo.Name, kind = "type", position = PosAnnotate, reason = "generic/unrenderable type name" });
                        continue;
                    }

                    membersEnumerated++;
                    var typeRef = qualifier + typeInfo.Name;

                    snippets.Add(new Snippet(asmName, moduleName, "type", typeInfo.Name, PosAnnotate,
                        $"{importLine}def _use(x: {typeRef}) -> None:\n    pass\n"));

                    if (IsSubclassable(typeInfo, clrType))
                    {
                        snippets.Add(new Snippet(asmName, moduleName, "type", typeInfo.Name, PosSubclass,
                            $"{importLine}class _Sub({typeRef}):\n    pass\n"));
                    }

                    // Instance methods (receiver supplied by a typed parameter — no ctor needed).
                    foreach (var group in typeInfo.Methods.GroupBy(m => m.Name))
                    {
                        // Compiler-generated members (records' `<Clone>$`, backing fields) are not
                        // a real public surface; skip anything that isn't a plain identifier.
                        if (!IsIdentifier(group.Key))
                        {
                            notAttempted.Add(new { module = moduleName, member = $"{typeInfo.Name}.{group.Key}", kind = "method", position = PosMethod, reason = "non-identifier / compiler-generated member" });
                            continue;
                        }

                        // Dunder methods are invoked through operators/protocols, not directly
                        // (SPY0427); the sweep exercises them via their protocol surface instead.
                        if (group.Key.StartsWith("__", StringComparison.Ordinal) && group.Key.EndsWith("__", StringComparison.Ordinal))
                        {
                            notAttempted.Add(new { module = moduleName, member = $"{typeInfo.Name}.{group.Key}", kind = "method", position = PosMethod, reason = "dunder invoked via protocol, not directly" });
                            continue;
                        }

                        membersEnumerated++;
                        // Instance methods reject explicit type args (recv.m[T](…) parses as
                        // indexing, SPY0320, #1133), so generic overloads stay notAttempted.
                        var chosen = PickCallableOverload(group.ToList(), allowGenericTypeArgs: false, out var margs, out _, out var mReason);
                        if (chosen != null)
                        {
                            var body = CallStatement($"recv.{group.Key}({margs})", chosen.ReturnType);
                            var src = $"{importLine}def _use(recv: {typeRef}) -> None:\n    {body}\n";
                            snippets.Add(new Snippet(asmName, moduleName, "method", $"{typeInfo.Name}.{group.Key}", PosMethod, src));
                        }
                        else
                        {
                            notAttempted.Add(new { module = moduleName, member = $"{typeInfo.Name}.{group.Key}", kind = "method", position = PosMethod, reason = mReason });
                        }
                    }

                    // Instance properties (Sharpy name is the reverse-mangled CLR name).
                    foreach (var prop in typeInfo.Properties)
                    {
                        var sharpyProp = NameMangler.ToSharpyName(prop.Name, ReverseNameContext.Property);
                        if (!IsIdentifier(sharpyProp))
                        {
                            notAttempted.Add(new { module = moduleName, member = $"{typeInfo.Name}.{sharpyProp}", kind = "property", position = PosProperty, reason = "non-identifier / compiler-generated member" });
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

        var failures = evaluated.Where(r => r.Stage != "crash").ToList();
        var crashes = evaluated.Where(r => r.Stage == "crash").ToList();
        var byPosition = snippets.GroupBy(s => s.Position).ToDictionary(g => g.Key, g => new PositionStats { Generated = g.Count() });
        foreach (var record in evaluated)
        {
            if (byPosition.TryGetValue(record.Snippet.Position, out var st))
            {
                if (record.Stage == "crash")
                    st.Crash++;
                else
                    st.Fail++;
            }
        }
        foreach (var st in byPosition.Values)
            st.Pass = st.Generated - st.Fail - st.Crash;
        var passCount = snippets.Count - failures.Count - crashes.Count;

        // Ratchet: any non-allowlisted failure/crash fails CI once an allowlist exists.
        var allowlist = LoadAllowlist();
        var offenders = failures.Concat(crashes)
            .Where(f => !allowlist.Matches(f.Snippet.Key))
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
                notAttempted = notAttempted.Count,
                skippedModules = skippedModules.Count,
                allowlistSize = allowlist.Count,
                nonAllowlistedFailures = offenders.Count,
            },
            ratchetMode = AllowlistFileExists(),
            byPosition = byPosition.ToDictionary(kv => kv.Key, kv => new { kv.Value.Generated, kv.Value.Pass, kv.Value.Fail, kv.Value.Crash }),
            scopeNotes = new[]
            {
                "Usage positions v1: annotate, call, reference, method, property, subclass.",
                "index/match positions are deferred (need per-type scrutinee/key synthesis); tracked as follow-up.",
                "Instance methods/properties use a typed parameter as the receiver, so no constructor is exercised.",
                "Generic functions/methods are called with synthesized type arguments (unconstrained/struct/new() -> int, class -> str); constraint-blocked overloads stay notAttempted (constraint synthesis future scope).",
                "Members with non-synthesizable required parameters or generic type names are counted as notAttempted, not failures.",
            },
            crashes = crashes.Take(100).Select(f => f.ToReport(allowlist)),
            failures = failures.Take(500).Select(f => f.ToReport(allowlist)),
            notAttempted = notAttempted.Take(200),
            skippedModules,
        });

        // A flat list of every failing key, to seed / audit the allowlist.
        WriteFailureKeys(failures.Concat(crashes).Select(f => f.Snippet.Key).Distinct().OrderBy(k => k, StringComparer.Ordinal));

        _output.WriteLine($"Members enumerated: {membersEnumerated}");
        _output.WriteLine($"Snippets generated: {snippets.Count}");
        _output.WriteLine($"Pass: {passCount}  Fail: {failures.Count}  Crash: {crashes.Count}  NotAttempted: {notAttempted.Count}");
        _output.WriteLine($"Allowlist size: {allowlist.Count}  Non-allowlisted failures: {offenders.Count}");

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
        }
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
            return new FailureRecord(snippet, "sharpy", errors);

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
    /// true (instance methods reject <c>recv.m[T](…)</c> — parsed as indexing, SPY0320, #1133 —
    /// so callers pass false there) AND the overload set is unambiguous: exactly one generic
    /// arity and no non-generic sibling. A non-generic sibling makes Sharpy prefer it and reject
    /// the type args (e.g. <c>len</c> binds <c>(object) -> int</c>); mixed arities make the
    /// count ambiguous (e.g. <c>itertools.product</c>). In those cases generic overloads are not
    /// rendered and the member stays notAttempted.
    /// </para>
    /// </summary>
    private static FunctionSignature? PickCallableOverload(
        IReadOnlyList<FunctionSignature> overloads,
        bool allowGenericTypeArgs,
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
            IReadOnlyDictionary<string, string>? subst = null;
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
                subst = sig.TypeParameters
                    .Select((tp, i) => (tp.Name, Arg: synthesized[i]))
                    .ToDictionary(x => x.Name, x => x.Arg, StringComparer.Ordinal);
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
        // synthesis being out of scope; then an unsafe generic overload set (either the
        // instance-method carve-out, #1133, or an ambiguous arity/non-generic-sibling set).
        notAttemptedReason =
            sawParamFailure ? "non-synthesizable required parameter"
            : sawUnsynthesizableConstraint ? "constraint synthesis future scope"
            : sawUnsafeGeneric ? (allowGenericTypeArgs
                ? "generic overload set — type-argument synthesis future scope"
                : "instance-method type-argument synthesis future scope (#1133)")
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
        // Interface / base-class constraints require a type satisfying an arbitrary contract —
        // future scope. (TypeParameterInfo records these under InterfaceConstraints.)
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
        if (clrType.IsSealed || clrType.IsAbstract || clrType.IsGenericType)
            return false;
        // Only classes with an accessible parameterless constructor: `class _Sub(T): pass`
        // must not need to forward base-constructor arguments.
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
    {
        try
        {
            var r = api.Compile($"import {moduleName}\n\ndef _p() -> None:\n    pass\n",
                new CompilerOptions { OutputType = "library" });
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
    }
}
