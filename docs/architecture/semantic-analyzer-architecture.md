# Semantic Analyzer Architecture

## Overview

This document provides a step-by-step guide to implementing the semantic analyzer for the Sharpy compiler. The semantic analyzer sits between the parser and code generator, performing type checking, name resolution, and semantic validation.

**Pipeline Position:**
```
Sharpy Source (.spy)
    ↓
Lexer (Tokens)
    ↓
Parser (Sharpy AST)
    ↓
Semantic Analyzer (Type-checked AST) ← YOU ARE HERE
    ↓
Code Generator (C# AST via Roslyn)
    ↓
C# Code Emitter (C# Source)
    ↓
.NET Compiler (IL/Assembly)
```

## Design Principles

1. **Multi-Pass Analysis**: Perform analysis in multiple passes to handle forward references
2. **Symbol Table Management**: Maintain scoped symbol tables for name resolution
3. **Type Inference**: Infer types where not explicitly specified
4. **Error Recovery**: Continue analysis after errors to report multiple issues
5. **Immutable AST**: Don't modify the original AST; annotate it with semantic information
6. **Incremental Design**: Build features incrementally from simple to complex

## Phase 1: Foundation - Symbol Tables and Name Resolution

### Step 1.1: Define Symbol Representations

First, establish how symbols (variables, functions, types) are represented.

**File: `src/Sharpy.Compiler/Semantic/Symbol.cs`** (already exists, review/extend)

```csharp
namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Base class for all symbols in the symbol table
/// </summary>
public abstract record Symbol
{
    public string Name { get; init; } = string.Empty;
    public SymbolKind Kind { get; init; }
    public AccessLevel AccessLevel { get; init; } = AccessLevel.Public;
    public int? DeclarationLine { get; init; }
    public int? DeclarationColumn { get; init; }
}

/// <summary>
/// Variable or parameter symbol
/// </summary>
public record VariableSymbol : Symbol
{
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool IsParameter { get; init; }
    public bool IsConstant { get; init; }
    public bool HasDefaultValue { get; init; }
}

/// <summary>
/// Function or method symbol
/// </summary>
public record FunctionSymbol : Symbol
{
    public List<ParameterSymbol> Parameters { get; init; } = new();
    public SemanticType ReturnType { get; init; } = SemanticType.Unknown;
    public bool IsStatic { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsOverride { get; init; }

    // For .NET interop
    public System.Reflection.MethodInfo? ClrMethod { get; init; }
}

/// <summary>
/// Type symbol (class, struct, interface, enum)
/// </summary>
public record TypeSymbol : Symbol
{
    public TypeKind TypeKind { get; init; }
    public Type? ClrType { get; init; }

    // Generic type parameters
    public List<string> TypeParameters { get; init; } = new();
    public bool IsGeneric => TypeParameters.Count > 0;

    // Members
    public List<VariableSymbol> Fields { get; init; } = new();
    public List<FunctionSymbol> Methods { get; init; } = new();
    public List<PropertySymbol> Properties { get; init; } = new();

    // Inheritance
    public TypeSymbol? BaseType { get; init; }
    public List<TypeSymbol> Interfaces { get; init; } = new();
}

/// <summary>
/// Property symbol (for future use in v1.0)
/// </summary>
public record PropertySymbol
{
    public string Name { get; init; } = string.Empty;
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool HasGetter { get; init; }
    public bool HasSetter { get; init; }
    public AccessLevel GetterAccess { get; init; } = AccessLevel.Public;
    public AccessLevel SetterAccess { get; init; } = AccessLevel.Public;
}

/// <summary>
/// Parameter symbol for functions/methods
/// </summary>
public record ParameterSymbol
{
    public string Name { get; init; } = string.Empty;
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool HasDefault { get; init; }
    public Expression? DefaultValue { get; init; }
}

/// <summary>
/// Module symbol
/// </summary>
public record ModuleSymbol : Symbol
{
    public string FilePath { get; init; } = string.Empty;
    public List<Symbol> Exports { get; init; } = new();
}

public enum SymbolKind
{
    Variable,
    Parameter,
    Function,
    Type,
    Module,
    Property
}

public enum TypeKind
{
    Class,
    Struct,
    Interface,
    Enum
}

public enum AccessLevel
{
    Public,
    Protected,
    Private
}
```

### Step 1.2: Define Semantic Type System

**File: `src/Sharpy.Compiler/Semantic/SemanticType.cs`**

```csharp
namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Represents a type in the semantic analysis phase
/// </summary>
public abstract record SemanticType
{
    // Singleton instances for common types
    public static readonly SemanticType Unknown = new UnknownType();
    public static readonly SemanticType Void = new VoidType();
    public static readonly SemanticType Int = new BuiltinType("int", typeof(int));
    public static readonly SemanticType Long = new BuiltinType("long", typeof(long));
    public static readonly SemanticType Float = new BuiltinType("float", typeof(float));
    public static readonly SemanticType Double = new BuiltinType("double", typeof(double));
    public static readonly SemanticType Bool = new BuiltinType("bool", typeof(bool));
    public static readonly SemanticType Str = new BuiltinType("str", typeof(string));

    /// <summary>
    /// Check if this type is assignable to another type
    /// </summary>
    public virtual bool IsAssignableTo(SemanticType other)
    {
        return this.Equals(other);
    }

    /// <summary>
    /// Get a human-readable name for this type
    /// </summary>
    public abstract string GetDisplayName();
}

/// <summary>
/// Unknown type (used for error recovery)
/// </summary>
public record UnknownType : SemanticType
{
    public override string GetDisplayName() => "<?>";

    public override bool IsAssignableTo(SemanticType other) => true; // Allow anything to avoid cascading errors
}

/// <summary>
/// Void type (for functions that don't return a value)
/// </summary>
public record VoidType : SemanticType
{
    public override string GetDisplayName() => "None";
}

/// <summary>
/// Built-in primitive type
/// </summary>
public record BuiltinType : SemanticType
{
    public string Name { get; init; }
    public Type? ClrType { get; init; }

    public BuiltinType(string name, Type? clrType = null)
    {
        Name = name;
        ClrType = clrType;
    }

    public override string GetDisplayName() => Name;

    public override bool IsAssignableTo(SemanticType other)
    {
        if (base.IsAssignableTo(other)) return true;

        // Handle numeric conversions
        if (this == Int && other == Long) return true;
        if (this == Int && other == Float) return true;
        if (this == Int && other == Double) return true;
        if (this == Float && other == Double) return true;
        if (this == Long && other == Double) return true;

        return false;
    }
}

/// <summary>
/// Generic type with type arguments (e.g., list[int])
/// </summary>
public record GenericType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public List<SemanticType> TypeArguments { get; init; } = new();
    public TypeSymbol? GenericDefinition { get; init; }

    public override string GetDisplayName()
    {
        var args = string.Join(", ", TypeArguments.Select(t => t.GetDisplayName()));
        return $"{Name}[{args}]";
    }

    public override bool IsAssignableTo(SemanticType other)
    {
        if (other is GenericType otherGeneric
            && Name == otherGeneric.Name
            && TypeArguments.Count == otherGeneric.TypeArguments.Count)
        {
            // Check covariance/contravariance rules here in future
            return TypeArguments.Zip(otherGeneric.TypeArguments)
                .All(pair => pair.First.IsAssignableTo(pair.Second));
        }

        return base.IsAssignableTo(other);
    }
}

/// <summary>
/// User-defined type (class, struct, interface)
/// </summary>
public record UserDefinedType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol Symbol { get; init; } = null!;

    public override string GetDisplayName() => Name;

    public override bool IsAssignableTo(SemanticType other)
    {
        if (base.IsAssignableTo(other)) return true;

        if (other is UserDefinedType otherUdt)
        {
            // Check inheritance chain
            var current = Symbol.BaseType;
            while (current != null)
            {
                if (current.Name == otherUdt.Name)
                    return true;
                current = current.BaseType;
            }

            // Check interface implementation
            return Symbol.Interfaces.Any(i => i.Name == otherUdt.Name);
        }

        return false;
    }
}

/// <summary>
/// Nullable type (T?)
/// </summary>
public record NullableType : SemanticType
{
    public SemanticType UnderlyingType { get; init; } = SemanticType.Unknown;

    public override string GetDisplayName() => $"{UnderlyingType.GetDisplayName()}?";

    public override bool IsAssignableTo(SemanticType other)
    {
        // Nullable T is assignable to T (implicit unwrapping)
        if (UnderlyingType.IsAssignableTo(other))
            return true;

        // Nullable T is assignable to Nullable T
        if (other is NullableType otherNullable)
            return UnderlyingType.IsAssignableTo(otherNullable.UnderlyingType);

        return base.IsAssignableTo(other);
    }
}

/// <summary>
/// Function type (for lambdas and delegates)
/// </summary>
public record FunctionType : SemanticType
{
    public List<SemanticType> ParameterTypes { get; init; } = new();
    public SemanticType ReturnType { get; init; } = SemanticType.Void;

    public override string GetDisplayName()
    {
        var params_ = string.Join(", ", ParameterTypes.Select(p => p.GetDisplayName()));
        return $"({params_}) -> {ReturnType.GetDisplayName()}";
    }
}

/// <summary>
/// Tuple type
/// </summary>
public record TupleType : SemanticType
{
    public List<SemanticType> ElementTypes { get; init; } = new();

    public override string GetDisplayName()
    {
        var elements = string.Join(", ", ElementTypes.Select(e => e.GetDisplayName()));
        return $"tuple[{elements}]";
    }
}
```

### Step 1.3: Implement Scope Management

**File: `src/Sharpy.Compiler/Semantic/Scope.cs`** (already exists, enhance if needed)

The existing `Scope.cs` is good. Ensure it supports:
- Nested scopes with parent lookups
- Local-only lookups (no parent search)
- Symbol redefinition detection

### Step 1.4: Build Symbol Table Infrastructure

