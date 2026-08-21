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
    /// <para><b>Scope: the whole document tree</b> (#1513). The walk is document-driven: it
    /// recurses only where the document actually has a nested mapping or sequence, so recursive
    /// type shapes terminate without a visited-set.</para>
    /// </summary>
    internal static class TypedLoadContract
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<FieldDescriptor>> FieldCache = new();

        /// <summary>
        /// Abstract view of a document node, supplied by each door so the recursive walk is
        /// library-agnostic. json provides a view over <c>JsonElement</c>; yaml provides one
        /// over its untyped <c>object</c> graph.
        /// </summary>
        internal interface IDocumentNode
        {
            bool IsMapping { get; }
            bool IsSequence { get; }
            IEnumerable<string> Keys { get; }
            IDocumentNode GetChild(string key);
            IEnumerable<(string indexLabel, IDocumentNode element)> Elements { get; }
        }

        /// <summary>
        /// A field of a typed-load target: the name to report, the normalized key for matching,
        /// and the CLR type for recursion into nested objects.
        /// </summary>
        private readonly struct FieldDescriptor
        {
            public FieldDescriptor(string reportedName, string normalizedKey, Type fieldType, bool isRequired)
            {
                ReportedName = reportedName;
                NormalizedKey = normalizedKey;
                FieldType = fieldType;
                IsRequired = isRequired;
            }

            public string ReportedName { get; }
            public string NormalizedKey { get; }
            public Type FieldType { get; }
            public bool IsRequired { get; }
        }

        /// <summary>
        /// The dotted path of the first required field of <typeparamref name="T"/> that the
        /// document node omits, walking the entire tree. Returns <c>null</c> when every required
        /// field at every depth is present (#1513).
        /// </summary>
        internal static string? FirstMissingRequiredFieldPath<T>(IDocumentNode root)
        {
            return WalkNode(typeof(T), root, prefix: "");
        }

        private static string? WalkNode(Type targetType, IDocumentNode node, string prefix)
        {
            var fields = FieldDescriptorsOf(targetType);
            if (fields.Count == 0 || !node.IsMapping)
                return null;

            var presentNormalized = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in node.Keys)
            {
                if (key != null)
                    presentNormalized.Add(Normalize(key));
            }

            foreach (var field in fields)
            {
                string path = prefix.Length == 0 ? field.ReportedName : prefix + "." + field.ReportedName;

                if (!presentNormalized.Contains(field.NormalizedKey))
                {
                    if (field.IsRequired)
                        return path;
                    continue;
                }

                if (!CouldRecurse(field.FieldType))
                    continue;

                IDocumentNode child;
                try
                {
                    child = FindChild(node, field.NormalizedKey);
                }
                catch
                {
                    continue;
                }

                var nested = WalkValue(field.FieldType, child, path);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        /// <summary>
        /// Recurses into a PRESENT value by its DECLARED type, one container layer at a time:
        /// a dict-typed layer descends per VALUE (<c>servers[key]</c> — the dict's keys are
        /// document data, not fields of the value type), a list-typed layer per element
        /// (<c>items[2]</c>), and a dataclass-shaped type walks its own fields. A node whose
        /// shape does not match the declared layer is not this check's business — the
        /// deserializer's own error reports it, exactly as malformed documents do today.
        /// </summary>
        private static string? WalkValue(Type declaredType, IDocumentNode node, string path)
        {
            var type = StripOptional(declaredType);

            if (TryDictValueType(type, out var dictValueType))
            {
                if (!node.IsMapping)
                    return null;

                foreach (var key in node.Keys)
                {
                    if (key == null)
                        continue;

                    var nested = WalkValue(dictValueType, node.GetChild(key), path + "[" + key + "]");
                    if (nested != null)
                        return nested;
                }

                return null;
            }

            if (TryListElementType(type, out var elementType))
            {
                if (!node.IsSequence)
                    return null;

                foreach (var (indexLabel, element) in node.Elements)
                {
                    var nested = WalkValue(elementType, element, path + "[" + indexLabel + "]");
                    if (nested != null)
                        return nested;
                }

                return null;
            }

            if (type == typeof(string) || type.IsPrimitive || FieldDescriptorsOf(type).Count == 0)
                return null;

            return WalkNode(type, node, path);
        }

        /// <summary>
        /// Whether <see cref="WalkValue"/> could find anything under this declared type — a
        /// pre-filter so scalar fields never pay for a child lookup. Follows the same layers
        /// the walk does, so the two cannot disagree about what is worth descending into.
        /// </summary>
        private static bool CouldRecurse(Type declaredType)
        {
            var type = StripOptional(declaredType);

            if (TryDictValueType(type, out var dictValueType))
                return CouldRecurse(dictValueType);

            if (TryListElementType(type, out var elementType))
                return CouldRecurse(elementType);

            return type != typeof(string) && !type.IsPrimitive && FieldDescriptorsOf(type).Count > 0;
        }

        private static IDocumentNode FindChild(IDocumentNode node, string normalizedKey)
        {
            foreach (var key in node.Keys)
            {
                if (key != null && Normalize(key) == normalizedKey)
                    return node.GetChild(key);
            }

            throw new InvalidOperationException("key not found");
        }

        /// <summary>
        /// The message both doors report for a missing required field, so a caller that matches on
        /// text sees one sentence rather than two dialects.
        /// </summary>
        internal static string MissingFieldMessage(Type target, string fieldName)
            => $"missing required field '{fieldName}' for {target.Name}";

        /// <summary>
        /// All fields of <paramref name="target"/>: the parameters of its single all-fields
        /// constructor, each tagged as required or optional. Required = no declared default and
        /// not optional-typed. The constructor is the authority because it is what both doors
        /// construct through, and a dataclass emits exactly one.
        ///
        /// <para>A type with no constructor, a parameterless one, or several is treated as having
        /// no fields — the same conservative fallthrough
        /// <c>AllFieldsConstructorObjectFactory</c> takes.</para>
        /// </summary>
        private static IReadOnlyList<FieldDescriptor> FieldDescriptorsOf(Type target)
            => FieldCache.GetOrAdd(target, static t =>
            {
                var constructors = t.GetConstructors();
                if (constructors.Length != 1)
                    return Array.Empty<FieldDescriptor>();

                var parameters = constructors[0].GetParameters();
                if (parameters.Length == 0)
                    return Array.Empty<FieldDescriptor>();

                var nullability = new NullabilityInfoContext();
                var descriptors = new List<FieldDescriptor>();
                foreach (var parameter in parameters)
                {
                    if (parameter.Name == null)
                        continue;

                    bool optional = IsOptional(parameter, nullability);
                    descriptors.Add(new FieldDescriptor(
                        ReportedNameFor(parameter, t),
                        Normalize(parameter.Name),
                        parameter.ParameterType,
                        isRequired: !optional));
                }

                return descriptors;
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

        /// <summary>Strips <c>Optional&lt;T&gt;</c>/<c>Nullable&lt;T&gt;</c> wrappers — absence is
        /// the caller's business; the walk only ever sees present values.</summary>
        private static Type StripOptional(Type type)
        {
            while (type.IsGenericType)
            {
                var def = type.GetGenericTypeDefinition();
                if (def != typeof(Optional<>) && def != typeof(Nullable<>))
                    break;

                type = type.GetGenericArguments()[0];
            }

            return type;
        }

        /// <summary>Dict&lt;K,V&gt;/Dictionary&lt;K,V&gt; and interface shapes → value V.</summary>
        private static bool TryDictValueType(Type type, out Type valueType)
        {
            if (type.IsGenericType)
            {
                var def = type.GetGenericTypeDefinition();
                if (def == typeof(Dict<,>)
                    || def == typeof(Dictionary<,>)
                    || def == typeof(IDictionary<,>)
                    || def == typeof(IReadOnlyDictionary<,>))
                {
                    valueType = type.GetGenericArguments()[1];
                    return true;
                }
            }

            valueType = typeof(object);
            return false;
        }

        /// <summary>List&lt;T&gt;/sequence interface shapes → element T.</summary>
        private static bool TryListElementType(Type type, out Type elementType)
        {
            if (type.IsGenericType)
            {
                var def = type.GetGenericTypeDefinition();
                if (def == typeof(List<>)
                    || def == typeof(IEnumerable<>)
                    || def == typeof(IList<>)
                    || def == typeof(System.Collections.Generic.List<>)
                    || def == typeof(IReadOnlyList<>))
                {
                    elementType = type.GetGenericArguments()[0];
                    return true;
                }
            }

            elementType = typeof(object);
            return false;
        }
    }
}
#endif
