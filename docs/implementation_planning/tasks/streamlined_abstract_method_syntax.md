# Task List: Streamlined Abstract Method Syntax

**Feature:** Simplify abstract method declarations by making `@abstract` decorator optional when ellipsis body is used in abstract classes/interfaces  
**Status:** Proposed  
**Created:** 2025-01-17  
**Related Specs:** `decorators.md`, `interfaces.md`, `grammar.ebnf.txt`

---

## Overview

### Current Syntax (Verbose)
```python
@abstract
class Shape:
    @abstract
    def area(self) -> float:
        ...
    
    @abstract
    def perimeter(self) -> float:
        ...

interface IDrawable:
    def draw(self) -> None:
        ...
```

### Proposed Syntax (Streamlined)
```python
@abstract
class Shape:
    def area(self) -> float: ...
    def perimeter(self) -> float: ...

interface IDrawable:
    def draw(self) -> None: ...
```

### Key Changes

1. **`@abstract` on methods becomes optional** when:
   - Method is inside an `@abstract` class AND has `...` body
   - Method is inside an `interface` (already the case)

2. **Inline ellipsis allowed** — `def foo(self) -> int: ...` on single line

3. **Semantic interpretation of `...` body:**

   | Context | `...` Body Meaning | C# Output |
   |---------|-------------------|-----------|
   | Inside `interface` | Abstract | No body (`;`) |
   | Inside `@abstract` class | Abstract | `abstract` modifier, no body |
   | Inside concrete class | TODO stub | `throw new NotImplementedException()` |

4. **`@abstract` class decorator remains required** — needed to:
   - Prevent direct instantiation
   - Signal that `...` means "abstract" not "TODO"

### Code Cleanup