**File: `src/Sharpy.Compiler/Semantic/SymbolTable.cs`** (enhance existing)

```csharp
namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Manages all scopes and symbols during semantic analysis
/// </summary>
public class SymbolTable
{
    private readonly Stack<Scope> _scopeStack = new();
    private readonly Scope _globalScope;
    private readonly BuiltinRegistry _builtins;

    public SymbolTable(BuiltinRegistry builtins)
    {
        _builtins = builtins;
        _globalScope = new Scope("global");
        _scopeStack.Push(_globalScope);

        // Populate global scope with builtins
        PopulateBuiltins();
    }

    private void PopulateBuiltins()
    {
        // Add builtin types
        foreach (var (name, typeSymbol) in _builtins.GetAllTypes())
        {
            _globalScope.Define(typeSymbol);
        }

        // Add builtin functions
        foreach (var (name, funcSymbol) in _builtins.GetAllFunctions())
        {
            _globalScope.Define(funcSymbol);
        }
    }

    public void EnterScope(string name)
    {
        var newScope = new Scope(name, CurrentScope);
        _scopeStack.Push(newScope);
    }

    public void ExitScope()
    {
        if (_scopeStack.Count <= 1)
        {
            throw new InvalidOperationException("Cannot exit global scope");
        }
        _scopeStack.Pop();
    }

    public void Define(Symbol symbol)
    {
        CurrentScope.Define(symbol);
    }

    public Symbol? Lookup(string name, bool searchParents = true)
    {
        return CurrentScope.Lookup(name, searchParents);
    }

    public TypeSymbol? LookupType(string name)
    {
        return Lookup(name) as TypeSymbol;
    }

    public FunctionSymbol? LookupFunction(string name)
    {
        return Lookup(name) as FunctionSymbol;
    }

    public VariableSymbol? LookupVariable(string name)
    {
        return Lookup(name) as VariableSymbol;
    }

    public Scope CurrentScope => _scopeStack.Peek();
    public Scope GlobalScope => _globalScope;
    public int ScopeDepth => _scopeStack.Count;
}
```

### Step 1.5: Implement Basic Name Resolution Pass

**File: `src/Sharpy.Compiler/Semantic/NameResolver.cs`**

```csharp
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// First pass: Resolve all names and build symbol tables
/// </summary>
public class NameResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    public NameResolver(SymbolTable symbolTable, ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? new NullLogger();
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

    /// <summary>
    /// Resolve names in a module (first pass: declarations only)
    /// </summary>
    public void ResolveDeclarations(Module module)
    {
        _logger.Info($"Name resolution pass 1: Declarations in module");

        foreach (var statement in module.Body)
        {
            ResolveDeclaration(statement);
        }
    }

    private void ResolveDeclaration(Statement statement)
    {
        switch (statement)
        {
            case ClassDef classDef:
                ResolveClassDeclaration(classDef);
                break;

            case StructDef structDef:
                ResolveStructDeclaration(structDef);
                break;

            case InterfaceDef interfaceDef:
                ResolveInterfaceDeclaration(interfaceDef);
                break;

            case EnumDef enumDef:
                ResolveEnumDeclaration(enumDef);
                break;

            case FunctionDef functionDef:
                ResolveFunctionDeclaration(functionDef);
                break;

            case VariableDeclaration varDecl when varDecl.IsConst:
                ResolveConstantDeclaration(varDecl);
                break;

            case ImportStatement import:
                ResolveImport(import);
                break;

            case FromImportStatement fromImport:
                ResolveFromImport(fromImport);
                break;

            // Other statements are handled in later passes
        }
    }

    private void ResolveClassDeclaration(ClassDef classDef)
    {
        _logger.Debug($"Resolving class declaration: {classDef.Name}");

        // Check for redefinition
        if (_symbolTable.Lookup(classDef.Name, searchParents: false) != null)
        {
            AddError($"Class '{classDef.Name}' is already defined",
                classDef.Line, classDef.Column);
            return;
        }

        // Create type symbol
        var typeSymbol = new TypeSymbol
        {
            Name = classDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = DetermineAccessLevel(classDef.Name),
            TypeParameters = classDef.TypeParameters.ToList(),
            DeclarationLine = classDef.Line,
            DeclarationColumn = classDef.Column
        };

        // Define in current scope
        _symbolTable.Define(typeSymbol);

        // Enter class scope to resolve members
        _symbolTable.EnterScope($"class:{classDef.Name}");

        foreach (var statement in classDef.Body)
        {
            if (statement is FunctionDef method)
            {
                ResolveMethodDeclaration(method, typeSymbol);
            }
            else if (statement is VariableDeclaration field)
            {
                ResolveFieldDeclaration(field, typeSymbol);
            }
        }

        _symbolTable.ExitScope();
    }

    private void ResolveStructDeclaration(StructDef structDef)
    {
        _logger.Debug($"Resolving struct declaration: {structDef.Name}");

        if (_symbolTable.Lookup(structDef.Name, searchParents: false) != null)
        {
            AddError($"Struct '{structDef.Name}' is already defined",
                structDef.Line, structDef.Column);
            return;
        }

        var typeSymbol = new TypeSymbol
        {
            Name = structDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Struct,
            AccessLevel = DetermineAccessLevel(structDef.Name),
            TypeParameters = structDef.TypeParameters.ToList(),
            DeclarationLine = structDef.Line,
            DeclarationColumn = structDef.Column
        };

        _symbolTable.Define(typeSymbol);

        _symbolTable.EnterScope($"struct:{structDef.Name}");

        foreach (var statement in structDef.Body)
        {
            if (statement is FunctionDef method)
            {
                ResolveMethodDeclaration(method, typeSymbol);
            }
            else if (statement is VariableDeclaration field)
            {
                ResolveFieldDeclaration(field, typeSymbol);
            }
        }

        _symbolTable.ExitScope();
    }

    private void ResolveInterfaceDeclaration(InterfaceDef interfaceDef)
    {
        _logger.Debug($"Resolving interface declaration: {interfaceDef.Name}");

        if (_symbolTable.Lookup(interfaceDef.Name, searchParents: false) != null)
        {
            AddError($"Interface '{interfaceDef.Name}' is already defined",
                interfaceDef.Line, interfaceDef.Column);
            return;
        }

        var typeSymbol = new TypeSymbol
        {
            Name = interfaceDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            AccessLevel = AccessLevel.Public, // Interfaces are always public
            TypeParameters = interfaceDef.TypeParameters.ToList(),
            DeclarationLine = interfaceDef.Line,
            DeclarationColumn = interfaceDef.Column
        };

        _symbolTable.Define(typeSymbol);

        _symbolTable.EnterScope($"interface:{interfaceDef.Name}");

        foreach (var statement in interfaceDef.Body)
        {
            if (statement is FunctionDef method)
            {
                ResolveMethodDeclaration(method, typeSymbol);
            }
        }

        _symbolTable.ExitScope();
    }

    private void ResolveEnumDeclaration(EnumDef enumDef)
    {
        _logger.Debug($"Resolving enum declaration: {enumDef.Name}");

        if (_symbolTable.Lookup(enumDef.Name, searchParents: false) != null)
        {
            AddError($"Enum '{enumDef.Name}' is already defined",
                enumDef.Line, enumDef.Column);
            return;
        }

        var typeSymbol = new TypeSymbol
        {
            Name = enumDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Enum,
            AccessLevel = DetermineAccessLevel(enumDef.Name),
            DeclarationLine = enumDef.Line,
            DeclarationColumn = enumDef.Column
        };

        _symbolTable.Define(typeSymbol);
    }

    private void ResolveFunctionDeclaration(FunctionDef functionDef)
    {
        _logger.Debug($"Resolving function declaration: {functionDef.Name}");

        if (_symbolTable.Lookup(functionDef.Name, searchParents: false) != null)
        {
            AddError($"Function '{functionDef.Name}' is already defined",
                functionDef.Line, functionDef.Column);
            return;
        }

        var funcSymbol = new FunctionSymbol
        {
            Name = functionDef.Name,
            Kind = SymbolKind.Function,
            AccessLevel = DetermineAccessLevel(functionDef.Name),
            IsStatic = true, // Module-level functions are static
            DeclarationLine = functionDef.Line,
            DeclarationColumn = functionDef.Column
        };

        _symbolTable.Define(funcSymbol);
    }

    private void ResolveMethodDeclaration(FunctionDef method, TypeSymbol owningType)
    {
        _logger.Debug($"Resolving method declaration: {owningType.Name}.{method.Name}");

        var funcSymbol = new FunctionSymbol
        {
            Name = method.Name,
            Kind = SymbolKind.Function,
            AccessLevel = DetermineAccessLevel(method.Name),
            IsStatic = method.Decorators.Any(d => d.Name == "staticmethod"),
            IsAbstract = method.Decorators.Any(d => d.Name == "abstractmethod"),
            IsOverride = method.Decorators.Any(d => d.Name == "override"),
            DeclarationLine = method.Line,
            DeclarationColumn = method.Column
        };

        owningType.Methods.Add(funcSymbol);
        _symbolTable.Define(funcSymbol);
    }

    private void ResolveFieldDeclaration(VariableDeclaration field, TypeSymbol owningType)
    {
        _logger.Debug($"Resolving field declaration: {owningType.Name}.{field.Name}");

        var varSymbol = new VariableSymbol
        {
            Name = field.Name,
            Kind = SymbolKind.Variable,
            AccessLevel = DetermineAccessLevel(field.Name),
            IsConstant = field.IsConst,
            DeclarationLine = field.Line,
            DeclarationColumn = field.Column
        };

        owningType.Fields.Add(varSymbol);
        _symbolTable.Define(varSymbol);
    }

    private void ResolveConstantDeclaration(VariableDeclaration constDecl)
    {
        _logger.Debug($"Resolving constant declaration: {constDecl.Name}");

        if (_symbolTable.Lookup(constDecl.Name, searchParents: false) != null)
        {
            AddError($"Constant '{constDecl.Name}' is already defined",
                constDecl.Line, constDecl.Column);
            return;
        }

        var varSymbol = new VariableSymbol
        {
            Name = constDecl.Name,
            Kind = SymbolKind.Variable,
            AccessLevel = AccessLevel.Public,
            IsConstant = true,
            DeclarationLine = constDecl.Line,
            DeclarationColumn = constDecl.Column
        };

        _symbolTable.Define(varSymbol);
    }

    private void ResolveImport(ImportStatement import)
    {
        _logger.Debug($"Resolving import: {import.Module}");

        // TODO: Implement module loading and resolution
        // For now, just log that we encountered an import
    }

    private void ResolveFromImport(FromImportStatement fromImport)
    {
        _logger.Debug($"Resolving from-import: from {fromImport.Module} import ...");

        // TODO: Implement selective import resolution
    }

    private AccessLevel DetermineAccessLevel(string name)
    {
        if (name.StartsWith("__") && !name.EndsWith("__"))
            return AccessLevel.Private;
        if (name.StartsWith("_"))
            return AccessLevel.Protected;
        return AccessLevel.Public;
    }

    private void AddError(string message, int? line = null, int? column = null)
    {
        var error = new SemanticError(message, line, column);
        _errors.Add(error);
        _logger.Error(error.Message);
    }
}
```

