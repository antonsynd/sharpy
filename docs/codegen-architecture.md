# Code Generation Architecture

## Overview

This document describes the architecture for generating C# code from Sharpy's Abstract Syntax Tree (AST). The code generator transforms the parsed AST into C# source code that can be compiled by the standard .NET compiler.

**Pipeline Overview:**
```
Sharpy Source (.spy)
    ↓
Lexer (Tokens)
    ↓
Parser (Sharpy AST)
    ↓
Semantic Analyzer (Type-checked AST)
    ↓
Code Generator (C# AST via Roslyn)
    ↓
C# Code Emitter (C# Source)
    ↓
.NET Compiler (IL/Assembly)
```

## Design Principles

1. **Roslyn-based Generation**: Use Roslyn's SyntaxFactory to build well-formed C# syntax trees
2. **Two-Pass Generation**: First pass for type resolution, second pass for code emission
3. **Name Transformation**: Convert Python naming conventions to C# conventions
4. **Type Mapping**: Map Sharpy types to appropriate .NET types
5. **Dunder Method Synthesis**: Generate C# operator overloads from Python-style dunder methods
6. **Preserve Semantics**: Maintain Sharpy's semantics while generating idiomatic C#

## Architecture Components

### 1. Semantic Analysis Phase

Before code generation, perform semantic analysis to:
- Resolve all types
- Build symbol tables
- Validate type constraints
- Resolve imports and dependencies
- Detect semantic errors

### 2. Code Generation Phase

Transform the type-checked AST into C# code:
- Generate C# syntax trees using Roslyn
- Apply naming conventions
- Synthesize runtime support code
- Emit C# source files

### 3. Runtime Support

The Sharpy runtime library (`Sharpy.dll`) provides:
- Collection wrappers (`Sharpy.List<T>`, `Sharpy.Dict<K,V>`, etc.)
- String wrapper (`Sharpy.Str`)
- Optional type (`Sharpy.Optional<T>`)
- Base classes (`Sharpy.Object`, `Sharpy.Exception`)
- Helper utilities (slicing, iteration, etc.)

## Step-by-Step Implementation Guide

### Step 1: Set Up Roslyn Infrastructure

**Required NuGet Packages:**
```xml
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.8.0" />
```

**Basic Code Generator Structure:**
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

public class CodeGenerator
{
    private readonly Module _module;
    private readonly SemanticAnalyzer _semantics;
    private readonly Dictionary<string, TypeInfo> _typeMap;

    public CodeGenerator(Module module, SemanticAnalyzer semantics)
    {
        _module = module;
        _semantics = semantics;
        _typeMap = new Dictionary<string, TypeInfo>();
    }

    public CompilationUnitSyntax Generate()
    {
        // Build C# compilation unit
        var usings = GenerateUsings();
        var namespaceDecl = GenerateNamespace();

        return SF.CompilationUnit()
            .WithUsings(SF.List(usings))
            .AddMembers(namespaceDecl)
            .NormalizeWhitespace();
    }
}
```

### Step 2: Implement Name Transformation

**Naming Convention Rules:**

| Sharpy | C# | Transformation |
|--------|-----|----------------|
| `my_function` | `my_function` | Keep snake_case for functions/methods |
| `MyClass` | `MyClass` | Keep PascalCase for classes |
| `my_variable` | `my_variable` | Keep snake_case for locals |
| `MAX_VALUE` | `MAX_VALUE` | Keep CAPS_SNAKE for constants |
| `_private` | `_private` | Keep leading underscore for private |
| `__dunder__` | `__dunder__` | Keep dunder names for special methods |

**Implementation:**
```csharp
public class NameTransformer
{
    public static string TransformIdentifier(string sharpyName, NameContext context)
    {
        // Handle literal names (backtick-escaped)
        if (sharpyName.StartsWith("`") && sharpyName.EndsWith("`"))
            return sharpyName[1..^1];

        // Keep snake_case, PascalCase, and CAPS_SNAKE as-is
        // Sharpy follows Python conventions which are kept in C#
        return sharpyName;
    }

    public static string TransformTypeName(string sharpyType)
    {
        // Map built-in types
        return sharpyType switch
        {
            "int" => "int",
            "float" => "float",
            "double" => "double",
            "str" => "Sharpy.Str",
            "bool" => "bool",
            "list" => "Sharpy.List",
            "dict" => "Sharpy.Dict",
            "set" => "Sharpy.Set",
            "tuple" => "System.ValueTuple",
            _ => sharpyType // User-defined types keep their name
        };
    }
}

