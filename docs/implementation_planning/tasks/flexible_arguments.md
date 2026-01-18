# Implementation Notes: Flexible Arguments

**Feature Specification**: `docs/language_specification/flexible_arguments.md`

---

## Overview

This document describes the implementation of Sharpy's flexible argument handling feature across three tiers:

| Tier | Feature | Compiler Changes |
|------|---------|------------------|
| 0 | `/` and `*` markers | Lexer, Parser, Semantic validation |
| 1 | `@kwargs` decorator | Semantic analysis, CodeGen (struct + overload) |
| 2 | `@dynamic_kwargs` decorator | Semantic analysis, CodeGen (dictionary param) |

---

## Phase 1: Lexer Changes

**File**: `src/Sharpy.Compiler/Lexer/Lexer.cs`

### New Token Types

No new token types are strictly required—`/` and `*` already exist as `Slash` and `Star`. However, we need context-aware handling in the parser to distinguish:
- `*` as multiplication operator vs `*` as keyword-only marker vs `*args`
- `/` as division operator vs `/` as positional-only marker

**Alternative approach**: Add dedicated token types for parameter context:

```csharp
// In TokenType enum (Token.cs)
PositionalOnlyMarker,  // / in parameter list
KeywordOnlyMarker,     // * in parameter list (without identifier)
```

**Recommendation**: Use existing tokens (`Slash`, `Star`) and let the parser disambiguate based on context. This is simpler and matches Python's approach.

### `**kwargs` Token Sequence

For `**kwargs: dict[str, T]`, the lexer produces:
```
DoubleStar → Identifier("kwargs") → Colon → Identifier("dict") → LeftBracket → ...
```

This already works with existing tokenization.

---

## Phase 2: AST Changes

**File**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

### Extended Parameter Record

```csharp
/// <summary>
/// Function/method parameter
/// </summary>
public record Parameter
{
    public string Name { get; init; } = "";
    public TypeAnnotation? Type { get; init; }
    public Expression? DefaultValue { get; init; }
    
    /// <summary>
    /// True if this parameter is variadic (*args). Maps to C# params T[].
    /// </summary>
    public bool IsVariadic { get; init; }
    
    /// <summary>
    /// True if this parameter is positional-only (appears before / marker).
    /// </summary>
    public bool IsPositionalOnly { get; init; }
    
    /// <summary>
    /// True if this parameter is keyword-only (appears after * or *args).
    /// </summary>
    public bool IsKeywordOnly { get; init; }
    
    /// <summary>
    /// True if this is a **kwargs parameter for dynamic keyword arguments.
    /// </summary>
    public bool IsDynamicKwargs { get; init; }

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

### FunctionDef Additions (Optional)

The `FunctionDef` record doesn't need changes since decorators are already stored in `List<Decorator> Decorators`. The semantic analyzer will check for `@kwargs` and `@dynamic_kwargs` by name.

However, for convenience during code generation, we could add computed properties:

```csharp
public record FunctionDef : Statement
{
    // ... existing properties ...
    
    /// <summary>
    /// True if function has @kwargs decorator (generates kwargs struct).
    /// </summary>
    public bool HasKwargsDecorator => Decorators.Any(d => d.Name == "kwargs");
    
    /// <summary>
    /// True if function has @dynamic_kwargs decorator (uses dictionary).
    /// </summary>
    public bool HasDynamicKwargsDecorator => Decorators.Any(d => d.Name == "dynamic_kwargs");
    
    /// <summary>
    /// True if function has any positional-only parameters (contains / marker).
    /// </summary>
    public bool HasPositionalOnlyParams => Parameters.Any(p => p.IsPositionalOnly);
    
    /// <summary>
    /// True if function has any keyword-only parameters (after * marker).
    /// </summary>
    public bool HasKeywordOnlyParams => Parameters.Any(p => p.IsKeywordOnly);
}
```

---

## Phase 3: Parser Changes

**File**: `src/Sharpy.Compiler/Parser/Parser.cs`

### Modified `ParseParameterList()`

The parameter list grammar becomes:

```ebnf
parameter_list = [positional_only_section] [regular_section] [keyword_only_section] [dynamic_kwargs] ;
positional_only_section = param {"," param} "," "/" ;
regular_section = param {"," param} ;
keyword_only_section = ("*" | variadic_param) "," param {"," param} ;
variadic_param = "*" IDENTIFIER ":" type ;
dynamic_kwargs = "**" IDENTIFIER ":" "dict" "[" type "," type "]" ;
```


```csharp
private List<Parameter> ParseParameterList()
{
    var parameters = new List<Parameter>();
    var seenSlash = false;      // Have we seen / marker?
    var seenStar = false;       // Have we seen * or *args?
    var seenDoubleStar = false; // Have we seen **kwargs?
    
    while (Current.Type != TokenType.RightParen)
    {
        // Check for / (positional-only marker)
        if (Current.Type == TokenType.Slash)
        {
            if (seenSlash)
                throw new ParserError("Duplicate / in parameter list", Current.Line, Current.Column);
            if (seenStar)
                throw new ParserError("/ must appear before * in parameter list", Current.Line, Current.Column);
            if (parameters.Count == 0)
                throw new ParserError("/ must have at least one parameter before it", Current.Line, Current.Column);
            
            // Mark all parameters so far as positional-only
            for (int i = 0; i < parameters.Count; i++)
            {
                parameters[i] = parameters[i] with { IsPositionalOnly = true };
            }
            
            seenSlash = true;
            Advance(); // consume /
            
            if (Current.Type == TokenType.Comma)
                Advance(); // consume trailing comma
            
            continue;
        }
        
        // Check for * (keyword-only marker or *args)
        if (Current.Type == TokenType.Star)
        {
            if (seenDoubleStar)
                throw new ParserError("* cannot appear after **kwargs", Current.Line, Current.Column);
            
            Advance(); // consume *
            
            // Check if this is bare * (keyword-only marker) or *args
            if (Current.Type == TokenType.Comma || Current.Type == TokenType.RightParen)
            {
                // Bare * - just marks keyword-only, no parameter created
                seenStar = true;
                if (Current.Type == TokenType.Comma)
                    Advance();
                continue;
            }
            
            // *args - variadic parameter
            if (seenStar)
                throw new ParserError("Cannot have multiple *args parameters", Current.Line, Current.Column);
            
            seenStar = true;
            var variadicParam = ParseSingleParameter(isVariadic: true, isKeywordOnly: false);
            parameters.Add(variadicParam);
            
            if (Current.Type == TokenType.Comma)
                Advance();
            
            continue;
        }
        
        // Check for **kwargs
        if (Current.Type == TokenType.DoubleStar)
        {
            if (seenDoubleStar)
                throw new ParserError("Cannot have multiple **kwargs parameters", Current.Line, Current.Column);
            
            Advance(); // consume **
            seenDoubleStar = true;
            
            var kwargsParam = ParseSingleParameter(isVariadic: false, isKeywordOnly: false);
            kwargsParam = kwargsParam with { IsDynamicKwargs = true };
            parameters.Add(kwargsParam);
            
            // **kwargs must be last
            if (Current.Type == TokenType.Comma)
            {
                Advance();
                if (Current.Type != TokenType.RightParen)
                    throw new ParserError("**kwargs must be the last parameter", Current.Line, Current.Column);
            }
            
            continue;
        }
        
        // Regular parameter
        var param = ParseSingleParameter(isVariadic: false, isKeywordOnly: seenStar);
        parameters.Add(param);
        
        if (Current.Type == TokenType.Comma)
            Advance();
    }
    
    return parameters;
}


