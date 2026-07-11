using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// Conformance test banning unlisted mutable static state in the compiler assembly (#1036).
///
/// Mutable process-global state is a determinism hazard: it makes compilation output depend
/// on invocation history rather than only on inputs. Two rules are enforced by reflecting
/// over <c>typeof(Sharpy.Compiler.Compiler).Assembly</c>:
///
/// <list type="number">
///   <item>Every static field must be <c>readonly</c> (IsInitOnly) or <c>const</c>
///     (IsLiteral) — a plain mutable static (e.g. <c>static int _counter</c>) fails.</item>
///   <item>A <c>static readonly</c> field whose type is a mutable collection
///     (<see cref="HashSet{T}"/>, <see cref="Dictionary{TKey,TValue}"/>, <see cref="List{T}"/>)
///     is flagged, because <c>readonly</c> freezes the reference, not the contents — this
///     catches <c>_loadedRuntimeNamespaces</c>-style caches that rule 1 misses.</item>
/// </list>
///
/// Both rules consult a single <see cref="Allowlist"/> keyed by declaring-type + field name.
/// Every entry carries a justification: either an init-once lookup table (populated at
/// declaration/static-ctor and never mutated afterward, so effectively immutable) or a
/// consciously-reviewed runtime-mutable field whose mutation is determinism-safe. Adding a
/// new mutable static forces a conscious allowlist entry, which is the point.
///
/// Scope note: this reflects over the compiler assembly only, not Sharpy.Core. Core carries
/// runtime-semantics state (interned singletons, caches) that would produce false-positive
/// noise here; the determinism-relevant surface is the compiler.
/// </summary>
public class StaticStateConformanceTests
{
    private readonly ITestOutputHelper _output;

