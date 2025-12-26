# Sharpy Implementation Status v1.0

This document tracks which features from the [Sharpy Language Reference v1](../specs/sharpy_language_reference_v1.md) are implemented in the compiler. Use this as a reference to identify remaining work and generate tasks for implementation.

**Last Updated**: December 4, 2025 (Audit #8)
**Verified Against**: `mainline` branch
**Audit Scope**: Keywords, AST nodes, CodeGen NotImplementedException locations, Semantic analysis, Standard library, Test coverage mapping, TokenType verification, Language Reference cross-check, Standard library builtins verification, Operator precedence, Naming conventions, Type casting syntax, Comprehension scoping, Dunder invocation rules audit

---

## Table of Contents

1. [Overview](#overview)
2. [Version Status Details](#version-status-details)
   - [v0.1 — Core Language](#v01--core-language--95-complete)
   - [v0.2 — Nullability & Collections](#v02--nullability--collections--complete)
   - [v0.3 — Structs, Interfaces, OOP](#v03--structs-interfaces-oop--95-complete)
   - [v0.4 — Generics](#v04--generics--85-complete)
   - [v0.5 — Enums & Operators](#v05--enums--operators--complete)
   - [v0.6 — Extended Syntax](#v06--extended-syntax--90-complete)
   - [v0.7 — Pattern Matching](#v07--pattern-matching--not-implemented)
   - [v0.8 — Type Aliases & ADTs](#v08--type-aliases--adts--not-implemented)
   - [v0.9 — Comprehensions & Properties](#v09--comprehensions--properties--60-complete)
   - [v1.0 — Resources & Async](#v10--resources--async--not-implemented)
3. [Standard Library Status](#standard-library-sharpy-core--verified)
4. [Verified Implementation Details](#verified-implementation-details)
5. [Implementation Summary](#implementation-summary)
6. [TODO: Implementation Tasks](#todo-implementation-tasks)
7. [Appendix: Code Locations](#appendix-verified-code-locations)
8. [Audit History](#audit-history)

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

## Version Status Details

### v0.1 — Core Language ⚠️ ~95% COMPLETE

#### Lexical Structure

| Feature | Lexer | Parser | CodeGen | Tests | Status |
|---------|-------|--------|---------|-------|--------|
| UTF-8 source files | ✅ | - | - | ✅ | ✅ |
| 4-space indentation | ✅ INDENT/DEDENT tokens | - | ✅ → `{ }` | ✅ | ✅ |
| Single-line comments (`#`) | ✅ `TokenType.Comment` | - | ✅ stripped | ✅ | ✅ |
| Identifiers | ✅ `TokenType.Identifier` | ✅ `Identifier` | ✅ | ✅ | ✅ |
| Backtick literal names | ✅ `TokenType.Backtick` | ✅ | ✅ | ✅ | ✅ |
| Line continuation (`\`) | ✅ `TokenType.Backslash` | ✅ | ✅ | ✅ | ✅ |
| Implicit continuation (brackets) | ✅ | ✅ | ✅ | ✅ | ✅ |

#### Keywords

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

#### Literals

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

#### Built-in Types

| Type | Parser | CodeGen | Status |
|------|--------|---------|--------|
| `int`, `long`, `short`, `byte` | ✅ | ✅ | ✅ |
| `uint`, `ulong`, `ushort`, `sbyte` | ✅ | ✅ | ✅ |
| `float`, `double`, `decimal` | ✅ | ✅ | ✅ |
| `bool` | ✅ | ✅ | ✅ |
| `str` | ✅ | ✅ → `string` | ✅ |
| `char` | ✅ | ✅ | ✅ |
| `object` | ✅ | ✅ | ✅ |

#### Type Hierarchy and Object Model

| Feature | Semantic | CodeGen | Tests | Status |
|---------|----------|---------|-------|--------|
| `object` as universal base type | ✅ | ✅ → `System.Object` | ✅ | ✅ |
| Primitives assignable to `object` (boxing) | ✅ | ✅ | ✅ | ✅ |
| Structs assignable to `object` (boxing) | ✅ | ✅ | ✅ | ✅ |
| `None` assignable to `object?` only | ✅ | ✅ | ⚠️ | ✅ |

#### Dunder Invocation Rules

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

#### Operators

| Category | Operators | Lexer | CodeGen | Status |
|----------|-----------|-------|---------|--------|
| Arithmetic | `+`, `-`, `*`, `/`, `//`, `%`, `**` | ✅ | ✅ (`**` → `Math.Pow`) | ✅ |
| Comparison | `==`, `!=`, `<`, `>`, `<=`, `>=` | ✅ | ✅ | ✅ |
| Logical | `and`, `or`, `not` | ✅ | ✅ → `&&`, `\|\|`, `!` | ✅ |
| Bitwise | `&`, `\|`, `^`, `~`, `<<`, `>>` | ✅ | ✅ | ✅ |
| Assignment | `=`, `+=`, `-=`, etc. | ✅ | ✅ | ✅ |
| Identity | `is`, `is not` | ✅ | ✅ | ✅ |
| Membership | `in`, `not in` | ✅ | ✅ → `.Contains()` | ✅ |

#### Statements

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

#### Control Flow

| Statement | Parser | CodeGen | Tests | Status |
|-----------|--------|---------|-------|--------|
| `if`/`elif`/`else` | ✅ `IfStatement` | ✅ | ✅ | ✅ |
| `while` | ✅ `WhileStatement` | ✅ | ✅ | ✅ |
| `for ... in` | ✅ `ForStatement` | ✅ → `foreach` | ✅ | ✅ |

#### Exception Handling

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| `try`/`except`/`finally` | ✅ `TryStatement` | ✅ → `try`/`catch`/`finally` | ✅ | ✅ |
| `except Type as e:` | ✅ `ExceptHandler` | ✅ | ✅ | ✅ |
| `raise` | ✅ `RaiseStatement` | ✅ → `throw` | ✅ | ✅ |
| `raise ... from ...` | ✅ | ✅ → inner exception | ⚠️ | ✅ |
| `else` clause in try | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |

#### Functions

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Function definition | ✅ `FunctionDef` | ✅ | ✅ | ✅ |
| Parameters with types | ✅ `Parameter` | ✅ | ✅ | ✅ |
| Return type annotation | ✅ | ✅ | ✅ | ✅ |
| Default parameters | ✅ | ✅ | ✅ | ✅ |
| Docstrings | ✅ | ✅ → XML doc comments | ✅ | ✅ |

#### Classes

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Class definition | ✅ `ClassDef` | ✅ | ✅ | ✅ |
| Field declarations | ✅ `VariableDeclaration` | ✅ | ✅ | ✅ |
| Constructor (`__init__`) | ✅ | ✅ → constructor | ✅ | ✅ |
| Instance methods | ✅ | ✅ | ✅ | ✅ |
| `self` parameter | ✅ | ✅ (filtered out) | ✅ | ✅ |

#### Imports

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| `import module` | ✅ `ImportStatement` | ✅ → `using` | ✅ | ✅ |
| `import module as alias` | ✅ | ✅ → `using alias = ` | ✅ | ✅ |
| `from module import name` | ✅ `FromImportStatement` | ✅ → `using static` | ✅ | ✅ |
| `from module import *` | ✅ | ✅ | ⚠️ | ✅ |
| Module name transformation | - | ✅ snake_case → PascalCase | ✅ | ✅ |

---

### v0.2 — Nullability & Collections ✅ COMPLETE

#### Nullable Types

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Nullable annotation (`T?`) | ✅ `NullableType` | ✅ → `T?` | ✅ | ✅ |
| Null-conditional (`?.`) | ✅ `MemberAccess.IsNullConditional` | ✅ → `?.` | ✅ | ✅ |
| Null-coalescing (`??`) | ✅ `BinaryOp.NullCoalesce` | ✅ → `??` | ✅ | ✅ |

#### Type Narrowing

| Feature | Semantic | CodeGen | Tests | Status |
|---------|----------|---------|-------|--------|
| `is None` / `is not None` | ✅ `TypeChecker._narrowedTypes` | ✅ | ✅ | ✅ |
| `isinstance(x, Type)` | ✅ | ✅ | ✅ | ✅ |

#### Collection Literals

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

#### Tuple Operations

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Tuple unpacking `x, y = point` | ✅ | ✅ → deconstruction | ✅ | ✅ |
| Star unpacking `first, *rest = items` | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |

#### Slicing

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Basic slice `[start:stop]` | ✅ `SliceAccess` | ✅ → `Slice()` method | ✅ | ✅ |
| Slice with step `[::step]` | ✅ | ✅ | ⚠️ | ✅ |
| Negative indices | ✅ | ✅ | ✅ | ✅ |

---

### v0.3 — Structs, Interfaces, OOP ⚠️ ~95% COMPLETE

#### Structs

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Struct definition | ✅ `StructDef` | ✅ → `struct` | ✅ | ✅ |
| Struct fields | ✅ | ✅ | ✅ | ✅ |
| Struct methods | ✅ | ✅ | ✅ | ✅ |
| Struct constructor | ✅ | ✅ | ✅ | ✅ |

#### Interfaces

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Interface definition | ✅ `InterfaceDef` | ✅ → `interface` | ✅ | ✅ |
| Interface methods | ✅ | ✅ | ✅ | ✅ |
| Interface inheritance | ✅ `BaseInterfaces` | ✅ | ✅ | ✅ |

#### Inheritance

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Single class inheritance | ✅ `ClassDef.BaseClasses` | ✅ → `: BaseClass` | ✅ | ✅ |
| Multiple interface implementation | ✅ | ✅ → `: IFoo, IBar` | ✅ | ✅ |
| `super().__init__()` | ✅ | ✅ → `: base()` | ✅ | ✅ |

#### Decorators (Access Modifiers)

| Decorator | Parser | CodeGen | Tests | Status |
|-----------|--------|---------|-------|--------|
| (default) = `public` | ✅ | ✅ | ✅ | ✅ |
| `@private` | ✅ `Decorator` | ✅ → `private` | ✅ | ✅ |
| `@protected` | ✅ | ✅ → `protected` | ✅ | ✅ |
| `@internal` | ✅ | ✅ → `internal` | ✅ | ✅ |
| Naming convention (`_`, `__`) | ✅ | ✅ | ✅ | ✅ |

#### Decorators (Method/Class Modifiers)

| Decorator | Parser | CodeGen | Tests | Status |
|-----------|--------|---------|-------|--------|
| `@static` | ✅ | ✅ → `static` | ✅ | ✅ |
| `@override` | ✅ | ✅ → `override` | ⚠️ | ✅ |
| `@virtual` | ✅ | ✅ → `virtual` | ⚠️ | ✅ |
| `@abstract` | ✅ | ✅ → `abstract` | ⚠️ | ✅ |
| `@final` (method) | ✅ | ⚠️ Uses `@sealed`, not `@final` | ⚠️ | ⚠️ PARTIAL |
| `@final` (class) | ✅ | ⚠️ Uses `@sealed`, not `@final` | ⚠️ | ⚠️ PARTIAL |

**Note**: Decorators `@virtual`, `@override`, `@abstract`, `@sealed` are implemented in `RoslynEmitter.cs` (lines 499-503 for methods, 765-768 for classes). The language spec uses `@final` but implementation uses `@sealed` — needs alignment.

#### Function Overloading

| Feature | Status | Notes |
|---------|--------|-------|
| User-defined function overloading | ❌ NOT IMPLEMENTED | `NameResolver.cs:262` explicitly rejects duplicate function names |
| Builtin function overloading | ✅ | Via `BuiltinRegistry.GetFunctionOverloads()` |
| Operator method overloading | ✅ | Via `TypeSymbol.OperatorMethods` dictionary |
| Constructor overloading | ✅ | Via multiple `__init__` methods |

---

### v0.4 — Generics ⚠️ ~85% COMPLETE

#### Generic Classes/Structs/Interfaces

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Generic class `Box[T]` | ✅ `ClassDef.TypeParameters` | ✅ → `Box<T>` | ✅ | ✅ |
| Generic struct `Pair[T1, T2]` | ✅ | ✅ | ✅ | ✅ |
| Generic interface `IContainer[T]` | ✅ | ✅ | ✅ | ✅ |
| Generic instantiation `Box[int]()` | ✅ `GenericType` | ✅ | ✅ | ✅ |

#### Generic Functions

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| Generic function `def identity[T](x: T) -> T` | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |

#### Type Constraints

| Constraint | Parser | CodeGen | Status |
|------------|--------|---------|--------|
| `T: Interface` | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| `T: class` | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| `T: struct` | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| `T: new()` | ❌ | ❌ | ❌ NOT IMPLEMENTED |

#### Type Casting

| Feature | Spec Syntax | Implementation | Status |
|---------|-------------|----------------|--------|
| Type cast | `cast[T](value)` | `value as T` (TypeCast AST) | ⚠️ SYNTAX MISMATCH |

**Note**: Language Reference specifies `cast[T](value)` syntax (line 350), but implementation uses `value as T` syntax. This is a spec vs implementation discrepancy.

#### Lambda Expressions

| Feature | Lexer | Parser | CodeGen | Tests | Status |
|---------|-------|--------|---------|-------|--------|
| Lambda `lambda x: x + 1` | ✅ `TokenType.Lambda` | ✅ `LambdaExpression` | ✅ → `x => x + 1` | ✅ | ✅ |
| Lambda with multiple params | ✅ | ✅ | ✅ | ✅ | ✅ |

---

### v0.5 — Enums & Operators ✅ COMPLETE

#### Enumerations

| Feature | Lexer | Parser | CodeGen | Tests | Status |
|---------|-------|--------|---------|-------|--------|
| Enum definition | ✅ `TokenType.Enum` | ✅ `EnumDef` | ✅ → C# `enum` | ✅ | ✅ |
| Integer enum values | ✅ | ✅ `EnumMember` | ✅ | ✅ | ✅ |
| String enum values | ✅ | ✅ | ⚠️ → static class | ⚠️ | ⚠️ PARTIAL |
| `.name` property | - | - | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| `.value` property | - | - | ❌ | ❌ | ❌ NOT IMPLEMENTED |

#### Operator Overloading (Dunder Methods)

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

### v0.6 — Extended Syntax ⚠️ ~90% COMPLETE

#### F-Strings

| Feature | Lexer | Parser | CodeGen | Tests | Status |
|---------|-------|--------|---------|-------|--------|
| Basic f-string `f"Hello {name}"` | ✅ `FStringStart/Text/ExprStart/End` | ✅ `FStringLiteral` | ✅ → `$"..."` | ✅ | ✅ |
| Expressions in f-string | ✅ | ✅ `FStringPart.Expression` | ✅ | ✅ | ✅ |
| Format specifiers `{x:.2f}` | ✅ `FStringFormatSpec` | ✅ `FStringPart.FormatSpec` | ✅ | ✅ | ✅ |
| Multi-line f-string | ✅ | ✅ | ✅ | ⚠️ | ✅ |

#### Extended Numeric Literals

| Literal | Lexer | Parser | CodeGen | Status |
|---------|-------|--------|---------|--------|
| Binary `0b1010` | ✅ | ✅ | ✅ | ✅ |
| Hexadecimal `0xFF` | ✅ | ✅ | ✅ | ✅ |
| Octal `0o755` | ✅ | ✅ → decimal | ✅ | ✅ |
| Scientific `6.022e23` | ✅ | ✅ | ✅ | ✅ |
| Underscores `1_000_000` | ✅ | ✅ | ✅ | ✅ |

#### Comparison Chaining

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| `a < b < c` | ✅ `ComparisonChain` | ✅ → `a < b && b < c` | ✅ | ✅ |
| Multiple comparisons | ✅ | ✅ | ✅ | ✅ |

#### Loop Else Clause

| Feature | Parser | CodeGen | Status |
|---------|--------|---------|--------|
| `for ... else:` | ❌ No AST field | ❌ | ❌ NOT IMPLEMENTED |
| `while ... else:` | ❌ | ❌ | ❌ NOT IMPLEMENTED |

---

### v0.7 — Pattern Matching ❌ NOT IMPLEMENTED

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

### v0.8 — Type Aliases & ADTs ❌ NOT IMPLEMENTED

| Feature | Lexer | Parser | CodeGen | Status |
|---------|-------|--------|---------|--------|
| `type` keyword | ❌ | ❌ | ❌ | ❌ |
| Type aliases `type UserId = int` | ❌ | ❌ | ❌ | ❌ |
| Generic type aliases | ❌ | ❌ | ❌ | ❌ |
| Tagged unions / ADTs | ❌ | ❌ | ❌ | ❌ |
| Variable shadowing with `auto` | ⚠️ Token exists | ❌ | ❌ | ❌ PARTIAL |

---

### v0.9 — Comprehensions & Properties ⚠️ ~60% COMPLETE

#### Comprehensions

| Feature | Parser | CodeGen | Tests | Status |
|---------|--------|---------|-------|--------|
| List comprehension `[x for x in items]` | ✅ `ListComprehension` | ✅ → LINQ `.Select().ToList()` | ✅ | ✅ |
| List comprehension with filter `[x for x in items if cond]` | ✅ | ✅ → LINQ `.Where().Select()` | ✅ | ✅ |
| Dict comprehension `{k: v for k, v in items}` | ✅ `DictComprehension` | ✅ → `.ToDictionary()` | ✅ | ✅ |
| Set comprehension `{x for x in items}` | ✅ `SetComprehension` | ✅ → `.ToHashSet()` | ✅ | ✅ |
| Nested comprehensions (multiple `for`) | ✅ Parser supports | ❌ `NotImplementedException` | ❌ | ❌ NOT IMPLEMENTED |
| Tuple unpacking in comprehensions | ✅ Parser supports | ❌ `NotImplementedException` | ❌ | ❌ NOT IMPLEMENTED |

#### Walrus Operator

| Feature | Lexer | Parser | CodeGen | Status |
|---------|-------|--------|---------|--------|
| `:=` assignment expression | ❌ | ❌ | ❌ | ❌ NOT IMPLEMENTED |

#### Properties

| Feature | Parser | CodeGen | Status |
|---------|--------|---------|--------|
| `property` keyword | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| Auto properties | ❌ | ❌ | ❌ NOT IMPLEMENTED |
| Explicit get/set | ❌ | ❌ | ❌ NOT IMPLEMENTED |

---

### v1.0 — Resources & Async ❌ NOT IMPLEMENTED

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
| `repr(x)` | `Repr.cs` | ✅ `Repr()` — calls `__Repr__` if defined, else `.ToString()` | ✅ |
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
| `hash(x)` | - | ❌ Needs `Hash()` — interfaces exist (`IHashable.__Hash__`) but no standalone function | ❌ NOT IMPLEMENTED |
| `id(x)` | - | ❌ Needs `Id()` — interface exists (`IIdentifiable.__Id__`) but no standalone function | ❌ NOT IMPLEMENTED |

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

## Verified Implementation Details

### TokenType Keywords — VERIFIED

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

### Operator Precedence — VERIFIED COMPLETE

| Precedence | Language Ref | Parser Method | Status |
|------------|-------------|---------------|--------|
| 1 (highest) | `()`, `[]`, `.`, `?.` | `ParsePostfix()` | ✅ |
| 2 | `**` (right-assoc) | `ParsePower()` — right-assoc via `ParseUnary()` call | ✅ |
| 3 | `+x`, `-x`, `~x` | `ParseUnary()` | ✅ |
| 4 | `*`, `/`, `//`, `%` | `ParseMultiplicative()` | ✅ |
| 5 | `+`, `-` | `ParseAdditive()` | ✅ |
| 6 | `<<`, `>>` | `ParseShift()` | ✅ |
| 7 | `&` | `ParseBitwiseAnd()` | ✅ |
| 8 | `^` | `ParseBitwiseXor()` | ✅ |
| 9 | `\|` | `ParseBitwiseOr()` | ✅ |
| 10 | `in`, `not in`, `is`, `is not`, comparisons | `ParseComparison()` + chain support | ✅ |
| 11 | `not` | `ParseLogicalNot()` | ✅ |
| 12 | `and` | `ParseLogicalAnd()` | ✅ |
| 13 | `or` | `ParseLogicalOr()` | ✅ |
| 14 | `??` | `ParseNullCoalesce()` | ✅ |
| 15 | `x if c else y` | `ParseConditionalExpression()` | ✅ |
| 16 | `lambda` | `ParseLambda()` (called from `ParsePrimary()`) | ✅ |

### Naming Conventions — VERIFIED COMPLETE

| Type | Sharpy | C# Transformation | Implementation | Status |
|------|--------|-------------------|----------------|--------|
| Method/Function | `snake_case` | `PascalCase` | `ToPascalCase()` | ✅ |
| Variable | `snake_case` | `camelCase` | `ToCamelCase()` | ✅ |
| Parameter | `snake_case` | `camelCase` | `ToCamelCase()` | ✅ |
| Field | `snake_case` | `camelCase` | `ToCamelCase()` | ✅ |
| Namespace/Module | `snake_case` | `PascalCase` | `SimpleToPascalCase()` | ✅ |
| Class/Struct | `PascalCase` | (unchanged) | Direct pass-through | ✅ |
| Interface | `IPascalCase` | (unchanged) | Direct pass-through | ✅ |
| Constant | `CAPS_SNAKE_CASE` | (unchanged) | Direct pass-through | ✅ |
| Enum value | `CAPS_SNAKE_CASE` | `PascalCase` | `ToPascalCase()` | ✅ |

### Comprehension Variable Scoping — VERIFIED COMPLETE

Per Language Reference, comprehension variables should be block-scoped and not leak to outer scope.

**Verified in `TypeChecker.cs`:**
- `CheckListComprehension()` (line 1389): `_symbolTable.EnterScope("list-comprehension")`
- `CheckSetComprehension()` (line 1455): `_symbolTable.EnterScope("set-comprehension")`
- `CheckDictComprehension()`: Uses similar scope management

---

## Implementation Summary

### ✅ Complete (No Action Required)

- **v0.1**: Core Language — all features except `try ... else:` clause and dunder invocation validation
- **v0.2**: Nullability & Collections — all features except star unpacking (`*rest`)
- **v0.3**: Structs, Interfaces, OOP — decorators `@virtual`/`@override`/`@abstract`/`@sealed` work
- **v0.5**: Enums (integer) & Operator Overloading — core features work
- **v0.6**: F-strings, extended numeric literals, comparison chaining
- **Standard Library**: Core builtins (`print`, `len`, `range`, `enumerate`, `zip`, `map`, `filter`, `sorted`, `reversed`, `min`, `max`, `sum`, `all`, `any`, `abs`, `pow`, `round`, `divmod`, `isinstance`, `type`, `input`, **`repr`**)

### ⚠️ Needs Completion (Prioritize for v1.0 Release)

| Version | Feature | Lexer | Parser | Semantic | CodeGen | Tests |
|---------|---------|-------|--------|----------|---------|-------|
| v0.1 | `try ... else:` clause | ✅ | ❌ | ❌ | ❌ | ❌ |
| v0.1 | Dunder invocation validation | - | - | ❌ | - | ❌ |
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

## TODO: Implementation Tasks

This section consolidates all implementation tasks. Tasks are organized by priority and grouped by component.

### PRIORITY 1: Missing Core Features (v0.1-v0.6)

These features are part of the core language specification and should be completed before v1.0 release.

#### 1.1 Try-Else Clause (v0.1)

**Description**: Add `else` clause support to try/except blocks. The else clause executes only if no exception was raised.

**Tasks**:
- [ ] Add `ElseBody` property to `TryStatement` AST node
- [ ] Update `Parser.ParseTryStatement()` to parse `else:` block after handlers
- [ ] Update `RoslynEmitter.GenerateTry()` to emit boolean flag pattern
- [ ] Add integration tests for try-else behavior

**Estimated Effort**: 4-6 hours

---

#### 1.2 Loop Else Clause (v0.6)

**Description**: Add `else` clause support to `for` and `while` loops. The else clause executes only if the loop completes without `break`.

**Tasks**:
- [ ] Add `ElseBody` property to `ForStatement` AST node
- [ ] Add `ElseBody` property to `WhileStatement` AST node
- [ ] Update `Parser.ParseForStatement()` to parse `else:` block
- [ ] Update `Parser.ParseWhileStatement()` to parse `else:` block
- [ ] Update `RoslynEmitter.GenerateFor()` to emit boolean flag pattern
- [ ] Update `RoslynEmitter.GenerateWhile()` to emit boolean flag pattern
- [ ] Add integration tests for loop-else behavior

**Estimated Effort**: 6-8 hours

---

#### 1.3 Generic Functions (v0.4)

**Description**: Support generic functions like `def identity[T](x: T) -> T`.

**Tasks**:
- [ ] Add `TypeParameters` property to `FunctionDef` AST node
- [ ] Update `Parser.ParseFunctionDef()` to parse `def foo[T](x: T)` syntax
- [ ] Update `RoslynEmitter.GenerateMethod()` to emit type parameters
- [ ] Add semantic analysis for generic function type constraints
- [ ] Add integration tests for generic functions

**Estimated Effort**: 8-12 hours

---

#### 1.4 Type Constraints (v0.4)

**Description**: Support generic type constraints like `T: IComparable[T]`, `T: class`, `T: struct`, `T: new()`.

**Tasks**:
- [ ] Design AST representation for type constraints
- [ ] Update parser to handle constraint syntax on generic types
- [ ] Update `RoslynEmitter` to emit `where T : IInterface` clauses
- [ ] Add semantic validation for constraint satisfaction

**Estimated Effort**: 8-12 hours

---

#### 1.5 Star Unpacking (v0.2)

**Description**: Support star unpacking like `first, *rest = items`.

**Tasks**:
- [ ] Add `StarredExpression` AST node for `*rest` syntax
- [ ] Add `TokenType.Star` handling for unpacking context in Lexer
- [ ] Update `Parser` to parse `first, *rest = items` patterns
- [ ] Update `RoslynEmitter` to generate appropriate C# (LINQ Take/Skip pattern)
- [ ] Add integration tests

**Estimated Effort**: 6-8 hours

---

#### 1.6 User-Defined Function Overloading (v0.3)

**Description**: Allow multiple function definitions with the same name but different signatures.

**Current Blocker**: `NameResolver.cs:262` explicitly rejects duplicate function names.

**Tasks**:
- [ ] Update `NameResolver.ResolveFunctionDeclaration()` to allow multiple definitions with same name
- [ ] Create overload resolution mechanism similar to `BuiltinRegistry.GetFunctionOverloads()`
- [ ] Update `TypeChecker` to resolve user function overloads based on argument types
- [ ] Update `RoslynEmitter` to emit multiple C# methods (already supports via normal method generation)
- [ ] Add integration tests for function overloading

**Estimated Effort**: 8-12 hours

---

#### 1.7 Dunder Invocation Rules (v0.1)

**Description**: Enforce language spec rules that dunders cannot be called directly by user code (only via operators/built-in functions).

**Tasks**:
- [ ] Add semantic analysis to detect and reject explicit dunder calls (e.g., `x.__eq__(y)`)
- [ ] Allow `self.__dunder__()` calls within dunder method bodies only
- [ ] Allow `super().__dunder__()` calls within dunder method bodies only
- [ ] Reject dunder method capture (e.g., `func = self.__eq__`)
- [ ] Add error messages guiding users to use operators/built-in functions instead
- [ ] Add integration tests for dunder invocation validation

**Estimated Effort**: 6-8 hours

---

#### 1.8 Built-in Functions with Dunder Dispatch (v0.1)

**Description**: Implement missing standard library functions that dispatch to dunder methods.

**Tasks**:
- [ ] **Implement `hash(x)`**: Add `Hash()` function to `Sharpy.Core/Builtins/Exports.cs` that calls `IHashable.__Hash__` if implemented, else `.GetHashCode()`
- [ ] **Implement `id(x)`**: Add `Id()` function to `Sharpy.Core/Builtins/Exports.cs` that calls `IIdentifiable.__Id__` if implemented, else `RuntimeHelpers.GetHashCode()`
- [x] **`repr(x)` IMPLEMENTED**: `Repr()` exists in `Repr.cs`
- [x] **`str(x)` IMPLEMENTED**: Verify `Str()` calls `__str__` if defined
- [x] **`len(x)` IMPLEMENTED**: `Len()` exists in `Builtins/Exports.cs`
- [ ] Add integration tests for dunder dispatch in built-in functions

**Estimated Effort**: 2-4 hours

---

### PRIORITY 2: v0.9 Features

These features complete the v0.9 milestone.

#### 2.1 Nested Comprehensions

**Description**: Support multiple `for` clauses in comprehensions like `[(x, y) for x in range(3) for y in range(3)]`.

**Current Blocker**: `RoslynEmitter` throws `NotImplementedException` at line 1956.

**Tasks**:
- [ ] Update `RoslynEmitter.GenerateListComprehension()` to handle multiple `ForClause`
- [ ] Generate SelectMany LINQ pattern for nested iterations
- [ ] Add integration tests

**Estimated Effort**: 4-6 hours

---

#### 2.2 Tuple Unpacking in Comprehensions

**Description**: Support tuple unpacking in comprehension for clauses like `{k: v for k, v in items}`.

**Current Blocker**: `RoslynEmitter` throws `NotImplementedException` at line 1927.

**Tasks**:
- [ ] Update comprehension codegen to support tuple targets in for clause
- [ ] Add integration tests

**Estimated Effort**: 4-6 hours

---

#### 2.3 Walrus Operator (`:=`)

**Description**: Support assignment expressions like `if (match := pattern.search(text)) is not None:`.

**Tasks**:
- [ ] Add `TokenType.ColonEquals` for `:=` token
- [ ] Add `AssignmentExpression` AST node
- [ ] Update `Parser.ParseExpression()` to handle `:=`
- [ ] Update `RoslynEmitter` to generate C# inline assignment
- [ ] Add semantic analysis for walrus operator scope rules
- [ ] Add integration tests

**Estimated Effort**: 6-8 hours

---

#### 2.4 Properties (v0.9)

**Description**: Support property syntax like `property celsius(self) -> double:`.

**Tasks**:
- [ ] Add `TokenType.Property` keyword
- [ ] Add `PropertyDef` AST node with `get`/`set` accessors
- [ ] Update Parser to handle `property x: int` syntax
- [ ] Update `RoslynEmitter` to generate C# property syntax
- [ ] Add integration tests

**Estimated Effort**: 8-12 hours

---

### PRIORITY 3: v0.7-v1.0 Features (Future Work)

These features are planned for later versions and can be implemented after core features are complete.

#### 3.1 Pattern Matching (v0.7)

**Tasks**:
- [ ] Add `TokenType.Match`, `TokenType.Case` keywords
- [ ] Add `MatchStatement`, `CaseClause` AST nodes
- [ ] Add pattern AST nodes: `LiteralPattern`, `TypePattern`, `WildcardPattern`, `GuardPattern`
- [ ] Update Parser for match/case syntax
- [ ] Update `RoslynEmitter` to generate C# switch expressions
- [ ] Add integration tests

**Estimated Effort**: 16-24 hours

---

#### 3.2 Type Aliases (v0.8)

**Tasks**:
- [ ] Add `TokenType.Type` keyword
- [ ] Add `TypeAliasStatement` AST node
- [ ] Update Parser for `type Name = ExistingType` syntax
- [ ] Update semantic analysis to resolve type aliases
- [ ] Update `RoslynEmitter` to generate `using Name = Type;`
- [ ] Add integration tests

**Estimated Effort**: 8-12 hours

---

#### 3.3 Context Managers (v1.0)

**Tasks**:
- [ ] Update Parser to handle `with expr as name:` syntax (token exists)
- [ ] Add `WithStatement` AST node
- [ ] Update `RoslynEmitter` to generate C# `using` statement
- [ ] Add integration tests

**Estimated Effort**: 4-6 hours

---

#### 3.4 Async/Await (v1.0)

**Tasks**:
- [ ] Add `TokenType.Async`, `TokenType.Await` keywords
- [ ] Add `async` decorator support
- [ ] Add `AwaitExpression` AST node
- [ ] Update `RoslynEmitter` to generate `async`/`await` C#
- [ ] Add semantic analysis for async context
- [ ] Add integration tests

**Estimated Effort**: 12-16 hours

---

### PRIORITY 4: Standard Library Gaps

#### 4.1 Missing Built-in Functions

**Tasks**:
- [ ] **Implement `hash(x)`**: Add global `Hash()` function
- [ ] **Implement `id(x)`**: Add global `Id()` function

**Estimated Effort**: 2-4 hours

---

#### 4.2 Enum Properties

**Tasks**:
- [ ] **Enum `.name` property**: Add extension method or codegen support for `Color.RED.name`
- [ ] **Enum `.value` property**: Add extension method or codegen support for `Color.RED.value`
- [ ] **String enum static class**: Update codegen to emit static class pattern for string-valued enums

**Estimated Effort**: 4-6 hours

---

### PRIORITY 5: Decorator Alignment

**Tasks**:
- [ ] Decide: Keep `@sealed` or rename to `@final` per language spec
- [ ] Update documentation to match implementation OR update implementation to match spec
- [ ] Add `@final` as alias for `@sealed` if keeping both

**Estimated Effort**: 1-2 hours

---

### PRIORITY 6: Specification Alignment

#### 6.1 Type Casting Syntax

**Issue**: Language Reference specifies `cast[T](value)` syntax, but implementation uses `value as T`.

**Options**:
1. Implement `cast[T](value)` syntax alongside existing (more work)
2. Update Language Reference to document `value as T` syntax (easier)

**Decision Required**: Before implementation.

**Estimated Effort**: 2-4 hours (option 1) or 1 hour (option 2)

---

### PRIORITY 7: Test Coverage

#### 7.1 Create Missing Integration Tests

| Test File | Features to Cover | Priority |
|-----------|-------------------|----------|
| `ExceptionTests.cs` | try/except/finally, raise statement | HIGH |
| `StructTests.cs` | Struct definition, fields, methods, constructor | HIGH |
| `InterfaceTests.cs` | Interface definition, implementation | HIGH |
| `GenericTests.cs` | Generic class instantiation | HIGH |
| `SlicingTests.cs` | Slicing with step, negative indices | MEDIUM |
| `CollectionLiteralTests.cs` | Empty set `{/}`, comparison chaining | MEDIUM |
| `EnumTests.cs` | Integer enums, enum usage | MEDIUM |
| `DecoratorTests.cs` | @static, @override, @virtual, @abstract | MEDIUM |

**Estimated Effort**: 8-12 hours total

---

### PRIORITY 8: Documentation and Verification

#### 8.1 Remaining Verification Tasks

| Section | Status | Notes |
|---------|--------|-------|
| Default Parameter Evaluation | ⚠️ Needs test | Verify Python vs C# semantics |
| .NET Interop | ❌ Not audited | Test actual .NET type usage |

**Tasks**:
- [ ] Create test case for mutable default parameter behavior
- [ ] Document actual behavior (Python semantics or C# semantics)
- [ ] Create `.NET Interop` test file verifying:
  - Importing .NET types
  - Calling .NET methods
  - Using .NET types as base classes
  - LINQ extension methods with Sharpy collections

**Estimated Effort**: 4-6 hours

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

### Key Files Reference

| Component | File Path | Purpose |
|-----------|-----------|---------|
| Lexer tokens | `src/Sharpy.Compiler/Lexer/Token.cs` | All TokenTypes |
| AST nodes | `src/Sharpy.Compiler/Parser/Ast/Statement.cs` | Statement AST |
| AST expressions | `src/Sharpy.Compiler/Parser/Ast/Expression.cs` | Expression AST |
| Parser | `src/Sharpy.Compiler/Parser/Parser.cs` | Main parser |
| Type checker | `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Type validation |
| Name resolver | `src/Sharpy.Compiler/Semantic/NameResolver.cs` | Symbol resolution |
| Code generator | `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | C# generation |
| Type mapper | `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` | Type translation |
| Name mangler | `src/Sharpy.Compiler/CodeGen/NameMangler.cs` | Name transformation |
| Standard library | `src/Sharpy.Core/` | Runtime builtins |
| Integration tests | `src/Sharpy.Compiler.Tests/Integration/` | End-to-end tests |

---

## Audit History

| Audit | Date | Key Findings |
|-------|------|--------------|
| #1-3 | Dec 2025 | Initial keyword/AST verification |
| #4 | Dec 3, 2025 | Test coverage mapping, semantic analysis structure |
| #5 | Dec 3, 2025 | TokenType cross-check, AST properties |
| #6 | Dec 3, 2025 | Standard library builtins, integration tests |
| #7 | Dec 3, 2025 | Operator precedence, naming conventions, type casting discrepancy |
| #8 | Dec 4, 2025 | Comprehension scoping, dunder rules, type casting syntax, complete token inventory |
