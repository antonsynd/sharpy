# Sharpy Implementation Status v1.0

This document tracks which features from the [Sharpy Language Reference v1](../specs/sharpy_language_reference_v1.md) are implemented in the compiler. Use this as a reference to identify remaining work and generate tasks for implementation.

**Last Updated**: December 3, 2025 (Audit #5)
**Verified Against**: `mainline` branch
**Audit Scope**: Keywords, AST nodes, CodeGen NotImplementedException locations, Semantic analysis, Standard library, Test coverage mapping, TokenType verification, Language Reference cross-check

---

## Overview

| Version | Focus Area | Implementation Status |
|---------|------------|----------------------|
| **v0.1** | Core Language | ⚠️ ~95% Complete (missing try-else, dunder invocation rules) |
| **v0.2** | Nullability & Collections | ✅ Complete (except star unpacking) |
| **v0.3** | Structs, Interfaces, OOP | ⚠️ ~95% Complete |
| **v0.4** | Generics | ⚠️ ~85% Complete |
| **v0.5** | Enums & Operators | ✅ Complete |
| **v0.6** | Extended Syntax | ⚠️ ~90% Complete |
| **v0.7** | Pattern Matching | ❌ Not Implemented |
| **v0.8** | Type Aliases & ADTs | ❌ Not Implemented |
| **v0.9** | Comprehensions & Properties | ⚠️ ~60% Complete |
| **v1.0** | Resources & Async | ❌ Not Implemented |

---

## v0.1 — Core Language ⚠️ ~95% COMPLETE

### Lexical Structure

| Feature | Lexer | Parser | CodeGen | Tests | Status |
|---------|-------|--------|---------|-------|--------|
| UTF-8 source files | ✅ | - | - | ✅ | ✅ |
| 4-space indentation | ✅ INDENT/DEDENT tokens | - | ✅ → `{ }` | ✅ | ✅ |
| Single-line comments (`#`) | ✅ `TokenType.Comment` | - | ✅ stripped | ✅ | ✅ |
| Identifiers | ✅ `TokenType.Identifier` | ✅ `Identifier` | ✅ | ✅ | ✅ |
| Backtick literal names | ✅ `TokenType.Backtick` | ✅ | ✅ | ✅ | ✅ |
| Line continuation (`\`) | ✅ `TokenType.Backslash` | ✅ | ✅ | ✅ | ✅ |
| Implicit continuation (brackets) | ✅ | ✅ | ✅ | ✅ | ✅ |

### Keywords

| Keyword | Token | Status |
|---------|-------|--------|
| `def`, `class`, `struct`, `interface`, `enum` | ✅ | ✅ |
| `if`, `elif`, `else` | ✅ | ✅ |
| `while`, `for`, `in` | ✅ | ✅ |
| `return`, `break`, `continue`, `pass` | ✅ | ✅ |
| `try`, `except`, `finally`, `raise`, `assert` | ✅ | ✅ |
| `import`, `from`, `as` | ✅ | ✅ |
| `and`, `or`, `not`, `is` | ✅ | ✅ |
| `const` | ✅ | ✅ |
| `True`, `False`, `None` | ✅ | ✅ |
| `lambda` | ✅ | ✅ (v0.4) |
| `with` | ✅ Token exists | ❌ Not implemented |
| `auto` | ✅ | ⚠️ Partial (v0.8) |

### Literals

| Literal Type | Lexer | Parser | CodeGen | Status |
|--------------|-------|--------|---------|--------|
| Integer (`42`, `1_000_000`) | ✅ `TokenType.Integer` | ✅ `IntegerLiteral` | ✅ | ✅ |
| Integer suffixes (`L`, `u`, `UL`) | ✅ | ✅ | ✅ | ✅ |
| Float (`3.14`, `0.5`) | ✅ `TokenType.Float` | ✅ `FloatLiteral` | ✅ | ✅ |
| Float suffixes (`f`, `d`, `m`) | ✅ | ✅ | ✅ | ✅ |
| String (single/double quotes) | ✅ `TokenType.String` | ✅ `StringLiteral` | ✅ | ✅ |
| Multi-line string (`"""..."""`) | ✅ | ✅ | ✅ | ✅ |
| Raw string (`r"..."`) | ✅ `TokenType.RawString` | ✅ `StringLiteral` | ✅ → `@"..."` | ✅ |
| Boolean (`True`, `False`) | ✅ | ✅ `BooleanLiteral` | ✅ → `true`/`false` | ✅ |
| None literal | ✅ `TokenType.None` | ✅ `NoneLiteral` | ✅ → `null` | ✅ |
| Ellipsis (`...`) | ✅ `TokenType.Ellipsis` | ✅ `EllipsisLiteral` | ✅ → `NotImplementedException` | ✅ |

### Built-in Types

| Type | Parser | CodeGen | Status |
|------|--------|---------|--------|
| `int`, `long`, `short`, `byte` | ✅ | ✅ | ✅ |
| `uint`, `ulong`, `ushort`, `sbyte` | ✅ | ✅ | ✅ |
| `float`, `double`, `decimal` | ✅ | ✅ | ✅ |
| `bool` | ✅ | ✅ | ✅ |
| `str` | ✅ | ✅ → `string` | ✅ |
| `char` | ✅ | ✅ | ✅ |
| `object` | ✅ | ✅ | ✅ |

### Type Hierarchy and Object Model

| Feature | Semantic | CodeGen | Tests | Status |
|---------|----------|---------|-------|--------|
| `object` as universal base type | ✅ | ✅ → `System.Object` | ✅ | ✅ |
| Primitives assignable to `object` (boxing) | ✅ | ✅ | ✅ | ✅ |
| Structs assignable to `object` (boxing) | ✅ | ✅ | ✅ | ✅ |
| `None` assignable to `object?` only | ✅ | ✅ | ⚠️ | ✅ |

### Dunder Invocation Rules

| Feature | Semantic | CodeGen | Tests | Status |
|---------|----------|---------|-------|--------|
| Explicit dunder calls are compile error | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| Dunder inheritance | ✅ | ✅ | ⚠️ | ✅ |
| `@override` on dunder methods | ✅ | ✅ | ⚠️ | ✅ |
| `super().__dunder__()` calls in dunder body | ✅ | ✅ | ⚠️ | ✅ |
| Cross-dunder `self.__dunder__()` calls | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| Block dunder capture (`func = self.__eq__`) | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |

**Note**: The language spec defines strict rules for dunder invocation:
- User code cannot call dunders directly (e.g., `x.__eq__(y)` is an error)
- Dunders should only be invoked via operators (`==`) or built-in functions (`repr(x)`)
- Exception: Within a dunder body, `self.__dunder__()` and `super().__dunder__()` are allowed for synthesis

### Operators

| Category | Operators | Lexer | CodeGen | Status |
|----------|-----------|-------|---------|--------|
| Arithmetic | `+`, `-`, `*`, `/`, `//`, `%`, `**` | ✅ | ✅ (`**` → `Math.Pow`) | ✅ |
| Comparison | `==`, `!=`, `<`, `>`, `<=`, `>=` | ✅ | ✅ | ✅ |
| Logical | `and`, `or`, `not` | ✅ | ✅ → `&&`, `\|\|`, `!` | ✅ |
| Bitwise | `&`, `\|`, `^`, `~`, `<<`, `>>` | ✅ | ✅ | ✅ |
| Assignment | `=`, `+=`, `-=`, etc. | ✅ | ✅ | ✅ |
| Identity | `is`, `is not` | ✅ | ✅ | ✅ |
| Membership | `in`, `not in` | ✅ | ✅ → `.Contains()` | ✅ |

### Statements

| Statement | Parser | CodeGen | Tests | Status |
|-----------|--------|---------|-------|--------|
| Expression statement | ✅ `ExpressionStatement` | ✅ | ✅ | ✅ |
| Assignment | ✅ `Assignment` | ✅ | ✅ | ✅ |
| Variable declaration (`x: int = 5`) | ✅ `VariableDeclaration` | ✅ | ✅ | ✅ |
| Constant declaration (`const`) | ✅ | ✅ → `const` | ✅ | ✅ |
| `pass` | ✅ `PassStatement` | ✅ | ✅ | ✅ |
| `break`, `continue` | ✅ | ✅ | ✅ | ✅ |
| `return` | ✅ `ReturnStatement` | ✅ | ✅ | ✅ |
| `assert` | ✅ `AssertStatement` | ✅ → `Debug.Assert` | ✅ | ✅ |

### Control Flow

| Statement | Parser | CodeGen | Tests | Status |
|-----------|--------|---------|-------|--------|
| `if`/`elif`/`else` | ✅ `IfStatement` | ✅ | ✅ | ✅ |
| `while` | ✅ `WhileStatement` | ✅ | ✅ | ✅ |
| `for ... in` | ✅ `ForStatement` | ✅ → `foreach` | ✅ | ✅ |

### Exception Handling

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| `try`/`except`/`finally` | ✅ `TryStatement` | ✅ → `try`/`catch`/`finally` | ✅ | ✅ |
| `except Type as e:` | ✅ `ExceptHandler` | ✅ | ✅ | ✅ |
| `raise` | ✅ `RaiseStatement` | ✅ → `throw` | ✅ | ✅ |
| `raise ... from ...` | ✅ | ✅ → inner exception | ⚠️ | ✅ |
| `else` clause in try | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |

### Functions

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Function definition | ✅ `FunctionDef` | ✅ | ✅ | ✅ |
| Parameters with types | ✅ `Parameter` | ✅ | ✅ | ✅ |
| Return type annotation | ✅ | ✅ | ✅ | ✅ |
| Default parameters | ✅ | ✅ | ✅ | ✅ |
| Docstrings | ✅ | ✅ → XML doc comments | ✅ | ✅ |

### Classes

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Class definition | ✅ `ClassDef` | ✅ | ✅ | ✅ |
| Field declarations | ✅ `VariableDeclaration` | ✅ | ✅ | ✅ |
| Constructor (`__init__`) | ✅ | ✅ → constructor | ✅ | ✅ |
| Instance methods | ✅ | ✅ | ✅ | ✅ |
| `self` parameter | ✅ | ✅ (filtered out) | ✅ | ✅ |

### Imports

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| `import module` | ✅ `ImportStatement` | ✅ → `using` | ✅ | ✅ |
| `import module as alias` | ✅ | ✅ → `using alias = ` | ✅ | ✅ |
| `from module import name` | ✅ `FromImportStatement` | ✅ → `using static` | ✅ | ✅ |
| `from module import *` | ✅ | ✅ | ⚠️ | ✅ |
| Module name transformation | - | ✅ snake_case → PascalCase | ✅ | ✅ |

---

## v0.2 — Nullability & Collections ✅ COMPLETE

### Nullable Types

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Nullable annotation (`T?`) | ✅ `NullableType` | ✅ → `T?` | ✅ | ✅ |
| Null-conditional (`?.`) | ✅ `MemberAccess.IsNullConditional` | ✅ → `?.` | ✅ | ✅ |
| Null-coalescing (`??`) | ✅ `BinaryOp.NullCoalesce` | ✅ → `??` | ✅ | ✅ |

### Type Narrowing

| Feature | Semantic | CodeGen | Tests | Status |
|---------|----------|---------|-------|--------|
| `is None` / `is not None` | ✅ `TypeChecker._narrowedTypes` | ✅ | ✅ | ✅ |
| `isinstance(x, Type)` | ✅ | ✅ | ✅ | ✅ |

### Collection Literals

| Collection | Parser | CodeGen | Tests | Status |
|------------|--------|---------|-------|--------|
| List `[1, 2, 3]` | ✅ `ListLiteral` | ✅ → `new List<T> { }` | ✅ | ✅ |
| Empty list `[]` | ✅ | ✅ | ✅ | ✅ |
| Dict `{"a": 1}` | ✅ `DictLiteral` | ✅ → `new Dictionary<K,V> { }` | ✅ | ✅ |
| Empty dict `{}` | ✅ | ✅ | ✅ | ✅ |
| Set `{1, 2, 3}` | ✅ `SetLiteral` | ✅ → `new HashSet<T> { }` | ✅ | ✅ |
| Empty set `{/}` | ✅ | ✅ → `new HashSet<T>()` | ⚠️ | ✅ |
| Tuple `(1, 2)` | ✅ `TupleLiteral` | ✅ → ValueTuple | ✅ | ✅ |
| Single-element tuple `(1,)` | ✅ | ✅ | ✅ | ✅ |

### Tuple Operations

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Tuple unpacking `x, y = point` | ✅ | ✅ → deconstruction | ✅ | ✅ |
| Star unpacking `first, *rest = items` | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |

### Slicing

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Basic slice `[start:stop]` | ✅ `SliceAccess` | ✅ → `Slice()` method | ✅ | ✅ |
| Slice with step `[::step]` | ✅ | ✅ | ⚠️ | ✅ |
| Negative indices | ✅ | ✅ | ✅ | ✅ |

---

## v0.3 — Structs, Interfaces, OOP ⚠️ ~95% COMPLETE

### Structs

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Struct definition | ✅ `StructDef` | ✅ → `struct` | ✅ | ✅ |
| Struct fields | ✅ | ✅ | ✅ | ✅ |
| Struct methods | ✅ | ✅ | ✅ | ✅ |
| Struct constructor | ✅ | ✅ | ✅ | ✅ |

### Interfaces

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Interface definition | ✅ `InterfaceDef` | ✅ → `interface` | ✅ | ✅ |
| Interface methods | ✅ | ✅ | ✅ | ✅ |
| Interface inheritance | ✅ `BaseInterfaces` | ✅ | ✅ | ✅ |

### Inheritance

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Single class inheritance | ✅ `ClassDef.BaseClasses` | ✅ → `: BaseClass` | ✅ | ✅ |
| Multiple interface implementation | ✅ | ✅ → `: IFoo, IBar` | ✅ | ✅ |
| `super().__init__()` | ✅ | ✅ → `: base()` | ✅ | ✅ |

### Decorators (Access Modifiers)

| Decorator | Parser | CodeGen | Tests | Status |
|-----------|--------|---------|-------|--------|
| (default) = `public` | ✅ | ✅ | ✅ | ✅ |
| `@private` | ✅ `Decorator` | ✅ → `private` | ✅ | ✅ |
| `@protected` | ✅ | ✅ → `protected` | ✅ | ✅ |
| `@internal` | ✅ | ✅ → `internal` | ✅ | ✅ |
| Naming convention (`_`, `__`) | ✅ | ✅ | ✅ | ✅ |

### Decorators (Method/Class Modifiers)

| Decorator | Parser | CodeGen | Tests | Status |
|-----------|--------|---------|-------|--------|
| `@static` | ✅ | ✅ → `static` | ✅ | ✅ |
| `@override` | ✅ | ✅ → `override` | ⚠️ | ✅ |
| `@virtual` | ✅ | ✅ → `virtual` | ⚠️ | ✅ |
| `@abstract` | ✅ | ✅ → `abstract` | ⚠️ | ✅ |
| `@final` (method) | ✅ | ⚠️ Uses `@sealed`, not `@final` | ⚠️ | ⚠️ PARTIAL |
| `@final` (class) | ✅ | ⚠️ Uses `@sealed`, not `@final` | ⚠️ | ⚠️ PARTIAL |

**Note**: Decorators `@virtual`, `@override`, `@abstract`, `@sealed` are implemented in `RoslynEmitter.cs` (lines 499-503 for methods, 765-768 for classes). The language spec uses `@final` but implementation uses `@sealed` — needs alignment.

### Function Overloading

| Feature | Status | Notes |
|---------|--------|-------|
| User-defined function overloading | ❌ NOT IMPLEMENTED | `NameResolver.cs:262` explicitly rejects duplicate function names |
| Builtin function overloading | ✅ | Via `BuiltinRegistry.GetFunctionOverloads()` |
| Operator method overloading | ✅ | Via `TypeSymbol.OperatorMethods` dictionary |
| Constructor overloading | ✅ | Via multiple `__init__` methods |

---

## v0.4 — Generics ⚠️ ~85% COMPLETE

### Generic Classes/Structs/Interfaces

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Generic class `Box[T]` | ✅ `ClassDef.TypeParameters` | ✅ → `Box<T>` | ✅ | ✅ |
| Generic struct `Pair[T1, T2]` | ✅ | ✅ | ✅ | ✅ |
| Generic interface `IContainer[T]` | ✅ | ✅ | ✅ | ✅ |
| Generic instantiation `Box[int]()` | ✅ `GenericType` | ✅ | ✅ | ✅ |

### Generic Functions

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Generic function `def identity[T](x: T) -> T` | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |

### Type Constraints

| Constraint | Parser | CodeGen | Status |
|------------|--------|---------|--------|
| `T: Interface` | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| `T: class` | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| `T: struct` | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| `T: new()` | ❌ | ❌ | ❌ NOT IMPLEMENTED |

### Lambda Expressions

| Feature | Lexer | Parser | CodeGen | Tests | Status |
|---------|-------|--------|---------|-------|--------|
| Lambda `lambda x: x + 1` | ✅ `TokenType.Lambda` | ✅ `LambdaExpression` | ✅ → `x => x + 1` | ✅ | ✅ |
| Lambda with multiple params | ✅ | ✅ | ✅ | ✅ | ✅ |

---

## v0.5 — Enums & Operators ✅ COMPLETE

### Enumerations

| Feature | Lexer | Parser | CodeGen | Tests | Status |
|---------|-------|--------|---------|-------|--------|
| Enum definition | ✅ `TokenType.Enum` | ✅ `EnumDef` | ✅ → C# `enum` | ✅ | ✅ |
| Integer enum values | ✅ | ✅ `EnumMember` | ✅ | ✅ | ✅ |
| String enum values | ✅ | ✅ | ⚠️ → static class | ⚠️ | ⚠️ PARTIAL |
| `.name` property | - | - | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| `.value` property | - | - | ❌ | ❌ | ❌ NOT IMPLEMENTED |

### Operator Overloading (Dunder Methods)

| Method | Semantic | CodeGen | Status |
|--------|----------|---------|--------|
| `__add__` → `+` | ✅ `OperatorValidator` | ✅ `operator +` | ✅ |
| `__sub__` → `-` | ✅ | ✅ | ✅ |
| `__mul__` → `*` | ✅ | ✅ | ✅ |
| `__truediv__` → `/` | ✅ | ✅ | ✅ |
| `__floordiv__` → `//` | ✅ | ⚠️ method call | ⚠️ |
| `__mod__` → `%` | ✅ | ✅ | ✅ |
| `__pow__` → `**` | ✅ | ⚠️ method call | ⚠️ |
| `__neg__` → unary `-` | ✅ | ✅ | ✅ |
| `__eq__` → `==` | ✅ | ✅ + `Equals()` | ✅ |
| `__ne__` → `!=` | ✅ | ✅ | ✅ |
| `__lt__`, `__le__`, `__gt__`, `__ge__` | ✅ | ✅ | ✅ |
| `__str__` → `ToString()` | ✅ | ✅ | ✅ |
| `__repr__` → debug repr | ✅ | ✅ | ✅ |
| `__hash__` → `GetHashCode()` | ✅ | ✅ | ✅ |
| `__len__` → `Count` | ✅ | ✅ | ✅ |
| `__contains__` → `Contains()` | ✅ | ✅ | ✅ |
| `__iter__` → `GetEnumerator()` | ✅ | ✅ | ✅ |
| `__getitem__` → indexer | ✅ | ✅ | ✅ |
| `__setitem__` → indexer | ✅ | ✅ | ✅ |

---

## v0.6 — Extended Syntax ⚠️ ~90% COMPLETE

### F-Strings

| Feature | Lexer | Parser | CodeGen | Tests | Status |
|---------|-------|--------|---------|-------|--------|
| Basic f-string `f"Hello {name}"` | ✅ `FStringStart/Text/ExprStart/End` | ✅ `FStringLiteral` | ✅ → `$"..."` | ✅ | ✅ |
| Expressions in f-string | ✅ | ✅ `FStringPart.Expression` | ✅ | ✅ | ✅ |
| Format specifiers `{x:.2f}` | ✅ `FStringFormatSpec` | ✅ `FStringPart.FormatSpec` | ✅ | ✅ | ✅ |
| Multi-line f-string | ✅ | ✅ | ✅ | ⚠️ | ✅ |

### Extended Numeric Literals

| Literal | Lexer | Parser | CodeGen | Status |
|---------|-------|--------|---------|--------|
| Binary `0b1010` | ✅ | ✅ | ✅ | ✅ |
| Hexadecimal `0xFF` | ✅ | ✅ | ✅ | ✅ |
| Octal `0o755` | ✅ | ✅ → decimal | ✅ | ✅ |
| Scientific `6.022e23` | ✅ | ✅ | ✅ | ✅ |
| Underscores `1_000_000` | ✅ | ✅ | ✅ | ✅ |

### Comparison Chaining

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| `a < b < c` | ✅ `ComparisonChain` | ✅ → `a < b && b < c` | ✅ | ✅ |
| Multiple comparisons | ✅ | ✅ | ✅ | ✅ |

### Loop Else Clause

| Feature | Parser | CodeGen | Status |
|---------|--------|---------|--------|
| `for ... else:` | ❌ No AST field | ❌ | ❌ NOT IMPLEMENTED |
| `while ... else:` | ❌ | ❌ | ❌ NOT IMPLEMENTED |

---

## v0.7 — Pattern Matching ❌ NOT IMPLEMENTED

| Feature | Lexer | Parser | CodeGen | Status |
|---------|-------|--------|---------|--------|
| `match` keyword | ❌ | ❌ | ❌ | ❌ |
| `case` keyword | ❌ | ❌ | ❌ | ❌ |
| Literal patterns | ❌ | ❌ | ❌ | ❌ |
| Type patterns | ❌ | ❌ | ❌ | ❌ |
| Wildcard `_` | ❌ | ❌ | ❌ | ❌ |
| Guard `if` | ❌ | ❌ | ❌ | ❌ |
| OR patterns `\|` | ❌ | ❌ | ❌ | ❌ |
| Tuple patterns | ❌ | ❌ | ❌ | ❌ |
| Property patterns | ❌ | ❌ | ❌ | ❌ |

---

## v0.8 — Type Aliases & ADTs ❌ NOT IMPLEMENTED

| Feature | Lexer | Parser | CodeGen | Status |
|---------|-------|--------|---------|--------|
| `type` keyword | ❌ | ❌ | ❌ | ❌ |
| Type aliases `type UserId = int` | ❌ | ❌ | ❌ | ❌ |
| Generic type aliases | ❌ | ❌ | ❌ | ❌ |
| Tagged unions / ADTs | ❌ | ❌ | ❌ | ❌ |
| Variable shadowing with `auto` | ⚠️ Token exists | ❌ | ❌ | ❌ PARTIAL |

---

## v0.9 — Comprehensions & Properties ⚠️ ~60% COMPLETE

### Comprehensions

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| List comprehension `[x for x in items]` | ✅ `ListComprehension` | ✅ → LINQ `.Select().ToList()` | ✅ | ✅ |
| List comprehension with filter `[x for x in items if cond]` | ✅ | ✅ → LINQ `.Where().Select()` | ✅ | ✅ |
| Dict comprehension `{k: v for k, v in items}` | ✅ `DictComprehension` | ✅ → `.ToDictionary()` | ✅ | ✅ |
| Set comprehension `{x for x in items}` | ✅ `SetComprehension` | ✅ → `.ToHashSet()` | ✅ | ✅ |
| Nested comprehensions (multiple `for`) | ✅ Parser supports | ❌ `NotImplementedException` | ❌ | ❌ NOT IMPLEMENTED |
| Tuple unpacking in comprehensions | ✅ Parser supports | ❌ `NotImplementedException` | ❌ | ❌ NOT IMPLEMENTED |

### Walrus Operator

| Feature | Lexer | Parser | CodeGen | Status |
|---------|-------|--------|---------|--------|
| `:=` assignment expression | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |

### Properties

| Feature | Parser | CodeGen | Status |
|---------|--------|---------|--------|
| `property` keyword | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| Auto properties | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| Explicit get/set | ❌ | ❌ | ❌ NOT IMPLEMENTED |

---

## v1.0 — Resources & Async ❌ NOT IMPLEMENTED

| Feature | Lexer | Parser | CodeGen | Status |
|---------|-------|--------|---------|--------|
| `with` statement | ✅ Token exists | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| `defer` statement | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| `event` declaration | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| `async`/`await` | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |

---

## Standard Library (`Sharpy.Core`) — VERIFIED

### Built-in Functions

| Function | File | Implementation | Status |
|----------|------|----------------|--------|
| `print(x)` | `Builtins/Exports.cs` | ✅ `Print()` with `sep`, `end`, `file`, `flush` options | ✅ |
| `len(x)` | `Builtins/Exports.cs` | ✅ `Len()` — calls `__len__` if defined, else `.Count`/`.Length` | ✅ |
| `str(x)` | `Builtins/Exports.cs` | ✅ `Str()` — calls `__str__` if defined, else `.ToString()` | ✅ |
| `repr(x)` | - | ❌ Needs `Repr()` — should call `__repr__`, fallback to `__str__`/`.ToString()` | ❌ NOT IMPLEMENTED |
| `range(n)` | `Range.cs` | ✅ `RangeIterator` with `start`, `stop`, `step` | ✅ |
| `enumerate(iter)` | `Enumerate.cs` | ✅ `EnumerateIterator<T>` with `start` parameter | ✅ |
| `zip(a, b)` | `Zip.cs` | ✅ `ZipIterator<T1, T2>` and 3-arity version | ✅ |
| `map(func, iter)` | `Map.cs` | ✅ `MapIterator<TIn, TOut>` | ✅ |
| `filter(pred, iter)` | `Filter.cs` | ✅ `FilterIterator<T>` | ✅ |
| `sorted(iter)` | `Sorted.cs` | ✅ With `key` and `reverse` parameters | ✅ |
| `reversed(iter)` | `Reversed.cs` | ✅ Via `IReversible<T>` interface | ✅ |
| `min(iter)` | `Min.cs` | ✅ With `key` parameter | ✅ |
| `max(iter)` | `Max.cs` | ✅ With `key` parameter | ✅ |
| `sum(iter)` | `Sum.cs` | ✅ Via `IAddable<T>` interface | ✅ |
| `all(iter)` | `All.cs` | ✅ Via LINQ `.All()` | ✅ |
| `any(iter)` | `Any.cs` | ✅ Via LINQ `.Any()` | ✅ |
| `abs(x)` | `Operator/Abs.cs` | ✅ For all numeric types | ✅ |
| `pow(x, y)` | `Pow.cs` | ✅ Via `Math.Pow()` | ✅ |
| `round(x, n)` | `Round.cs` | ✅ With decimal places parameter | ✅ |
| `divmod(a, b)` | `DivMod.cs` | ✅ For `int`, `long`, `double`, `float` | ✅ |
| `isinstance(x, T)` | `Isinstance.cs` | ✅ Generic and runtime type checking | ✅ |
| `type(x)` | `Type.cs` | ✅ Returns runtime type | ✅ |
| `input(prompt)` | `Input.cs` | ✅ With optional prompt | ✅ |
| `hash(x)` | - | ❌ Needs `Hash()` — should call `__hash__` if defined, else `.GetHashCode()` | ❌ NOT IMPLEMENTED |
| `id(x)` | - | ❌ Needs `Id()` — uses `RuntimeHelpers.GetHashCode()` for object identity | ❌ NOT IMPLEMENTED |

### Pythonic Collections

| Collection | Implementation | Features | Status |
|------------|----------------|----------|--------|
| `list[T]` | `Partial.List/List.cs` | ✅ Full Python-like API (`append`, `pop`, `sort`, `copy`, slicing) | ✅ |
| `dict[K, V]` | `Dict.cs` | ✅ With `get`, `copy`, `clear`, `contains` | ✅ |
| `set[T]` | `Partial.Set/Set.cs` | ✅ Set operations, `add`, `remove`, `clear` | ✅ |
| Slicing | `Slice.cs`, `Index.cs` | ✅ Negative indices, step support | ✅ |

### Iterator Protocol

| Interface | File | Purpose | Status |
|-----------|------|---------|--------|
| `IIterable<T>` | `Collections/Interfaces/` | ✅ Python `__iter__` equivalent | ✅ |
| `Iterator<T>` | `Partial.Iterator/` | ✅ With `__Next__()` and `StopIteration` | ✅ |
| `IReversible<T>` | - | ✅ For `reversed()` support | ✅ |
| `ISized` | - | ✅ For `len()` support | ✅ |

---

## Verified Implementation Details (December 3, 2025 Audit)

### TokenType Keywords — VERIFIED

The following keywords are present in `src/Sharpy.Compiler/Lexer/Token.cs`:

| Keyword | TokenType | Status |
|---------|-----------|--------|
| `def`, `class`, `struct`, `interface`, `enum` | ✅ Present | ✅ |
| `if`, `else`, `elif`, `while`, `for`, `in` | ✅ Present | ✅ |
| `return`, `break`, `continue`, `pass` | ✅ Present | ✅ |
| `try`, `except`, `finally`, `raise`, `assert` | ✅ Present | ✅ |
| `import`, `from`, `as` | ✅ Present | ✅ |
| `and`, `or`, `not`, `is` | ✅ Present | ✅ |
| `const`, `lambda`, `auto` | ✅ Present | ✅ |
| `True`, `False`, `None` | ✅ Present | ✅ |
| `with` | ✅ Present | ⚠️ Token only, not implemented |
| `match`, `case` | ❌ NOT Present | ❌ v0.7 - Needs implementation |
| `type` | ❌ NOT Present | ❌ v0.8 - Needs implementation |
| `defer`, `event` | ❌ NOT Present | ❌ v1.0 - Needs implementation |
| `async`, `await` | ❌ NOT Present | ❌ v1.0 - Needs implementation |
| `property` | ❌ NOT Present | ❌ v0.9 - Needs implementation |

### AST Nodes — VERIFIED

**ForStatement** (`Parser/Ast/Statement.cs` line 136):
```csharp
public record ForStatement : Statement
{
    public Expression Target { get; init; } = null!;
    public Expression Iterator { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();
    // NOTE: NO ElseBody property - loop else clause NOT supported
}
```

**WhileStatement** (`Parser/Ast/Statement.cs` line 127):
```csharp
public record WhileStatement : Statement
{
    public Expression Test { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();
    // NOTE: NO ElseBody property - loop else clause NOT supported
}
```

**TryStatement** (`Parser/Ast/Statement.cs` line 146):
```csharp
public record TryStatement : Statement
{
    public List<Statement> Body { get; init; } = new();
    public List<ExceptHandler> Handlers { get; init; } = new();
    public List<Statement> FinallyBody { get; init; } = new();
    // NOTE: NO ElseBody property - try-else clause NOT supported
}
```

**FunctionDef** (`Parser/Ast/Statement.cs` line 173):
```csharp
public record FunctionDef : Statement
{
    public string Name { get; init; } = "";
    public List<Parameter> Parameters { get; init; } = new();
    public TypeAnnotation? ReturnType { get; init; }
    public List<Statement> Body { get; init; } = new();
    public List<Decorator> Decorators { get; init; } = new();
    public string? DocString { get; init; }
    // NOTE: NO TypeParameters property - generic functions NOT supported
}
```

**ClassDef** (`Parser/Ast/Statement.cs` line 186) — HAS TypeParameters:
```csharp
public record ClassDef : Statement
{
    public string Name { get; init; } = "";
    public List<string> TypeParameters { get; init; } = new(); // ✅ Generics supported
    public List<TypeAnnotation> BaseClasses { get; init; } = new();
    // ...
}
```

### Codegen NotImplementedException Locations — VERIFIED

| Location | Feature | Code Path |
|----------|---------|-----------|
| `RoslynEmitter.cs:1927` | Tuple unpacking in list comprehensions | `GenerateListComprehension` |
| `RoslynEmitter.cs:1956` | Nested comprehensions (multiple for) | `GenerateListComprehension` |
| `RoslynEmitter.cs:1997` | Tuple unpacking in dict comprehensions | `GenerateDictComprehension` |
| `RoslynEmitter.cs:2024` | Nested dict comprehensions | `GenerateDictComprehension` |
| `RoslynEmitter.cs:2066` | Tuple unpacking in set comprehensions | `GenerateSetComprehension` |
| `RoslynEmitter.cs:2093` | Nested set comprehensions | `GenerateSetComprehension` |

### Star Unpacking (`*rest`) — NOT IMPLEMENTED

Searched for `StarredExpr`, `StarExpression`, `Starred` in `src/Sharpy.Compiler/` — **No matches found**.

Star unpacking like `first, *rest = items` is NOT supported in lexer, parser, or codegen.

### Walrus Operator (`:=`) — NOT IMPLEMENTED

Searched for `Walrus`, `ColonEquals`, `:=` in `src/Sharpy.Compiler/` — **No matches found** (only documentation references).

Assignment expressions are NOT supported in lexer, parser, or codegen.

### Enum String Values — PARTIAL

`RoslynEmitter.cs:GenerateEnumDeclaration` (line 681) generates standard C# enums. String enum values would use `GenerateExpression` which supports `StringLiteral`, but C# enums don't support string values directly. **Needs static class pattern for string enums** per language spec.

---

## TODO: Implementation Tasks

### PRIORITY 1: Missing Core Features (v0.1-v0.6)

#### 1.1 Try-Else Clause (v0.1)
- [ ] Add `ElseBody` property to `TryStatement` AST node
- [ ] Update `Parser.ParseTryStatement()` to parse `else:` block after handlers
- [ ] Update `RoslynEmitter.GenerateTry()` to emit boolean flag pattern
- [ ] Add integration tests for try-else behavior

#### 1.2 Loop Else Clause (v0.6)
- [ ] Add `ElseBody` property to `ForStatement` AST node
- [ ] Add `ElseBody` property to `WhileStatement` AST node
- [ ] Update `Parser.ParseForStatement()` to parse `else:` block
- [ ] Update `Parser.ParseWhileStatement()` to parse `else:` block
- [ ] Update `RoslynEmitter.GenerateFor()` to emit boolean flag pattern
- [ ] Update `RoslynEmitter.GenerateWhile()` to emit boolean flag pattern
- [ ] Add integration tests for loop-else behavior

#### 1.3 Generic Functions (v0.4)
- [ ] Add `TypeParameters` property to `FunctionDef` AST node
- [ ] Update `Parser.ParseFunctionDef()` to parse `def foo[T](x: T)` syntax
- [ ] Update `RoslynEmitter.GenerateMethod()` to emit type parameters
- [ ] Add semantic analysis for generic function type constraints
- [ ] Add integration tests for generic functions

#### 1.4 Type Constraints (v0.4)
- [ ] Design AST representation for type constraints (`T: IInterface`, `T: class`, etc.)
- [ ] Update parser to handle constraint syntax on generic types
- [ ] Update `RoslynEmitter` to emit `where T : IInterface` clauses
- [ ] Add semantic validation for constraint satisfaction

#### 1.5 Star Unpacking (v0.2)
- [ ] Add `StarredExpression` AST node for `*rest` syntax
- [ ] Add `TokenType.Star` handling for unpacking context in Lexer
- [ ] Update `Parser` to parse `first, *rest = items` patterns
- [ ] Update `RoslynEmitter` to generate appropriate C# (LINQ Take/Skip pattern)
- [ ] Add integration tests

#### 1.6 User-Defined Function Overloading (v0.3)
- [ ] Update `NameResolver.ResolveFunctionDeclaration()` to allow multiple definitions with same name
- [ ] Create overload resolution mechanism similar to `BuiltinRegistry.GetFunctionOverloads()`
- [ ] Update `TypeChecker` to resolve user function overloads based on argument types
- [ ] Update `RoslynEmitter` to emit multiple C# methods (already supports via normal method generation)
- [ ] Add integration tests for function overloading

#### 1.7 Dunder Invocation Rules (v0.1)
- [ ] Add semantic analysis to detect and reject explicit dunder calls (e.g., `x.__eq__(y)`)
- [ ] Allow `self.__dunder__()` calls within dunder method bodies only
- [ ] Allow `super().__dunder__()` calls within dunder method bodies only
- [ ] Reject dunder method capture (e.g., `func = self.__eq__`)
- [ ] Add error messages guiding users to use operators/built-in functions instead
- [ ] Add integration tests for dunder invocation validation

#### 1.8 Built-in Functions with Dunder Dispatch (v0.1)
- [ ] **Implement `repr(x)`**: Add `Repr()` function that calls `__repr__` if defined, else `__str__`, else `.ToString()`
- [ ] **Update `hash(x)`**: Add `Hash()` function that calls `__hash__` if defined, else `.GetHashCode()`
- [ ] **Verify `str(x)`**: Ensure `Str()` calls `__str__` if defined for Sharpy types
- [ ] **Verify `len(x)`**: Ensure `Len()` calls `__len__` if defined for Sharpy types
- [ ] Add integration tests for dunder dispatch in built-in functions

### PRIORITY 2: v0.9 Features

#### 2.1 Nested Comprehensions
- [ ] Update `RoslynEmitter.GenerateListComprehension()` to handle multiple `ForClause`
- [ ] Generate SelectMany LINQ pattern for nested iterations
- [ ] Add integration tests

#### 2.2 Tuple Unpacking in Comprehensions
- [ ] Update comprehension codegen to support tuple targets in for clause
- [ ] Add integration tests

#### 2.3 Walrus Operator (`:=`)
- [ ] Add `TokenType.ColonEquals` for `:=` token
- [ ] Add `AssignmentExpression` AST node
- [ ] Update `Parser.ParseExpression()` to handle `:=`
- [ ] Update `RoslynEmitter` to generate C# inline assignment
- [ ] Add semantic analysis for walrus operator scope rules
- [ ] Add integration tests

#### 2.4 Properties (v0.9)
- [ ] Add `TokenType.Property` keyword
- [ ] Add `PropertyDef` AST node with `get`/`set` accessors
- [ ] Update Parser to handle `property x: int` syntax
- [ ] Update `RoslynEmitter` to generate C# property syntax
- [ ] Add integration tests

### PRIORITY 3: v0.7-v1.0 Features (Future Work)

#### 3.1 Pattern Matching (v0.7)
- [ ] Add `TokenType.Match`, `TokenType.Case` keywords
- [ ] Add `MatchStatement`, `CaseClause` AST nodes
- [ ] Add pattern AST nodes: `LiteralPattern`, `TypePattern`, `WildcardPattern`, `GuardPattern`
- [ ] Update Parser for match/case syntax
- [ ] Update `RoslynEmitter` to generate C# switch expressions
- [ ] Add integration tests

#### 3.2 Type Aliases (v0.8)
- [ ] Add `TokenType.Type` keyword
- [ ] Add `TypeAliasStatement` AST node
- [ ] Update Parser for `type Name = ExistingType` syntax
- [ ] Update semantic analysis to resolve type aliases
- [ ] Update `RoslynEmitter` to generate `using Name = Type;`
- [ ] Add integration tests

#### 3.3 Context Managers (v1.0)
- [ ] Update Parser to handle `with expr as name:` syntax (token exists)
- [ ] Add `WithStatement` AST node
- [ ] Update `RoslynEmitter` to generate C# `using` statement
- [ ] Add integration tests

#### 3.4 Async/Await (v1.0)
- [ ] Add `TokenType.Async`, `TokenType.Await` keywords
- [ ] Add `async` decorator support
- [ ] Add `AwaitExpression` AST node
- [ ] Update `RoslynEmitter` to generate `async`/`await` C#
- [ ] Add semantic analysis for async context
- [ ] Add integration tests

### PRIORITY 4: Standard Library Gaps

- [ ] **Implement `repr(x)`**: Add global `Repr()` function that calls `__repr__` if defined, else `__str__`, else `.ToString()`
- [ ] **Implement `hash(x)`**: Add global `Hash()` function that calls `__hash__` if defined, else `.GetHashCode()`
- [ ] **Implement `id(x)`**: Add global `Id()` function using `RuntimeHelpers.GetHashCode()` for object identity
- [ ] **Enum `.name` property**: Add extension method or codegen support for `Color.RED.name`
- [ ] **Enum `.value` property**: Add extension method or codegen support for `Color.RED.value`
- [ ] **String enum static class**: Update codegen to emit static class pattern for string-valued enums

### PRIORITY 5: Decorator Alignment

- [ ] Decide: Keep `@sealed` or rename to `@final` per language spec
- [ ] Update documentation to match implementation OR update implementation to match spec
- [ ] Add `@final` as alias for `@sealed` if keeping both

---

## AUDIT #4 FINDINGS (December 3, 2025)

### Test Coverage Mapping — PARTIALLY COMPLETED

**Integration Test Files:**
| Test File | Features Covered | Status |
|-----------|------------------|--------|
| `BasicProgramTests.cs` | Hello world, functions, fibonacci, arithmetic | ✅ Well covered |
| `ControlFlowTests.cs` | if/elif/else, while, for, break, continue, nested loops | ✅ Well covered |
| `FunctionTests.cs` | Functions, default params, recursive calls | ✅ Well covered |
| `VariableAssignmentNegativeTests.cs` | Variable assignment error cases | ✅ Exists |
| `CompilerIntegrationTests.cs` | Module loading, builtins, references | ✅ Exists |
| `ThirdPartyModuleTests.cs` | External module import | ✅ Exists |
| `ModuleDiscoveryWorkflowTests.cs` | Module discovery | ✅ Exists |

**Parser Test Files:**
| Test File | Features Covered | Status |
|-----------|------------------|--------|
| `ParserEdgeCaseTests.cs` | Comprehensions, try/except, decorators, imports | ✅ Comprehensive |
| `ParserTests.cs` | Basic parsing scenarios | ✅ Exists |
| `ParserNegativeTests.cs` | Error cases | ✅ Exists |
| `ParserPositionTests.cs` | Source location tracking | ✅ Exists |

**CodeGen Test Files:**
| Test File | Features Covered | Status |
|-----------|------------------|--------|
| `RoslynEmitterStatementTests.cs` | Statement codegen (raise, assert, etc.) | ✅ Exists |
| `RoslynEmitterExpressionTests.cs` | Expression codegen | ✅ Exists |
| `RoslynEmitterOperatorTests.cs` | Operator codegen | ✅ Exists |
| `RoslynEmitterDefinitionTests.cs` | Class/struct/function codegen | ✅ Exists |
| `TypeMapperTests.cs` | Type mapping | ✅ Exists |
| `NameManglerTests.cs` | Name transformation | ✅ Exists |

**Semantic Test Files:**
| Test File | Features Covered | Status |
|-----------|------------------|--------|
| `TypeCheckerTests.cs` | Type checking | ✅ Exists |
| `NameResolverTests.cs` | Name resolution | ✅ Exists |
| `TypeResolverTests.cs` | Type annotation resolution | ✅ Exists |
| `OperatorValidatorTests.cs` | Operator validation | ✅ Exists |
| `ProtocolValidatorTests.cs` | Protocol validation | ✅ Exists |
| `SemanticAnalyzerEdgeCaseTests.cs` | Edge cases | ✅ Exists |

### Confirmed Test Coverage Gaps

| Feature | Has Parser Test | Has Integration Test | Notes |
|---------|-----------------|---------------------|-------|
| Struct definition | ✅ `NameResolverTests.cs:111` | ❌ | Need integration test |
| Interface definition | ✅ | ❌ | Need integration test |
| Generic class instantiation | ✅ | ❌ | Need integration test |
| try/except/finally | ✅ `ParserEdgeCaseTests.cs:857` | ❌ | Need integration test |
| try-else | ❌ Skipped test | ❌ | NOT IMPLEMENTED |
| raise statement | ✅ `RoslynEmitterStatementTests.cs:94` | ❌ | Need integration test |
| Comparison chaining | ✅ | ❌ | Need integration test |
| Slicing with step | ✅ | ⚠️ | Need integration test |
| Empty set `{/}` | ✅ | ❌ | Need integration test |
| F-string format specifiers | ✅ `ParserEdgeCaseTests.cs:217` | ❌ | Need integration test |

### Skipped Parser Tests — CONFIRMED

| Test | Reason | Implementation Priority |
|------|--------|------------------------|
| `ParsesNestedListComprehension` (line 564) | Multiple for clauses not supported | v0.9 |
| `ParsesDictComprehension` (line 572) | Tuple unpacking not supported | v0.9 |
| `ParsesCallableType` (line 284) | Callable type syntax not supported | Future |
| `ParsesFunctionWithVarArgs` (line 303) | `*args` not supported | Future |
| `ParsesFunctionWithKwargs` (line 312) | `**kwargs` not supported | Future |
| `ParsesDecoratorWithArguments` (line 349) | Decorator arguments not supported | Future |
| `ParsesTryExceptElse` (line 917) | try-else not supported | v0.1 gap |

### Semantic Analysis Structure — DOCUMENTED

**Pass Order:**
1. `NameResolver.ResolveDeclarations()` - Registers all top-level symbols (classes, structs, interfaces, enums, functions, constants)
2. `NameResolver.ResolveInheritance()` - Resolves base classes and interface implementations
3. `TypeResolver.ResolveTypeAnnotation()` - Converts AST type annotations to `SemanticType` objects
4. `TypeChecker.CheckModule()` - Type checks all expressions and statements

**Key Data Structures:**
- `SymbolTable` - Scoped symbol storage with `Lookup()` and `Define()`
- `SemanticInfo` - Caches resolved types, expression types, symbol references
- `_narrowedTypes` dictionary in `TypeChecker` - Tracks type narrowing in conditionals (line 29)

**Validation Components:**
- `ControlFlowValidator` - Validates break/continue in loops, return in functions
- `AccessValidator` - Validates access modifiers on member access
- `OperatorValidator` - Validates binary/unary operators, membership (`in`), identity (`is`)
- `ProtocolValidator` - Validates dunder methods (`__iter__`, `__len__`, `__contains__`, etc.)

### User Function Overloading — CONFIRMED NOT IMPLEMENTED

`NameResolver.cs` lines 262-266:
```csharp
if (_symbolTable.Lookup(functionDef.Name, searchParents: false) != null)
{
    AddError($"Function '{functionDef.Name}' is already defined",
        functionDef.LineStart, functionDef.ColumnStart);
    return;
}
```

This explicitly rejects duplicate function names. To implement user function overloading:
1. Change `NameResolver` to allow multiple function definitions with same name
2. Create overload resolution mechanism (similar to `BuiltinRegistry.GetFunctionOverloads()`)
3. Update `TypeChecker` to resolve overloads based on argument types

---

## NEEDS AUDIT / NEXT ITERATION

The following sections still require verification and documentation in a future iteration:

### 1. Semantic Analysis Deep Dive — PARTIALLY COMPLETED
- [x] Document 3-pass analysis structure (NameResolver → TypeResolver → TypeChecker)
- [x] Document key data structures (SymbolTable, SemanticInfo, _narrowedTypes)
- [x] Verify user function overloading is NOT implemented
- [ ] Document type inference implementation details (how unknown types resolve)
- [ ] Document operator overload resolution flow (`CachedOverloadDiscoveryService` → `OverloadIndexBuilder`)
- [ ] Trace protocol validation for all dunder methods through `ProtocolValidator`

### 2. Test Coverage Audit — PARTIALLY COMPLETED
- [x] Map integration tests to language features
- [x] Identify skipped parser tests
- [ ] Create comprehensive test matrix: every v0.1-v0.6 feature → test file mapping
- [ ] Identify features with ZERO test coverage
- [ ] Run coverage report and document percentages

### 3. Error Message Quality — NEEDS REVIEW
- [ ] Document all `NotImplementedException` messages with user-facing text
- [ ] Test common error scenarios and document error message quality
- [ ] Identify errors that need better suggestions

### 4. .NET Interop — NEEDS TESTING
- [ ] Verify calling .NET methods from Sharpy
- [ ] Verify using .NET types as base classes
- [ ] Test LINQ extension methods with Sharpy collections

---

## Summary for Task Generation

### ✅ Complete (No Action Required)
- **v0.1**: Core Language — all features except `try ... else:` clause
- **v0.2**: Nullability & Collections — all features except star unpacking (`*rest`)
- **v0.3**: Structs, Interfaces, OOP — decorators `@virtual`/`@override`/`@abstract`/`@sealed` work
- **v0.5**: Enums (integer) & Operator Overloading — core features work
- **v0.6**: F-strings, extended numeric literals, comparison chaining
- **Standard Library**: Core builtins (`print`, `len`, `range`, `enumerate`, `zip`, `map`, `filter`, `sorted`, `reversed`, `min`, `max`, `sum`, `all`, `any`, `abs`, `pow`, `round`, `divmod`, `isinstance`, `type`, `input`)

### ⚠️ Needs Completion (Prioritize for v1.0 Release)

| Version | Feature | Lexer | Parser | Semantic | CodeGen | Tests |
|---------|---------|-------|--------|----------|---------|-------|
| v0.1 | `try ... else:` clause | ✅ | ❌ | ❌ | ❌ | ❌ |
| v0.2 | Star unpacking `*rest` | ❌ | ❌ | ❌ | ❌ | ❌ |
| v0.3 | User function overloading | ✅ | ✅ | ❌ | ✅ | ❌ |
| v0.4 | Generic functions `def foo[T]` | ✅ | ❌ | ❌ | ❌ | ❌ |
| v0.4 | Type constraints `T: IFoo` | ❌ | ❌ | ❌ | ❌ | ❌ |
| v0.5 | Enum `.name`, `.value` props | - | - | ❌ | ❌ | ❌ |
| v0.5 | String enum → static class | - | ✅ | ⚠️ | ❌ | ❌ |
| v0.6 | Loop else `for...else:` | ✅ | ❌ | ❌ | ❌ | ❌ |
| v0.9 | Nested comprehensions | ✅ | ✅ | ✅ | ❌ | ❌ |
| v0.9 | Tuple unpacking in compr. | ✅ | ✅ | ⚠️ | ❌ | ❌ |
| v0.9 | Walrus operator `:=` | ❌ | ❌ | ❌ | ❌ | ❌ |
| v0.9 | Properties | ❌ | ❌ | ❌ | ❌ | ❌ |
| - | `hash(x)` builtin | - | - | - | - | ❌ |
| - | `id(x)` builtin | - | - | - | - | ❌ |

### ❌ Not Started (Future Work — Post v1.0)

| Version | Feature | Notes |
|---------|---------|-------|
| v0.7 | Pattern matching | `match`/`case` keywords not in lexer |
| v0.8 | Type aliases | `type` keyword not in lexer |
| v0.8 | Tagged unions/ADTs | Requires type alias foundation |
| v0.8 | Variable shadowing `auto` | Token exists, parser/codegen missing |
| v1.0 | Context managers `with` | Token exists, parser/codegen missing |
| v1.0 | `defer` statement | Token not in lexer |
| v1.0 | `event` declaration | Token not in lexer |
| v1.0 | `async`/`await` | Tokens not in lexer |

---

## Next Documentation Iteration

### COMPLETED IN AUDIT #4 (December 3, 2025)
1. ✅ Mapped all Integration test files to features covered
2. ✅ Mapped all Parser test files to features covered
3. ✅ Mapped all CodeGen test files to features covered
4. ✅ Mapped all Semantic test files to features covered
5. ✅ Identified 7 skipped parser tests and their reasons
6. ✅ Documented semantic analysis pass structure (NameResolver → TypeResolver → TypeChecker)
7. ✅ Documented key data structures (SymbolTable, SemanticInfo, _narrowedTypes)
8. ✅ Confirmed user function overloading is NOT implemented (NameResolver.cs:262-266)
9. ✅ Identified 10 features with test coverage gaps needing integration tests
10. ✅ Created list of new test files to create (StructTests, InterfaceTests, ExceptionTests, GenericTests)

### COMPLETED IN AUDIT #5 (December 3, 2025)
1. ✅ Cross-checked TokenType enum in `Token.cs` against language reference keywords
2. ✅ Verified FunctionDef does NOT have TypeParameters (generic functions NOT supported)
3. ✅ Verified ClassDef, StructDef, InterfaceDef DO have TypeParameters (generics supported)
4. ✅ Confirmed ForStatement, WhileStatement have NO ElseBody property
5. ✅ Confirmed TryStatement has NO ElseBody property
6. ✅ Verified no ColonEquals/`:=` token exists (walrus NOT in lexer)
7. ✅ Verified no StarredExpr AST node exists (star unpacking NOT supported)
8. ✅ Confirmed `hash(x)` and `id(x)` NOT implemented as standalone functions in Sharpy.Core
9. ✅ Verified PropertyDef AST node does NOT exist (properties NOT supported)
10. ✅ Documented full TokenType list with 115 token types

### COMPLETED IN AUDIT #3 (December 3, 2025)
1. ✅ Verified TokenType keywords present/missing in Lexer
2. ✅ Verified AST node properties for loop-else, try-else, generic functions
3. ✅ Verified `NotImplementedException` locations in RoslynEmitter
4. ✅ Confirmed star unpacking (`*rest`) not implemented anywhere
5. ✅ Confirmed walrus operator (`:=`) not implemented anywhere
6. ✅ Created prioritized implementation task list
7. ✅ Verified type narrowing (`is None`, `isinstance`) implemented in TypeChecker
8. ✅ Confirmed `hash(x)` and `id(x)` builtins NOT implemented as standalone functions
9. ✅ Verified overload resolution infrastructure exists (`OverloadIndexCache`, `OverloadIndexBuilder`)
10. ✅ Verified comprehension test coverage (parser tests exist, some skipped for nested/tuple)

### HIGH PRIORITY — NEXT ITERATION

#### 1. Complete Semantic Analysis Deep Dive (Estimated: 2-3 hours)
- [ ] Document type inference implementation (trace `InferType` method in TypeChecker)
- [ ] Document operator overload resolution flow:
  - `CachedOverloadDiscoveryService.cs` → `OverloadIndexBuilder.cs` → resolution
- [ ] Trace protocol validation for all dunder methods through `ProtocolValidator`
- [ ] Document how `SemanticInfo` caches and retrieves resolved information

#### 2. Run Coverage Report (Estimated: 1 hour)
- [ ] Run: `dotnet test --collect:"XPlat Code Coverage"`
- [ ] Generate report: `reportgenerator -reports:coverage.cobertura.xml -targetdir:coveragereport`
- [ ] Document overall coverage percentage
- [ ] Identify files with < 50% coverage

#### 3. Create Missing Integration Tests (Estimated: 3-4 hours)
Priority order:
1. [ ] `ExceptionTests.cs` — try/except/finally, raise statement
2. [ ] `StructTests.cs` — struct definition, fields, methods, constructor
3. [ ] `InterfaceTests.cs` — interface definition, implementation
4. [ ] `GenericTests.cs` — generic class instantiation
5. [ ] `SlicingTests.cs` — slicing with step, negative indices
6. [ ] `CollectionLiteralTests.cs` — empty set `{/}`, comparison chaining

#### 4. Error Message Quality Audit (Estimated: 1 hour)
- [ ] Document all `NotImplementedException` messages with user-facing text
- [ ] Test common error scenarios and document error message quality
- [ ] Identify errors that need better suggestions

### MEDIUM PRIORITY — FUTURE ITERATION

#### 4. Language Reference Section Verification
The following sections from `sharpy_language_reference_v1.md` have NOT been fully audited against implementation:
- [ ] **Expressions** (lines 800-900): Verify all expression types parse and codegen correctly
- [ ] **Operator Precedence** (lines 700-800): Verify precedence matches C# output
- [ ] **Default Parameter Evaluation** (lines 1200-1250): Verify mutable default behavior
- [ ] **.NET Interop** (lines 2500-2620): Test actual .NET type usage scenarios
- [ ] **Module Resolution** (lines 1100-1150): Verify snake_case → PascalCase transformation

#### 5. Standard Library Completeness
- [ ] Audit `Sharpy.Core` against Python builtins list in language reference
- [ ] Document which Python builtins are missing or have different behavior
- [ ] Create compatibility matrix: Python function → Sharpy.Core implementation

#### 6. Integration Test Gaps — Create Tests For:
- [ ] Struct definitions and usage (codegen exists, no integration test)
- [ ] Interface implementation (codegen exists, no integration test)
- [ ] Generic class instantiation
- [ ] try/except/finally (parser test exists, no integration test)
- [ ] raise statement (codegen test exists, no integration test)
- [ ] Comparison chaining
- [ ] F-string format specifiers
- [ ] Slicing with step
- [ ] Empty set literal `{/}`

### LOW PRIORITY — BACKLOG

#### 7. Performance and Edge Cases
- [ ] Document any known slow paths in compiler
- [ ] Test large file compilation performance
- [ ] Test deeply nested code structures

#### 8. Documentation Generation
- [ ] Generate API documentation from XML comments
- [ ] Create "implemented features" summary page

### FILES TO REVIEW IN NEXT ITERATION

**For Semantic Analysis Deep Dive:**
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs` — Type inference details (`InferType` method)
- `src/Sharpy.Compiler/Semantic/CachedOverloadDiscoveryService.cs` — Overload resolution
- `src/Sharpy.Compiler/Semantic/OverloadIndexBuilder.cs` — Overload index structure
- `src/Sharpy.Compiler/Semantic/ProtocolValidator.cs` — Dunder method validation flow

**For Test Coverage Audit:**
- Run: `dotnet test --collect:"XPlat Code Coverage"`
- Analyze: `reportgenerator -reports:coverage.cobertura.xml -targetdir:coveragereport`

**For Integration Tests to Create:**
- `src/Sharpy.Compiler.Tests/Integration/StructTests.cs` — NEW
- `src/Sharpy.Compiler.Tests/Integration/InterfaceTests.cs` — NEW
- `src/Sharpy.Compiler.Tests/Integration/ExceptionTests.cs` — NEW
- `src/Sharpy.Compiler.Tests/Integration/GenericTests.cs` — NEW

---

## Appendix: Verified Code Locations

### NotImplementedException Locations in RoslynEmitter.cs

| Line | Feature | Method |
|------|---------|--------|
| 1362 | Complex tuple unpacking | `GenerateAssignment` |
| 1365 | Unknown assignment target | `GenerateAssignment` |
| 1385 | Unknown augmented assignment op | `GenerateAugmentedAssignment` |
| 1571 | Complex for loop tuple unpacking | `GenerateFor` |
| 1574 | Unknown for loop target | `GenerateFor` |
| 1666 | Unknown expression type | `GenerateExpression` |
| 1699 | Complex function expressions | `GenerateFunctionCall` |
| 1812 | Unknown binary operator | `GenerateBinaryOp` |
| 1828 | Unknown unary operator | `GenerateUnaryOp` |
| 1927 | Tuple unpacking in list comprehensions | `GenerateListComprehension` |
| 1956 | Nested list comprehensions | `GenerateListComprehension` |
| 1997 | Tuple unpacking in dict comprehensions | `GenerateDictComprehension` |
| 2024 | Nested dict comprehensions | `GenerateDictComprehension` |
| 2066 | Tuple unpacking in set comprehensions | `GenerateSetComprehension` |
| 2093 | Nested set comprehensions | `GenerateSetComprehension` |
| 2204 | Unknown comparison operator in chains | `GenerateComparisonChain` |

### Parser Skipped Tests

| Test File | Test Name | Reason |
|-----------|-----------|--------|
| `ParserEdgeCaseTests.cs:564` | `ParsesNestedListComprehension` | Multiple for clauses not supported |
| `ParserEdgeCaseTests.cs:572` | `ParsesDictComprehension` | Tuple unpacking not supported |

### Missing TokenTypes for Future Versions

| Keyword | Required For | Current Status |
|---------|--------------|----------------|
| `match` | v0.7 Pattern Matching | ❌ Not in TokenType |
| `case` | v0.7 Pattern Matching | ❌ Not in TokenType |
| `type` | v0.8 Type Aliases | ❌ Not in TokenType |
| `defer` | v1.0 Defer Statement | ❌ Not in TokenType |
| `event` | v1.0 Events | ❌ Not in TokenType |
| `async` | v1.0 Async | ❌ Not in TokenType |
| `await` | v1.0 Async | ❌ Not in TokenType |
| `property` | v0.9 Properties | ❌ Not in TokenType |

---

## AUDIT #5 FINDINGS: Language Reference Cross-Check

### v0.1 Lexical Structure — VERIFIED COMPLETE

| Feature | Spec Section | Implementation | Status |
|---------|--------------|----------------|--------|
| UTF-8 source files | Lexical Structure | ✅ .NET handles | ✅ |
| 4-space indentation | Line Structure | ✅ Lexer INDENT/DEDENT | ✅ |
| Tabs not allowed | Line Structure | ✅ Enforced | ✅ |
| Single-line comments `#` | Comments | ✅ TokenType.Comment | ✅ |
| Identifier syntax | Identifiers | ✅ TokenType.Identifier | ✅ |
| Backtick escaping | Literal Names | ✅ TokenType.Backtick | ✅ |
| Line continuation `\` | Physical vs Logical Lines | ✅ TokenType.Backslash | ✅ |
| Implicit continuation (brackets) | Physical vs Logical Lines | ✅ Lexer handles | ✅ |
| Newline significance | Newline Significance | ✅ TokenType.Newline | ✅ |

### v0.1 Keywords — VERIFIED STATUS

| Keyword | Spec | TokenType | Implementation |
|---------|------|-----------|----------------|
| `and`, `or`, `not`, `is` | Hard Keywords | ✅ Present | ✅ Complete |
| `as` | Hard Keywords | ✅ TokenType.As | ✅ Complete |
| `assert` | Hard Keywords | ✅ TokenType.Assert | ✅ Complete |
| `break`, `continue` | Hard Keywords | ✅ Present | ✅ Complete |
| `class` | Hard Keywords | ✅ TokenType.Class | ✅ Complete |
| `const` | Hard Keywords | ✅ TokenType.Const | ✅ Complete |
| `def` | Hard Keywords | ✅ TokenType.Def | ✅ Complete |
| `elif`, `else`, `if` | Hard Keywords | ✅ Present | ✅ Complete |
| `except`, `finally`, `try` | Hard Keywords | ✅ Present | ✅ Complete |
| `for`, `while`, `in` | Hard Keywords | ✅ Present | ✅ Complete |
| `from`, `import` | Hard Keywords | ✅ Present | ✅ Complete |
| `lambda` | Hard Keywords | ✅ TokenType.Lambda | ✅ Complete (v0.4) |
| `pass` | Hard Keywords | ✅ TokenType.Pass | ✅ Complete |
| `raise` | Hard Keywords | ✅ TokenType.Raise | ✅ Complete |
| `return` | Hard Keywords | ✅ TokenType.Return | ✅ Complete |
| `True`, `False`, `None` | Hard Keywords | ✅ Present | ✅ Complete |
| `auto` | Hard Keywords (v0.8) | ✅ TokenType.Auto | ⚠️ Token only |
| `case` | Hard Keywords (v0.7) | ❌ Missing | ❌ Not Started |
| `defer` | Hard Keywords (v1.0) | ❌ Missing | ❌ Not Started |
| `enum` | Hard Keywords (v0.5) | ✅ TokenType.Enum | ✅ Complete |
| `event` | Hard Keywords (v1.0) | ❌ Missing | ❌ Not Started |
| `interface` | Hard Keywords (v0.3) | ✅ TokenType.Interface | ✅ Complete |
| `match` | Hard Keywords (v0.7) | ❌ Missing | ❌ Not Started |
| `property` | Hard Keywords (v0.9) | ❌ Missing | ❌ Not Started |
| `struct` | Hard Keywords (v0.3) | ✅ TokenType.Struct | ✅ Complete |
| `type` | Hard Keywords (v0.8) | ❌ Missing | ❌ Not Started |
| `with` | Hard Keywords (v1.0) | ✅ TokenType.With | ⚠️ Token only |

### v0.1 Literals — VERIFIED COMPLETE

| Literal | Spec | Implementation | Status |
|---------|------|----------------|--------|
| Integer `42`, `1_000` | Integer Literals | ✅ TokenType.Integer | ✅ |
| Integer suffixes `L`, `u`, `UL` | Integer Literals | ✅ Parsed | ✅ |
| Float `3.14`, `0.5` | Float Literals | ✅ TokenType.Float | ✅ |
| Float suffixes `f`, `d`, `m` | Float Literals | ✅ Parsed | ✅ |
| String single/double quotes | String Literals | ✅ TokenType.String | ✅ |
| Multi-line string `"""` | String Literals | ✅ Parsed | ✅ |
| Raw string `r"..."` | Raw Strings | ✅ TokenType.RawString | ✅ |
| Boolean `True`, `False` | Boolean Literals | ✅ TokenType.True/False | ✅ |
| None literal | None Literal | ✅ TokenType.None | ✅ |
| Ellipsis `...` | Special Literals | ✅ TokenType.Ellipsis | ✅ |
| Empty set `{/}` | Special Literals | ✅ Parsed | ✅ |

### v0.6 Extended Literals — VERIFIED COMPLETE

| Literal | Spec | Implementation | Status |
|---------|------|----------------|--------|
| Binary `0b1010` | Extended Numeric | ✅ Lexer parses | ✅ |
| Hexadecimal `0xFF` | Extended Numeric | ✅ Lexer parses | ✅ |
| Octal `0o755` | Extended Numeric | ✅ Lexer parses → decimal | ✅ |
| Scientific `6.022e23` | Extended Numeric | ✅ Lexer parses | ✅ |
| Underscores `1_000_000` | Extended Numeric | ✅ Lexer strips | ✅ |

### v0.6 F-Strings — VERIFIED COMPLETE

| Feature | Spec | Implementation | Status |
|---------|------|----------------|--------|
| Basic f-string `f"...{x}..."` | F-Strings | ✅ FString tokens | ✅ |
| Expressions in f-string | F-Strings | ✅ FStringExprStart/End | ✅ |
| Format specifiers `{x:.2f}` | F-Strings | ✅ FStringFormatSpec | ✅ |
| Multi-line f-string | F-Strings | ✅ Supported | ✅ |

### v0.1 Built-in Types — VERIFIED COMPLETE

| Type | Spec | TypeMapper | Status |
|------|------|------------|--------|
| `int` → `System.Int32` | Built-in Types | ✅ | ✅ |
| `long` → `System.Int64` | Built-in Types | ✅ | ✅ |
| `short` → `System.Int16` | Built-in Types | ✅ | ✅ |
| `byte` → `System.Byte` | Built-in Types | ✅ | ✅ |
| `uint` → `System.UInt32` | Built-in Types | ✅ | ✅ |
| `ulong` → `System.UInt64` | Built-in Types | ✅ | ✅ |
| `ushort` → `System.UInt16` | Built-in Types | ✅ | ✅ |
| `sbyte` → `System.SByte` | Built-in Types | ✅ | ✅ |
| `float` → `System.Single` | Built-in Types | ✅ | ✅ |
| `double` → `System.Double` | Built-in Types | ✅ | ✅ |
| `decimal` → `System.Decimal` | Built-in Types | ✅ | ✅ |
| `bool` → `System.Boolean` | Built-in Types | ✅ | ✅ |
| `str` → `System.String` | Built-in Types | ✅ | ✅ |
| `char` → `System.Char` | Built-in Types | ✅ | ✅ |
| `object` → `System.Object` | Built-in Types | ✅ | ✅ |

### v0.1 Operators — VERIFIED COMPLETE

| Operator | Spec | TokenType | CodeGen | Status |
|----------|------|-----------|---------|--------|
| `+`, `-`, `*`, `/` | Arithmetic | ✅ | ✅ | ✅ |
| `//` (floor div) | Arithmetic | ✅ DoubleSlash | ✅ → `(int)(x/y)` | ✅ |
| `%` (modulo) | Arithmetic | ✅ Percent | ✅ | ✅ |
| `**` (power) | Arithmetic | ✅ DoubleStar | ✅ → `Math.Pow()` | ✅ |
| `==`, `!=`, `<`, `>`, `<=`, `>=` | Comparison | ✅ | ✅ | ✅ |
| `and`, `or`, `not` | Logical | ✅ Keywords | ✅ → `&&`, `\|\|`, `!` | ✅ |
| `&`, `\|`, `^`, `~`, `<<`, `>>` | Bitwise | ✅ | ✅ | ✅ |
| `=`, `+=`, `-=`, etc. | Assignment | ✅ | ✅ | ✅ |
| `is`, `is not` | Identity | ✅ | ✅ | ✅ |
| `in`, `not in` | Membership | ✅ | ✅ → `.Contains()` | ✅ |

### v0.2 Nullable Types — VERIFIED COMPLETE

| Feature | Spec | Implementation | Status |
|---------|------|----------------|--------|
| `T?` annotation | Nullable Types | ✅ NullableType AST | ✅ |
| `?.` null-conditional | Null-Conditional | ✅ TokenType.NullConditional | ✅ |
| `??` null-coalescing | Null-Coalescing | ✅ TokenType.NullCoalesce | ✅ |
| `is None` narrowing | Type Narrowing | ✅ TypeChecker._narrowedTypes | ✅ |
| `isinstance()` narrowing | Type Narrowing | ✅ TypeChecker handles | ✅ |

### v0.4 Generic Functions — NOT IMPLEMENTED

Per Language Reference (lines 1650-1660):
```python
def identity[T](value: T) -> T:
    return value
```

**Implementation Status:**
- ❌ `FunctionDef` AST has NO `TypeParameters` property
- ❌ Parser cannot parse `def foo[T](...)`
- ❌ CodeGen has no support for generic methods

**Required Changes:**
1. Add `TypeParameters` property to `FunctionDef` record
2. Update `Parser.ParseFunctionDef()` to parse `[T, U, ...]` after function name
3. Update `RoslynEmitter.GenerateMethod()` to emit type parameters
4. Update semantic analysis to handle generic function type resolution

### v0.4 Type Constraints — NOT IMPLEMENTED

Per Language Reference (lines 1680-1700):
```python
def find_max[T: IComparable[T]](items: list[T]) -> T:
    ...
```

**Implementation Status:**
- ❌ No constraint syntax in parser
- ❌ No `TypeConstraint` AST node
- ❌ No constraint validation in semantic analysis

### v0.7 Pattern Matching — NOT IMPLEMENTED

Per Language Reference (lines 1880-1950):
- Missing `TokenType.Match` and `TokenType.Case`
- No `MatchStatement` or `CaseClause` AST nodes
- No pattern AST nodes

### v0.8 Type Aliases — NOT IMPLEMENTED

Per Language Reference (lines 1960-2000):
```python
type UserId = int
type Callback[T] = (T) -> None
```

**Implementation Status:**
- ❌ Missing `TokenType.Type`
- ❌ No `TypeAliasStatement` AST node

### v0.8 Tagged Unions — NOT IMPLEMENTED

Per Language Reference (lines 2000-2100):
```python
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)
```

**Implementation Status:**
- ❌ Requires type alias foundation
- ❌ No case-with-data enum support

### v0.9 Walrus Operator — NOT IMPLEMENTED

Per Language Reference (lines 2150-2180):
```python
if (match := pattern.search(text)) is not None:
    ...
```

**Implementation Status:**
- ❌ No `ColonEquals` TokenType
- ❌ No `AssignmentExpression` AST node (Sharpy-specific)
- ❌ Parser doesn't recognize `:=`

### v0.9 Properties — NOT IMPLEMENTED

Per Language Reference (lines 2190-2250):
```python
class Temperature:
    property celsius(self) -> double:
        return self.__celsius
```

**Implementation Status:**
- ❌ No `TokenType.Property`
- ❌ No `PropertyDef` AST node
- ❌ Parser doesn't recognize `property` keyword

### v1.0 Context Managers — PARTIAL

Per Language Reference (lines 2260-2280):
```python
with open("file.txt", "r") as f:
    content = f.read()
```

**Implementation Status:**
- ✅ `TokenType.With` exists
- ❌ No `WithStatement` AST node
- ❌ Parser doesn't parse `with` statement

### v1.0 Defer Statement — NOT IMPLEMENTED

Per Language Reference (lines 2290-2350):
```python
def process_file(path: str) -> str:
    file = open(path, "r")
    defer:
        file.close()
```

**Implementation Status:**
- ❌ No `TokenType.Defer`
- ❌ No `DeferStatement` AST node

### v1.0 Events — NOT IMPLEMENTED

Per Language Reference (lines 2360-2430):
```python
class Button:
    event clicked: (object, EventArgs) -> None
```

**Implementation Status:**
- ❌ No `TokenType.Event`
- ❌ No `EventDeclaration` AST node

### v1.0 Async/Await — NOT IMPLEMENTED

Per Language Reference (lines 2440-2500):
```python
async def fetch_data(url: str) -> str:
    await asyncio.sleep(1.0)
```

**Implementation Status:**
- ❌ No `TokenType.Async`, `TokenType.Await`
- ❌ No async-related AST nodes

### Standard Library Gaps — VERIFIED

| Function | Spec | Sharpy.Core | Status |
|----------|------|-------------|--------|
| `hash(x)` | Object Functions | ❌ No standalone | ❌ NOT IMPLEMENTED |
| `id(x)` | Object Functions | ❌ No standalone | ❌ NOT IMPLEMENTED |
| `open()` | I/O Functions | ❌ Not found | ❌ NOT IMPLEMENTED |
| `input(prompt)` | I/O Functions | ✅ Input.cs | ✅ |

---

## TODO: Next Iteration Actions

### HIGHEST PRIORITY — For Next Audit Session

1. **Verify Operator Precedence Implementation**
   - Cross-check Language Reference precedence table (lines 700-750) against Parser.cs
   - Verify `GenerateExpression` respects precedence
   - Test edge cases: `a or b and c`, `a ** b ** c` (right-associative)

2. **Verify Default Parameter Behavior**
   - Language Reference warns about mutable defaults (lines 1200-1250)
   - Test: Does Sharpy evaluate defaults at definition time or call time?
   - Document actual behavior vs Python behavior

3. **Audit .NET Interop Features**
   - Test importing .NET types: `from system.collections.generic import List`
   - Test calling .NET methods
   - Test .NET property access (Language Reference lines 2540-2560)
   - Test extension methods (LINQ)

4. **Complete Feature Coverage Matrix**
   - For each v0.1-v0.6 feature, list:
     - Lexer support (TokenType)
     - Parser support (AST node)
     - Semantic support (TypeChecker method)
     - CodeGen support (RoslynEmitter method)
     - Integration test file

5. **Document Remaining Language Reference Sections**
   The following sections have NOT been audited:
   - **Comprehensions** (lines 2100-2150): Verify nested comprehension behavior
   - **Naming Conventions** (lines 2560-2590): Verify `snake_case` → `PascalCase`
   - **Program Entry Point** (lines 2590-2610): Verify `main()` and top-level
   - **v2.0+ Deferred Features** (lines 2612-2620): Document explicitly

### FILES TO REVIEW IN NEXT AUDIT

**For Operator Precedence Verification:**
- `src/Sharpy.Compiler/Parser/Parser.cs` — `ParseExpression()` precedence climbing
- `src/Sharpy.Compiler/Parser/Precedence.cs` — If exists

**For Default Parameter Verification:**
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` — `GenerateMethod()` default handling
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs` — Default parameter type checking

**For .NET Interop Testing:**
- Create test file: `samples/dotnet_interop_test.spy`
- Test: Import System.Collections.Generic.List, call methods, access properties
