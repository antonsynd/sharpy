namespace Sharpy.Compiler.Shared;

/// <summary>
/// Classifies the naming convention of an identifier.
/// </summary>
internal enum NameForm
{
    SnakeCase,            // get_user_name (all lowercase + digits + single underscores)
    PascalCase,           // HttpClient (starts uppercase, no underscores)
    CamelCase,            // httpClient (starts lowercase, no underscores, has uppercase)
    ScreamingSnakeCase,   // MAX_SIZE (all uppercase + digits + single underscores)
    SingleWordLower,      // hello (all lowercase, no underscores)
    SingleWordUpper,      // HTTP (all uppercase, no underscores)
    Dunder,               // __init__ (double underscore bookends)
    Unrecognized          // foo__bar, Foo_bar, mixed patterns
}

/// <summary>
/// Detects the naming convention form of an identifier body.
/// Used by <see cref="NameMangler"/> to decide how to transform names.
/// </summary>
internal static class NameFormDetector
{
    /// <summary>
    /// Detect the naming form of a name body.
    /// Callers should strip <c>_</c>/<c>__</c> prefixes and trailing underscores before calling,
    /// except for dunders which are detected from the full name.
    /// </summary>
    public static NameForm Detect(string nameBody)
    {
        if (string.IsNullOrEmpty(nameBody))
            return NameForm.Unrecognized;

        // Dunder: starts with __ AND ends with __ AND length > 4
        if (nameBody.StartsWith("__") && nameBody.EndsWith("__") && nameBody.Length > 4)
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

        // Has single underscores — classify segments
        var segments = nameBody.Split('_');

        // Check for empty segments (would indicate leading/trailing _ or consecutive __)
        // Consecutive __ already handled above, but leading/trailing _ can produce empty segments
        bool allSegmentsLower = true;
        bool allSegmentsUpper = true;

        foreach (var segment in segments)
        {
            if (segment.Length == 0)
            {
                // Empty segment from leading/trailing underscore — skip
                continue;
            }

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
    /// Check if the name body contains consecutive underscores.
    /// Callers should strip dunder bookends before calling — this method checks the inner body only.
    /// </summary>
    public static bool HasConsecutiveUnderscores(string nameBody)
    {
        return nameBody.Contains("__", StringComparison.Ordinal);
    }

    /// <summary>
    /// Check if a name follows constant naming convention (all uppercase + underscores + digits, with at least one uppercase char).
    /// Matches both <see cref="NameForm.ScreamingSnakeCase"/> and <see cref="NameForm.SingleWordUpper"/>.
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
