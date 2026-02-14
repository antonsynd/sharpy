# Phase 5: Polish & Spec Corrections

This phase has two parts: (A) smaller implementation polish items and (B) spec document corrections. Part A items are independent and can be done in any order. Part B should be a single commit updating all four spec files.

---

## Part A: Implementation Polish

---

### A1. Constructor Chaining (`self.__init__()` -> `: this()`)

#### Context

The spec (`docs/language_specification/constructors.md`, lines 23-66) says that `self.__init__(...)` as the first statement in a constructor should lower to C#'s `: this(...)` constructor initializer. Currently, `super().__init__(...)` -> `: base(...)` works (implemented at line 258-298 in `RoslynEmitter.ClassMembers.cs`), but `self.__init__(...)` is not detected and falls through to normal statement generation, producing an invalid method call.

Without this feature, users cannot chain constructors within the same class, which is a standard pattern when multiple constructor overloads share initialization logic.

#### Files to Modify

1. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`** -- `GenerateConstructor()` method (line 211-406). This is the only file that needs modification. The existing `super().__init__()` detection pattern at lines 262-298 is the template.

2. **`src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs`** (or `.Statements.cs`) -- The type checker should validate that `self.__init__(...)` appears only as the first statement. Check whether this validation already exists or needs to be added.

#### Step-by-Step Implementation

1. **Extend the constructor initializer detection loop** (line 262-273 of `RoslynEmitter.ClassMembers.cs`). Currently the loop only looks for `super().__init__()`. Add a second pattern that matches `self.__init__()`:

   ```
   Pattern to detect:
   ExpressionStatement
     -> FunctionCall
       -> MemberAccess
         -> Object: Identifier with Name "self"
         -> Member: "__init__"
   ```

   In the existing `for` loop (line 262), add a second check after the `SuperExpression` check:

   ```csharp
   // Check for self.__init__() -> : this(...)
   if (func.Body[i] is ExpressionStatement es2 &&
       es2.Expression is FunctionCall selfCall &&
       selfCall.Function is MemberAccess ma2 &&
       ma2.Object is Identifier selfId &&
       string.Equals(selfId.Name, "self", StringComparison.OrdinalIgnoreCase) &&
       ma2.Member == DunderNames.Init)
   {
       selfInitIndex = i;
       break;
   }
   ```

2. **Generate `ThisConstructorInitializer` instead of `BaseConstructorInitializer`**. When `self.__init__()` is detected:

   ```csharp
   var thisInitializer = ConstructorInitializer(
       SyntaxKind.ThisConstructorInitializer,
       ArgumentList(SeparatedList(thisArgs)));
   ```

3. **Handle mutual exclusion**: A constructor cannot have both `super().__init__()` and `self.__init__()`. If both are detected, emit an error diagnostic. Use an existing code generation error code or allocate a new one.

4. **Skip the `self.__init__()` statement in body generation**, the same way `superInitIndex` is handled (line 309).

#### Decision Guidance

- **Should `self.__init__()` be allowed at positions other than index 0?** No. The spec says "must be the first statement" (line 45). If it appears elsewhere, emit a semantic error. However, the codegen can be lenient and still detect it at any position (matching the `super().__init__()` behavior which already searches the entire body), but the semantic validator should enforce the "first statement" rule.

- **Should validation live in TypeChecker or a Validator?** Follow the existing pattern: the `super().__init__()` call has no special semantic validation beyond being a valid call. Add validation for "first statement only" in `TypeChecker.Definitions.cs` alongside any existing constructor body checks, or in a validator if one exists for constructors.

#### Testing

1. **Integration test fixture**: Create `src/Sharpy.Compiler.Tests/Integration/TestFixtures/constructors/constructor_chaining_self_0001.spy`:
   ```python
   class Point:
       x: float
       y: float

       def __init__(self):
           self.__init__(0.0, 0.0)

       def __init__(self, x: float, y: float):
           self.x = x
           self.y = y

       def __init__(self, xy: float):
           self.__init__(xy, xy)

   p1 = Point()
   print(p1.x)
   print(p1.y)
   p2 = Point(3.0)
   print(p2.x)
   print(p2.y)
   ```
   Expected output:
   ```
   0
   0
   3
   3
   ```

2. **Error test**: `self.__init__()` not as first statement should produce a diagnostic.

3. **C# snapshot test** (optional): Verify the generated constructor has `: this(...)` initializer.

#### Commit Message

```
feat: Lower self.__init__() to C# constructor chaining (: this(...))
```

---

### A2. Enum `.name`, `.value`, and Iteration

#### Context

The spec (`docs/language_specification/enums.md`, lines 23-25, 40-56) specifies that enums support `.name` (returns the member name as a string), `.value` (returns the underlying value), and iteration (`for x in EnumType:`). Currently the compiler generates C# `enum` declarations for integer enums and `sealed class` with `static readonly string` fields for string enums (see `RoslynEmitter.TypeDeclarations.cs`, lines 666-823), but member access for `.name` and `.value` is not handled, and iteration over enum types is not supported.

This is a user-visible gap: any Sharpy program that uses `color.name` or `for c in Color:` will fail to compile.

#### Files to Modify

1. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`** -- `GenerateMemberAccess()` method. Add handling for `.name` and `.value` on enum-typed expressions.

2. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs`** -- `GenerateForStatement()` or wherever `for x in Type:` is handled. Need to detect when the iterable is an enum type and generate `Enum.GetValues()`.

3. **`src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`** -- Type checking for `.name` and `.value` member access on enum types.

4. **`src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs`** -- Type checking for `for x in EnumType:` where the iterable is a type name.

#### Step-by-Step Implementation

**For integer enums:**

1. **`.name` property**: In `GenerateMemberAccess()`, when the object's type is an integer enum and the member is `"name"`, generate:
   ```csharp
   // favorite.name -> Enum.GetName(typeof(Color), favorite)
   // or simply: favorite.ToString()
   ```
   The simplest C# equivalent is `.ToString()` which returns the member name for integer enums. Generate: `InvocationExpression(MemberAccessExpression(..., "ToString"))`.

2. **`.value` property**: Generate a cast to the underlying integer type:
   ```csharp
   // favorite.value -> (int)favorite
   ```
   Generate: `CastExpression(PredefinedType(Token(SyntaxKind.IntKeyword)), expr)`.

3. **Iteration (`for x in EnumType:`)**: Generate:
   ```csharp
   // for color in Color: -> foreach (var color in Enum.GetValues<Color>())
   // Or for C# 9.0 compat: foreach (Color color in Enum.GetValues(typeof(Color)))
   ```
   Since generated code targets C# 9.0 (for Sharpy.Core), use:
   ```csharp
   foreach (Color color in (Color[])System.Enum.GetValues(typeof(Color)))
   ```

**For string enums:**

String enums are `sealed class` with `static readonly string` fields. The `.name` and `.value` concepts don't apply in the same way because there's no enum member object -- values are just strings. This is a design decision:

- **Option A**: String enums don't support `.name`/`.value`/iteration -- document this limitation.
- **Option B**: Generate additional infrastructure (a backing array, lookup methods) to support these features.

**Recommendation**: Go with Option A for now. The spec says `.name` and `.value` are for "Simple Enums", and the integer enum is the primary form. String enums are already lowered to a different structure. Add a TODO for string enum `.name`/`.value` support.

#### Decision Guidance

- **Which C# API for enum iteration?** Use `System.Enum.GetValues(typeof(T))` cast to `T[]`. This is compatible with `netstandard2.0`/`netstandard2.1`. The generic `Enum.GetValues<T>()` requires .NET 5+ and won't work in all Sharpy.Core targets.

- **Where to detect "iterating over an enum type"?** In the for-loop codegen, check if the iterator expression is an `Identifier` whose symbol resolves to a `TypeSymbol` with `TypeKind.Enum`. This is a special case since normally for-loop iterables are expressions, not types.

- **Semantic analysis for `for x in EnumType:`**: The TypeChecker needs to handle the case where a type name appears as the iterable in a for loop. Currently this likely produces a "not iterable" error. Add a special case in the type checker to recognize enum types as iterable, with element type being the enum type itself.

#### Testing

1. **Integer enum `.name`/`.value`**:
   ```python
   enum Color:
       RED = 1
       GREEN = 2
       BLUE = 3

   c = Color.RED
   print(c.name)
   print(c.value)
   ```
   Expected: `Red\n1\n` (note: `.ToString()` returns PascalCase mangled name)

   **Important consideration**: The mangled enum member names are PascalCase (`Red`, `Green`, `Blue`), but the spec expects `.name` to return `"RED"`. This is a conflict. Options:
   - Generate a lookup dictionary mapping PascalCase names back to original CAPS_SNAKE_CASE names.
   - Accept that `.name` returns the C# name (PascalCase).
   - Store original names as attributes/comments.

   **Recommendation**: For now, accept PascalCase names from `.name`. Open a tracking issue for faithful `.name` behavior. This matches the "Axiom 1 (.NET) wins" principle.

2. **Enum iteration**:
   ```python
   enum Color:
       RED = 1
       GREEN = 2
       BLUE = 3

   for color in Color:
       print(color)
   ```

#### Commit Message

```
feat: Support enum .name, .value properties and for-in iteration
```

---

### A3. Generic Type Aliases

#### Context

The spec (`docs/language_specification/type_aliases.md`, lines 12-13) shows generic aliases like `type Callback[T] = (T) -> None`. Currently, the `TypeAlias` AST node (`src/Sharpy.Compiler/Parser/Ast/Statement.cs`, line 544-563) has no `TypeParameters` property. The parser (`src/Sharpy.Compiler/Parser/Parser.Definitions.cs`, lines 744-812) reads `type Name = ...` but does not parse `[T]` type parameters after the name.

Without this, users cannot create reusable generic type abbreviations, which are common for callback types and container aliases.

#### Files to Modify

1. **`src/Sharpy.Compiler/Parser/Ast/Statement.cs`** -- Add `TypeParameters` to `TypeAlias` record.
2. **`src/Sharpy.Compiler/Parser/Parser.Definitions.cs`** -- `ParseTypeAlias()` method: parse optional `[T, U, ...]` after the name.
3. **`src/Sharpy.Compiler/Semantic/NameResolver.cs`** -- Handle generic type alias registration.
4. **`src/Sharpy.Compiler/Semantic/TypeResolver.cs`** -- Resolve generic type alias references with type arguments.
5. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs`** or `.ModuleClass.cs` -- Handle generic type alias in codegen (inline expansion or `using` alias).

