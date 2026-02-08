namespace Sharpy.Compiler.Utilities;

/// <summary>
/// Provides Levenshtein edit distance computation for "did you mean?" suggestions.
/// </summary>
internal static class EditDistance
{
    /// <summary>
    /// Computes the Levenshtein edit distance between two strings.
    /// Uses case-insensitive comparison. Returns int.MaxValue if either string is null.
    /// </summary>
    internal static int Compute(string? a, string? b)
    {
        if (a is null || b is null)
            return int.MaxValue;

        var aLower = a.ToLowerInvariant();
        var bLower = b.ToLowerInvariant();

        var m = aLower.Length;
        var n = bLower.Length;

        // Early termination for empty strings
        if (m == 0)
            return n;
        if (n == 0)
            return m;

        // Single-row optimization: O(min(n,m)) space
        // Ensure we iterate over the shorter string in the inner loop
        if (m < n)
        {
            (aLower, bLower) = (bLower, aLower);
            (m, n) = (n, m);
        }

        var prev = new int[n + 1];
        var curr = new int[n + 1];

        for (var j = 0; j <= n; j++)
            prev[j] = j;

        for (var i = 1; i <= m; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= n; j++)
            {
                var cost = aLower[i - 1] == bLower[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[n];
    }

    /// <summary>
    /// Finds the closest match to <paramref name="name"/> from <paramref name="candidates"/>
    /// within <paramref name="maxDistance"/>. Returns null if no match is within threshold.
    /// Case-insensitive comparison for distance, but returns the original candidate casing.
    /// </summary>
    internal static string? FindClosestMatch(string name, IEnumerable<string> candidates, int maxDistance = 2)
    {
        // Don't suggest for very short names (too many false positives)
        if (name.Length <= 2)
            return null;

        string? bestMatch = null;
        var bestDistance = int.MaxValue;

        foreach (var candidate in candidates)
        {
            // Skip exact matches
            if (string.Equals(name, candidate, StringComparison.Ordinal))
                continue;

            // Early termination: length difference exceeding maxDistance means distance > maxDistance
            if (Math.Abs(name.Length - candidate.Length) > maxDistance)
                continue;

            var distance = Compute(name, candidate);

            // Only suggest if distance <= maxDistance AND distance < name.Length
            if (distance <= maxDistance && distance < name.Length && distance < bestDistance)
            {
                bestDistance = distance;
                bestMatch = candidate;
            }
        }

        return bestMatch;
    }
}
