# Sharpy Implementation Status v1.0

This document tracks which features from the [Sharpy Language Reference v1](../specs/sharpy_language_reference_v1.md) are implemented in the compiler. Use this as a reference to identify remaining work and generate tasks for implementation.

**Last Updated**: December 3, 2025
**Verified Against**: `mainline` branch

---

## Overview

| Version | Focus Area | Implementation Status |
|---------|------------|----------------------|
| **v0.1** | Core Language | ✅ Complete (except try-else) |
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

## v0.1 — Core Language ✅ COMPLETE

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
| User-defined function overloading | ❌ NOT IMPLEMENTED | Parser allows, but no semantic support |
| Constructor overloading | ✅ | Via multiple `__init__` |

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
| `len(x)` | `Builtins/Exports.cs` | ✅ `Len()` for strings, arrays, collections | ✅ |
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
| `hash(x)` | - | ❌ No standalone `Hash()` function | ❌ NOT IMPLEMENTED |
| `id(x)` | - | ❌ No standalone `Id()` function (interface `IIdentifiable` exists) | ❌ NOT IMPLEMENTED |

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

## TODO: Additional Documentation Needed

The following sections still require verification and documentation in a future iteration:

### 1. Missing Standard Library Functions
- [ ] **Implement `hash(x)`**: Add global `Hash()` function in `Sharpy.Core` that calls `GetHashCode()`
- [ ] **Implement `id(x)`**: Add global `Id()` function using `RuntimeHelpers.GetHashCode()` for identity

### 2. Semantic Analysis (`Sharpy.Compiler/Semantic/`) — NEEDS VERIFICATION
- [ ] Document type inference implementation (appears functional based on `TypeChecker.cs` with `_narrowedTypes`)
- [ ] Document operator overload resolution (`CachedOverloadDiscoveryService` in `Discovery/`)
- [ ] Document protocol validation (`ProtocolValidator.cs`, `ProtocolSignatureValidator.cs`)
- [ ] Verify dunder method semantic validation coverage (`OperatorValidator.cs`, `OperatorSignatureValidator.cs`)

### 3. Test Coverage Gaps — NEEDS AUDIT
- [ ] Audit integration test coverage for all v0.1-v0.6 features
- [ ] Identify missing edge case tests (especially for comprehensions, slicing)
- [ ] Document any features that pass parsing but fail in codegen or runtime
- [ ] Create tests for newly verified standard library functions

### 4. Error Messages — NEEDS REVIEW
- [ ] Document which error messages exist for incomplete features
- [ ] Identify user-facing error message improvements needed
- [ ] Verify `NotImplementedException` messages are user-friendly

### 5. .NET Interop Verification — NEEDS TESTING
- [ ] Test and document calling .NET methods from Sharpy
- [ ] Test and document using .NET types as base classes/interfaces
- [ ] Verify LINQ extension methods work correctly with Sharpy collections

### 6. Language Reference Coverage — NEXT ITERATION
The following sections from the language reference still need detailed verification:

#### v0.7 — Pattern Matching (NOT STARTED)
- [ ] Add `match` and `case` keywords to Lexer (`TokenType`)
- [ ] Add `MatchStatement`, `CaseClause`, pattern AST nodes to Parser
- [ ] Implement pattern matching codegen to C# switch expressions

#### v0.8 — Type Aliases & ADTs (NOT STARTED)
- [ ] Add `type` keyword to Lexer (currently not present)
- [ ] Add `TypeAlias` AST node to Parser
- [ ] Implement tagged unions/ADTs (discriminated union pattern)
- [ ] Implement variable shadowing with `auto` (token exists, needs parser/codegen)

#### v1.0 — Resources & Async (NOT STARTED)
- [ ] Implement `with` statement parsing (`With` token exists)
- [ ] Add `defer`, `event`, `async`, `await` keywords to Lexer
- [ ] Implement context manager codegen → C# `using`
- [ ] Implement defer codegen → `try`/`finally` pattern
- [ ] Implement async/await codegen → C# `async`/`await`

### 7. Codegen NotImplementedException Audit — NEEDS FIX
The following throw `NotImplementedException` in `RoslynEmitter.cs`:
- [ ] **Line 1362**: Complex tuple unpacking (non-identifier targets) in assignments
- [ ] **Line 1571**: Complex for loop tuple unpacking
- [ ] **Line 1699**: Complex function expressions
- [ ] **Line 1927, 1997, 2066**: Tuple unpacking in comprehensions
- [ ] **Line 1956, 2024, 2093**: Nested comprehensions (multiple for clauses)

---

## Summary for Task Generation


### ✅ Complete (No Action Required)
- v0.1: Core Language (except `try ... else:` clause)
- v0.2: Nullability & Collections (except star unpacking `*rest`)
- v0.3: Structs, Interfaces, OOP (decorators `@virtual`/`@override`/`@abstract`/`@sealed` work)
- v0.5: Enums & Operators (core features)
- Standard Library: Core builtins (`print`, `len`, `range`, `enumerate`, `zip`, `map`, `filter`, etc.)

### ⚠️ Needs Completion (Prioritize)
- **v0.1**: `try ... else:` clause (execute code only if no exception)
- **v0.3**: Align `@final` decorator (spec) with `@sealed` (implementation), user-defined function overloading
- **v0.4**: Generic functions with type parameters, type constraints
- **v0.6**: Loop else clause (`for ... else:`, `while ... else:`)
- **v0.9**: Nested comprehensions, tuple unpacking in comprehensions, walrus operator (`:=`), properties
- **Standard Library**: `hash(x)`, `id(x)` global functions

### ❌ Not Started (Future Work)
- **v0.7**: Pattern matching (`match`/`case`) — Lexer, Parser, CodeGen all needed
- **v0.8**: Type aliases (`type` keyword), tagged unions/ADTs, variable shadowing
- **v1.0**: Context managers (`with`), `defer`, events, async/await

---

## Next Documentation Iteration

The following areas were NOT fully verified in this iteration and should be the focus of the next documentation pass:

### High Priority (Blocking Task Generation)
1. **Test Coverage Audit**: Run `dotnet test --filter "FullyQualifiedName~Integration"` and map test coverage to features
2. **Semantic Analysis Documentation**: Trace through `NameResolver` → `TypeResolver` → `TypeChecker` flow and document
3. **Generic Functions**: Verify if `def foo[T](x: T) -> T` is really not supported or just undocumented

### Medium Priority (Improves Task Quality)
1. **.NET Interop Testing**: Create test files to verify calling .NET APIs from Sharpy works as expected
2. **Error Message Quality**: Run compiler on intentionally broken code and document error messages
3. **Enum String Values**: Verify if string enum values generate to static class correctly

### Low Priority (Nice to Have)
1. **Performance**: Document any known performance issues in collections or codegen
2. **Edge Cases**: Document any known quirks or edge cases in the implementation
3. **Cross-Reference**: Link each feature to its corresponding test file(s)

### Files to Review Next Iteration
- `src/Sharpy.Compiler.Tests/Integration/` - Integration test coverage
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs` - Type checking and inference
- `src/Sharpy.Compiler/Discovery/` - Overload resolution
- `src/Sharpy.Core.Tests/` - Standard library test coverage