The codebase has references to `"abstractmethod"` which is **not a valid Sharpy decorator** (per spec, it's just `@abstract`). This should be cleaned up.

---

# Phase AM.1: Language Specification Updates

**Goal**: Document the streamlined syntax formally.

---

## Task AM.1.1: Update Decorators Specification

📁 **Files**: `docs/language_specification/decorators.md`

**Changes:**

Update the "Abstract Classes" section to show:
1. `@abstract` on methods is optional when `...` body is present
2. Inline ellipsis syntax is valid
3. Both styles are equivalent

```markdown
## Abstract Classes

Classes marked `@abstract` cannot be instantiated directly and may contain abstract members.

### Declaring Abstract Methods

Inside an `@abstract` class, methods with `...` (ellipsis) body are automatically abstract.
The `@abstract` decorator on individual methods is optional but allowed for explicitness:

```python
@abstract
class Shape:
    # These are equivalent:
    def area(self) -> float: ...              # Implicit abstract (recommended)
    
    @abstract
    def perimeter(self) -> float: ...         # Explicit abstract (also valid)
    
    # Concrete method (has real body)
    def describe(self) -> str:
        return f"Shape with area {self.area()}"
```

### Inline vs Multi-line Ellipsis

Both forms are valid and equivalent:

```python
# Inline (recommended for declarations)
def area(self) -> float: ...

# Multi-line (also valid)
def area(self) -> float:
    ...
```
```

**Acceptance Criteria:**
- [ ] Optional `@abstract` on methods documented
- [ ] Inline ellipsis syntax shown
- [ ] Both styles shown as equivalent
- [ ] Context-dependent meaning of `...` documented

---

## Task AM.1.2: Update Interfaces Specification

📁 **Files**: `docs/language_specification/interfaces.md`

**Changes:**

Update examples to use inline ellipsis syntax consistently:

```python
interface IDrawable:
    def draw(self) -> None: ...
    def get_bounds(self) -> tuple[float, float, float, float]: ...

interface IContainer[T]:
    def add(self, item: T) -> None: ...
    def get(self, index: int) -> T: ...
    def count(self) -> int: ...
```

**Acceptance Criteria:**
- [ ] All interface examples use inline ellipsis where appropriate
- [ ] Multi-line examples retained where they show default implementations

---

## Task AM.1.3: Verify Grammar Supports Inline Ellipsis

📁 **Files**: `docs/language_specification/grammar.ebnf.txt`

**Verification:**

The grammar already has:
```ebnf
func_body ::= suite
            | ELLIPSIS NEWLINE
```

And for interface methods:
```ebnf
interface_method ::= 'def' ... ':' ( ELLIPSIS NEWLINE | suite )
```

**Task:** Verify this is correctly implemented in the parser and add a note to the grammar clarifying inline ellipsis:

```ebnf
(* Note: ELLIPSIS NEWLINE allows inline form: def foo(): ... *)
func_body ::= suite
            | ELLIPSIS NEWLINE
```

**Acceptance Criteria:**
- [ ] Grammar verified to support inline ellipsis
- [ ] Clarifying comment added if helpful

---

# Phase AM.2: Compiler Implementation

**Goal**: Implement the streamlined abstract method detection.

---

## Task AM.2.1: Update TypeChecker - Abstract Method Detection

📁 **Files**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Current Code (approximately lines 800-830):**
```csharp
// Check for @abstract decorator
bool isAbstract = functionDef.Decorators.Any(d => d.Name == "abstract" || d.Name == "abstractmethod");

// Validate abstract methods
if (isAbstract)
{
    // Abstract methods must have ... body
    if (functionDef.Body.Count != 1 || ...)
    {
        AddError($"Abstract method '{functionDef.Name}' must have '...' as its body", ...);
    }
    
    // Abstract methods must be in an abstract class
    if (_currentClass != null && !_currentClass.IsAbstract)
    {
        AddError($"Abstract method '{functionDef.Name}' can only be declared in an abstract class.", ...);
    }
}
```

**New Logic:**
```csharp
// Helper: Check if body is just ellipsis
bool HasEllipsisBody(FunctionDef func) =>
    func.Body.Count == 1 
    && func.Body[0] is ExpressionStatement exprStmt 
    && exprStmt.Expression is EllipsisLiteral;

// Determine if method is abstract:
// 1. Has @abstract decorator explicitly, OR
// 2. Is in an @abstract class AND has ellipsis body
bool hasAbstractDecorator = functionDef.Decorators.Any(d => d.Name == "abstract");
bool isInAbstractClass = _currentClass?.IsAbstract == true;
bool hasEllipsisBody = HasEllipsisBody(functionDef);

bool isAbstractMethod = hasAbstractDecorator || (isInAbstractClass && hasEllipsisBody);

// Validation
if (hasAbstractDecorator && !hasEllipsisBody)
{
    AddError($"Abstract method '{functionDef.Name}' must have '...' as its body", ...);
}

if (hasAbstractDecorator && !isInAbstractClass && _currentClass != null)
{
    AddError($"Abstract method '{functionDef.Name}' can only be declared in an abstract class. " +
             "Add @abstract decorator to the class.", ...);
}

// Note: Ellipsis body in concrete class is valid (generates NotImplementedException)
// So we don't error on that case
```

**Also remove the `"abstractmethod"` check** — this is not a valid decorator per spec.

**Acceptance Criteria:**
- [ ] Methods with `...` body in `@abstract` class are treated as abstract
- [ ] Explicit `@abstract` decorator still works
- [ ] `"abstractmethod"` string removed from code
- [ ] Ellipsis in concrete class still generates `NotImplementedException`
- [ ] Clear error messages for invalid combinations

---

## Task AM.2.2: Update RoslynEmitter - Abstract Method Detection

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Current Code (approximately line 1036):**
```csharp
bool isAbstract = func.Decorators.Any(d =>
    d.Name == "abstractmethod" || d.Name == "abstract");
```

**New Logic:**
```csharp
// Check if method is abstract:
// 1. Has @abstract decorator, OR
// 2. Is in abstract class context AND has ellipsis body
bool hasAbstractDecorator = func.Decorators.Any(d => d.Name == "abstract");
bool hasEllipsisBody = func.Body.Count == 1 
    && func.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };

// Note: isInAbstractClass needs to be passed or tracked in context
bool isAbstract = hasAbstractDecorator || (isInAbstractClass && hasEllipsisBody);
```

**Context tracking:**
The emitter needs to know if it's generating methods for an abstract class. Options:
1. Pass `isAbstractClass` flag to `GenerateClassMethod()`
2. Track in a field like `_currentClassIsAbstract`

**Also remove the `"abstractmethod"` check.**

**Acceptance Criteria:**
- [ ] Abstract method detection uses new logic
- [ ] `"abstractmethod"` string removed
- [ ] Context tracking for abstract class implemented
- [ ] Generated C# is correct for all cases

---

## Task AM.2.3: Verify Parser Supports Inline Ellipsis

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`

**Verification task:** Ensure the parser correctly handles:

```python
def foo(self) -> int: ...
```

This should parse to:
```
FunctionDef {
    Name: "foo",
    Body: [ExpressionStatement { Expression: EllipsisLiteral }]
}
```

**Test by examining `ParseFunctionDef()` or equivalent:**
- After parsing `:`, check for `ELLIPSIS` token
- If found, create body with single `ExpressionStatement` containing `EllipsisLiteral`
- Consume `NEWLINE`

**If not implemented, add support:**
```csharp
// After parsing ':' in function definition
if (Check(TokenType.Ellipsis))
{
    Advance(); // consume ...
    Expect(TokenType.Newline);
    return new FunctionDef {
        // ... other properties
        Body = new List<Statement> {
            new ExpressionStatement { Expression = new EllipsisLiteral() }
        }
    };
}
else
{
    // Parse normal suite
    var body = ParseSuite();
    // ...
}
```

**Acceptance Criteria:**
- [ ] Inline ellipsis parses correctly: `def foo(): ...`
- [ ] Multi-line ellipsis still works: `def foo():\n    ...`
- [ ] Both produce identical AST

---

# Phase AM.3: Unit Tests

**Goal**: Comprehensive test coverage for the new behavior.

---

## Task AM.3.1: TypeChecker Tests for Abstract Method Detection

📁 **Files**: `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerAbstractTests.cs` (new or extend existing)

```csharp
public class TypeCheckerAbstractMethodTests
{
    #region Implicit Abstract (ellipsis in @abstract class)
    
    [Fact]
    public void EllipsisBody_InAbstractClass_TreatedAsAbstract()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...
";
        var result = TypeCheck(source);
        Assert.True(result.Success);
        // Verify method is marked abstract in semantic info
    }
    
    [Fact]
    public void EllipsisBody_InAbstractClass_MultiLine_TreatedAsAbstract()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float:
        ...
";
        var result = TypeCheck(source);
        Assert.True(result.Success);
    }
    
    [Fact]
    public void MultipleAbstractMethods_AllTreatedAsAbstract()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...
    def perimeter(self) -> float: ...
    def contains(self, x: float, y: float) -> bool: ...