private Parameter ParseSingleParameter(bool isVariadic, bool isKeywordOnly)
{
    var startLine = Current.Line;
    var startColumn = Current.Column;
    
    Expect(TokenType.Identifier);
    var name = Previous.Value;
    
    TypeAnnotation? type = null;
    Expression? defaultValue = null;
    
    // Type annotation
    if (Current.Type == TokenType.Colon)
    {
        Advance();
        type = ParseTypeAnnotation();
    }
    
    // Default value
    if (Current.Type == TokenType.Assign)
    {
        Advance();
        defaultValue = ParseExpression();
    }
    
    return new Parameter
    {
        Name = name,
        Type = type,
        DefaultValue = defaultValue,
        IsVariadic = isVariadic,
        IsKeywordOnly = isKeywordOnly,
        IsPositionalOnly = false, // Set by caller after seeing /
        IsDynamicKwargs = false,  // Set by caller for **kwargs
        LineStart = startLine,
        ColumnStart = startColumn,
        LineEnd = Previous.Line,
        ColumnEnd = Previous.Column + Previous.Value.Length
    };
}
```

---

## Phase 4: Semantic Analysis

**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs` (or new file)

### Validation Rules

#### Tier 0: `/` and `*` Markers

```csharp
public class FlexibleArgumentValidator
{
    public void ValidateFunctionDef(FunctionDef func, SymbolTable symbolTable)
    {
        // Rule 1: Positional-only params must come before regular params
        var seenNonPositionalOnly = false;
        foreach (var param in func.Parameters)
        {
            if (!param.IsPositionalOnly)
                seenNonPositionalOnly = true;
            else if (seenNonPositionalOnly)
                AddError("Positional-only parameter after regular parameter", param);
        }
        
        // Rule 2: Keyword-only params must come after * or *args
        var seenKeywordOnly = false;
        foreach (var param in func.Parameters)
        {
            if (param.IsKeywordOnly)
                seenKeywordOnly = true;
            else if (seenKeywordOnly && !param.IsDynamicKwargs)
                AddError("Non-keyword-only parameter after keyword-only parameter", param);
        }
        
        // Rule 3: Default values after non-default (within each category)
        ValidateDefaultValueOrdering(func.Parameters.Where(p => p.IsPositionalOnly));
        ValidateDefaultValueOrdering(func.Parameters.Where(p => !p.IsPositionalOnly && !p.IsKeywordOnly));
        // Note: Keyword-only params can have defaults in any order
    }
    
    private void ValidateDefaultValueOrdering(IEnumerable<Parameter> params)
    {
        var seenDefault = false;
        foreach (var param in params)
        {
            if (param.DefaultValue != null)
                seenDefault = true;
            else if (seenDefault)
                AddError("Non-default parameter after default parameter", param);
        }
    }
}
```


#### Tier 1: `@kwargs` Decorator

```csharp
public void ValidateKwargsDecorator(FunctionDef func)
{
    if (!func.HasKwargsDecorator) return;
    
    // Must have at least one keyword-only parameter
    var keywordOnlyParams = func.Parameters.Where(p => p.IsKeywordOnly && !p.IsVariadic).ToList();
    if (keywordOnlyParams.Count == 0)
    {
        AddError("@kwargs requires at least one keyword-only parameter (after *)", func);
    }
    
    // Cannot have **kwargs with @kwargs
    if (func.Parameters.Any(p => p.IsDynamicKwargs))
    {
        AddError("Cannot use @kwargs with **kwargs parameter; use @dynamic_kwargs instead", func);
    }
}
```

#### Tier 2: `@dynamic_kwargs` Decorator

```csharp
public void ValidateDynamicKwargsDecorator(FunctionDef func)
{
    if (!func.HasDynamicKwargsDecorator) return;
    
    // Must have exactly one **kwargs parameter
    var kwargsParams = func.Parameters.Where(p => p.IsDynamicKwargs).ToList();
    if (kwargsParams.Count == 0)
    {
        AddError("@dynamic_kwargs requires a **kwargs parameter", func);
    }
    else if (kwargsParams.Count > 1)
    {
        AddError("Cannot have multiple **kwargs parameters", func);
    }
    
    // Validate type annotation is dict[str, T]
    var kwargs = kwargsParams.FirstOrDefault();
    if (kwargs?.Type != null)
    {
        if (kwargs.Type.Name != "dict" || kwargs.Type.TypeArguments.Count != 2)
        {
            AddError("**kwargs must have type dict[str, T]", kwargs);
        }
        else if (kwargs.Type.TypeArguments[0].Name != "str")
        {
            AddError("**kwargs key type must be str", kwargs);
        }
    }
}
```

### Call-Site Validation