public enum NameContext
{
    Function,
    Method,
    Variable,
    Parameter,
    Type,
    Constant
}
```

### Step 3: Generate Module Structure

**Module to C# Namespace Mapping:**

```csharp
public NamespaceDeclarationSyntax GenerateNamespace()
{
    // Module file path determines namespace
    // Example: foo/bar/baz.spy -> Sharpy.Modules.Foo.Bar
    var namespaceName = GetNamespaceName(_module.FilePath);

    var members = new List<MemberDeclarationSyntax>();

    // Generate static module class for module-level members
    if (HasModuleLevelMembers())
    {
        members.Add(GenerateModuleClass());
    }

    // Generate type declarations
    foreach (var stmt in _module.Body)
    {
        if (stmt is ClassDef classDef)
            members.Add(GenerateClass(classDef));
        else if (stmt is StructDef structDef)
            members.Add(GenerateStruct(structDef));
        else if (stmt is InterfaceDef interfaceDef)
            members.Add(GenerateInterface(interfaceDef));
        else if (stmt is EnumDef enumDef)
            members.Add(GenerateEnum(enumDef));
    }

    return SF.NamespaceDeclaration(SF.ParseName(namespaceName))
        .AddMembers(members.ToArray());
}

private ClassDeclarationSyntax GenerateModuleClass()
{
    var moduleName = Path.GetFileNameWithoutExtension(_module.FilePath);
    var className = ToPascalCase(moduleName);

    var members = new List<MemberDeclarationSyntax>();

    // Add module metadata
    members.Add(GenerateConstant("__name__", SF.LiteralExpression(
        SyntaxKind.StringLiteralExpression,
        SF.Literal(moduleName))));

    members.Add(GenerateConstant("__file__", SF.LiteralExpression(
        SyntaxKind.StringLiteralExpression,
        SF.Literal(_module.FilePath))));

    if (_module.DocString != null)
    {
        members.Add(GenerateReadonlyField("__doc__", SF.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            SF.Literal(_module.DocString))));
    }

    // Add module-level constants and functions
    foreach (var stmt in _module.Body)
    {
        if (stmt is VariableDeclaration varDecl && varDecl.IsConst)
            members.Add(GenerateConstant(varDecl.Name, GenerateExpression(varDecl.InitialValue)));
        else if (stmt is FunctionDef funcDef && !IsNestedInClass(funcDef))
            members.Add(GenerateFunction(funcDef, isStatic: true));
    }

    return SF.ClassDeclaration(className)
        .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.StaticKeyword))
        .AddMembers(members.ToArray());
}
```

### Step 4: Generate Type Declarations

#### Class Generation

```csharp
public ClassDeclarationSyntax GenerateClass(ClassDef classDef)
{
    var classModifiers = new List<SyntaxToken>
    {
        SF.Token(SyntaxKind.PublicKeyword)
    };

    // Check for abstract methods
    if (HasAbstractMethods(classDef))
        classModifiers.Add(SF.Token(SyntaxKind.AbstractKeyword));

    var classDecl = SF.ClassDeclaration(classDef.Name)
        .AddModifiers(classModifiers.ToArray());

    // Add type parameters for generics
    if (classDef.TypeParameters.Count > 0)
    {
        classDecl = classDecl.AddTypeParameterListParameters(
            classDef.TypeParameters.Select(tp => SF.TypeParameter(tp)).ToArray());
    }

    // Add base class and interfaces
    var baseTypes = new List<BaseTypeSyntax>();

    if (classDef.BaseClasses.Count > 0)
    {
        // First base is the class, rest are interfaces
        var baseClass = classDef.BaseClasses[0];
        baseTypes.Add(SF.SimpleBaseType(GenerateType(baseClass)));

        for (int i = 1; i < classDef.BaseClasses.Count; i++)
        {
            baseTypes.Add(SF.SimpleBaseType(GenerateType(classDef.BaseClasses[i])));
        }
    }
    else
    {
        // All classes inherit from Sharpy.Object
        baseTypes.Add(SF.SimpleBaseType(SF.ParseTypeName("Sharpy.Object")));
    }

    if (baseTypes.Count > 0)
        classDecl = classDecl.AddBaseListTypes(baseTypes.ToArray());

    // Add members
    var members = new List<MemberDeclarationSyntax>();

    foreach (var stmt in classDef.Body)
    {
        if (stmt is VariableDeclaration varDecl)
            members.Add(GenerateField(varDecl));
        else if (stmt is FunctionDef funcDef)
            members.Add(GenerateMethod(funcDef));
    }

    // Generate __init__ as constructor
    var init = classDef.Body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == "__init__");
    if (init != null)
        members.Insert(0, GenerateConstructor(classDef.Name, init));

    // Generate dunder method overloads
    members.AddRange(GenerateDunderMethodOverloads(classDef));

    classDecl = classDecl.AddMembers(members.ToArray());

    // Add docstring as XML comment
    if (classDef.DocString != null)
        classDecl = classDecl.WithLeadingTrivia(GenerateDocComment(classDef.DocString));

    return classDecl;
}
```

#### Struct Generation

```csharp
public StructDeclarationSyntax GenerateStruct(StructDef structDef)
{
    var structDecl = SF.StructDeclaration(structDef.Name)
        .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.ReadOnlyKeyword));

    // Structs should be readonly for safety
    var members = new List<MemberDeclarationSyntax>();

    // Generate readonly fields
    foreach (var stmt in structDef.Body)
    {
        if (stmt is VariableDeclaration varDecl)
        {
            var field = GenerateField(varDecl, isReadonly: true);
            members.Add(field);
        }
    }

    // Generate constructor
    if (HasFields(structDef))
        members.Add(GenerateStructConstructor(structDef));

    // Generate methods
    foreach (var stmt in structDef.Body)
    {
        if (stmt is FunctionDef funcDef && funcDef.Name != "__init__")
            members.Add(GenerateMethod(funcDef));
    }

    structDecl = structDecl.AddMembers(members.ToArray());

    if (structDef.DocString != null)
        structDecl = structDecl.WithLeadingTrivia(GenerateDocComment(structDef.DocString));

    return structDecl;
}
```

#### Interface Generation

```csharp
public InterfaceDeclarationSyntax GenerateInterface(InterfaceDef interfaceDef)
{
    var interfaceDecl = SF.InterfaceDeclaration($"I{interfaceDef.Name}")
        .AddModifiers(SF.Token(SyntaxKind.PublicKeyword));

    var members = new List<MemberDeclarationSyntax>();

    foreach (var stmt in interfaceDef.Body)
    {
        if (stmt is FunctionDef funcDef)
        {
            // Interface methods are abstract by default
            var method = SF.MethodDeclaration(
                GenerateType(funcDef.ReturnType ?? new TypeAnnotation { Name = "object" }),
                funcDef.Name)
                .AddParameterListParameters(GenerateParameters(funcDef.Parameters))
                .WithSemicolonToken(SF.Token(SyntaxKind.SemicolonToken));

            members.Add(method);
        }
    }

    interfaceDecl = interfaceDecl.AddMembers(members.ToArray());

    if (interfaceDef.DocString != null)
        interfaceDecl = interfaceDecl.WithLeadingTrivia(GenerateDocComment(interfaceDef.DocString));

    return interfaceDecl;
}
```

#### Enum Generation

```csharp
public EnumDeclarationSyntax GenerateEnum(EnumDef enumDef)
{
    var enumDecl = SF.EnumDeclaration(enumDef.Name)
        .AddModifiers(SF.Token(SyntaxKind.PublicKeyword));

    var members = new List<EnumMemberDeclarationSyntax>();

    foreach (var member in enumDef.Members)
    {
        var enumMember = SF.EnumMemberDeclaration(member.Name);

        if (member.Value != null)
        {
            enumMember = enumMember.WithEqualsValue(
                SF.EqualsValueClause(GenerateExpression(member.Value)));
        }

        members.Add(enumMember);
    }

    enumDecl = enumDecl.AddMembers(members.ToArray());

    if (enumDef.DocString != null)
        enumDecl = enumDecl.WithLeadingTrivia(GenerateDocComment(enumDef.DocString));

    return enumDecl;
}
```

### Step 5: Generate Type Annotations

```csharp
public TypeSyntax GenerateType(TypeAnnotation type)
{
    if (type == null)
        return SF.ParseTypeName("object");

    // Map Sharpy type to C# type
    var baseTypeName = NameTransformer.TransformTypeName(type.Name);
    TypeSyntax result = SF.ParseTypeName(baseTypeName);

    // Add generic type arguments
    if (type.TypeArguments.Count > 0)
    {
        var typeArgs = type.TypeArguments.Select(GenerateType).ToArray();
        result = SF.GenericName(SF.Identifier(baseTypeName))
            .WithTypeArgumentList(SF.TypeArgumentList(SF.SeparatedList(typeArgs)));
    }

    // Handle nullable
    if (type.IsNullable)
    {
        result = SF.NullableType(result);
    }

    return result;
}
```

### Step 6: Generate Expressions

```csharp
public ExpressionSyntax GenerateExpression(Expression expr)
{
    return expr switch
    {
        IntegerLiteral intLit => GenerateIntegerLiteral(intLit),
        FloatLiteral floatLit => GenerateFloatLiteral(floatLit),
        StringLiteral strLit => GenerateStringLiteral(strLit),
        BooleanLiteral boolLit => SF.LiteralExpression(
            boolLit.Value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
        NoneLiteral => SF.LiteralExpression(SyntaxKind.NullLiteralExpression),

        Identifier id => SF.IdentifierName(id.Name),

        BinaryOp binOp => GenerateBinaryOperation(binOp),
        UnaryOp unOp => GenerateUnaryOperation(unOp),
        ComparisonChain chain => GenerateComparisonChain(chain),

        MemberAccess memberAccess => GenerateMemberAccess(memberAccess),
        IndexAccess indexAccess => GenerateIndexAccess(indexAccess),
        SliceAccess sliceAccess => GenerateSliceAccess(sliceAccess),
        FunctionCall call => GenerateFunctionCall(call),

        ListLiteral list => GenerateListLiteral(list),
        DictLiteral dict => GenerateDictLiteral(dict),
        SetLiteral set => GenerateSetLiteral(set),
        TupleLiteral tuple => GenerateTupleLiteral(tuple),

        ConditionalExpression cond => GenerateConditionalExpression(cond),
        LambdaExpression lambda => GenerateLambdaExpression(lambda),

        TypeCast cast => GenerateTypeCast(cast),
        TypeCheck check => GenerateTypeCheck(check),

        FStringLiteral fstring => GenerateFString(fstring),

        _ => throw new NotImplementedException($"Expression type {expr.GetType().Name} not implemented")
    };
}

private ExpressionSyntax GenerateIntegerLiteral(IntegerLiteral intLit)
{
    var value = long.Parse(intLit.Value);

    // Apply suffix for explicit type
    if (intLit.Suffix != null)
    {
        return intLit.Suffix.ToLower() switch
        {
            "l" => SF.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                SF.Literal(value)),
            "u" => SF.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                SF.Literal((uint)value)),
            "ul" => SF.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                SF.Literal((ulong)value)),
            _ => SF.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                SF.Literal((int)value))
        };
    }

    return SF.LiteralExpression(SyntaxKind.NumericLiteralExpression,
        SF.Literal((int)value));
}