";
        var result = TypeCheck(source);
        Assert.True(result.Success);
    }
    
    #endregion
    
    #region Explicit @abstract decorator (still valid)
    
    [Fact]
    public void ExplicitAbstractDecorator_StillWorks()
    {
        var source = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float: ...
";
        var result = TypeCheck(source);
        Assert.True(result.Success);
    }
    
    #endregion
    
    #region Mixed abstract and concrete methods
    
    [Fact]
    public void MixedAbstractAndConcrete_InAbstractClass()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...  # abstract
    
    def describe(self) -> str:     # concrete
        return ""shape""
";
        var result = TypeCheck(source);
        Assert.True(result.Success);
    }
    
    #endregion
    
    #region Ellipsis in concrete class (NotImplementedException)
    
    [Fact]
    public void EllipsisBody_InConcreteClass_NotTreatedAsAbstract()
    {
        var source = @"
class TodoService:
    def not_done_yet(self) -> int: ...
";
        var result = TypeCheck(source);
        Assert.True(result.Success);
        // Should NOT be abstract - will generate NotImplementedException
    }
    
    #endregion
    
    #region Error cases
    
    [Fact]
    public void AbstractDecorator_OnMethod_InConcreteClass_Error()
    {
        var source = @"
class Shape:
    @abstract
    def area(self) -> float: ...
";
        var result = TypeCheck(source);
        Assert.False(result.Success);
        Assert.Contains("abstract class", result.Errors[0].Message);
    }
    
    [Fact]
    public void AbstractDecorator_WithRealBody_Error()
    {
        var source = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float:
        return 0.0  # Error: abstract method cannot have implementation
";
        var result = TypeCheck(source);
        Assert.False(result.Success);
        Assert.Contains("...", result.Errors[0].Message);
    }
    
    #endregion
}
```

**Acceptance Criteria:**
- [ ] Implicit abstract detection tested
- [ ] Explicit decorator still works
- [ ] Mixed methods tested
- [ ] Concrete class with ellipsis tested
- [ ] Error cases tested with clear messages

---

## Task AM.3.2: Parser Tests for Inline Ellipsis

📁 **Files**: `src/Sharpy.Compiler.Tests/Parser/ParserInlineEllipsisTests.cs` (new or extend existing)

```csharp
public class ParserInlineEllipsisTests
{
    [Fact]
    public void ParseFunctionDef_InlineEllipsis()
    {
        var source = "def area(self) -> float: ...";
        var module = Parse(source);
        
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.Name.Should().Be("area");
        func.Body.Should().HaveCount(1);
        func.Body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<EllipsisLiteral>();
    }
    
