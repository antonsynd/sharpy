namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// DiagnosticExplanations partial: Parser diagnostic entries (SPY0100-SPY0199)
/// </summary>
public static partial class DiagnosticExplanations
{
    private static void AddParserEntries(Dictionary<string, DiagnosticExplanation> dict)
    {
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

        Add(dict, DiagnosticCodes.Parser.MultipleStarsInPattern, "Multiple stars in list pattern", "Parser",
            "A list (sequence) pattern may contain at most one '*' capture, which collects the remaining elements. Two or more stars are ambiguous.",
            "match items:\n    case [*a, *b]:\n        ...",
            "Use a single star capture:\nmatch items:\n    case [first, *rest]:\n        ...");

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
            "Reserved diagnostic. Keyword argument '_' placeholders are supported and lower to a lambda whose parameter is named after the keyword.",
            "result = f(x=_)",
            "result = f(x=_)  # lowers to: (x) => f(x=x)");

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

    }
}