```csharp
public void ValidateFunctionCall(FunctionCall call, FunctionSymbol func)
{
    // Track which parameters have been provided
    var providedPositionally = new HashSet<int>();
    var providedByName = new HashSet<string>();
    
    // Check positional arguments
    for (int i = 0; i < call.Arguments.Count; i++)
    {
        if (i >= func.Parameters.Count)
        {
            // Could be *args, handled separately
            continue;
        }
        
        var param = func.Parameters[i];
        
        // Check: can this parameter be passed positionally?
        if (param.IsKeywordOnly)
        {
            AddError($"Parameter '{param.Name}' is keyword-only; use {param.Name}=value", call.Arguments[i]);
        }
        
        providedPositionally.Add(i);
    }
    
    // Check keyword arguments
    foreach (var kwarg in call.KeywordArguments)
    {
        var param = func.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
        if (param == null)
        {
            AddError($"Unknown keyword argument '{kwarg.Name}'", kwarg);
            continue;
        }
        
        var paramIndex = func.Parameters.IndexOf(param);
        
        // Check: can this parameter be passed by name?
        if (param.IsPositionalOnly)
        {
            AddError($"Parameter '{param.Name}' is positional-only; pass it positionally", kwarg);
        }
        
        // Check: not already provided positionally
        if (providedPositionally.Contains(paramIndex))
        {
            AddError($"Parameter '{param.Name}' already provided positionally", kwarg);
        }
        
        providedByName.Add(param.Name);
    }
    
    // Check all required parameters are provided
    for (int i = 0; i < func.Parameters.Count; i++)
    {
        var param = func.Parameters[i];
        if (param.DefaultValue == null && 
            !param.IsVariadic && 
            !param.IsDynamicKwargs &&
            !providedPositionally.Contains(i) && 
            !providedByName.Contains(param.Name))
        {
            AddError($"Missing required parameter '{param.Name}'", call);
        }
    }
}
```


#### Tier 1: `@kwargs` Decorator

```csharp
public void ValidateKwargsDecorator(FunctionDef func)
{
    if (!func.HasKwargsDecorator) return;
    
    // Must have at least one keyword-only parameter
    var keywordOnlyParams = func.Parameters.Where(p => p.IsKeywordOnly && !p.IsVariadic).ToList();
    if (keywordOnlyParams.Count == 0)
    {
        AddError("@kwargs requires at least one keyword-only parameter (after *)", func);
    }
    
    // Cannot have **kwargs with @kwargs
    if (func.Parameters.Any(p => p.IsDynamicKwargs))
    {
        AddError("Cannot use @kwargs with **kwargs parameter; use @dynamic_kwargs instead", func);
    }
}
```

#### Tier 2: `@dynamic_kwargs` Decorator

```csharp
public void ValidateDynamicKwargsDecorator(FunctionDef func)
{
    if (!func.HasDynamicKwargsDecorator) return;
    
    // Must have exactly one **kwargs parameter
    var kwargsParams = func.Parameters.Where(p => p.IsDynamicKwargs).ToList();
    if (kwargsParams.Count == 0)
    {
        AddError("@dynamic_kwargs requires a **kwargs parameter", func);
    }
    else if (kwargsParams.Count > 1)
    {
        AddError("Cannot have multiple **kwargs parameters", func);
    }
    
    // Validate type annotation is dict[str, T]
    var kwargs = kwargsParams.FirstOrDefault();
    if (kwargs?.Type != null)
    {
        if (kwargs.Type.Name != "dict" || kwargs.Type.TypeArguments.Count != 2)
        {
            AddError("**kwargs must have type dict[str, T]", kwargs);
        }
        else if (kwargs.Type.TypeArguments[0].Name != "str")
        {
            AddError("**kwargs key type must be str", kwargs);
        }
    }
}
```

### Call-Site Validation

```csharp
public void ValidateFunctionCall(FunctionCall call, FunctionSymbol func)
{
    // Track which parameters have been provided
    var providedPositionally = new HashSet<int>();
    var providedByName = new HashSet<string>();
    
    // Check positional arguments
    for (int i = 0; i < call.Arguments.Count; i++)
    {
        if (i >= func.Parameters.Count)
        {
            // Could be *args, handled separately
            continue;
        }
        
        var param = func.Parameters[i];
        
        // Check: can this parameter be passed positionally?
        if (param.IsKeywordOnly)
        {
            AddError($"Parameter '{param.Name}' is keyword-only; use {param.Name}=value", call.Arguments[i]);
        }
        
        providedPositionally.Add(i);
    }
    
    // Check keyword arguments
    foreach (var kwarg in call.KeywordArguments)
    {
        var param = func.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
        if (param == null)
        {
            AddError($"Unknown keyword argument '{kwarg.Name}'", kwarg);
            continue;
        }
        
        var paramIndex = func.Parameters.IndexOf(param);
        
        // Check: can this parameter be passed by name?
        if (param.IsPositionalOnly)
        {
            AddError($"Parameter '{param.Name}' is positional-only; pass it positionally", kwarg);
        }
        
        // Check: not already provided positionally
        if (providedPositionally.Contains(paramIndex))
        {
            AddError($"Parameter '{param.Name}' already provided positionally", kwarg);
        }
        
        providedByName.Add(param.Name);
    }
    
    // Check all required parameters are provided
    for (int i = 0; i < func.Parameters.Count; i++)
    {
        var param = func.Parameters[i];
        if (param.DefaultValue == null && 
            !param.IsVariadic && 
            !param.IsDynamicKwargs &&
            !providedPositionally.Contains(i) && 
            !providedByName.Contains(param.Name))
        {
            AddError($"Missing required parameter '{param.Name}'", call);
        }
    }
}
```


---

## Phase 5: Code Generation

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Tier 0: No Code Changes

Positional-only and keyword-only validation is purely compile-time. The generated C# signature is unchanged:

```python
# Sharpy
def example(a: int, /, b: int, *, c: int = 3) -> int:
    return a + b + c
```

```csharp
// Generated C# (identical to without markers)
public static int Example(int a, int b, int c = 3) => a + b + c;
```

### Tier 1: `@kwargs` Struct Generation