    public StaticStateConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Allowlist of known static mutable-typed fields: (declaring type full name, field name)
    /// → justification. Keep this list minimal and justified; new entries mean a new static
    /// mutable field was introduced and must be reviewed for determinism safety.
    /// </summary>
    // Note on ConcurrentDictionary caches: ClrTypeHelper._paramsIndexerCache is a
    // static readonly ConcurrentDictionary used as an idempotent cache keyed by CLR type.
    // It is intentionally NOT flagged (rule 2 excludes ConcurrentDictionary) and needs no
    // allowlist entry: its contents are a deterministic function of the key.
    private static readonly IReadOnlyDictionary<(string Type, string Field), string> Allowlist =
        new Dictionary<(string, string), string>
        {
            // ---- Genuinely runtime-mutable / thread-local (determinism-reviewed, #1036) ----
            [("Sharpy.Compiler.Semantic.Registry.ModuleRegistry", "_loadedRuntimeNamespaces")] =
                "Process-global set of runtime namespaces already registered. Monotonic and " +
                "idempotent (Add-if-absent guard); affects one-time registration side effects, " +
                "not per-compile output.",
            [("Sharpy.Compiler.Discovery.ClrTypeBridge", "_interfaceListInProgress")] =
                "[ThreadStatic] re-entrancy guard for recursive interface expansion; lazily " +
                "created and cleared per traversal, thread-local so it cannot leak across compiles.",

            // ---- Init-once lookup tables (populated at declaration / static ctor, then only
            //      read — effectively immutable; listed so any NEW static collection is a
            //      conscious review, per #1036). ----
            [("Sharpy.Compiler.Project.SymbolSerializer+TypeCodecRegistry", "s_encoders")] =
                "Type→encoder table populated once in the static constructor, read-only after.",
            [("Sharpy.Compiler.Project.SymbolSerializer+TypeCodecRegistry", "s_decoders")] =
                "Prefix→decoder table populated once in the static constructor, read-only after.",
            [("Sharpy.Compiler.CodeGen.CollectionTypeRegistry", "_entries")] =
                "Collection-type metadata table, initialized inline and read-only after.",
            [("Sharpy.Compiler.CodeGen.TypeSyntaxMapper", "_builtinTypeMap")] =
                "Builtin→C# type-name map, populated in the static constructor, read-only after.",
            [("Sharpy.Compiler.Diagnostics.DiagnosticExplanations", "_explanations")] =
                "Diagnostic-code explanation table built once via BuildExplanations(), read-only after.",
            [("Sharpy.Compiler.Discovery.CachedModuleDiscovery", "SharpyToClrNameMap")] =
                "Sharpy→CLR name map, initialized inline and read-only after.",
            [("Sharpy.Compiler.Discovery.Caching.OverloadIndexBuilder", "ClrOperatorToDunder")] =
                "CLR-operator→dunder map, initialized inline and read-only after.",
            [("Sharpy.Compiler.Discovery.Caching.OverloadIndexBuilder", "ExcludedObjectMethods")] =
                "Set of object methods excluded from overload indexing, initialized inline.",
            [("Sharpy.Compiler.Discovery.OverloadExpander", "ReturnTypeOverrides")] =
                "Return-type override table for known overloads, initialized inline and read-only.",
            [("Sharpy.Compiler.Lexer.Lexer", "Keywords")] =
                "Keyword→token-type table, initialized inline and read-only after.",
            [("Sharpy.Compiler.Semantic.CodeGenInfoComputer", "IteratorProtocolReservedNames")] =
                "Reserved iterator-protocol member names, initialized inline and read-only.",
            [("Sharpy.Compiler.Semantic.Registry.BuiltinRegistry", "RegisteredPrimitiveNames")] =
                "Primitive type names, initialized inline and read-only after.",
            [("Sharpy.Compiler.Semantic.Registry.BuiltinRegistry", "TaggedUnionConstructors")] =
                "Tagged-union constructor names, initialized inline and read-only after.",
            [("Sharpy.Compiler.Semantic.SynthesisAnalyzer", "SynthesizableSharpyCoreInterfaces")] =
                "Set of synthesizable Sharpy.Core interfaces, initialized inline and read-only.",
            [("Sharpy.Compiler.Semantic.Validation.DecoratorValidator", "AccessModifierDecorators")] =
                "Access-modifier decorator names, initialized inline and read-only.",
            [("Sharpy.Compiler.Semantic.Validation.DecoratorValidator", "AllKnownDecorators")] =
                "All known decorator names, initialized inline and read-only.",
            [("Sharpy.Compiler.Semantic.Validation.DecoratorValidator", "InvalidOnInterface")] =
                "Decorators invalid on interfaces, initialized inline and read-only.",
            [("Sharpy.Compiler.Semantic.Validation.DecoratorValidator", "UnsupportedDecorators")] =
                "Unsupported decorator→message map, initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.CSharpKeywords", "All")] =
                "Set of C# reserved keywords for identifier escaping, initialized inline.",
            [("Sharpy.Compiler.Shared.DataclassesEquivalences", "Messages")] =
                "dataclasses guidance-message table, initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.DunderDetector", "DunderProperties")] =
                "Set of dunder property names, initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.DunderNameMapping", "_dunderMethodMap")] =
                "Dunder→C# operator/method map, initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.NameMangler", "_listMethodMap")] =
                "List method-name map, initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.NameMangler", "_upperCaseAcronyms")] =
                "Uppercase-acronym set for namespace casing, initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.TypingEquivalences", "Messages")] =
                "typing guidance-message table, initialized inline and read-only.",
        };

    [Fact]
    public void CompilerAssembly_HasNoUnlistedMutableStaticState()
    {
        var assembly = typeof(Sharpy.Compiler.Compiler).Assembly;
        var violations = new List<string>();
        var matchedAllowlistKeys = new HashSet<(string, string)>();

        foreach (var type in assembly.GetTypes())
        {
            if (IsCompilerGenerated(type))
            {
                continue;
            }

            foreach (var field in type.GetFields(
                         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (IsCompilerGenerated(field) || field.IsLiteral && field.FieldType.IsEnum)
                {
                    continue;
                }

                var key = (type.FullName ?? type.Name, field.Name);
                var allowlisted = Allowlist.ContainsKey(key);

                // Rule 1: static fields must be readonly or const.
                if (!field.IsInitOnly && !field.IsLiteral)
                {
                    if (allowlisted)
                    {
                        matchedAllowlistKeys.Add(key);
                    }
                    else
                    {
                        violations.Add(
                            $"[rule 1: not readonly/const] {key.Item1}.{field.Name} : {FriendlyType(field.FieldType)}");
                    }
                    continue;
                }

                // Rule 2: static readonly mutable collections must be allowlisted.
                if (field.IsInitOnly && IsMutableCollection(field.FieldType))
                {
                    if (allowlisted)
                    {
                        matchedAllowlistKeys.Add(key);
                    }
                    else
                    {
                        violations.Add(
                            $"[rule 2: mutable static collection] {key.Item1}.{field.Name} : {FriendlyType(field.FieldType)}");
                    }
                }
            }
        }

        if (violations.Count > 0)
        {
            _output.WriteLine("Unlisted mutable static state found:");
            foreach (var v in violations.OrderBy(v => v, StringComparer.Ordinal))
            {
                _output.WriteLine("  " + v);
            }
        }

        Assert.True(violations.Count == 0,
            "Unlisted mutable static state in the compiler assembly (determinism hazard). " +
            "If intentional, add an allowlist entry with a determinism justification in " +
            $"StaticStateConformanceTests.Allowlist:\n  {string.Join("\n  ", violations.OrderBy(v => v, StringComparer.Ordinal))}");

        // Keep the allowlist honest: report entries that no longer match a flagged field
        // (field removed, renamed, or made immutable). This is a soft warning rather than a
        // hard failure so that legitimate refactors which drop a static table don't break CI;
        // the load-bearing direction — no NEW unlisted mutable static — is the hard assert above.
        var staleEntries = Allowlist.Keys.Where(k => !matchedAllowlistKeys.Contains(k)).ToList();
        if (staleEntries.Count > 0)
        {
            _output.WriteLine("Stale allowlist entries (no matching flagged field — consider removing):");
            foreach (var k in staleEntries.OrderBy(k => $"{k.Item1}.{k.Item2}", StringComparer.Ordinal))
            {
                _output.WriteLine($"  {k.Item1}.{k.Item2}");
            }
        }
    }

    private static bool IsMutableCollection(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var def = type.GetGenericTypeDefinition();
        return def == typeof(HashSet<>)
            || def == typeof(Dictionary<,>)
            || def == typeof(List<>);
        // ConcurrentDictionary<,> and immutable/frozen/read-only collection types are
        // intentionally not treated as mutable here.
    }

    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.GetCustomAttribute<CompilerGeneratedAttribute>() != null
        || (member.Name?.IndexOf('<') >= 0);

    private static string FriendlyType(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
        {
            name = name.Substring(0, tick);
        }

        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(FriendlyType))}>";
    }
}
