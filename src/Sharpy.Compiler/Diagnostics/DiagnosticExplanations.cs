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

        // ── Lexer errors (SHP0001-SHP0099) ──────────────────────────────

        Add(dict, DiagnosticCodes.Lexer.UnterminatedString, "Unterminated string literal", "Lexer",
            "A string literal was opened with a quote character but never closed. The lexer reached the end of the line or file without finding a matching closing quote.",
            "x: str = \"hello",
            "Add the closing quote:\n  x: str = \"hello\"");

        Add(dict, DiagnosticCodes.Lexer.InvalidEscapeSequence, "Invalid escape sequence", "Lexer",
            "A backslash in a string literal is followed by a character that is not a recognized escape sequence. Valid escapes include \\n, \\t, \\\\, \\\", \\', \\0, \\a, \\b, \\f, \\r, \\v, \\x, and \\u.",
            "path: str = \"C:\\new_folder\"",
            "Use a raw string or double the backslash:\n  path: str = r\"C:\\new_folder\"\n  path: str = \"C:\\\\new_folder\"");

        Add(dict, DiagnosticCodes.Lexer.MixedTabsAndSpaces, "Mixed tabs and spaces", "Lexer",
            "The source file uses both tabs and spaces for indentation. Sharpy requires consistent indentation using spaces only (4 spaces per level).",
            null,
            "Convert all tabs to spaces. Most editors have a setting to convert tabs to spaces automatically.");

        Add(dict, DiagnosticCodes.Lexer.InvalidIndentation, "Invalid indentation", "Lexer",
            "The indentation level does not match any expected indentation. Each indentation level must be exactly 4 spaces.",
            "def foo():\n   return 1  # 3 spaces instead of 4",
            "Use exactly 4 spaces per indentation level:\ndef foo():\n    return 1");

        Add(dict, DiagnosticCodes.Lexer.UnexpectedCharacter, "Unexpected character", "Lexer",
            "The lexer encountered a character that is not part of any valid token. This may be a non-ASCII character, a stray symbol, or a character that is not valid in the current context.",
            "x: int = 42§",
            "Remove or replace the unexpected character.");

        // ── Parser errors (SHP0100-SHP0199) ─────────────────────────────

        Add(dict, DiagnosticCodes.Parser.UnexpectedToken, "Unexpected token", "Parser",
            "The parser encountered a token that does not fit the expected grammar at this position. This usually indicates a syntax error such as a missing operator, misplaced keyword, or malformed expression.",
            "x: int = if",
            "Check the syntax around the reported location. Common causes: missing operators, extra commas, or misplaced keywords.");

        Add(dict, DiagnosticCodes.Parser.ExpectedIdentifier, "Expected identifier", "Parser",
            "The parser expected a name (identifier) but found something else. This commonly occurs when a keyword is used where a variable or function name is expected.",
            "def 42():\n    pass",
            "Provide a valid identifier:\ndef my_function():\n    pass");

        Add(dict, DiagnosticCodes.Parser.ExpectedToken, "Expected token", "Parser",
            "A specific token (such as a colon, parenthesis, or comma) was expected but not found. The error message will indicate which token was expected.",
            "def greet(name: str)\n    print(name)",
            "Add the missing token. In this example, add a colon after the parameter list:\ndef greet(name: str):\n    print(name)");

        Add(dict, DiagnosticCodes.Parser.PositionalAfterKeyword, "Positional argument after keyword argument", "Parser",
            "A positional argument appeared after a keyword argument in a function call. Once you use a keyword argument, all subsequent arguments must also be keyword arguments.",
            "greet(name=\"Alice\", 42)",
            "Move positional arguments before keyword arguments, or use keyword syntax for all:\ngreet(42, name=\"Alice\")\ngreet(age=42, name=\"Alice\")");

        Add(dict, DiagnosticCodes.Parser.EmptyEnum, "Empty enum", "Parser",
            "An enum declaration has no members. Enums must have at least one member.",
            "enum Color:\n    pass",
            "Add at least one enum member:\nenum Color:\n    RED\n    GREEN\n    BLUE");

        Add(dict, DiagnosticCodes.Parser.FreeUnionNotSupported, "Free union types not supported", "Parser",
            "A union type annotation like `int | str` was used outside of a supported context. Sharpy uses Optional[T] for nullable types and does not support arbitrary union types.",
            "x: int | str = 42",
            "Use a common base type, Optional[T] for nullable values, or redesign to avoid union types:\nx: Optional[int] = 42");

        // ── Semantic errors: Name resolution (SHP0200-SHP0219) ──────────

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

        Add(dict, DiagnosticCodes.Semantic.DuplicateDefinition, "Duplicate definition", "Semantic",
            "A name was defined more than once in the same scope. Each name can only be defined once per scope (function, class, or module level).",
            "x: int = 1\nx: str = \"hello\"  # duplicate",
            "Use a different name or remove the duplicate definition.");

        Add(dict, DiagnosticCodes.Semantic.DuplicateClassField, "Duplicate class field", "Semantic",
            "A class or struct has two fields with the same name. Each field name must be unique within a type.",
            "class Point:\n    x: int\n    x: float  # duplicate",
            "Rename one of the fields:\nclass Point:\n    x: int\n    y: float");

        Add(dict, DiagnosticCodes.Semantic.DuplicateParameter, "Duplicate parameter", "Semantic",
            "A function or method has two parameters with the same name.",
            "def add(x: int, x: int) -> int:\n    return x + x",
            "Give each parameter a unique name:\ndef add(x: int, y: int) -> int:\n    return x + y");

        // ── Semantic errors: Type checking (SHP0220-SHP0259) ────────────

        Add(dict, DiagnosticCodes.Semantic.TypeMismatch, "Type mismatch", "Semantic",
            "The actual type of an expression does not match the expected type. This can occur in assignments, function arguments, return statements, and other contexts where a specific type is required.",
            "x: int = \"hello\"  # str assigned to int",
            "Ensure the types match. Either change the value, add a type conversion, or change the type annotation.");

        Add(dict, DiagnosticCodes.Semantic.WrongArgumentCount, "Wrong argument count", "Semantic",
            "A function or method was called with the wrong number of arguments. The error message indicates how many arguments were expected and how many were provided.",
            "def greet(name: str) -> str:\n    return f\"Hello, {name}\"\n\ngreet()  # expected 1 argument",
            "Provide the correct number of arguments:\ngreet(\"Alice\")");

        Add(dict, DiagnosticCodes.Semantic.MissingTypeAnnotation, "Missing type annotation", "Semantic",
            "A variable declaration is missing a type annotation. Sharpy requires explicit type annotations for all variable declarations to ensure static type safety.",
            "x = 42  # missing type annotation",
            "Add a type annotation:\nx: int = 42");

        Add(dict, DiagnosticCodes.Semantic.NullabilityViolation, "Nullability violation", "Semantic",
            "A potentially null value is being used in a context that requires a non-null value. Use Optional[T] for values that can be None, and handle the None case before using the value.",
            "def get_name() -> Optional[str]:\n    return None\n\nname: str = get_name()  # might be None",
            "Handle the null case:\nresult = get_name()\nif result is not None:\n    name: str = result");

        Add(dict, DiagnosticCodes.Semantic.ConditionNotBoolean, "Condition is not boolean", "Semantic",
            "An expression used as a condition in an if, while, or similar statement does not evaluate to a boolean type. Conditions must be explicitly boolean in Sharpy.",
            "x: int = 42\nif x:  # int is not bool\n    print(\"truthy\")",
            "Use an explicit comparison:\nif x != 0:\n    print(\"non-zero\")");

        // ── Semantic errors: Return and control flow (SHP0260-SHP0279) ──

        Add(dict, DiagnosticCodes.Semantic.BreakOutsideLoop, "Break outside loop", "Semantic",
            "A break statement was used outside of a for or while loop. Break can only be used inside loop bodies.",
            "def main():\n    break  # not inside a loop",
            "Move the break inside a loop, or use return to exit a function.");

        Add(dict, DiagnosticCodes.Semantic.ContinueOutsideLoop, "Continue outside loop", "Semantic",
            "A continue statement was used outside of a for or while loop. Continue can only be used inside loop bodies.",
            "def main():\n    continue  # not inside a loop",
            "Move the continue inside a loop.");

        Add(dict, DiagnosticCodes.Semantic.UnreachableCode, "Unreachable code", "Semantic",
            "Code was detected that can never be executed. This typically occurs after a return, raise, break, or continue statement. The compiler uses control flow graph analysis to detect these cases.",
            "def foo() -> int:\n    return 1\n    print(\"never runs\")  # unreachable",
            "Remove the unreachable code, or restructure the control flow so the code is reachable.");

        Add(dict, DiagnosticCodes.Semantic.NotAllPathsReturn, "Not all paths return a value", "Semantic",
            "A function declared with a non-void return type has execution paths that do not return a value. All possible paths through the function must end with a return statement.",
            "def abs_val(x: int) -> int:\n    if x >= 0:\n        return x\n    # missing return for x < 0",
            "Ensure all paths return a value:\ndef abs_val(x: int) -> int:\n    if x >= 0:\n        return x\n    return -x");

        // ── Semantic errors: Class and inheritance (SHP0280-SHP0299) ────

        Add(dict, DiagnosticCodes.Semantic.AbstractInstantiation, "Cannot instantiate abstract class", "Semantic",
            "An attempt was made to create an instance of an abstract class. Abstract classes can only be subclassed, not instantiated directly.",
            "abstract class Shape:\n    def area(self) -> float:\n        ...\n\ns = Shape()  # cannot instantiate",
            "Create a concrete subclass:\nclass Circle(Shape):\n    radius: float\n    def area(self) -> float:\n        return 3.14159 * self.radius * self.radius");

        Add(dict, DiagnosticCodes.Semantic.InvalidInheritance, "Invalid inheritance", "Semantic",
            "A class attempted to inherit from a type that cannot be used as a base class. For example, inheriting from a struct, enum, or sealed class.",
            "struct Point:\n    x: int\n    y: int\n\nclass Point3D(Point):  # cannot inherit from struct\n    z: int",
            "Use class inheritance only with other classes, or use composition instead.");

        Add(dict, DiagnosticCodes.Semantic.AccessViolation, "Access violation", "Semantic",
            "Code attempted to access a member that is not accessible from the current context. Members prefixed with _ are protected (accessible in subclasses), and members prefixed with __ are private (accessible only within the defining class).",
            "class Secret:\n    __key: str = \"hidden\"\n\ns = Secret()\nprint(s.__key)  # private access",
            "Use a public method to expose the value, or change the access level.");

        // ── Semantic errors: Import (SHP0300-SHP0319) ───────────────────

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

        // ── Validation errors (SHP0400-SHP0499) ────────────────────────

        Add(dict, DiagnosticCodes.Validation.MutableDefault, "Mutable default parameter", "Validation",
            "A function parameter has a mutable default value (list, dict, or set literal). In Python, mutable defaults are shared across calls, leading to subtle bugs. Sharpy prevents this pattern.",
            "def append_to(item: int, lst: list[int] = []) -> list[int]:\n    lst.append(item)\n    return lst",
            "Use None as the default and create the mutable object inside the function:\ndef append_to(item: int, lst: Optional[list[int]] = None) -> list[int]:\n    if lst is None:\n        lst = []\n    lst.append(item)\n    return lst");

        // ── Code generation errors (SHP0500-SHP0599) ───────────────────

        Add(dict, DiagnosticCodes.CodeGen.EmitError, "Code generation error", "CodeGen",
            "An error occurred during C# code generation. This is typically an internal compiler error that should be reported as a bug.",
            null,
            "Report this error at https://github.com/anthropics/sharpy/issues with the source file that triggered it.");

        Add(dict, DiagnosticCodes.CodeGen.UnsupportedFeature, "Unsupported feature in code generation", "CodeGen",
            "The code uses a language feature that the code generator does not yet support. The feature is valid Sharpy syntax but cannot be compiled to C# yet.",
            null,
            "Check the language specification for supported features, or file a feature request.");

        // ── Infrastructure errors (SHP0900-SHP0999) ────────────────────

        Add(dict, DiagnosticCodes.Infrastructure.CompilationFailed, "Compilation failed", "Infrastructure",
            "The overall compilation process failed. This is a summary error that accompanies more specific errors from earlier phases.",
            null,
            "Fix the errors reported in earlier phases (lexer, parser, semantic, or code generation).");

        Add(dict, DiagnosticCodes.Infrastructure.CompilationCancelled, "Compilation cancelled", "Infrastructure",
            "The compilation was cancelled, either by user request or by a timeout. No output was produced.",
            null,
            "Re-run the compilation. If it keeps timing out, check for very large files or circular dependencies.");

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