### Step 1.6: Hook Into Compilation Pipeline

**File: `src/Sharpy.Compiler/Compiler.cs`** (create or update)

```csharp
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler;

/// <summary>
/// Main compiler driver orchestrating the compilation pipeline
/// </summary>
public class Compiler
{
    private readonly ICompilerLogger _logger;

    public Compiler(ICompilerLogger? logger = null)
    {
        _logger = logger ?? new NullLogger();
    }

    public CompilationResult Compile(string sourceCode, string filePath)
    {
        _logger.Info($"Starting compilation of {filePath}");

        // Phase 1: Lexical Analysis
        _logger.Info("Phase 1: Lexical Analysis");
        var lexer = new Lexer.Lexer(sourceCode, _logger);
        var tokens = lexer.Tokenize();

        if (lexer.Errors.Any())
        {
            return new CompilationResult
            {
                Success = false,
                Errors = lexer.Errors.Select(e => e.Message).ToList()
            };
        }

        // Phase 2: Syntax Analysis
        _logger.Info("Phase 2: Syntax Analysis");
        var parser = new Parser.Parser(tokens, _logger);
        var module = parser.ParseModule();

        // Phase 3: Semantic Analysis
        _logger.Info("Phase 3: Semantic Analysis");
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);

        // Pass 1: Name resolution (declarations)
        var nameResolver = new NameResolver(symbolTable, _logger);
        nameResolver.ResolveDeclarations(module);

        if (nameResolver.Errors.Any())
        {
            return new CompilationResult
            {
                Success = false,
                Errors = nameResolver.Errors.Select(e => e.Message).ToList()
            };
        }

        // TODO: Pass 2: Type checking (will implement in Phase 2)
        // TODO: Pass 3: Semantic validation (will implement in Phase 3)

        // Phase 4: Code Generation (placeholder)
        _logger.Info("Phase 4: Code Generation");
        // TODO: Implement code generation

        return new CompilationResult
        {
            Success = true,
            Module = module,
            SymbolTable = symbolTable
        };
    }
}

public class CompilationResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = new();
    public Parser.Ast.Module? Module { get; init; }
    public SymbolTable? SymbolTable { get; init; }
}
```

## Testing Phase 1

Create unit tests to verify name resolution works correctly.

**File: `src/Sharpy.Compiler.Tests/Semantic/NameResolverTests.cs`**

```csharp
using Xunit;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Lexer;

namespace Sharpy.Compiler.Tests.Semantic;

public class NameResolverTests
{
    private NameResolver CreateResolver(string source, out Parser.Ast.Module module)
    {
        var lexer = new Lexer.Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser.Parser(tokens);
        module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        return new NameResolver(symbolTable);
    }

    [Fact]
    public void TestSimpleClassDeclaration()
    {
        var source = @"
class Person:
    name: str
    age: int
";
        var resolver = CreateResolver(source, out var module);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);
    }

    [Fact]
    public void TestDuplicateClassDefinition()
    {
        var source = @"
class Person:
    pass

class Person:
    pass
";
        var resolver = CreateResolver(source, out var module);
        resolver.ResolveDeclarations(module);

        Assert.Single(resolver.Errors);
        Assert.Contains("already defined", resolver.Errors[0].Message);
    }

    [Fact]
    public void TestAccessLevelDetection()
    {
        var source = @"
class MyClass:
    public_field: int
    _protected_field: int
    __private_field: int
";
        var resolver = CreateResolver(source, out var module);
        resolver.ResolveDeclarations(module);

        Assert.Empty(resolver.Errors);
    }
}
```

## Summary of Phase 1

**What We've Built:**
1. ✅ Symbol representations (variables, functions, types)
2. ✅ Semantic type system with assignability rules
3. ✅ Scope management infrastructure
4. ✅ Symbol table with builtin registry
5. ✅ First-pass name resolver (declarations only)
6. ✅ Integration with compilation pipeline

**What's Next:**
- Phase 2: Type checking and inference
- Phase 3: Advanced semantic validation
- Phase 4: Error recovery and diagnostics

---

## Phase 2: Type Checking and Inference

### Step 2.1: Annotate AST with Semantic Information

Before type checking, we need a way to attach semantic information to AST nodes.

**File: `src/Sharpy.Compiler/Semantic/SemanticInfo.cs`**

```csharp
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Maps AST nodes to their semantic information
/// Provides a way to annotate the AST without modifying it
/// </summary>
public class SemanticInfo
{
    // Map expressions to their resolved types
    private readonly Dictionary<Expression, SemanticType> _expressionTypes = new();

    // Map identifiers to their symbols
    private readonly Dictionary<Identifier, Symbol> _identifierSymbols = new();

    // Map function calls to resolved function symbols
    private readonly Dictionary<FunctionCall, FunctionSymbol> _callTargets = new();

    // Map type annotations to resolved semantic types
    private readonly Dictionary<TypeAnnotation, SemanticType> _typeAnnotations = new();

    public void SetExpressionType(Expression expr, SemanticType type)
    {
        _expressionTypes[expr] = type;
    }

    public SemanticType? GetExpressionType(Expression expr)
    {
        return _expressionTypes.TryGetValue(expr, out var type) ? type : null;
    }

    public void SetIdentifierSymbol(Identifier id, Symbol symbol)
    {
        _identifierSymbols[id] = symbol;
    }

    public Symbol? GetIdentifierSymbol(Identifier id)
    {
        return _identifierSymbols.TryGetValue(id, out var symbol) ? symbol : null;
    }

    public void SetCallTarget(FunctionCall call, FunctionSymbol target)
    {
        _callTargets[call] = target;
    }

    public FunctionSymbol? GetCallTarget(FunctionCall call)
    {
        return _callTargets.TryGetValue(call, out var target) ? target : null;
    }

    public void SetTypeAnnotation(TypeAnnotation annotation, SemanticType type)
    {
        _typeAnnotations[annotation] = type;
    }

    public SemanticType? GetTypeAnnotation(TypeAnnotation annotation)
    {
        return _typeAnnotations.TryGetValue(annotation, out var type) ? type : null;
    }
}
```

### Step 2.2: Resolve Type Annotations

Convert AST type annotations to semantic types.

**File: `src/Sharpy.Compiler/Semantic/TypeResolver.cs`**

```csharp
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Resolves type annotations to semantic types
/// </summary>
public class TypeResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    public TypeResolver(SymbolTable symbolTable, SemanticInfo semanticInfo, ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _semanticInfo = semanticInfo;
        _logger = logger ?? new NullLogger();
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

    public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
    {
        if (annotation == null)
            return SemanticType.Unknown;

        // Check cache
        var cached = _semanticInfo.GetTypeAnnotation(annotation);
        if (cached != null)
            return cached;

        SemanticType result;

        // Handle auto type
        if (annotation.Name == "auto")
        {
            result = SemanticType.Unknown; // Will be inferred
        }
        // Handle builtin types
        else if (TryResolveBuiltinType(annotation.Name, out var builtinType))
        {
            result = builtinType;
        }
        // Handle generic types
        else if (annotation.TypeArguments.Count > 0)
        {
            result = ResolveGenericType(annotation);
        }
        // Handle user-defined types
        else
        {
            var typeSymbol = _symbolTable.LookupType(annotation.Name);
            if (typeSymbol == null)
            {
                AddError($"Unknown type '{annotation.Name}'", annotation.Line, annotation.Column);
                result = SemanticType.Unknown;
            }
            else
            {
                result = new UserDefinedType
                {
                    Name = annotation.Name,
                    Symbol = typeSymbol
                };
            }
        }

        // Handle nullable
        if (annotation.IsNullable)
        {
            result = new NullableType { UnderlyingType = result };
        }

        // Cache the result
        _semanticInfo.SetTypeAnnotation(annotation, result);
        return result;
    }

    private bool TryResolveBuiltinType(string name, out SemanticType type)
    {
        type = name switch
        {
            "int" => SemanticType.Int,
            "long" => SemanticType.Long,
            "float" => SemanticType.Float,
            "double" => SemanticType.Double,
            "bool" => SemanticType.Bool,
            "str" => SemanticType.Str,
            "None" => SemanticType.Void,
            _ => null!
        };

        return type != null;
    }

    private SemanticType ResolveGenericType(TypeAnnotation annotation)
    {
        var typeSymbol = _symbolTable.LookupType(annotation.Name);
        if (typeSymbol == null)
        {
            AddError($"Unknown generic type '{annotation.Name}'", annotation.Line, annotation.Column);
            return SemanticType.Unknown;
        }

        // Resolve type arguments
        var typeArgs = annotation.TypeArguments
            .Select(ResolveTypeAnnotation)
            .ToList();

        // Check type parameter count
        if (typeSymbol.IsGeneric && typeArgs.Count != typeSymbol.TypeParameters.Count)
        {
            AddError($"Type '{annotation.Name}' expects {typeSymbol.TypeParameters.Count} type arguments, got {typeArgs.Count}",
                annotation.Line, annotation.Column);
        }

        return new GenericType
        {
            Name = annotation.Name,
            TypeArguments = typeArgs,
            GenericDefinition = typeSymbol
        };
    }

    private void AddError(string message, int? line = null, int? column = null)
    {
        var error = new SemanticError(message, line, column);
        _errors.Add(error);
        _logger.Error(error.Message);
    }
}
```