```csharp
private void GenerateKwargsArtifacts(FunctionDef func)
{
    if (!func.HasKwargsDecorator) return;
    
    var keywordOnlyParams = func.Parameters
        .Where(p => p.IsKeywordOnly && !p.IsVariadic)
        .ToList();
    
    if (keywordOnlyParams.Count == 0) return;
    
    // Generate the kwargs struct
    var structName = $"{NameMangler.Transform(func.Name, NameContext.Type)}Kwargs";
    
    var structMembers = keywordOnlyParams.Select(p => 
        PropertyDeclaration(
            NullableType(_typeMapper.MapType(p.Type!)),
            NameMangler.Transform(p.Name, NameContext.Property))
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
        .WithAccessorList(AccessorList(List(new[]
        {
            AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
            AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
        })))
    );
    
    var structDecl = StructDeclaration(structName)
        .WithModifiers(TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.ReadOnlyKeyword)))
        .WithMembers(List<MemberDeclarationSyntax>(structMembers));
    
    _generatedTypes.Add(structDecl);
    
    // Generate the kwargs overload
    GenerateKwargsOverload(func, structName, keywordOnlyParams);
}


private void GenerateKwargsOverload(
    FunctionDef func, 
    string structName, 
    List<Parameter> keywordOnlyParams)
{
    // Build parameter list: positional/regular params + kwargs struct
    var overloadParams = func.Parameters
        .Where(p => !p.IsKeywordOnly || p.IsVariadic)
        .Select(GenerateParameter)
        .ToList();
    
    // Add kwargs struct parameter with default
    overloadParams.Add(
        Parameter(Identifier("kwargs"))
            .WithType(IdentifierName(structName))
            .WithDefault(EqualsValueClause(
                LiteralExpression(SyntaxKind.DefaultLiteralExpression))));
    
    // Build call to primary overload with null-coalescing for each kwarg
    var callArgs = func.Parameters
        .Where(p => !p.IsKeywordOnly || p.IsVariadic)
        .Select(p => Argument(IdentifierName(NameMangler.Transform(p.Name, NameContext.Local))))
        .ToList();
    
    foreach (var kwParam in keywordOnlyParams)
    {
        var propName = NameMangler.Transform(kwParam.Name, NameContext.Property);
        var defaultExpr = kwParam.DefaultValue != null
            ? GenerateExpression(kwParam.DefaultValue)
            : LiteralExpression(SyntaxKind.DefaultLiteralExpression);
        
        callArgs.Add(Argument(
            BinaryExpression(
                SyntaxKind.CoalesceExpression,
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("kwargs"),
                    IdentifierName(propName)),
                defaultExpr)));
    }
    
    var body = ArrowExpressionClause(
        InvocationExpression(
            IdentifierName(NameMangler.Transform(func.Name, NameContext.Method)))
        .WithArgumentList(ArgumentList(SeparatedList(callArgs))));
    
    // Generate overload method
    var overloadMethod = MethodDeclaration(
        func.ReturnType != null ? _typeMapper.MapType(func.ReturnType) : PredefinedType(Token(SyntaxKind.VoidKeyword)),
        NameMangler.Transform(func.Name, NameContext.Method))
        .WithModifiers(GenerateModifiersFromDecorators(func.Decorators))
        .WithParameterList(ParameterList(SeparatedList(overloadParams)))
        .WithExpressionBody(body)
        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    
    _generatedMethods.Add(overloadMethod);
}
```

### Tier 2: `@dynamic_kwargs` Generation

```csharp
private MethodDeclarationSyntax GenerateDynamicKwargsMethod(FunctionDef func)
{
    var kwargsParam = func.Parameters.First(p => p.IsDynamicKwargs);
    var valueType = kwargsParam.Type!.TypeArguments[1]; // dict[str, T] -> T
    
    // Generate parameter: IDictionary<string, T>? kwargs = null
    var dictType = GenericName("IDictionary")
        .WithTypeArgumentList(TypeArgumentList(SeparatedList(new TypeSyntax[]
        {
            PredefinedType(Token(SyntaxKind.StringKeyword)),
            _typeMapper.MapType(valueType)
        })));
    
    var kwargsParameter = Parameter(Identifier(kwargsParam.Name))
        .WithType(NullableType(dictType))
        .WithDefault(EqualsValueClause(
            LiteralExpression(SyntaxKind.NullLiteralExpression)));
    
    // Generate body: kwargs ??= new Dictionary<string, T>();
    var nullCoalesceAssign = ExpressionStatement(
        AssignmentExpression(
            SyntaxKind.CoalesceAssignmentExpression,
            IdentifierName(kwargsParam.Name),
            ObjectCreationExpression(
                GenericName("Dictionary")
                    .WithTypeArgumentList(TypeArgumentList(SeparatedList(new TypeSyntax[]
                    {
                        PredefinedType(Token(SyntaxKind.StringKeyword)),
                        _typeMapper.MapType(valueType)
                    }))))
            .WithArgumentList(ArgumentList())));
    
    // ... rest of method body generation
}
```


---

## Phase 6: Tests

**File**: `src/Sharpy.Compiler.Tests/Parser/FlexibleArgumentsParserTests.cs`

### Positive Tests - Tier 0

```csharp
public class FlexibleArgumentsParserTests
{
    #region Tier 0: Positional-Only and Keyword-Only Markers

    [Fact]
    public void ParseFunction_PositionalOnlyParams_SetsFlag()
    {
        var source = @"
def example(a: int, b: int, /) -> int:
    return a + b
";
        var module = Parse(source);
        var func = module.Body.OfType<FunctionDef>().First();
        
        func.Parameters.Should().HaveCount(2);
        func.Parameters[0].Name.Should().Be("a");
        func.Parameters[0].IsPositionalOnly.Should().BeTrue();
        func.Parameters[1].Name.Should().Be("b");
        func.Parameters[1].IsPositionalOnly.Should().BeTrue();
    }

    [Fact]
    public void ParseFunction_KeywordOnlyParams_SetsFlag()
    {
        var source = @"
def example(*, a: int, b: int = 5) -> int:
    return a + b
";
        var module = Parse(source);
        var func = module.Body.OfType<FunctionDef>().First();
        
        func.Parameters.Should().HaveCount(2);
        func.Parameters[0].Name.Should().Be("a");
        func.Parameters[0].IsKeywordOnly.Should().BeTrue();
        func.Parameters[1].Name.Should().Be("b");
        func.Parameters[1].IsKeywordOnly.Should().BeTrue();
        func.Parameters[1].DefaultValue.Should().NotBeNull();
    }

    [Fact]
    public void ParseFunction_MixedMarkers_ParsesCorrectly()
    {
        var source = @"
def search(query: str, /, limit: int = 10, *, case_sensitive: bool = False) -> list[str]:
    pass
";
        var module = Parse(source);
        var func = module.Body.OfType<FunctionDef>().First();
        
        func.Parameters.Should().HaveCount(3);
        
        func.Parameters[0].Name.Should().Be("query");
        func.Parameters[0].IsPositionalOnly.Should().BeTrue();
        func.Parameters[0].IsKeywordOnly.Should().BeFalse();
        
        func.Parameters[1].Name.Should().Be("limit");
        func.Parameters[1].IsPositionalOnly.Should().BeFalse();
        func.Parameters[1].IsKeywordOnly.Should().BeFalse();
        
        func.Parameters[2].Name.Should().Be("case_sensitive");
        func.Parameters[2].IsPositionalOnly.Should().BeFalse();
        func.Parameters[2].IsKeywordOnly.Should().BeTrue();
    }

    [Fact]
    public void ParseFunction_VariadicWithKeywordOnly_ParsesCorrectly()
    {
        var source = @"
def format(template: str, *args: object, sep: str = """", end: str = ""\n"") -> str:
    pass
";
        var module = Parse(source);
        var func = module.Body.OfType<FunctionDef>().First();
        
        func.Parameters.Should().HaveCount(4);
        
        func.Parameters[0].Name.Should().Be("template");
        func.Parameters[0].IsVariadic.Should().BeFalse();
        
        func.Parameters[1].Name.Should().Be("args");
        func.Parameters[1].IsVariadic.Should().BeTrue();
        
        func.Parameters[2].Name.Should().Be("sep");
        func.Parameters[2].IsKeywordOnly.Should().BeTrue();
        
        func.Parameters[3].Name.Should().Be("end");
        func.Parameters[3].IsKeywordOnly.Should().BeTrue();
    }

    #endregion


    #region Tier 1: @kwargs Decorator

    [Fact]
    public void ParseFunction_KwargsDecorator_ParsesCorrectly()
    {
        var source = @"
@kwargs
def configure(host: str, /, *, port: int = 8080, timeout: float = 30.0) -> Config:
    pass
";
        var module = Parse(source);
        var func = module.Body.OfType<FunctionDef>().First();
        
        func.Decorators.Should().ContainSingle(d => d.Name == "kwargs");
        func.HasKwargsDecorator.Should().BeTrue();
    }

    #endregion

    #region Tier 2: @dynamic_kwargs Decorator

    [Fact]
    public void ParseFunction_DynamicKwargsParam_ParsesCorrectly()
    {
        var source = @"
@dynamic_kwargs
def forward(endpoint: str, **kwargs: dict[str, object]) -> Response:
    pass
";
        var module = Parse(source);
        var func = module.Body.OfType<FunctionDef>().First();
        
        func.Decorators.Should().ContainSingle(d => d.Name == "dynamic_kwargs");
        func.Parameters.Should().HaveCount(2);
        
        var kwargsParam = func.Parameters[1];
        kwargsParam.Name.Should().Be("kwargs");
        kwargsParam.IsDynamicKwargs.Should().BeTrue();
        kwargsParam.Type!.Name.Should().Be("dict");
        kwargsParam.Type.TypeArguments.Should().HaveCount(2);
        kwargsParam.Type.TypeArguments[0].Name.Should().Be("str");
        kwargsParam.Type.TypeArguments[1].Name.Should().Be("object");
    }

    #endregion
}
```