#### Step-by-Step Implementation

1. **Add `TypeParameters` to the `TypeAlias` AST node**:
   ```csharp
   public record TypeAlias : Statement
   {
       public string Name { get; init; } = "";
       public ImmutableArray<TypeParameterDef> TypeParameters { get; init; }
           = ImmutableArray<TypeParameterDef>.Empty;
       public TypeAnnotation? Type { get; init; }
       public FunctionType? FunctionType { get; init; }
       // ...
   }
   ```

2. **Update `ParseTypeAlias()` to parse type parameters**. After `ExpectIdentifier()` (line 751), check for `[`:
   ```csharp
   var name = ExpectIdentifier();

   // Parse optional type parameters: type Callback[T] = ...
   var typeParams = ImmutableArray<TypeParameterDef>.Empty;
   if (Current.Type == TokenType.LeftBracket)
   {
       typeParams = ParseTypeParameterList();
   }

   Expect(TokenType.Assign);
   ```

   Reuse the existing `ParseTypeParameterList()` method that is used for generic class/function definitions. Verify it exists by searching for it in `Parser.Definitions.cs`.

3. **Register generic type aliases in `NameResolver`**. The `TypeAliasSymbol` likely needs a `TypeParameters` property. Check the existing `TypeAliasSymbol` record in `Symbol.cs` and add type parameters if missing.

4. **Type resolution**: When a generic alias is used with arguments (e.g., `Callback[int]`), the type resolver should substitute the type parameters in the alias's target type. For example, `Callback[int]` with `type Callback[T] = (T) -> None` resolves to `(int) -> None`.

5. **Code generation**: Generic type aliases are purely a compile-time construct. They expand at use sites during type resolution. No codegen changes needed beyond ensuring the expanded types are correctly mapped.

#### Decision Guidance

- **Should generic aliases support constraints?** Follow the spec. The spec shows `type Callback[T] = (T) -> None` without constraints. Support unconstrained type parameters first; constraints can be added later by reusing the same `TypeParameterDef` structure (which already supports `Constraints`).

- **Should generic aliases be emitted as C# `using` aliases?** No. C# `using` aliases don't support generic parameters. Generic type aliases must be expanded inline at every use site during type resolution.

- **Where does substitution happen?** In `TypeResolver.cs` during type annotation resolution. When a `TypeAnnotation` references a `TypeAliasSymbol` that has type parameters, apply the provided type arguments to the alias body.

#### Testing

1. **Simple generic alias**:
   ```python
   type Mapper[T] = (T) -> T

   def apply_mapper(m: Mapper[int], x: int) -> int:
       return m(x)

   result = apply_mapper(lambda x: x * 2, 5)
   print(result)
   ```
   Expected: `10`

2. **Generic alias with collection type**:
   ```python
   type IntList = list[int]
   type Pair[T] = tuple[T, T]

   p: Pair[int] = (1, 2)
   print(p)
   ```

3. **Error test**: Using wrong number of type arguments should produce a diagnostic.

#### Commit Message

```
feat: Support generic type aliases (type Callback[T] = (T) -> None)
```

---

### A4. Multi-`for` Comprehensions (SelectMany)

#### Context

The spec (`docs/language_specification/comprehensions.md`, lines 50-71, 73-81) defines multi-`for` comprehensions that lower to LINQ `SelectMany`. Currently, `GenerateComprehensionChain()` in `RoslynEmitter.Expressions.cs` (line 946) emits error `SPY0515` ("Nested comprehensions (multiple 'for' clauses) are not yet supported") when a second `ForClause` is encountered (lines 990-994).

This is a commonly used Python pattern. Users writing `[(x, y) for x in xs for y in ys]` will hit this error.

#### Files to Modify

1. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`** -- `GenerateComprehensionChain()` method (line 946). Replace the error emission for `ForClause` with `SelectMany` generation.

2. **`src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`** -- Ensure type checking handles multiple `ForClause` entries in comprehension clauses (likely already works since the parser produces multiple clauses).

#### Step-by-Step Implementation

1. **Replace the `ForClause` error case** in `GenerateComprehensionChain()` (lines 990-994) with `SelectMany` logic.

2. **Generate SelectMany chain**. For each additional `ForClause`, chain a `.SelectMany()` call:

   ```python
   [(x, y) for x in range(3) for y in range(3)]
   ```
   becomes:
   ```csharp
   Enumerable.Range(0, 3)
       .SelectMany(x => Enumerable.Range(0, 3), (x, y) => (x, y))
       .ToList()
   ```

   The two-argument `SelectMany` overload is: `SelectMany(x => innerSequence, (x, y) => resultSelector)`. This is the cleanest form because it keeps both variables in scope for the result selector.

3. **Handle `IfClause` after inner `ForClause`**. If clauses after the inner for should be `.Where()` calls on the `SelectMany` result:

   ```python
   [(x, y) for x in range(3) for y in range(3) if x != y]
   ```
   becomes:
   ```csharp
   Enumerable.Range(0, 3)
       .SelectMany(x => Enumerable.Range(0, 3), (x, y) => new { x, y })
       .Where(t => t.x != t.y)
       .Select(t => (t.x, t.y))
       .ToList()
   ```

   **Simpler approach**: Use an anonymous type or tuple to carry variables through the chain. However, since Sharpy targets C# 9.0, value tuples `(x, y)` are available.

4. **Refactored approach**: Instead of modifying `GenerateComprehensionChain()`, consider a new method `GenerateMultiForComprehension()` that handles the general case. The existing single-for path can remain as-is for the common case.

   The algorithm for N for-clauses with interleaved if-clauses:
   - Start with the first for-clause's iterator
   - For each subsequent for-clause, wrap in `SelectMany` using a transparent identifier tuple
   - For each if-clause, add `.Where()`
   - Final `.Select()` produces the element expression

#### Decision Guidance

- **Anonymous types vs value tuples for transparent identifiers?** Use value tuples `(x, y)` since C# 9.0 supports them. This avoids needing anonymous types.

- **How many for-clauses to support?** Start with 2 (the most common case: nested loops). Generalize to N if the implementation naturally supports it (it should with the tuple-chaining approach).

- **Should if-clauses between for-clauses be handled?** Yes. `[(x, y) for x in xs if x > 0 for y in ys]` means "filter xs first, then cross with ys." This means if-clauses before the second for should apply to the outer sequence, and if-clauses after the second for apply to the combined sequence. Process clauses left-to-right.

#### Testing

1. **Basic two-for comprehension**:
   ```python
   pairs = [(x, y) for x in range(3) for y in range(3)]
   for p in pairs:
       print(p)
   ```

2. **With filter**:
   ```python
   filtered = [(x, y) for x in range(3) for y in range(3) if x != y]
   print(len(filtered))
   ```
   Expected: `6`

3. **Later clause references earlier variable**:
   ```python
   triangular = [(x, y) for x in range(4) for y in range(x)]
   print(len(triangular))
   ```
   Expected: `6` (pairs: (1,0), (2,0), (2,1), (3,0), (3,1), (3,2))

4. **Dict comprehension with two for-clauses** (if applicable).

5. **Error test**: Same variable name in multiple for-clauses should error (per spec lines 163-180).

#### Commit Message

```
feat: Support multi-for comprehensions via LINQ SelectMany
```

---

### A5. Partial Application (`add(5, _)` -> lambda)

#### Context

The spec (`docs/language_specification/partial_application.md`) defines partial application using `_` as a placeholder in function call arguments. Currently nothing is implemented -- there are no AST nodes, no parser support, and no codegen. The `_` identifier has no special handling in call argument positions.

This is a significant feature (the spec is 321 lines) but the core lowering is straightforward: `f(a, _, c)` becomes `(b) => f(a, b, c)`.

#### Files to Modify

1. **`src/Sharpy.Compiler/Parser/Ast/Expression.cs`** -- Add `PlaceholderExpression` AST node.
2. **`src/Sharpy.Compiler/Parser/Parser.Expressions.cs`** or `.Primaries.cs` -- Parse `_` as `PlaceholderExpression` in call argument context.
3. **`src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`** -- Type-check partial application calls. Infer the resulting function type.
4. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`** -- Lower partial application to lambda.
5. **`src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs`** -- Add visitor method for new node.

#### Step-by-Step Implementation

1. **Add `PlaceholderExpression` AST node**:
   ```csharp
   /// <summary>
   /// Placeholder expression (_) used in partial application.
   /// </summary>
   public record PlaceholderExpression : Expression;
   ```