private ExpressionSyntax GenerateBinaryOperation(BinaryOp binOp)
{
    var left = GenerateExpression(binOp.Left);
    var right = GenerateExpression(binOp.Right);

    var kind = binOp.Operator switch
    {
        BinaryOperator.Add => SyntaxKind.AddExpression,
        BinaryOperator.Subtract => SyntaxKind.SubtractExpression,
        BinaryOperator.Multiply => SyntaxKind.MultiplyExpression,
        BinaryOperator.Divide => SyntaxKind.DivideExpression,
        BinaryOperator.FloorDivide => SyntaxKind.DivideExpression, // Needs runtime support
        BinaryOperator.Modulo => SyntaxKind.ModuloExpression,
        BinaryOperator.Power => SyntaxKind.InvocationExpression, // Math.Pow

        BinaryOperator.Equal => SyntaxKind.EqualsExpression,
        BinaryOperator.NotEqual => SyntaxKind.NotEqualsExpression,
        BinaryOperator.LessThan => SyntaxKind.LessThanExpression,
        BinaryOperator.LessThanOrEqual => SyntaxKind.LessThanOrEqualExpression,
        BinaryOperator.GreaterThan => SyntaxKind.GreaterThanExpression,
        BinaryOperator.GreaterThanOrEqual => SyntaxKind.GreaterThanOrEqualExpression,

        BinaryOperator.And => SyntaxKind.LogicalAndExpression,
        BinaryOperator.Or => SyntaxKind.LogicalOrExpression,

        BinaryOperator.BitwiseAnd => SyntaxKind.BitwiseAndExpression,
        BinaryOperator.BitwiseOr => SyntaxKind.BitwiseOrExpression,
        BinaryOperator.BitwiseXor => SyntaxKind.ExclusiveOrExpression,
        BinaryOperator.LeftShift => SyntaxKind.LeftShiftExpression,
        BinaryOperator.RightShift => SyntaxKind.RightShiftExpression,

        BinaryOperator.NullCoalesce => SyntaxKind.CoalesceExpression,

        _ => throw new NotImplementedException($"Binary operator {binOp.Operator} not implemented")
    };

    // Special cases
    if (binOp.Operator == BinaryOperator.Power)
    {
        // Generate Math.Pow(left, right)
        return SF.InvocationExpression(
            SF.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                SF.IdentifierName("Math"),
                SF.IdentifierName("Pow")))
            .AddArgumentListArguments(
                SF.Argument(left),
                SF.Argument(right));
    }

    if (binOp.Operator == BinaryOperator.FloorDivide)
    {
        // Generate (int)(left / right) for integer division
        return SF.CastExpression(
            SF.PredefinedType(SF.Token(SyntaxKind.IntKeyword)),
            SF.BinaryExpression(SyntaxKind.DivideExpression, left, right));
    }

    return SF.BinaryExpression(kind, left, right);
}
```

### Step 7: Generate Statements

```csharp
public StatementSyntax GenerateStatement(Statement stmt)
{
    return stmt switch
    {
        ExpressionStatement exprStmt => SF.ExpressionStatement(GenerateExpression(exprStmt.Expression)),

        Assignment assign => GenerateAssignment(assign),
        VariableDeclaration varDecl => GenerateVariableDeclaration(varDecl),

        IfStatement ifStmt => GenerateIfStatement(ifStmt),
        WhileStatement whileStmt => GenerateWhileStatement(whileStmt),
        ForStatement forStmt => GenerateForStatement(forStmt),

        ReturnStatement retStmt => GenerateReturnStatement(retStmt),
        BreakStatement => SF.BreakStatement(),
        ContinueStatement => SF.ContinueStatement(),
        PassStatement => SF.EmptyStatement(),

        RaiseStatement raiseStmt => GenerateRaiseStatement(raiseStmt),
        TryStatement tryStmt => GenerateTryStatement(tryStmt),
        AssertStatement assertStmt => GenerateAssertStatement(assertStmt),

        _ => throw new NotImplementedException($"Statement type {stmt.GetType().Name} not implemented")
    };
}