### Negative Tests - Parser Errors

```csharp
public class FlexibleArgumentsParserErrorTests
{
    [Fact]
    public void ParseFunction_DuplicateSlash_ThrowsError()
    {
        var source = @"
def bad(a: int, /, b: int, /) -> int:
    pass
";
        var act = () => Parse(source);
        act.Should().Throw<ParserError>()
            .WithMessage("*Duplicate / in parameter list*");
    }

    [Fact]
    public void ParseFunction_SlashAfterStar_ThrowsError()
    {
        var source = @"
def bad(*, a: int, /) -> int:
    pass
";
        var act = () => Parse(source);
        act.Should().Throw<ParserError>()
            .WithMessage("*/ must appear before * in parameter list*");
    }

    [Fact]
    public void ParseFunction_SlashWithNoParams_ThrowsError()
    {
        var source = @"
def bad(/, a: int) -> int:
    pass
";
        var act = () => Parse(source);
        act.Should().Throw<ParserError>()
            .WithMessage("*/ must have at least one parameter before it*");
    }

    [Fact]
    public void ParseFunction_MultipleVariadic_ThrowsError()
    {
        var source = @"
def bad(*args: int, *more: int) -> int:
    pass
";
        var act = () => Parse(source);
        act.Should().Throw<ParserError>()
            .WithMessage("*Cannot have multiple *args parameters*");
    }

    [Fact]
    public void ParseFunction_MultipleKwargs_ThrowsError()
    {
        var source = @"
@dynamic_kwargs
def bad(**a: dict[str, int], **b: dict[str, str]) -> None:
    pass
";
        var act = () => Parse(source);
        act.Should().Throw<ParserError>()
            .WithMessage("*Cannot have multiple **kwargs parameters*");
    }

    [Fact]
    public void ParseFunction_ParamAfterKwargs_ThrowsError()
    {
        var source = @"
@dynamic_kwargs
def bad(**kwargs: dict[str, object], extra: int) -> None:
    pass
";
        var act = () => Parse(source);
        act.Should().Throw<ParserError>()
            .WithMessage("***kwargs must be the last parameter*");
    }
}
```


### Negative Tests - Semantic Errors

**File**: `src/Sharpy.Compiler.Tests/Semantic/FlexibleArgumentsSemanticTests.cs`

```csharp
public class FlexibleArgumentsSemanticTests
{
    #region Call-Site Validation

    [Fact]
    public void Validate_PositionalOnlyPassedByName_ReportsError()
    {
        var source = @"
def example(x: int, /) -> int:
    return x

result = example(x=5)  # ERROR: x is positional-only
";
        var errors = GetSemanticErrors(source);
        errors.Should().ContainSingle()
            .Which.Message.Should().Contain("positional-only");
    }

    [Fact]
    public void Validate_KeywordOnlyPassedPositionally_ReportsError()
    {
        var source = @"
def example(*, x: int) -> int:
    return x

result = example(5)  # ERROR: x is keyword-only
";
        var errors = GetSemanticErrors(source);
        errors.Should().ContainSingle()
            .Which.Message.Should().Contain("keyword-only");
    }

    [Fact]
    public void Validate_ValidMixedCall_NoErrors()
    {
        var source = @"
def search(query: str, /, limit: int = 10, *, case_sensitive: bool = False) -> list[str]:
    pass

r1 = search(""hello"")
r2 = search(""hello"", 20)
r3 = search(""hello"", limit=20)
r4 = search(""hello"", 20, case_sensitive=True)
r5 = search(""hello"", case_sensitive=True)
";
        var errors = GetSemanticErrors(source);
        errors.Should().BeEmpty();
    }

    #endregion

    #region Decorator Validation

    [Fact]
    public void Validate_KwargsWithoutKeywordOnlyParams_ReportsError()
    {
        var source = @"
@kwargs
def bad(a: int, b: int) -> int:
    return a + b
";
        var errors = GetSemanticErrors(source);
        errors.Should().ContainSingle()
            .Which.Message.Should().Contain("keyword-only parameter");
    }

    [Fact]
    public void Validate_KwargsWithDynamicKwargs_ReportsError()
    {
        var source = @"
@kwargs
def bad(*, port: int = 8080, **extras: dict[str, object]) -> None:
    pass
";
        var errors = GetSemanticErrors(source);
        errors.Should().ContainSingle()
            .Which.Message.Should().Contain("Cannot use @kwargs with **kwargs");
    }

    [Fact]
    public void Validate_DynamicKwargsWithoutParam_ReportsError()
    {
        var source = @"
@dynamic_kwargs
def bad(a: int) -> int:
    return a
";
        var errors = GetSemanticErrors(source);
        errors.Should().ContainSingle()
            .Which.Message.Should().Contain("requires a **kwargs parameter");
    }

    [Fact]
    public void Validate_DynamicKwargsWrongType_ReportsError()
    {
        var source = @"
@dynamic_kwargs
def bad(**kwargs: list[str]) -> None:
    pass
";
        var errors = GetSemanticErrors(source);
        errors.Should().ContainSingle()
            .Which.Message.Should().Contain("must have type dict[str, T]");
    }

    [Fact]
    public void Validate_DynamicKwargsNonStrKey_ReportsError()
    {
        var source = @"
@dynamic_kwargs
def bad(**kwargs: dict[int, str]) -> None:
    pass
";
        var errors = GetSemanticErrors(source);
        errors.Should().ContainSingle()
            .Which.Message.Should().Contain("key type must be str");
    }

    #endregion
}
```


