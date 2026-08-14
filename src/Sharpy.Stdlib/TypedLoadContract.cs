#if NET10_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Sharpy
{
    /// <summary>
    /// The one authority for what the typed stdlib doors — <c>json.loads[T]</c>/<c>load[T]</c> and
    /// <c>yaml.safe_load_typed[T]</c> — consider a REQUIRED field of the target type, and for the
    /// message they report when a document omits one.
    ///
    /// <para><b>Why it exists.</b> Both doors construct the target through its all-fields
    /// constructor and then let the deserializer populate what the document mentions. An absent key
    /// therefore left whatever the constructor was handed, and both libraries hand a placeholder:
    /// <c>yaml.safe_load_typed[Config]("port: 8080")</c> returned <c>Ok</c> with
    /// <c>max_connections == 0</c> for a field the user never made optional (#1505). Silently
    /// fabricating <c>0</c>/<c>null</c>/<c>false</c> for a required field is the wrong-value class,
    /// and the owner ruling (2026-08-13) decided it for BOTH doors at once: <b>Err on a missing
    /// required field, naming it; declared defaults keep their defaults; an absent <c>T?</c> is
    /// None.</b></para>
    ///
    /// <para><b>Why shared.</b> json and yaml live in one assembly, so the rule is one function
    /// rather than two implementations that agree today. The doors' agreement is then a testable
    /// property rather than an intention — see the typed-door agreement corpus, which runs the same
    /// document through both and asserts they answer cell-by-cell alike.</para>
    ///
    /// <para><b>Scope: the ROOT object only.</b> A required field missing from a NESTED object is
    /// not detected — the check reads the top-level key set, not the whole document tree. Stated
    /// rather than implied, and filed as #1513; the nested case still deserializes as before.</para>
    /// </summary>
    internal static class TypedLoadContract
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<RequiredField>> Cache = new();

        /// <summary>
        /// A required field of a typed-load target: the name to report, plus the normalized key the
        /// document would have to carry for it to count as present.
        /// </summary>
        private readonly struct RequiredField
        {
            public RequiredField(string reportedName, string normalizedKey)
            {
                ReportedName = reportedName;
                NormalizedKey = normalizedKey;
            }

            /// <summary>The spelling the user wrote in the <c>.spy</c> source, snake_case.</summary>
            public string ReportedName { get; }

            /// <summary>Case- and underscore-insensitive form, for matching document keys.</summary>
            public string NormalizedKey { get; }
        }

        /// <summary>
        /// The name of the first required field of <typeparamref name="T"/> that
        /// <paramref name="presentKeys"/> does not cover, or <c>null</c> when every required field
        /// is present (including when <typeparamref name="T"/> has none, which is every non-record
        /// shape both doors already handled).
        /// </summary>
        /// <param name="presentKeys">The document's top-level keys, in the source spelling.</param>
        internal static string? FirstMissingRequiredField<T>(IEnumerable<string> presentKeys)
        {
            var required = RequiredFieldsOf(typeof(T));
            if (required.Count == 0)
                return null;

            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in presentKeys)
            {
                if (key != null)
                    present.Add(Normalize(key));
            }

            foreach (var field in required)
            {
                if (!present.Contains(field.NormalizedKey))
                    return field.ReportedName;
            }

            return null;
        }

        /// <summary>
        /// The message both doors report for a missing required field, so a caller that matches on
        /// text sees one sentence rather than two dialects.
        /// </summary>
        internal static string MissingFieldMessage(Type target, string fieldName)
            => $"missing required field '{fieldName}' for {target.Name}";

        /// <summary>
        /// The required fields of <paramref name="target"/>: the parameters of its single
        /// all-fields constructor that have NO declared default and are NOT optional-typed. The
        /// constructor is the authority because it is what both doors construct through, and a
        /// dataclass emits exactly one.
        ///
        /// <para>A type with no constructor, a parameterless one, or several is treated as having
        /// no required fields — the same conservative fallthrough
        /// <c>AllFieldsConstructorObjectFactory</c> takes, since guessing among overloads would be
        /// worse than the behaviour that already exists.</para>
        /// </summary>
        private static IReadOnlyList<RequiredField> RequiredFieldsOf(Type target)
            => Cache.GetOrAdd(target, static t =>
            {
                var constructors = t.GetConstructors();
                if (constructors.Length != 1)
                    return Array.Empty<RequiredField>();

                var parameters = constructors[0].GetParameters();
                if (parameters.Length == 0)
                    return Array.Empty<RequiredField>();

                var nullability = new NullabilityInfoContext();
                var required = new List<RequiredField>();
                foreach (var parameter in parameters)
                {
                    if (parameter.Name == null || IsOptional(parameter, nullability))
                        continue;

                    required.Add(new RequiredField(
                        ReportedNameFor(parameter, t),
                        Normalize(parameter.Name)));
                }

                return required;
            });

        /// <summary>
        /// Whether a constructor parameter is one the ruling exempts: it declares a default (the
        /// default applies), or its type is <c>Optional&lt;T&gt;</c>, <c>Nullable&lt;T&gt;</c> or a
        /// nullable reference — all three being how a Sharpy <c>T?</c> field can reach here, and
        /// all three meaning "absence is a value this field can hold" (absent → None).
        /// </summary>
        private static bool IsOptional(ParameterInfo parameter, NullabilityInfoContext nullability)
        {
            if (parameter.HasDefaultValue)
                return true;

            var type = parameter.ParameterType;
            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                if (definition == typeof(Nullable<>) || definition == typeof(Optional<>))
                    return true;
            }

            return !type.IsValueType
                && nullability.Create(parameter).WriteState == NullabilityState.Nullable;
        }

        /// <summary>
        /// The parameter's name as the USER wrote it. The emitted constructor parameter is
        /// camelCase (it shares the authority every other emitted parameter uses), while the
        /// document and the <c>.spy</c> declaration are snake_case, so reporting the parameter name
        /// verbatim would name a spelling that appears nowhere in the user's program. Recovered
        /// from the target's own property/field names — the emitted PascalCase member — falling
        /// back to a mechanical de-camelCasing when no member matches.
        /// </summary>
        private static string ReportedNameFor(ParameterInfo parameter, Type target)
        {
            var normalized = Normalize(parameter.Name!);

            foreach (var member in target.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                if (member is not (PropertyInfo or FieldInfo))
                    continue;

                if (Normalize(member.Name) == normalized)
                    return ToSnakeCase(member.Name);
            }

            return ToSnakeCase(parameter.Name!);
        }

        /// <summary>
        /// Case- and underscore-insensitive key form. Matching on this is what lets one rule serve
        /// both doors: System.Text.Json binds constructor parameters to members case-insensitively
        /// but NOT underscore-insensitively, and YamlDotNet's underscored convention spells the
        /// same member with underscores — so <c>max_connections</c>, <c>maxConnections</c> and
        /// <c>MaxConnections</c> must all normalize to one string.
        /// </summary>
        private static string Normalize(string name)
        {
            var builder = new System.Text.StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (c != '_')
                    builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }

        /// <summary>PascalCase/camelCase → snake_case, for reporting in the user's spelling.</summary>
        private static string ToSnakeCase(string name)
        {
            var builder = new System.Text.StringBuilder(name.Length + 4);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0 && name[i - 1] != '_')
                        builder.Append('_');
                    builder.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// The top-level keys of a YAML/JSON mapping already materialized as a dictionary, for the
        /// doors that have one in hand. A non-mapping document (a scalar, a sequence, an empty
        /// document) yields no keys, so a target WITH required fields correctly reports the first
        /// of them as missing.
        /// </summary>
        internal static IEnumerable<string> KeysOf(object? mapping)
            => mapping switch
            {
                IDictionary<string, object?> typed => typed.Keys,
                System.Collections.IDictionary untyped => untyped.Keys
                    .Cast<object?>()
                    .Select(k => k?.ToString() ?? string.Empty),
                _ => Array.Empty<string>(),
            };
    }
}
#endif