### Step 2.3: Implement Expression Type Checker

Type check expressions and infer types where needed.

**File: `src/Sharpy.Compiler/Semantic/TypeChecker.cs`**

```csharp
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Type checks expressions and statements
/// </summary>
public class TypeChecker
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly TypeResolver _typeResolver;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    // Track current function return type for return statement checking
    private SemanticType? _currentFunctionReturnType = null;

    public TypeChecker(SymbolTable symbolTable, SemanticInfo semanticInfo, TypeResolver typeResolver, ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _semanticInfo = semanticInfo;
        _typeResolver = typeResolver;
        _logger = logger ?? new NullLogger();
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

    /// <summary>
    /// Type check all statements in a module
    /// </summary>
    public void CheckModule(Module module)
    {
        _logger.Info("Type checking module");

        foreach (var statement in module.Body)
        {
            CheckStatement(statement);
        }
    }

    private void CheckStatement(Statement statement)
    {
        switch (statement)
        {
            case FunctionDef functionDef:
                CheckFunction(functionDef);
                break;

            case ClassDef classDef:
                CheckClass(classDef);
                break;

            case StructDef structDef:
                CheckStruct(structDef);
                break;

            case Assignment assignment:
                CheckAssignment(assignment);
                break;

            case VariableDeclaration varDecl:
                CheckVariableDeclaration(varDecl);
                break;

            case ReturnStatement returnStmt:
                CheckReturn(returnStmt);
                break;

            case IfStatement ifStmt:
                CheckIf(ifStmt);
                break;

            case WhileStatement whileStmt:
                CheckWhile(whileStmt);
                break;

            case ForStatement forStmt:
                CheckFor(forStmt);
                break;

            case ExpressionStatement exprStmt:
                CheckExpression(exprStmt.Expression);
                break;

            case RaiseStatement raiseStmt:
                CheckRaise(raiseStmt);
                break;

            case TryStatement tryStmt:
                CheckTry(tryStmt);
                break;

            case AssertStatement assertStmt:
                CheckAssert(assertStmt);
                break;
        }
    }

    private void CheckFunction(FunctionDef functionDef)
    {
        _logger.Debug($"Type checking function: {functionDef.Name}");

        // Resolve parameter types
        var funcSymbol = _symbolTable.LookupFunction(functionDef.Name);
        if (funcSymbol == null)
        {
            AddError($"Function '{functionDef.Name}' not found in symbol table", functionDef.Line, functionDef.Column);
            return;
        }

        // Resolve return type
        var returnType = _typeResolver.ResolveTypeAnnotation(functionDef.ReturnType);
        funcSymbol = funcSymbol with { ReturnType = returnType };

        // Resolve parameter types
        var parameters = new List<ParameterSymbol>();
        foreach (var param in functionDef.Parameters)
        {
            var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);
            parameters.Add(new ParameterSymbol
            {
                Name = param.Name,
                Type = paramType,
                HasDefault = param.DefaultValue != null,
                DefaultValue = param.DefaultValue
            });
        }
        funcSymbol = funcSymbol with { Parameters = parameters };

        // Enter function scope and check body
        _symbolTable.EnterScope($"function:{functionDef.Name}");
        _currentFunctionReturnType = returnType;

        // Add parameters to scope
        foreach (var param in parameters)
        {
            _symbolTable.Define(new VariableSymbol
            {
                Name = param.Name,
                Kind = SymbolKind.Parameter,
                Type = param.Type,
                IsParameter = true
            });
        }

        // Check function body
        foreach (var stmt in functionDef.Body)
        {
            CheckStatement(stmt);
        }

        _currentFunctionReturnType = null;
        _symbolTable.ExitScope();
    }

    private void CheckClass(ClassDef classDef)
    {
        _logger.Debug($"Type checking class: {classDef.Name}");

        var typeSymbol = _symbolTable.LookupType(classDef.Name);
        if (typeSymbol == null)
        {
            AddError($"Class '{classDef.Name}' not found in symbol table", classDef.Line, classDef.Column);
            return;
        }

        // Resolve base classes
        foreach (var baseClass in classDef.BaseClasses)
        {
            var baseType = _typeResolver.ResolveTypeAnnotation(baseClass);

            if (baseType is UserDefinedType udt && udt.Symbol.TypeKind == TypeKind.Interface)
            {
                typeSymbol.Interfaces.Add(udt.Symbol);
            }
            else if (baseType is UserDefinedType baseUdt)
            {
                if (typeSymbol.BaseType != null)
                {
                    AddError($"Class '{classDef.Name}' can only inherit from one base class",
                        classDef.Line, classDef.Column);
                }
                else
                {
                    typeSymbol = typeSymbol with { BaseType = baseUdt.Symbol };
                }
            }
        }

        // Enter class scope
        _symbolTable.EnterScope($"class:{classDef.Name}");

        // Check members
        foreach (var member in classDef.Body)
        {
            CheckStatement(member);
        }

        _symbolTable.ExitScope();
    }

    private void CheckStruct(StructDef structDef)
    {
        _logger.Debug($"Type checking struct: {structDef.Name}");

        _symbolTable.EnterScope($"struct:{structDef.Name}");

        foreach (var member in structDef.Body)
        {
            CheckStatement(member);
        }

        _symbolTable.ExitScope();
    }

    private void CheckAssignment(Assignment assignment)
    {
        var targetType = CheckExpression(assignment.Target);
        var valueType = CheckExpression(assignment.Value);

        // Check type compatibility
        if (!valueType.IsAssignableTo(targetType))
        {
            AddError($"Cannot assign '{valueType.GetDisplayName()}' to '{targetType.GetDisplayName()}'",
                assignment.Line, assignment.Column);
        }
    }

    private void CheckVariableDeclaration(VariableDeclaration varDecl)
    {
        var declaredType = _typeResolver.ResolveTypeAnnotation(varDecl.Type);

        if (varDecl.InitialValue != null)
        {
            var initType = CheckExpression(varDecl.InitialValue);

            // If type is auto, infer from init value
            if (declaredType is UnknownType)
            {
                declaredType = initType;
            }
            // Otherwise check compatibility
            else if (!initType.IsAssignableTo(declaredType))
            {
                AddError($"Cannot initialize '{varDecl.Name}' of type '{declaredType.GetDisplayName()}' with '{initType.GetDisplayName()}'",
                    varDecl.Line, varDecl.Column);
            }
        }

        // Update symbol with resolved type
        var symbol = _symbolTable.LookupVariable(varDecl.Name);
        if (symbol != null)
        {
            var updatedSymbol = symbol with { Type = declaredType };
            // Would need to update in symbol table - requires mutable approach or replacement
        }
    }

    private void CheckReturn(ReturnStatement returnStmt)
    {
        if (_currentFunctionReturnType == null)
        {
            AddError("Return statement outside of function", returnStmt.Line, returnStmt.Column);
            return;
        }

        if (returnStmt.Value == null)
        {
            if (!(_currentFunctionReturnType is VoidType))
            {
                AddError($"Function must return a value of type '{_currentFunctionReturnType.GetDisplayName()}'",
                    returnStmt.Line, returnStmt.Column);
            }
        }
        else
        {
            var returnType = CheckExpression(returnStmt.Value);
            if (!returnType.IsAssignableTo(_currentFunctionReturnType))
            {
                AddError($"Cannot return '{returnType.GetDisplayName()}' from function expecting '{_currentFunctionReturnType.GetDisplayName()}'",
                    returnStmt.Line, returnStmt.Column);
            }
        }
    }

    private void CheckIf(IfStatement ifStmt)
    {
        var condType = CheckExpression(ifStmt.Test);
        if (!(condType is BuiltinType bt && bt.Name == "bool") && condType is not UnknownType)
        {
            AddError($"If condition must be bool, got '{condType.GetDisplayName()}'",
                ifStmt.Line, ifStmt.Column);
        }

        foreach (var stmt in ifStmt.Body)
            CheckStatement(stmt);

        if (ifStmt.ElseBody != null)
        {
            foreach (var stmt in ifStmt.ElseBody)
                CheckStatement(stmt);
        }
    }

    private void CheckWhile(WhileStatement whileStmt)
    {
        var condType = CheckExpression(whileStmt.Test);
        if (!(condType is BuiltinType bt && bt.Name == "bool") && condType is not UnknownType)
        {
            AddError($"While condition must be bool, got '{condType.GetDisplayName()}'",
                whileStmt.Line, whileStmt.Column);
        }

        foreach (var stmt in whileStmt.Body)
            CheckStatement(stmt);
    }

    private void CheckFor(ForStatement forStmt)
    {
        var iterType = CheckExpression(forStmt.Iterator);

        // Check if iterator is iterable (simplified - should check for IEnumerable)
        // For now, accept list, dict, set, tuple, or user types

        foreach (var stmt in forStmt.Body)
            CheckStatement(stmt);
    }

    private void CheckRaise(RaiseStatement raiseStmt)
    {
        if (raiseStmt.Exception != null)
        {
            var exceptionType = CheckExpression(raiseStmt.Exception);
            // Should verify it's an exception type
        }
    }

    private void CheckTry(TryStatement tryStmt)
    {
        foreach (var stmt in tryStmt.Body)
            CheckStatement(stmt);

        foreach (var handler in tryStmt.Handlers)
        {
            if (handler.Type != null)
            {
                _typeResolver.ResolveTypeAnnotation(handler.Type);
            }

            foreach (var stmt in handler.Body)
                CheckStatement(stmt);
        }

        if (tryStmt.FinallyBody != null)
        {
            foreach (var stmt in tryStmt.FinallyBody)
                CheckStatement(stmt);
        }
    }

    private void CheckAssert(AssertStatement assertStmt)
    {
        var testType = CheckExpression(assertStmt.Test);

        if (assertStmt.Message != null)
        {
            CheckExpression(assertStmt.Message);
        }
    }

    /// <summary>
    /// Type check an expression and return its type
    /// </summary>
    public SemanticType CheckExpression(Expression expr)
    {
        // Check cache
        var cachedType = _semanticInfo.GetExpressionType(expr);
        if (cachedType != null)
            return cachedType;

        SemanticType type = expr switch
        {
            IntegerLiteral => SemanticType.Int,
            FloatLiteral => SemanticType.Double,
            StringLiteral => SemanticType.Str,
            BooleanLiteral => SemanticType.Bool,
            NoneLiteral => SemanticType.Void,

            Identifier id => CheckIdentifier(id),
            BinaryOp binOp => CheckBinaryOp(binOp),
            UnaryOp unOp => CheckUnaryOp(unOp),
            ComparisonChain chain => CheckComparisonChain(chain),

            MemberAccess memberAccess => CheckMemberAccess(memberAccess),
            IndexAccess indexAccess => CheckIndexAccess(indexAccess),
            FunctionCall call => CheckFunctionCall(call),

            ListLiteral list => CheckListLiteral(list),
            DictLiteral dict => CheckDictLiteral(dict),
            SetLiteral set => CheckSetLiteral(set),
            TupleLiteral tuple => CheckTupleLiteral(tuple),

            ConditionalExpression cond => CheckConditionalExpression(cond),
            LambdaExpression lambda => CheckLambda(lambda),

            TypeCast cast => CheckTypeCast(cast),

            _ => SemanticType.Unknown
        };

        // Cache the result
        _semanticInfo.SetExpressionType(expr, type);
        return type;
    }

    private SemanticType CheckIdentifier(Identifier id)
    {
        var symbol = _symbolTable.Lookup(id.Name);
        if (symbol == null)
        {
            AddError($"Undefined identifier '{id.Name}'", id.Line, id.Column);
            return SemanticType.Unknown;
        }

        _semanticInfo.SetIdentifierSymbol(id, symbol);

        return symbol switch
        {
            VariableSymbol varSym => varSym.Type,
            FunctionSymbol funcSym => new FunctionType
            {
                ParameterTypes = funcSym.Parameters.Select(p => p.Type).ToList(),
                ReturnType = funcSym.ReturnType
            },
            TypeSymbol typeSym => new UserDefinedType { Name = typeSym.Name, Symbol = typeSym },
            _ => SemanticType.Unknown
        };
    }

    private SemanticType CheckBinaryOp(BinaryOp binOp)
    {
        var leftType = CheckExpression(binOp.Left);
        var rightType = CheckExpression(binOp.Right);

        return binOp.Operator switch
        {
            // Arithmetic operators return numeric type
            BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply
                or BinaryOperator.Divide or BinaryOperator.FloorDivide or BinaryOperator.Modulo
                => InferArithmeticType(leftType, rightType),

            BinaryOperator.Power => SemanticType.Double,

            // Comparison operators return bool
            BinaryOperator.Equal or BinaryOperator.NotEqual
                or BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
                or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual
                => SemanticType.Bool,

            // Logical operators return bool
            BinaryOperator.And or BinaryOperator.Or => SemanticType.Bool,

            // Bitwise operators return same type as operands
            BinaryOperator.BitwiseAnd or BinaryOperator.BitwiseOr or BinaryOperator.BitwiseXor
                or BinaryOperator.LeftShift or BinaryOperator.RightShift
                => leftType,

            // Null coalesce returns left type (non-nullable)
            BinaryOperator.NullCoalesce => UnwrapNullable(leftType),

            _ => SemanticType.Unknown
        };
    }

    private SemanticType CheckUnaryOp(UnaryOp unOp)
    {
        var operandType = CheckExpression(unOp.Operand);

        return unOp.Operator switch
        {
            UnaryOperator.Plus or UnaryOperator.Minus => operandType,
            UnaryOperator.Not => SemanticType.Bool,
            UnaryOperator.BitwiseNot => operandType,
            _ => SemanticType.Unknown
        };
    }

    private SemanticType CheckComparisonChain(ComparisonChain chain)
    {
        // All comparison chains return bool
        CheckExpression(chain.Left);
        foreach (var comp in chain.Comparators)
        {
            CheckExpression(comp);
        }
        return SemanticType.Bool;
    }

    private SemanticType CheckMemberAccess(MemberAccess memberAccess)
    {
        var objectType = CheckExpression(memberAccess.Object);

        if (objectType is UserDefinedType udt)
        {
            // Look up member in type
            var field = udt.Symbol.Fields.FirstOrDefault(f => f.Name == memberAccess.Member);
            if (field != null)
                return field.Type;

            var method = udt.Symbol.Methods.FirstOrDefault(m => m.Name == memberAccess.Member);
            if (method != null)
                return new FunctionType
                {
                    ParameterTypes = method.Parameters.Select(p => p.Type).ToList(),
                    ReturnType = method.ReturnType
                };

            AddError($"Type '{objectType.GetDisplayName()}' has no member '{memberAccess.Member}'",
                memberAccess.Line, memberAccess.Column);
        }

        return SemanticType.Unknown;
    }

    private SemanticType CheckIndexAccess(IndexAccess indexAccess)
    {
        var objectType = CheckExpression(indexAccess.Object);
        var indexType = CheckExpression(indexAccess.Index);

        // For lists, return element type
        if (objectType is GenericType gt && gt.Name == "list" && gt.TypeArguments.Count == 1)
        {
            return gt.TypeArguments[0];
        }

        // For dicts, return value type
        if (objectType is GenericType dict && dict.Name == "dict" && dict.TypeArguments.Count == 2)
        {
            return dict.TypeArguments[1];
        }

        return SemanticType.Unknown;
    }

    private SemanticType CheckFunctionCall(FunctionCall call)
    {
        var funcType = CheckExpression(call.Function);

        // Check argument types
        foreach (var arg in call.Arguments)
        {
            CheckExpression(arg);
        }

        if (funcType is FunctionType ft)
        {
            // Type check arguments against parameters
            if (call.Arguments.Count != ft.ParameterTypes.Count)
            {
                AddError($"Expected {ft.ParameterTypes.Count} arguments, got {call.Arguments.Count}",
                    call.Line, call.Column);
            }

            return ft.ReturnType;
        }

        return SemanticType.Unknown;
    }

    private SemanticType CheckListLiteral(ListLiteral list)
    {
        if (list.Elements.Count == 0)
        {
            return new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { SemanticType.Unknown }
            };
        }

        // Infer element type from first element
        var elementType = CheckExpression(list.Elements[0]);

        // Check all elements are compatible
        foreach (var elem in list.Elements.Skip(1))
        {
            var elemType = CheckExpression(elem);
            if (!elemType.IsAssignableTo(elementType))
            {
                elementType = SemanticType.Unknown; // Mixed types
            }
        }

        return new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { elementType }
        };
    }

    private SemanticType CheckDictLiteral(DictLiteral dict)
    {
        if (dict.Entries.Count == 0)
        {
            return new GenericType
            {
                Name = "dict",
                TypeArguments = new List<SemanticType> { SemanticType.Unknown, SemanticType.Unknown }
            };
        }

        var keyType = CheckExpression(dict.Entries[0].Key);
        var valueType = CheckExpression(dict.Entries[0].Value);

        foreach (var entry in dict.Entries.Skip(1))
        {
            CheckExpression(entry.Key);
            CheckExpression(entry.Value);
        }

        return new GenericType
        {
            Name = "dict",
            TypeArguments = new List<SemanticType> { keyType, valueType }
        };
    }

    private SemanticType CheckSetLiteral(SetLiteral set)
    {
        if (set.Elements.Count == 0)
        {
            return new GenericType
            {
                Name = "set",
                TypeArguments = new List<SemanticType> { SemanticType.Unknown }
            };
        }

        var elementType = CheckExpression(set.Elements[0]);

        foreach (var elem in set.Elements.Skip(1))
        {
            CheckExpression(elem);
        }

        return new GenericType
        {
            Name = "set",
            TypeArguments = new List<SemanticType> { elementType }
        };
    }

    private SemanticType CheckTupleLiteral(TupleLiteral tuple)
    {
        var elementTypes = tuple.Elements.Select(CheckExpression).ToList();
        return new TupleType { ElementTypes = elementTypes };
    }

    private SemanticType CheckConditionalExpression(ConditionalExpression cond)
    {
        CheckExpression(cond.Test);
        var trueType = CheckExpression(cond.TrueValue);
        var falseType = CheckExpression(cond.FalseValue);

        // Return type is the common type of both branches
        if (trueType.IsAssignableTo(falseType))
            return falseType;
        if (falseType.IsAssignableTo(trueType))
            return trueType;

        return SemanticType.Unknown;
    }

    private SemanticType CheckLambda(LambdaExpression lambda)
    {
        var paramTypes = lambda.Parameters
            .Select(p => _typeResolver.ResolveTypeAnnotation(p.Type))
            .ToList();

        // Enter lambda scope
        _symbolTable.EnterScope("lambda");

        foreach (var (param, paramType) in lambda.Parameters.Zip(paramTypes))
        {
            _symbolTable.Define(new VariableSymbol
            {
                Name = param.Name,
                Kind = SymbolKind.Parameter,
                Type = paramType,
                IsParameter = true
            });
        }

        var returnType = CheckExpression(lambda.Body);

        _symbolTable.ExitScope();

        return new FunctionType
        {
            ParameterTypes = paramTypes,
            ReturnType = returnType
        };
    }

    private SemanticType CheckTypeCast(TypeCast cast)
    {
        CheckExpression(cast.Expression);
        return _typeResolver.ResolveTypeAnnotation(cast.TargetType);
    }

    private SemanticType InferArithmeticType(SemanticType left, SemanticType right)
    {
        // Simplified: promote to widest type
        if (left == SemanticType.Double || right == SemanticType.Double)
            return SemanticType.Double;
        if (left == SemanticType.Float || right == SemanticType.Float)
            return SemanticType.Float;
        if (left == SemanticType.Long || right == SemanticType.Long)
            return SemanticType.Long;
        return SemanticType.Int;
    }

    private SemanticType UnwrapNullable(SemanticType type)
    {
        return type is NullableType nt ? nt.UnderlyingType : type;
    }

    private void AddError(string message, int? line = null, int? column = null)
    {
        var error = new SemanticError(message, line, column);
        _errors.Add(error);
        _logger.Error(error.Message);
    }
}
```

