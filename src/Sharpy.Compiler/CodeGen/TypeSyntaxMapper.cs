using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Maps Sharpy types to C# types using Roslyn syntax nodes
/// </summary>
internal class TypeSyntaxMapper
{
    private readonly CodeGenContext _context;

    // Built-in type mappings, populated from PrimitiveCatalog at startup
    private static readonly Dictionary<string, string> _builtinTypeMap;

    static TypeSyntaxMapper()
    {
        _builtinTypeMap = new Dictionary<string, string>();

        // Add all primitives from PrimitiveCatalog
        foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
        {
            _builtinTypeMap.TryAdd(name, info.CSharpName);
        }

        // Add non-primitive type mappings (collections, etc.)
        _builtinTypeMap[BuiltinNames.List] = CSharpTypeNames.SharpyList;
        _builtinTypeMap[BuiltinNames.Dict] = CSharpTypeNames.SharpyDict;
        _builtinTypeMap[BuiltinNames.Set] = CSharpTypeNames.SharpySet;
        _builtinTypeMap[BuiltinNames.DefaultDict] = CSharpTypeNames.SharpyDefaultDict;
        _builtinTypeMap["DefaultDict"] = CSharpTypeNames.SharpyDefaultDict;
        _builtinTypeMap[BuiltinNames.FrozenDict] = CSharpTypeNames.SharpyFrozenDict;
        _builtinTypeMap["FrozenDict"] = CSharpTypeNames.SharpyFrozenDict;
        _builtinTypeMap[BuiltinNames.Bytes] = CSharpTypeNames.SharpyBytes;
        _builtinTypeMap[BuiltinNames.Template] = CSharpTypeNames.SharpyTemplate;
        _builtinTypeMap[BuiltinNames.Tuple] = "System.ValueTuple";
    }

    public TypeSyntaxMapper(CodeGenContext context)
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
            BuiltinType builtin when type == SemanticType.Decimal => PredefinedType(Token(SyntaxKind.DecimalKeyword)),
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

            // Template emits as Sharpy.Template
            TemplateType => ParseTypeName("global::" + CSharpTypeNames.SharpyTemplate),

            // LiteralString emits as string (compile-time only distinction)
            LiteralStringType => PredefinedType(Token(SyntaxKind.StringKeyword)),

            // Handle Self type — emit as the concrete declaring class type
            SelfType selfType when selfType.DeclaringType != null =>
                MapSelfType(selfType),

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
                ? RoslynEmitter.MakeGlobalQualifiedName("System", "Threading", "Tasks", "Task")
                : QualifiedGenericName("System.Threading.Tasks.Task", globalQualified: true, MapSemanticType(tt.ResultType)),