private StatementSyntax GenerateAssignment(Assignment assign)
{
    var target = GenerateExpression(assign.Target);
    var value = GenerateExpression(assign.Value);

    var kind = assign.Operator switch
    {
        AssignmentOperator.Assign => SyntaxKind.SimpleAssignmentExpression,
        AssignmentOperator.PlusAssign => SyntaxKind.AddAssignmentExpression,
        AssignmentOperator.MinusAssign => SyntaxKind.SubtractAssignmentExpression,
        AssignmentOperator.StarAssign => SyntaxKind.MultiplyAssignmentExpression,
        AssignmentOperator.SlashAssign => SyntaxKind.DivideAssignmentExpression,
        AssignmentOperator.ModuloAssign => SyntaxKind.ModuloAssignmentExpression,
        _ => SyntaxKind.SimpleAssignmentExpression
    };

    return SF.ExpressionStatement(
        SF.AssignmentExpression(kind, target, value));
}

private StatementSyntax GenerateForStatement(ForStatement forStmt)
{
    // Python-style for loop: for x in iterable:
    // Translates to: foreach (var x in iterable)

    var variable = SF.VariableDeclaration(
        SF.IdentifierName("var"))
        .AddVariables(SF.VariableDeclarator(GetTargetName(forStmt.Target)));

    var iterator = GenerateExpression(forStmt.Iterator);

    var body = SF.Block(forStmt.Body.Select(GenerateStatement));

    return SF.ForEachStatement(
        SF.IdentifierName("var"),
        GetTargetName(forStmt.Target),
        iterator,
        body);
}