2. **Parser changes**. In the primary expression parser, when `_` is encountered in a function call argument position, produce `PlaceholderExpression` instead of `Identifier("_")`. The disambiguation rule from the spec: `_` in function call argument positions is always a placeholder; in `case` pattern positions it's always a wildcard.

   The simplest approach: parse `_` as `Identifier("_")` as today, then in the call expression parser, detect `Identifier("_")` arguments and rewrite them to `PlaceholderExpression`. This avoids parser-level context sensitivity.

   **Alternative (cleaner)**: Add a flag or context to the parser that tracks "we are inside function call arguments" and parse `_` as `PlaceholderExpression` directly. But this adds complexity to the parser. The post-hoc rewrite in the call parser is simpler.

3. **Detect partial application in `FunctionCall` generation**. In `GenerateExpression()` for `FunctionCall`, check if any argument is a `PlaceholderExpression` (or `Identifier` with name `"_"` if using the rewrite approach). If so:

   a. Count the placeholders to determine lambda parameter count.
   b. Generate fresh parameter names (`__p0`, `__p1`, ...).
   c. Build the argument list substituting placeholders with parameter references.
   d. Wrap in a `SimpleLambdaExpression` or `ParenthesizedLambdaExpression`.

   ```csharp
   // add(5, _) -> (__p0) => Add(5, __p0)
   // format(_, _, "Smith") -> (__p0, __p1) => Format(__p0, __p1, "Smith")
   ```

4. **Type inference**: The type checker must infer the function type of the partial application result. Given `f: (A, B, C) -> R` and `f(a, _, c)`, the result type is `(B) -> R`. This requires:
   - Resolving the target function's type
   - Identifying which parameters correspond to placeholders
   - Building a `FunctionType` with the placeholder parameter types and the original return type

5. **Operator sections** (`(_ * 2)`) can be deferred to a follow-up. They require different parsing (placeholder in binary expression context). The core partial application for function calls is the priority.

#### Decision Guidance

- **Should `_` be a new AST node or reuse `Identifier`?** Prefer a new `PlaceholderExpression` node. It makes intent explicit in the AST and avoids confusion with legitimate uses of `_` as a variable name (e.g., `_ = compute()` to discard). The parser can create `PlaceholderExpression` when `_` appears in a call argument list.

- **Should operator sections be included in this PR?** No. Implement function call partial application first. Operator sections (`(_ * 2)`) require additional parser changes (detecting `_` in binary expression context within parentheses) and can be a follow-up.

- **What about nested partial application?** `f(g(_, 1), _)` -- the inner `g(_, 1)` is itself a partial application. This should work naturally since each call is processed independently. The inner call produces a lambda, which becomes an argument to the outer call.

#### Testing

1. **Basic partial application**:
   ```python
   def add(x: int, y: int) -> int:
       return x + y

   add_five = add(5, _)
   print(add_five(3))
   ```
   Expected: `8`

2. **Multiple placeholders**:
   ```python
   def format_name(first: str, middle: str, last: str) -> str:
       return f"{first} {middle} {last}"

   format_smith = format_name(_, _, "Smith")
   print(format_smith("John", "Paul"))
   ```
   Expected: `John Paul Smith`

3. **Error test**: `_` in keyword argument position should error.

#### Commit Message

```
feat: Implement partial application (f(a, _, c) -> lambda lowering)
```

---

### A6. User-Defined Method Overloading

#### Context

The spec (`docs/language_specification/function_parameters.md`, lines 122-141) says methods can be overloaded if they differ in parameter count or types. Currently, only constructors (`__init__`) and dunder methods support overloading in `NameResolver.cs` (lines 536-558). Regular methods are registered with `_symbolTable.Define(funcSymbol)` (line 563) which will throw or silently overwrite on duplicate names.

Without this, users cannot define multiple overloads of a regular method, which is a fundamental C# capability that the spec promises.

#### Files to Modify