### Code Generation Tests

**File**: `src/Sharpy.Compiler.Tests/CodeGen/FlexibleArgumentsCodeGenTests.cs`

```csharp
public class FlexibleArgumentsCodeGenTests
{
    #region Tier 0: Markers Don't Affect Generated Code

    [Fact]
    public void Generate_PositionalOnlyParams_GeneratesVanillaSignature()
    {
        var source = @"
def example(a: int, b: int, /) -> int:
    return a + b
";
        var csharp = GenerateCSharp(source);
        
        // Markers don't appear in generated C#
        csharp.Should().Contain("public static int Example(int a, int b)");
        csharp.Should().NotContain("/");
    }

    [Fact]
    public void Generate_KeywordOnlyParams_GeneratesVanillaSignature()
    {
        var source = @"
def example(*, a: int, b: int = 5) -> int:
    return a + b
";
        var csharp = GenerateCSharp(source);
        
        csharp.Should().Contain("public static int Example(int a, int b = 5)");
    }

    #endregion

    #region Tier 1: @kwargs Struct Generation

    [Fact]
    public void Generate_KwargsDecorator_GeneratesStruct()
    {
        var source = @"
@kwargs
def configure(host: str, /, *, port: int = 8080, timeout: float = 30.0) -> None:
    pass
";
        var csharp = GenerateCSharp(source);
        
        // Should generate kwargs struct
        csharp.Should().Contain("public readonly struct ConfigureKwargs");
        csharp.Should().Contain("public int? Port { get; init; }");
        csharp.Should().Contain("public float? Timeout { get; init; }");
    }

    [Fact]
    public void Generate_KwargsDecorator_GeneratesOverload()
    {
        var source = @"
@kwargs
def configure(host: str, /, *, port: int = 8080, timeout: float = 30.0) -> None:
    pass
";
        var csharp = GenerateCSharp(source);
        
        // Primary signature
        csharp.Should().Contain("public static void Configure(string host, int port = 8080, float timeout = 30.0f)");
        
        // Kwargs overload
        csharp.Should().Contain("public static void Configure(string host, ConfigureKwargs kwargs = default)");
        
        // Overload delegates to primary with null coalescing
        csharp.Should().Contain("kwargs.Port ?? 8080");
        csharp.Should().Contain("kwargs.Timeout ?? 30.0f");
    }

    #endregion

    #region Tier 2: @dynamic_kwargs Dictionary Parameter

    [Fact]
    public void Generate_DynamicKwargs_GeneratesDictionaryParam()
    {
        var source = @"
@dynamic_kwargs
def forward(endpoint: str, **kwargs: dict[str, object]) -> None:
    pass
";
        var csharp = GenerateCSharp(source);
        
        csharp.Should().Contain("IDictionary<string, object?>? kwargs = null");
        csharp.Should().Contain("kwargs ??= new Dictionary<string, object?>();");
    }

    [Fact]
    public void Generate_DynamicKwargsTypedValue_GeneratesCorrectType()
    {
        var source = @"
@dynamic_kwargs
def headers(url: str, **kwargs: dict[str, str]) -> None:
    pass
";
        var csharp = GenerateCSharp(source);
        
        csharp.Should().Contain("IDictionary<string, string?>? kwargs = null");
    }

    #endregion
}
```


### Integration Tests

**File**: `src/Sharpy.Compiler.Tests/Integration/FlexibleArgumentsIntegrationTests.cs`

```csharp
public class FlexibleArgumentsIntegrationTests
{
    [Fact]
    public void Integration_PositionalOnlyInFunction_CompilesAndRuns()
    {
        var source = @"
def main() -> None:
    # Create a function with positional-only params
    items = [1, 2, 3, 4, 5]
    
    # Filter using keyword-only for clarity
    def is_even(x: int, /, *, strict: bool = True) -> bool:
        return x % 2 == 0
    
    evens = [x for x in items if is_even(x)]
    print(evens)  # [2, 4]
";
        var result = CompileAndRun(source);
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public void Integration_KwargsStruct_CompilesAndRuns()
    {
        var source = @"
@kwargs
def connect(host: str, /, *, port: int = 5432, ssl: bool = True, timeout: float = 30.0) -> str:
    return f""{host}:{port} (ssl={ssl}, timeout={timeout})""

def main() -> None:
    # Standard call
    r1 = connect(""localhost"", port=5433)
    print(r1)
    
    # Using kwargs struct
    opts = ConnectKwargs(port=5433, ssl=False)
    r2 = connect(""localhost"", opts)
    print(r2)
";
        var result = CompileAndRun(source);
        result.ExitCode.Should().Be(0);
        result.Output.Should().Contain("localhost:5433");
    }

    [Fact]
    public void Integration_MixedParameters_CompilesAndRuns()
    {
        var source = @"
def format_message(
    template: str,           # positional-only
    /,
    name: str,               # regular (positional or keyword)
    *,
    uppercase: bool = False  # keyword-only
) -> str:
    result = template.format(name=name)
    if uppercase:
        result = result.upper()
    return result

def main() -> None:
    msg1 = format_message(""Hello, {name}!"", ""World"")
    msg2 = format_message(""Hello, {name}!"", name=""World"", uppercase=True)
    print(msg1)
    print(msg2)
";
        var result = CompileAndRun(source);
        result.ExitCode.Should().Be(0);
        result.Output.Should().Contain("Hello, World!");
        result.Output.Should().Contain("HELLO, WORLD!");
    }
}
```