private string GetTargetName(Expression target)
{
    return target switch
    {
        Identifier id => id.Name,
        TupleLiteral => "item", // Tuple unpacking handled separately
        _ => "item"
    };
}
```

### Step 8: Generate Dunder Method Overloads

```csharp
public IEnumerable<MemberDeclarationSyntax> GenerateDunderMethodOverloads(ClassDef classDef)
{
    var overloads = new List<MemberDeclarationSyntax>();

    // Find dunder methods
    foreach (var member in classDef.Body.OfType<FunctionDef>())
    {
        switch (member.Name)
        {
            case "__add__":
                overloads.Add(GenerateOperatorOverload(classDef.Name, member, "operator +"));
                break;
            case "__sub__":
                overloads.Add(GenerateOperatorOverload(classDef.Name, member, "operator -"));
                break;
            case "__mul__":
                overloads.Add(GenerateOperatorOverload(classDef.Name, member, "operator *"));
                break;
            case "__truediv__":
                overloads.Add(GenerateOperatorOverload(classDef.Name, member, "operator /"));
                break;
            case "__eq__":
                overloads.Add(GenerateOperatorOverload(classDef.Name, member, "operator =="));
                overloads.Add(GenerateNotEqualsOverload(classDef.Name, member));
                break;
            case "__lt__":
                overloads.Add(GenerateOperatorOverload(classDef.Name, member, "operator <"));
                break;
            case "__le__":
                overloads.Add(GenerateOperatorOverload(classDef.Name, member, "operator <="));
                break;
            case "__gt__":
                overloads.Add(GenerateOperatorOverload(classDef.Name, member, "operator >"));
                break;
            case "__ge__":
                overloads.Add(GenerateOperatorOverload(classDef.Name, member, "operator >="));
                break;
            case "__str__":
                overloads.Add(GenerateToStringOverride(member));
                break;
            case "__repr__":
                // If no __str__, use __repr__ for ToString
                if (!classDef.Body.OfType<FunctionDef>().Any(f => f.Name == "__str__"))
                    overloads.Add(GenerateToStringOverride(member));
                break;
            case "__hash__":
                overloads.Add(GenerateGetHashCodeOverride(member));
                break;
        }
    }

    return overloads;
}

