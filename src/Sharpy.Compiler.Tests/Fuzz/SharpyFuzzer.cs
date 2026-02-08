using System;
using System.Text;

namespace Sharpy.Compiler.Tests.Fuzz;

/// <summary>
/// Generates random Sharpy-like source code for fuzz testing the lexer and parser.
/// Produces both valid-looking and intentionally malformed programs.
/// </summary>
public class SharpyFuzzer
{
    private readonly System.Random _random;

    // Keywords and identifiers used in generation
    private static readonly string[] Keywords = {
        "def", "class", "if", "else", "elif", "while", "for", "in",
        "return", "break", "continue", "pass", "try", "except", "finally",
        "raise", "assert", "import", "from", "as", "True", "False", "None",
        "and", "or", "not", "is", "lambda", "yield", "async", "await",
        "match", "case", "struct", "interface", "enum", "with", "del",
        "to", "maybe", "super", "property", "event", "const", "auto", "type"
    };

    private static readonly string[] Identifiers = {
        "x", "y", "z", "foo", "bar", "baz", "self", "value", "result",
        "count", "items", "data", "name", "main", "init", "MyClass",
        "_private", "__dunder__", "CamelCase", "snake_case"
    };

    private static readonly string[] TypeAnnotations = {
        "int", "str", "float", "bool", "list", "dict", "tuple",
        "List[int]", "Dict[str, int]", "Optional[str]"
    };

    private static readonly string[] Operators = {
        "+", "-", "*", "/", "//", "%", "**", "==", "!=", "<", ">",
        "<=", ">=", "=", "+=", "-=", "*=", "/=", "->", ":", ":=",
        ".", ",", "(", ")", "[", "]", "{", "}", "??", "?.", "|>",
        "<<", ">>", "&", "|", "^", "~", "!", "@", "..."
    };

    private static readonly string[] StringLiterals = {
        "\"hello\"", "'world'", "\"\"\"triple\"\"\"", "'''triple'''",
        "f\"value={42}\"", "f'x={1+2}'", "r\"raw\\nstring\"",
        "\"escape\\n\\t\\r\"", "\"unicode\\u0041\"", "\"\""
    };

    public SharpyFuzzer(int seed)
    {
        _random = new System.Random(seed);
    }

