namespace Sharpy
{
    /// <summary>
    /// String constants matching Python's string module.
    /// Provides character classification constants for ASCII characters.
    /// </summary>
    public static partial class StringModule
    {
        /// <summary>
        /// The lowercase letters 'abcdefghijklmnopqrstuvwxyz'.
        /// </summary>
        public const string ascii_lowercase = "abcdefghijklmnopqrstuvwxyz";

        /// <summary>
        /// The uppercase letters 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.
        /// </summary>
        public const string ascii_uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>
        /// The concatenation of <see cref="ascii_lowercase"/> and <see cref="ascii_uppercase"/>.
        /// </summary>
        public const string ascii_letters = ascii_lowercase + ascii_uppercase;

        /// <summary>
        /// The string '0123456789'.
        /// </summary>
        public const string digits = "0123456789";

        /// <summary>
        /// The string '0123456789abcdefABCDEF'.
        /// </summary>
        public const string hexdigits = "0123456789abcdefABCDEF";

        /// <summary>
        /// The string '01234567'.
        /// </summary>
        public const string octdigits = "01234567";

        /// <summary>
        /// String of ASCII characters which are considered punctuation characters
        /// in the C locale: !"#$%&amp;'()*+,-./:;&lt;=&gt;?@[\]^_`{|}~
        /// </summary>
        public const string punctuation = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

        /// <summary>
        /// A string containing whitespace characters: space, tab, linefeed,
        /// return, formfeed, and vertical tab.
        /// </summary>
        public const string whitespace = " \t\n\r\x0b\x0c";

        /// <summary>
        /// String of ASCII characters which are considered printable.
        /// This is a combination of <see cref="digits"/>, <see cref="ascii_letters"/>,
        /// <see cref="punctuation"/>, and <see cref="whitespace"/>.
        /// </summary>
        public const string printable = digits + ascii_letters + punctuation + whitespace;
    }
}