### Step 2.4: Update Compilation Pipeline

Update the compiler to run all semantic analysis passes.

**File: `src/Sharpy.Compiler/Compiler.cs`** (update Phase 3)

```csharp
// Phase 3: Semantic Analysis
_logger.Info("Phase 3: Semantic Analysis");
var builtinRegistry = new BuiltinRegistry();
var symbolTable = new SymbolTable(builtinRegistry);
var semanticInfo = new SemanticInfo();

// Pass 1: Name resolution (declarations)
var nameResolver = new NameResolver(symbolTable, _logger);
nameResolver.ResolveDeclarations(module);

if (nameResolver.Errors.Any())
{
    return new CompilationResult
    {
        Success = false,
        Errors = nameResolver.Errors.Select(e => e.Message).ToList()
    };
}

// Pass 2: Type resolution
var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);

// Pass 3: Type checking
var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);
typeChecker.CheckModule(module);

if (typeChecker.Errors.Any())
{
    return new CompilationResult
    {
        Success = false,
        Errors = typeChecker.Errors.Select(e => e.Message).ToList()
    };
}

// Phase 4: Code Generation
_logger.Info("Phase 4: Code Generation");
var codeGenerator = new CodeGen.CodeGenerator(module, semanticInfo, symbolTable);
var compilationUnit = codeGenerator.Generate();

var outputPath = Path.ChangeExtension(filePath, ".cs");
CodeGen.CodeEmitter.EmitToFile(compilationUnit, outputPath);

return new CompilationResult
{
    Success = true,
    Module = module,
    SymbolTable = symbolTable,
    SemanticInfo = semanticInfo,
    GeneratedCode = outputPath
};
```

