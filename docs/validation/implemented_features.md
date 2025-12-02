# Sharpy Language Features - Implementation Status

This document provides a thorough inventory of all Sharpy language features currently implemented in the compiler codebase. Each feature is documented to enable close review of the implementation.

---

## Table of Contents

1. [Lexical Structure](#lexical-structure)
2. [Literals](#literals)
3. [Types and Type System](#types-and-type-system)
4. [Operators](#operators)
5. [Expressions](#expressions)
6. [Statements](#statements)
7. [Control Flow](#control-flow)
8. [Type Definitions](#type-definitions)
9. [Functions and Methods](#functions-and-methods)
10. [Decorators and Access Modifiers](#decorators-and-access-modifiers)
11. [Import System](#import-system)
12. [Semantic Analysis](#semantic-analysis)
13. [Code Generation](#code-generation)
14. [Standard Library (Sharpy.Core)](#standard-library-sharpycore)

---

## Lexical Structure

### Source File Handling
- **Files**: Lexer in `Lexer/Lexer.cs` (1661 lines)
- UTF-8 encoding support
- Line endings: LF (`\n`) and CRLF (`\r\n`) supported
- Source file extension: `.spy`

### Indentation-Based Syntax
- **Implementation**: `Lexer.MeasureIndentation()`, `NextToken()` indentation handling
- 4-space indentation enforced
- Tabs are not allowed (throws `LexerError`)
- `INDENT` and `DEDENT` tokens generated automatically
- Indentation stack maintained via `_indentStack`
- Blank and comment-only lines don't affect indentation

### Line Continuation
- **Explicit**: Backslash (`\`) at end of line
  - Validates no trailing whitespace after backslash
  - Error handling for backslash at EOF
- **Implicit**: Inside brackets `()`, `[]`, `{}`
  - Tracked via `_bracketDepth` counter
  - Newlines inside brackets are ignored

### Comments
- Single-line comments starting with `#`
- Comments extend to end of line
- Comment lines don't generate `NEWLINE` tokens

### Identifiers
- **Pattern**: `(letter | '_') (letter | digit | '_')*`
- Case-sensitive
- Keywords are reserved
- Backtick-delimited literal names (`` `identifier` ``) to bypass transformations

---

## Literals

### Integer Literals
- **AST Node**: `IntegerLiteral` in `Parser/Ast/Expression.cs`
- **Lexer**: `ReadNumber()` method
- Decimal integers supported
- Underscore separators supported (e.g., `1_000_000`)
- Suffix support: `L`, `U`, `UL` for type specification
- **Code Generation**: `GenerateIntegerLiteral()` in `RoslynEmitter.cs`

### Float Literals
- **AST Node**: `FloatLiteral` in `Parser/Ast/Expression.cs`
- Decimal floats with required digit before/after decimal point
- Underscore separators supported
- Suffix support: `f`, `F`, `d`, `D`, `m`, `M`
- **Code Generation**: `GenerateFloatLiteral()` in `RoslynEmitter.cs`

### String Literals
- **AST Node**: `StringLiteral` in `Parser/Ast/Expression.cs`
- **Lexer**: `ReadString()` method
- Single-quoted (`'...'`) and double-quoted (`"..."`) strings
- Triple-quoted multi-line strings (`"""..."""` and `'''...'''`)
- Escape sequences:
  - `\\`, `\'`, `\"`, `\n`, `\r`, `\t`, `\b`, `\f`, `\0`
  - `\xHH` (hex byte)
  - `\uHHHH` (Unicode 16-bit)
  - `\UHHHHHHHH` (Unicode 32-bit)

### Raw Strings
- **Token Type**: `TokenType.RawString`
- **Lexer**: `ReadRawString()` method
- Prefix: `r"..."` or `r'...'`
- Backslashes are not processed as escapes

### F-Strings (Formatted String Literals)
- **AST Node**: `FStringLiteral` with `FStringPart` elements
- **Token Types**: `FStringStart`, `FStringText`, `FStringExprStart`, `FStringExprEnd`, `FStringFormatSpec`, `FStringEnd`
- **Lexer**: `ReadFStringStart()`, `NextFStringToken()`, F-string context stack
- Prefix: `f"..."` or `f'...'`
- Interpolation: `{expression}` syntax
- Format specifiers: `{value:.2f}` syntax supported
- Nested braces: `{{` and `}}` for literal braces
- **Code Generation**: `GenerateFString()` → C# interpolated strings

### Boolean Literals
- **AST Node**: `BooleanLiteral` in `Parser/Ast/Expression.cs`
- `True` and `False` keywords
- **Token Types**: `TokenType.True`, `TokenType.False`
- **Code Generation**: Maps to C# `true`/`false`

### None Literal
- **AST Node**: `NoneLiteral` in `Parser/Ast/Expression.cs`
- `None` keyword
- **Token Type**: `TokenType.None`
- **Code Generation**: Maps to C# `null`

### Ellipsis Literal
- **AST Node**: `EllipsisLiteral` in `Parser/Ast/Expression.cs`
- `...` token
- **Token Type**: `TokenType.Ellipsis`
- **Code Generation**: `GenerateEllipsisLiteral()` → `throw new NotImplementedException()`
- Used as placeholder in function/class bodies

---

## Types and Type System

### Primitive Types
- **Implementation**: `PrimitiveCatalog.cs`, `BuiltinRegistry.cs`
- **Supported Types**:
  - `int` → `System.Int32`
  - `long` → `System.Int64`
  - `float` → `System.Single`
  - `double` → `System.Double`
  - `bool` → `System.Boolean`
  - `str` → `System.String` (C# `string`)
  - `byte`, `sbyte`, `short`, `ushort`, `uint`, `ulong`, `char` (extended primitives)
  - `decimal` for high-precision decimals
  - `object` → `System.Object`
  - `None` → `void` (for return types)

### Type Annotations
- **AST Node**: `TypeAnnotation` in `Parser/Ast/Types.cs`
- **Parser**: `ParseTypeAnnotation()` method
- Syntax: `variable: type`
- Nullable types: `T?` syntax
- Generic types: `list[int]`, `dict[str, int]`
- Function types: `(int, str) -> bool`
- **Resolution**: `TypeResolver.ResolveTypeAnnotation()`

### Type Inference
- `auto` keyword for inferred types
- **Code Generation**: Maps to C# `var`
- **Semantic**: `SemanticType.Unknown` until inferred

### Generic Types
- **AST**: `TypeAnnotation.TypeArguments` list
- **Semantic**: `GenericType` record
- Syntax: `ClassName[T, U]`
- Type parameter validation in `TypeResolver`

### Nullable Types
- **AST**: `TypeAnnotation.IsNullable`
- **Semantic**: `NullableType` wrapping `UnderlyingType`
- Syntax: `T?` (e.g., `str?`, `int?`)
- **Code Generation**: `NullableType(baseType)` in Roslyn

### Collection Types
- **Sharpy.Core Collections**:
  - `list[T]` → `global::Sharpy.Core.List<T>`
  - `dict[K, V]` → `global::Sharpy.Core.Dict<K, V>`
  - `set[T]` → `global::Sharpy.Core.Set<T>`
- **Type Mapping**: `TypeMapper.cs` with `_builtinTypeMap`
- `tuple[T1, T2, ...]` → `System.ValueTuple<T1, T2, ...>`

### Semantic Type Hierarchy
- **Base**: `SemanticType` abstract record
- **Derived Types**:
  - `UnknownType` - error recovery
  - `VoidType` - None/void
  - `BuiltinType` - primitives
  - `GenericType` - parameterized types
  - `UserDefinedType` - classes, structs, interfaces
  - `NullableType` - nullable wrapper
  - `FunctionType` - callable types

---

## Operators

### Arithmetic Operators
- **Binary**: `+`, `-`, `*`, `/`, `//` (floor division), `%`, `**` (power)
- **Unary**: `+`, `-`
- **Token Types**: `Plus`, `Minus`, `Star`, `Slash`, `DoubleSlash`, `Percent`, `DoubleStar`
- **Code Generation**:
  - Standard operators map directly to C#
  - `**` → `Math.Pow(x, y)`
  - `//` → `(int)(x / y)`

### Comparison Operators
- `==`, `!=`, `<`, `>`, `<=`, `>=`
- **Token Types**: `Equal`, `NotEqual`, `Less`, `Greater`, `LessEqual`, `GreaterEqual`
- **Comparison Chaining**: `a < b < c` → `a < b && b < c`
  - **AST Node**: `ComparisonChain`
  - **Code Generation**: `GenerateComparisonChain()`

### Logical Operators
- `and`, `or`, `not` (keyword-based)
- **Token Types**: `And`, `Or`, `Not`
- Short-circuit evaluation
- **Code Generation**: `&&`, `||`, `!` in C#

### Bitwise Operators
- `&`, `|`, `^`, `~`, `<<`, `>>`
- **Token Types**: `Ampersand`, `Pipe`, `Caret`, `Tilde`, `LeftShift`, `RightShift`
- **Code Generation**: Direct mapping to C#

### Membership Operators
- `in`, `not in`
- **Token Type**: `In`
- **Binary Operators**: `BinaryOperator.In`, `BinaryOperator.NotIn`
- **Code Generation**: `obj.__Contains__(value)` / `!obj.__Contains__(value)`

### Identity Operators
- `is`, `is not`
- **Token Type**: `Is`
- **Binary Operators**: `BinaryOperator.Is`, `BinaryOperator.IsNot`
- **Code Generation**:
  - `x is None` → `x == null`
  - `x is not None` → `x != null`
  - General case → `object.ReferenceEquals(x, y)`

### Null-Coalescing Operator
- `??` operator
- **Token Type**: `NullCoalesce`
- **Binary Operator**: `BinaryOperator.NullCoalesce`
- **Code Generation**: C# `??` operator

### Null-Conditional Access
- `?.` operator
- **Token Type**: `NullConditional`
- **AST**: `MemberAccess.IsNullConditional`
- **Code Generation**: `ConditionalAccessExpression` in Roslyn

### Assignment Operators
- Simple: `=`
- Augmented: `+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=`
- Bitwise augmented: `&=`, `|=`, `^=`, `<<=`, `>>=`
- **Token Types**: `Assign`, `PlusAssign`, etc.
- **AST**: `AssignmentOperator` enum
- **Code Generation**: `GenerateAssignment()` handles all variants

---

## Expressions

### Primary Expressions
- **Identifier**: `Identifier` AST node
- **Literals**: All literal types (see Literals section)
- **Parenthesized**: `Parenthesized` AST node

### Collection Literals
- **List**: `[elem1, elem2, ...]` → `ListLiteral`
- **Dict**: `{key1: val1, key2: val2}` → `DictLiteral` with `DictEntry` items
- **Set**: `{elem1, elem2, ...}` → `SetLiteral`
  - Empty set: `{/}` special syntax
- **Tuple**: `(elem1, elem2)` or `elem1, elem2` → `TupleLiteral`
- **Code Generation**: Creates `new global::Sharpy.Core.List<T> { ... }` etc.

### Member Access
- **AST Node**: `MemberAccess`
- Standard: `obj.member`
- Null-conditional: `obj?.member`
- **Code Generation**: `GenerateMemberAccess()`

### Index Access
- **AST Node**: `IndexAccess`
- Syntax: `obj[index]`
- **Code Generation**: `GenerateIndexAccess()` → `ElementAccessExpression`

### Slice Access
- **AST Node**: `SliceAccess`
- Syntax: `obj[start:stop:step]`
- All components optional
- Negative indices supported
- **Code Generation**: `GenerateSliceAccess()` → `Sharpy.Core.Slice(obj, start, stop, step)`

### Function Calls
- **AST Node**: `FunctionCall`
- Positional arguments: `func(arg1, arg2)`
- **Keyword Arguments**: `KeywordArgument` support in AST
- **Code Generation**: `GenerateCall()`
  - Builtin functions prefixed with `global::Sharpy.Core.Exports.`
  - Name mangling to PascalCase

### Comprehensions
- **List Comprehension**: `[expr for x in iterable if condition]`
  - **AST Node**: `ListComprehension`
  - **Clauses**: `ForClause`, `IfClause`
  - **Code Generation**: LINQ chain `.Where(...).Select(...).ToList()`

- **Set Comprehension**: `{expr for x in iterable if condition}`
  - **AST Node**: `SetComprehension`
  - **Code Generation**: `.Where(...).Select(...).ToHashSet()`

- **Dict Comprehension**: `{key: value for x in iterable if condition}`
  - **AST Node**: `DictComprehension`
  - **Code Generation**: `.Where(...).ToDictionary(x => key, x => value)`

### Unary Operations
- **AST Node**: `UnaryOp`
- Operators: `+`, `-`, `not`, `~`
- **Enum**: `UnaryOperator`
- **Code Generation**: `GenerateUnaryOp()`

### Binary Operations
- **AST Node**: `BinaryOp`
- Full operator set (see Operators section)
- **Enum**: `BinaryOperator`
- **Code Generation**: `GenerateBinaryOp()` with special cases for power, floor division, membership, identity

### Conditional Expression (Ternary)
- **AST Node**: `ConditionalExpression`
- Syntax: `true_value if condition else false_value`
- **Code Generation**: `GenerateConditionalExpression()` → C# `condition ? trueVal : falseVal`

### Lambda Expression
- **AST Node**: `LambdaExpression`
- Syntax: `lambda x, y: x + y`
- Parameters without type annotations (inferred)
- Single expression body only
- **Code Generation**: `GenerateLambdaExpression()` → C# `(x, y) => x + y`

### Type Cast
- **AST Node**: `TypeCast`
- Syntax: `value as Type`
- **Code Generation**: `GenerateTypeCast()` → C# `(Type)value`

### Type Check
- **AST Node**: `TypeCheck`
- Syntax: `value is Type`
- **Code Generation**: `GenerateTypeCheck()` → C# `value is Type`

---

## Statements

### Expression Statement
- **AST Node**: `ExpressionStatement`
- Any expression used as a statement

### Variable Declaration
- **AST Node**: `VariableDeclaration`
- Syntax: `x: int = value` or `x: int`
- Optional initializer
- `const` modifier supported
- **Code Generation**: `GenerateVariableDeclaration()`

### Assignment Statement
- **AST Node**: `Assignment`
- Targets: identifier, index access, member access, tuple unpacking
- All assignment operators supported
- Variable redefinition handling with version tracking

### Tuple Unpacking
- **Parser**: `ParseSimpleStatement()` handles comma-separated targets
- Syntax: `x, y = 1, 2` or `a, b, c = some_tuple`
- **Code Generation**: C# deconstruction `var (x, y) = expr`

### Pass Statement
- **AST Node**: `PassStatement`
- **Token Type**: `Pass`
- **Code Generation**: `EmptyStatement()`

### Break Statement
- **AST Node**: `BreakStatement`
- **Token Type**: `Break`
- **Code Generation**: C# `break;`

### Continue Statement
- **AST Node**: `ContinueStatement`
- **Token Type**: `Continue`
- **Code Generation**: C# `continue;`

### Return Statement
- **AST Node**: `ReturnStatement`
- Syntax: `return` or `return value`
- **Code Generation**: `GenerateReturn()`

### Raise Statement
- **AST Node**: `RaiseStatement`
- Syntax: `raise` (re-raise) or `raise Exception`
- **Code Generation**: `GenerateRaise()` → C# `throw` or `throw exception`

### Assert Statement
- **AST Node**: `AssertStatement`
- Syntax: `assert condition` or `assert condition, message`
- **Code Generation**: `GenerateAssert()` → `System.Diagnostics.Debug.Assert(condition, message)`

---

## Control Flow

### If Statement
- **AST Node**: `IfStatement`
- Components:
  - `Test`: condition expression
  - `ThenBody`: true branch statements
  - `ElifClauses`: list of `ElifClause` (test + body)
  - `ElseBody`: optional else branch
- **Code Generation**: `GenerateIf()` with nested if-else structure

### While Statement
- **AST Node**: `WhileStatement`
- Syntax: `while condition:`
- **Code Generation**: `GenerateWhile()`

### For Statement
- **AST Node**: `ForStatement`
- Syntax: `for item in iterable:`
- **Target types**: single identifier, tuple unpacking
- **Code Generation**: `GenerateFor()`
  - Single variable: `foreach (var item in iterable)`
  - Tuple unpacking: `foreach (var (x, y) in iterable)`

### Try-Except-Finally
- **AST Node**: `TryStatement`
- Components:
  - `Body`: try block statements
  - `Handlers`: list of `ExceptHandler`
  - `FinallyBody`: optional finally block
- **ExceptHandler**:
  - `ExceptionType`: optional type annotation
  - `Name`: optional binding name (`except Exception as e`)
  - `Body`: handler statements
- **Code Generation**: `GenerateTry()` → C# try-catch-finally

---

## Type Definitions

### Class Definition
- **AST Node**: `ClassDef`
- **Parser**: `ParseClassDef()`
- Components:
  - `Name`: class name
  - `TypeParameters`: generic parameters `[T, U]`
  - `BaseClasses`: inheritance list `(ParentClass, Interface1)`
  - `Body`: class members
  - `Decorators`: applied decorators
  - `DocString`: triple-quoted docstring
- **Code Generation**: `GenerateClassDeclaration()` → C# class

### Struct Definition
- **AST Node**: `StructDef`
- **Parser**: `ParseStructDef()`
- Same structure as ClassDef
- Structs can only implement interfaces (no inheritance)
- **Code Generation**: `GenerateStructDeclaration()` → C# struct

### Interface Definition
- **AST Node**: `InterfaceDef`
- **Parser**: `ParseInterfaceDef()`
- Components:
  - `Name`, `TypeParameters`, `BaseInterfaces`, `Body`, `DocString`
- Methods have no body (just declarations)
- **Code Generation**: `GenerateInterfaceDeclaration()`

### Enum Definition
- **AST Node**: `EnumDef`
- **Parser**: `ParseEnumDef()`
- **EnumMember**: name and optional explicit value
- Simple enums only (no complex discriminated unions)
- **Code Generation**: `GenerateEnumDeclaration()` → C# enum

### Fields
- **Variable declarations** in class/struct body
- **Code Generation**: `GenerateField()`
  - PascalCase naming for public fields
  - Optional initializers
  - `const` modifier support

### Constructors
- `__init__` method → constructor
- **Code Generation**: `GenerateConstructor()`
- `self.field = value` → `this.Field = value`
- Skips `self` parameter in parameter list

---

## Functions and Methods

### Function Definition
- **AST Node**: `FunctionDef`
- **Parser**: `ParseFunctionDef()`
- Components:
  - `Name`: function name
  - `Parameters`: list of `Parameter`
  - `ReturnType`: optional type annotation
  - `Body`: function statements
  - `Decorators`: applied decorators
  - `DocString`: triple-quoted docstring

### Parameters
- **AST Node**: `Parameter`
- Components:
  - `Name`: parameter name
  - `Type`: optional type annotation
  - `DefaultValue`: optional default expression
- **Validation**: Non-default parameters cannot follow default parameters

### Method Definitions
- Instance methods: first parameter is `self`
- Class methods: `@staticmethod` decorator (no `self`)
- **Code Generation**: `GenerateClassMethod()`
  - Skips `self` and `cls` parameters

### Dunder Methods (Magic Methods)
- **NameMangler**: Maps dunder methods to C# equivalents
- **Special Mappings**:
  - `__init__` → constructor
  - `__str__`, `__repr__` → `ToString()` (override)
  - `__eq__` → `Equals()` (override)
  - `__hash__` → `GetHashCode()` (override)
  - `__len__` → `Length`
  - `__contains__` → `Contains()`
  - `__iter__` → `GetEnumerator()`
  - `__getitem__`, `__setitem__` → indexer support
  - `__bool__` → `ToBoolean()`

### Operator Overloading via Dunder Methods
- **Code Generation**: `TryGenerateOperatorOverload()`, `GenerateBinaryOperator()`, `GenerateUnaryOperator()`, `GenerateComparisonOperator()`
- **Supported Operators**:
  - Arithmetic: `__add__`, `__sub__`, `__mul__`, `__div__`, `__mod__`
  - Bitwise: `__and__`, `__or__`, `__xor__`, `__lshift__`, `__rshift__`
  - Comparison: `__eq__`, `__ne__`, `__lt__`, `__le__`, `__gt__`, `__ge__`
  - Unary: `__neg__`, `__pos__`, `__invert__`
- **Complementary Operators**: Auto-generates `!=` when only `__eq__` defined (and vice versa)

---

## Decorators and Access Modifiers

### Decorator Syntax
- **AST Node**: `Decorator`
- Syntax: `@decorator_name`
- Applied to functions, classes, structs
- Simple identifier only (no arguments or dotted names)

### Access Modifiers
- **Decorators**: `@public`, `@private`, `@protected`, `@internal`
- **Default**: `public` if no access modifier specified
- **Code Generation**: `GenerateModifiersFromDecorators()`, `GenerateTypeModifiersFromDecorators()`, `GenerateMethodModifiersFromDecorators()`

### Other Modifiers
- `@static` / `@staticmethod` → C# `static`
- `@abstract` / `@abstractmethod` → C# `abstract`
- `@virtual` → C# `virtual`
- `@override` → C# `override`
- `@sealed` → C# `sealed` (for types)

---

## Import System

### Import Statement
- **AST Node**: `ImportStatement`
- **Parser**: `ParseImportStatement()`
- Syntax: `import module` or `import module as alias`
- **ImportAlias**: name and optional alias
- **Code Generation**: `GenerateImportUsings()` → C# `using` directives

### From-Import Statement
- **AST Node**: `FromImportStatement`
- **Parser**: `ParseFromImportStatement()`
- Syntax: `from module import name1, name2` or `from module import *`
- **ImportAll**: boolean flag for `import *`
- **Code Generation**: `GenerateFromImportUsings()` → C# `using` directive

### Module Name Conversion
- `ConvertModuleNameToNamespace()`: Python module names → C# namespaces
- `system.io` → `System.IO`
- `my_module.sub_module` → `MyModule.SubModule`
- Common acronyms uppercased: `io`, `ui`, `xml`, `html`, `api`, `sql`, `http`, etc.

---

## Semantic Analysis

### Name Resolution
- **File**: `Semantic/NameResolver.cs`
- **Two-pass process**:
  1. `ResolveDeclarations()`: Register all type/function declarations
  2. `ResolveInheritance()`: Resolve base classes and interfaces
- **Symbol Registration**: Classes, structs, interfaces, enums, functions, constants
- **Scope Management**: Enter/exit scopes for nested definitions

### Type Resolution
- **File**: `Semantic/TypeResolver.cs`
- `ResolveTypeAnnotation()`: Convert AST type annotations to semantic types
- Builtin type lookup
- Generic type resolution with type argument validation
- Caching via `SemanticInfo`

### Type Checking
- **File**: `Semantic/TypeChecker.cs` (1828 lines)
- Statement and expression type checking
- **Validators**:
  - `ControlFlowValidator`: Return path analysis
  - `AccessValidator`: Access level checking
  - `OperatorValidator`: Operator type compatibility
- **Type Narrowing**: Tracks narrowed types in `_narrowedTypes` dictionary
  - `is None` checks narrow to `None`
  - `is not None` removes `None` from nullable types
  - `isinstance()` checks narrow to specific types

### Symbol Table
- **File**: `Semantic/SymbolTable.cs`
- **Scoped Lookups**: `EnterScope()`, `ExitScope()`, `Lookup()`
- **Symbol Types**: `VariableSymbol`, `FunctionSymbol`, `TypeSymbol`, `ModuleSymbol`, `PropertySymbol`
- Function overload tracking

### Builtin Registry
- **File**: `Semantic/BuiltinRegistry.cs`
- Registers primitive types from `PrimitiveCatalog`
- Collection types: `list`, `dict`, `set`
- Builtin functions loaded via `CachedModuleDiscovery`
- Reflection-based discovery of `Sharpy.Core.Exports`

---

## Code Generation

### Roslyn Emitter
- **File**: `CodeGen/RoslynEmitter.cs` (2552 lines)
- Generates C# code using Roslyn `SyntaxFactory`
- Produces `CompilationUnitSyntax` from Sharpy AST

### Name Mangling
- **File**: `CodeGen/NameMangler.cs`
- **Transformations**:
  - `snake_case` → `PascalCase` for methods/functions
  - `snake_case` → `camelCase` for variables/parameters
  - `CAPS_SNAKE_CASE` preserved for constants
  - Type names preserved as-is
  - Interface names preserved (I prefix pattern)
- **Dunder Method Handling**: Preserves `__name__` format for operators, maps overrides
- **C# Keyword Escaping**: Adds `@` prefix when needed

### Type Mapping
- **File**: `CodeGen/TypeMapper.cs`
- Maps Sharpy types to C# types
- Handles generics, nullable types, function types, tuple types
- Element type inference for collections

### Namespace Generation
- File-based: Directory structure → namespace hierarchy
- Project-based: Uses `ProjectNamespace` and `ProjectRootPath`
- Common directories filtered (`src`, `lib`)

### Module Wrapper
- Module code wrapped in `public static class __Module__`
- Module-level statements:
  - Without `main()`: Wrapped in generated `Main()`
  - With `main()`: User's main becomes entry point

### Variable Redefinition Handling
- Version tracking via `_variableVersions` dictionary
- Redefinitions get versioned names: `x`, `x_1`, `x_2`
- Scope-based tracking reset on function entry

### Assembly Compilation
- **File**: `AssemblyCompiler.cs`
- Compiles generated C# to .NET assembly
- Debug/Release configuration support
- PDB generation for debugging
- Runtime config and deps file generation

### Code Validation
- **File**: `CodeGen/CodeValidator.cs`
- Validates generated C# syntax trees
- Checks for syntax errors, empty names, duplicate members

---

## Standard Library (Sharpy.Core)

### Builtin Functions
- Discovered via reflection from `Sharpy.Core.Exports`
- **Common builtins** (registered in `BuiltinRegistry`):
  - `print()`, `len()`, `range()`, `enumerate()`, `zip()`
  - `map()`, `filter()`, `sorted()`, `reversed()`
  - `abs()`, `min()`, `max()`, `sum()`
  - Type conversions: `int()`, `str()`, `float()`, `bool()`, `list()`, `dict()`, `set()`
  - `input()`, `open()`, `type()`, `isinstance()`

### Collection Types
- **`Sharpy.Core.List<T>`**: Python-like list
  - Negative indexing
  - Slicing support
  - Python-compatible methods
- **`Sharpy.Core.Dict<K, V>`**: Python-like dictionary
- **`Sharpy.Core.Set<T>`**: Python-like set
- All collections support Python semantics while being .NET-native

### Type Interop
- Uses `global::` prefix to avoid namespace conflicts
- Sharpy.Core types preferred for Python compatibility
- Direct C# interop for primitives (`string` instead of custom `Str`)

---

## Compiler Driver

### Main Compiler Pipeline
- **File**: `Compiler.cs` (512 lines)
- **Class**: `Compiler` - Main driver orchestrating the compilation pipeline
- **Five-phase pipeline**:
  1. **Lexical Analysis**: Tokenizes source via `Lexer.Lexer`
  2. **Syntax Analysis**: Parses tokens to AST via `Parser.Parser`
  3. **Semantic Analysis**: Name resolution → Type resolution → Type checking
  4. **Code Generation**: AST → C# via `RoslynEmitter`
  5. **Assembly Compilation**: C# → .NET assembly via `AssemblyCompiler`

### Compilation Modes
- **Single File Compilation**: `Compile(string sourceCode, string filePath)` method
  - Returns `CompilationResult` with Module, SymbolTable, SemanticInfo, and generated C# code
- **Project Compilation**: `CompileProject(ProjectConfig projectConfig)` method
  - Handles multi-file projects from `.spyproj` files
  - Resolves imports across files
  - Shared symbol table across all modules
  - Returns `ProjectCompilationResult` with output assembly path

### Compiler Options
- **`CompilerOptions` class**:
  - `ModulePaths`: Paths to search for module assemblies
  - `References`: Paths to .NET assemblies to reference
- **Module Registry**: `ModuleRegistry` manages third-party .NET module loading

### Result Types
- **`CompilationResult`**: Single-file compilation result
  - `Success`, `Errors`, `Module`, `SymbolTable`, `SemanticInfo`, `GeneratedCSharpCode`, `Metrics`
- **`ProjectCompilationResult`**: Multi-file project result
  - `Success`, `Errors`, `Warnings`, `OutputAssemblyPath`, `GeneratedCSharpFiles`, `Metrics`

---

## Diagnostics & Compilation Metrics

### Phase Timing
- **File**: `Diagnostics/CompilationMetrics.cs` (409 lines)
- **`CompilationMetrics` class**: Tracks per-file compilation phases
  - `StartPhase(string phaseName)` / `EndPhase()` methods
  - Tracks: Duration, Memory before/after, Memory delta
- **`PhaseMetric` class**: Individual phase timing data
  - Duration calculated as `EndTime - StartTime`
  - Memory delta: `MemoryAfter - MemoryBefore`

### Project-Level Metrics
- **`ProjectCompilationMetrics` class**: Aggregates metrics across files
  - `AddFileMetrics()` / `SetAssemblyMetrics()` methods
  - `AggregatePhaseMetrics`: Groups by phase across all files
  - `TotalDuration` / `TotalMemoryDelta` properties

### Output Formats
- **Text Format**: `FormatAsText()` - Human-readable table
- **JSON Format**: `FormatAsJson()` - Machine-readable with all details
  - Includes: timestamp, compiler_version, phases breakdown, per-file metrics

---

## Module Discovery & Caching

### Cached Module Discovery
- **File**: `Discovery/CachedModuleDiscovery.cs` (165 lines)
- **Purpose**: Discover and cache function overloads from .NET assemblies
- **Thread-safe**: Uses `ConcurrentDictionary` for loaded indices
- **Cache Key**: `{assemblyName}-{version}-{hash}.json.gz`

### Assembly Identity
- **File**: `Discovery/Caching/AssemblyIdentity.cs` (88 lines)
- **Properties**: `Name`, `Version`, `ContentHash`, `FilePath`
- **Hash Generation**: SHA256 of assembly file content
- **Cache Key Format**: `{name}-{version}-{hash12chars}.json.gz`

### Overload Index
- **File**: `Discovery/Caching/OverloadIndex.cs` (66 lines)
- **Data Structures**:
  - `OverloadIndex`: Top-level index with `AssemblyIdentity` and `Modules` dictionary
  - `ModuleOverloads`: Functions grouped by module name
  - `FunctionSignature`: Name, Parameters, ReturnType, MethodToken
  - `ParameterSignature`: Name, Type, HasDefault, DefaultValue, IsVariadic
  - `TypeSignature`: Name, IsGeneric, TypeArguments, ClrTypeName

### Overload Index Builder
- **File**: `Discovery/Caching/OverloadIndexBuilder.cs` (224 lines)
- **Discovery Method**: Reflects on `public static class Exports` in assemblies
- **Function Filtering**:
  - Excludes methods starting with `_`, `get_`, `set_`
  - Excludes special names and generic method definitions
  - Excludes type constructors (`Bool`, `Int`, `Str`, etc.)
- **Name Conversion**: PascalCase → snake_case via regex

### Overload Index Cache
- **File**: `Discovery/Caching/OverloadIndexCache.cs` (210 lines)
- **Cache Location**: `~/.sharpy/cache/overload-index/`
- **Format**: GZip-compressed JSON
- **Cache Cleanup**: Deletes old versions after 7 days
- **Methods**: `TryLoad()`, `Save()`, `ClearAll()`, `GetInfo()`

### CLR Type Mapper (Discovery)
- **File**: `Discovery/TypeMapper.cs` (160 lines)
- **Maps CLR types to Sharpy SemanticType**:
  - Primitives: `int` → `SemanticType.Int`, etc.
  - Arrays: `T[]` → `list[T]`
  - Nullable value types: `T?` → `NullableType`
  - Generic collections: `List<T>` → `list[T]`, `Dictionary<K,V>` → `dict[K,V]`
  - Tuples: `ValueTuple<...>` → `TupleType`
- **Thread-safe caching**: `ConcurrentDictionary<Type, SemanticType>`

---

## Logging System

### Logger Interface
- **File**: `Logging/ICompilerLogger.cs` (67 lines)
- **Methods**:
  - `LogTokenRead()`: Lexer token events
  - `LogIndentChange()`: Indentation level changes
  - `LogParseEnter()` / `LogParseExit()`: Parser rule tracking
  - `LogError()`, `LogWarning()`, `LogInfo()`, `LogDebug()`, `LogTrace()`
  - `LogMetrics()`: Compilation metrics output
  - `IsEnabled(CompilerLogLevel)`: Level check

### Log Levels
- **`CompilerLogLevel` enum**:
  - `None` (0), `Error` (1), `Warning` (2), `Info` (3), `Debug` (4), `Trace` (5)

### Console Logger
- **File**: `Logging/ConsoleCompilerLogger.cs` (97 lines)
- **Output**: Formatted console output with level prefixes
- **Token/Parse Events**: Only at Trace level
- **Configurable**: Minimum log level, custom output streams

### Null Logger
- **File**: `Logging/NullLogger.cs` (44 lines)
- **Pattern**: Null object pattern (all methods are no-ops)
- **Performance**: `MethodImpl(AggressiveInlining)` for zero overhead
- **Singleton**: `NullLogger.Instance`

---

## Error Handling

### Lexer Errors
- **File**: `Lexer/LexerError.cs` (17 lines)
- **Exception**: `LexerError : Exception`
- **Properties**: `Line`, `Column`
- **Message Format**: `"Lexer error at line {line}, column {column}: {message}"`

### Parser Errors
- **File**: `Parser/ParserError.cs` (17 lines)
- **Exception**: `ParserError : Exception`
- **Properties**: `Line`, `Column`
- **Message Format**: `"Parser error at line {line}, column {column}: {message}"`

### Semantic Errors
- **File**: `Semantic/SemanticError.cs` (30 lines)
- **Exception**: `SemanticError : Exception`
- **Properties**: `Line?`, `Column?` (nullable)
- **Flexible Formatting**: With/without location info

---

## AST Infrastructure

### Base Node
- **File**: `Parser/Ast/Node.cs` (23 lines)
- **Abstract Record**: `Node` with location info
  - `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`
- **Module Record**: Root node with `Body` (List<Statement>) and `DocString`

### AST Dumper
- **File**: `Parser/AstDumper.cs` (773 lines)
- **Purpose**: Human-readable tree format for debugging
- **Output Format**: Tree with `├─`, `└─`, `│` connectors
- **Coverage**: All AST node types (statements, expressions, types)
- **Method**: `Dump(Module module)` → string

---

## Semantic Validators

### Access Validator
- **File**: `Semantic/AccessValidator.cs` (136 lines)
- **Purpose**: Validate member access based on Python-style naming
- **Access Levels** (derived from name):
  - `__name` (double underscore, not dunder) → Private
  - `_name` (single underscore) → Protected
  - `name` → Public
- **Hierarchy Check**: `IsInHierarchy()` for protected access validation
- **Context**: `EnterClass()` / `ExitClass()` for tracking current class

### Control Flow Validator
- **File**: `Semantic/ControlFlowValidator.cs` (221 lines)
- **Purpose**: Validate control flow correctness
- **Validations**:
  - Unreachable code detection
  - All code paths return for non-void functions
  - `break`/`continue` only inside loops
- **Loop Tracking**: `_loopDepth` counter for nested loops
- **Branch Analysis**: If/elif/else all-paths-return checking
- **Try/Except/Finally**: Complex return path analysis

### Operator Signature Validator
- **File**: `Semantic/OperatorSignatureValidator.cs` (169 lines)
- **Purpose**: Validate dunder operator method signatures
- **Operator Categories**:
  - Binary arithmetic: `__add__`, `__sub__`, `__mul__`, `__truediv__`, etc.
  - Binary bitwise: `__and__`, `__or__`, `__xor__`, `__lshift__`, `__rshift__`
  - In-place: `__iadd__`, `__isub__`, etc.
  - Comparison: `__eq__`, `__ne__`, `__lt__`, `__le__`, `__gt__`, `__ge__`
  - Unary: `__pos__`, `__neg__`, `__invert__`
- **Validations**:
  - Unary operators: exactly 1 parameter (`self`)
  - Binary operators: exactly 2 parameters (`self`, `other`)
  - Comparison operators must return `bool`
  - Non-comparison operators must not return void

### Operator Validator
- **File**: `Semantic/OperatorValidator.cs` (1053 lines)
- **Purpose**: Validate operator usage and resolve result types
- **Dual Support**: Sharpy dunder methods AND CLR operator overloads
- **Caching**: Per-operator-combo type caching (not thread-safe)
- **Special Cases**:
  - Logical `and`/`or` always return `bool`
  - Membership `in`/`not in` always return `bool`
  - Identity `is`/`is not` always return `bool`
- **Operator-to-Dunder Mapping**: Comprehensive map from operators to `__xxx__` methods
- **CLR Operator Mapping**: Maps to `op_Addition`, `op_Subtraction`, etc.

---

## Primitive Type System

### Primitive Catalog
- **File**: `Semantic/PrimitiveCatalog.cs` (339 lines)
- **Purpose**: Exhaustive registry of Sharpy primitive types
- **Thread-safe**: Uses `FrozenDictionary` for immutable lookups

### Registered Primitives
- **Signed Integers**: `sbyte` (8), `short` (16), `int` (32), `long` (64)
- **Unsigned Integers**: `byte` (8), `ushort` (16), `uint` (32), `ulong` (64)
- **Floating Point**: `float` (32), `double` (64), `decimal` (128)
- **Non-Numeric**: `bool`, `char`, `str`/`string` (alias), `None`/`void` (alias)

### Type Information
- **`PrimitiveInfo` record**: `SharpyName`, `CSharpName`, `ClrType`, `NumericKind`, `SizeInBits`, `IsSigned`
- **`NumericKind` enum**: `None`, `SignedInteger`, `UnsignedInteger`, `FloatingPoint`, `Decimal`

### Query Methods
- `GetByName(string)` / `GetByClrType(Type)`
- `IsPrimitive(string)` / `IsNumeric(SemanticType)` / `IsInteger(SemanticType)`
- `IsFloatingPoint(SemanticType)` / `IsDecimal(SemanticType)`

### Numeric Promotion Rules
- **`GetPromotedType(left, right)`**: Returns result type for arithmetic
- **Priority-based**: `double` > `float` > integers by size
- **Mixed signedness**: Promotes to larger signed type (e.g., `int + uint` → `long`)
- **Decimal restriction**: Doesn't mix with float/double

### Conversion Rules
- **Implicit conversions**: Widening, integer → float, float → double
- **Explicit conversions**: All numerics can cast to each other, char ↔ integer

---

## Scope Management

### Scope Class
- **File**: `Semantic/Scope.cs` (59 lines)
- **Hierarchical**: Parent scope linking for lookup chains
- **Symbol Storage**: `Dictionary<string, Symbol>`
- **Redefinition**: Non-const variables can be redefined (Pythonic behavior)

### Symbol Table
- **File**: `Semantic/SymbolTable.cs` (86 lines)
- **Scope Stack**: Stack-based enter/exit for nested scopes
- **Global Scope**: Pre-populated with builtin types and functions
- **Lookup Methods**: `Lookup()`, `LookupType()`, `LookupFunction()`, `LookupVariable()`

### Semantic Info
- **File**: `Semantic/SemanticInfo.cs` (56 lines)
- **Purpose**: Maps AST nodes to semantic information without modifying AST
- **Mappings**:
  - Expressions → `SemanticType`
  - Identifiers → `Symbol`
  - Function calls → `FunctionSymbol`
  - Type annotations → `SemanticType`

---

## Import System (Extended)

### Import Resolver
- **File**: `Semantic/ImportResolver.cs` (418 lines)
- **Dual Resolution**: .spy files AND .NET assemblies
- **Circular Import Detection**: `_loadingModules` set
- **Module Cache**: `_moduleCache` dictionary

### Import Modes
- **`import module`**: Full module import
- **`from module import name1, name2`**: Selective import
- **`from module import *`**: Import all exports

### Module Path Resolution
- Search order: Current directory, search path, working directory
- Supports package directories with `__init__.spy`
- Module name conversion: `module.submodule` → `module/submodule.spy`

### .NET Module Integration
- **`ModuleInfo.IsNetModule`**: Flag for .NET assemblies
- **No AST**: .NET modules have no `Module` property (null)
- **Symbol Export**: All public functions exported

### Module Registry
- **File**: `Semantic/ModuleRegistry.cs` (199 lines)
- **Purpose**: Manage third-party .NET module loading
- **Thread-safe**: Uses `ConcurrentDictionary` and `ConcurrentBag`
- **Discovery**: Integrates with `CachedModuleDiscovery`
- **Assembly Resolution**: Checks absolute, relative, module paths

---

## Project Configuration

### Project Config
- **File**: `ProjectConfig.cs` (234 lines)
- **Project File Format**: `.spyproj` (XML-based)
- **Properties**:
  - `RootNamespace` (required)
  - `OutputType`: `"library"` or `"exe"`
  - `TargetFramework`: e.g., `"net8.0"`
  - `AssemblyName` (optional, defaults to namespace)
  - `Configuration`: `"Debug"` or `"Release"`

### Project File Parser
- **`ProjectFileParser.Load()`**: Parses .spyproj XML
- **Elements**:
  - `<PropertyGroup>`: Project settings
  - `<ItemGroup>`: Source files, references, module paths
- **Glob Support**: `<SpyFile Include="src/**/*.spy" />` via `Microsoft.Extensions.FileSystemGlobbing`

### Output Paths
- **`OutputPath`**: `{ProjectDir}/bin/{Config}/{TargetFramework}/`
- **`OutputAssemblyPath`**: Full path to output DLL/EXE

---

## Files Analyzed

The following files were analyzed to produce this document:

- `Lexer/Lexer.cs` - Tokenization
- `Lexer/Token.cs` - Token types
- `Lexer/LexerError.cs` - Lexer error handling
- `Parser/Parser.cs` - Parsing
- `Parser/Ast/Expression.cs` - Expression AST nodes
- `Parser/Ast/Statement.cs` - Statement AST nodes
- `Parser/Ast/Types.cs` - Type annotation AST nodes
- `Parser/Ast/Node.cs` - Base AST node
- `Parser/AstDumper.cs` - AST visualization
- `Parser/ParserError.cs` - Parser error handling
- `Semantic/NameResolver.cs` - Name resolution
- `Semantic/TypeResolver.cs` - Type resolution
- `Semantic/TypeChecker.cs` - Type checking
- `Semantic/SymbolTable.cs` - Symbol management
- `Semantic/Scope.cs` - Scope management
- `Semantic/Symbol.cs` - Symbol definitions
- `Semantic/SemanticType.cs` - Semantic type hierarchy
- `Semantic/SemanticInfo.cs` - AST-to-semantic mapping
- `Semantic/SemanticError.cs` - Semantic error handling
- `Semantic/BuiltinRegistry.cs` - Builtin registration
- `Semantic/AccessValidator.cs` - Access level validation
- `Semantic/ControlFlowValidator.cs` - Control flow validation
- `Semantic/OperatorValidator.cs` - Operator validation
- `Semantic/OperatorSignatureValidator.cs` - Dunder signature validation
- `Semantic/PrimitiveCatalog.cs` - Primitive type registry
- `Semantic/ImportResolver.cs` - Import resolution
- `Semantic/ModuleRegistry.cs` - Module management
- `CodeGen/RoslynEmitter.cs` - C# code generation
- `CodeGen/NameMangler.cs` - Name transformations
- `CodeGen/TypeMapper.cs` - Type mapping
- `CodeGen/CodeGenContext.cs` - Generation context
- `CodeGen/CodeValidator.cs` - Validation
- `AssemblyCompiler.cs` - Assembly compilation
- `Compiler.cs` - Main compiler driver
- `ProjectConfig.cs` - Project configuration
- `Diagnostics/CompilationMetrics.cs` - Compilation metrics
- `Discovery/CachedModuleDiscovery.cs` - Module discovery
- `Discovery/TypeMapper.cs` - CLR type mapping
- `Discovery/Caching/AssemblyIdentity.cs` - Assembly identification
- `Discovery/Caching/OverloadIndex.cs` - Function index
- `Discovery/Caching/OverloadIndexBuilder.cs` - Index building
- `Discovery/Caching/OverloadIndexCache.cs` - Index caching
- `Logging/ICompilerLogger.cs` - Logger interface
- `Logging/ConsoleCompilerLogger.cs` - Console logger
- `Logging/NullLogger.cs` - Null logger

---

## Notes for Reviewers

1. **Variable redefinition**: The compiler supports Pythonic variable redefinition with type changes via version tracking. Review `RoslynEmitter.GetMangledVariableName()`.

2. **Operator overloading**: Dunder methods generate both the method and the corresponding C# operator. Review `TryGenerateOperatorOverload()` and related methods.

3. **Type narrowing**: Limited to `is None`/`is not None` and `isinstance()` checks. Review `TypeChecker._narrowedTypes`.

4. **Comprehensions**: Only single `for` clause supported; nested comprehensions throw `NotImplementedException`.

5. **F-strings**: Full implementation with format specifiers. Complex state machine in lexer.

6. **Import system**: Maps to C# `using` directives. Supports both .spy modules and .NET assemblies.

7. **Numeric promotion**: Uses .NET standard promotion rules with special handling for mixed signed/unsigned.

8. **Module caching**: GZip-compressed JSON cache in `~/.sharpy/cache/` with 7-day cleanup.

9. **Thread safety**: `CachedModuleDiscovery` and `ModuleRegistry` are thread-safe; `OperatorValidator` is NOT.

10. **Access validation**: Python naming conventions (`_protected`, `__private`) enforced at semantic level.
