using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Maps Sharpy types to C# types using Roslyn syntax nodes
/// </summary>
public class TypeMapper
{
    private readonly CodeGenContext _context;

    // Built-in type mappings for v0.5
    private static readonly Dictionary<string, string> _builtinTypeMap = new()
    {
        // Primitive types - direct mapping
        { "int", "int" },
        { "long", "long" },
        { "float", "float" },
        { "double", "double" },
        { "bool", "bool" },
        { "byte", "byte" },
        { "sbyte", "sbyte" },
        { "short", "short" },
        { "ushort", "ushort" },
        { "uint", "uint" },
        { "ulong", "ulong" },
        { "char", "char" },
        { "object", "object" },

        // Sharpy runtime types
        { "str", "Sharpy.Core.Str" },
        { "list", "Sharpy.Core.List" },
        { "dict", "Sharpy.Core.Dict" },
        { "set", "Sharpy.Core.Set" },
        { "tuple", "System.ValueTuple" },

        // Common .NET types
        { "string", "string" },  // Allow string alias
        { "void", "void" },
    };

    public TypeMapper(CodeGenContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Maps a Sharpy type annotation to a C# TypeSyntax
    /// </summary>
    public TypeSyntax MapType(TypeAnnotation? type)
    {
        // Default to object if no type specified
        if (type == null)
        {
            return PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        // Get base type name
        var baseTypeName = GetMappedTypeName(type.Name);

        // Handle generic type arguments
        if (type.TypeArguments.Count > 0)
        {
            var typeArgs = type.TypeArguments
                .Select(MapType)
                .ToArray();

            var result = GenericName(Identifier(baseTypeName))
                .WithTypeArgumentList(
                    TypeArgumentList(SeparatedList(typeArgs)));

            // Handle nullable generic types
            return type.IsNullable
                ? NullableType(result)
                : result;
        }

        // Handle nullable non-generic types
        var typeSyntax = ParseTypeName(baseTypeName);
        return type.IsNullable
            ? NullableType(typeSyntax)
            : typeSyntax;
    }

    /// <summary>
    /// Maps a Sharpy type name to a C# type name
    /// </summary>
    private string GetMappedTypeName(string sharpyTypeName)
    {
        // Check if it's a built-in type
        if (_builtinTypeMap.TryGetValue(sharpyTypeName, out var csharpType))
        {
            return csharpType;
        }

        // Check if it's a known builtin from the registry
        if (_context.IsBuiltinType(sharpyTypeName))
        {
            // Builtin types from registry should be in Sharpy namespace
            return $"Sharpy.{sharpyTypeName}";
        }

        // User-defined types keep their original name
        // Name mangling will be applied separately if needed
        return sharpyTypeName;
    }

    /// <summary>
    /// Maps a FunctionType to a C# delegate or Func/Action type
    /// </summary>
    public TypeSyntax MapFunctionType(Parser.Ast.FunctionType funcType)
    {
        var paramTypes = funcType.ParameterTypes
            .Select(MapType)
            .ToArray();

        var returnType = MapType(funcType.ReturnType);

        // If return type is void, use Action<T1, T2, ...>
        if (IsVoidType(funcType.ReturnType))
        {
            if (paramTypes.Length == 0)
            {
                return ParseTypeName("System.Action");
            }

            return GenericName("System.Action")
                .WithTypeArgumentList(
                    TypeArgumentList(SeparatedList(paramTypes)));
        }

        // Otherwise use Func<T1, T2, ..., TResult>
        var allTypes = paramTypes.Append(returnType).ToArray();

        if (allTypes.Length == 1)
        {
            return GenericName("System.Func")
                .WithTypeArgumentList(
                    TypeArgumentList(SingletonSeparatedList(returnType)));
        }

        return GenericName("System.Func")
            .WithTypeArgumentList(
                TypeArgumentList(SeparatedList(allTypes)));
    }

    /// <summary>
    /// Maps a TupleType to a C# ValueTuple type
    /// </summary>
    public TypeSyntax MapTupleType(Parser.Ast.TupleType tupleType)
    {
        if (tupleType.ElementTypes.Count == 0)
        {
            // Empty tuple
            return ParseTypeName("System.ValueTuple");
        }

        var elementTypes = tupleType.ElementTypes
            .Select(MapType)
            .ToArray();

        // For single element, it's just the type (not a tuple)
        if (elementTypes.Length == 1)
        {
            return elementTypes[0];
        }

        // Use ValueTuple<T1, T2, ...>
        return GenericName("System.ValueTuple")
            .WithTypeArgumentList(
                TypeArgumentList(SeparatedList(elementTypes)));
    }

    /// <summary>
    /// Creates a Sharpy collection type with element type
    /// </summary>
    public TypeSyntax CreateCollectionType(string collectionName, TypeSyntax elementType)
    {
        var baseType = GetMappedTypeName(collectionName);

        return GenericName(Identifier(baseType))
            .WithTypeArgumentList(
                TypeArgumentList(SingletonSeparatedList(elementType)));
    }

    /// <summary>
    /// Creates a Sharpy.Dict type with key and value types
    /// </summary>
    public TypeSyntax CreateDictType(TypeSyntax keyType, TypeSyntax valueType)
    {
        return GenericName("Sharpy.Dict")
            .WithTypeArgumentList(
                TypeArgumentList(SeparatedList(new[] { keyType, valueType })));
    }

    /// <summary>
    /// Checks if a type is void
    /// </summary>
    private bool IsVoidType(TypeAnnotation? type)
    {
        return type?.Name == "void" || type?.Name == "None";
    }

    /// <summary>
    /// Infers the element type from a collection of expressions
    /// </summary>
    public TypeSyntax InferElementType(IEnumerable<Expression> expressions)
    {
        // For v0.5, we'll use a simple heuristic:
        // - If all expressions are the same literal type, use that
        // - Otherwise, use object

        var exprs = expressions.ToList();
        if (exprs.Count == 0)
        {
            return PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        var firstExpr = exprs[0];
        var inferredType = InferExpressionType(firstExpr);

        // Check if all expressions have the same type
        if (exprs.All(e => TypesMatch(InferExpressionType(e), inferredType)))
        {
            return MapTypeFromInferredType(inferredType);
        }

        // Fall back to object
        return PredefinedType(Token(SyntaxKind.ObjectKeyword));
    }

    /// <summary>
    /// Infers the type of an expression (simple version for literals)
    /// </summary>
    private string InferExpressionType(Expression expr)
    {
        return expr switch
        {
            IntegerLiteral => "int",
            FloatLiteral floatLit => floatLit.Suffix?.ToLower() switch
            {
                "f" => "float",
                "m" => "decimal",
                _ => "double"
            },
            StringLiteral => "string",
            BooleanLiteral => "bool",
            NoneLiteral => "object",
            ListLiteral => "list",
            DictLiteral => "dict",
            SetLiteral => "set",
            TupleLiteral => "tuple",
            _ => "object"
        };
    }

    /// <summary>
    /// Checks if two inferred type names match
    /// </summary>
    private bool TypesMatch(string type1, string type2)
    {
        return type1 == type2;
    }

    /// <summary>
    /// Maps an inferred type name to a TypeSyntax
    /// </summary>
    private TypeSyntax MapTypeFromInferredType(string typeName)
    {
        return typeName switch
        {
            "int" => PredefinedType(Token(SyntaxKind.IntKeyword)),
            "long" => PredefinedType(Token(SyntaxKind.LongKeyword)),
            "float" => PredefinedType(Token(SyntaxKind.FloatKeyword)),
            "double" => PredefinedType(Token(SyntaxKind.DoubleKeyword)),
            "decimal" => PredefinedType(Token(SyntaxKind.DecimalKeyword)),
            "string" => PredefinedType(Token(SyntaxKind.StringKeyword)),
            "bool" => PredefinedType(Token(SyntaxKind.BoolKeyword)),
            "object" => PredefinedType(Token(SyntaxKind.ObjectKeyword)),
            _ => ParseTypeName(GetMappedTypeName(typeName))
        };
    }

    /// <summary>
    /// Creates a nullable version of a type
    /// </summary>
    public TypeSyntax MakeNullable(TypeSyntax type)
    {
        return NullableType(type);
    }

    /// <summary>
    /// Creates an array type
    /// </summary>
    public TypeSyntax MakeArrayType(TypeSyntax elementType, int rank = 1)
    {
        var rankSpecifier = ArrayRankSpecifier(
            SeparatedList<ExpressionSyntax>(
                Enumerable.Repeat(OmittedArraySizeExpression(), rank)));

        return ArrayType(elementType)
            .AddRankSpecifiers(rankSpecifier);
    }
}