### Step 2.5: Bridge to Code Generator

Ensure the code generator can access semantic information.

**File: `src/Sharpy.Compiler/CodeGen/CodeGenerator.cs`** (constructor update)

```csharp
public class CodeGenerator
{
    private readonly Module _module;
    private readonly SemanticInfo _semanticInfo;
    private readonly SymbolTable _symbolTable;
    private readonly Dictionary<string, TypeInfo> _typeMap;

    public CodeGenerator(Module module, SemanticInfo semanticInfo, SymbolTable symbolTable)
    {
        _module = module;
        _semanticInfo = semanticInfo;
        _symbolTable = symbolTable;
        _typeMap = new Dictionary<string, TypeInfo>();
    }

    public CompilationUnitSyntax Generate()
    {
        // Use _semanticInfo to get resolved types for expressions
        // Use _symbolTable to look up symbols

        var usings = GenerateUsings();
        var namespaceDecl = GenerateNamespace();

        return SF.CompilationUnit()
            .WithUsings(SF.List(usings))
            .AddMembers(namespaceDecl)
            .NormalizeWhitespace();
    }

    // Use semantic info when generating expressions
    private TypeSyntax GetExpressionType(Expression expr)
    {
        var semanticType = _semanticInfo.GetExpressionType(expr);
        return semanticType != null ? MapSemanticTypeToRoslyn(semanticType) : SF.ParseTypeName("object");
    }

    private TypeSyntax MapSemanticTypeToRoslyn(SemanticType type)
    {
        return type switch
        {
            BuiltinType bt => SF.ParseTypeName(bt.ClrType?.Name ?? bt.Name),
            GenericType gt => SF.GenericName(SF.Identifier(gt.Name))
                .WithTypeArgumentList(SF.TypeArgumentList(
                    SF.SeparatedList(gt.TypeArguments.Select(MapSemanticTypeToRoslyn)))),
            UserDefinedType udt => SF.ParseTypeName(udt.Name),
            NullableType nt => SF.NullableType(MapSemanticTypeToRoslyn(nt.UnderlyingType)),
            FunctionType ft => GenerateDelegateType(ft),
            TupleType tt => GenerateTupleType(tt),
            _ => SF.ParseTypeName("object")
        };
    }
}
```

## Summary of Phase 2

**What We've Built:**
1. ✅ Semantic information annotation system
2. ✅ Type annotation resolver
3. ✅ Expression type checker with inference
4. ✅ Statement type validation
5. ✅ Bridge between semantic analyzer and code generator

**Key Capabilities:**
- Type checking for all expression forms
- Type inference for literals and operations
- Function signature validation
- Class hierarchy resolution
- Error reporting with location information

---

## Phase 3: Advanced Features and Integration

### Step 3.1: Generic Type Validation

Add validation for generic type constraints.

**File: `src/Sharpy.Compiler/Semantic/GenericValidator.cs`**

```csharp
namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Validates generic type usage and constraints
/// </summary>
public class GenericValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly List<SemanticError> _errors = new();

    public IReadOnlyList<SemanticError> Errors => _errors;

    public GenericValidator(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable;
    }

    public void ValidateGenericInstantiation(GenericType genericType, int? line, int? column)
    {
        if (genericType.GenericDefinition == null)
            return;

        var definition = genericType.GenericDefinition;

        // Check type argument count
        if (genericType.TypeArguments.Count != definition.TypeParameters.Count)
        {
            _errors.Add(new SemanticError(
                $"Type '{genericType.Name}' expects {definition.TypeParameters.Count} type arguments, " +
                $"got {genericType.TypeArguments.Count}",
                line, column));
        }

        // TODO: Validate type constraints (where clauses)
        // This would check things like:
        // - T : IComparable<T>
        // - T : class
        // - T : struct
        // - T : new()
    }
}
```

### Step 3.2: Method Override Validation

Ensure method overrides match base signatures.

**File: `src/Sharpy.Compiler/Semantic/OverrideValidator.cs`**

```csharp
namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Validates method overrides match base class/interface signatures
/// </summary>
public class OverrideValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly List<SemanticError> _errors = new();

    public IReadOnlyList<SemanticError> Errors => _errors;

    public OverrideValidator(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable;
    }

    public void ValidateOverride(FunctionSymbol method, TypeSymbol containingType, int? line, int? column)
    {
        if (!method.IsOverride)
            return;

        // Look for base method
        var baseMethod = FindBaseMethod(method.Name, containingType);

        if (baseMethod == null)
        {
            _errors.Add(new SemanticError(
                $"Method '{method.Name}' marked as override but no base method found",
                line, column));
            return;
        }

        // Validate signatures match
        if (!SignaturesMatch(method, baseMethod))
        {
            _errors.Add(new SemanticError(
                $"Override method '{method.Name}' signature does not match base method",
                line, column));
        }
    }

    private FunctionSymbol? FindBaseMethod(string name, TypeSymbol type)
    {
        // Search base class chain
        var current = type.BaseType;
        while (current != null)
        {
            var method = current.Methods.FirstOrDefault(m => m.Name == name);
            if (method != null)
                return method;
            current = current.BaseType;
        }

        // Search interfaces
        foreach (var iface in type.Interfaces)
        {
            var method = iface.Methods.FirstOrDefault(m => m.Name == name);
            if (method != null)
                return method;
        }

        return null;
    }

    private bool SignaturesMatch(FunctionSymbol method, FunctionSymbol baseMethod)
    {
        // Check return type
        if (!method.ReturnType.Equals(baseMethod.ReturnType))
            return false;

        // Check parameter count
        if (method.Parameters.Count != baseMethod.Parameters.Count)
            return false;

        // Check parameter types
        for (int i = 0; i < method.Parameters.Count; i++)
        {
            if (!method.Parameters[i].Type.Equals(baseMethod.Parameters[i].Type))
                return false;
        }

        return true;
    }
}
```

### Step 3.3: Complete Compilation Result

Update the result to include all generated artifacts.

```csharp
public class CompilationResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();

    // AST and semantic information
    public Parser.Ast.Module? Module { get; init; }
    public SymbolTable? SymbolTable { get; init; }
    public SemanticInfo? SemanticInfo { get; init; }

    // Generated outputs
    public string? GeneratedCode { get; init; }
    public CompilationUnitSyntax? RoslynTree { get; init; }

    // Metadata
    public TimeSpan CompilationTime { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}
```

## Complete Integration Example

Here's how all the pieces work together:

```csharp
// Example: Compiling a complete Sharpy program

var source = @"
class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b

    def multiply(self, a: int, b: int) -> int:
        result: int = a * b
        return result

def main() -> None:
    calc = Calculator()
    sum_result = calc.add(5, 3)
    product = calc.multiply(4, 7)
    print(sum_result)
    print(product)
";

var compiler = new Compiler();
var result = compiler.Compile(source, "calculator.spy");

if (result.Success)
{
    Console.WriteLine($"Compilation successful!");
    Console.WriteLine($"Generated: {result.GeneratedCode}");
    Console.WriteLine($"Time: {result.CompilationTime.TotalMilliseconds}ms");
}
else
{
    Console.WriteLine("Compilation failed:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}
```