private OperatorDeclarationSyntax GenerateOperatorOverload(
    string className, FunctionDef method, string operatorToken)
{
    // Extract operator symbol
    var op = operatorToken.Replace("operator ", "");
    var opKind = op switch
    {
        "+" => SyntaxKind.PlusToken,
        "-" => SyntaxKind.MinusToken,
        "*" => SyntaxKind.AsteriskToken,
        "/" => SyntaxKind.SlashToken,
        "==" => SyntaxKind.EqualsEqualsToken,
        "!=" => SyntaxKind.ExclamationEqualsToken,
        "<" => SyntaxKind.LessThanToken,
        "<=" => SyntaxKind.LessThanEqualsToken,
        ">" => SyntaxKind.GreaterThanToken,
        ">=" => SyntaxKind.GreaterThanEqualsToken,
        _ => throw new NotImplementedException()
    };

    var returnType = GenerateType(method.ReturnType ?? new TypeAnnotation { Name = className });
    var parameters = GenerateParameters(method.Parameters.Skip(1)); // Skip 'self'

    var body = SF.Block(method.Body.Select(GenerateStatement));

    return SF.OperatorDeclaration(returnType, SF.Token(opKind))
        .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.StaticKeyword))
        .AddParameterListParameters(parameters)
        .WithBody(body);
}
```

### Step 9: Generate Import Statements

```csharp
public IEnumerable<UsingDirectiveSyntax> GenerateUsings()
{
    var usings = new List<UsingDirectiveSyntax>();

    // Always include System and Sharpy
    usings.Add(SF.UsingDirective(SF.ParseName("System")));
    usings.Add(SF.UsingDirective(SF.ParseName("System.Collections.Generic")));
    usings.Add(SF.UsingDirective(SF.ParseName("Sharpy")));

    // Process import statements
    foreach (var stmt in _module.Body)
    {
        if (stmt is ImportStatement import)
        {
            foreach (var name in import.Names)
            {
                var modulePath = name.Name.Replace(".", "::");
                var namespaceName = $"Sharpy.Modules.{modulePath}";
                var alias = name.AsName ?? GetLastSegment(name.Name);

                usings.Add(SF.UsingDirective(SF.ParseName(namespaceName))
                    .WithAlias(SF.NameEquals(SF.IdentifierName(alias))));
            }
        }
        else if (stmt is FromImportStatement fromImport)
        {
            var modulePath = fromImport.Module.Replace(".", "::");
            var namespaceName = $"Sharpy.Modules.{modulePath}";

            if (fromImport.ImportAll)
            {
                // from foo import * -> using static Sharpy.Modules.Foo;
                usings.Add(SF.UsingDirective(SF.ParseName(namespaceName))
                    .WithStaticKeyword(SF.Token(SyntaxKind.StaticKeyword)));
            }
            else
            {
                // Selective imports handled by using static
                usings.Add(SF.UsingDirective(SF.ParseName(namespaceName))
                    .WithStaticKeyword(SF.Token(SyntaxKind.StaticKeyword)));
            }
        }
    }

    return usings;
}
```

### Step 10: Emit C# Source Code

```csharp
public class CodeEmitter
{
    public static string EmitCode(CompilationUnitSyntax compilationUnit)
    {
        // Normalize whitespace and format
        var normalized = compilationUnit.NormalizeWhitespace(indentation: "    ", eol: "\n");

        // Convert to string
        return normalized.ToFullString();
    }