---

## Implementation Order

1. **AST Changes** (`Statement.cs`) — Add flags to `Parameter`
2. **Parser Changes** (`Parser.cs`) — Handle `/`, `*`, `**` in parameter lists
3. **Parser Tests** — Verify AST construction
4. **Semantic Validation** — Add call-site and decorator validation
5. **Semantic Tests** — Verify error detection
6. **Code Generation: Tier 0** — No changes needed (verify vanilla output)
7. **Code Generation: Tier 1** — Struct + overload generation
8. **Code Generation: Tier 2** — Dictionary parameter generation
9. **CodeGen Tests** — Verify generated C#
10. **Integration Tests** — End-to-end compilation and execution

---

## Open Questions

1. **Struct naming for overloaded methods**: If `process(str)` and `process(bytes)` both have `@kwargs`, should structs be `ProcessKwargs_Str` and `ProcessKwargs_Bytes`? Or nested under a single name with disambiguation?

2. **Spread syntax (`**opts`)**: Should this be a special call-site syntax, or should callers just pass the struct directly as the second argument?

3. **Kwargs struct with expression**: Should `connect("host", opts with port=9000)` be valid syntax for inline modification?

4. **C# 9.0 `init` accessor limitation**: The `init` accessor was introduced in C# 9.0 and is supported. However, verify Unity's C# 9.0 support includes init-only properties for the kwargs struct pattern.

---

## Phase 7: .NET Metadata Attributes

To enable cross-library enforcement (Sharpy code importing compiled Sharpy `.dll` files), the compiler emits custom attributes that preserve flexible argument metadata in the .NET assembly.

### Attribute Definitions

**File**: `src/Sharpy.Core/Attributes/FlexibleArgsAttributes.cs`

```csharp
namespace Sharpy.Attributes;

/// <summary>
/// Indicates this method has flexible argument semantics (positional-only and/or keyword-only parameters).
/// The boundary indices indicate where the / and * markers appeared in the original Sharpy signature.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
public sealed class FlexibleArgsAttribute : Attribute
{
    /// <summary>
    /// Parameters at indices 0 through PositionalOnlyBoundary (inclusive) are positional-only.
    /// Value of -1 indicates no positional-only parameters.
    /// </summary>
    public int PositionalOnlyBoundary { get; }
    
    /// <summary>
    /// Parameters at indices KeywordOnlyBoundary and above are keyword-only.
    /// Value of -1 indicates no keyword-only parameters.
    /// </summary>
    public int KeywordOnlyBoundary { get; }
    
    public FlexibleArgsAttribute(int positionalOnlyBoundary = -1, int keywordOnlyBoundary = -1)
    {
        PositionalOnlyBoundary = positionalOnlyBoundary;
        KeywordOnlyBoundary = keywordOnlyBoundary;
    }
}

/// <summary>
/// Marks a parameter as positional-only (cannot be passed by name).
/// Optional attribute for IDE/tooling support; the canonical source is FlexibleArgsAttribute.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class PositionalOnlyAttribute : Attribute { }

/// <summary>
/// Marks a parameter as keyword-only (must be passed by name).
/// Optional attribute for IDE/tooling support; the canonical source is FlexibleArgsAttribute.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class KeywordOnlyAttribute : Attribute { }
```

### Code Generation Updates

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

When generating a function with `/` or `*` markers, emit the `[FlexibleArgs]` attribute:

```csharp
private MethodDeclarationSyntax GenerateMethodWithFlexibleArgs(FunctionDef func)
{
    var method = GenerateMethod(func);  // Existing method generation
    
    // Calculate boundaries
    int positionalOnlyBoundary = -1;
    int keywordOnlyBoundary = -1;
    
    for (int i = 0; i < func.Parameters.Count; i++)
    {
        var param = func.Parameters[i];
        if (param.IsPositionalOnly)
            positionalOnlyBoundary = i;
        if (param.IsKeywordOnly && keywordOnlyBoundary == -1)
            keywordOnlyBoundary = i;
    }
    
    // Only emit attribute if there are constraints
    if (positionalOnlyBoundary >= 0 || keywordOnlyBoundary >= 0)
    {
        var attribute = Attribute(
            IdentifierName("FlexibleArgs"))
            .WithArgumentList(AttributeArgumentList(SeparatedList(new[]
            {
                AttributeArgument(
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(positionalOnlyBoundary)))
                    .WithNameEquals(NameEquals("positionalOnlyBoundary")),
                AttributeArgument(
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(keywordOnlyBoundary)))
                    .WithNameEquals(NameEquals("keywordOnlyBoundary"))
            })));
        
        method = method.WithAttributeLists(
            method.AttributeLists.Add(
                AttributeList(SingletonSeparatedList(attribute))));
    }
    
    return method;
}
```

### Generated Code Example

```python
# Sharpy source
def search(query: str, /, limit: int = 10, *, case_sensitive: bool = False) -> list[str]:
    pass
```

```csharp
// Generated C#
[FlexibleArgs(positionalOnlyBoundary: 0, keywordOnlyBoundary: 2)]
public static List<string> Search(
    string query,
    int limit = 10,
    bool caseSensitive = false)
{
    // ...
}
```

### Discovery Updates

**File**: `src/Sharpy.Compiler/Discovery/Caching/OverloadIndex.cs`

Add fields to cache the boundary information:

```csharp
public class FunctionSignature
{
    // ... existing properties ...
    
    /// <summary>
    /// Parameters at indices 0..PositionalOnlyBoundary are positional-only. -1 if none.
    /// </summary>
    public int PositionalOnlyBoundary { get; set; } = -1;
    
    /// <summary>
    /// Parameters at indices KeywordOnlyBoundary..end are keyword-only. -1 if none.
    /// </summary>
    public int KeywordOnlyBoundary { get; set; } = -1;
}
```

**File**: `src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs`

Read the attribute during discovery:

```csharp
private FunctionSignature CreateFunctionSignature(MethodInfo method, string functionName)
{
    var signature = new FunctionSignature
    {
        Name = functionName,
        // ... existing initialization ...
    };
    
    // Read FlexibleArgs attribute if present
    var flexibleArgsAttr = method.GetCustomAttribute<FlexibleArgsAttribute>();
    if (flexibleArgsAttr != null)
    {
        signature.PositionalOnlyBoundary = flexibleArgsAttr.PositionalOnlyBoundary;
        signature.KeywordOnlyBoundary = flexibleArgsAttr.KeywordOnlyBoundary;
    }
    
    return signature;
}
```