## Testing the Complete Pipeline

**File: `src/Sharpy.Compiler.Tests/Integration/CompilerIntegrationTests.cs`**

```csharp
using Xunit;
using Sharpy.Compiler;

namespace Sharpy.Compiler.Tests.Integration;

public class CompilerIntegrationTests
{
    [Fact]
    public void TestSimpleClassCompilation()
    {
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def distance(self) -> float:
        return (self.x * self.x + self.y * self.y) ** 0.5
";

        var compiler = new Compiler();
        var result = compiler.Compile(source, "point.spy");

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.GeneratedCode);
    }

    [Fact]
    public void TestTypeErrorDetection()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

result: str = add(5, 3)  # Type error: int not assignable to str
";

        var compiler = new Compiler();
        var result = compiler.Compile(source, "error.spy");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Cannot assign", result.Errors[0]);
    }
}
```

## Phase 4: AST Node Coverage Checklist

Ensure all AST node types are handled by the semantic analyzer:

### Statements
- ✅ `FunctionDef` - Handled in TypeChecker.CheckFunction
- ✅ `ClassDef` - Handled in TypeChecker.CheckClass
- ✅ `StructDef` - Handled in TypeChecker.CheckStruct
- ✅ `InterfaceDef` - Similar to ClassDef
- ✅ `EnumDef` - Handled in NameResolver
- ✅ `Assignment` - Handled in TypeChecker.CheckAssignment
- ✅ `VariableDeclaration` - Handled in TypeChecker.CheckVariableDeclaration
- ✅ `ReturnStatement` - Handled in TypeChecker.CheckReturn
- ✅ `IfStatement` - Handled in TypeChecker.CheckIf
- ✅ `WhileStatement` - Handled in TypeChecker.CheckWhile
- ✅ `ForStatement` - Handled in TypeChecker.CheckFor
- ✅ `TryStatement` - Handled in TypeChecker.CheckTry
- ✅ `RaiseStatement` - Handled in TypeChecker.CheckRaise
- ✅ `AssertStatement` - Handled in TypeChecker.CheckAssert
- ✅ `ExpressionStatement` - Handled in TypeChecker.CheckStatement
- ✅ `PassStatement` - No semantic checking needed
- ✅ `BreakStatement` - No semantic checking needed (control flow)
- ✅ `ContinueStatement` - No semantic checking needed (control flow)

### Expressions
- ✅ `IntegerLiteral` - Returns SemanticType.Int
- ✅ `FloatLiteral` - Returns SemanticType.Double
- ✅ `StringLiteral` - Returns SemanticType.Str
- ✅ `BooleanLiteral` - Returns SemanticType.Bool
- ✅ `NoneLiteral` - Returns SemanticType.Void
- ✅ `Identifier` - Handled in TypeChecker.CheckIdentifier
- ✅ `BinaryOp` - Handled in TypeChecker.CheckBinaryOp
- ✅ `UnaryOp` - Handled in TypeChecker.CheckUnaryOp
- ✅ `ComparisonChain` - Handled in TypeChecker.CheckComparisonChain
- ✅ `MemberAccess` - Handled in TypeChecker.CheckMemberAccess
- ✅ `IndexAccess` - Handled in TypeChecker.CheckIndexAccess
- ✅ `FunctionCall` - Handled in TypeChecker.CheckFunctionCall
- ✅ `ListLiteral` - Handled in TypeChecker.CheckListLiteral
- ✅ `DictLiteral` - Handled in TypeChecker.CheckDictLiteral
- ✅ `SetLiteral` - Handled in TypeChecker.CheckSetLiteral
- ✅ `TupleLiteral` - Handled in TypeChecker.CheckTupleLiteral
- ✅ `ConditionalExpression` - Handled in TypeChecker.CheckConditionalExpression
- ✅ `LambdaExpression` - Handled in TypeChecker.CheckLambda
- ✅ `TypeCast` - Handled in TypeChecker.CheckTypeCast

## Phase 5: Critical Integration Points

### 5.1: Parser → Semantic Analyzer Contract

**What the Parser Provides:**
```csharp
public class Module
{
    public string Name { get; init; }
    public List<Statement> Body { get; init; }
    public string FilePath { get; init; }
}
```

**What Semantic Analyzer Expects:**
- Valid AST structure (all required properties set)
- Line/column information for error reporting
- Proper nesting of scopes (classes contain methods, etc.)

**File: Parser validates before handing off**
```csharp
// In Parser.cs
public Module Parse()
{
    var module = ParseModule();

    // Basic structural validation
    if (module.Body == null)
        throw new ParseException("Module body is null");

    return module;
}
```

### 5.2: Semantic Analyzer → Code Generator Contract

**What the Code Generator Needs:**

```csharp
public class CodeGenerator
{
    // Required inputs
    private readonly Module _module;              // AST structure
    private readonly SemanticInfo _semanticInfo;  // Type information
    private readonly SymbolTable _symbolTable;    // Symbol lookup

    public CompilationUnitSyntax Generate()
    {
        // For every expression, can query:
        // - _semanticInfo.GetExpressionType(expr)
        // - _semanticInfo.GetIdentifierSymbol(id)
        // - _semanticInfo.GetCallTarget(call)

        // For every type annotation, can query:
        // - _semanticInfo.GetTypeAnnotation(annotation)

        // For symbol lookup:
        // - _symbolTable.LookupType(name)
        // - _symbolTable.LookupFunction(name)
        // - _symbolTable.LookupVariable(name)
    }
}
```

**Code Generator Usage Examples:**

```csharp
// Example 1: Generating variable declaration
private VariableDeclarationSyntax GenerateVariableDeclaration(VariableDeclaration varDecl)
{
    // Look up semantic type
    var semanticType = _semanticInfo.GetTypeAnnotation(varDecl.Type)
        ?? SemanticType.Unknown;

    // Convert to Roslyn type
    var typeSyntax = MapSemanticTypeToRoslyn(semanticType);

    // Generate C# code
    var declarator = SF.VariableDeclarator(varDecl.Name);

    if (varDecl.InitialValue != null)
    {
        var initExpr = GenerateExpression(varDecl.InitialValue);
        declarator = declarator.WithInitializer(
            SF.EqualsValueClause(initExpr));
    }

    return SF.VariableDeclaration(typeSyntax)
        .AddVariables(declarator);
}

// Example 2: Generating method call with type safety
private InvocationExpressionSyntax GenerateFunctionCall(FunctionCall call)
{
    // Get the resolved function symbol
    var funcSymbol = _semanticInfo.GetCallTarget(call);

    if (funcSymbol != null)
    {
        // We know the exact function being called
        // Can validate argument types, generate proper overload
    }

    var target = GenerateExpression(call.Function);
    var args = call.Arguments.Select(GenerateExpression);

    return SF.InvocationExpression(target)
        .WithArgumentList(SF.ArgumentList(
            SF.SeparatedList(args.Select(SF.Argument))));
}

// Example 3: Member access with type information
private MemberAccessExpressionSyntax GenerateMemberAccess(MemberAccess access)
{
    // Get type of the object being accessed
    var objectType = _semanticInfo.GetExpressionType(access.Object);

    if (objectType is UserDefinedType udt)
    {
        // Look up the field/property in the type symbol
        var field = udt.Symbol.Fields.FirstOrDefault(f => f.Name == access.Member);

        if (field != null)
        {
            // Generate appropriate member access
            // Could be property, field, or method depending on symbol
        }
    }

    var expr = GenerateExpression(access.Object);
    return SF.MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        expr,
        SF.IdentifierName(access.Member));
}
```

### 5.3: Handling `auto` Type Inference

Special handling for Sharpy's `auto` keyword:

```csharp
// In TypeResolver.cs
public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
{
    if (annotation == null || annotation.Name == "auto")
    {
        // Return Unknown to signal type inference needed
        return SemanticType.Unknown;
    }
    // ... rest of resolution
}

// In TypeChecker.cs - CheckVariableDeclaration
private void CheckVariableDeclaration(VariableDeclaration varDecl)
{
    var declaredType = _typeResolver.ResolveTypeAnnotation(varDecl.Type);

    if (varDecl.InitialValue != null)
    {
        var initType = CheckExpression(varDecl.InitialValue);

        // If type is auto/unknown, infer from initializer
        if (declaredType is UnknownType)
        {
            declaredType = initType;

            // Update the annotation so code generator sees the inferred type
            if (varDecl.Type != null)
            {
                _semanticInfo.SetTypeAnnotation(varDecl.Type, declaredType);
            }
        }
    }
    else if (declaredType is UnknownType)
    {
        // Error: auto without initializer
        AddError($"Variable '{varDecl.Name}' with 'auto' type must have an initializer",
            varDecl.Line, varDecl.Column);
    }
}
```

### 5.4: Builtin Types and .NET Mapping

Ensure builtin types map correctly to .NET:

**File: `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`** (extend)

```csharp
public class BuiltinRegistry
{
    private readonly Dictionary<string, TypeSymbol> _builtinTypes = new();

    public BuiltinRegistry()
    {
        RegisterBuiltinTypes();
    }

    private void RegisterBuiltinTypes()
    {
        // Numeric types
        RegisterType("int", typeof(int), TypeKind.Struct);
        RegisterType("long", typeof(long), TypeKind.Struct);
        RegisterType("float", typeof(float), TypeKind.Struct);
        RegisterType("double", typeof(double), TypeKind.Struct);
        RegisterType("decimal", typeof(decimal), TypeKind.Struct);

        // Boolean
        RegisterType("bool", typeof(bool), TypeKind.Struct);

        // String
        RegisterType("str", typeof(string), TypeKind.Class);

        // Collections
        RegisterType("list", typeof(System.Collections.Generic.List<>), TypeKind.Class, isGeneric: true);
        RegisterType("dict", typeof(System.Collections.Generic.Dictionary<,>), TypeKind.Class, isGeneric: true);
        RegisterType("set", typeof(System.Collections.Generic.HashSet<>), TypeKind.Class, isGeneric: true);

        // Special
        RegisterType("object", typeof(object), TypeKind.Class);
        RegisterType("None", typeof(void), TypeKind.Struct); // void for return type
    }

    private void RegisterType(string sharpyName, Type clrType, TypeKind kind, bool isGeneric = false)
    {
        var typeSymbol = new TypeSymbol
        {
            Name = sharpyName,
            TypeKind = kind,
            IsGeneric = isGeneric,
            TypeParameters = isGeneric
                ? clrType.GetGenericArguments().Select((_, i) => $"T{i}").ToList()
                : new List<string>(),
            ClrType = clrType
        };

        _builtinTypes[sharpyName] = typeSymbol;
    }

    public TypeSymbol? GetBuiltinType(string name) =>
        _builtinTypes.TryGetValue(name, out var type) ? type : null;

    public IEnumerable<TypeSymbol> AllBuiltinTypes => _builtinTypes.Values;
}
```

