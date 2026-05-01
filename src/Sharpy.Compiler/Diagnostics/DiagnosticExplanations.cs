namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Detailed explanation for a diagnostic code.
/// </summary>
public class DiagnosticExplanation
{
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Example { get; set; }
    public string? Fix { get; set; }
    public string Category { get; set; } = "";
}

/// <summary>
/// Registry of detailed explanations for Sharpy compiler diagnostic codes.
/// Used by the <c>sharpyc explain</c> command to provide rich error documentation.
/// </summary>
public static class DiagnosticExplanations
{
    private static readonly Dictionary<string, DiagnosticExplanation> _explanations = BuildExplanations();

    /// <summary>
    /// Look up an explanation by diagnostic code (case-insensitive).
    /// Returns null if the code is not documented.
    /// </summary>
    public static DiagnosticExplanation? Get(string code)
    {
        _explanations.TryGetValue(code.ToUpperInvariant(), out var explanation);
        return explanation;
    }

    /// <summary>
    /// Returns all documented diagnostic explanations.
    /// </summary>
    public static IReadOnlyDictionary<string, DiagnosticExplanation> GetAll() => _explanations;

    private static Dictionary<string, DiagnosticExplanation> BuildExplanations()
    {
        var dict = new Dictionary<string, DiagnosticExplanation>(StringComparer.OrdinalIgnoreCase);

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

        // ── Parser errors (SPY0100-SPY0199) ─────────────────────────────

        Add(dict, DiagnosticCodes.Parser.UnexpectedToken, "Unexpected token", "Parser",
            "The parser encountered a token that does not fit the expected grammar at this position. This usually indicates a syntax error such as a missing operator, misplaced keyword, or malformed expression.",
            "x: int = if",
            "Check the syntax around the reported location. Common causes: missing operators, extra commas, or misplaced keywords.");

        Add(dict, DiagnosticCodes.Parser.ExpectedIdentifier, "Expected identifier", "Parser",
            "The parser expected a name (identifier) but found something else. This commonly occurs when a keyword is used where a variable or function name is expected.",
            "def 42():\n    pass",
            "Provide a valid identifier:\ndef my_function():\n    pass");

        Add(dict, DiagnosticCodes.Parser.ExpectedNewline, "Expected newline", "Parser",
            "The parser expected a newline (end of statement) but found additional tokens. Each statement must be on its own line.",
            "x: int = 1 y: int = 2",
            "Put each statement on its own line:\n  x: int = 1\n  y: int = 2");

        Add(dict, DiagnosticCodes.Parser.ExpectedEndOfStatement, "Expected end of statement", "Parser",
            "The parser reached a point where a statement should have ended but found additional tokens that don't form a valid continuation.",
            "return 1 2 3",
            "Remove the extra tokens or split into separate statements.");

        Add(dict, DiagnosticCodes.Parser.ExpectedToken, "Expected token", "Parser",
            "A specific token (such as a colon, parenthesis, or comma) was expected but not found. The error message will indicate which token was expected.",
            "def greet(name: str)\n    print(name)",
            "Add the missing token. In this example, add a colon after the parameter list:\ndef greet(name: str):\n    print(name)");

        Add(dict, DiagnosticCodes.Parser.InvalidDecoratorTarget, "Invalid decorator target", "Parser",
            "A decorator was applied to a statement that cannot be decorated. Decorators can only be applied to function definitions and class definitions.",
            "@staticmethod\nx: int = 42",
            "Apply the decorator to a function or class:\n@staticmethod\ndef my_method():\n    pass");

        Add(dict, DiagnosticCodes.Parser.TupleAsStatement, "Tuple expression as statement", "Parser",
            "A comma-separated expression (tuple) was used as a standalone statement, which has no effect. This is likely a mistake.",
            "def main():\n    1, 2, 3",
            "If you meant to create a tuple, assign it to a variable:\n  t: tuple[int, int, int] = (1, 2, 3)");

        Add(dict, DiagnosticCodes.Parser.InvalidTypeAnnotationTarget, "Invalid type annotation target", "Parser",
            "A type annotation was applied to an expression that cannot have one. Type annotations can only be applied to simple variable names.",
            "x[0]: int = 42",
            "Use a type annotation only on simple names:\n  x: list[int] = [42]");

        Add(dict, DiagnosticCodes.Parser.PositionalAfterKeyword, "Positional argument after keyword argument", "Parser",
            "A positional argument appeared after a keyword argument in a function call. Once you use a keyword argument, all subsequent arguments must also be keyword arguments.",
            "greet(name=\"Alice\", 42)",
            "Move positional arguments before keyword arguments, or use keyword syntax for all:\ngreet(42, name=\"Alice\")\ngreet(age=42, name=\"Alice\")");

        Add(dict, DiagnosticCodes.Parser.MultipleVariadic, "Multiple variadic parameters", "Parser",
            "A function has more than one variadic (*args) parameter. Only one variadic parameter is allowed per function.",
            "def foo(*a: int, *b: str):\n    pass",
            "Use only one variadic parameter:\ndef foo(*a: int, name: str = \"\"):\n    pass");

        Add(dict, DiagnosticCodes.Parser.VariadicWithDefault, "Variadic parameter with default value", "Parser",
            "A variadic (*args) parameter was given a default value. Variadic parameters collect zero or more arguments and cannot have defaults.",
            "def foo(*args: int = [1, 2]):\n    pass",
            "Remove the default value:\ndef foo(*args: int):\n    pass");

        Add(dict, DiagnosticCodes.Parser.VariadicNotLast, "Variadic parameter not last", "Parser",
            "A variadic (*args) parameter must be the last positional parameter in a function definition.",
            "def foo(*args: int, x: int):\n    pass",
            "Move the variadic parameter to the end:\ndef foo(x: int, *args: int):\n    pass");

        Add(dict, DiagnosticCodes.Parser.EmptyEnum, "Empty enum", "Parser",
            "An enum declaration has no members. Enums must have at least one member.",
            "enum Color:\n    pass",
            "Add at least one enum member:\nenum Color:\n    RED\n    GREEN\n    BLUE");

        Add(dict, DiagnosticCodes.Parser.FreeUnionNotSupported, "Free union types not supported", "Parser",
            "A union type annotation like `int | str` was used outside of a supported context. Sharpy uses Optional[T] for nullable types and does not support arbitrary union types.",
            "x: int | str = 42",
            "Use a common base type, Optional[T] for nullable values, or redesign to avoid union types:\nx: Optional[int] = 42");

        Add(dict, DiagnosticCodes.Parser.EmptyListShorthand, "Empty list shorthand not allowed", "Parser",
            "An empty list type shorthand [] was used without a type parameter. List type annotations require an element type.",
            "x: [] = []",
            "Specify the element type:\n  x: list[int] = []");

        Add(dict, DiagnosticCodes.Parser.EmptySetDictShorthand, "Empty set/dict shorthand not allowed", "Parser",
            "An empty set or dict type shorthand {} was used without type parameters. Set and dict type annotations require element types.",
            "x: {} = {}",
            "Specify the types:\n  x: dict[str, int] = {}\n  x: set[int] = set()");

        Add(dict, DiagnosticCodes.Parser.ExpectedModuleName, "Expected module name", "Parser",
            "An import statement is missing the module name to import from.",
            "from import foo",
            "Provide the module name:\n  from my_module import foo");

        Add(dict, DiagnosticCodes.Parser.ExpectedDecoratorName, "Expected decorator name", "Parser",
            "A decorator (@) was not followed by a valid decorator name.",
            "@\ndef foo():\n    pass",
            "Add a valid decorator name:\n@staticmethod\ndef foo():\n    pass");

        Add(dict, DiagnosticCodes.Parser.MixedNamedUnnamedTupleElements, "Mixed named and unnamed tuple elements", "Parser",
            "A tuple type or literal has a mix of named and unnamed elements. All elements must be either named or unnamed.",
            "type Point = tuple[x: float, float]",
            "Make all elements named or all unnamed:\n  type Point = tuple[x: float, y: float]\n  type Pair = tuple[float, float]");

        Add(dict, DiagnosticCodes.Parser.MaxRecursionDepthExceeded, "Maximum recursion depth exceeded", "Parser",
            "The parser exceeded its maximum nesting depth while parsing deeply nested expressions. This typically indicates excessively nested parentheses, operators, or other constructs that create deep parse trees.",
            "# Extremely deeply nested expression\nresult = (((((((...)))))))",
            "Simplify the expression by breaking it into intermediate variables:\n  part1 = a + b\n  part2 = part1 * c\n  result = part2 + d");

        Add(dict, DiagnosticCodes.Parser.ExpectedPattern, "Expected pattern", "Parser",
            "The parser expected a pattern in a match case but found something else. Valid patterns include: variable bindings, literal values, tuple patterns, and wildcard (_).",
            "match x:\n    case ???:\n        pass",
            "Use a valid pattern:\nmatch x:\n    case 42:\n        print(\"found it\")\n    case _:\n        print(\"default\")");

        Add(dict, DiagnosticCodes.Parser.ExpectedCase, "Expected case clause", "Parser",
            "A match statement must contain at least one 'case' clause. The parser found the match body but no case clauses inside it.",
            "match x:\n    pass",
            "Add at least one case clause:\nmatch x:\n    case _:\n        pass");

        Add(dict, DiagnosticCodes.Parser.RaiseFromNotSupported, "'raise ... from ...' not supported", "Parser",
            "Sharpy does not support the Python 'raise ... from ...' syntax for exception chaining. This feature relies on runtime exception mutation that does not map cleanly to .NET's immutable inner exception model.",
            "raise RuntimeError(\"Failed\") from original_error",
            "Pass the cause as a constructor argument instead:\nraise RuntimeError(\"Failed\", original_error)");

        Add(dict, DiagnosticCodes.Parser.DictSpreadCallNotSupported, "Dict spread in calls not supported", "Parser",
            "Dict spread arguments (**expr) in function calls are not yet supported. This feature requires keyword argument expansion at compile time, which is planned for a future release.",
            "f(**kwargs)",
            "Pass keyword arguments explicitly:\nf(key1=value1, key2=value2)");

        Add(dict, DiagnosticCodes.Parser.EmptyUnion, "Empty union", "Parser",
            "A union declaration has no cases. Unions must have at least one case.",
            "union Shape:\n    pass",
            "Add at least one union case:\nunion Shape:\n    case Circle(radius: float)\n    case Rectangle(width: float, height: float)");

        Add(dict, DiagnosticCodes.Parser.GenericTypeInPattern, "Generic type in pattern", "Parser",
            "Generic type arguments (e.g., Box[int]) are not supported in match case patterns. The compiler matches by the base type name; type arguments are not needed for pattern matching.",
            "match value:\n    case Box[int]() as x:\n        ...",
            "Remove the type arguments from the pattern:\nmatch value:\n    case Box() as x:\n        ...");

        Add(dict, DiagnosticCodes.Parser.SlashAfterStar, "'/' after '*' in parameter list", "Parser",
            "The positional-only marker '/' must appear before the keyword-only marker '*' or variadic '*args' in a parameter list. Having '/' after '*' is not valid.",
            "def foo(a: int, *, b: int, /): ...",
            "Place '/' before '*':\ndef foo(a: int, /, *, b: int): ...");

        Add(dict, DiagnosticCodes.Parser.DuplicateSlashMarker, "Duplicate '/' marker", "Parser",
            "The positional-only marker '/' can only appear once in a parameter list.",
            "def foo(a: int, /, b: int, /): ...",
            "Use '/' only once:\ndef foo(a: int, /, b: int): ...");

        Add(dict, DiagnosticCodes.Parser.DuplicateStarMarker, "Duplicate '*' marker", "Parser",
            "The keyword-only marker '*' (or '*args') can only appear once in a parameter list. A second bare '*' or '*args' is not allowed.",
            "def foo(*, a: int, *, b: int): ...",
            "Use '*' only once:\ndef foo(*, a: int, b: int): ...");

        Add(dict, DiagnosticCodes.Parser.SlashAtStart, "'/' at start of parameter list", "Parser",
            "The positional-only marker '/' must have at least one parameter before it. Parameters preceding '/' become positional-only.",
            "def foo(/, a: int): ...",
            "Place at least one parameter before '/':\ndef foo(a: int, /): ...");

        Add(dict, DiagnosticCodes.Parser.PlaceholderInKeywordArg, "Placeholder in keyword argument", "Parser",
            "The '_' placeholder for partial application cannot be used as the value of a keyword argument. Placeholders are only allowed in positional arguments.",
            "result = f(x=_)",
            "Use a lambda instead:\nresult = lambda v: f(x=v)");

        Add(dict, DiagnosticCodes.Parser.PlaceholderWithSpread, "Placeholder with spread argument", "Parser",
            "The '_' placeholder for partial application cannot be mixed with spread (*) arguments in the same call. The number of arguments would be ambiguous.",
            "result = f(_, *args)",
            "Use a lambda instead:\nresult = lambda v: f(v, *args)");

        Add(dict, DiagnosticCodes.Parser.PlaceholderOutsideCallOrOperator, "Placeholder outside call or operator", "Parser",
            "The '_' placeholder for partial application is only valid inside function call arguments or parenthesized operator expressions.",
            "x = _",
            "Use '_' inside a function call:\nx = f(_)");

        Add(dict, DiagnosticCodes.Parser.NestedPlaceholder, "Nested placeholder", "Parser",
            "The '_' placeholder cannot be used in nested positions within partial application expressions.",
            "result = f(g(_)(_))",
            "Break into separate expressions:\ninner = g(_)\nresult = f(inner(_))");

        Add(dict, DiagnosticCodes.Parser.RejectedPythonKeyword, "Rejected Python keyword", "Parser",
            "This Python keyword is not supported in Sharpy. Sharpy follows C# scoping and lifetime rules (Axiom 1: .NET first), so Python-specific scope modifiers like 'global' and 'nonlocal' do not apply.",
            "global x",
            "Remove the keyword. Variables in Sharpy follow C# scoping rules — closures capture by reference automatically.");

        Add(dict, DiagnosticCodes.Parser.AutoEventWithBody, "Auto-event with accessor keyword", "Parser",
            "An auto-event declaration ('event name: DelegateType') must not have an 'add' or 'remove' accessor keyword. Auto-events generate backing delegate fields and accessors automatically.",
            "event add on_click: EventHandler",
            "Remove the accessor keyword for auto-events:\n  event on_click: EventHandler\n\nOr use function-style syntax for custom accessors:\n  event add on_click(self, handler: EventHandler):\n      ...");

        Add(dict, DiagnosticCodes.Parser.FunctionStyleEventWithoutAccessor, "Function-style event without accessor", "Parser",
            "A function-style event with parameters requires an 'add' or 'remove' accessor keyword. Without it, the parser cannot determine which accessor is being defined.",
            "event on_click(self, handler: EventHandler):\n    ...",
            "Add 'add' or 'remove' keyword:\n  event add on_click(self, handler: EventHandler):\n      ...\n  event remove on_click(self, handler: EventHandler):\n      ...");

        Add(dict, DiagnosticCodes.Parser.ExceptWithAsRequiresParens, "Multiple exception types with 'as' require parentheses", "Parser",
            "When catching multiple exception types and binding the exception to a name with 'as', the exception types must be enclosed in parentheses. Without parentheses, the 'as' clause is ambiguous.",
            "except ValueError, TypeError as e:\n    print(e)",
            "Wrap the exception types in parentheses:\n  except (ValueError, TypeError) as e:\n      print(e)");

        Add(dict, DiagnosticCodes.Parser.ExceptStarRequiresType, "'except*' requires an exception type", "Parser",
            "An 'except*' handler (PEP 654) must specify an exception type. Unlike regular 'except', bare 'except*:' without a type is not allowed because each except* handler matches a specific type within an ExceptionGroup.",
            "try:\n    ...\nexcept*:\n    print(\"error\")",
            "Specify the exception type:\n  except* ValueError as eg:\n      print(eg)");

        Add(dict, DiagnosticCodes.Parser.MixedExceptAndExceptStar, "Cannot mix 'except' and 'except*' handlers", "Parser",
            "A try block cannot have both regular 'except' and 'except*' handlers. The 'except*' syntax (PEP 654) is used exclusively with ExceptionGroup handling and cannot be mixed with traditional exception handling.",
            "try:\n    ...\nexcept ValueError:\n    ...\nexcept* TypeError as eg:\n    ...",
            "Use either all 'except' or all 'except*' handlers:\n  try:\n      ...\n  except* ValueError as eg:\n      ...\n  except* TypeError as eg:\n      ...");

        // ── Semantic errors: Name resolution (SPY0200-SPY0219) ──────────

        Add(dict, DiagnosticCodes.Semantic.UndefinedVariable, "Undefined variable", "Semantic",
            "A variable was referenced that has not been declared in the current scope or any enclosing scope. Variables must be declared with a type annotation before use.",
            "def main():\n    print(x)  # x is not defined",
            "Declare the variable before using it:\ndef main():\n    x: int = 42\n    print(x)");

        Add(dict, DiagnosticCodes.Semantic.UndefinedFunction, "Undefined function", "Semantic",
            "A function was called that has not been defined or imported. Check for typos in the function name and ensure the module containing the function has been imported.",
            "def main():\n    result = calculate(1, 2)  # calculate not defined",
            "Define the function or import it:\ndef calculate(a: int, b: int) -> int:\n    return a + b");

        Add(dict, DiagnosticCodes.Semantic.UndefinedType, "Undefined type", "Semantic",
            "A type was referenced in an annotation or expression that has not been defined or imported. This includes class names, struct names, enum names, and type aliases.",
            "x: Widget = Widget()  # Widget not defined",
            "Define the type or import it:\nclass Widget:\n    name: str");

        Add(dict, DiagnosticCodes.Semantic.UndefinedMember, "Undefined member", "Semantic",
            "An attribute or method was accessed on a type that does not define it. This can occur with field access, method calls, or property access.",
            "class Point:\n    x: int\n    y: int\n\np = Point(1, 2)\nprint(p.z)  # Point has no member 'z'",
            "Check the type definition for available members. Fix the member name or add the missing member to the type.");

        Add(dict, DiagnosticCodes.Semantic.DuplicateDefinition, "Duplicate definition", "Semantic",
            "A name was defined more than once in the same scope. Each name can only be defined once per scope (function, class, or module level).",
            "x: int = 1\nx: str = \"hello\"  # duplicate",
            "Use a different name or remove the duplicate definition.");

        Add(dict, DiagnosticCodes.Semantic.DuplicateParameter, "Duplicate parameter", "Semantic",
            "A function or method has two parameters with the same name.",
            "def add(x: int, x: int) -> int:\n    return x + x",
            "Give each parameter a unique name:\ndef add(x: int, y: int) -> int:\n    return x + y");

        Add(dict, DiagnosticCodes.Semantic.InvalidTypeAlias, "Invalid type alias", "Semantic",
            "A type alias definition is invalid. The target type may not be a valid type expression.",
            "type Bad = 42",
            "Use a valid type expression:\n  type IntList = list[int]\n  type Callback = Callable[[int], str]");

        // ── Semantic errors: Type checking (SPY0220-SPY0259) ────────────

        Add(dict, DiagnosticCodes.Semantic.TypeMismatch, "Type mismatch", "Semantic",
            "The actual type of an expression does not match the expected type. This can occur in assignments, function arguments, return statements, and other contexts where a specific type is required.",
            "x: int = \"hello\"  # str assigned to int",
            "Ensure the types match. Either change the value, add a type conversion, or change the type annotation.");

        Add(dict, DiagnosticCodes.Semantic.IncompatibleTypes, "Incompatible types", "Semantic",
            "Two types are incompatible in the given context. This is similar to a type mismatch but may involve more complex type relationships such as generic constraints or inheritance hierarchies.",
            "items: list[int] = [1, 2, 3]\nitems = \"hello\"  # list[int] and str are incompatible",
            "Ensure the types are compatible. Use explicit conversions if needed.");

        Add(dict, DiagnosticCodes.Semantic.InvalidBinaryOperation, "Invalid binary operation", "Semantic",
            "A binary operator was used with operand types that do not support it. For example, using + between an int and a bool, or - with strings.",
            "x: str = \"hello\" - \"world\"  # str doesn't support -",
            "Use an operator that is supported for the given types, or convert the operands to compatible types.");

        Add(dict, DiagnosticCodes.Semantic.InvalidUnaryOperation, "Invalid unary operation", "Semantic",
            "A unary operator was used with an operand type that does not support it. For example, using the negation operator (-) on a string.",
            "x: str = -\"hello\"",
            "Use an operator that is supported for the given type.");

        Add(dict, DiagnosticCodes.Semantic.WrongArgumentCount, "Wrong argument count", "Semantic",
            "A function or method was called with the wrong number of arguments. The error message indicates how many arguments were expected and how many were provided.",
            "def greet(name: str) -> str:\n    return f\"Hello, {name}\"\n\ngreet()  # expected 1 argument",
            "Provide the correct number of arguments:\ngreet(\"Alice\")");

        Add(dict, DiagnosticCodes.Semantic.InvalidAssignmentTarget, "Invalid assignment target", "Semantic",
            "The left-hand side of an assignment is not a valid target. Assignments can only target variables, fields, and subscript expressions.",
            "1 + 2 = 3  # cannot assign to expression",
            "Assign to a variable or field:\n  result: int = 1 + 2");

        Add(dict, DiagnosticCodes.Semantic.MissingTypeAnnotation, "Missing type annotation", "Semantic",
            "A variable declaration is missing a type annotation. Sharpy requires explicit type annotations for all variable declarations to ensure static type safety.",
            "x = 42  # missing type annotation",
            "Add a type annotation:\nx: int = 42");

        Add(dict, DiagnosticCodes.Semantic.CannotInferType, "Cannot infer type", "Semantic",
            "The compiler cannot determine the type of an expression. This may occur with complex expressions or when type information is insufficient.",
            "x: auto = some_complex_expression()",
            "Add an explicit type annotation to help the compiler:\n  x: int = some_complex_expression()");

        Add(dict, DiagnosticCodes.Semantic.InvalidCast, "Invalid cast", "Semantic",
            "A type cast is invalid because the source and target types are not compatible. Only related types can be cast to each other.",
            "x: int = int(\"not_a_number\")  # runtime error potential",
            "Ensure the cast is between compatible types or use a conversion function.");

        Add(dict, DiagnosticCodes.Semantic.NullabilityViolation, "Nullability violation", "Semantic",
            "A potentially null value is being used in a context that requires a non-null value. Use Optional[T] for values that can be None, and handle the None case before using the value.",
            "def get_name() -> Optional[str]:\n    return None\n\nname: str = get_name()  # might be None",
            "Handle the null case:\nresult = get_name()\nif result is not None:\n    name: str = result");

        Add(dict, DiagnosticCodes.Semantic.NotCallable, "Type is not callable", "Semantic",
            "An expression was called like a function but its type does not support being called. Only functions, methods, and types with __call__ can be called.",
            "x: int = 42\ny: int = x()  # int is not callable",
            "Ensure you are calling a function or method, not a value.");

        Add(dict, DiagnosticCodes.Semantic.InvalidPipeTarget, "Invalid pipe target", "Semantic",
            "The right-hand side of a pipe operator (|>) is not a valid pipe target. The target must be a callable that accepts the piped value as its first argument.",
            "42 |> \"hello\"  # str is not a valid pipe target",
            "Pipe into a function:\n  42 |> str\n  items |> len");

        Add(dict, DiagnosticCodes.Semantic.InvalidSelfUsage, "Invalid 'self' usage", "Semantic",
            "'self' was used outside of an instance method or in an invalid context. 'self' is only available inside instance methods of a class.",
            "def free_function():\n    print(self.x)  # no self outside a class",
            "Move the code into a class method or pass the object as a parameter.");

        Add(dict, DiagnosticCodes.Semantic.InvalidNothingUsage, "Invalid 'Nothing' usage", "Semantic",
            "'Nothing' was used in an invalid context. Nothing is the bottom type and can only be used in specific type-level contexts.",
            "x: Nothing = 42",
            "Nothing is typically used as a return type for functions that never return:\ndef fail(msg: str) -> Nothing:\n    raise Error(msg)");

        Add(dict, DiagnosticCodes.Semantic.UnknownKeywordArgument, "Unknown keyword argument", "Semantic",
            "A keyword argument was passed to a function using a name that does not match any parameter. Check the function signature for the correct parameter names.",
            "def greet(name: str):\n    print(name)\n\ngreet(nme=\"Alice\")  # typo in 'name'",
            "Use the correct parameter name:\n  greet(name=\"Alice\")");

        Add(dict, DiagnosticCodes.Semantic.DuplicateArgument, "Duplicate argument", "Semantic",
            "The same argument was provided more than once in a function call, either as a duplicate keyword argument or as both a positional and keyword argument.",
            "greet(\"Alice\", name=\"Bob\")  # name supplied twice",
            "Remove the duplicate argument:\n  greet(name=\"Alice\")");

        Add(dict, DiagnosticCodes.Semantic.InvalidNullConditional, "Invalid null-conditional access", "Semantic",
            "The null-conditional operator (?.) was used on a type that is not nullable. This operator is only meaningful on Optional[T] types.",
            "x: int = 42\ny = x?.to_string()  # int is never null",
            "Use regular member access for non-nullable types:\n  y = x.to_string()");

        Add(dict, DiagnosticCodes.Semantic.CannotInferGenericType, "Cannot infer generic type argument", "Semantic",
            "The compiler cannot infer the type arguments for a generic type or function. Provide explicit type arguments.",
            "items = []  # can't infer element type",
            "Specify the type:\n  items: list[int] = []");

        Add(dict, DiagnosticCodes.Semantic.InvalidComprehension, "Invalid comprehension", "Semantic",
            "A list, set, or dict comprehension contains invalid syntax or type mismatches in its clauses.",
            "result: list[int] = [x for x in \"hello\"]  # str elements, not int",
            "Ensure the comprehension expression type matches the target type.");

        Add(dict, DiagnosticCodes.Semantic.InvalidTupleUnpacking, "Invalid tuple unpacking", "Semantic",
            "A tuple unpacking assignment has a mismatch between the number of variables and the number of tuple elements.",
            "a, b = (1, 2, 3)  # 2 targets, 3 values",
            "Match the number of variables to the tuple size:\n  a, b, c = (1, 2, 3)");

        Add(dict, DiagnosticCodes.Semantic.InvalidAutoVariable, "Invalid auto variable", "Semantic",
            "A variable declared with 'auto' type cannot have its type inferred from the initializer. The initializer expression's type is ambiguous or unresolvable.",
            null,
            "Add an explicit type annotation instead of using 'auto'.");

        Add(dict, DiagnosticCodes.Semantic.ConditionNotBoolean, "Condition is not boolean", "Semantic",
            "An expression used as a condition in an if, while, or similar statement does not evaluate to a boolean type. Conditions must be explicitly boolean in Sharpy.",
            "x: int = 42\nif x:  # int is not bool\n    print(\"truthy\")",
            "Use an explicit comparison:\nif x != 0:\n    print(\"non-zero\")");

        Add(dict, DiagnosticCodes.Semantic.InvalidRaise, "Invalid raise statement", "Semantic",
            "A raise statement is used incorrectly. Bare 'raise' can only be used inside an except block, and 'raise X' requires X to be an exception type.",
            "def foo():\n    raise  # bare raise outside except block",
            "Raise a specific exception:\n  raise ValueError(\"something went wrong\")\n\nOr use bare raise inside an except block:\n  try:\n      ...\n  except Exception as e:\n      raise");

        Add(dict, DiagnosticCodes.Semantic.InvalidMaybeExpression, "Invalid Maybe expression", "Semantic",
            "A Maybe expression is used incorrectly. Maybe wraps optional values and must be used with compatible types.",
            null,
            "Ensure the expression is compatible with the Maybe/Optional type pattern.");

        Add(dict, DiagnosticCodes.Semantic.InvalidNoneConstructor, "Invalid None constructor usage", "Semantic",
            "None was used in a context that requires a specific type. None can only be used where an Optional[T] type is expected.",
            "x: int = None  # int is not nullable",
            "Use Optional[T] for nullable types:\n  x: Optional[int] = None");

        Add(dict, DiagnosticCodes.Semantic.InvalidSomeConstructor, "Invalid Some constructor usage", "Semantic",
            "The Some() constructor for Optional types was used incorrectly. Some wraps a non-null value into an Optional.",
            "x: Optional[int] = Some(None)  # Some cannot wrap None",
            "Pass a non-null value to Some:\n  x: Optional[int] = Some(42)");

        Add(dict, DiagnosticCodes.Semantic.InvalidOkErrConstructor, "Invalid Ok/Err constructor usage", "Semantic",
            "An Ok() or Err() constructor for Result types was used incorrectly. Ok wraps a success value and Err wraps an error value.",
            null,
            "Ensure Ok/Err is used with compatible Result[T, E] types.");

        Add(dict, DiagnosticCodes.Semantic.MissingMethodBody, "Missing method body", "Semantic",
            "A non-abstract method in a class or struct is missing its body. Only abstract methods and interface methods can omit the body.",
            "class Foo:\n    def bar(self) -> int:",
            "Add a method body:\nclass Foo:\n    def bar(self) -> int:\n        return 42\n\nOr mark the method as abstract:\nabstract class Foo:\n    def bar(self) -> int: ...");

        Add(dict, DiagnosticCodes.Semantic.InvalidOverride, "Invalid override", "Semantic",
            "A method override is invalid. The overriding method must match the signature of the base class method in parameter types and return type.",
            "class Base:\n    def foo(self) -> int:\n        return 1\n\nclass Sub(Base):\n    def foo(self) -> str:  # return type mismatch\n        return \"hello\"",
            "Match the base class method signature:\nclass Sub(Base):\n    def foo(self) -> int:\n        return 2");

        Add(dict, DiagnosticCodes.Semantic.MissingParameterAnnotation, "Missing parameter type annotation", "Semantic",
            "A function parameter is missing its type annotation. All parameters in Sharpy must have explicit type annotations.",
            "def add(a, b) -> int:\n    return a + b",
            "Add type annotations to all parameters:\ndef add(a: int, b: int) -> int:\n    return a + b");

        Add(dict, DiagnosticCodes.Semantic.InvalidDefaultValue, "Invalid default value", "Semantic",
            "A parameter's default value is not compatible with its declared type.",
            "def greet(name: int = \"hello\"):\n    print(name)",
            "Ensure the default value matches the parameter type:\ndef greet(name: str = \"hello\"):\n    print(name)");

        Add(dict, DiagnosticCodes.Semantic.InterfaceMethodBody, "Interface method has body", "Semantic",
            "An interface method was defined with a body. Interface methods must be abstract (no implementation).",
            "interface Printable:\n    def display(self) -> str:\n        return \"hello\"  # not allowed",
            "Remove the body and use ... (ellipsis) for abstract methods:\ninterface Printable:\n    def display(self) -> str: ...");

        Add(dict, DiagnosticCodes.Semantic.UninitializedStructField, "Uninitialized struct field", "Semantic",
            "A struct field does not have a default value and is not initialized in the constructor. Struct fields must be initialized.",
            "struct Point:\n    x: int\n    y: int\n    z: int  # no default, not set in __init__",
            "Provide a default value or ensure all fields are set in the constructor:\nstruct Point:\n    x: int = 0\n    y: int = 0\n    z: int = 0");

        Add(dict, DiagnosticCodes.Semantic.InvalidEnumValue, "Invalid enum value", "Semantic",
            "An enum member has an invalid value. Enum values must be constant expressions of the correct type.",
            "enum Status:\n    ACTIVE = some_function()  # not a constant",
            "Use a constant value:\nenum Status:\n    ACTIVE = 1\n    INACTIVE = 0");

        Add(dict, DiagnosticCodes.Semantic.InvalidFunctionType, "Invalid function type", "Semantic",
            "A function type annotation is invalid. Function types must use the Callable syntax with proper parameter and return types.",
            "f: int -> str = ...  # invalid syntax",
            "Use Callable syntax:\n  f: Callable[[int], str] = my_func");

        Add(dict, DiagnosticCodes.Semantic.UnrecognizedStatementType, "Unrecognized statement type in type checker", "Semantic",
            "The type checker encountered a statement type that it does not have a handler for. " +
            "This is a compiler bug — the type checker is missing a case for this AST node type, " +
            "which means the statement was not type-checked.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        Add(dict, DiagnosticCodes.Semantic.UnrecognizedExpressionType, "Unrecognized expression type in type checker", "Semantic",
            "The type checker encountered an expression type that it does not have a handler for. " +
            "The expression was assigned the Unknown type, which passes all type checks and may mask errors. " +
            "This is a compiler bug — the type checker is missing a case for this AST node type.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        Add(dict, DiagnosticCodes.Semantic.TuplePatternLengthMismatch, "Tuple pattern length mismatch", "Semantic",
            "A tuple pattern in a match statement has a different number of elements than the scrutinee tuple type. The pattern must have exactly the same number of elements as the tuple being matched.",
            "def main():\n    t = (1, 2, 3)\n    match t:\n        case (a, b):  # 2 elements vs 3\n            pass",
            "Ensure the pattern has the same number of elements as the tuple:\nmatch t:\n    case (a, b, c):\n        print(a, b, c)");

        Add(dict, DiagnosticCodes.Semantic.TupleIndexOutOfRange, "Tuple index out of range", "Semantic",
            "A tuple was indexed with a constant integer that is greater than or equal to the number of elements in the tuple. Tuple indices are zero-based, so for a tuple with N elements, valid indices are 0 through N-1.",
            "def main():\n    t = (1, 2, 3)\n    print(t[3])  # only indices 0, 1, 2 are valid",
            "Use a valid index within range:\ndef main():\n    t = (1, 2, 3)\n    print(t[2])  # last element");

        Add(dict, DiagnosticCodes.Semantic.TupleNegativeIndex, "Negative tuple index", "Semantic",
            "A tuple was indexed with a negative integer. Unlike Python lists, Sharpy tuples do not support negative indexing because tuple access is resolved at compile time to specific .ItemN fields.",
            "def main():\n    t = (1, 2, 3)\n    print(t[-1])  # negative index not supported",
            "Use a positive index instead:\ndef main():\n    t = (1, 2, 3)\n    print(t[2])  # last element");

        // ── Semantic errors: Return and control flow (SPY0260-SPY0279) ──

        Add(dict, DiagnosticCodes.Semantic.MissingReturnValue, "Missing return value", "Semantic",
            "A return statement in a function with a non-void return type is missing the return value. Functions that declare a return type must return a value of that type.",
            "def square(x: int) -> int:\n    return  # missing value",
            "Return a value matching the declared type:\ndef square(x: int) -> int:\n    return x * x");

        Add(dict, DiagnosticCodes.Semantic.MissingReturnType, "Missing return type annotation", "Semantic",
            "A function definition is missing its return type annotation. Sharpy requires explicit return types on all functions. Use -> None for functions that don't return a value.",
            "def greet(name: str):\n    print(f\"Hello, {name}\")",
            "Add a return type:\ndef greet(name: str) -> None:\n    print(f\"Hello, {name}\")");

        Add(dict, DiagnosticCodes.Semantic.ReturnOutsideFunction, "Return outside function", "Semantic",
            "A return statement was used outside of a function body. Return can only be used inside functions and methods.",
            "return 42  # at module level",
            "Move the return inside a function:\ndef main():\n    return 42");

        Add(dict, DiagnosticCodes.Semantic.BreakOutsideLoop, "Break outside loop", "Semantic",
            "A break statement was used outside of a for or while loop. Break can only be used inside loop bodies.",
            "def main():\n    break  # not inside a loop",
            "Move the break inside a loop, or use return to exit a function.");

        Add(dict, DiagnosticCodes.Semantic.ContinueOutsideLoop, "Continue outside loop", "Semantic",
            "A continue statement was used outside of a for or while loop. Continue can only be used inside loop bodies.",
            "def main():\n    continue  # not inside a loop",
            "Move the continue inside a loop.");

        Add(dict, DiagnosticCodes.Semantic.YieldOutsideFunction, "Yield outside function", "Semantic",
            "A yield statement was used outside of a function body. Yield can only be used inside function definitions to create generator functions.",
            "yield 42  # not inside a function",
            "Move the yield inside a function definition:\ndef gen():\n    yield 42");

        Add(dict, DiagnosticCodes.Semantic.NotAllPathsReturn, "Not all paths return a value", "Semantic",
            "A function declared with a non-void return type has execution paths that do not return a value. All possible paths through the function must end with a return statement.",
            "def abs_val(x: int) -> int:\n    if x >= 0:\n        return x\n    # missing return for x < 0",
            "Ensure all paths return a value:\ndef abs_val(x: int) -> int:\n    if x >= 0:\n        return x\n    return -x");

        Add(dict, DiagnosticCodes.Semantic.YieldWithReturn, "Yield with return value in generator", "Semantic",
            "A generator function (one that uses yield) cannot also use 'return' with a value. Use yield to produce values and bare 'return' to stop the generator.",
            "def gen() -> int:\n    yield 1\n    return 42  # cannot return a value",
            "Use bare return to stop the generator:\ndef gen() -> int:\n    yield 1\n    return  # stops iteration");

        Add(dict, DiagnosticCodes.Semantic.YieldInNext, "Yield in __next__ method", "Semantic",
            "The __next__ method cannot contain yield statements. Use __iter__ for generator-based iteration, or implement __next__ as an explicit iterator (with return).",
            "class MyIter:\n    def __next__(self) -> int:\n        yield 1  # not allowed",
            "Use __iter__ for generators:\nclass MyIter:\n    def __iter__(self) -> int:\n        yield 1\n        yield 2");

        Add(dict, DiagnosticCodes.Semantic.GeneratorIterConflict, "Generator __iter__ conflicts with __next__", "Semantic",
            "A class cannot have both a generator __iter__ (using yield) and a __next__ method. Choose either the generator pattern (yield in __iter__) or the explicit iterator pattern (__next__ with return).",
            "class Bad:\n    def __iter__(self) -> int:\n        yield 1\n    def __next__(self) -> int:\n        return 0",
            "Choose one pattern:\nclass GenIter:\n    def __iter__(self) -> int:\n        yield 1\n        yield 2");

        Add(dict, DiagnosticCodes.Semantic.YieldInTryExcept,
            "Yield in try/except block", "Semantic",
            "A 'yield' statement cannot appear inside a 'try' block that has 'except' handlers. " +
            "This is a .NET restriction: the CLR does not support yield return inside try-catch. " +
            "Move the yield outside the try/except block or use try/finally instead.",
            "def gen() -> int:\n    try:\n        yield 1  # not allowed\n    except Exception as e:\n        pass",
            "Move yield outside try/except:\ndef gen() -> int:\n    yield 1\n    try:\n        risky_operation()\n    except Exception as e:\n        handle_error(e)");

        Add(dict, DiagnosticCodes.Semantic.YieldInCatchHandler,
            "Yield in except handler", "Semantic",
            "A 'yield' statement cannot appear inside an 'except' handler. " +
            "This is a .NET restriction: the CLR does not support yield return inside catch blocks (CS1631). " +
            "Move the yield outside the except handler.",
            "def gen() -> int:\n    try:\n        pass\n    except Exception as e:\n        yield 1  # not allowed",
            "Move yield outside the except handler:\ndef gen() -> int:\n    try:\n        risky_operation()\n    except Exception as e:\n        handle_error(e)\n    yield 1");

        Add(dict, DiagnosticCodes.Semantic.YieldInFinallyBlock,
            "Yield in finally block", "Semantic",
            "A 'yield' statement cannot appear inside a 'finally' block. " +
            "This is a .NET restriction: the CLR does not support yield return inside finally blocks (CS1625). " +
            "Move the yield outside the finally block.",
            "def gen() -> int:\n    try:\n        pass\n    finally:\n        yield 1  # not allowed",
            "Move yield outside the finally block:\ndef gen() -> int:\n    yield 1\n    try:\n        risky_operation()\n    finally:\n        cleanup()");

        Add(dict, DiagnosticCodes.Semantic.AwaitOutsideAsync,
            "Await outside async function", "Semantic",
            "The 'await' keyword can only be used inside functions declared with 'async def'. " +
            "Regular functions and lambdas cannot use 'await'.",
            "def fetch() -> str:\n    return await get_data()  # not async",
            "Declare the function as async:\nasync def fetch() -> str:\n    return await get_data()");

        Add(dict, DiagnosticCodes.Semantic.InvalidAwaitOperand,
            "Cannot await non-Task type", "Semantic",
            "The 'await' keyword can only be used with expressions that return a Task type. " +
            "The operand must be a call to an async function or an expression that produces a Task.",
            "async def run():\n    x: int = await 42  # int is not awaitable",
            "Await an async function call:\nasync def run():\n    x: int = await get_value()");

        // ── Semantic errors: Class and inheritance (SPY0280-SPY0299) ────

        Add(dict, DiagnosticCodes.Semantic.AbstractInstantiation, "Cannot instantiate abstract class", "Semantic",
            "An attempt was made to create an instance of an abstract class. Abstract classes can only be subclassed, not instantiated directly.",
            "abstract class Shape:\n    def area(self) -> float:\n        ...\n\ns = Shape()  # cannot instantiate",
            "Create a concrete subclass:\nclass Circle(Shape):\n    radius: float\n    def area(self) -> float:\n        return 3.14159 * self.radius * self.radius");

        Add(dict, DiagnosticCodes.Semantic.InvalidInheritance, "Invalid inheritance", "Semantic",
            "A class attempted to inherit from a type that cannot be used as a base class. For example, inheriting from a struct, enum, or sealed class.",
            "struct Point:\n    x: int\n    y: int\n\nclass Point3D(Point):  # cannot inherit from struct\n    z: int",
            "Use class inheritance only with other classes, or use composition instead.");

        Add(dict, DiagnosticCodes.Semantic.IncompatibleOverride, "Incompatible method override", "Semantic",
            "A method in a subclass overrides a base class method but with an incompatible signature. The parameter types and return type must be compatible with the base method.",
            "class Base:\n    def process(self, x: int) -> str:\n        return str(x)\n\nclass Sub(Base):\n    def process(self, x: str) -> int:  # incompatible\n        return 0",
            "Match the base method's signature:\nclass Sub(Base):\n    def process(self, x: int) -> str:\n        return f\"value: {x}\"");

        Add(dict, DiagnosticCodes.Semantic.AccessViolation, "Access violation", "Semantic",
            "Code attempted to access a member that is not accessible from the current context. Members prefixed with _ are protected (accessible in subclasses), and members prefixed with __ are private (accessible only within the defining class).",
            "class Secret:\n    __key: str = \"hidden\"\n\ns = Secret()\nprint(s.__key)  # private access",
            "Use a public method to expose the value, or change the access level.");

        Add(dict, DiagnosticCodes.Semantic.SuperOutsideClass, "super() outside class", "Semantic",
            "The super() call was used outside of a class method. super() is only valid inside instance methods of a class that has a base class.",
            "def foo():\n    super().__init__()  # not in a class",
            "Use super() only inside a class method:\nclass Sub(Base):\n    def __init__(self):\n        super().__init__()");

        Add(dict, DiagnosticCodes.Semantic.SuperNoParent, "super() in class without parent", "Semantic",
            "super() was called in a class that does not have a base class. super() is only meaningful in classes that inherit from another class.",
            "class Root:\n    def __init__(self):\n        super().__init__()  # Root has no parent",
            "Remove the super() call or add a base class:\nclass Root(Base):\n    def __init__(self):\n        super().__init__()");

        Add(dict, DiagnosticCodes.Semantic.DuplicateClass, "Duplicate class definition", "Semantic",
            "A class name was defined more than once in the same scope. Each class name must be unique.",
            "class Foo:\n    x: int\n\nclass Foo:  # duplicate\n    y: str",
            "Rename one of the classes or merge them into a single definition.");

        Add(dict, DiagnosticCodes.Semantic.InvalidSuperUsage, "Invalid super() usage", "Semantic",
            "super() was used in an invalid way. super() must be called as a function and used to access base class methods.",
            "class Sub(Base):\n    def foo(self):\n        x = super  # not called as a function",
            "Call super() as a function:\nclass Sub(Base):\n    def foo(self):\n        super().foo()");

        Add(dict, DiagnosticCodes.Semantic.CircularInheritance, "Circular inheritance detected", "Semantic",
            "A class or interface inherits from itself through its inheritance chain, creating a cycle. " +
            "For example, class A extends B and class B extends A, or interface IA extends IB and IB extends IA.",
            "class A(B):\n    pass\n\nclass B(A):\n    pass",
            "Break the cycle by removing one of the inheritance relationships or restructuring the type hierarchy.");

        Add(dict, DiagnosticCodes.Semantic.InstanceFieldViaTypeName,
            "Instance field accessed via type name",
            "Semantic",
            "An instance field is being accessed through the type name (e.g., ClassName.field) rather than " +
            "through an instance. Only static and const fields can be accessed via the type name.",
            "class Config:\n    timeout: int = 30\n\ndef main():\n    t = Config.timeout  # error: timeout is an instance field",
            "Mark the field as @static if it should be shared across instances:\nclass Config:\n    @static\n    timeout: int = 30\n\n" +
            "Or use an instance:\ndef main():\n    c = Config()\n    t = c.timeout");

        Add(dict, DiagnosticCodes.Semantic.MaybeOnUnconstrainedTypeParameter,
            "'maybe' on unconstrained generic type parameter",
            "Semantic",
            "The 'maybe' operator cannot be used with an unconstrained generic type parameter because " +
            "Optional.From<T> has separate overloads for reference types (where T : class) and value types " +
            "(where T : struct). When T is unconstrained, the compiler cannot determine which overload to use.",
            "def wrap[T](value: T | None) -> T?:\n    return maybe value  # error: T is unconstrained",
            "Constrain the type parameter or use a different pattern to handle the optional value.");

        // ── Semantic errors: Import (SPY0300-SPY0319) ───────────────────

        Add(dict, DiagnosticCodes.Semantic.ModuleNotFound, "Module not found", "Semantic",
            "An import statement references a module that could not be found. The compiler searches the current directory, configured module paths, and standard library paths.",
            "from utils import helper  # utils.spy not found",
            "Ensure the module file exists and is in a searchable path. Use --module-path to add search directories.");

        Add(dict, DiagnosticCodes.Semantic.ImportError, "Import error", "Semantic",
            "A symbol requested in a from-import statement was not found in the target module. The module exists but does not export the specified name.",
            "from math_utils import nonexistent_func",
            "Check the module's exported symbols. Make sure the name is spelled correctly and is public (not prefixed with _ or __).");

        Add(dict, DiagnosticCodes.Semantic.CircularImport, "Circular import", "Semantic",
            "Two or more modules import each other, creating a cycle. Module A imports from B, and B (directly or indirectly) imports from A.",
            "# a.spy\nfrom b import foo\n\n# b.spy\nfrom a import bar",
            "Break the cycle by:\n  1. Moving shared code to a third module\n  2. Restructuring the dependency graph\n  3. Using lazy imports where possible");

        Add(dict, DiagnosticCodes.Semantic.ModuleLoadError, "Module load error", "Semantic",
            "A module was found but could not be loaded, typically due to syntax errors in the module's source file.",
            null,
            "Fix the errors in the imported module first, then retry the compilation.");

        Add(dict, DiagnosticCodes.Semantic.AssemblyNotFound, "Assembly not found", "Semantic",
            "A .NET assembly referenced in an import could not be found. The compiler searches configured assembly paths.",
            "import SomeLibrary  # .NET assembly not found",
            "Ensure the assembly DLL is available and the assembly search path is configured correctly.");

        Add(dict, DiagnosticCodes.Semantic.AssemblyLoadError, "Assembly load error", "Semantic",
            "A .NET assembly was found but could not be loaded, typically due to version incompatibility or missing dependencies.",
            null,
            "Check the assembly's target framework compatibility and ensure all dependencies are available.");

        // ── Semantic errors: Protocol and operator (SPY0320-SPY0339) ────

        Add(dict, DiagnosticCodes.Semantic.ProtocolMissingMethod, "Protocol method not implemented", "Semantic",
            "A class claims to implement an interface (protocol) but is missing one or more required methods. All interface methods must be implemented.",
            "interface Printable:\n    def display(self) -> str: ...\n\nclass Foo(Printable):  # missing display()\n    x: int",
            "Implement all required methods:\nclass Foo(Printable):\n    x: int\n    def display(self) -> str:\n        return str(self.x)");

        Add(dict, DiagnosticCodes.Semantic.InvalidOperatorSignature, "Invalid operator signature", "Semantic",
            "An operator overload method has an incorrect signature. Operator methods must follow specific parameter and return type conventions.",
            "class Vec:\n    def __add__(self) -> Vec:  # missing 'other' parameter\n        return self",
            "Use the correct operator signature:\nclass Vec:\n    def __add__(self, other: Vec) -> Vec:\n        return Vec()");

        Add(dict, DiagnosticCodes.Semantic.InvalidDecoratorUsage, "Invalid decorator usage", "Semantic",
            "A decorator was used in an invalid context or with invalid arguments. Check that the decorator is appropriate for the target (function, method, or class).",
            "@override\ndef top_level_func():  # @override only valid on class methods\n    pass",
            "Use the decorator on an appropriate target, or remove it.");

        Add(dict, DiagnosticCodes.Semantic.ConflictingSynthesizedInterface,
            "Conflicting synthesized interface",
            "Semantic",
            "A class would synthesize a generic interface (e.g., IEquatable<T>, IEnumerator<T>) " +
            "from a dunder method, but an ancestor class already implements the same generic interface " +
            "with different type arguments. C# does not allow a type to implement the same generic interface " +
            "with conflicting type arguments.",
            "class Base:\n    def __eq__(self, other: str) -> bool:\n        return False\n\n" +
            "class Derived(Base):\n    def __eq__(self, other: int) -> bool:  # conflicts with Base's IEquatable<str>\n        return False",
            "Remove the conflicting dunder method from the derived class, or restructure the hierarchy " +
            "so that both classes use the same type argument for the interface.");

        Add(dict, DiagnosticCodes.Semantic.WithNotDisposable,
            "Type not usable in with statement",
            "Semantic",
            "The expression used in a 'with' statement must either implement IDisposable (for .NET types) " +
            "or define __enter__/__exit__ methods (for Sharpy context manager protocol). " +
            "For 'async with', the type must implement IAsyncDisposable or define __aenter__/__aexit__ methods.",
            "class Foo:\n    pass\n\ndef main():\n    with Foo() as f:\n        print(f)",
            "Either implement IDisposable on the class, or add __enter__ and __exit__ methods for the context manager protocol.");

        Add(dict, DiagnosticCodes.Semantic.InterfaceMethodNotImplemented,
            "Interface method not implemented",
            "Semantic",
            "A class or struct declares that it implements an interface but does not provide an implementation " +
            "for one or more of the interface's abstract methods. All abstract methods from all implemented " +
            "interfaces (including base interfaces) must be implemented unless the class is abstract.",
            "interface Drawable:\n    def draw(self) -> str:\n        ...\n\nclass Circle(Drawable):\n    pass",
            "Add the missing method implementation:\nclass Circle(Drawable):\n    def draw(self) -> str:\n        return \"circle\"");

        // ── Semantic errors: Module level (SPY0340-SPY0349) ─────────────

        Add(dict, DiagnosticCodes.Semantic.ModuleLevelExecutableStatement, "Executable statement at module level", "Semantic",
            "An executable statement (like a function call or expression) was found at module level. Only declarations (functions, classes, variables, imports) are allowed at module level. Executable code must be inside the main() function.",
            "print(\"hello\")  # at module level\n\ndef main():\n    pass",
            "Move executable code into the main() function:\ndef main():\n    print(\"hello\")");

        Add(dict, DiagnosticCodes.Semantic.ModuleLevelNoTypeAnnotation, "Module-level variable without type annotation", "Semantic",
            "A variable at module level is missing its type annotation. Module-level variables must always have explicit type annotations.",
            "x = 42  # missing type annotation at module level",
            "Add a type annotation:\n  x: int = 42");

        // ── Semantic errors: Additional (SPY0350-SPY0399) ───────────────

        Add(dict, DiagnosticCodes.Semantic.SelfInitOutsideConstructor, "self.__init__() outside constructor", "Semantic",
            "A call to self.__init__() was found outside of an __init__ method. Constructor chaining via self.__init__() is only valid inside a constructor.",
            "def method(self):\n    self.__init__(42)  # not allowed here",
            "Move self.__init__() calls into an __init__ method for constructor chaining.");

        Add(dict, DiagnosticCodes.Semantic.ConflictingConstructorInitializers, "Conflicting constructor initializers", "Semantic",
            "A constructor has both super().__init__() and self.__init__() calls. A constructor can chain to either a base constructor or another constructor of the same class, but not both.",
            "def __init__(self, x: int):\n    super().__init__()\n    self.__init__(x, 0)",
            "Remove one of the chaining calls — use either super().__init__() or self.__init__(), not both.");

        Add(dict, DiagnosticCodes.Semantic.TypeAliasArityMismatch, "Type alias arity mismatch", "Semantic",
            "A generic type alias was used with the wrong number of type arguments.",
            "type Pair[T] = tuple[T, T]\nx: Pair[int, str] = (1, \"a\")  # Pair takes 1 type argument, got 2",
            "Provide the correct number of type arguments matching the alias definition.");

        Add(dict, DiagnosticCodes.Semantic.AmbiguousOverload, "Ambiguous method overload", "Semantic",
            "A method call matches multiple overloads equally well and the compiler cannot determine which to use.",
            "class Foo:\n    def bar(self, x: int): ...\n    def bar(self, x: float): ...\nfoo.bar(42)",
            "Add an explicit type annotation or cast to disambiguate the call.");

        Add(dict, DiagnosticCodes.Semantic.NoMatchingOverload, "No matching method overload", "Semantic",
            "A method call does not match any of the available overloads for the method.",
            "class Foo:\n    def bar(self, x: int): ...\nfoo.bar(\"hello\")",
            "Check the argument types and ensure they match one of the declared overloads.");

        Add(dict, DiagnosticCodes.Semantic.DuplicateMethodSignature, "Duplicate method signature", "Semantic",
            "Two method overloads have identical parameter signatures. Overloads must differ in parameter count or types.",
            "class Foo:\n    def bar(self, x: int): ...\n    def bar(self, y: int): ...",
            "Change the parameter types or count to differentiate overloads.");

        Add(dict, DiagnosticCodes.Semantic.MultipleStarExpressions, "Multiple star expressions in unpacking", "Semantic",
            "More than one starred expression (*rest) was found in an unpacking target. Only one starred expression is allowed per unpacking.",
            "first, *middle, *end = items",
            "Use only one starred expression:\nfirst, *rest = items");

        Add(dict, DiagnosticCodes.Semantic.SpreadIntoNonVariadic, "Spread into non-variadic parameter", "Semantic",
            "A spread argument (*args) was used in a call to a function that does not have a variadic parameter.",
            "def foo(a: int, b: int): ...\nitems: list[int] = [1, 2]\nfoo(*items)",
            "Use a function with variadic parameters (*args) or pass arguments individually.");

        Add(dict, DiagnosticCodes.Semantic.UnsupportedFeature, "Unsupported feature", "Semantic",
            "A language feature was used that is recognized by the parser but not yet supported in semantic analysis or code generation.",
            "match x:\n    case [1, 2, 3]:  # list patterns not yet supported\n        print(x)",
            "Use a supported pattern type such as literal patterns, binding patterns, wildcard patterns, tuple patterns, or member access patterns.");
        Add(dict, DiagnosticCodes.Semantic.BindingInOrPattern, "Binding pattern in or-pattern", "Semantic",
            "A binding pattern was used inside an or-pattern (|). C# does not allow variable bindings in or-patterns because the variable would only be assigned in one branch.",
            "match x:\n    case y | 2:  # binding 'y' not allowed in or-pattern\n        print(y)",
            "Use literal patterns, wildcard patterns, or member access patterns inside or-patterns:\nmatch x:\n    case 1 | 2:\n        print(\"one or two\")");

        Add(dict, DiagnosticCodes.Semantic.RelationalPatternTypeMismatch, "Relational pattern type mismatch", "Semantic",
            "A relational pattern (>, <, >=, <=) was used with a non-numeric scrutinee type. Relational patterns require a numeric type.",
            "match name:\n    case > 0:  # name is str, not numeric\n        print(name)",
            "Use relational patterns only with numeric types (int, float, etc.).");

        Add(dict, DiagnosticCodes.Semantic.TypePatternIncompatible, "Incompatible type pattern", "Semantic",
            "A type pattern was used with a type that is incompatible with the scrutinee type.",
            "x: int = 42\nmatch x:\n    case str() as s:  # int cannot be str\n        print(s)",
            "Use a type pattern that is compatible with the scrutinee type.");

        Add(dict, DiagnosticCodes.Semantic.PropertyPatternUnknownField, "Unknown field in property pattern", "Semantic",
            "A property pattern referenced a field that does not exist on the matched type.",
            "class Point:\n    x: int\n    y: int\nmatch p:\n    case Point(z=0):  # Point has no field 'z'\n        ...",
            "Use field names that exist on the type being matched.");

        Add(dict, DiagnosticCodes.Semantic.PositionalPatternCountMismatch, "Positional pattern count mismatch", "Semantic",
            "A positional pattern has a different number of elements than the fields of the matched type.",
            "class Point:\n    x: int\n    y: int\nmatch p:\n    case Point(1, 2, 3):  # Point has 2 fields, got 3\n        ...",
            "Provide the correct number of positional elements matching the type's fields.");

        Add(dict, DiagnosticCodes.Semantic.UnsupportedPatternInMemberAccessOr, "Unsupported pattern mixed with member access in or-pattern", "Semantic",
            "An or-pattern that contains a member access pattern also contains a pattern type that cannot be combined with it. Only literal, member access, and wildcard patterns can be mixed with member access patterns in or-patterns.",
            "match x:\n    case int() | Color.RED:  # type pattern cannot mix with member access\n        ...",
            "Use only literal values, member access, or wildcard patterns alongside member access patterns:\nmatch x:\n    case 1 | Color.RED:\n        ...");

        Add(dict, DiagnosticCodes.Semantic.DuplicateUnionCase, "Duplicate union case", "Semantic",
            "A union type has two or more cases with the same name. Each union case must have a unique name.",
            "union Shape:\n    case Circle(radius: float)\n    case Circle(diameter: float)",
            "Give each union case a unique name:\nunion Shape:\n    case Circle(radius: float)\n    case Square(side: float)");

        Add(dict, DiagnosticCodes.Semantic.UnionCaseNameConflict, "Union case name conflicts with union type", "Semantic",
            "A union case has the same name as its enclosing union type. This would produce a C# nested class with the same name as its parent, which is invalid.",
            "union Shape:\n    case Shape(radius: float)  # case name collides with union name",
            "Give the union case a different name:\nunion Shape:\n    case Circle(radius: float)");

        Add(dict, DiagnosticCodes.Semantic.UnionCaseNotFound, "Unknown union case in pattern", "Semantic",
            "A pattern references a case name that does not exist on the union type being matched.",
            "union Result:\n    case Ok(value: int)\n    case Err(msg: str)\nmatch r:\n    case Unknown(v):  # no case 'Unknown' on Result\n        ...",
            "Use a case name that exists on the union type:\nmatch r:\n    case Ok(v):\n        print(v)");

        Add(dict, DiagnosticCodes.Semantic.UnionCaseFieldMismatch, "Wrong number of fields in union case pattern", "Semantic",
            "A union case pattern has a different number of field bindings than the union case declares.",
            "union Result:\n    case Ok(value: int)\nmatch r:\n    case Ok(a, b):  # Ok has 1 field, got 2\n        ...",
            "Provide the correct number of field bindings matching the union case definition:\nmatch r:\n    case Ok(v):\n        print(v)");

        Add(dict, DiagnosticCodes.Semantic.PositionalPatternNoDeconstruct, "Positional pattern without Deconstruct", "Semantic",
            "A positional pattern was used on a type that does not support it. The type has no Deconstruct method and the number of pattern elements does not match the type's field count.",
            "class Point:\n    x: int\n    y: int\nmatch p:\n    case Point(a, b, c):  # Point has 2 fields, not 3\n        ...",
            "Use the correct number of positional elements matching the type's fields, or use a property pattern:\nmatch p:\n    case Point(a, b):\n        print(a, b)");

        Add(dict, DiagnosticCodes.Semantic.PositionalOnlyPassedByKeyword, "Positional-only parameter passed by keyword", "Semantic",
            "A parameter that is declared as positional-only (before '/') was passed as a keyword argument. Positional-only parameters cannot be referred to by name at call sites.",
            "def foo(x: int, /) -> int:\n    return x\nfoo(x=1)  # error: 'x' is positional-only",
            "Pass the argument positionally:\nfoo(1)");

        Add(dict, DiagnosticCodes.Semantic.KeywordOnlyPassedPositionally, "Keyword-only parameter passed positionally", "Semantic",
            "A parameter that is declared as keyword-only (after '*' or '*args') was passed positionally. Keyword-only parameters must be passed by name.",
            "def foo(*, key: int) -> int:\n    return key\nfoo(1)  # error: 'key' is keyword-only",
            "Pass the argument by name:\nfoo(key=1)");


        // ── Semantic errors: Events (SPY0373-SPY0379) ──────────────────────

        Add(dict, DiagnosticCodes.Semantic.EventTypeNotDelegate, "Event type is not a delegate", "Semantic",
            "An event declaration specifies a type that is not a delegate. Events must be declared with a delegate type that specifies the handler signature.",
            "event on_click: int  # error: int is not a delegate type",
            "Use a delegate type for the event:\ndelegate EventHandler(self) -> None\nevent on_click: EventHandler");

        Add(dict, DiagnosticCodes.Semantic.EventAccessorParamMismatch, "Event accessor parameter mismatch", "Semantic",
            "A function-style event accessor has parameters that don't match the event's delegate type. The handler parameter must be assignable to the delegate type.",
            "delegate ClickHandler(self) -> None\nevent add on_click(self, handler: int):  # error: int != ClickHandler\n    pass",
            "Use the correct delegate type as the handler parameter:\nevent add on_click(self, handler: ClickHandler):\n    pass");

        Add(dict, DiagnosticCodes.Semantic.DirectEventAssignment, "Direct event assignment not allowed", "Semantic",
            "An event was assigned to directly using '=' instead of using '+=' to add a handler or '-=' to remove a handler. Events cannot be overwritten; handlers must be added or removed.",
            "btn.on_click = my_handler  # error: direct assignment",
            "Use += to add a handler or -= to remove one:\nbtn.on_click += my_handler");

        Add(dict, DiagnosticCodes.Semantic.EventHandlerTypeMismatch, "Event handler type mismatch", "Semantic",
            "A handler being added to or removed from an event is not compatible with the event's delegate type. The handler type must be assignable to the delegate type.",
            "delegate ClickHandler(self) -> None\nevent on_click: ClickHandler\ndef invalid_handler(x: int) -> None: pass\nbtn.on_click += invalid_handler  # error: signature mismatch",
            "Use a handler with the correct signature:\ndef valid_handler(self) -> None: pass\nbtn.on_click += valid_handler");

        Add(dict, DiagnosticCodes.Semantic.RaiseEventOutsideClass, "Event raise outside class", "Semantic",
            "An event is being raised (invoked) from outside the class that declares it. Events are protected and can only be raised from within their declaring class.",
            "btn.on_click()  # error: cannot raise from outside Button class",
            "Only call the event from within its declaring class, or provide a public method to raise it:\n# Inside Button class:\ndef do_click(self):\n    self.on_click()  # OK: inside the class");

        Add(dict, DiagnosticCodes.Semantic.EventUnsupportedOperator, "Unsupported event operator", "Semantic",
            "An augmented operator other than '+=' or '-=' was used on an event. Events only support adding handlers with '+=' and removing handlers with '-='.",
            "btn.on_click *= handler  # error: *= not supported",
            "Use '+=' to add a handler or '-=' to remove one:\nbtn.on_click += handler\nbtn.on_click -= handler");

        // ── Semantic errors: Dataclass (SPY0380-SPY0383) ────────────────────

        Add(dict, DiagnosticCodes.Semantic.DataclassOnNonClass, "@dataclass on non-class type", "Semantic",
            "The @dataclass decorator can only be applied to class definitions. Structs and interfaces do not support @dataclass.",
            "@dataclass\nstruct Point:\n    x: int\n    y: int",
            "Use a class instead:\n@dataclass\nclass Point:\n    x: int\n    y: int");

        Add(dict, DiagnosticCodes.Semantic.DataclassFieldOrdering, "Dataclass field ordering error", "Semantic",
            "A field without a default value follows a field with a default value in a @dataclass. Fields without defaults must come before fields with defaults.",
            "@dataclass\nclass Bad:\n    x: int = 10\n    y: int  # error: no default after default",
            "Reorder fields so non-default fields come first:\n@dataclass\nclass Good:\n    y: int\n    x: int = 10");

        Add(dict, DiagnosticCodes.Semantic.DataclassFieldNoType, "Dataclass field missing type annotation", "Semantic",
            "A field in a @dataclass does not have a type annotation. All dataclass fields must have explicit type annotations.",
            "@dataclass\nclass Bad:\n    x = 10  # error: no type annotation",
            "Add a type annotation:\n@dataclass\nclass Good:\n    x: int = 10");

        Add(dict, DiagnosticCodes.Semantic.DataclassInvalidOption, "Invalid @dataclass option", "Semantic",
            "An unrecognized or invalid option was passed to @dataclass. Valid options are frozen, eq, and repr, all of which must be boolean values.",
            "@dataclass(frozen=\"yes\")  # error: must be True/False\nclass Bad:\n    x: int",
            "Use boolean values for @dataclass options:\n@dataclass(frozen=True)\nclass Good:\n    x: int");

        // ── Semantic errors: Self type (SPY0384-SPY0385) ────────────────────

        Add(dict, DiagnosticCodes.Semantic.SelfOutsideClass, "Self type outside class", "Semantic",
            "'Self' can only be used inside a class, struct, or interface definition. It refers to the enclosing type and has no meaning at module level or in standalone functions.",
            "def make() -> Self:\n    ...",
            "Use Self only inside a class:\nclass Builder:\n    def make(self) -> Self:\n        return self");

        Add(dict, DiagnosticCodes.Semantic.SelfInStaticMethod, "Self type in static method", "Semantic",
            "'Self' cannot be used in static methods because there is no instance type to refer to.",
            "class Foo:\n    @static\n    def create() -> Self:\n        ...",
            "Use the concrete class name instead:\nclass Foo:\n    @static\n    def create() -> Foo:\n        return Foo()");

        // ── Semantic errors: Builtin call errors (SPY0386+) ─────────────

        Add(dict, DiagnosticCodes.Semantic.UnsupportedTypeNone, "type(None) is not supported", "Semantic",
            "Python's type(None) returns <class 'NoneType'>, but NoneType has no Sharpy equivalent. None in Sharpy is a null literal, not an instance of a type.",
            "x = type(None)",
            "Use a type annotation or isinstance() check instead of type(None).");

        // ── Semantic errors: Parameter modifier errors (SPY0387-SPY0391) ─

        Add(dict, DiagnosticCodes.Semantic.ModifierWithDefault, "ref/out/in parameter cannot have a default value", "Semantic",
            "Parameters with ref, out, or in modifiers require the caller to explicitly pass the argument at the call site. A default value would bypass this requirement, which is semantically invalid.",
            "def foo(x: ref int = 5): ...",
            "Remove the default value: def foo(x: ref int): ...");

        Add(dict, DiagnosticCodes.Semantic.ModifierWithVariadic, "variadic parameter cannot have a modifier", "Semantic",
            "Variadic parameters (*args) collect multiple arguments into a list. ref/out/in modifiers require pass-by-reference semantics which are incompatible with variadic collection.",
            "def foo(*args: ref int): ...",
            "Remove the modifier or use individual parameters: def foo(a: ref int, b: ref int): ...");

        Add(dict, DiagnosticCodes.Semantic.ModifierRequiresVariable, "ref/out argument must be a variable", "Semantic",
            "ref and out arguments must refer to a storage location (variable, field, or indexer) that can be written to. Literals and expression results have no storage location.",
            "swap(ref 5, ref 10)",
            "Pass a variable instead: x = 5; swap(ref x, ref y)");

        Add(dict, DiagnosticCodes.Semantic.InParameterReassignment, "cannot reassign 'in' parameter", "Semantic",
            "Parameters declared with the 'in' modifier are passed by readonly reference. They cannot be reassigned to prevent unintended mutation of the caller's value.",
            "def foo(x: in int):\n    x = 0  # Error",
            "Remove the 'in' modifier if reassignment is needed, or use a local copy: local_x = x");

        // ── Semantic errors: except* (SPY0391-SPY0394) ─────────────────
        Add(dict, DiagnosticCodes.Semantic.ExceptStarCatchesExceptionGroup, "'except*' cannot catch ExceptionGroup", "Semantic",
            "An 'except*' handler cannot catch ExceptionGroup directly. The except* syntax is designed to match individual exception types within an ExceptionGroup, not the group itself. Use a regular 'except' handler to catch ExceptionGroup.",
            "try:\n    ...\nexcept* ExceptionGroup as eg:\n    ...",
            "Use 'except' instead of 'except*':\n  except ExceptionGroup as eg:\n      ...");

        Add(dict, DiagnosticCodes.Semantic.BreakInExceptStar, "'break' not allowed in except* handler", "Semantic",
            "'break' statements are not allowed inside 'except*' handlers (PEP 654). This restriction exists because except* handlers may execute for only a subset of exceptions in an ExceptionGroup, and control flow statements would interfere with the exception splitting logic.",
            "for i in range(10):\n    try:\n        ...\n    except* ValueError as eg:\n        break  # Error",
            "Handle the break condition outside the except* handler, for example by setting a flag variable.");

        Add(dict, DiagnosticCodes.Semantic.ContinueInExceptStar, "'continue' not allowed in except* handler", "Semantic",
            "'continue' statements are not allowed inside 'except*' handlers (PEP 654). This restriction exists because except* handlers may execute for only a subset of exceptions in an ExceptionGroup, and control flow statements would interfere with the exception splitting logic.",
            "for i in range(10):\n    try:\n        ...\n    except* ValueError as eg:\n        continue  # Error",
            "Handle the continue condition outside the except* handler, for example by setting a flag variable.");

        Add(dict, DiagnosticCodes.Semantic.ReturnInExceptStar, "'return' not allowed in except* handler", "Semantic",
            "'return' statements are not allowed inside 'except*' handlers (PEP 654). This restriction exists because except* handlers may execute for only a subset of exceptions in an ExceptionGroup, and control flow statements would interfere with the exception splitting logic.",
            "def foo():\n    try:\n        ...\n    except* ValueError as eg:\n        return  # Error",
            "Handle the return condition outside the except* handler, for example by setting a flag variable.");

        // ── Semantic errors: Generic type parameter defaults (SPY0395-SPY0396) ─

        Add(dict, DiagnosticCodes.Semantic.TypeParameterDefaultOrdering,
            "Type parameter without default follows one with default",
            "Semantic",
            "In a type parameter list, once one type parameter has a default, all subsequent type parameters must also have defaults (PEP 696).",
            "class Foo[T = int, U]:  # Error: U has no default but T does",
            "Either add a default to all trailing type parameters or reorder them:\nclass Foo[U, T = int]: ...");

        Add(dict, DiagnosticCodes.Semantic.TypeParameterDefaultViolatesConstraint,
            "Type parameter default violates constraint",
            "Semantic",
            "A type parameter's default type does not satisfy its declared constraints. The default type must conform to all constraints (class, struct, or interface/base type).",
            "class Foo[T: class = int]:  # Error: int is a value type, not a class",
            "Use a default type that satisfies the constraint:\nclass Foo[T: class = str]: ...");

        // ── Semantic errors: Exception filters (SPY0397-SPY0398) ──

        Add(dict, DiagnosticCodes.Semantic.ExceptionFilterNotBoolean,
            "Exception filter must be a boolean expression",
            "Semantic",
            "The 'when' clause in an except handler must evaluate to a boolean value. The filter determines whether the handler matches the exception.",
            "except ValueError as e when \"not a bool\":",
            "Use a boolean expression:\nexcept ValueError as e when e.message == \"expected\":");

        Add(dict, DiagnosticCodes.Semantic.ExceptStarWhenNotSupported,
            "'except*' handlers do not support 'when' filters",
            "Semantic",
            "Exception filters (when clauses) cannot be used with except* handlers. This is a language restriction.",
            "except* ValueError as e when True:",
            "Use a regular except handler with a when filter, or filter inside the except* body.");

        // ── Validation errors (SPY0400-SPY0499) ────────────────────────

        Add(dict, DiagnosticCodes.Validation.MutableDefault, "Mutable default parameter", "Validation",
            "A function parameter has a mutable default value (list, dict, or set literal). In Python, mutable defaults are shared across calls, leading to subtle bugs. Sharpy prevents this pattern.",
            "def append_to(item: int, lst: list[int] = []) -> list[int]:\n    lst.append(item)\n    return lst",
            "Use None as the default and create the mutable object inside the function:\ndef append_to(item: int, lst: Optional[list[int]] = None) -> list[int]:\n    if lst is None:\n        lst = []\n    lst.append(item)\n    return lst");

        Add(dict, DiagnosticCodes.Validation.NonConstDefault, "Non-constant default parameter value", "Validation",
            "A function parameter has a default value that is not a compile-time constant. Default values must be literals or constants.",
            "x: int = 10\ndef foo(n: int = x):  # x is not a constant\n    pass",
            "Use a literal or constant default:\ndef foo(n: int = 10):\n    pass");

        Add(dict, DiagnosticCodes.Validation.UnsupportedOperator, "Unsupported operator for type", "Validation",
            "An operator was used with types that don't support it. The validation pipeline checks operator compatibility beyond basic type checking.",
            null,
            "Use an operator that is supported for the given types, or implement the corresponding dunder method on your class.");

        Add(dict, DiagnosticCodes.Validation.MissingMainFunction, "Missing main() function", "Validation",
            "The program does not define a main() function. Every Sharpy program must have a main() function as its entry point.",
            "def helper() -> int:\n    return 42\n# no main function",
            "Add a main function:\ndef main():\n    result: int = helper()\n    print(result)");

        Add(dict, DiagnosticCodes.Validation.InvalidNullCoalesce, "Invalid null-coalescing operator usage", "Validation",
            "The null-coalescing operator (??) was used with a left operand that is not nullable, or with incompatible types.",
            "x: int = 42\ny: int = x ?? 0  # x is never null",
            "Only use ?? with Optional types:\n  x: Optional[int] = get_value()\n  y: int = x ?? 0");

        // ── Validation errors: Property validation (SPY0405-SPY0415) ───

        Add(dict, DiagnosticCodes.Validation.PropertyFieldNameConflict,
            "Property conflicts with field name",
            "Validation",
            "A property has the same name as a field in the same class or struct. Properties and fields occupy the same namespace and cannot share names.",
            "class Foo:\n    name: str\n    property get name(self) -> str:\n        return self._name",
            "Rename the field or the property to avoid the conflict.");

        Add(dict, DiagnosticCodes.Validation.PropertyMethodNameConflict,
            "Property conflicts with method name",
            "Validation",
            "A property has the same name as a method in the same class or struct. Properties and methods cannot share names because they both generate members on the C# type.",
            "class Foo:\n    property get name(self) -> str:\n        return self._name\n    def name(self) -> str:\n        return self._name",
            "Rename the method or the property to avoid the conflict.");

        Add(dict, DiagnosticCodes.Validation.MixedAutoAndFunctionStyleProperty,
            "Mixed auto-property and function-style property",
            "Validation",
            "The same property name has both auto-property and function-style definitions. A property must be either entirely auto-property or entirely function-style.",
            "class Foo:\n    property name: str\n    property get name(self) -> str:\n        return self._name",
            "Choose one style: either auto-property or function-style with getter/setter bodies.");

        Add(dict, DiagnosticCodes.Validation.InitOnlyFunctionStyleProperty,
            "'property init' used with function-style property",
            "Validation",
            "The 'property init' accessor is only valid for auto-properties. Function-style properties cannot use init-only semantics.",
            "class Foo:\n    property init name(self, value: str):\n        self._name = value",
            "Use 'property set' for function-style setters, or switch to an auto-property with 'property init name: str'.");

        Add(dict, DiagnosticCodes.Validation.AbstractPropertyMustHaveEllipsisBody,
            "@abstract property must have ellipsis body",
            "Validation",
            "A property decorated with @abstract must use '...' (ellipsis) as its body. Abstract properties declare the interface without providing an implementation.",
            "class Shape:\n    @abstract\n    property get area(self) -> float:\n        return 0.0",
            "Use ellipsis for the body:\n    @abstract\n    property get area(self) -> float: ...");

        Add(dict, DiagnosticCodes.Validation.FinalWithAbstractOrVirtual,
            "@final combined with @abstract or @virtual on property",
            "Validation",
            "A property cannot be both @final and @abstract or @virtual. @final prevents overriding, while @abstract/@virtual require it.",
            "class Foo:\n    @final\n    @abstract\n    property get name(self) -> str: ...",
            "Remove either @final or @abstract/@virtual.");

        Add(dict, DiagnosticCodes.Validation.InvalidPropertyOverride,
            "Invalid property override",
            "Validation",
            "A property with @override has no matching virtual or abstract property in the base class, or the types are incompatible. " +
            "Override properties must have a corresponding virtual or abstract property in the base class with a compatible type.",
            "class Base:\n    property get name(self) -> str:\n        return \"base\"\n\nclass Derived(Base):\n    @override\n    property get missing(self) -> str:\n        return \"derived\"",
            "Ensure the base class has a virtual or abstract property with the same name and compatible type.");

        Add(dict, DiagnosticCodes.Validation.FinalWithoutOverride,
            "@final without @override on method",
            "Validation",
            "A method is marked @final but not @override. The @final decorator prevents further overriding, " +
            "but only makes sense on a method that is itself an override of a virtual or abstract base method.",
            "class Base:\n    @virtual\n    def greet(self) -> str:\n        return \"hello\"\n\n" +
            "class Child(Base):\n    @final\n    def greet(self) -> str:  # missing @override\n        return \"hi\"",
            "Add @override before @final:\nclass Child(Base):\n    @override\n    @final\n    def greet(self) -> str:\n        return \"hi\"");

        Add(dict, DiagnosticCodes.Validation.DunderInUserInterface,
            "Dunder method in user-defined interface",
            "Validation",
            "Dunder methods (e.g., __len__, __str__, __eq__) cannot be declared in user-defined interfaces. " +
            "Only standard library interfaces (ISized, IBoolConvertible, etc.) may declare dunder methods. " +
            "User-defined interfaces should declare regular methods instead.",
            "interface IMyProtocol:\n    def __len__(self) -> int:\n        ...",
            "Use a regular method name in your interface:\ninterface IMyProtocol:\n    def get_length(self) -> int:\n        ...");

        Add(dict, DiagnosticCodes.Validation.UnknownDunderMethod,
            "Unknown dunder method",
            "Validation",
            "Only recognized operator and protocol dunder methods are supported. " +
            "Unknown dunder methods like __custom__ are rejected at compile time. " +
            "Recognized operator dunders include __add__, __sub__, __eq__, __ne__, __lt__, __gt__, etc. " +
            "Recognized protocol dunders include __len__, __bool__, __str__, __iter__, __next__, __contains__, __reversed__, etc.",
            "class Foo:\n    def __custom__(self) -> int:\n        return 42",
            "Use a regular method name instead:\nclass Foo:\n    def custom(self) -> int:\n        return 42");

        Add(dict, DiagnosticCodes.Validation.VirtualOnStructMethod,
            "@virtual on struct method",
            "Validation",
            "Struct methods cannot be marked @virtual because structs are implicitly sealed in C# — " +
            "they cannot be inherited from. The @virtual decorator only makes sense on class methods.",
            "struct Point:\n    x: int\n    @virtual\n    def __str__(self) -> str:\n        return \"point\"",
            "Remove the @virtual decorator:\nstruct Point:\n    x: int\n    def __str__(self) -> str:\n        return \"point\"");

        Add(dict, DiagnosticCodes.Validation.NonExhaustiveMatchExpression, "Non-exhaustive match expression", "Validation",
            "A match expression does not cover all possible values of the scrutinee type. Match expressions must be exhaustive because they produce a value. " +
            "For enums, all members must be covered. For bools, both True and False must be covered. For tagged unions, all cases must be covered. " +
            "A wildcard pattern (_) or binding pattern covers all remaining cases. Guard clauses do not count toward exhaustiveness.",
            "union Option:\n    case Some(value: int)\n    case None_\nx: int = match opt:\n    case Some(v): v  # missing None_ case",
            "Cover all cases or add a wildcard:\nx: int = match opt:\n    case Some(v): v\n    case _: 0");

        Add(dict, DiagnosticCodes.Validation.VarianceOnClassOrStruct, "Variance annotation not allowed on class/struct", "Validation",
            "Type parameter variance annotations (in/out) are only allowed on delegate and interface declarations. " +
            "Classes and structs cannot have variant type parameters because they have both input and output positions.",
            "class Box[out T]:  # error: variance not allowed on class\n    value: T",
            "Remove the variance annotation:\nclass Box[T]:\n    value: T\n\nOr use a delegate or interface instead:\ndelegate Producer[out T]() -> T");

        Add(dict, DiagnosticCodes.Validation.CovariantInContravariantPosition, "Covariant type parameter in contravariant position", "Validation",
            "A type parameter declared as covariant (out) appears in a contravariant position (e.g., as a parameter type). " +
            "Covariant type parameters can only appear in output positions such as return types.",
            "delegate BadHandler[out T](value: T) -> None  # error: T is covariant but used as parameter",
            "Change the variance to 'in' or remove it:\ndelegate Handler[in T](value: T) -> None");

        Add(dict, DiagnosticCodes.Validation.ContravariantInCovariantPosition, "Contravariant type parameter in covariant position", "Validation",
            "A type parameter declared as contravariant (in) appears in a covariant position (e.g., as a return type). " +
            "Contravariant type parameters can only appear in input positions such as parameter types.",
            "delegate BadProducer[in T]() -> T  # error: T is contravariant but used as return type",
            "Change the variance to 'out' or remove it:\ndelegate Producer[out T]() -> T");


        // ── Event validation (SPY0420-SPY0423) ───────────────────────────

        Add(dict, DiagnosticCodes.Validation.UnpairedEventAccessor, "Unpaired event accessor", "Validation",
            "An event declaration has a function-style accessor (add/remove) without both accessors. Auto-events (without parentheses) generate both add and remove automatically. Function-style events with custom logic must define both.",
            "event add on_click(self, handler: EventHandler):  # error: missing remove accessor\n    pass",
            "Add the missing accessor or use auto-event syntax:\nevent add on_click(self, handler: EventHandler):\n    pass\nevent remove on_click(self, handler: EventHandler):\n    pass");

        Add(dict, DiagnosticCodes.Validation.EventFieldNameConflict, "Event conflicts with field name", "Validation",
            "An event has the same name as a field in the same class or struct. Events and fields occupy the same namespace and cannot share names.",
            "class Button:\n    on_click: str  # field\n    event on_click: EventHandler  # error: conflicts with field",
            "Rename either the field or the event to avoid the conflict.");

        Add(dict, DiagnosticCodes.Validation.EventMethodNameConflict, "Event conflicts with method name", "Validation",
            "An event has the same name as a method in the same class or struct. Events and methods occupy the same namespace and cannot share names.",
            "class Button:\n    def on_click(self) -> None: pass  # method\n    event on_click: EventHandler  # error: conflicts with method",
            "Rename either the method or the event to avoid the conflict.");

        Add(dict, DiagnosticCodes.Validation.AbstractEventWithBody, "Abstract event must not have a body", "Validation",
            "An abstract event has a function-style accessor with a body. Abstract events define only the signature and cannot provide an implementation.",
            "abstract event on_click(self, handler: EventHandler):  # error: abstract events cannot have implementation\n    pass",
            "Remove the body or remove the abstract keyword:\nabstract event on_click(self, handler: EventHandler)  # no body\n\nOr:\nevent add on_click(self, handler: EventHandler):\n    pass");

        // ── Decorator argument validation (SPY0425) ─────────────────────

        Add(dict, DiagnosticCodes.Validation.NonConstantDecoratorArgument, "Decorator argument must be a compile-time constant", "Validation",
            "Custom decorator arguments must be compile-time constant expressions because they map to C# attribute arguments. Allowed: string, int, float, bool literals, None, enum member access (e.g., MyEnum.value), and type(X).",
            "@custom(1 + 2)  # error: arithmetic expression is not a compile-time constant\ndef foo():\n    pass",
            "Use a literal value instead:\n@custom(3)\ndef foo():\n    pass");

        Add(dict, DiagnosticCodes.Validation.InitPropertyNotAssigned, "Init property not assigned in constructor", "Validation",
            "A 'property init' field without a default value must be assigned in every constructor (__init__). Init properties are set-once, so they must be initialized during construction.",
            "class Config:\n    property init port: int\n\n    def __init__(self):\n        pass  # error: 'port' not assigned",
            "Assign the init property in the constructor:\ndef __init__(self):\n    self.port = 8080");

        // ── Validation warnings (SPY0450-SPY0499) ──────────────────────

        Add(dict, DiagnosticCodes.Validation.UnreachableCodeWarning, "Unreachable code detected", "Validation",
            "Code after a return, raise, break, or continue statement can never be executed. This usually indicates dead code that should be removed.",
            "def foo() -> int:\n    return 1\n    x: int = 2  # unreachable",
            "Remove the unreachable code:\ndef foo() -> int:\n    return 1");

        Add(dict, DiagnosticCodes.Validation.UnusedVariable, "Unused variable", "Validation",
            "A local variable is assigned a value but never read. This often indicates a typo in a variable name or leftover debugging code.",
            "def foo():\n    x: int = 42  # x is never used\n    print(\"hello\")",
            "Remove the unused variable, or prefix it with underscore if intentionally unused:\ndef foo():\n    _x: int = 42  # intentionally unused\n    print(\"hello\")");

        Add(dict, DiagnosticCodes.Validation.UnusedImport, "Unused import", "Validation",
            "An imported name is never referenced in the module. Unused imports clutter the code and slow down compilation.",
            "from math import sqrt, pi  # pi is never used\ndef main():\n    print(sqrt(4))",
            "Remove the unused import:\nfrom math import sqrt\ndef main():\n    print(sqrt(4))");

        Add(dict, DiagnosticCodes.Validation.NamingConventionWarning,
            "Naming Convention Warning",
            "Naming",
            "Identifier contains consecutive underscores which may cause name collision after name mangling. " +
            "For example, 'foo__bar' and 'foo_bar' would both mangle to the same C# name.",
            "x: int = 1\nfoo__bar: int = 2  # warning: consecutive underscores",
            "Rename the identifier or use backtick escaping: `foo__bar`");

        Add(dict, DiagnosticCodes.Validation.EqWithoutObjectOverload,
            "__eq__ without object overload",
            "Validation",
            "A class defines '__eq__' but none of its overloads has parameter type 'object'. " +
            "Without '__eq__(self, other: object)', collections like set and dict will use reference equality " +
            "instead of value equality for instances of this class.",
            "class Point:\n    x: int\n    def __eq__(self, other: Point) -> bool:\n        return self.x == other.x",
            "Add an '__eq__(self, other: object)' overload, or if reference equality for collections is intended, suppress the warning.");

        Add(dict, DiagnosticCodes.Validation.EqObjectWithoutHash,
            "__eq__(object) without __hash__",
            "Validation",
            "A class defines '__eq__(self, other: object)' but not '__hash__'. " +
            "The .NET equality contract requires that if Equals is overridden, GetHashCode must also be overridden. " +
            "Without both, the type will behave incorrectly in hash-based collections (set, dict).",
            "class Foo:\n    x: int\n    def __eq__(self, other: object) -> bool:\n        return False",
            "Add a '__hash__(self) -> int' method:\nclass Foo:\n    x: int\n    def __eq__(self, other: object) -> bool:\n        return False\n    def __hash__(self) -> int:\n        return self.x");

        Add(dict, DiagnosticCodes.Validation.HashWithoutEqObject,
            "__hash__ without __eq__(object)",
            "Validation",
            "A class defines '__hash__' but not '__eq__(self, other: object)'. " +
            "The .NET equality contract requires that if GetHashCode is overridden, Equals must also be overridden. " +
            "Without both, the type will behave incorrectly in hash-based collections (set, dict).",
            "class Foo:\n    x: int\n    def __hash__(self) -> int:\n        return self.x",
            "Add an '__eq__(self, other: object) -> bool' method:\nclass Foo:\n    x: int\n    def __eq__(self, other: object) -> bool:\n        return False\n    def __hash__(self) -> int:\n        return self.x");

        Add(dict, DiagnosticCodes.Validation.UnsupportedDunderReversed,
            "__reversed__ now fully supported via generators",
            "Validation",
            "This diagnostic is no longer emitted. The '__reversed__' dunder method is fully supported " +
            "using generator functions with 'yield'. Define '__reversed__' as a generator that yields " +
            "elements in reverse order, and the compiler will generate a 'GetReverseEnumerator()' method " +
            "returning 'IEnumerator<T>' to satisfy 'IReverseEnumerable<T>'.",
            "class Countdown:\n    start: int\n    def __init__(self, start: int):\n        self.start = start\n    def __reversed__(self) -> int:\n        i = self.start\n        while i > 0:\n            yield i\n            i = i - 1",
            "Use 'yield' in '__reversed__' to produce elements in reverse order.");

        Add(dict, DiagnosticCodes.Validation.VirtualOnObjectOverride,
            "@virtual is redundant on Object override method",
            "Validation",
            "The @virtual decorator is redundant on __str__, __hash__, and __eq__(self, other: object) because " +
            "these always generate 'override' for the corresponding Object methods (ToString, GetHashCode, Equals). " +
            "The @virtual decorator will be ignored.",
            "class Foo:\n    @virtual\n    def __str__(self) -> str:\n        return \"foo\"",
            "Remove the @virtual decorator — the method is already an override:\nclass Foo:\n    def __str__(self) -> str:\n        return \"foo\"");

        Add(dict, DiagnosticCodes.Validation.StaticFieldViaInstance,
            "Static field accessed via instance",
            "Validation",
            "A static field (marked with @static) is being accessed via 'self' instead of the class name. " +
            "While this works, it is misleading because static fields are shared across all instances. " +
            "Prefer accessing them via the class name for clarity.",
            "class Counter:\n    @static\n    count: int = 0\n    def get(self) -> int:\n        return self.count  # warning: static field via instance",
            "Access the field via the class name:\nclass Counter:\n    @static\n    count: int = 0\n    def get(self) -> int:\n        return Counter.count");

        // ── Validation warnings: Exhaustiveness (SPY0463) ─────────────

        Add(dict, DiagnosticCodes.Validation.NonExhaustiveMatch, "Non-exhaustive match statement", "Validation",
            "A match statement does not cover all possible values of the scrutinee type. " +
            "For enums, all members should be covered. For bools, both True and False should be covered. For tagged unions, all cases should be covered. " +
            "A wildcard pattern (_) or binding pattern covers all remaining cases. Guard clauses do not count toward exhaustiveness.",
            "union Shape:\n    case Circle(r: float)\n    case Square(s: float)\nmatch shape:\n    case Circle(r):  # missing Square case\n        print(r)",
            "Cover all cases or add a wildcard:\nmatch shape:\n    case Circle(r):\n        print(r)\n    case _:\n        pass");

        // ── Validation errors: Dunder invocation rules (SPY0427-SPY0429)

        Add(dict, DiagnosticCodes.Validation.DunderDirectInvocation,
            "Direct dunder method invocation",
            "Validation",
            "Dunder methods (double-underscore methods like __eq__, __str__, __len__) cannot be called directly from user code. " +
            "They define how a type behaves with operators and built-in functions, but users should invoke that behavior " +
            "through operators (==, +, <) or built-in functions (str(), len()), not by calling dunders directly. " +
            "Dunder-to-dunder calls on self or super() are allowed only inside another dunder method body.",
            "class Foo:\n    value: int\n    def compare(self, other: Foo) -> bool:\n        return self.__eq__(other)  # error: direct dunder call",
            "Use the corresponding operator or built-in function:\nclass Foo:\n    value: int\n    def compare(self, other: Foo) -> bool:\n        return self == other  # use == operator");

        Add(dict, DiagnosticCodes.Validation.DunderWrongReceiver,
            "Dunder call on wrong receiver",
            "Validation",
            "Inside a dunder method, dunder calls are only allowed on 'self' (for cross-dunder synthesis) or 'super()' " +
            "(for calling the base class implementation). Calling a dunder on any other object is not allowed — " +
            "use the corresponding operator or built-in function instead.",
            "class Foo:\n    def __eq__(self, other: object) -> bool:\n        return other.__eq__(self)  # error: receiver is not self or super()",
            "Use the corresponding operator:\nclass Foo:\n    def __eq__(self, other: object) -> bool:\n        return other == self  # use == operator");

        Add(dict, DiagnosticCodes.Validation.DunderCapture,
            "Captured dunder method reference",
            "Validation",
            "Dunder method references cannot be captured (assigned to variables, passed as arguments, etc.). " +
            "Dunder methods must be called immediately as part of a function call expression. " +
            "This restriction ensures that dunder dispatch is always static and verifiable at compile time.",
            "class Foo:\n    def __str__(self) -> str:\n        f = self.__eq__  # error: captured dunder reference\n        return \"Foo\"",
            "Call the dunder method immediately instead of capturing it:\nclass Foo:\n    def __str__(self) -> str:\n        result: bool = self.__eq__(other)  # OK: immediate call\n        return \"Foo\"");

        // ── Validation errors: Access modifier decorators (SPY0430-SPY0431)

        Add(dict, DiagnosticCodes.Validation.ConflictingAccessModifiers,
            "Conflicting access modifier decorators",
            "Validation",
            "A definition has multiple conflicting access modifier decorators (e.g., @private and @protected). " +
            "Only one access modifier decorator is allowed per definition.",
            "class Foo:\n    @private\n    @protected\n    def method(self) -> None:\n        ...",
            "Use only one access modifier decorator:\nclass Foo:\n    @private\n    def method(self) -> None:\n        ...");

        Add(dict, DiagnosticCodes.Validation.AccessModifierOnDunder,
            "Access modifier on dunder method",
            "Validation",
            "An access modifier decorator (@private, @protected, @internal) was applied to a dunder method. " +
            "Dunder methods are protocol methods with well-defined semantics and should not have their access level changed.",
            "class Foo:\n    @private\n    def __init__(self) -> None:\n        ...",
            "Remove the access modifier decorator from the dunder method:\nclass Foo:\n    def __init__(self) -> None:\n        ...");

        // ── Validation errors: Unsupported Python constructs (SPY0432) ──

        Add(dict, DiagnosticCodes.Validation.NamedtupleNotSupported,
            "collections.namedtuple is not supported",
            "Validation",
            "Sharpy does not support collections.namedtuple. Use native named tuples " +
            "(type aliases with named fields) or @dataclass for data-holding classes instead.",
            "from collections import namedtuple\nPoint = namedtuple(\"Point\", [\"x\", \"y\"])",
            "Use native named tuples:\ntype Point = tuple[x: float, y: float]\n\n" +
            "Or use @dataclass:\n@dataclass\nclass Point:\n    x: float\n    y: float");

        // ── Validation errors: Late-bound defaults (SPY0433-SPY0434) ────

        Add(dict, DiagnosticCodes.Validation.LateBoundSelfReference,
            "Late-bound default references its own parameter",
            "Validation",
            "A late-bound default expression (=>) references the parameter it is the default for. " +
            "This would be a circular evaluation and is not allowed.",
            "def f(x: int => x) -> int: ...",
            "Remove the self-reference or use a different expression:\n" +
            "def f(x: int => 0) -> int: ...");

        Add(dict, DiagnosticCodes.Validation.LateBoundForwardReference,
            "Late-bound default references a later parameter",
            "Validation",
            "A late-bound default expression (=>) references a parameter that is declared after it. " +
            "Late-bound defaults may only reference parameters that appear before them in the parameter list.",
            "def f(x: int => y, y: int = 0) -> int: ...",
            "Reorder parameters so that referenced parameters come first:\n" +
            "def f(y: int = 0, x: int => y) -> int: ...");

        // ── Validation errors: Struct field ordering (SPY0435) ─────────

        Add(dict, DiagnosticCodes.Validation.StructFieldOrdering,
            "Non-default struct field follows a field with a default value",
            "Validation",
            "In a struct definition, fields without default values must be declared before fields " +
            "with default values. This ensures the auto-generated constructor has required parameters " +
            "before optional ones, which is required by C#.",
            "struct Bad:\n    x: int = 0\n    y: int  # error: no default after default",
            "Move fields without defaults before fields with defaults:\n" +
            "struct Good:\n    y: int\n    x: int = 0");

        // ── Validation errors: Conversion operators (SPY0436-SPY0439) ────

        Add(dict, DiagnosticCodes.Validation.ConversionOperatorNotStatic,
            "Conversion operator must be @static",
            "Validation",
            "Conversion operators (__implicit__ and __explicit__) must be declared as @static methods with no 'self' parameter.",
            "def __implicit__(self, val: int) -> MyType: ...",
            "Add @static and remove self:\n@static\ndef __implicit__(val: int) -> MyType: ...");

        Add(dict, DiagnosticCodes.Validation.ConversionOperatorParamCount,
            "Conversion operator must have exactly 1 parameter",
            "Validation",
            "Conversion operators must have exactly one parameter (the source value to convert).",
            "@static\ndef __implicit__(a: int, b: int) -> MyType: ...",
            "Use exactly one parameter:\n@static\ndef __implicit__(val: int) -> MyType: ...");

        Add(dict, DiagnosticCodes.Validation.ConversionOperatorNoEnclosingType,
            "At least one type must be the enclosing type",
            "Validation",
            "In a conversion operator, either the parameter type or the return type must be the enclosing class/struct type.",
            "class Foo:\n    @static\n    def __implicit__(val: int) -> str: ...",
            "Ensure one of the types is the enclosing type:\nclass Foo:\n    @static\n    def __implicit__(val: int) -> Foo: ...");

        Add(dict, DiagnosticCodes.Validation.ConversionOperatorDuplicate,
            "Cannot define both implicit and explicit for the same type pair",
            "Validation",
            "A class cannot define both __implicit__ and __explicit__ conversion operators for the same source and target type pair.",
            "@static\ndef __implicit__(val: int) -> Foo: ...\n@static\ndef __explicit__(val: int) -> Foo: ...",
            "Choose either __implicit__ or __explicit__ for each type pair.");

        // ── Validation warnings: Deprecation (SPY0464) ─────────────────

        Add(dict, DiagnosticCodes.Validation.DeprecatedBodylessSyntax,
            "Deprecated body-less abstract method syntax",
            "Validation",
            "An abstract method uses the deprecated body-less syntax (no body at all). " +
            "The preferred syntax uses '...' (ellipsis) as the method body to indicate an abstract method.",
            "@abstract\nclass Shape:\n    def area(self) -> float  # deprecated: no body",
            "Use ellipsis as the body:\n@abstract\nclass Shape:\n    def area(self) -> float: ...");

        // ── Validation warnings: Identity operator (SPY0465) ──────────────

        Add(dict, DiagnosticCodes.Validation.IsWithValueTypes,
            "Identity operator used with value types",
            "Validation",
            "The 'is' operator (identity/reference equality) is used with a value type. " +
            "Value types are compared by value, not by reference, so 'is' may not behave as expected. " +
            "Use '==' for value comparison instead.",
            "x: int = 42\nif x is 42: ...",
            "Use '==' instead of 'is' for value types:\nif x == 42: ...");
        // ── Validation warnings: Deprecated usage (SPY0466) ──────────────

        Add(dict, DiagnosticCodes.Validation.DeprecatedUsage,
            "Usage of deprecated symbol",
            "Validation",
            "A function, method, class, or property marked with @deprecated is being used. " +
            "The deprecation message explains why and what to use instead.",
            "@deprecated(\"Use bar() instead\")\ndef foo(): ...\n\nfoo()  # SPY0466 warning",
            "Follow the deprecation message and migrate to the suggested alternative.");

        // ── Validation warnings: Readonly property assignment (SPY0467) ────

        Add(dict, DiagnosticCodes.Validation.ReadonlyPropertyAssignment,
            "Assignment to readonly property",
            "Validation",
            "A property marked with @readonly cannot be assigned to after construction. " +
            "@readonly properties can only be set in __init__.",
            "@readonly\nproperty name: str\n\ndef change(self):\n    self.name = \"new\"  # SPY0467 error",
            "Remove the assignment or change the property to not be @readonly.");

        // ── Validation warnings: Constant pattern shadow (SPY0468) ─────

        Add(dict, DiagnosticCodes.Validation.ConstantPatternShadow,
            "Pattern capture shadows constant",
            "Validation",
            "A capture variable in a match pattern has the same name as a module-level constant. " +
            "The identifier is treated as a constant pattern (matching its value), not a capture binding. " +
            "Use a different name if you intended to capture the matched value.",
            "MAX: Final[int] = 100\nmatch x:\n    case MAX:  # matches value 100, does NOT capture into MAX",
            "Rename the capture variable to avoid ambiguity with the constant.");

        // ── Validation transition hints (SPY0470-SPY0489) ──────────────
        // Hint-severity advisories about behavioral differences from Python/C#.
        // Suppressible like warnings, but never promoted to errors under -Werror.

        Add(dict, DiagnosticCodes.Validation.Utf16StringLengthHint,
            "len(str) returns UTF-16 code units, not Unicode code points",
            "Validation",
            "In Sharpy, len(s) on a string returns the number of UTF-16 code units (matching .NET's String.Length), " +
            "not the number of Unicode code points (as Python does). Strings outside the BMP (e.g., emoji, " +
            "supplementary plane characters) are encoded as surrogate pairs and count as 2 code units. This is a " +
            "deliberate Axiom 1 (.NET first) decision; helper methods in the str module provide code-point counts.",
            "len(\"\\U0001F600\")  # Sharpy: 2 (UTF-16 surrogate pair); Python: 1 (1 code point)",
            "If you need Unicode code-point counts, use the explicit helper (e.g., str.code_point_count(s)).");

        Add(dict, DiagnosticCodes.Validation.StructValueSemanticsHint,
            "Struct assignment copies the value (value semantics)",
            "Validation",
            "Sharpy structs follow .NET value semantics: assigning a struct or passing it to a function copies the " +
            "entire value, so mutations on the copy do not affect the original. This differs from Python (where " +
            "everything is a reference) and from Sharpy classes. Mark a parameter as `ref` (or use a class) if you " +
            "want shared mutation.",
            "@struct\nclass Point:\n    x: int = 0\n\np = Point()\nq = p          # copy — q.x = 5 won't change p.x\nq.x = 5",
            "Use `ref` parameters for in-place mutation, or model the type as a class if reference semantics are required.");

        Add(dict, DiagnosticCodes.Validation.HomogeneousVariadicHint,
            "Variadic parameters in Sharpy are homogeneous and statically typed",
            "Validation",
            "Sharpy's `*args` declares a typed, homogeneous list (`list[T]`), not Python's heterogeneous tuple of " +
            "`Any`. Every argument forwarded through `*args` must satisfy the declared element type. This enforces " +
            "Axiom 3 (type safety) — there is no `Any` escape hatch.",
            "def log(*args: int) -> None: ...\nlog(1, 2, \"three\")  # SPY0220 — \"three\" violates int element type",
            "Annotate the variadic with the broadest concrete type your callers need (e.g., `*args: object`), or define overloads.");

        Add(dict, DiagnosticCodes.Validation.NoClassmethodHint,
            "@classmethod is not supported — use @staticmethod or a factory",
            "Validation",
            "Sharpy intentionally omits Python's @classmethod decorator. .NET's type system does not pass the class " +
            "object as a first parameter, and the feature would require a runtime indirection that conflicts with " +
            "Axiom 1. Use @staticmethod for type-independent helpers, or define a regular factory method.",
            "@classmethod\ndef from_string(cls, s: str) -> Self: ...   # not supported",
            "Use @staticmethod and reference the type by name, or use a factory function on the module.");

        Add(dict, DiagnosticCodes.Validation.NoAsyncComprehensionHint,
            "Async comprehensions are not supported",
            "Validation",
            "Sharpy does not support `async for` inside list/set/dict comprehensions or generator expressions. " +
            ".NET's async streaming model (IAsyncEnumerable<T>) is exposed differently and is most cleanly used " +
            "via explicit `async for` loops over async iterables.",
            "results = [x async for x in stream()]  # not supported",
            "Rewrite using an explicit `async for` loop and append/yield each element.");

        Add(dict, DiagnosticCodes.Validation.SingleIsinstanceTypeHint,
            "isinstance() takes exactly one type argument",
            "Validation",
            "Unlike Python, Sharpy's isinstance() accepts only a single type, not a tuple of types. This keeps the " +
            "result type narrowing precise (the value is narrowed to that one type) and avoids tuple-shaped " +
            "argument quirks. Compose multiple checks with `or`.",
            "if isinstance(x, (int, str)):  # not supported\n    ...",
            "Combine type checks with `or`: `if isinstance(x, int) or isinstance(x, str): ...`");

        Add(dict, DiagnosticCodes.Validation.NegativeTupleIndexHint,
            "Negative tuple indices are rejected at compile time",
            "Validation",
            "Tuples in Sharpy have a fixed, statically known length, so negative indexing (Python's t[-1] for the " +
            "last element) is rejected at compile time as SPY0259 — you should use the positive index of the " +
            "corresponding element. This is a transition hint that explains the diagnostic for Python users.",
            "t = (1, 2, 3)\nlast = t[-1]  # SPY0259 — use t[2] instead",
            "Use the explicit positive index, or convert to a list if dynamic indexing is needed.");

        Add(dict, DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint,
            "@static / @staticmethod is unnecessary on a method without 'self'",
            "Validation",
            "Sharpy infers static methods automatically: a method declared inside a class, struct, or interface " +
            "is treated as static when its first parameter is not the implicit 'self' (an untyped first " +
            "parameter named 'self'). The '@static' decorator is therefore optional, and the Python " +
            "'@staticmethod' decorator is rejected outright (DecoratorValidator). This hint flags the redundant " +
            "case so users transitioning from Python or C# learn the convention and can simplify their code.",
            "class Math:\n    @static                     # redundant — already static\n    def square(x: int) -> int:\n        return x * x",
            "Drop the '@static' / '@staticmethod' decorator. The method remains static because it has no 'self' parameter.");

        // ── Code generation errors (SPY0500-SPY0599) ───────────────────

        Add(dict, DiagnosticCodes.CodeGen.EmitError, "Code generation error", "CodeGen",
            "An error occurred during C# code generation. This is typically an internal compiler error that should be reported as a bug.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the source file that triggered it.");

        Add(dict, DiagnosticCodes.CodeGen.UnsupportedFeature, "Unsupported feature in code generation", "CodeGen",
            "The code uses a language feature that the code generator does not yet support. The feature is valid Sharpy syntax but cannot be compiled to C# yet.",
            null,
            "Check the language specification for supported features, or file a feature request.");

        Add(dict, DiagnosticCodes.CodeGen.EmptyClassName, "Empty class name in code generation", "CodeGen",
            "The code generator encountered a class with an empty name. This is an internal compiler error.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues.");

        Add(dict, DiagnosticCodes.CodeGen.DuplicateMember, "Duplicate member in generated code", "CodeGen",
            "The code generator detected a duplicate member name in the generated C# class. This can happen when name mangling produces a collision.",
            null,
            "Rename one of the conflicting members to avoid the collision.");

        Add(dict, DiagnosticCodes.CodeGen.EmptyMethodName, "Empty method name in code generation", "CodeGen",
            "The code generator encountered a method with an empty name. This is an internal compiler error.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues.");

        Add(dict, DiagnosticCodes.CodeGen.AbstractMethodWithBody, "Abstract method with body in code generation", "CodeGen",
            "The code generator encountered an abstract method that has a body. Abstract methods should not have implementations.",
            null,
            "This is an internal compiler error. The semantic analyzer should have caught this. Report it at https://github.com/antonsynd/sharpy/issues.");

        Add(dict, DiagnosticCodes.CodeGen.NonAbstractMethodWithoutBody, "Non-abstract method without body", "CodeGen",
            "The code generator encountered a concrete (non-abstract) method that has no body. Only abstract and interface methods can omit the body.",
            null,
            "This is an internal compiler error. The semantic analyzer should have caught this. Report it at https://github.com/antonsynd/sharpy/issues.");

        Add(dict, DiagnosticCodes.CodeGen.VarWithoutInitializer, "Variable without initializer in code generation", "CodeGen",
            "The code generator encountered a variable declaration without an initializer. All variables should have initializers by this point in the compilation pipeline.",
            null,
            "This is an internal compiler error. Report it at https://github.com/antonsynd/sharpy/issues.");

        Add(dict, DiagnosticCodes.CodeGen.PositionalPatternFallback, "Positional pattern Deconstruct fallback", "CodeGen",
            "The code generator is emitting a positional pattern as a Deconstruct call. " +
            "This is a defensive warning — the semantic layer should have caught types without Deconstruct (SPY0369).",
            null,
            "This is an internal compiler warning. If Deconstruct is missing, check that type checking caught it.");

        Add(dict, DiagnosticCodes.CodeGen.UnrecognizedStatementType, "Unrecognized statement type not emitted", "CodeGen",
            "The code generator encountered a statement type that it does not know how to emit. " +
            "The statement was silently skipped, meaning the generated code does not include it. " +
            "This is a compiler bug — the code generator is missing a handler for this AST node type.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        Add(dict, DiagnosticCodes.CodeGen.UnsupportedExpressionType, "Unsupported expression type in code generation", "CodeGen",
            "The code generator encountered an expression or statement type that it does not know how to emit. " +
            "This is either a not-yet-implemented feature or a compiler bug.",
            null,
            "If you believe this is valid Sharpy code, report it at https://github.com/antonsynd/sharpy/issues.");

        Add(dict, DiagnosticCodes.CodeGen.UnsupportedOperator, "Unsupported operator in code generation", "CodeGen",
            "The code generator encountered an operator that it does not know how to emit. " +
            "This is either a not-yet-implemented operator or a compiler bug.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        Add(dict, DiagnosticCodes.CodeGen.NameCollision, "Module class name collision", "CodeGen",
            "A type name in the source file matches the module class name derived from the file name. " +
            "For class types, the class absorbs module-level members and becomes the module representative. " +
            "For struct, interface, or enum types, this is an error because they cannot serve as module classes.",
            "# File: animal.spy\n# Module class name would be 'Animal', but 'struct Animal' collides\nstruct Animal:\n    name: str",
            "Rename the type or the source file so that the type name does not match the file name in PascalCase.");

        Add(dict, DiagnosticCodes.CodeGen.MemberNameCollision, "Member name collision after mangling", "CodeGen",
            "Two symbols in the same scope produce the same C# name after name mangling. " +
            "For example, 'foo_bar' and 'FooBar' both compile to 'FooBar'.",
            "class Foo:\n    def foo_bar(self): ...\n    def FooBar(self): ...",
            "Rename one of the conflicting symbols or use backtick escaping.");

        Add(dict, DiagnosticCodes.CodeGen.FunctionModuleClassCollision, "Function name collides with module class name", "CodeGen",
            "A module-level function's mangled name matches the module class name derived from the source filename. " +
            "In C#, a member cannot have the same name as its enclosing type (CS0542).",
            "# File: bubble_sort.spy\ndef bubble_sort(arr: list[int]) -> list[int]:\n    ...\n# 'bubble_sort' compiles to 'BubbleSort', same as class 'BubbleSort' from filename",
            "Rename the function or the source file so the function's PascalCase name does not match the filename's PascalCase name.");

        Add(dict, DiagnosticCodes.CodeGen.TypeReExportNotSupported, "Type re-export not supported", "CodeGen",
            "A type cannot be re-exported from an __init__.spy package file. Types should be imported directly " +
            "from their defining module rather than re-exported through package init files.",
            "# __init__.spy\nfrom .helpers import MyClass  # MyClass is a type — cannot re-export",
            "Import the type directly from its defining module instead of through the package init file.");

        Add(dict, DiagnosticCodes.CodeGen.InternalGeneratedCSharpParseError, "Internal error: generated C# contains syntax errors", "CodeGen",
            "The compiler generated C# code that fails to parse. This indicates a bug in the Sharpy compiler's code generation phase. " +
            "The generated C# has syntax errors that would prevent compilation.",
            null,
            "This is an internal compiler error. Please report it at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        // ── Infrastructure errors (SPY0900-SPY0999) ────────────────────

        Add(dict, DiagnosticCodes.Infrastructure.CompilationFailed, "Compilation failed", "Infrastructure",
            "The overall compilation process failed. This is a summary error that accompanies more specific errors from earlier phases.",
            null,
            "Fix the errors reported in earlier phases (lexer, parser, semantic, or code generation).");

        Add(dict, DiagnosticCodes.Infrastructure.CompilationCancelled, "Compilation cancelled", "Infrastructure",
            "The compilation was cancelled, either by user request or by a timeout. No output was produced.",
            null,
            "Re-run the compilation. If it keeps timing out, check for very large files or circular dependencies.");

        Add(dict, DiagnosticCodes.Infrastructure.AssemblyCompilationFailed, "Assembly compilation failed", "Infrastructure",
            "The Roslyn C# compilation of the generated code failed. This means the compiler produced C# code that the .NET compiler could not compile.",
            null,
            "This is likely an internal compiler error. Report it at https://github.com/antonsynd/sharpy/issues with the source file.");

        Add(dict, DiagnosticCodes.Infrastructure.FileReadError, "File read error", "Infrastructure",
            "A source file could not be read from disk. This may be due to missing files, permission issues, or invalid file paths.",
            null,
            "Verify the file exists, the path is correct, and you have read permissions.");

        Add(dict, DiagnosticCodes.Infrastructure.InvariantViolation, "Internal invariant violation", "Infrastructure",
            "An internal compiler invariant was violated. This is a compiler bug — " +
            "the semantic pipeline produced data that fails a post-phase consistency check. " +
            "The compilation may still succeed, but the generated code could be incorrect.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        Add(dict, DiagnosticCodes.Infrastructure.TooManyErrors, "Too many errors", "Infrastructure",
            "The compiler stopped reporting errors because the maximum error limit was reached. " +
            "Additional errors may exist but were suppressed. The reported errors should be fixed first, " +
            "as later errors are often caused by earlier ones.",
            null,
            "Fix the reported errors and re-compile. Use '--max-errors N' to increase the limit if needed.");

        Add(dict, DiagnosticCodes.Infrastructure.ParserLoopStall, "Parser loop stall detected", "Infrastructure",
            "The parser detected that it made no progress in a parsing loop. This is a safety mechanism " +
            "that prevents the parser from hanging on malformed input. The parser forcibly advanced past " +
            "the problematic token to continue parsing. This warning indicates the input may be malformed " +
            "or there is an edge case in the parser that should be reported.",
            null,
            "Check the source code at the indicated location for syntax errors. If the input looks correct, " +
            "report this at https://github.com/antonsynd/sharpy/issues with the source file.");

        Add(dict, DiagnosticCodes.Infrastructure.UnexpectedUnknownType, "Unexpected unknown type", "Infrastructure",
            "Type inference produced an UnknownType for an expression without a corresponding error diagnostic. " +
            "This indicates a gap in the type checker where a type could not be resolved but no user-facing error " +
            "was emitted. This is distinct from error-recovery Unknown types, which are expected when the user " +
            "writes invalid code.",
            null,
            "Report this at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        // ── Informational diagnostics (SPY1000-SPY1099) ────────────────────

        Add(dict, DiagnosticCodes.Info.ImplicitInterfaceSynthesis, "Implicit interface synthesis", "CodeGen",
            "The compiler automatically added a .NET interface to the generated class because the class defines " +
            "a dunder method that maps to that interface. For example, defining __len__ causes the class to " +
            "implement ISized, and defining __bool__ causes it to implement IBoolConvertible.",
            null,
            "This is informational only. No action is required. The synthesized interface enables interop with " +
            ".NET code that expects the interface (e.g., len() dispatch via ISized).");

        return dict;
    }

    private static void Add(
        Dictionary<string, DiagnosticExplanation> dict,
        string code,
        string title,
        string category,
        string description,
        string? example,
        string? fix)
    {
        dict[code] = new DiagnosticExplanation
        {
            Code = code,
            Title = title,
            Description = description,
            Example = example,
            Fix = fix,
            Category = category
        };
    }
}