            // Exhaustive check - if a new SemanticType is added, this will fail at runtime
            _ => throw new InvalidOperationException(
                $"Unhandled SemanticType in MapSemanticType: {type.GetType().Name}")
        };
    }

    private TypeSyntax MapGenericSemanticType(GenericType generic)
    {
        // Array types map to C# T[] (not a generic type)
        if (generic.Name == BuiltinNames.Array && generic.TypeArguments.Count == 1)
        {
            var elementType = MapSemanticType(generic.TypeArguments[0]);
            return MakeArrayType(elementType);
        }

        // Module-qualified / cross-file generic types (e.g. difflib.SequenceMatcher[str],
        // geometry.Box[int]) must be fully qualified via their resolved definition symbol —
        // the bare/dotted generic.Name alone does not carry enough information for the name
        // lookup. Mirrors the non-generic GetMappedTypeNameFromSymbol path (#17/#881).
        var baseTypeName = generic.GenericDefinition is { } genDef && RequiresQualifiedName(genDef)
            ? GetMappedTypeNameFromSymbol(new UserDefinedType { Name = generic.Name, Symbol = genDef })
            : GetMappedTypeName(generic.Name);
        var typeArgs = generic.TypeArguments
            .Select(MapSemanticType)
            .ToArray();

        return QualifiedGenericName(baseTypeName, typeArgs);
    }

    /// <summary>
    /// Returns true when a type symbol must be emitted with a fully-qualified C# name because it
    /// belongs to an imported module or another file in the project. Mirrors the cross-file /
    /// cross-module conditions in <see cref="GetMappedTypeNameFromSymbol"/>.
    /// </summary>
    private bool RequiresQualifiedName(TypeSymbol symbol)
    {
        if (!string.IsNullOrEmpty(symbol.DefiningModule))
            return true;

        return !string.IsNullOrEmpty(symbol.DefiningFilePath)
            && !string.IsNullOrEmpty(_context.SourceFilePath)
            && !string.Equals(symbol.DefiningFilePath, _context.SourceFilePath, StringComparison.OrdinalIgnoreCase);
    }

    private TypeSyntax MapSelfType(SelfType selfType)
    {
        var declaringType = selfType.DeclaringType!;
        if (declaringType.TypeParameters.Count > 0)
        {
            // For generic classes (e.g., Box[T]), emit Box<T> with type parameter references
            var typeArgs = declaringType.TypeParameters
                .Select(tp => (TypeSyntax)IdentifierName(tp.Name))
                .ToArray();
            var baseName = GetMappedTypeNameFromSymbol(new UserDefinedType { Name = declaringType.Name, Symbol = declaringType });
            return GenericName(Identifier(baseName))
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgs)));
        }
        return MapSemanticType(new UserDefinedType { Name = declaringType.Name, Symbol = declaringType });
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
                return RoslynEmitter.MakeGlobalQualifiedName("System", "Action");
            }

            return QualifiedGenericName("System.Action", globalQualified: true, paramTypes);
        }

        // Otherwise use Func<T1, T2, ..., TResult>
        var allTypes = paramTypes.Append(returnType).ToArray();

        if (allTypes.Length == 1)
        {
            return QualifiedGenericName("System.Func", globalQualified: true, returnType);
        }

        return QualifiedGenericName("System.Func", globalQualified: true, allTypes);
    }

    private TypeSyntax MapSemanticTupleType(Semantic.TupleType tupleType)
    {
        if (tupleType.ElementTypes.Count == 0)
        {
            return RoslynEmitter.MakeGlobalQualifiedName("System", "ValueTuple");
        }

        var elementTypes = tupleType.ElementTypes
            .Select(MapSemanticType)
            .ToArray();

        // C# has no single-element tuple syntax (e.g. `(T)` is just `T`), so a 1-tuple
        // must be emitted as the explicit generic form global::System.ValueTuple<T>.
        if (elementTypes.Length == 1)
        {
            return QualifiedGenericName("System.ValueTuple", globalQualified: true, elementTypes);
        }

        // Named tuples use C# tuple syntax with element names: (double x, double y)
        if (tupleType.IsNamed)
        {
            var elements = elementTypes.Select((type, i) =>
            {
                var element = TupleElement(type);
                var name = tupleType.ElementNames!.Value[i];
                if (name != null)
                {
                    element = element.WithIdentifier(Identifier(name));
                }
                return element;
            });

            return SyntaxFactory.TupleType(SeparatedList(elements));
        }

        // Use ValueTuple<T1, T2, ...>
        return QualifiedGenericName("System.ValueTuple", globalQualified: true, elementTypes);
    }

    /// <summary>
    /// Maps a Sharpy type annotation to a C# TypeSyntax.
    /// T? (IsOptional) maps to Optional&lt;T&gt; (Sharpy.Core struct).
    /// T !E (IsResult) maps to Result&lt;T, E&gt;.
    /// IsCSharpNullable maps to C# T? (nullable).
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

        // Check SemanticInfo first for the resolved type. The TypeResolver (during type checking)
        // resolves each TypeAnnotation instance to the correct SemanticType, accounting for scoped
        // alias shadowing. SemanticInfo uses reference equality on TypeAnnotation, so each usage
        // site gets its own resolution — this correctly handles shadowing.
        {
            var resolvedFromSemantic = _context.SemanticInfo?.GetTypeAnnotation(type);
            if (resolvedFromSemantic != null && resolvedFromSemantic is not UnknownType)
            {
                return MapSemanticType(resolvedFromSemantic);
            }
        }

        // Fall back to SymbolTable alias lookup (for annotations not yet resolved by TypeResolver).
        var aliasSymbol = _context.SymbolTable.LookupTypeAlias(type.Name);
        if (aliasSymbol != null)
        {
            // Generic type alias: use the resolved SemanticType which has substitutions applied
            if (aliasSymbol.TypeParameters.Count > 0 && type.TypeArguments.Length > 0)
            {
                var resolvedType = _context.SemanticInfo?.GetTypeAnnotation(type);
                if (resolvedType != null)
                    return WrapOptionalOrNullable(MapSemanticType(resolvedType), type);
            }

            // Expand the alias
            if (aliasSymbol.TypeAnnotation != null)
            {
                // For type annotations, recursively map the underlying type
                var expandedType = MapType(aliasSymbol.TypeAnnotation);
                // Apply nullable/optional modifier from usage site
                return WrapOptionalOrNullable(expandedType, type);
            }
            else if (aliasSymbol.FunctionType != null)
            {
                // For function types, map to C# delegate/Func/Action
                var expandedType = MapFunctionType(aliasSymbol.FunctionType);
                // Function types typically shouldn't be nullable, but handle it anyway
                return WrapOptionalOrNullable(expandedType, type);
            }
        }

        // Handle Self type — resolve via SemanticType to emit the concrete class type
        if (type.Name == "Self")
        {
            var resolvedType = _context.SemanticInfo?.GetTypeAnnotation(type);
            if (resolvedType is SelfType selfType && selfType.DeclaringType != null)
                return WrapOptionalOrNullable(MapSemanticType(selfType), type);
        }

        // Get base type name
        var baseTypeName = GetMappedTypeName(type.Name);

        // Handle named tuple type annotations: tuple[x: float, y: float] -> (double x, double y)
        if (type.Name == BuiltinNames.Tuple && !type.TupleElementNames.IsEmpty && type.TypeArguments.Length >= 2)
        {
            var elements = type.TypeArguments.Select((ta, i) =>
            {
                var elementType = MapType(ta);
                var element = TupleElement(elementType);
                var name = type.TupleElementNames[i];
                if (name != null)
                {
                    element = element.WithIdentifier(Identifier(name));
                }
                return element;
            });

            var result = SyntaxFactory.TupleType(SeparatedList(elements));
            return WrapOptionalOrNullable(result, type);
        }

        // Handle function type annotations: (T, U) -> V parsed as Name="function"
        // TypeArguments contain [param types..., return type] where return type is the last element
        if (type.Name == "function" && type.TypeArguments.Length > 0)
        {
            var allTypeArgs = type.TypeArguments.Select(MapType).ToArray();
            var returnTypeSyntax = allTypeArgs[^1];
            var paramTypeSyntaxes = allTypeArgs.Take(allTypeArgs.Length - 1).ToArray();

            TypeSyntax result;

            // Check if return type is void → use Action
            if (IsVoidType(type.TypeArguments[^1]))
            {
                if (paramTypeSyntaxes.Length == 0)
                {
                    result = RoslynEmitter.MakeGlobalQualifiedName("System", "Action");
                }
                else
                {
                    result = QualifiedGenericName("System.Action", globalQualified: true, paramTypeSyntaxes);
                }
            }
            else
            {
                // Use Func<params..., return>
                var funcTypeArgs = paramTypeSyntaxes.Append(returnTypeSyntax).ToArray();

                if (funcTypeArgs.Length == 1)
                {
                    result = QualifiedGenericName("System.Func", globalQualified: true, returnTypeSyntax);
                }
                else
                {
                    result = QualifiedGenericName("System.Func", globalQualified: true, funcTypeArgs);
                }
            }

            return WrapOptionalOrNullable(result, type);
        }

        // Handle array type annotations: array[T] or T[] -> C# T[]
        if (type.Name == BuiltinNames.Array && type.TypeArguments.Length == 1)
        {
            var elementType = MapType(type.TypeArguments[0]);
            var result = MakeArrayType(elementType);
            return WrapOptionalOrNullable(result, type);
        }

        // Handle generic type arguments
        if (type.TypeArguments.Length > 0)
        {
            var typeArgs = type.TypeArguments
                .Select(MapType)
                .ToArray();

            var result = QualifiedGenericName(baseTypeName, typeArgs);

            // Handle nullable/optional generic types
            return WrapOptionalOrNullable(result, type);
        }

        // Handle nullable/optional non-generic types
        var typeSyntax = ParseTypeName(baseTypeName);
        return WrapOptionalOrNullable(typeSyntax, type);
    }

    /// <summary>
    /// Wraps a type with Optional&lt;T&gt; or C# nullable T? depending on the type annotation flags.
    /// IsOptional → Optional&lt;T&gt; (Sharpy.Core struct)
    /// IsCSharpNullable → T? (C# nullable)
    /// </summary>
    private TypeSyntax WrapOptionalOrNullable(TypeSyntax innerType, TypeAnnotation type)
    {
        if (type.IsOptional)
        {
            return GenericName(Identifier("Optional"))
                .WithTypeArgumentList(
                    TypeArgumentList(SingletonSeparatedList(innerType)));
        }
        if (type.IsCSharpNullable)
        {
            return NullableType(innerType);
        }
        return innerType;
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
        var typeSymbol = _context.SymbolTable.LookupType(sharpyTypeName)
            ?? LookupModuleQualifiedType(sharpyTypeName);
        if (typeSymbol != null)
        {
            // For aliased imports, resolve the original type name for code generation.
            // E.g., "from helper import Config as Cfg" should generate "Helper.Config", not "Helper.Cfg".
            var codeGenInfo = _context.SemanticBinding.GetCodeGenInfo(typeSymbol)
                ?? typeSymbol.CodeGenInfo;
            var resolvedName = codeGenInfo?.OriginalImportName ?? sharpyTypeName;

            // Check if type is from a different file (cross-file reference in same project)
            if (!string.IsNullOrEmpty(typeSymbol.DefiningFilePath) &&
                !string.IsNullOrEmpty(_context.SourceFilePath) &&
                !string.Equals(typeSymbol.DefiningFilePath, _context.SourceFilePath, StringComparison.OrdinalIgnoreCase))
            {
                // Type from another file - use fully qualified name
                return GetFullyQualifiedTypeName(typeSymbol, resolvedName);
            }

            // Check if type is from an external module (imported)
            if (!string.IsNullOrEmpty(typeSymbol.DefiningModule))
            {
                // Type from another module - use fully qualified name
                return GetFullyQualifiedTypeName(typeSymbol, resolvedName);
            }

            // Type is in current scope (user-defined in current file) - use simple name
            // This takes priority over builtin registry to allow shadowing
            return NameCasing.ResolveType(sharpyTypeName, false);
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
                    // When inside a user-defined namespace, use global:: to avoid ambiguity
                    // (e.g., inside namespace MyApp, "System" could resolve to "MyApp.System").
                    if (!string.IsNullOrEmpty(_context.ProjectNamespace))
                        return $"global::{ClrNameHelper.ToCSharpQualifiedName(builtinTypeSymbol.ClrType.FullName!)}";
                    return builtinTypeSymbol.ClrType.Name;
                }
                // Sharpy types need global:: qualification
                var fullName = ClrNameHelper.ToCSharpQualifiedName(builtinTypeSymbol.ClrType.FullName!);
                return $"global::{fullName}";
            }
            return sharpyTypeName;
        }

        // User-defined types in current module keep their PascalCase name
        return NameCasing.ResolveType(sharpyTypeName, false);
    }

    /// <summary>
    /// Gets the fully qualified C# type name for a type from another file/module.
    /// Types are nested inside the module class, so cross-file references use
    /// Namespace.ModuleClass.TypeName.
    /// </summary>
    private string GetFullyQualifiedTypeName(TypeSymbol typeSymbol, string sharpyTypeName)
    {
        if (typeSymbol.ClrType != null)
        {
            // Strip CLR generic arity suffix (e.g., DefaultDict`2 → DefaultDict)
            // because type arguments are added separately by the caller via QualifiedGenericName.
            // Also convert CLR nested type notation (+) to C# dot notation.
            var fullName = ClrNameHelper.StripArity(typeSymbol.ClrType.FullName!)
                .Replace('+', '.');

            // Always use global:: for Sharpy namespace CLR types to avoid
            // Sharpy.Sharpy.X when code is inside namespace Sharpy.
            if (typeSymbol.ClrType.Namespace == "Sharpy")
                return $"global::{fullName}";

            // When inside a user-defined namespace, use global:: for all CLR types
            // to avoid ambiguity (e.g., inside namespace Sharpy, "System.IComparable"
            // could resolve to "Sharpy.System.IComparable").
            if (!string.IsNullOrEmpty(_context.ProjectNamespace))
                return $"global::{fullName}";

            return fullName;
        }

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
            return NameCasing.ResolveType(sharpyTypeName, false);
        }

        var typeName = NameCasing.ResolveType(sharpyTypeName, false);

        // Check for collision: when the file/directory name (PascalCase) matches the type name,
        // the type IS the module class (collision merge), not nested inside it.
        // e.g., animal.spy with class Animal → type is Sharpy.Test.Animal, not Sharpy.Test.Animal.Animal
        var lastSegment = moduleNamespace.Contains('.', StringComparison.Ordinal)
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
                        namespaceParts.Add(NameMangler.ToNamespacePart(part));
                    }
                }
            }

            // Add file name part (skip __init__ as it represents the package itself)
            if (!string.Equals(fileName, DunderNames.Init, StringComparison.OrdinalIgnoreCase))
            {
                namespaceParts.Add(NameMangler.ToNamespacePart(fileName));
            }

            if (namespaceParts.Count > 0)
            {
                return string.Join(".", namespaceParts);
            }
        }

        // Fallback: just use file name
        var fallbackFileName = Path.GetFileNameWithoutExtension(filePath);
        return NameMangler.ToNamespacePart(fallbackFileName);
    }

    /// <summary>
    /// Maps a UserDefinedType to its fully qualified C# name, using the Symbol if available.
    /// For aliased imports (e.g., "from pkg import Config as Cfg"), uses the original type
    /// name from the Symbol rather than the alias from the UserDefinedType.
    /// </summary>
    private string GetMappedTypeNameFromSymbol(UserDefinedType udt)
    {
        if (udt.Symbol != null)
        {
            // Use OriginalImportName for aliased imports, otherwise Symbol.Name.
            // This ensures "from pkg import Config as Cfg" generates "Pkg.Config", not "Pkg.Cfg".
            var codeGenInfo = _context.SemanticBinding.GetCodeGenInfo(udt.Symbol)
                ?? udt.Symbol.CodeGenInfo;
            var originalName = codeGenInfo?.OriginalImportName ?? udt.Symbol.Name;

            // Check if type is from a different file (cross-file reference)
            if (!string.IsNullOrEmpty(udt.Symbol.DefiningFilePath) &&
                !string.IsNullOrEmpty(_context.SourceFilePath) &&
                !string.Equals(udt.Symbol.DefiningFilePath, _context.SourceFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return GetFullyQualifiedTypeName(udt.Symbol, originalName);
            }

            // Check if type is from an external module (imported)
            if (!string.IsNullOrEmpty(udt.Symbol.DefiningModule))
            {
                return GetFullyQualifiedTypeName(udt.Symbol, originalName);
            }
        }

        // Handle nested types — emit qualified name (e.g., Outer.Middle.Inner)
        if (udt.Symbol?.DeclaringType != null)
        {
            var parts = new List<string>();
            var current = udt.Symbol;
            while (current != null)
            {
                parts.Add(NameCasing.ResolveType(current.Name, false));
                current = current.DeclaringType;
            }
            parts.Reverse();
            return string.Join(".", parts);
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
        return string.Join(".", parts.Select(p => NameMangler.ToNamespacePart(p)));
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
                return RoslynEmitter.MakeGlobalQualifiedName("System", "Action");
            }

            return QualifiedGenericName("System.Action", globalQualified: true, paramTypes);
        }

        // Otherwise use Func<T1, T2, ..., TResult>
        var allTypes = paramTypes.Append(returnType).ToArray();

        if (allTypes.Length == 1)
        {
            return QualifiedGenericName("System.Func", globalQualified: true, returnType);
        }

        return QualifiedGenericName("System.Func", globalQualified: true, allTypes);
    }

    /// <summary>
    /// Maps a TupleType to a C# ValueTuple type
    /// </summary>
    public TypeSyntax MapTupleType(Parser.Ast.TupleType tupleType)
    {
        if (tupleType.ElementTypes.IsEmpty)
        {
            // Empty tuple
            return RoslynEmitter.MakeGlobalQualifiedName("System", "ValueTuple");
        }

        var elementTypes = tupleType.ElementTypes
            .Select(MapType)
            .ToArray();

        // For single element, it's just the type (not a tuple)
        if (elementTypes.Length == 1)
        {
            return elementTypes[0];
        }

        // Named tuples use C# tuple syntax with element names: (double x, double y)
        if (!tupleType.ElementNames.IsEmpty)
        {
            var elements = elementTypes.Select((type, i) =>
            {
                var element = TupleElement(type);
                var name = tupleType.ElementNames[i];
                if (name != null)
                {
                    element = element.WithIdentifier(Identifier(name));
                }
                return element;
            });

            return SyntaxFactory.TupleType(SeparatedList(elements));
        }

        // Use ValueTuple<T1, T2, ...>
        return QualifiedGenericName("System.ValueTuple", globalQualified: true, elementTypes);
    }

    /// <summary>
    /// Creates a Sharpy collection type with element type
    /// </summary>
    public TypeSyntax CreateCollectionType(string collectionName, TypeSyntax elementType)
    {
        var baseType = GetMappedTypeName(collectionName);

        return QualifiedGenericName(baseType, elementType);
    }

    /// <summary>
    /// Creates a Sharpy.Dict type with key and value types.
    /// </summary>
    public TypeSyntax CreateDictType(TypeSyntax keyType, TypeSyntax valueType)
    {
        return QualifiedGenericName(CSharpTypeNames.SharpyDict, keyType, valueType);
    }

    /// <summary>
    /// Checks if a type is void
    /// </summary>
    private bool IsVoidType(TypeAnnotation? type)
    {
        return type?.Name == BuiltinNames.Void || type?.Name == BuiltinNames.None;
    }

    /// <summary>
    /// Infers the element type from a collection of expressions
    /// </summary>
    public TypeSyntax InferElementType(IEnumerable<Expression> expressions)
    {
        // Infer element type by checking if all expressions share the same type.
        // If they do, use that type; otherwise fall back to object.

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
            IntegerLiteral => BuiltinNames.Int,
            FloatLiteral floatLit => floatLit.Suffix?.ToLower() switch
            {
                "f" => "float",
                "m" => "decimal",
                _ => BuiltinNames.Double
            },
            StringLiteral => "string",
            BooleanLiteral => BuiltinNames.Bool,
            NoneLiteral => BuiltinNames.Object,
            ListLiteral => BuiltinNames.List,
            DictLiteral => BuiltinNames.Dict,
            SetLiteral => BuiltinNames.Set,
            TupleLiteral => BuiltinNames.Tuple,
            _ => BuiltinNames.Object
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
            BuiltinNames.Int => PredefinedType(Token(SyntaxKind.IntKeyword)),
            BuiltinNames.Long => PredefinedType(Token(SyntaxKind.LongKeyword)),
            "float" => PredefinedType(Token(SyntaxKind.FloatKeyword)),
            BuiltinNames.Double => PredefinedType(Token(SyntaxKind.DoubleKeyword)),
            "decimal" => PredefinedType(Token(SyntaxKind.DecimalKeyword)),
            "string" => PredefinedType(Token(SyntaxKind.StringKeyword)),
            BuiltinNames.Bool => PredefinedType(Token(SyntaxKind.BoolKeyword)),
            BuiltinNames.Object => PredefinedType(Token(SyntaxKind.ObjectKeyword)),
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
            return GenericName(NameCasing.ResolveType(nestedTypeName.Name, nestedTypeName.IsNameBacktickEscaped))
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgs)));
        }

        // Module-qualified type used as an expression (e.g. email.MessageError). The type
        // checker records the referenced type on the MemberAccess node, so map that semantic
        // type instead of falling back to object (#903).
        if (expr is MemberAccess memberAccess
            && _context.SemanticInfo?.GetExpressionType(memberAccess) is { } resolvedExprType
            && resolvedExprType is not UnknownType)
        {
            return MapSemanticType(resolvedExprType);
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

    /// <summary>
    /// Creates a properly qualified generic name for potentially dotted type names.
    /// For simple names (e.g. "List"), returns GenericName("List").WithTypeArgumentList(...).
    /// For dotted names (e.g. "Sharpy.List"), returns QualifiedName(IdentifierName("Sharpy"), GenericName("List").WithTypeArgumentList(...)).
    /// This avoids passing dotted names to GenericName(), which expects a simple identifier.
    /// </summary>
    internal static NameSyntax QualifiedGenericName(string dottedTypeName, params TypeSyntax[] typeArguments)
        => QualifiedGenericName(dottedTypeName, globalQualified: false, typeArguments);

    internal static NameSyntax QualifiedGenericName(string dottedTypeName, bool globalQualified, params TypeSyntax[] typeArguments)
    {
        var typeArgList = TypeArgumentList(
            typeArguments.Length == 1
                ? SingletonSeparatedList(typeArguments[0])
                : SeparatedList(typeArguments));

        var parts = dottedTypeName.Split('.');

        if (parts.Length == 1)
        {
            return GenericName(parts[0]).WithTypeArgumentList(typeArgList);
        }

        NameSyntax result = globalQualified
            ? AliasQualifiedName(
                IdentifierName(Token(SyntaxKind.GlobalKeyword)),
                IdentifierName(parts[0]))
            : IdentifierName(parts[0]);
        for (var i = 1; i < parts.Length - 1; i++)
        {
            result = QualifiedName(result, IdentifierName(parts[i]));
        }

        return QualifiedName(result, GenericName(parts[^1]).WithTypeArgumentList(typeArgList));
    }

    private TypeSymbol? LookupModuleQualifiedType(string dottedName)
    {
        if (!dottedName.Contains('.', StringComparison.Ordinal))
            return null;

        var parts = dottedName.Split('.');

        if (_context.SymbolTable.Lookup(parts[0]) is not ModuleSymbol moduleSymbol)
            return null;

        return moduleSymbol.ResolveQualifiedType(parts, startIndex: 1);
    }
}