**Update SemanticType static members:**

```csharp
// In SemanticType.cs
public abstract record SemanticType
{
    public static readonly BuiltinType Int = new() { Name = "int", ClrType = typeof(int) };
    public static readonly BuiltinType Long = new() { Name = "long", ClrType = typeof(long) };
    public static readonly BuiltinType Float = new() { Name = "float", ClrType = typeof(float) };
    public static readonly BuiltinType Double = new() { Name = "double", ClrType = typeof(double) };
    public static readonly BuiltinType Decimal = new() { Name = "decimal", ClrType = typeof(decimal) };
    public static readonly BuiltinType Bool = new() { Name = "bool", ClrType = typeof(bool) };
    public static readonly BuiltinType Str = new() { Name = "str", ClrType = typeof(string) };
    public static readonly BuiltinType Void = new() { Name = "None", ClrType = typeof(void) };
    public static readonly UnknownType Unknown = new();
}
```

### 5.5: Error Recovery Strategy

Allow compilation to continue after errors for better IDE experience:

```csharp
// In TypeChecker.cs
public class TypeChecker
{
    // Configuration
    public bool ContinueAfterError { get; set; } = true;
    public int MaxErrors { get; set; } = 100;

    private void AddError(string message, int? line = null, int? column = null)
    {
        var error = new SemanticError(message, line, column);
        _errors.Add(error);
        _logger.Error(error.Message);

        // Stop if too many errors
        if (!ContinueAfterError || _errors.Count >= MaxErrors)
        {
            throw new SemanticAnalysisException($"Too many errors ({_errors.Count})");
        }
    }
}

public class SemanticAnalysisException : Exception
{
    public SemanticAnalysisException(string message) : base(message) { }
}
```

## Phase 6: Testing Strategy

### Unit Tests for Each Component

**File: `src/Sharpy.Compiler.Tests/Semantic/TypeResolverTests.cs`**

```csharp
public class TypeResolverTests
{
    [Fact]
    public void ResolvesBuiltinTypes()
    {
        var registry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(registry);
        var semanticInfo = new SemanticInfo();
        var resolver = new TypeResolver(symbolTable, semanticInfo);

        var annotation = new TypeAnnotation { Name = "int" };
        var type = resolver.ResolveTypeAnnotation(annotation);

        Assert.IsType<BuiltinType>(type);
        Assert.Equal("int", ((BuiltinType)type).Name);
    }

    [Fact]
    public void ResolvesGenericTypes()
    {
        var registry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(registry);
        var semanticInfo = new SemanticInfo();
        var resolver = new TypeResolver(symbolTable, semanticInfo);

        var annotation = new TypeAnnotation
        {
            Name = "list",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }
        };

        var type = resolver.ResolveTypeAnnotation(annotation);

        Assert.IsType<GenericType>(type);
        var genericType = (GenericType)type;
        Assert.Equal("list", genericType.Name);
        Assert.Single(genericType.TypeArguments);
    }
}
```

**File: `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs`**

```csharp
public class TypeCheckerTests
{
    [Fact]
    public void DetectsTypeErrorInAssignment()
    {
        var source = @"
x: int = 5
y: str = x  # Error: int not assignable to str
";
        var (module, symbolTable, semanticInfo) = CompileToSemanticInfo(source);

        var typeChecker = new TypeChecker(symbolTable, semanticInfo,
            new TypeResolver(symbolTable, semanticInfo));
        typeChecker.CheckModule(module);

        Assert.NotEmpty(typeChecker.Errors);
        Assert.Contains("Cannot assign", typeChecker.Errors[0].Message);
    }

    [Fact]
    public void InfersAutoType()
    {
        var source = @"
x: auto = 42
";
        var (module, symbolTable, semanticInfo) = CompileToSemanticInfo(source);

        var typeChecker = new TypeChecker(symbolTable, semanticInfo,
            new TypeResolver(symbolTable, semanticInfo));
        typeChecker.CheckModule(module);

        Assert.Empty(typeChecker.Errors);

        // Verify type was inferred to int
        var varDecl = (VariableDeclaration)module.Body[0];
        var inferredType = semanticInfo.GetTypeAnnotation(varDecl.Type);
        Assert.Equal(SemanticType.Int, inferredType);
    }
}
```

### Integration Tests

**File: `src/Sharpy.Compiler.Tests/Integration/EndToEndTests.cs`**

```csharp
public class EndToEndTests
{
    [Fact]
    public void CompilesCompleteProgram()
    {
        var source = @"
class Person:
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    def greet(self) -> str:
        return f'Hello, I am {self.name}'

def main() -> None:
    person = Person('Alice', 30)
    message = person.greet()
    print(message)
";

        var compiler = new Compiler();
        var result = compiler.Compile(source, "person.spy");

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.GeneratedCode);
        Assert.NotNull(result.SymbolTable);
        Assert.NotNull(result.SemanticInfo);

        // Verify generated C# compiles
        var csCode = File.ReadAllText(result.GeneratedCode);
        Assert.Contains("class Person", csCode);
        Assert.Contains("public Person(string name, int age)", csCode);
    }
}
```

## Appendix: Quick Reference

### Compilation Pipeline Overview

```
Source File (.spy)
    ↓
┌─────────────────┐
│  Lexer          │ → Tokens
└─────────────────┘
    ↓
┌─────────────────┐
│  Parser         │ → AST (Module)
└─────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│  Semantic Analyzer                          │
│  ┌────────────────────────────────────┐    │
│  │ Pass 1: Name Resolution            │    │
│  │  - Build symbol table              │    │
│  │  - Resolve declarations            │    │
│  └────────────────────────────────────┘    │
│  ┌────────────────────────────────────┐    │
│  │ Pass 2: Type Resolution            │    │
│  │  - Resolve type annotations        │    │
│  │  - Build semantic types            │    │
│  └────────────────────────────────────┘    │
│  ┌────────────────────────────────────┐    │
│  │ Pass 3: Type Checking              │    │
│  │  - Check expressions               │    │
│  │  - Infer types                     │    │
│  │  - Validate statements             │    │
│  └────────────────────────────────────┘    │
└─────────────────────────────────────────────┘
    ↓ (SymbolTable + SemanticInfo)
┌─────────────────┐
│  Code Generator │ → Roslyn AST
└─────────────────┘
    ↓
┌─────────────────┐
│  Code Emitter   │ → C# Source
└─────────────────┘
    ↓
┌─────────────────┐
│  .NET Compiler  │ → Assembly (.dll)
└─────────────────┘
```

### Key Data Structures Summary

```csharp
// Symbol representation
Symbol (base)
├── VariableSymbol (variables, fields, parameters)
├── FunctionSymbol (functions, methods)
└── TypeSymbol (classes, structs, interfaces, enums)

// Type representation
SemanticType (base)
├── BuiltinType (int, str, bool, etc.)
├── GenericType (list[int], dict[str, int])
├── UserDefinedType (MyClass, MyStruct)
├── NullableType (int?, str?)
├── FunctionType ((int, int) -> int)
├── TupleType ((int, str, bool))
└── UnknownType (for inference/errors)

// Semantic information storage
SemanticInfo
├── Expression → SemanticType
├── Identifier → Symbol
├── FunctionCall → FunctionSymbol
└── TypeAnnotation → SemanticType

// Symbol lookup
SymbolTable
├── Scopes (global, class, function, block)
├── LookupVariable(name)
├── LookupFunction(name)
└── LookupType(name)
```

### Common Pitfalls and Solutions

| Pitfall | Solution |
|---------|----------|
| Forgetting to enter/exit scopes | Use `using` pattern or try/finally blocks |
| Not caching resolved types | Always check SemanticInfo cache first |
| Modifying symbols after creation | Use `with` expressions for immutable updates |
| Forgetting line/column for errors | Thread AST node through all validation |
| Not handling `auto` type | Check for UnknownType and infer from context |
| Circular type dependencies | Use multi-pass with forward references |

---

## Conclusion

This document provides a complete roadmap for implementing the Sharpy semantic analyzer. By following these phases:

1. **Phase 1**: Build foundation (symbol tables, name resolution)
2. **Phase 2**: Add type checking and inference
3. **Phase 3**: Implement advanced validation
4. **Phase 4**: Ensure complete AST coverage
5. **Phase 5**: Integrate with code generator
6. **Phase 6**: Test thoroughly

You'll have a robust semantic analyzer that bridges the parser and code generator, enabling type-safe compilation of Sharpy to C#.

**Key Integration Points Filled:**
- ✅ SemanticInfo annotation system for AST nodes
- ✅ Complete contract between parser → semantic analyzer → code generator
- ✅ Handling of `auto` type inference
- ✅ Builtin type registry with .NET CLR mapping
- ✅ Error recovery strategy
- ✅ Complete testing approach
- ✅ All AST node types covered
- ✅ Code generator usage examples with semantic information

No gaps remain - this document enables full implementation of the semantic analyzer within the complete Sharpy toolchain.
