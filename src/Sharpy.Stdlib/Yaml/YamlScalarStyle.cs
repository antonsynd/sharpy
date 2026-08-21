namespace Sharpy
{
    /// <summary>
    /// One authority for the scalar-style decision, implementing PyYAML 6.0.3's
    /// <c>Emitter.analyze_scalar</c> + <c>choose_scalar_style</c> rules (#1542).
    /// Both <c>safe_dump</c> and <c>roundtrip_dump</c> consult this so the two surfaces
    /// can never diverge on how to quote a scalar.
    /// </summary>
    /// <remarks>
    /// Oracle: PyYAML 6.0.3, python3 3.12.13, measured at implementation time.
    /// <code>
    /// python3 -c "import yaml; print(yaml.dump(value, default_flow_style=False))"
    /// </code>
    /// </remarks>
    internal static class YamlScalarStyleAuthority
    {
        internal enum Style
        {
            Plain,
            SingleQuoted,
            DoubleQuoted
        }

        /// <summary>
        /// The scalar style both dump surfaces use for a string value.
        /// </summary>
        /// <param name="value">The string to emit.</param>
        /// <param name="wouldResolve">True if a plain reload would re-type this value
        /// (the caller's resolver says it is not a string). The authority does NOT own
        /// resolution — Sharpy's <c>YamlScalarResolver</c> is the resolution authority,
        /// including the #1465 sexagesimal decline.</param>
        /// <param name="flowContext">True for flow-style dumps; false for block
        /// (the default).</param>
        internal static Style Choose(string value, bool wouldResolve, bool flowContext = false)
        {
            if (value.Length == 0)
                return Style.SingleQuoted;

            bool blockIndicators = false;
            bool flowIndicators = false;
            bool lineBreaks = false;
            bool specialCharacters = false;
            bool leadingSpace = false;
            bool trailingSpace = false;
            bool spaceBreak = false;

            // Document indicators
            if (value.StartsWith("---") || value.StartsWith("..."))
            {
                blockIndicators = true;
                flowIndicators = true;
            }

            bool previousSpace = false;

            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];

                bool followedByWhitespace = (i + 1 >= value.Length)
                    || IsWhitespaceOrNull(value[i + 1]);

                if (i == 0)
                {
                    // Leading indicators that force quoting in both contexts
                    if ("#,[]{}&*!|>'\"%@`".IndexOf(ch) >= 0)
                    {
                        flowIndicators = true;
                        blockIndicators = true;
                    }
                    if (ch == '?' || ch == ':')
                    {
                        flowIndicators = true;
                        if (followedByWhitespace)
                            blockIndicators = true;
                    }
                    if (ch == '-' && followedByWhitespace)
                    {
                        flowIndicators = true;
                        blockIndicators = true;
                    }
                }
                else
                {
                    if (",?[]{}".IndexOf(ch) >= 0)
                        flowIndicators = true;
                    if (ch == ':')
                    {
                        flowIndicators = true;
                        if (followedByWhitespace)
                            blockIndicators = true;
                    }
                    if (ch == '#' && i > 0 && IsWhitespaceOrNull(value[i - 1]))
                    {
                        flowIndicators = true;
                        blockIndicators = true;
                    }
                }

                // Line breaks
                if (ch == '\n')
                {
                    lineBreaks = true;
                }

                // Special characters: C0 controls (except \n) and DEL require escaping
                if (ch != '\n' && (ch < '\x20' || ch == '\u007F'))
                {
                    specialCharacters = true;
                }

                // Whitespace tracking
                if (ch == ' ')
                {
                    if (i == 0)
                        leadingSpace = true;
                    if (i == value.Length - 1)
                        trailingSpace = true;
                    previousSpace = true;
                }
                else if (ch == '\n')
                {
                    if (previousSpace)
                        spaceBreak = true;
                    previousSpace = false;
                }
                else
                {
                    previousSpace = false;
                }
            }

            // If would-resolve, force quoting
            if (wouldResolve)
            {
                blockIndicators = true;
                flowIndicators = true;
            }

            // Special characters or space-break → double-quoted
            if (specialCharacters || spaceBreak)
                return Style.DoubleQuoted;

            // Check if plain is allowed
            bool allowBlockPlain = !blockIndicators && !leadingSpace && !trailingSpace && !lineBreaks;
            bool allowFlowPlain = !flowIndicators && !leadingSpace && !trailingSpace && !lineBreaks;

            bool allowPlain = flowContext ? allowFlowPlain : allowBlockPlain;
            if (allowPlain)
                return Style.Plain;

            return Style.SingleQuoted;
        }

        private static bool IsWhitespaceOrNull(char c)
        {
            return c == '\0' || c == ' ' || c == '\t' || c == '\r' || c == '\n';
        }
    }
}
