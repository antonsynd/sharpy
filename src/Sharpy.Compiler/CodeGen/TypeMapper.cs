using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Maps Sharpy types to C# types using Roslyn syntax nodes
/// </summary>
internal class TypeMapper
{
    private readonly CodeGenContext _context;

    // Built-in type mappings, populated from PrimitiveCatalog at startup
    private static readonly Dictionary<string, string> _builtinTypeMap;

    static TypeMapper()
    {
        _builtinTypeMap = new Dictionary<string, string>();

        // Add all primitives from PrimitiveCatalog
        foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
        {
            _builtinTypeMap.TryAdd(name, info.CSharpName);
        }

        // Add non-primitive type mappings (collections, etc.)
        // v0.1.x uses .NET types directly per phases.md (Sharpy.Core wrappers in v0.2.x+)
        _builtinTypeMap["list"] = "System.Collections.Generic.List";
        _builtinTypeMap["dict"] = "System.Collections.Generic.Dictionary";
        _builtinTypeMap["set"] = "System.Collections.Generic.HashSet";
        _builtinTypeMap["tuple"] = "System.ValueTuple";
    }

    public TypeMapper(CodeGenContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Maps a SemanticType to a C# TypeSyntax
    /// </summary>
    public TypeSyntax MapSemanticType(SemanticType type)
    {
        return type switch
        {
            null or UnknownType => PredefinedType(Token(SyntaxKind.ObjectKeyword)),
            VoidType => PredefinedType(Token(SyntaxKind.VoidKeyword)),

            // Handle builtin types by matching against singleton instances
            BuiltinType builtin when type == SemanticType.Int => PredefinedType(Token(SyntaxKind.IntKeyword)),
            BuiltinType builtin when type == SemanticType.Long => PredefinedType(Token(SyntaxKind.LongKeyword)),
            BuiltinType builtin when type == SemanticType.Bool => PredefinedType(Token(SyntaxKind.BoolKeyword)),
            BuiltinType builtin when type == SemanticType.Str => PredefinedType(Token(SyntaxKind.StringKeyword)),
            BuiltinType builtin when type == SemanticType.Float => PredefinedType(Token(SyntaxKind.DoubleKeyword)), // Sharpy float = C# double
            BuiltinType builtin when type == SemanticType.Double => PredefinedType(Token(SyntaxKind.DoubleKeyword)),
            BuiltinType builtin when type == SemanticType.Float32 => PredefinedType(Token(SyntaxKind.FloatKeyword)),
            BuiltinType builtin => ParseTypeName(GetMappedTypeName(builtin.Name)),

            // Handle generic types
            GenericType generic => MapGenericSemanticType(generic),

            // Handle optional types (T? → Optional<T>)
            OptionalType opt => GenericName(Identifier("Optional"))
                .WithTypeArgumentList(
                    TypeArgumentList(SingletonSeparatedList(MapSemanticType(opt.UnderlyingType)))),

            // Handle result types (T !E → Result<T, E>)
            ResultType res => GenericName(Identifier("Result"))
                .WithTypeArgumentList(
                    TypeArgumentList(SeparatedList(new[]
                    {
                        MapSemanticType(res.OkType),
                        MapSemanticType(res.ErrorType)
                    }))),

            // Handle nullable types
            NullableType nullable => NullableType(MapSemanticType(nullable.UnderlyingType)),

            // Handle user-defined types
            UserDefinedType udt => ParseTypeName(GetMappedTypeNameFromSymbol(udt)),

            // Handle type parameters (e.g., T in class Box[T])
            TypeParameterType typeParam => IdentifierName(typeParam.Name),

            // Handle function types
            Semantic.FunctionType funcType => MapSemanticFunctionType(funcType),

            // Handle tuple types
            Semantic.TupleType tupleType => MapSemanticTupleType(tupleType),

            // Handle module types (error case - modules can't be used as types directly)
            ModuleType mt => throw new InvalidOperationException(
                $"Cannot use module '{mt.Symbol.Name}' as a type. Did you mean to access a type from the module?"),

            // Handle generic function types (instantiated generic functions)
            GenericFunctionType gft => MapSemanticFunctionType(new Semantic.FunctionType
            {
                ParameterTypes = gft.FunctionSymbol.Parameters.Select(p => p.Type).ToList(),
                ReturnType = gft.FunctionSymbol.ReturnType
            }),

            // Handle union types (v0.2.x placeholder)
            UnionType ut => throw new NotSupportedException(
                $"Union types are not yet supported in code generation. Type: {ut.Name}"),

            // Handle task types (v0.2.x placeholder)
            TaskType tt => tt.ResultType == null
                ? ParseTypeName("System.Threading.Tasks.Task")
                : GenericName(Identifier("System.Threading.Tasks.Task"))
                    .WithTypeArgumentList(
                        TypeArgumentList(SingletonSeparatedList(MapSemanticType(tt.ResultType)))),

            // Exhaustive check - if a new SemanticType is added, this will fail at runtime
            _ => throw new InvalidOperationException(
                $"Unhandled SemanticType in MapSemanticType: {type.GetType().Name}")
        };
    }

    private TypeSyntax MapGenericSemanticType(GenericType generic)
    {
        var baseTypeName = GetMappedTypeName(generic.Name);
        var typeArgs = generic.TypeArguments
            .Select(MapSemanticType)
            .ToArray();

        return GenericName(Identifier(baseTypeName))
            .WithTypeArgumentList(
                TypeArgumentList(SeparatedList(typeArgs)));
    }

    private TypeSyntax MapSemanticFunctionType(Semantic.FunctionType funcType)
    {
        var paramTypes = funcType.ParameterTypes
            .Select(MapSemanticType)
            .ToArray();

        var returnType = MapSemanticType(funcType.ReturnType);

        // If return type is void, use Action<T1, T2, ...>
        if (funcType.ReturnType is VoidType)
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

    private TypeSyntax MapSemanticTupleType(Semantic.TupleType tupleType)
    {
        if (tupleType.ElementTypes.Count == 0)
        {
            return ParseTypeName("System.ValueTuple");
        }

        var elementTypes = tupleType.ElementTypes
            .Select(MapSemanticType)
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
    /// Maps a Sharpy type annotation to a C# TypeSyntax.
    /// Note: T? (IsOptional) currently maps to C# T? for backward compatibility
    /// with existing null-based tests. T !E (IsResult) maps to Result&lt;T, E&gt;.
    /// Use MapSemanticType for the canonical Optional&lt;T&gt; mapping.
    /// </summary>
    public TypeSyntax MapType(TypeAnnotation? type)
    {
        // Default to object if no type specified
        if (type == null)
        {
            return PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        // Handle T !E → Result<T, E>
        if (type.IsResult)
        {
            var okType = MapType(new TypeAnnotation { Name = type.Name, TypeArguments = type.TypeArguments });
            var errType = MapType(type.ErrorType);
            return GenericName(Identifier("Result"))
                .WithTypeArgumentList(
                    TypeArgumentList(SeparatedList(new[] { okType, errType })));
        }

        // Check if this is a type alias and expand it
        var aliasSymbol = _context.SymbolTable.LookupTypeAlias(type.Name);
        if (aliasSymbol != null)
        {
            // Expand the alias
            if (aliasSymbol.TypeAnnotation != null)
            {
                // For type annotations, recursively map the underlying type
                var expandedType = MapType(aliasSymbol.TypeAnnotation);
                // Apply nullable modifier from usage site
                return (type.IsOptional || type.IsCSharpNullable) ? NullableType(expandedType) : expandedType;
            }
            else if (aliasSymbol.FunctionType != null)
            {
                // For function types, map to C# delegate/Func/Action
                var expandedType = MapFunctionType(aliasSymbol.FunctionType);
                // Function types typically shouldn't be nullable, but handle it anyway
                return (type.IsOptional || type.IsCSharpNullable) ? NullableType(expandedType) : expandedType;
            }
        }

        // Get base type name
        var baseTypeName = GetMappedTypeName(type.Name);

        // Handle generic type arguments
        if (type.TypeArguments.Length > 0)
        {
            var typeArgs = type.TypeArguments
                .Select(MapType)
                .ToArray();

            var result = GenericName(Identifier(baseTypeName))
                .WithTypeArgumentList(
                    TypeArgumentList(SeparatedList(typeArgs)));

            // Handle nullable generic types
            return (type.IsOptional || type.IsCSharpNullable)
                ? NullableType(result)
                : result;
        }

        // Handle nullable non-generic types
        var typeSyntax = ParseTypeName(baseTypeName);
        return (type.IsOptional || type.IsCSharpNullable)
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

        // Check if it's a user-defined type from another file/module (takes priority over builtins)
        var typeSymbol = _context.SymbolTable.LookupType(sharpyTypeName);
        if (typeSymbol != null)
        {
            // Check if type is from a different file (cross-file reference in same project)
            if (!string.IsNullOrEmpty(typeSymbol.DefiningFilePath) &&
                !string.IsNullOrEmpty(_context.SourceFilePath) &&
                !string.Equals(typeSymbol.DefiningFilePath, _context.SourceFilePath, StringComparison.OrdinalIgnoreCase))
            {
                // Type from another file - use fully qualified name
                return GetFullyQualifiedTypeName(typeSymbol, sharpyTypeName);
            }

            // Check if type is from an external module (imported)
            if (!string.IsNullOrEmpty(typeSymbol.DefiningModule))
            {
                // Type from another module - use fully qualified name
                return GetFullyQualifiedTypeName(typeSymbol, sharpyTypeName);
            }

            // Type is in current scope (user-defined in current file) - use simple name
            // This takes priority over builtin registry to allow shadowing
            return NameMangler.ToPascalCase(sharpyTypeName);
        }

        // Check if it's a known builtin from the registry (exception types, etc.)
        var builtinTypeSymbol = _context.Builtins.GetType(sharpyTypeName);
        if (builtinTypeSymbol != null)
        {
            if (builtinTypeSymbol.ClrType != null)
            {
                var ns = builtinTypeSymbol.ClrType.Namespace ?? string.Empty;
                // System namespace types are always available in C# without qualification
                if (ns == "System" || ns.StartsWith("System."))
                {
                    return builtinTypeSymbol.ClrType.Name;
                }
                // Sharpy types need global:: qualification
                return $"global::{builtinTypeSymbol.ClrType.FullName}";
            }
            return sharpyTypeName;
        }

        // User-defined types in current module keep their PascalCase name
        return NameMangler.ToPascalCase(sharpyTypeName);
    }

    /// <summary>
    /// Gets the fully qualified C# type name for a type from another file/module.
    /// Types are nested inside the module class, so cross-file references use
    /// Namespace.ModuleClass.TypeName.
    /// </summary>
    private string GetFullyQualifiedTypeName(TypeSymbol typeSymbol, string sharpyTypeName)
    {
        string moduleNamespace;

        if (!string.IsNullOrEmpty(typeSymbol.DefiningModule))
        {
            // Use DefiningModule (e.g., "animal" from import)
            moduleNamespace = ConvertModuleToNamespace(typeSymbol.DefiningModule);
        }
        else if (!string.IsNullOrEmpty(typeSymbol.DefiningFilePath))
        {
            // Derive module namespace from file path
            moduleNamespace = GetModuleNameFromFilePath(typeSymbol.DefiningFilePath);
        }
        else
        {
            // Fallback - shouldn't happen
            return NameMangler.ToPascalCase(sharpyTypeName);
        }

        var typeName = NameMangler.ToPascalCase(sharpyTypeName);

        // Check for collision: when the file/directory name (PascalCase) matches the type name,
        // the type IS the module class (collision merge), not nested inside it.
        // e.g., animal.spy with class Animal → type is Sharpy.Test.Animal, not Sharpy.Test.Animal.Animal
        var lastSegment = moduleNamespace.Contains('.')
            ? moduleNamespace.Split('.').Last()
            : moduleNamespace;

        if (string.Equals(lastSegment, typeName, StringComparison.Ordinal))
        {
            // Type IS the module class — module path is the type path
            if (!string.IsNullOrEmpty(_context.ProjectNamespace))
            {
                return $"{_context.ProjectNamespace}.{moduleNamespace}";
            }
            return moduleNamespace;
        }

        // Type is nested inside the module class
        if (!string.IsNullOrEmpty(_context.ProjectNamespace))
        {
            return $"{_context.ProjectNamespace}.{moduleNamespace}.{typeName}";
        }
        return $"{moduleNamespace}.{typeName}";
    }

    /// <summary>
    /// Derives a module namespace from a file path, computing the full package path
    /// relative to the project root.
    /// E.g., for project root "/temp" and file "/temp/mypackage/submodule.spy" -> "Mypackage.Submodule"
    /// </summary>
    private string GetModuleNameFromFilePath(string filePath)
    {
        // If we have a project root, compute relative path for proper namespace
        if (!string.IsNullOrEmpty(_context.ProjectRootPath))
        {
            var relativePath = Path.GetRelativePath(_context.ProjectRootPath, filePath);
            var relativeDir = Path.GetDirectoryName(relativePath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            var namespaceParts = new List<string>();

            // Add directory parts (package hierarchy)
            if (!string.IsNullOrEmpty(relativeDir) && relativeDir != ".")
            {
                var dirParts = relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var part in dirParts)
                {
                    if (!string.IsNullOrEmpty(part) && part != ".")
                    {
                        namespaceParts.Add(SimpleToPascalCase(part));
                    }
                }
            }

            // Add file name part (skip __init__ as it represents the package itself)
            if (!string.Equals(fileName, DunderNames.Init, StringComparison.OrdinalIgnoreCase))
            {
                namespaceParts.Add(SimpleToPascalCase(fileName));
            }

            if (namespaceParts.Count > 0)
            {
                return string.Join(".", namespaceParts);
            }
        }

        // Fallback: just use file name
        var fallbackFileName = Path.GetFileNameWithoutExtension(filePath);
        return SimpleToPascalCase(fallbackFileName);
    }

    /// <summary>
    /// Maps a UserDefinedType to its fully qualified C# name, using the Symbol if available.
    /// </summary>
    private string GetMappedTypeNameFromSymbol(UserDefinedType udt)
    {
        if (udt.Symbol != null)
        {
            // Check if type is from a different file (cross-file reference)
            if (!string.IsNullOrEmpty(udt.Symbol.DefiningFilePath) &&
                !string.IsNullOrEmpty(_context.SourceFilePath) &&
                !string.Equals(udt.Symbol.DefiningFilePath, _context.SourceFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return GetFullyQualifiedTypeName(udt.Symbol, udt.Name);
            }

            // Check if type is from an external module (imported)
            if (!string.IsNullOrEmpty(udt.Symbol.DefiningModule))
            {
                return GetFullyQualifiedTypeName(udt.Symbol, udt.Name);
            }
        }

        // Fall back to name-based lookup
        return GetMappedTypeName(udt.Name);
    }

    /// <summary>
    /// Converts a module path (e.g., "animal" or "lib.animal") to a C# namespace segment.
    /// </summary>
    private static string ConvertModuleToNamespace(string modulePath)
    {
        var parts = modulePath.Split('.');
        return string.Join(".", parts.Select(p => SimpleToPascalCase(p)));
    }

    private static string SimpleToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        // Use NameMangler for proper snake_case to PascalCase conversion
        return NameMangler.ToPascalCase(name);
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
        if (tupleType.ElementTypes.IsEmpty)
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
    /// Creates a System.Collections.Generic.Dictionary type with key and value types
    /// v0.1.x uses .NET types directly per phases.md
    /// </summary>
    public TypeSyntax CreateDictType(TypeSyntax keyType, TypeSyntax valueType)
    {
        return GenericName("System.Collections.Generic.Dictionary")
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
        // For v0.1, we'll use a simple heuristic:
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
    /// Infers and maps the type from an expression to a C# TypeSyntax
    /// </summary>
    public TypeSyntax InferTypeFromExpression(Expression expr)
    {
        var inferredType = InferExpressionType(expr);
        return MapTypeFromInferredType(inferredType);
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

    /// <summary>
    /// Maps an expression (typically an identifier used as a type) to a TypeSyntax.
    /// Used for generic type instantiation like Box[int](42) where "int" is parsed as an expression.
    /// </summary>
    public TypeSyntax MapTypeFromExpression(Expression expr)
    {
        if (expr is Identifier id)
        {
            // Create a type annotation from the identifier and map it
            var annotation = new TypeAnnotation { Name = id.Name };
            return MapType(annotation);
        }

        // Handle nested generic types (e.g., Box[int] in Container[Box[int]])
        if (expr is IndexAccess indexAccess && indexAccess.Object is Identifier nestedTypeName)
        {
            var typeArgs = MapTypeArgumentsFromExpression(indexAccess.Index);
            return GenericName(NameMangler.ToPascalCase(nestedTypeName.Name))
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgs)));
        }

        // For more complex expressions, fall back to object
        return PredefinedType(Token(SyntaxKind.ObjectKeyword));
    }

    /// <summary>
    /// Maps an expression containing one or more type arguments to a list of TypeSyntax.
    /// Handles both single type arguments (int) and multiple type arguments (int, str as TupleLiteral).
    /// Used for generic instantiation like Box[int] or Pair[int, str].
    /// </summary>
    public TypeSyntax[] MapTypeArgumentsFromExpression(Expression expr)
    {
        // Handle multiple type arguments: Pair[int, str] parses as TupleLiteral
        if (expr is TupleLiteral tuple)
        {
            return tuple.Elements.Select(MapTypeFromExpression).ToArray();
        }

        // Handle single type argument
        return new[] { MapTypeFromExpression(expr) };
    }
}
