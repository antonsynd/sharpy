namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

/// <summary>
/// Bracket-aware scanning helpers shared by the parenthesis transforms. All of them run over a
/// <see cref="MaskedSource"/> line (string literals and comments already blanked), so a bracket or
/// comma found here is always real syntax.
/// </summary>
internal static class ParenBalance
{
    /// <summary>Index of the <c>)</c> matching the <c>(</c> at <paramref name="openIndex"/>, or -1.</summary>
    public static int MatchingClose(string maskedLine, int openIndex)
    {
        if (openIndex < 0 || openIndex >= maskedLine.Length || maskedLine[openIndex] != '(')
            return -1;

        var depth = 0;
        for (int i = openIndex; i < maskedLine.Length; i++)
        {
            var c = maskedLine[i];
            if (c is '(' or '[' or '{')
                depth++;
            else if (c is ')' or ']' or '}')
            {
                depth--;
                if (depth == 0)
                    return c == ')' ? i : -1;
                if (depth < 0)
                    return -1;
            }
        }
        return -1;
    }

    /// <summary>
    /// True when an argument-list body is exactly one positional expression, so wrapping it in
    /// parentheses is a no-op. Rejects argument separators, keyword arguments, unpacking and
    /// generator expressions — parenthesizing any of those changes what the call means.
    /// </summary>
    public static bool IsSinglePositionalArgument(string maskedArgs)
    {
        var trimmed = maskedArgs.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('*'))
            return false;

        var depth = 0;
        for (int i = 0; i < maskedArgs.Length; i++)
        {
            var c = maskedArgs[i];
            if (c is '(' or '[' or '{')
                depth++;
            else if (c is ')' or ']' or '}')
                depth--;
            else if (depth != 0)
                continue;
            else if (c == ',')
                return false;
            else if (c == '=' && !IsPartOfComparison(maskedArgs, i))
                return false; // keyword argument
        }

        // A bare generator expression argument — print(x for x in xs) — is not parenthesizable
        // without changing the call's argument count semantics in the general case.
        return !ContainsTopLevelWord(maskedArgs, "for");
    }

    private static bool IsPartOfComparison(string text, int equalsIndex)
    {
        if (equalsIndex + 1 < text.Length && text[equalsIndex + 1] == '=')
            return true;
        if (equalsIndex == 0)
            return false;
        var prev = text[equalsIndex - 1];
        return prev is '=' or '!' or '<' or '>' or '+' or '-' or '*' or '/' or '%' or '&' or '|' or '^' or '~';
    }

    private static bool ContainsTopLevelWord(string maskedText, string word)
    {
        var depth = 0;
        for (int i = 0; i < maskedText.Length; i++)
        {
            var c = maskedText[i];
            if (c is '(' or '[' or '{')
                depth++;
            else if (c is ')' or ']' or '}')
                depth--;
            else if (depth == 0 && c == word[0] && MatchesWordAt(maskedText, i, word))
                return true;
        }
        return false;
    }

    private static bool MatchesWordAt(string text, int index, string word)
    {
        if (index + word.Length > text.Length)
            return false;
        if (string.CompareOrdinal(text, index, word, 0, word.Length) != 0)
            return false;
        if (index > 0 && IsIdentifierChar(text[index - 1]))
            return false;
        var after = index + word.Length;
        return after >= text.Length || !IsIdentifierChar(text[after]);
    }

    public static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