    [Fact]
    public void ParseFunctionDef_InlineEllipsis_NoReturnType()
    {
        var source = "def do_something(self): ...";
        var module = Parse(source);
        
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.Body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<EllipsisLiteral>();
    }
    
    [Fact]
    public void ParseFunctionDef_InlineEllipsis_EquivalentToMultiLine()
    {
        var inlineSource = "def area(self) -> float: ...";
        var multiLineSource = @"def area(self) -> float:
    ...";
        
        var inlineFunc = Parse(inlineSource).Body[0] as FunctionDef;
        var multiLineFunc = Parse(multiLineSource).Body[0] as FunctionDef;
        
        // Bodies should be structurally identical
        inlineFunc.Body.Should().BeEquivalentTo(multiLineFunc.Body);
    }
    
    [Fact]
    public void ParseClassDef_WithInlineEllipsisMethods()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...
    def perimeter(self) -> float: ...
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        
        classDef.Body.Should().HaveCount(2);
        foreach (var stmt in classDef.Body)
        {
            var func = stmt.Should().BeOfType<FunctionDef>().Subject;
            func.Body[0].Should().BeOfType<ExpressionStatement>()
                .Which.Expression.Should().BeOfType<EllipsisLiteral>();
        }
    }
    
    [Fact]
    public void ParseInterface_WithInlineEllipsisMethods()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> None: ...
    def get_bounds(self) -> tuple[float, float]: ...
";
        var module = Parse(source);
        var interfaceDef = module.Body[0].Should().BeOfType<InterfaceDef>().Subject;
        
        interfaceDef.Body.Should().HaveCount(2);
    }
}
```

**Acceptance Criteria:**
- [ ] Inline ellipsis parses correctly
- [ ] No return type case works
- [ ] Inline and multi-line produce equivalent AST
- [ ] Works in class and interface context

---

## Task AM.3.3: CodeGen Tests for Abstract Methods

📁 **Files**: `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterAbstractTests.cs` (new or extend existing)

```csharp
public class RoslynEmitterAbstractMethodTests
{
    [Fact]
    public void GenerateAbstractMethod_ImplicitAbstract_NoBody()
    {
        // Method with ellipsis in @abstract class - should have no body
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...
";
        var code = CompileToCSharp(source);
        
        Assert.Contains("public abstract class Shape", code);
        Assert.Contains("public abstract double Area();", code);
        Assert.DoesNotContain("NotImplementedException", code);
    }
    