1. **`src/Sharpy.Compiler/Semantic/NameResolver.cs`** -- `ResolveMethodDeclaration()` (around line 530). Change regular method registration to allow overloads.
2. **`src/Sharpy.Compiler/Semantic/Symbol.cs`** -- `TypeSymbol` may need an overload tracking mechanism for regular methods (similar to `OperatorMethods` dictionary).
3. **`src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`** -- Method call resolution needs to perform overload resolution among candidates.
4. **`src/Sharpy.Compiler/Semantic/SymbolTable.cs`** -- May need a method to register overloaded symbols.
5. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`** -- No changes expected; each `FunctionDef` is already independently emitted as a C# method.

#### Step-by-Step Implementation

1. **Add overload tracking to `TypeSymbol`**. Add a dictionary similar to `OperatorMethods`:
   ```csharp
   // Regular method overloads
   // Maps method names to lists of overloads
   public Dictionary<string, List<FunctionSymbol>> MethodOverloads { get; init; } = new();
   ```

2. **Modify `ResolveMethodDeclaration()` in `NameResolver.cs`**. Replace the current line 562-563:
   ```csharp
   // Current: For regular methods, register in symbol table normally
   _symbolTable.Define(funcSymbol);
   ```
   With overload-aware registration:
   ```csharp
   // Track overloads
   if (!owningType.MethodOverloads.TryGetValue(method.Name, out var methodOverloads))
   {
       methodOverloads = new List<FunctionSymbol>();
       owningType.MethodOverloads[method.Name] = methodOverloads;
   }
   methodOverloads.Add(funcSymbol);

   // Only register the first overload in the symbol table
   if (methodOverloads.Count == 1)
   {
       _symbolTable.Define(funcSymbol);
   }
   ```

3. **Update method call resolution in TypeChecker**. When resolving a method call `obj.method(args)`, look up the method name in the target type's `MethodOverloads` dictionary (falling back to `Methods` list). If multiple overloads exist, perform overload resolution based on argument count and types. Follow C# overload resolution rules as described in the spec.

4. **Module-level function overloading**. The spec shows module-level function overloading too. This requires similar changes to how module-level functions are registered. Check `ResolveModuleLevelFunction()` or equivalent in `NameResolver.cs`.

#### Decision Guidance

- **Should module-level functions also support overloading?** Yes, the spec example (lines 125-131) shows module-level functions. Implement for both methods and module-level functions.

- **How to handle overload resolution?** Reuse or extend the existing overload resolution logic. The `BuiltinRegistry.GetFunctionOverloads()` pattern already exists for builtins. Create a general-purpose overload resolution method that can be used for both builtins and user-defined overloads. Start with simple overload resolution (parameter count + exact type match), then add implicit conversion ranking later.

- **What about the `Methods` list on `TypeSymbol`?** Keep it. `Methods` stores all methods (including all overloads). `MethodOverloads` is an index for fast lookup by name. Both are populated during resolution.

#### Testing

1. **Method overloading by parameter count**:
   ```python
   class Printer:
       def show(self, x: int) -> None:
           print(f"int: {x}")

       def show(self, x: str) -> None:
           print(f"str: {x}")

   p = Printer()
   p.show(42)
   p.show("hello")
   ```
   Expected:
   ```
   int: 42
   str: hello
   ```

2. **Module-level function overloading**:
   ```python
   def process(value: int) -> str:
       return f"Integer: {value}"

   def process(value: str) -> str:
       return f"String: {value}"

   print(process(42))
   print(process("hello"))
   ```

3. **Error test**: Ambiguous overload should produce a diagnostic.

#### Commit Message

```
feat: Support user-defined method and function overloading
```

---

### A7. Flexible Arguments (Positional-Only `/`, Keyword-Only `*`)

#### Context

The spec (`docs/language_specification/flexible_arguments.md`) defines three tiers of argument flexibility. Tier 0 (positional-only `/` and keyword-only `*` markers) is compile-time only with zero runtime cost. Currently nothing is implemented -- the parser does not recognize `/` or bare `*` as parameter separators, and there is no `PositionalOnly`/`KeywordOnly` tracking on the `Parameter` AST node. No matches were found for these terms in the compiler source.

This is a large feature. Start with Tier 0 only (the `/` and `*` markers with compile-time validation). Tiers 1 and 2 (`@kwargs` struct generation and `@dynamic_kwargs`) should be deferred to a follow-up.

#### Files to Modify

1. **`src/Sharpy.Compiler/Parser/Ast/Statement.cs`** -- Add `ParameterKind` enum and property to `Parameter` record, or add sentinel parameters for `/` and `*`.
2. **`src/Sharpy.Compiler/Parser/Parser.Definitions.cs`** -- `ParseParameterList()` or equivalent. Parse `/` and `*` as parameter separators.
3. **`src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`** -- Validate call sites: positional-only parameters cannot be passed by name, keyword-only parameters must be passed by name.
4. **`src/Sharpy.Compiler/Semantic/Validation/`** -- Possibly a new validator or extension of `DefaultParameterValidator` for parameter ordering rules.
5. **`src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs`** -- New diagnostic codes for positional-only/keyword-only violations.

#### Step-by-Step Implementation

1. **Add `ParameterKind` to the `Parameter` record**:
   ```csharp
   public enum ParameterKind
   {
       Normal,         // Can be passed positionally or by name
       PositionalOnly, // Before /
       KeywordOnly     // After *
   }

   public record Parameter
   {
       // ... existing properties ...
       public ParameterKind Kind { get; init; } = ParameterKind.Normal;
   }
   ```

2. **Parse `/` and `*` separators**. In the parameter list parser:
   - When `/` is encountered as a "parameter", mark all preceding parameters as `PositionalOnly`.
   - When bare `*` (not `*args`) is encountered, mark all subsequent parameters as `KeywordOnly`.
   - Validate ordering: `/` before `*`, each appears at most once.

   The `/` token is `TokenType.Slash` and `*` is `TokenType.Star`. The parser must distinguish bare `*` (keyword-only marker) from `*args` (variadic parameter). The distinction: bare `*` is followed by `,` or `)`, while `*args` is followed by an identifier.

3. **Call-site validation**. In the TypeChecker, when resolving a function call with named arguments:
   - If a named argument targets a `PositionalOnly` parameter, emit error: `"'param_name' is positional-only"`.
   - If a positional argument is provided for a `KeywordOnly` parameter, emit error: `"'param_name' is keyword-only"`.

4. **No codegen changes needed**. The C# signature is identical regardless of `/` and `*` markers. These are purely compile-time Sharpy semantics.

5. **Add diagnostic codes**:
   ```csharp
   public const string PositionalOnlyViolation = "SPY0280"; // or next available
   public const string KeywordOnlyViolation = "SPY0281";
   ```

#### Decision Guidance

- **Should `/` and `*` be stored as sentinel parameters in the AST, or as properties on regular parameters?** Use properties on regular parameters (`ParameterKind`). Sentinel parameters would complicate parameter iteration throughout the codebase. The parser sets the `Kind` based on the position relative to `/` and `*`.

- **How to handle `*args` acting as the `*` separator?** Per the spec (line 39): "`*args` (variadic) implicitly acts as the `*` marker for keyword-only separation." If a variadic parameter is present, all parameters after it are keyword-only. This is already implicitly true in C# (`params` must be last), but Sharpy allows keyword-only parameters after `*args` (they would have default values).

- **Should Tier 1 and Tier 2 be included?** No. Tier 0 is the foundation. Implement Tiers 1-2 in separate PRs.

#### Testing

1. **Positional-only enforcement**:
   ```python
   def set_position(x: int, y: int, /) -> None:
       print(f"{x}, {y}")

   set_position(10, 20)
   ```
   Expected: `10, 20`

2. **Error: named arg for positional-only**:
   ```python
   def set_position(x: int, y: int, /) -> None:
       pass

   set_position(x=10, y=20)
   ```
   Expected: compile error mentioning positional-only.

3. **Keyword-only enforcement**:
   ```python
   def configure(*, host: str, port: int = 8080) -> None:
       print(f"{host}:{port}")

   configure(host="localhost")
   ```
   Expected: `localhost:8080`

4. **Error: positional arg for keyword-only**:
   ```python
   def configure(*, host: str) -> None:
       pass

   configure("localhost")
   ```
   Expected: compile error mentioning keyword-only.

#### Commit Message

```
feat: Implement Tier 0 flexible arguments (positional-only /, keyword-only *)
```

---

## Part B: Spec Document Corrections

These are cases where the spec needs to be updated to match intentional implementation decisions.

---

### B1. `operator_overloading.md` -- Remove Unsupported Dunders

**File to modify**: `docs/language_specification/operator_overloading.md`

**What to change**:

The "Arithmetic Operators" table (lines 12-24) currently lists:

| Current row | Action |
|-------------|--------|
| `__truediv__(self, other: T)` ... Binary `/` | Remove this row |
| `__floordiv__(self, other: T)` ... Binary `//` | Remove this row |
| `__pow__(self, other: T)` ... Binary `**` | Remove this row |