    public static void EmitToFile(CompilationUnitSyntax compilationUnit, string outputPath)
    {
        var code = EmitCode(compilationUnit);

        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Write to file
        File.WriteAllText(outputPath, code);
    }
}
```

## Type System Integration

### Type Mapping Reference

| Sharpy Type | C# Type | Notes |
|-------------|---------|-------|
| `int` | `int` | System.Int32 |
| `long` | `long` | System.Int64 |
| `float` | `float` | System.Single |
| `double` | `double` | System.Double |
| `bool` | `bool` | System.Boolean |
| `str` | `Sharpy.Str` | Wrapper around string |
| `list[T]` | `Sharpy.List<T>` | Wrapper around List<T> |
| `dict[K,V]` | `Sharpy.Dict<K,V>` | Wrapper around OrderedDictionary<K,V> |
| `set[T]` | `Sharpy.Set<T>` | Wrapper around HashSet<T> |
| `tuple[T1,T2,...]` | `Sharpy.Tuple<T1,T2,...>` | Wrapper around ValueTuple |
| `T?` | `T?` | Nullable reference type |
| `Optional[T]` | `Sharpy.Optional<T>` | True optional type |

### Collection Literal Generation

```csharp
private ExpressionSyntax GenerateListLiteral(ListLiteral list)
{
    // new Sharpy.List<T> { elem1, elem2, elem3 }
    var elementType = InferElementType(list.Elements);
    var elements = list.Elements.Select(GenerateExpression);

    return SF.ObjectCreationExpression(
        SF.GenericName("Sharpy.List")
            .AddTypeArgumentListArguments(elementType))
        .WithInitializer(SF.InitializerExpression(
            SyntaxKind.CollectionInitializerExpression,
            SF.SeparatedList(elements)));
}

