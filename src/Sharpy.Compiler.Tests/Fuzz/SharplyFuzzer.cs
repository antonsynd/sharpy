using System;
using System.Text;

namespace Sharpy.Compiler.Tests.Fuzz;

/// <summary>
/// Generates random Sharpy-like source code for fuzz testing the lexer and parser.
/// Produces both valid-looking and intentionally malformed programs.
/// </summary>
public class SharplyFuzzer
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

    public SharplyFuzzer(int seed)
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
        sb.Append(prefix);

        switch (_random.Next(8))
        {
            case 0: // Assignment
                GenerateAssignment(sb, indent);
                sb.AppendLine();
                break;
            case 1: // Return
                sb.Append("return ");
                sb.AppendLine(GenerateExpression());
                break;
            case 2: // If
                sb.AppendLine($"if {GenerateExpression()}:");
                GenerateStatement(sb, indent + 1);
                break;
            case 3: // Pass
                sb.AppendLine("pass");
                break;
            case 4: // Expression statement
                sb.AppendLine(GenerateExpression());
                break;
            case 5: // For
                sb.AppendLine($"for {Identifiers[_random.Next(Identifiers.Length)]} in {GenerateExpression()}:");
                sb.Append(new string(' ', (indent + 1) * 4));
                sb.AppendLine("pass");
                break;
            case 6: // While
                sb.AppendLine($"while {GenerateExpression()}:");
                sb.Append(new string(' ', (indent + 1) * 4));
                sb.AppendLine("pass");
                break;
            default: // Break/continue (may be invalid outside loop)
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
}