The authoritative dunder methods spec (`docs/language_specification/dunder_methods.md`, lines 108-110) explicitly states:

> Sharpy does not support `__pow__` or `__floordiv__` as these are not overridable operators in C#, and as a result, `__truediv__` is renamed to `__div__` to reflect the lack of a contrasting `__floordiv__`.

The operator mapping table (lines 134-144) also needs correction:

| Current row | Corrected row |
|-------------|---------------|
| `/` ... `__truediv__` ... `operator /` | `/` ... `__div__` ... `operator /` |
| `//` ... `__floordiv__` ... `(method call)` | Remove this row |
| `**` ... `__pow__` ... `(method call)` | Remove this row |

Additionally, in the "Dunder Method Signatures" header table, replace the `__truediv__` row with `__div__`, and remove the `__floordiv__` and `__pow__` rows.

Add a note after the arithmetic operators table:

```markdown
**Note:** `__truediv__`, `__floordiv__`, and `__pow__` are not supported in Sharpy. Use `__div__` for the `/` operator. For `//` (floor division) and `**` (power), use `math.floor_div()` and `math.pow()` respectively. See [dunder_methods.md](dunder_methods.md) for details.
```

**Rationale**: The `operator_overloading.md` spec contradicts the authoritative `dunder_methods.md` spec. The dunder_methods spec is canonical and reflects the intentional design decision that `//` and `**` are not overloadable operators in C#, so their corresponding dunders are unsupported.

**Commit message** (combined with all Part B changes):
```
fix(spec): Correct operator_overloading, keywords, naming_conventions, builtin_functions docs
```

---

### B2. `keywords.md` -- Add `super`

**File to modify**: `docs/language_specification/keywords.md`

**What to change**:

The `super` keyword is implemented in the lexer (`src/Sharpy.Compiler/Lexer/Lexer.cs`, line 136, as `TokenType.Super`) and parser (`SuperExpression` AST node at `src/Sharpy.Compiler/Parser/Ast/Expression.cs`, line 753), but is missing from the Hard Keywords table in the spec.

Add a row to the Hard Keywords table (between `struct` and `True`, to maintain alphabetical order):

```markdown
| `super` | Super class access |
```

The complete insertion point is after line 43 (`| struct | ...`) and before line 44 (`| True | ...`):

```markdown
| `struct` | Struct declaration |
| `super` | Super class access |
| `True` | Boolean true literal |
```

**Rationale**: `super` is a reserved keyword in the lexer (mapped to `TokenType.Super`), actively used for `super().__init__()` and `super().method()` calls, and implemented with its own AST node (`SuperExpression`). Its omission from the keyword table is an oversight.

---

### B3. `naming_conventions.md` -- Clarify Local Variable camelCase

**File to modify**: `docs/language_specification/naming_conventions.md`

**What to change**:

The table at lines 1-17 currently says:

```markdown
| Local variable | `snake_case` | (unchanged) |
```

This is incorrect. The `NameMangler` (`src/Sharpy.Compiler/CodeGen/NameMangler.cs`, line 276) maps `NameContext.Variable` to `ToCamelCase()`, meaning local variables like `result_count` become `resultCount` in generated C#.

The `name_mangling.md` spec (line 63, 217) correctly documents this:
> **For camelCase target** (parameters, local variables)
> `Local variable | camelCase | result_count -> resultCount`

Update the naming conventions table:

```markdown
| Local variable | `snake_case` | `camelCase` |
```

**Rationale**: The naming conventions table contradicts both the implementation and the more detailed `name_mangling.md` spec. The NameMangler applies `ToCamelCase` for both `Variable` and `Parameter` contexts. The "(unchanged)" notation is misleading and incorrect.

---

### B4. `builtin_functions.md` -- Clarify `repr()` and `hash()` Behavior

**File to modify**: `docs/language_specification/builtin_functions.md`

**What to change**:

**For `repr()`** (lines 298-305):

The current text says:
```markdown
| `repr(x)` | Debug representation | Calls `__repr__` if defined, else `__str__`, else `.ToString()` |

**`repr(x)`** returns a string representation suitable for debugging:
- For Sharpy types with `__repr__`: calls `__repr__`
- Fallback: tries `__str__`, then `.ToString()`
- Typically includes type name and distinguishing attributes
```

The `dunder_methods.md` spec (line 228) explicitly states:
> `__repr__(self) -> str` | Not supported | No direct C# equivalent; use `__str__` for string representation

Since `__repr__` is unsupported, `repr()` as a builtin function that dispatches to `__repr__` cannot work as described. Update to:

```markdown
| `repr(x)` | Debug representation | Not yet implemented |

**`repr(x)`** is not yet implemented. The `__repr__` dunder is not supported in Sharpy (see [dunder_methods.md](dunder_methods.md)). For string representation, use `str(x)` which dispatches to `__str__` or `.ToString()`.
```

**For `hash()`** (lines 299, 306-310):

The current text is:
```markdown
| `hash(x)` | Hash code | Calls `__hash__` if defined, else `.GetHashCode()` |

**`hash(x)`** returns the hash code for use in dictionaries and sets:
- For Sharpy types with `__hash__`: calls `__hash__`
- For all types: falls back to `.GetHashCode()`
```

This is actually correct behavior. The `__hash__` dunder lowers to `GetHashCode()` override (`dunder_methods.md`, line 190), so `hash(x)` calling `GetHashCode()` correctly invokes the user's `__hash__` body. No change needed for `hash()`.

However, add a clarifying note:

```markdown
**`hash(x)`** returns the hash code for use in dictionaries and sets:
- Calls `.GetHashCode()` on the object
- If `__hash__` is defined, it compiles to a `GetHashCode()` override, so `hash(x)` automatically invokes the user's implementation
- If `__eq__` is defined, `__hash__` must also be defined (and vice versa)
```

**Rationale**: The `repr()` entry creates false expectations about `__repr__` support. The `hash()` entry is technically correct but could benefit from clarifying how `__hash__` -> `GetHashCode()` -> `hash()` works end-to-end.

---

### Part B Combined Commit Message

All four spec corrections should be committed together:

```
fix(spec): Correct operator_overloading, keywords, naming_conventions, builtin_functions docs

- operator_overloading.md: Remove __truediv__, __floordiv__, __pow__ from supported
  dunders; replace with __div__ for / operator (matches dunder_methods.md)
- keywords.md: Add missing `super` keyword to hard keywords table
- naming_conventions.md: Fix local variable column from "(unchanged)" to "camelCase"
  (matches NameMangler implementation and name_mangling.md)
- builtin_functions.md: Mark repr() as not yet implemented (__repr__ is unsupported);
  clarify hash() dispatch through GetHashCode()
```