    [Fact]
    public void GenerateAbstractMethod_ExplicitDecorator_NoBody()
    {
        var source = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float: ...
";
        var code = CompileToCSharp(source);
        
        Assert.Contains("public abstract double Area();", code);
    }
    
    [Fact]
    public void GenerateConcreteMethod_WithEllipsis_ThrowsNotImplementedException()
    {
        var source = @"
class TodoService:
    def not_done(self) -> int: ...
";
        var code = CompileToCSharp(source);
        
        Assert.Contains("throw new System.NotImplementedException()", code);
    }
    
    [Fact]
    public void GenerateInterfaceMethod_WithInlineEllipsis_NoBody()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> None: ...
";
        var code = CompileToCSharp(source);
        
        Assert.Contains("void Draw();", code);
        Assert.DoesNotContain("NotImplementedException", code);
    }
}
```

**Acceptance Criteria:**
- [ ] Implicit abstract generates correct C#
- [ ] Explicit abstract still works
- [ ] Concrete class ellipsis generates NotImplementedException
- [ ] Interface methods correct

---

## Task AM.3.4: Fix Existing Tests Using Wrong Decorator

📁 **Files**: 
- `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterDefinitionTests.cs`
- `snippets/test_abstract_ellipsis.spy`
- Any other files using `@abstractmethod`

**Changes:**
Replace all instances of `@abstractmethod` with `@abstract`:

```csharp
// Before
new Decorator { Name = "abstractmethod" }

// After
new Decorator { Name = "abstract" }
```

```python
# Before
@abstractmethod
def area(self) -> float:
    ...

# After
@abstract
def area(self) -> float: ...

# Or simply (in @abstract class):
def area(self) -> float: ...
```

**Acceptance Criteria:**
- [ ] No references to `"abstractmethod"` in codebase
- [ ] All tests pass with correct decorator
- [ ] Test snippets updated

---

# Phase AM.4: File-Based Integration Tests

**Goal**: End-to-end tests using the streamlined syntax.

---

## Task AM.4.1: Create Streamlined Syntax Test Fixtures

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/inheritance/`

Create new test files using streamlined syntax:

**`abstract_class_streamlined.spy`:**
```python
@abstract
class Shape:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    def area(self) -> float: ...
    def perimeter(self) -> float: ...
    
    def describe(self) -> str:
        return f"{self.name}: area={self.area()}"

class Circle(Shape):
    radius: float
    
    def __init__(self, radius: float):
        super().__init__("Circle")
        self.radius = radius
    
    @override
    def area(self) -> float:
        return 3.14159 * self.radius * self.radius
    
    @override
    def perimeter(self) -> float:
        return 2.0 * 3.14159 * self.radius

def main() -> None:
    circle = Circle(5.0)
    print(circle.describe())
    print(circle.perimeter())
```

**`abstract_class_streamlined.expected`:**
```
Circle: area=78.53975
31.4159
```

**`interface_streamlined.spy`:**
```python
interface IGreeter:
    def greet(self, name: str) -> str: ...
    def farewell(self, name: str) -> str: ...

class FriendlyGreeter(IGreeter):
    @override
    def greet(self, name: str) -> str:
        return f"Hello, {name}!"
    
    @override
    def farewell(self, name: str) -> str:
        return f"Goodbye, {name}!"

def main() -> None:
    greeter = FriendlyGreeter()
    print(greeter.greet("World"))
    print(greeter.farewell("World"))
```

**`interface_streamlined.expected`:**
```
Hello, World!
Goodbye, World!
```

**Acceptance Criteria:**
- [ ] Abstract class with streamlined syntax compiles and runs
- [ ] Interface with streamlined syntax compiles and runs
- [ ] Output matches expected

---

## Task AM.4.2: Create Variations of Existing Tests

📁 **Files**: Various in `TestFixtures/inheritance/` and `TestFixtures/interfaces/`

For existing tests that use the verbose syntax, create streamlined variations:

