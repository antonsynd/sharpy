using System;

namespace Sharpy
{
    /// <summary>
    /// The naming convention of an identifier body, as <see cref="NameFormDetector"/> classifies it.
    /// </summary>
    public enum NameForm
    {
        /// <summary><c>get_user_name</c>: lowercase and digits with single underscores.</summary>
        SnakeCase,
        /// <summary><c>HttpClient</c>: starts uppercase, no underscores.</summary>
        PascalCase,
        /// <summary><c>httpClient</c>: starts lowercase, no underscores, has an uppercase letter.</summary>
        CamelCase,
        /// <summary><c>MAX_SIZE</c>: uppercase and digits with single underscores.</summary>
        ScreamingSnakeCase,
        /// <summary><c>hello</c>: all lowercase, no underscores.</summary>
        SingleWordLower,
        /// <summary><c>HTTP</c>: all uppercase, no underscores.</summary>
        SingleWordUpper,
        /// <summary><c>__init__</c>: double-underscore bookends.</summary>
        Dunder,
        /// <summary><c>foo__bar</c>, <c>Foo_bar</c> and other mixed patterns.</summary>
        Unrecognized,
    }

    /// <summary>
    /// Detects the naming convention form of an identifier body — the classification the forward
    /// name rule (<see cref="NameMangling"/>) keys on. Moved from the compiler with the rule (#2040);
    /// the compiler's naming-convention validator reads it too.
    /// </summary>
    public static class NameFormDetector
    {
        /// <summary>
        /// Detect the naming form of a name body. Callers strip <c>_</c>/<c>__</c> prefixes and
        /// trailing underscores first, except for dunders, which are detected from the full name.
        /// </summary>
        public static NameForm Detect(string nameBody)
        {
            if (string.IsNullOrEmpty(nameBody))
                return NameForm.Unrecognized;

            // Dunder: starts with __ AND ends with __ AND length > 4
            if (nameBody.StartsWith("__", StringComparison.Ordinal) && nameBody.EndsWith("__", StringComparison.Ordinal) && nameBody.Length > 4)
                return NameForm.Dunder;

            // Consecutive underscores → Unrecognized
            if (nameBody.Contains("__", StringComparison.Ordinal))
                return NameForm.Unrecognized;

            bool hasUnderscore = nameBody.Contains('_', StringComparison.Ordinal);

            if (!hasUnderscore)
            {
                bool allLower = true;
                bool allUpper = true;
                bool hasUpperChar = false;

                foreach (char c in nameBody)
                {
                    if (char.IsUpper(c))
                    {
                        allLower = false;
                        hasUpperChar = true;
                    }
                    else if (char.IsLower(c))
                    {
                        allUpper = false;
                    }
                    // digits don't affect upper/lower classification
                }

                if (allLower)
                    return NameForm.SingleWordLower;
                if (allUpper)
                    return NameForm.SingleWordUpper;
                if (char.IsUpper(nameBody[0]))
                    return NameForm.PascalCase;
                if (char.IsLower(nameBody[0]) && hasUpperChar)
                    return NameForm.CamelCase;

                return NameForm.Unrecognized;
            }

            // Has single underscores — classify segments. Leading/trailing _ produce empty
            // segments, which are skipped.
            bool allSegmentsLower = true;
            bool allSegmentsUpper = true;

            foreach (var segment in nameBody.Split('_'))
            {
                if (segment.Length == 0)
                    continue;

                bool segmentAllLower = true;
                bool segmentAllUpper = true;

                foreach (char c in segment)
                {
                    if (char.IsUpper(c))
                        segmentAllLower = false;
                    else if (char.IsLower(c))
                        segmentAllUpper = false;
                }

                if (!segmentAllLower)
                    allSegmentsLower = false;
                if (!segmentAllUpper)
                    allSegmentsUpper = false;
            }

            if (allSegmentsLower)
                return NameForm.SnakeCase;
            if (allSegmentsUpper)
                return NameForm.ScreamingSnakeCase;

            return NameForm.Unrecognized;
        }

        /// <summary>
        /// Whether the name body contains consecutive underscores. Callers strip dunder bookends
        /// first — this checks the inner body only.
        /// </summary>
        public static bool HasConsecutiveUnderscores(string nameBody)
            => nameBody.Contains("__", StringComparison.Ordinal);

        /// <summary>
        /// Whether a name follows the constant convention: uppercase letters, underscores and digits,
        /// with at least one uppercase letter (<see cref="NameForm.ScreamingSnakeCase"/> and
        /// <see cref="NameForm.SingleWordUpper"/>).
        /// </summary>
        public static bool IsConstantCaseName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            bool hasUpper = false;
            foreach (char c in name)
            {
                if (char.IsUpper(c))
                    hasUpper = true;
                else if (c != '_' && !char.IsDigit(c))
                    return false;
            }

            return hasUpper;
        }
    }
}