**File**: `src/Sharpy.Compiler/Discovery/CachedModuleDiscovery.cs`

Reconstruct parameter flags when converting to `FunctionSymbol`:

```csharp
private FunctionSymbol ConvertToFunctionSymbol(FunctionSignature signature)
{
    var parameters = new List<ParameterSymbol>();
    var overload = signature.Overloads[0];
    
    for (int i = 0; i < overload.Parameters.Count; i++)
    {
        var paramSig = overload.Parameters[i];
        parameters.Add(new ParameterSymbol
        {
            Name = paramSig.Name,
            Type = ConvertTypeSignature(paramSig.Type),
            HasDefault = paramSig.HasDefault,
            IsVariadic = paramSig.IsVariadic,
            // Reconstruct from boundaries
            IsPositionalOnly = signature.PositionalOnlyBoundary >= 0 && i <= signature.PositionalOnlyBoundary,
            IsKeywordOnly = signature.KeywordOnlyBoundary >= 0 && i >= signature.KeywordOnlyBoundary
        });
    }
    
    return new FunctionSymbol
    {
        Name = signature.Name,
        Parameters = parameters,
        ReturnType = ConvertTypeSignature(overload.ReturnType)
    };
}
```

### Optional: Per-Parameter Attributes for Tooling

For enhanced IDE support (tooltips, Roslyn analyzers), optionally emit per-parameter attributes:

```csharp
// Controlled by compiler flag: --emit-parameter-attributes
[FlexibleArgs(positionalOnlyBoundary: 0, keywordOnlyBoundary: 2)]
public static List<string> Search(
    [PositionalOnly] string query,
    int limit = 10,
    [KeywordOnly] bool caseSensitive = false)
{ }
```

This is opt-in because:
- It increases generated code verbosity
- The method-level attribute is sufficient for Sharpy-to-Sharpy interop
- Per-parameter attributes are only useful for C# tooling integration

### Tests

**File**: `src/Sharpy.Compiler.Tests/CodeGen/FlexibleArgsAttributeTests.cs`

```csharp
public class FlexibleArgsAttributeTests
{
    [Fact]
    public void Generate_PositionalOnlyParams_EmitsFlexibleArgsAttribute()
    {
        var source = @"
def example(a: int, b: int, /) -> int:
    return a + b
";
        var csharp = GenerateCSharp(source);
        
        csharp.Should().Contain("[FlexibleArgs(positionalOnlyBoundary: 1, keywordOnlyBoundary: -1)]");
    }

    [Fact]
    public void Generate_KeywordOnlyParams_EmitsFlexibleArgsAttribute()
    {
        var source = @"
def example(*, a: int, b: int) -> int:
    return a + b
";
        var csharp = GenerateCSharp(source);
        
        csharp.Should().Contain("[FlexibleArgs(positionalOnlyBoundary: -1, keywordOnlyBoundary: 0)]");
    }

    [Fact]
    public void Generate_MixedParams_EmitsCorrectBoundaries()
    {
        var source = @"
def search(query: str, /, limit: int = 10, *, case_sensitive: bool = False) -> list[str]:
    pass
";
        var csharp = GenerateCSharp(source);
        
        // query (idx 0) is positional-only, case_sensitive (idx 2) is keyword-only
        csharp.Should().Contain("[FlexibleArgs(positionalOnlyBoundary: 0, keywordOnlyBoundary: 2)]");
    }

    [Fact]
    public void Generate_NoMarkers_NoAttribute()
    {
        var source = @"
def example(a: int, b: int) -> int:
    return a + b
";
        var csharp = GenerateCSharp(source);
        
        csharp.Should().NotContain("[FlexibleArgs");
    }
}
```

**File**: `src/Sharpy.Compiler.Tests/Discovery/FlexibleArgsDiscoveryTests.cs`

```csharp
public class FlexibleArgsDiscoveryTests
{
    [Fact]
    public void Discovery_FlexibleArgsAttribute_ReconstructsParameterFlags()
    {
        // Compile a Sharpy library with flexible args
        var librarySource = @"
def search(query: str, /, limit: int = 10, *, case_sensitive: bool = False) -> list[str]:
    return []
";
        var assembly = CompileToAssembly(librarySource);
        
        // Discover functions from the compiled assembly
        var discovery = new CachedModuleDiscovery(null);
        discovery.LoadAssembly(assembly);
        
        var searchFunc = discovery.GetModuleFunctions("test_module")
            .First(f => f.Name == "search");
        
        searchFunc.Parameters[0].Name.Should().Be("query");
        searchFunc.Parameters[0].IsPositionalOnly.Should().BeTrue();
        searchFunc.Parameters[0].IsKeywordOnly.Should().BeFalse();
        
        searchFunc.Parameters[1].Name.Should().Be("limit");
        searchFunc.Parameters[1].IsPositionalOnly.Should().BeFalse();
        searchFunc.Parameters[1].IsKeywordOnly.Should().BeFalse();
        
        searchFunc.Parameters[2].Name.Should().Be("case_sensitive");
        searchFunc.Parameters[2].IsPositionalOnly.Should().BeFalse();
        searchFunc.Parameters[2].IsKeywordOnly.Should().BeTrue();
    }
}
```

---

## Updated Implementation Order

1. **AST Changes** (`Statement.cs`) — Add flags to `Parameter`
2. **Parser Changes** (`Parser.cs`) — Handle `/`, `*`, `**` in parameter lists
3. **Parser Tests** — Verify AST construction
4. **Attribute Definitions** (`Sharpy.Core/Attributes/`) — Define `FlexibleArgsAttribute`
5. **Semantic Validation** — Add call-site and decorator validation
6. **Semantic Tests** — Verify error detection
7. **Code Generation: Tier 0** — Emit `[FlexibleArgs]` attribute
8. **Code Generation: Tier 1** — Struct + overload generation
9. **Code Generation: Tier 2** — Dictionary parameter generation
10. **Discovery Updates** — Read `[FlexibleArgs]` and reconstruct flags
11. **CodeGen Tests** — Verify generated C# including attributes
12. **Discovery Tests** — Verify cross-library constraint enforcement
13. **Integration Tests** — End-to-end compilation and execution

---

## Related Specifications

- [Flexible Arguments](../../../language_specification/flexible_arguments.md) — Full language specification
- [Function Parameters](../../../language_specification/function_parameters.md) — Base parameter handling
- [Decorators](../../../language_specification/decorators.md) — Decorator system
