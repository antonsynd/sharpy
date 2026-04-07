using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Unicode case folding support for Str.
    /// </summary>
    public readonly partial struct Str
    {
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
