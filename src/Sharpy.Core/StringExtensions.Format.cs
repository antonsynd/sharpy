using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Format, maketrans, translate, encode, and case folding extension methods for string.
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// Return a formatted version of the string, using positional arguments.
        /// Python: <c>str.format(*args)</c>
        /// </summary>
        [SharpyKeywordSteer("str.format takes positional fields only; use an f-string (f\"{...}\") or format_map({...})")]
        public static string Format([FormatTemplate] this string s, params object[] args)
        {
            return FormatInternal(s, args, false, null!);
        }

        /// <summary>
        /// Return a formatted version of the string, using a mapping of keyword arguments.
        /// Python: <c>str.format_map(mapping)</c>
        /// </summary>
        public static string FormatMap(this string s, Dict<string, object> mapping)
        {
            return FormatInternal(s, null!, true, mapping);
        }

        private static string FormatInternal(string template, object[] args, bool useMapping, Dict<string, object> mapping)
        {
            int autoIndex = 0;
            bool usedAutoNumbering = false;
            bool usedManualNumbering = false;
            // #1943: the template is recursion depth 2, a nested format spec depth 1, a field inside
            // that spec depth 0 — matching CPython's str.format() recursion limit, which is why
            // '{:{:{}}}'.format(...) raises "Max string recursion exceeded" but '{:{}}' does not.
            return Vformat(template, args, useMapping, mapping,
                ref autoIndex, ref usedAutoNumbering, ref usedManualNumbering, recursionDepth: 2);
        }

        /// <summary>
        /// The ONE nesting-aware replacement-field splitter shared by <c>str.format</c> and
        /// <c>str.format_map</c> (#1943). A field's format spec may itself contain replacement fields
        /// (<c>"{:{}}".format(1234, "&gt;8")</c>); those are expanded by re-entering this same method
        /// so the auto/manual numbering state and the positional/mapping resolver are shared across
        /// the two levels. The f-string route splits its holes in the lexer; parity between the two
        /// is pinned by <c>FormatEngineConsumerParityTests</c>, not by a shared splitter (Axiom 1: the
        /// compile-time and runtime routes are different assemblies by design).
        /// </summary>
        private static string Vformat(string template, object[] args, bool useMapping, Dict<string, object> mapping,
            ref int autoIndex, ref bool usedAutoNumbering, ref bool usedManualNumbering, int recursionDepth)
        {
            if (recursionDepth <= 0)
            {
                throw new ValueError("Max string recursion exceeded");
            }

            var sb = new StringBuilder(template.Length);
            int i = 0;

            while (i < template.Length)
            {
                char c = template[i];

                if (c == '{')
                {
                    if (i + 1 < template.Length && template[i + 1] == '{')
                    {
                        sb.Append('{');
                        i += 2;
                        continue;
                    }

                    // Read the whole field, counting nested braces so the spec's own '{...}' fields
                    // stay inside it rather than truncating at the first '}'.
                    string field = ReadReplacementField(template, ref i);

                    // Split on ':' to separate the field expression from the (possibly nested) spec.
                    string fieldExpr;
                    string? specText;
                    int colonPos = field.IndexOf(':');
                    if (colonPos >= 0)
                    {
                        fieldExpr = field.Substring(0, colonPos);
                        specText = field.Substring(colonPos + 1);
                    }
                    else
                    {
                        fieldExpr = field;
                        specText = null;
                    }

                    // Parse conversion flag (!r, !s, !a) from the field expression.
                    char conversion = '\0';
                    int bangPos = fieldExpr.IndexOf('!');
                    if (bangPos >= 0)
                    {
                        string convStr = fieldExpr.Substring(bangPos + 1);
                        if (convStr.Length != 1 || (convStr[0] != 'r' && convStr[0] != 's' && convStr[0] != 'a'))
                        {
                            throw new ValueError("Unknown conversion specifier '" + convStr + "'");
                        }
                        conversion = convStr[0];
                        fieldExpr = fieldExpr.Substring(0, bangPos);
                    }

                    // Resolve the OUTER field value first so it claims its auto/manual index before
                    // any nested field in the spec claims the next one (CPython's ordering:
                    // '{:{}}{}'.format(1, '>3', 9) is '  19', outer=0, nested=1, trailing=2).
                    object value = ResolveFieldValue(fieldExpr, args, useMapping, mapping,
                        ref autoIndex, ref usedAutoNumbering, ref usedManualNumbering);

                    // Apply conversion flag.
                    if (conversion == 's')
                    {
                        // !s is str(value) — the one str() authority, not object.ToString(). #1883:
                        // ToString() spells a whole double "100" where str() spells it "100.0".
                        value = Builtins.Str(value);
                    }
                    else if (conversion == 'r')
                    {
                        value = Builtins.Repr(value);
                    }
                    else if (conversion == 'a')
                    {
                        value = Builtins.Ascii(value);
                    }

                    // Expand nested replacement fields in the spec through the SAME resolver, one
                    // level deep — a spec containing '{' at depth 0 raises "Max string recursion
                    // exceeded". A spec with no '{' is used verbatim.
                    string spec;
                    if (specText == null)
                    {
                        spec = "";
                    }
                    else if (specText.IndexOf('{') < 0)
                    {
                        spec = specText;
                    }
                    else
                    {
                        spec = Vformat(specText, args, useMapping, mapping,
                            ref autoIndex, ref usedAutoNumbering, ref usedManualNumbering, recursionDepth - 1);
                    }

                    // A spec-less "{}" is a spec of "" — the same engine, the same empty-spec rule.
                    // #1883: appending the object let StringBuilder call ToString(), which is a
                    // second (and wrong) rendering rule: "{}".format(100.0) printed "100" while
                    // f"{100.0}" printed "100.0", and "{}".format(None) printed nothing at all.
                    sb.Append(PyFormat.Apply(value, spec));
                }
                else if (c == '}')
                {
                    if (i + 1 < template.Length && template[i + 1] == '}')
                    {
                        sb.Append('}');
                        i += 2;
                        continue;
                    }
                    throw new ValueError("Single '}' encountered in format string");
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Read one replacement field starting at the opening <c>{</c> that <paramref name="i"/> points
        /// at (the caller has already ruled out the <c>{{</c> escape), returning the field text
        /// between the braces and advancing <paramref name="i"/> past the matching <c>}</c>. Braces are
        /// counted so a nested field in the spec (<c>{:{}}</c>) is kept whole rather than truncated at
        /// its first inner <c>}</c>.
        /// </summary>
        private static string ReadReplacementField(string template, ref int i)
        {
            int start = i + 1;
            int depth = 1;
            int j = start;
            while (j < template.Length)
            {
                char ch = template[j];
                if (ch == '{')
                {
                    depth++;
                }
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }
                j++;
            }

            if (depth != 0)
            {
                throw new ValueError("Single '{' encountered in format string");
            }

            string field = template.Substring(start, j - start);
            i = j + 1;
            return field;
        }

        /// <summary>
        /// Resolve a field expression (<c>baseField</c> plus an optional <c>.attr</c>/<c>[key]</c>
        /// access path) to its value, sharing the auto/manual numbering state so nested fields in a
        /// spec draw from the same positional stream. Mapping mode looks the base field up by name.
        /// </summary>
        private static object ResolveFieldValue(string fieldExpr, object[] args, bool useMapping,
            Dict<string, object> mapping, ref int autoIndex, ref bool usedAutoNumbering, ref bool usedManualNumbering)
        {
            // Split fieldExpr into base field and nested access path (first '.' or '[').
            string baseField;
            string? accessPath;
            int dotPos = fieldExpr.IndexOf('.');
            int bracketPos = fieldExpr.IndexOf('[');
            int accessStart = -1;
            if (dotPos >= 0 && (bracketPos < 0 || dotPos < bracketPos))
            {
                accessStart = dotPos;
            }
            else if (bracketPos >= 0)
            {
                accessStart = bracketPos;
            }

            if (accessStart >= 0)
            {
                baseField = fieldExpr.Substring(0, accessStart);
                accessPath = fieldExpr.Substring(accessStart);
            }
            else
            {
                baseField = fieldExpr;
                accessPath = null;
            }

            object value;

            if (useMapping)
            {
                // format_map mode: look up by name
                try
                {
                    value = mapping[baseField];
                }
                catch (KeyError)
                {
                    throw new KeyError(baseField);
                }
            }
            else
            {
                // format mode: positional
                int index;
                if (baseField.Length == 0)
                {
                    if (usedManualNumbering)
                    {
                        throw new ValueError(
                            "cannot switch from manual field specification to automatic field numbering");
                    }
                    usedAutoNumbering = true;
                    index = autoIndex++;
                }
                else if (int.TryParse(baseField, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
                {
                    if (usedAutoNumbering)
                    {
                        throw new ValueError(
                            "cannot switch from automatic field numbering to manual field specification");
                    }
                    usedManualNumbering = true;
                    index = parsed;
                }
                else
                {
                    throw new ValueError("cannot use keyword arguments with format(), use format_map()");
                }

                if (args == null || index < 0 || index >= args.Length)
                {
                    throw new IndexError(
                        "Replacement index " + index + " out of range for positional args tuple");
                }
                value = args[index];
            }

            // Resolve nested field access (.attr, [key], [index]).
            if (accessPath != null)
            {
                value = ResolveFieldAccess(value, accessPath);
            }

            return value;
        }


        /// <summary>
        /// Resolve nested field access in a format field expression.
        /// Supports <c>.attr</c> (property access) and <c>[key]</c> (item/index access),
        /// including chaining (e.g., <c>.items[0]</c>).
        /// </summary>
        private static object ResolveFieldAccess(object value, string accessPath)
        {
            int pos = 0;
            while (pos < accessPath.Length)
            {
                char ch = accessPath[pos];
                if (ch == '.')
                {
                    // Attribute access: .attr
                    pos++;
                    int start = pos;
                    while (pos < accessPath.Length && accessPath[pos] != '.' && accessPath[pos] != '[')
                    {
                        pos++;
                    }
                    string attr = accessPath.Substring(start, pos - start);
                    value = ResolveAttribute(value, attr);
                }
                else if (ch == '[')
                {
                    // Item access: [key] or [index]
                    pos++;
                    int closeBracket = accessPath.IndexOf(']', pos);
                    if (closeBracket < 0)
                    {
                        throw new ValueError("Missing ']' in format field expression");
                    }
                    string key = accessPath.Substring(pos, closeBracket - pos);
                    pos = closeBracket + 1;
                    value = ResolveItem(value, key);
                }
                else
                {
                    throw new ValueError("Unexpected character '" + ch + "' in format field expression");
                }
            }
            return value;
        }

        private static object ResolveAttribute(object value, string attr)
        {
            if (value == null)
            {
                throw new AttributeError("'NoneType' object has no attribute '" + attr + "'");
            }

            var type = value.GetType();
            var prop = type.GetProperty(attr, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                return prop.GetValue(value)!;
            }

            var field = type.GetField(attr, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(value)!;
            }

            // Python-style error message
            string typeName = type.Name;
            throw new AttributeError("'" + typeName + "' object has no attribute '" + attr + "'");
        }

        private static object ResolveItem(object value, string key)
        {
            if (value == null)
            {
                throw new TypeError("'NoneType' object is not subscriptable");
            }

            // Try non-generic IList first (covers System.Collections.Generic.List<T>, arrays, etc.)
            if (value is System.Collections.IList nonGenericList)
            {
                if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                {
                    if (idx < 0 || idx >= nonGenericList.Count)
                    {
                        throw new IndexError("list index out of range");
                    }
                    return nonGenericList[idx]!;
                }
                throw new KeyError(key);
            }

            // Try non-generic IDictionary (covers System.Collections.Generic.Dictionary<K,V>)
            if (value is IDictionary nonGenericDict)
            {
                if (!nonGenericDict.Contains(key))
                {
                    throw new KeyError(key);
                }
                return nonGenericDict[key]!;
            }

            // Handle generic IList<T> types that don't implement non-generic IList
            // (e.g., Sharpy.List<T>).
            var valueType = value.GetType();
            Type? listInterface = FindGenericInterface(valueType, typeof(IList<>));
            if (listInterface != null)
            {
                if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                {
                    int count = GetCollectionCount(value, valueType);
                    if (idx < 0 || idx >= count)
                    {
                        throw new IndexError("list index out of range");
                    }
                    var indexerProp = listInterface.GetProperty("Item");
                    return indexerProp!.GetValue(value, new object[] { idx })!;
                }
                throw new KeyError(key);
            }

            // Handle generic IDictionary<K,V> types that don't implement non-generic IDictionary
            // (e.g., Sharpy.Dict<K,V>).
            Type? dictInterface = FindGenericInterface(valueType, typeof(IDictionary<,>));
            if (dictInterface != null)
            {
                var containsMethod = dictInterface.GetMethod("ContainsKey");
                var keyType = dictInterface.GetGenericArguments()[0];
                object convertedKey;
                try
                {
                    convertedKey = keyType == typeof(string)
                        ? (object)key
                        : Convert.ChangeType(key, keyType, CultureInfo.InvariantCulture);
                }
                catch
                {
                    throw new KeyError(key);
                }

                bool contains = (bool)containsMethod!.Invoke(value, new[] { convertedKey })!;
                if (!contains)
                {
                    throw new KeyError(key);
                }
                var indexerProp = dictInterface.GetProperty("Item");
                return indexerProp!.GetValue(value, new[] { convertedKey })!;
            }

            throw new TypeError("'" + valueType.Name + "' object is not subscriptable");
        }

        private static Type? FindGenericInterface(Type type, Type genericInterfaceDefinition)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == genericInterfaceDefinition)
                {
                    return iface;
                }
            }
            return null;
        }

        private static int GetCollectionCount(object value, Type valueType)
        {
            // Try ISized (Sharpy collections).
            if (value is ISized sized)
            {
                return sized.Count;
            }
            // Fall back to Count property via reflection.
            var countProp = valueType.GetProperty("Count");
            if (countProp != null)
            {
                return (int)countProp.GetValue(value)!;
            }
            return 0;
        }

        /// <summary>
        /// Build a translation table mapping characters in <paramref name="x"/>
        /// to corresponding characters in <paramref name="y"/>.
        /// Python: <c>str.maketrans(x, y)</c>
        /// </summary>
        public static Dictionary<char, string> Maketrans(string x, string y)
        {
            if (x.Length != y.Length)
            {
                throw new ValueError("the first two maketrans arguments must have equal length");
            }
            var table = new Dictionary<char, string>(x.Length);
            for (int idx = 0; idx < x.Length; idx++)
            {
                table[x[idx]] = y[idx].ToString();
            }
            return table;
        }

        /// <summary>
        /// Build a translation table with a deletion set.
        /// Python: <c>str.maketrans(x, y, z)</c>
        /// </summary>
        public static Dictionary<char, string> Maketrans(string x, string y, string z)
        {
            var table = Maketrans(x, y);
            foreach (char c in z)
            {
                table[c] = "";
            }
            return table;
        }

        /// <summary>
        /// Return a copy of the string in which each character has been mapped
        /// through the given translation table.
        /// Python: <c>str.translate(table)</c>
        /// </summary>
        public static string Translate(this string s, Dictionary<char, string> table)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (table.TryGetValue(c, out var replacement))
                {
                    sb.Append(replacement);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Encode the string using the specified encoding and return as bytes.
        /// Python: <c>str.encode(encoding='utf-8')</c>
        /// </summary>
        public static Bytes Encode(this string s, string encoding = "utf-8")
        {
#pragma warning disable CA1307
            switch (encoding.ToLowerInvariant().Replace("-", "").Replace("_", ""))
#pragma warning restore CA1307
            {
                case "utf8":
                    return new Bytes(Encoding.UTF8.GetBytes(s));
                case "ascii":
                    return new Bytes(Encoding.ASCII.GetBytes(s));
                case "utf16":
                case "utf16le":
                    return new Bytes(Encoding.Unicode.GetBytes(s));
                case "utf16be":
                    return new Bytes(Encoding.BigEndianUnicode.GetBytes(s));
                case "utf32":
                    return new Bytes(Encoding.UTF32.GetBytes(s));
                case "latin1":
                case "iso88591":
                    return new Bytes(Encoding.GetEncoding("iso-8859-1").GetBytes(s));
                default:
                    throw new LookupError("unknown encoding: " + encoding);
            }
        }

        // ----------------------------------------------------------------
        // Case Folding
        // ----------------------------------------------------------------

        // Unicode full case folding table (status "F" and "C" entries from CaseFolding.txt)
        // where the result differs from ToLowerInvariant(). Cherokee ranges are handled
        // by range checks in CaseFoldChar() to keep this table compact.
        private static readonly Dictionary<char, string> s_caseFoldTable = new Dictionary<char, string>(125)
        {
            // Latin/Common
            { '\u00B5', "\u03bc" },       // MICRO SIGN -> Greek small mu
            { '\u00DF', "ss" },           // LATIN SMALL LETTER SHARP S
            { '\u0149', "\u02bcn" },      // LATIN SMALL LETTER N PRECEDED BY APOSTROPHE
            { '\u017F', "s" },            // LATIN SMALL LETTER LONG S
            { '\u01F0', "j\u030c" },      // LATIN SMALL LETTER J WITH CARON

            // Greek
            { '\u0345', "\u03b9" },       // COMBINING GREEK YPOGEGRAMMENI -> iota
            { '\u0390', "\u03b9\u0308\u0301" }, // GREEK SMALL LETTER IOTA WITH DIALYTIKA AND TONOS
            { '\u03B0', "\u03c5\u0308\u0301" }, // GREEK SMALL LETTER UPSILON WITH DIALYTIKA AND TONOS
            { '\u03C2', "\u03c3" },       // GREEK SMALL LETTER FINAL SIGMA -> sigma
            { '\u03D0', "\u03b2" },       // GREEK BETA SYMBOL -> beta
            { '\u03D1', "\u03b8" },       // GREEK THETA SYMBOL -> theta
            { '\u03D5', "\u03c6" },       // GREEK PHI SYMBOL -> phi
            { '\u03D6', "\u03c0" },       // GREEK PI SYMBOL -> pi
            { '\u03F0', "\u03ba" },       // GREEK KAPPA SYMBOL -> kappa
            { '\u03F1', "\u03c1" },       // GREEK RHO SYMBOL -> rho
            { '\u03F5', "\u03b5" },       // GREEK LUNATE EPSILON SYMBOL -> epsilon

            // Armenian
            { '\u0587', "\u0565\u0582" }, // ARMENIAN SMALL LIGATURE ECH YIWN

            // Cyrillic
            { '\u1C80', "\u0432" },       // CYRILLIC SMALL LETTER ROUNDED VE
            { '\u1C81', "\u0434" },       // CYRILLIC SMALL LETTER LONG-LEGGED DE
            { '\u1C82', "\u043e" },       // CYRILLIC SMALL LETTER NARROW O
            { '\u1C83', "\u0441" },       // CYRILLIC SMALL LETTER WIDE ES
            { '\u1C84', "\u0442" },       // CYRILLIC SMALL LETTER TALL TE
            { '\u1C85', "\u0442" },       // CYRILLIC SMALL LETTER THREE-LEGGED TE
            { '\u1C86', "\u044a" },       // CYRILLIC SMALL LETTER TALL HARD SIGN
            { '\u1C87', "\u0463" },       // CYRILLIC SMALL LETTER TALL YAT
            { '\u1C88', "\ua64b" },       // CYRILLIC SMALL LETTER UNBLENDED UK

            // Latin Extended Additional
            { '\u1E96', "h\u0331" },      // LATIN SMALL LETTER H WITH LINE BELOW
            { '\u1E97', "t\u0308" },      // LATIN SMALL LETTER T WITH DIAERESIS
            { '\u1E98', "w\u030a" },      // LATIN SMALL LETTER W WITH RING ABOVE
            { '\u1E99', "y\u030a" },      // LATIN SMALL LETTER Y WITH RING ABOVE
            { '\u1E9A', "a\u02be" },      // LATIN SMALL LETTER A WITH RIGHT HALF RING
            { '\u1E9B', "\u1e61" },       // LATIN SMALL LETTER LONG S WITH DOT ABOVE
            { '\u1E9E', "ss" },           // LATIN CAPITAL LETTER SHARP S

            // Greek Extended
            { '\u1F50', "\u03c5\u0313" },
            { '\u1F52', "\u03c5\u0313\u0300" },
            { '\u1F54', "\u03c5\u0313\u0301" },
            { '\u1F56', "\u03c5\u0313\u0342" },
            { '\u1F80', "\u1f00\u03b9" },
            { '\u1F81', "\u1f01\u03b9" },
            { '\u1F82', "\u1f02\u03b9" },
            { '\u1F83', "\u1f03\u03b9" },
            { '\u1F84', "\u1f04\u03b9" },
            { '\u1F85', "\u1f05\u03b9" },
            { '\u1F86', "\u1f06\u03b9" },
            { '\u1F87', "\u1f07\u03b9" },
            { '\u1F88', "\u1f00\u03b9" },
            { '\u1F89', "\u1f01\u03b9" },
            { '\u1F8A', "\u1f02\u03b9" },
            { '\u1F8B', "\u1f03\u03b9" },
            { '\u1F8C', "\u1f04\u03b9" },
            { '\u1F8D', "\u1f05\u03b9" },
            { '\u1F8E', "\u1f06\u03b9" },
            { '\u1F8F', "\u1f07\u03b9" },
            { '\u1F90', "\u1f20\u03b9" },
            { '\u1F91', "\u1f21\u03b9" },
            { '\u1F92', "\u1f22\u03b9" },
            { '\u1F93', "\u1f23\u03b9" },
            { '\u1F94', "\u1f24\u03b9" },
            { '\u1F95', "\u1f25\u03b9" },
            { '\u1F96', "\u1f26\u03b9" },
            { '\u1F97', "\u1f27\u03b9" },
            { '\u1F98', "\u1f20\u03b9" },
            { '\u1F99', "\u1f21\u03b9" },
            { '\u1F9A', "\u1f22\u03b9" },
            { '\u1F9B', "\u1f23\u03b9" },
            { '\u1F9C', "\u1f24\u03b9" },
            { '\u1F9D', "\u1f25\u03b9" },
            { '\u1F9E', "\u1f26\u03b9" },
            { '\u1F9F', "\u1f27\u03b9" },
            { '\u1FA0', "\u1f60\u03b9" },
            { '\u1FA1', "\u1f61\u03b9" },
            { '\u1FA2', "\u1f62\u03b9" },
            { '\u1FA3', "\u1f63\u03b9" },
            { '\u1FA4', "\u1f64\u03b9" },
            { '\u1FA5', "\u1f65\u03b9" },
            { '\u1FA6', "\u1f66\u03b9" },
            { '\u1FA7', "\u1f67\u03b9" },
            { '\u1FA8', "\u1f60\u03b9" },
            { '\u1FA9', "\u1f61\u03b9" },
            { '\u1FAA', "\u1f62\u03b9" },
            { '\u1FAB', "\u1f63\u03b9" },
            { '\u1FAC', "\u1f64\u03b9" },
            { '\u1FAD', "\u1f65\u03b9" },
            { '\u1FAE', "\u1f66\u03b9" },
            { '\u1FAF', "\u1f67\u03b9" },
            { '\u1FB2', "\u1f70\u03b9" },
            { '\u1FB3', "\u03b1\u03b9" },
            { '\u1FB4', "\u03ac\u03b9" },
            { '\u1FB6', "\u03b1\u0342" },
            { '\u1FB7', "\u03b1\u0342\u03b9" },
            { '\u1FBC', "\u03b1\u03b9" },
            { '\u1FBE', "\u03b9" },
            { '\u1FC2', "\u1f74\u03b9" },
            { '\u1FC3', "\u03b7\u03b9" },
            { '\u1FC4', "\u03ae\u03b9" },
            { '\u1FC6', "\u03b7\u0342" },
            { '\u1FC7', "\u03b7\u0342\u03b9" },
            { '\u1FCC', "\u03b7\u03b9" },
            { '\u1FD2', "\u03b9\u0308\u0300" },
            { '\u1FD3', "\u03b9\u0308\u0301" },
            { '\u1FD6', "\u03b9\u0342" },
            { '\u1FD7', "\u03b9\u0308\u0342" },
            { '\u1FE2', "\u03c5\u0308\u0300" },
            { '\u1FE3', "\u03c5\u0308\u0301" },
            { '\u1FE4', "\u03c1\u0313" },
            { '\u1FE6', "\u03c5\u0342" },
            { '\u1FE7', "\u03c5\u0308\u0342" },
            { '\u1FF2', "\u1f7c\u03b9" },
            { '\u1FF3', "\u03c9\u03b9" },
            { '\u1FF4', "\u03ce\u03b9" },
            { '\u1FF6', "\u03c9\u0342" },
            { '\u1FF7', "\u03c9\u0342\u03b9" },
            { '\u1FFC', "\u03c9\u03b9" },

            // Ligatures / Compatibility
            { '\uFB00', "ff" },
            { '\uFB01', "fi" },
            { '\uFB02', "fl" },
            { '\uFB03', "ffi" },
            { '\uFB04', "ffl" },
            { '\uFB05', "st" },
            { '\uFB06', "st" },

            // Armenian ligatures
            { '\uFB13', "\u0574\u0576" },
            { '\uFB14', "\u0574\u0565" },
            { '\uFB15', "\u0574\u056b" },
            { '\uFB16', "\u057e\u0576" },
            { '\uFB17', "\u0574\u056d" },
        };

        private static string CaseFoldChar(char c)
        {
            // Cherokee uppercase U+13A0-U+13F5: casefold is identity (not lowercased)
            if (c >= '\u13A0' && c <= '\u13F5')
            {
                return c.ToString();
            }

            // Cherokee small U+13F8-U+13FD: casefold maps to U+13F0-U+13F5
            if (c >= '\u13F8' && c <= '\u13FD')
            {
                return ((char)(c - 8)).ToString();
            }

            // Cherokee small letter U+AB70-U+ABBF: casefold maps to U+13A0-U+13EF
            if (c >= '\uAB70' && c <= '\uABBF')
            {
                return ((char)(c - 0x97D0)).ToString();
            }

            // Check the folding table for special mappings
            if (s_caseFoldTable.TryGetValue(c, out var folded))
            {
                return folded;
            }

            // Default: use invariant lowercase
            return char.ToLowerInvariant(c).ToString();
        }
    }
}
