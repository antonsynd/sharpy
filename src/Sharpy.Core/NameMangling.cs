using System;
using System.Linq;

namespace Sharpy
{
    /// <summary>
    /// The forward member-name rule: a Sharpy (Python-style) identifier to the CLR name the compiler
    /// emits for it — <c>snake_case</c> to <c>PascalCase</c> for members, to <c>camelCase</c> for
    /// locals and parameters, <c>SCREAMING_SNAKE_CASE</c> kept for constants and enum members.
    /// </summary>
    /// <remarks>
    /// The ONE copy of the rule (#2040, R-CG). The compiler's <c>NameMangler</c> delegates every
    /// forward call here and adds only the C#-text concern it owns — escaping a result that is a C#
    /// keyword (<c>@class</c>) — so the names returned here are the UNESCAPED metadata names. The
    /// runtime reads the same rule where it has to find a member by its Sharpy spelling:
    /// <c>str.format</c>'s <c>{0.attr}</c> field resolves <c>ToPascalCase(attr)</c> before the
    /// verbatim spelling, so a field declared <c>n_items</c> is found as the CLR property
    /// <c>NItems</c> the compiler emitted.
    /// </remarks>
    public static class NameMangling
    {
        /// <summary>
        /// A member or function name. <c>snake_case</c> and single lowercase words capitalize each
        /// segment (the rest preserved); <c>SCREAMING_SNAKE_CASE</c> title-cases each segment;
        /// <c>PascalCase</c>, <c>camelCase</c>, dunders and unrecognized forms pass through. A
        /// <c>_</c>/<c>__</c> prefix and trailing underscores are kept.
        /// </summary>
        public static string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var (prefix, cleanName, trailing) = SplitAffixes(name);
            var form = NameFormDetector.Detect(cleanName);
            string result;
            switch (form)
            {
                case NameForm.SnakeCase:
                case NameForm.SingleWordLower:
                    result = string.Join("", cleanName.Split('_').Select(CapitalizePreserving));
                    break;
                case NameForm.ScreamingSnakeCase:
                    result = string.Join("", cleanName.Split('_').Select(CapitalizeNormalizing));
                    break;
                default:
                    // PascalCase, SingleWordUpper, CamelCase, Dunder (callers map dunders
                    // themselves), Unrecognized: pass through.
                    result = cleanName;
                    break;
            }

            return prefix + result + trailing;
        }

        /// <summary>
        /// A local or parameter name. <c>snake_case</c> lowers the first segment and capitalizes the
        /// rest; <c>SCREAMING_SNAKE_CASE</c> lowers the first and title-cases the rest;
        /// <c>PascalCase</c> lowers its first character; a single uppercase word lowers entirely;
        /// <c>camelCase</c>, dunders and unrecognized forms pass through.
        /// </summary>
        public static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // Dunders are not locals, but pass through untouched if one arrives here.
            if (name.StartsWith("__", StringComparison.Ordinal) && name.EndsWith("__", StringComparison.Ordinal) && name.Length > 4)
                return name;

            var (prefix, cleanName, trailing) = SplitAffixes(name);
            var form = NameFormDetector.Detect(cleanName);
            string result;
            switch (form)
            {
                case NameForm.SnakeCase:
                case NameForm.SingleWordLower:
                    {
                        var parts = cleanName.Split('_');
                        result = parts[0].ToLowerInvariant()
                            + string.Join("", parts.Skip(1).Select(CapitalizePreserving));
                        break;
                    }
                case NameForm.ScreamingSnakeCase:
                    {
                        var parts = cleanName.Split('_');
                        result = parts[0].ToLowerInvariant()
                            + string.Join("", parts.Skip(1).Select(CapitalizeNormalizing));
                        break;
                    }
                case NameForm.PascalCase:
                    result = char.ToLowerInvariant(cleanName[0]) + cleanName.Substring(1);
                    break;
                case NameForm.SingleWordUpper:
                    result = cleanName.ToLowerInvariant();
                    break;
                default:
                    result = cleanName; // CamelCase, Dunder, Unrecognized
                    break;
            }

            return prefix + result + trailing;
        }

        /// <summary>
        /// A constant name. <c>SCREAMING_SNAKE_CASE</c> and single uppercase words keep their
        /// spelling (<c>MAX_SIZE</c>, <c>PI</c>); <c>snake_case</c> becomes <c>PascalCase</c>; other
        /// forms pass through.
        /// </summary>
        public static string ToConstantCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            switch (NameFormDetector.Detect(name))
            {
                case NameForm.SnakeCase:
                case NameForm.SingleWordLower:
                    return string.Join("", name.Split('_').Select(CapitalizePreserving));
                default:
                    return name; // ScreamingSnakeCase, SingleWordUpper, PascalCase, CamelCase, Unrecognized
            }
        }

        /// <summary>
        /// An int-backed enum member name. <c>SCREAMING_SNAKE_CASE</c> and single uppercase words
        /// keep their spelling; any other form title-cases each underscore-delimited segment
        /// (<c>dark_blue</c> → <c>DarkBlue</c>).
        /// </summary>
        public static string ToEnumMemberName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var form = NameFormDetector.Detect(name);
            if (form == NameForm.ScreamingSnakeCase || form == NameForm.SingleWordUpper)
                return name;

            return string.Join("", name.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant()));
        }

        /// <summary>
        /// Splits off a <c>__</c> (not a dunder) or <c>_</c> prefix and any trailing underscores
        /// (Python's <c>x_</c>, <c>x__</c> are distinct names), so the form is detected on the body.
        /// A body made only of underscores is not trimmed, yet its trailing run is still re-appended
        /// — the rule's historical output for such names, kept byte-identical by the move.
        /// </summary>
        private static (string Prefix, string Body, string Trailing) SplitAffixes(string name)
        {
            var hasDoublePrivatePrefix = name.StartsWith("__", StringComparison.Ordinal)
                && !name.EndsWith("__", StringComparison.Ordinal);
            var hasPrivatePrefix = !hasDoublePrivatePrefix && name.StartsWith("_", StringComparison.Ordinal)
                && !name.StartsWith("__", StringComparison.Ordinal);

            var prefix = hasDoublePrivatePrefix ? "__" : hasPrivatePrefix ? "_" : "";
            var body = name.Substring(prefix.Length);

            var trailingCount = 0;
            for (int i = body.Length - 1; i >= 0 && body[i] == '_'; i--)
                trailingCount++;
            if (trailingCount > 0 && trailingCount < body.Length)
                body = body.Substring(0, body.Length - trailingCount);

            return (prefix, body, new string('_', trailingCount));
        }

        /// <summary>A <c>snake_case</c> segment: capitalize the first character, keep the rest.</summary>
        private static string CapitalizePreserving(string word)
            => string.IsNullOrEmpty(word) ? word : char.ToUpperInvariant(word[0]) + word.Substring(1);

        /// <summary>A <c>SCREAMING_SNAKE_CASE</c> segment: capitalize the first character, lower the rest.</summary>
        private static string CapitalizeNormalizing(string word)
            => string.IsNullOrEmpty(word) ? word : char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
    }
}