private ExpressionSyntax GenerateDictLiteral(DictLiteral dict)
{
    // new Sharpy.Dict<K,V> { { key1, value1 }, { key2, value2 } }
    var keyType = InferElementType(dict.Entries.Select(e => e.Key));
    var valueType = InferElementType(dict.Entries.Select(e => e.Value));

    var initializers = dict.Entries.Select(entry =>
        SF.InitializerExpression(SyntaxKind.ComplexElementInitializerExpression,
            SF.SeparatedList(new[]
            {
                GenerateExpression(entry.Key),
                GenerateExpression(entry.Value)
            })));

    return SF.ObjectCreationExpression(
        SF.GenericName("Sharpy.Dict")
            .AddTypeArgumentListArguments(keyType, valueType))
        .WithInitializer(SF.InitializerExpression(
            SyntaxKind.CollectionInitializerExpression,
            SF.SeparatedList<ExpressionSyntax>(initializers)));
}
```

## Advanced Features

### F-String Generation

```csharp
private ExpressionSyntax GenerateFString(FStringLiteral fstring)
{
    // f"Hello {name}, you are {age} years old"
    // Translates to: $"Hello {name}, you are {age} years old"

    var parts = new List<InterpolatedStringContentSyntax>();

    foreach (var part in fstring.Parts)
    {
        if (part.Text != null)
        {
            parts.Add(SF.InterpolatedStringText()
                .WithTextToken(SF.Token(
                    SF.TriviaList(),
                    SyntaxKind.InterpolatedStringTextToken,
                    part.Text,
                    part.Text,
                    SF.TriviaList())));
        }
        else if (part.Expression != null)
        {
            parts.Add(SF.Interpolation(GenerateExpression(part.Expression)));
        }
    }

    return SF.InterpolatedStringExpression(SF.Token(SyntaxKind.InterpolatedStringStartToken))
        .WithContents(SF.List(parts));
}
```

### Slice Access Generation

```csharp
private ExpressionSyntax GenerateSliceAccess(SliceAccess slice)
{
    // arr[start:stop:step]
    // Translates to: Sharpy.Slice(arr, start, stop, step)

    var obj = GenerateExpression(slice.Object);
    var start = slice.Start != null ? GenerateExpression(slice.Start) : SF.LiteralExpression(SyntaxKind.NullLiteralExpression);
    var stop = slice.Stop != null ? GenerateExpression(slice.Stop) : SF.LiteralExpression(SyntaxKind.NullLiteralExpression);
    var step = slice.Step != null ? GenerateExpression(slice.Step) : SF.LiteralExpression(SyntaxKind.NullLiteralExpression);

    return SF.InvocationExpression(
        SF.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
            SF.IdentifierName("Sharpy"),
            SF.IdentifierName("Slice")))
        .AddArgumentListArguments(
            SF.Argument(obj),
            SF.Argument(start),
            SF.Argument(stop),
            SF.Argument(step));
}
```

### Null-Conditional Member Access

```csharp
private ExpressionSyntax GenerateMemberAccess(MemberAccess memberAccess)
{
    var obj = GenerateExpression(memberAccess.Object);
    var member = SF.IdentifierName(memberAccess.Member);

    if (memberAccess.IsNullConditional)
    {
        // obj?.member
        return SF.ConditionalAccessExpression(obj,
            SF.MemberBindingExpression(member));
    }
    else
    {
        // obj.member
        return SF.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            obj,
            member);
    }
}
```

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public void TestGenerateSimpleClass()
{
    var classDef = new ClassDef
    {
        Name = "Person",
        Body = new List<Statement>
        {
            new VariableDeclaration { Name = "name", Type = new TypeAnnotation { Name = "str" } },
            new VariableDeclaration { Name = "age", Type = new TypeAnnotation { Name = "int" } }
        }
    };

    var generator = new CodeGenerator(new Module(), new SemanticAnalyzer());
    var result = generator.GenerateClass(classDef);

    var code = result.NormalizeWhitespace().ToFullString();

    Assert.Contains("public class Person : Sharpy.Object", code);
    Assert.Contains("private Sharpy.Str name;", code);
    Assert.Contains("private int age;", code);
}
```

### Integration Tests

Test complete Sharpy programs:

```python
# test.spy
class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b

calc = Calculator()
result = calc.add(5, 3)
print(result)
```

Expected C# output:
```csharp
using System;
using Sharpy;

namespace Sharpy.Modules;

public class Calculator : Sharpy.Object
{
    public virtual int add(int a, int b)
    {
        return a + b;
    }
}

public static class Test
{
    public static void Main()
    {
        var calc = new Calculator();
        var result = calc.add(5, 3);
        Console.WriteLine(result);
    }
}
```

## Performance Considerations

1. **Roslyn SyntaxFactory Caching**: Reuse syntax nodes where possible
2. **Parallel Generation**: Generate independent classes in parallel
3. **Incremental Compilation**: Only regenerate changed files
4. **Lazy Type Resolution**: Defer type resolution until needed
5. **String Interning**: Intern frequently used strings (type names, keywords)

## Error Handling

### Code Generation Errors

```csharp
public class CodeGenException : Exception
{
    public AstNode Node { get; }
    public int Line { get; }
    public int Column { get; }

    public CodeGenException(string message, AstNode node)
        : base($"{message} at line {node.LineStart}, column {node.ColumnStart}")
    {
        Node = node;
        Line = node.LineStart;
        Column = node.ColumnStart;
    }
}
```

### Error Recovery

- Continue generating code even with errors where possible
- Emit error comments in generated code
- Collect all errors for batch reporting

## Future Enhancements

1. **Optimization Passes**: Add IL-level optimizations
2. **Debug Info Generation**: Emit PDB files for debugging
3. **Incremental Compilation**: Cache generated code for unchanged files
4. **Multi-targeting**: Generate code for different .NET versions
5. **Source Maps**: Map generated C# back to Sharpy source lines

## References

- [Roslyn API Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/)
- [Language Reference](language_reference.md)
- [Type System](type_system.md)
- [Compiler Design](compiler_design.md)