    /// <summary>
    /// Generates a random program that looks like valid Sharpy code.
    /// May or may not actually be valid.
    /// </summary>
    public string GenerateValidLooking()
    {
        var sb = new StringBuilder();
        var numTopLevel = _random.Next(1, 5);

        for (int i = 0; i < numTopLevel; i++)
        {
            switch (_random.Next(4))
            {
                case 0:
                    GenerateFunction(sb, 0);
                    break;
                case 1:
                    GenerateClass(sb, 0);
                    break;
                case 2:
                    GenerateAssignment(sb, 0);
                    sb.AppendLine();
                    break;
                case 3:
                    GenerateImport(sb);
                    break;
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates completely random token sequences (may be syntactically invalid).
    /// </summary>
    public string GenerateRandomTokens()
    {
        var sb = new StringBuilder();
        var numTokens = _random.Next(1, 50);

        for (int i = 0; i < numTokens; i++)
        {
            switch (_random.Next(8))
            {
                case 0:
                    sb.Append(Keywords[_random.Next(Keywords.Length)]);
                    break;
                case 1:
                    sb.Append(Identifiers[_random.Next(Identifiers.Length)]);
                    break;
                case 2:
                    sb.Append(Operators[_random.Next(Operators.Length)]);
                    break;
                case 3:
                    sb.Append(_random.Next(0, 1000));
                    break;
                case 4:
                    sb.Append(_random.NextDouble().ToString("F2"));
                    break;
                case 5:
                    sb.Append(StringLiterals[_random.Next(StringLiterals.Length)]);
                    break;
                case 6:
                    sb.AppendLine();
                    break;
                case 7:
                    // Random indentation
                    sb.Append(new string(' ', _random.Next(0, 5) * 4));
                    break;
            }

            // Space between tokens (sometimes)
            if (_random.Next(3) > 0)
                sb.Append(' ');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates strings designed to stress the lexer: unterminated strings,
    /// unusual escape sequences, deep nesting, extreme indentation, etc.
    /// </summary>
    public string GenerateLexerStress()
    {
        switch (_random.Next(12))
        {
            case 0: // Unterminated string
                return "x = \"unterminated\n";
            case 1: // Deeply nested brackets
                return new string('(', 50) + "x" + new string(')', 50);
            case 2: // Very long identifier
                return new string('a', 10000) + " = 1\n";
            case 3: // Mixed indentation
                return "if True:\n\t    x = 1\n";
            case 4: // Empty f-string expression
                return "x = f\"{}\"\n";
            case 5: // Nested f-string
                return "x = f\"outer {f'inner {42}'}\"\n";
            case 6: // Many newlines
                return string.Concat(Enumerable.Repeat("\n", 1000));
            case 7: // Only whitespace
                return new string(' ', 500);
            case 8: // Backslash at end
                return "x = 1 \\\n";
            case 9: // Unicode characters
                return "x = \"\u00e9\u00e8\u00ea\"\n";
            case 10: // Very deep indentation
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("if True:");
                    for (int i = 1; i <= 20; i++)
                    {
                        sb.Append(new string(' ', i * 4));
                        sb.AppendLine("if True:");
                    }
                    sb.Append(new string(' ', 21 * 4));
                    sb.AppendLine("pass");
                    return sb.ToString();
                }
            default: // Random bytes (as string)
                {
                    var bytes = new byte[_random.Next(10, 200)];
                    _random.NextBytes(bytes);
                    // Filter to printable ASCII + whitespace to avoid encoding issues
                    var chars = bytes
                        .Select(b => (char)(b % 95 + 32)) // printable ASCII range
                        .ToArray();
                    return new string(chars);
                }
        }
    }

    /// <summary>
    /// Generates programs with syntax errors at various positions.
    /// </summary>
    public string GenerateWithSyntaxErrors()
    {
        switch (_random.Next(10))
        {
            case 0: // Missing colon after def
                return "def foo()\n    pass\n";
            case 1: // Missing colon after class
                return "class Foo\n    pass\n";
            case 2: // Missing closing paren
                return "def foo(x, y:\n    pass\n";
            case 3: // Unexpected dedent
                return "    x = 1\n";
            case 4: // Double colon
                return "def foo():: \n    pass\n";
            case 5: // Missing body after colon
                return "def foo():\n\ndef bar():\n    pass\n";
            case 6: // Invalid indentation
                return "if True:\n  x = 1\n"; // 2 spaces instead of 4
            case 7: // Keyword as identifier
                return "def = 42\n";
            case 8: // Multiple errors
                return "def (\n    x = \"unterminated\n    class\n";
            default: // Empty class body
                return "class Foo:\nclass Bar:\n    pass\n";
        }
    }

    private void GenerateFunction(StringBuilder sb, int indent)
    {
        var name = Identifiers[_random.Next(Identifiers.Length)];
        var prefix = new string(' ', indent * 4);
        sb.Append(prefix);
        sb.Append($"def {name}(");

        // Parameters
        var numParams = _random.Next(0, 4);
        for (int i = 0; i < numParams; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(Identifiers[_random.Next(Identifiers.Length)]);
            if (_random.Next(2) == 0)
            {
                sb.Append(": ");
                sb.Append(TypeAnnotations[_random.Next(TypeAnnotations.Length)]);
            }
        }

        sb.Append(')');

        // Return type
        if (_random.Next(2) == 0)
        {
            sb.Append(" -> ");
            sb.Append(TypeAnnotations[_random.Next(TypeAnnotations.Length)]);
        }

        sb.AppendLine(":");

        // Body
        var numStatements = _random.Next(1, 4);
        for (int i = 0; i < numStatements; i++)
        {
            GenerateStatement(sb, indent + 1);
        }
    }

    private void GenerateClass(StringBuilder sb, int indent)
    {
        var name = "Class" + _random.Next(100);
        var prefix = new string(' ', indent * 4);
        sb.Append(prefix);

        sb.Append($"class {name}");

        // Optional base class
        if (_random.Next(3) == 0)
        {
            sb.Append("(Base)");
        }

        sb.AppendLine(":");

        // Members
        var numMembers = _random.Next(1, 3);
        for (int i = 0; i < numMembers; i++)
        {
            if (_random.Next(2) == 0)
            {
                GenerateAssignment(sb, indent + 1);
                sb.AppendLine();
            }
            else
            {
                GenerateFunction(sb, indent + 1);
            }
        }
    }

    private void GenerateStatement(StringBuilder sb, int indent)
    {
        var prefix = new string(' ', indent * 4);

        switch (_random.Next(8))
        {
            case 0: // Assignment (GenerateAssignment handles its own prefix)
                GenerateAssignment(sb, indent);
                sb.AppendLine();
                break;
            case 1: // Return
                sb.Append(prefix);
                sb.Append("return ");
                sb.AppendLine(GenerateExpression());
                break;
            case 2: // If
                sb.Append(prefix);
                sb.AppendLine($"if {GenerateExpression()}:");
                GenerateStatement(sb, indent + 1);
                break;
            case 3: // Pass
                sb.Append(prefix);
                sb.AppendLine("pass");
                break;
            case 4: // Expression statement
                sb.Append(prefix);
                sb.AppendLine(GenerateExpression());
                break;
            case 5: // For
                sb.Append(prefix);
                sb.AppendLine($"for {Identifiers[_random.Next(Identifiers.Length)]} in {GenerateExpression()}:");
                sb.Append(new string(' ', (indent + 1) * 4));
                sb.AppendLine("pass");
                break;
            case 6: // While
                sb.Append(prefix);
                sb.AppendLine($"while {GenerateExpression()}:");
                sb.Append(new string(' ', (indent + 1) * 4));
                sb.AppendLine("pass");
                break;
            default: // Break/continue (may be invalid outside loop)
                sb.Append(prefix);
                sb.AppendLine(_random.Next(2) == 0 ? "break" : "continue");
                break;
        }
    }

    private void GenerateAssignment(StringBuilder sb, int indent)
    {
        var prefix = new string(' ', indent * 4);
        sb.Append(prefix);
        var name = Identifiers[_random.Next(Identifiers.Length)];
        sb.Append(name);

        // Optional type annotation
        if (_random.Next(2) == 0)
        {
            sb.Append(": ");
            sb.Append(TypeAnnotations[_random.Next(TypeAnnotations.Length)]);
        }

        sb.Append(" = ");
        sb.Append(GenerateExpression());
    }

    private string GenerateExpression()
    {
        switch (_random.Next(7))
        {
            case 0:
                return _random.Next(1000).ToString();
            case 1:
                return Identifiers[_random.Next(Identifiers.Length)];
            case 2:
                return $"\"{Identifiers[_random.Next(Identifiers.Length)]}\"";
            case 3:
                return _random.Next(2) == 0 ? "True" : "False";
            case 4:
                return "None";
            case 5:
                return $"{Identifiers[_random.Next(Identifiers.Length)]}({_random.Next(10)})";
            default:
                var left = _random.Next(100).ToString();
                var op = new[] { "+", "-", "*", "/", "==", "!=", "<", ">" }[_random.Next(8)];
                var right = _random.Next(100).ToString();
                return $"{left} {op} {right}";
        }
    }

    private void GenerateImport(StringBuilder sb)
    {
        if (_random.Next(2) == 0)
        {
            sb.AppendLine($"import {Identifiers[_random.Next(Identifiers.Length)]}");
        }
        else
        {
            sb.AppendLine($"from {Identifiers[_random.Next(Identifiers.Length)]} import {Identifiers[_random.Next(Identifiers.Length)]}");
        }
    }

    // --- New generators for Phase 2c ---

    /// <summary>
    /// Generates classes with inheritance, interfaces, and method overrides.
    /// Exercises inheritance resolution and type checking paths.
    /// </summary>
    public string GenerateClassHierarchy()
    {
        var sb = new StringBuilder();
        var classNames = new List<string>();

        // Generate a base class
        var baseName = "Base" + _random.Next(100);
        classNames.Add(baseName);
        sb.AppendLine($"class {baseName}:");

        // Add __init__ and some methods
        sb.AppendLine($"    def __init__(self):");
        sb.AppendLine($"        self.value: int = 0");
        sb.AppendLine();

        var numMethods = _random.Next(1, 3);
        for (int m = 0; m < numMethods; m++)
        {
            var methodName = $"method_{m}";
            sb.AppendLine($"    def {methodName}(self) -> int:");
            sb.AppendLine($"        return {_random.Next(100)}");
            sb.AppendLine();
        }

        // Generate 1-3 derived classes
        var numDerived = _random.Next(1, 4);
        for (int d = 0; d < numDerived; d++)
        {
            var derivedName = $"Derived{d}_{_random.Next(100)}";
            var parentIdx = _random.Next(classNames.Count);
            var parentName = classNames[parentIdx];
            classNames.Add(derivedName);

            sb.AppendLine($"class {derivedName}({parentName}):");

            // Override __init__
            sb.AppendLine($"    def __init__(self):");
            sb.AppendLine($"        super().__init__()");
            sb.AppendLine($"        self.extra: int = {_random.Next(100)}");
            sb.AppendLine();

            // Optionally override a method
            if (_random.Next(2) == 0 && numMethods > 0)
            {
                var overrideIdx = _random.Next(numMethods);
                sb.AppendLine($"    def method_{overrideIdx}(self) -> int:");
                sb.AppendLine($"        return {_random.Next(100)}");
                sb.AppendLine();
            }

            // Add a unique method
            sb.AppendLine($"    def unique_{d}(self) -> str:");
            sb.AppendLine($"        return \"{derivedName}\"");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates programs with generic type usage (list[int], dict[str, int],
    /// custom generic classes). Exercises GenericTypeInferenceService.
    /// </summary>
    public string GenerateGenericUsage()
    {
        var sb = new StringBuilder();

        // Generic container usage
        var containerTypes = new[]
        {
            ("list", "int"), ("list", "str"), ("list", "float"), ("list", "bool"),
            ("dict", "str, int"), ("dict", "str, str"), ("dict", "int, str"),
            ("set", "int"), ("set", "str"),
            ("tuple", "int, str"), ("tuple", "int, int, int")
        };

        var numDecls = _random.Next(2, 6);
        for (int i = 0; i < numDecls; i++)
        {
            var (container, typeArgs) = containerTypes[_random.Next(containerTypes.Length)];
            var varName = $"var_{i}";

            switch (container)
            {
                case "list":
                    sb.AppendLine($"{varName}: {container}[{typeArgs}] = [{GenerateTypedLiteral(typeArgs)}]");
                    break;
                case "dict":
                    {
                        var parts = typeArgs.Split(", ");
                        sb.AppendLine($"{varName}: {container}[{typeArgs}] = {{{GenerateTypedLiteral(parts[0])}: {GenerateTypedLiteral(parts[1])}}}");
                        break;
                    }
                case "set":
                    sb.AppendLine($"{varName}: {container}[{typeArgs}] = {{{GenerateTypedLiteral(typeArgs)}}}");
                    break;
                case "tuple":
                    {
                        var parts = typeArgs.Split(", ");
                        var elements = string.Join(", ", parts.Select(GenerateTypedLiteral));
                        sb.AppendLine($"{varName}: {container}[{typeArgs}] = ({elements},)");
                        break;
                    }
            }
        }

        // Function using generics
        sb.AppendLine();
        sb.AppendLine("def process_list(items: list[int]) -> int:");
        sb.AppendLine("    result: int = 0");
        sb.AppendLine("    for item in items:");
        sb.AppendLine("        result = result + item");
        sb.AppendLine("    return result");

        return sb.ToString();
    }

    /// <summary>
    /// Generates multi-file import scenarios.
    /// Returns a dictionary of filename -> source content for multi-file tests.
    /// Exercises ImportResolver and ModuleLoader.
    /// </summary>
    public Dictionary<string, string> GenerateImportGraph()
    {
        var files = new Dictionary<string, string>();
        var moduleNames = new List<string>();

        // Generate 2-4 library modules
        var numModules = _random.Next(2, 5);
        for (int m = 0; m < numModules; m++)
        {
            var moduleName = $"lib_{m}";
            moduleNames.Add(moduleName);
            var moduleSb = new StringBuilder();

            // Each module exports a class and/or function
            if (_random.Next(2) == 0)
            {
                var className = $"Lib{m}Class";
                moduleSb.AppendLine($"class {className}:");
                moduleSb.AppendLine($"    def __init__(self):");
                moduleSb.AppendLine($"        self.value: int = {_random.Next(100)}");
                moduleSb.AppendLine();
            }

            var funcName = $"lib_{m}_func";
            moduleSb.AppendLine($"def {funcName}(x: int) -> int:");
            moduleSb.AppendLine($"    return x + {_random.Next(100)}");

            files[$"{moduleName}.spy"] = moduleSb.ToString();
        }

        // Generate a main module that imports from the libraries
        var mainSb = new StringBuilder();
        foreach (var modName in moduleNames)
        {
            if (_random.Next(2) == 0)
            {
                mainSb.AppendLine($"import {modName}");
            }
            else
            {
                mainSb.AppendLine($"from {modName} import {modName}_func");
            }
        }
        mainSb.AppendLine();
        mainSb.AppendLine("def main():");
        mainSb.AppendLine($"    result: int = {_random.Next(100)}");
        mainSb.AppendLine("    print(result)");

        files["main.spy"] = mainSb.ToString();

        return files;
    }

    /// <summary>
    /// Generates functions and variables with type annotations including
    /// optional types T?, tuple types, and function types.
    /// Exercises TypeResolver.
    /// </summary>
    public string GenerateTypeAnnotations()
    {
        var sb = new StringBuilder();

        var simpleTypes = new[] { "int", "str", "float", "bool" };
        var collectionTypes = new[] { "list[int]", "list[str]", "dict[str, int]", "set[int]", "tuple[int, str]" };
        var allTypes = simpleTypes.Concat(collectionTypes).ToArray();

        // Variables with various type annotations
        var numVars = _random.Next(2, 6);
        for (int i = 0; i < numVars; i++)
        {
            var varType = allTypes[_random.Next(allTypes.Length)];
            var varName = $"var_{i}";

            // Sometimes make it optional with ?
            var isOptional = _random.Next(4) == 0;
            var annotation = isOptional ? $"{varType}?" : varType;

            sb.Append($"{varName}: {annotation} = ");

            if (isOptional && _random.Next(2) == 0)
            {
                sb.AppendLine("None");
            }
            else
            {
                sb.AppendLine(GenerateTypedLiteral(varType.Split('[')[0]));
            }
        }

        sb.AppendLine();

        // Functions with typed parameters and return types
        var numFuncs = _random.Next(1, 4);
        for (int f = 0; f < numFuncs; f++)
        {
            var returnType = simpleTypes[_random.Next(simpleTypes.Length)];
            sb.Append($"def func_{f}(");

            var numParams = _random.Next(1, 4);
            for (int p = 0; p < numParams; p++)
            {
                if (p > 0)
                    sb.Append(", ");
                var paramType = simpleTypes[_random.Next(simpleTypes.Length)];
                sb.Append($"p{p}: {paramType}");

                // Sometimes add a default value
                if (_random.Next(3) == 0)
                {
                    sb.Append($" = {GenerateTypedLiteral(paramType)}");
                }
            }

            sb.AppendLine($") -> {returnType}:");
            sb.AppendLine($"    return {GenerateTypedLiteral(returnType)}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Takes a valid Sharpy program and introduces random mutations.
    /// This tests error recovery on near-valid programs (the most common real-world scenario).
    /// </summary>
    public string MutateProgram(string validProgram)
    {
        var sb = new StringBuilder(validProgram);
        var numMutations = _random.Next(1, 4);

        for (int i = 0; i < numMutations; i++)
        {
            if (sb.Length == 0)
                break;
            switch (_random.Next(5))
            {
                case 0: // Delete a random character
                    sb.Remove(_random.Next(sb.Length), 1);
                    break;
                case 1: // Insert a random character
                    sb.Insert(_random.Next(sb.Length), (char)_random.Next(32, 127));
                    break;
                case 2: // Replace a random character
                    sb[_random.Next(sb.Length)] = (char)_random.Next(32, 127);
                    break;
                case 3: // Duplicate a random line
                    var lines = sb.ToString().Split('\n');
                    if (lines.Length > 0)
                    {
                        var idx = _random.Next(lines.Length);
                        var lineList = lines.ToList();
                        lineList.Insert(idx, lines[idx]);
                        sb.Clear();
                        sb.Append(string.Join('\n', lineList));
                    }
                    break;
                case 4: // Delete a random line
                    var lines2 = sb.ToString().Split('\n').ToList();
                    if (lines2.Count > 1)
                    {
                        lines2.RemoveAt(_random.Next(lines2.Count));
                        sb.Clear();
                        sb.Append(string.Join('\n', lines2));
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    private string GenerateTypedLiteral(string typeName)
    {
        return typeName switch
        {
            "int" => _random.Next(-100, 1000).ToString(),
            "str" => $"\"{Identifiers[_random.Next(Identifiers.Length)]}\"",
            "float" => $"{_random.NextDouble():F2}",
            "bool" => _random.Next(2) == 0 ? "True" : "False",
            _ => _random.Next(100).ToString()
        };
    }
}
