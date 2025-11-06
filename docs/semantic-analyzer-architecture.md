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

(To be continued in next response - this establishes the foundation)

The document will continue with:
- Step 2.1: Type annotation resolution
- Step 2.2: Expression type checking
- Step 2.3: Type inference for variables
- Step 2.4: Function signature checking
- Step 2.5: Generic type resolution
- And so on...

Would you like me to continue with Phase 2 now, or would you like to review Phase 1 first?
