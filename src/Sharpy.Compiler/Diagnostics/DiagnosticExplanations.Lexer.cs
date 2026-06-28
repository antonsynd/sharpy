namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// DiagnosticExplanations partial: Lexer diagnostic entries (SPY0001-SPY0099)
/// </summary>
public static partial class DiagnosticExplanations
{
    private static void AddLexerEntries(Dictionary<string, DiagnosticExplanation> dict)
    {
        // ── Lexer errors (SPY0001-SPY0099) ──────────────────────────────

        Add(dict, DiagnosticCodes.Lexer.UnterminatedString, "Unterminated string literal", "Lexer",
            "A string literal was opened with a quote character but never closed. The lexer reached the end of the line or file without finding a matching closing quote.",
            "x: str = \"hello",
            "Add the closing quote:\n  x: str = \"hello\"");

        Add(dict, DiagnosticCodes.Lexer.UnterminatedFString, "Unterminated f-string literal", "Lexer",
            "An f-string literal was opened with f\" but never closed. The lexer reached the end of the line or file without finding a matching closing quote.",
            "msg: str = f\"Hello, {name}",
            "Add the closing quote:\n  msg: str = f\"Hello, {name}\"");

        Add(dict, DiagnosticCodes.Lexer.UnterminatedRawString, "Unterminated raw string literal", "Lexer",
            "A raw string literal was opened with r\" but never closed. Raw strings treat backslashes as literal characters.",
            "path: str = r\"C:\\Users\\name",
            "Add the closing quote:\n  path: str = r\"C:\\Users\\name\"");

        Add(dict, DiagnosticCodes.Lexer.InvalidEscapeSequence, "Invalid escape sequence", "Lexer",
            "A backslash in a string literal is followed by a character that is not a recognized escape sequence. Valid escapes include \\n, \\t, \\\\, \\\", \\', \\0, \\a, \\b, \\f, \\r, \\v, \\x, and \\u.",
            "path: str = \"C:\\new_folder\"",
            "Use a raw string or double the backslash:\n  path: str = r\"C:\\new_folder\"\n  path: str = \"C:\\\\new_folder\"");

        Add(dict, DiagnosticCodes.Lexer.InvalidHexEscape, "Invalid hex escape sequence", "Lexer",
            "A \\x escape sequence in a string is not followed by exactly two valid hexadecimal digits (0-9, a-f, A-F).",
            "s: str = \"\\xZZ\"",
            "Use valid hex digits:\n  s: str = \"\\x41\"  # 'A'");

        Add(dict, DiagnosticCodes.Lexer.InvalidUnicodeEscape, "Invalid unicode escape sequence", "Lexer",
            "A \\u escape sequence in a string is not followed by exactly four valid hexadecimal digits representing a Unicode code point.",
            "s: str = \"\\u00GG\"",
            "Use valid hex digits:\n  s: str = \"\\u0041\"  # 'A'");

        Add(dict, DiagnosticCodes.Lexer.InvalidNumber, "Invalid number literal", "Lexer",
            "A numeric literal is malformed. This may be caused by multiple decimal points, invalid digit sequences, or other formatting issues.",
            "x: float = 3.14.15",
            "Use a valid number format:\n  x: float = 3.1415");

        Add(dict, DiagnosticCodes.Lexer.InvalidHexLiteral, "Invalid hex literal", "Lexer",
            "A hexadecimal literal starting with 0x is not followed by valid hex digits (0-9, a-f, A-F).",
            "x: int = 0xGG",
            "Use valid hex digits:\n  x: int = 0xFF");

        Add(dict, DiagnosticCodes.Lexer.InvalidBinaryLiteral, "Invalid binary literal", "Lexer",
            "A binary literal starting with 0b is not followed by valid binary digits (0 or 1).",
            "x: int = 0b1234",
            "Use only 0 and 1:\n  x: int = 0b1010");

        Add(dict, DiagnosticCodes.Lexer.InvalidOctalLiteral, "Invalid octal literal", "Lexer",
            "An octal literal starting with 0o is not followed by valid octal digits (0-7).",
            "x: int = 0o89",
            "Use digits 0-7:\n  x: int = 0o77");

        Add(dict, DiagnosticCodes.Lexer.MixedTabsAndSpaces, "Mixed tabs and spaces", "Lexer",
            "The source file uses both tabs and spaces for indentation. Sharpy requires consistent indentation using spaces only (4 spaces per level).",
            null,
            "Convert all tabs to spaces. Most editors have a setting to convert tabs to spaces automatically.");

        Add(dict, DiagnosticCodes.Lexer.TabsNotAllowed, "Tabs not allowed for indentation", "Lexer",
            "Tab characters were used for indentation. Sharpy requires spaces only (4 spaces per indentation level).",
            null,
            "Replace tabs with 4 spaces per indentation level. Most editors can be configured to insert spaces when the Tab key is pressed.");

        Add(dict, DiagnosticCodes.Lexer.InvalidIndentation, "Invalid indentation", "Lexer",
            "The indentation level does not match any expected indentation. Each indentation level must be exactly 4 spaces.",
            "def foo():\n   return 1  # 3 spaces instead of 4",
            "Use exactly 4 spaces per indentation level:\ndef foo():\n    return 1");

        Add(dict, DiagnosticCodes.Lexer.IndentationMismatch, "Indentation mismatch", "Lexer",
            "The indentation level does not match the expected dedent level. When decreasing indentation, it must return to a previously established indentation level.",
            "def foo():\n    if True:\n        x: int = 1\n  y: int = 2  # doesn't match any previous level",
            "Align the indentation with a previous level:\ndef foo():\n    if True:\n        x: int = 1\n    y: int = 2");

        Add(dict, DiagnosticCodes.Lexer.UnexpectedCharacter, "Unexpected character", "Lexer",
            "The lexer encountered a character that is not part of any valid token. This may be a non-ASCII character, a stray symbol, or a character that is not valid in the current context.",
            "x: int = 42§",
            "Remove or replace the unexpected character.");

        Add(dict, DiagnosticCodes.Lexer.BackslashAtEof, "Backslash at end of file", "Lexer",
            "A line continuation backslash (\\) was found at the very end of the file with no following line.",
            "x: int = 1 + \\",
            "Remove the trailing backslash or add the continuation on the next line:\n  x: int = 1 + \\\n      2");

        Add(dict, DiagnosticCodes.Lexer.BackslashTrailingWhitespace, "Backslash followed by whitespace", "Lexer",
            "A line continuation backslash (\\) is followed by whitespace before the newline. The backslash must be the last character on the line.",
            null,
            "Remove any spaces or tabs after the backslash.");

        Add(dict, DiagnosticCodes.Lexer.UnterminatedBacktickIdentifier, "Unterminated backtick identifier", "Lexer",
            "A backtick-quoted identifier was opened with ` but never closed. Backtick identifiers allow using reserved words as names.",
            "x: int = `class",
            "Add the closing backtick:\n  x: int = `class`");

        Add(dict, DiagnosticCodes.Lexer.InvalidFloatLiteral, "Invalid float literal", "Lexer",
            "A floating-point literal is malformed. This may be caused by missing digits after the decimal point or invalid exponent notation.",
            "x: float = 1.e",
            "Use a valid float format:\n  x: float = 1.0\n  x: float = 1.0e10");

        Add(dict, DiagnosticCodes.Lexer.UnterminatedFStringExpression, "Unterminated f-string expression", "Lexer",
            "An expression inside an f-string (within { }) was not properly closed. The lexer reached the end of the string without finding the closing brace.",
            "msg: str = f\"Value: {x + 1\"",
            "Close the expression brace:\n  msg: str = f\"Value: {x + 1}\"");

        Add(dict, DiagnosticCodes.Lexer.UnmatchedBraceInFString, "Unmatched brace in f-string", "Lexer",
            "A closing brace } was found in an f-string without a matching opening brace, or vice versa. To include a literal brace in an f-string, use {{ or }}.",
            "msg: str = f\"100%}\"",
            "Escape literal braces by doubling them:\n  msg: str = f\"100%}}\"");

        Add(dict, DiagnosticCodes.Lexer.UnterminatedFormatSpec, "Unterminated format specifier", "Lexer",
            "A format specifier in an f-string expression (after the colon) was not properly terminated.",
            "msg: str = f\"{value:.2f\"",
            "Close the expression brace:\n  msg: str = f\"{value:.2f}\"");

        Add(dict, DiagnosticCodes.Lexer.InvalidFStringConversion, "Invalid f-string conversion flag", "Lexer",
            "An f-string conversion flag (after '!') must be one of 'r' (repr), 's' (str), or 'a' (ascii), followed by '}' or a ':' format spec.",
            "msg: str = f\"{value!q}\"",
            "Use a valid conversion flag:\n  msg: str = f\"{value!r}\"");

        Add(dict, DiagnosticCodes.Lexer.InvalidNumericSuffix, "Invalid numeric suffix", "Lexer",
            "A numeric literal is followed by characters that form an invalid suffix. Numeric literals must not be immediately followed by identifier characters.",
            "x: int = 42abc",
            "Separate the number from the identifier:\n  x: int = 42\n  abc: str = \"value\"");

        Add(dict, DiagnosticCodes.Lexer.OctalEscapeOverflow, "Octal escape overflow", "Lexer",
            "An octal escape sequence (\\NNN) represents a value greater than 255 (\\377), which is out of range for a single byte.",
            "s: str = \"\\400\"",
            "Use a value within the valid range (\\000 to \\377):\n  s: str = \"\\377\"");

        Add(dict, DiagnosticCodes.Lexer.DotInBacktickIdentifier, "Dot in backtick identifier", "Lexer",
            "A backtick-delimited identifier contains a dot (.), which is not allowed. Dots are namespace/member separators and cannot appear inside a single identifier. Use separate backtick-delimited segments joined by dots instead.",
            "import `System.IO`",
            "Split into separate backtick segments:\n  import `System`.IO");

        Add(dict, DiagnosticCodes.Lexer.UnterminatedByteString, "Unterminated byte string literal", "Lexer",
            "A byte string literal was opened with b\" or b' but never closed. The lexer reached the end of the line or file without finding a matching closing quote.",
            "data: bytes = b\"hello",
            "Add the closing quote:\n  data: bytes = b\"hello\"");

        Add(dict, DiagnosticCodes.Lexer.UnicodeEscapeInByteString, "Unicode escape in byte string", "Lexer",
            "A byte string literal contains a \\u or \\U unicode escape sequence, which is not allowed. Byte strings can only contain values in the 0-255 range. Use \\x escapes for hex byte values instead.",
            "data: bytes = b\"\\u0041\"",
            "Use a hex escape instead:\n  data: bytes = b\"\\x41\"");

        Add(dict, DiagnosticCodes.Lexer.NonAsciiInByteString, "Non-ASCII character in byte string", "Lexer",
            "A byte string literal contains a non-ASCII character (code point > 127). Byte strings can only contain ASCII literal characters. Use \\x escape sequences for non-ASCII byte values.",
            "data: bytes = b\"€\"",
            "Use a hex escape instead:\n  data: bytes = b\"\\xe2\\x82\\xac\"");

        Add(dict, DiagnosticCodes.Lexer.DedentedStringIndentationError, "Dedented string indentation error", "Lexer",
            "A dedented (d/dr/df-prefixed) triple-quoted string has inconsistent indentation. The amount of leading whitespace to strip is determined by the indentation of the closing triple-quote line; every non-blank content line must begin with at least that many whitespace characters, and the closing delimiter's line must contain only whitespace before the closing \"\"\".",
            "msg: str = d\"\"\"\n    hello\n  world\n    \"\"\"",
            "Align every content line (and the closing \"\"\" delimiter) to a consistent indentation:\n  msg: str = d\"\"\"\n      hello\n      world\n      \"\"\"");

    }
}