| Existing Test | Streamlined Variation |
|--------------|----------------------|
| `inheritance/abstract_class.spy` | `inheritance/abstract_class_inline.spy` |
| `interfaces/basic_interface.spy` | `interfaces/basic_interface_inline.spy` |

Both versions should produce **identical output**.

**Acceptance Criteria:**
- [ ] At least 3 existing tests have streamlined variations
- [ ] All variations produce identical output to originals

---

## Task AM.4.3: Add Error Case Integration Tests

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/`

**`abstract_method_in_concrete_class.spy`:**
```python
class NotAbstract:
    @abstract
    def must_implement(self) -> int: ...
```

**`abstract_method_in_concrete_class.error`:**
```
abstract class
```

**`abstract_method_with_body.spy`:**
```python
@abstract
class Shape:
    @abstract
    def area(self) -> float:
        return 0.0
```

**`abstract_method_with_body.error`:**
```
must have '...'
```

**Acceptance Criteria:**
- [ ] Error cases compile with expected error messages
- [ ] Messages are helpful and actionable

---

# Phase AM.5: Documentation and Cleanup

**Goal**: Final documentation updates and code cleanup.

---

## Task AM.5.1: Update Implementation Walkthrough Docs

📁 **Files**: `docs/implementation_walkthrough/` (relevant files)

Update any implementation walkthrough documents that reference abstract method handling to reflect the new detection logic.

**Acceptance Criteria:**
- [ ] Walkthrough docs updated
- [ ] No references to `@abstractmethod`

---

## Task AM.5.2: Update Test Snippet

📁 **Files**: `snippets/test_abstract_ellipsis.spy`

Update to use streamlined syntax as the primary example:

```python
# Test abstract methods with streamlined ellipsis syntax

@abstract
class Shape:
    """Base shape class with abstract methods"""
    
    def area(self) -> float: ...
    def perimeter(self) -> float: ...
    
    def describe(self) -> str:
        return "I am a shape"

class Circle(Shape):
    def __init__(self, radius: float):
        self.radius = radius
    
    @override
    def area(self) -> float:
        return 3.14159 * self.radius * self.radius
    
    @override
    def perimeter(self) -> float:
        return 2.0 * 3.14159 * self.radius

# Concrete class with TODO stub (not abstract)
class TodoClass:
    def not_done_yet(self) -> int: ...  # Throws NotImplementedException

# Interface with streamlined syntax
interface IDrawable:
    def draw(self) -> None: ...
    def get_bounds(self) -> tuple[float, float, float, float]: ...
```

**Acceptance Criteria:**
- [ ] Snippet demonstrates streamlined syntax
- [ ] Comments explain the behavior

---

## Task AM.5.3: Remove `"abstractmethod"` References

📁 **Files**: All compiler source files

Search and remove/replace all references to the non-existent `"abstractmethod"` decorator:

```bash
grep -r "abstractmethod" src/
```

Files to check:
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
- Any test files

**Acceptance Criteria:**
- [ ] No references to `"abstractmethod"` in source code
- [ ] All tests pass after removal

---

# Summary

| Phase | Tasks | Priority | Complexity |
|-------|-------|----------|------------|
| AM.1 | Specification | High | Low |
| AM.2 | Compiler Implementation | High | Medium |
| AM.3 | Unit Tests | High | Medium |
| AM.4 | Integration Tests | Medium | Low |
| AM.5 | Documentation/Cleanup | Medium | Low |

**Dependencies:**
- AM.2 depends on AM.1 (spec must be finalized)
- AM.3 depends on AM.2 (tests need implementation)
- AM.4 depends on AM.2 (integration tests need working compiler)
- AM.5 can proceed in parallel

**Key Files to Modify:**
- `docs/language_specification/decorators.md`
- `docs/language_specification/interfaces.md`
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
- `src/Sharpy.Compiler/Parser/Parser.cs` (verify only)
- Multiple test files

**Estimated Total Effort:** Medium (localized changes to abstract detection logic)

**Breaking Changes:** None — existing `@abstract` decorator on methods still works, just becomes optional.
