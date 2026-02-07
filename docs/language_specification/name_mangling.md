# Name Mangling

Sharpy uses Pythonic `snake_case` naming conventions in source code, but generates C# code following .NET `PascalCase` conventions. This document specifies the complete name transformation algorithm.

## Overview

| Sharpy Convention | C# Output | Example |
|-------------------|-----------|---------|
| `snake_case` functions | `PascalCase` | `get_user_name()` → `GetUserName()` |
| `snake_case` parameters | `camelCase` | `user_name: str` → `string userName` |
| `camelCase` identifiers | Preserved | `httpClient` → `httpClient` |
| `PascalCase` identifiers | Preserved | `XMLParser` → `XMLParser` |
| `_private` fields | `_camelCase` | `_user_count` → `_userCount` |
| `__name` fields | `__PascalCase` | `__private_field` → `__PrivateField` |
| `__dunder__` methods | Special mapping | `__init__` → constructor |
| `CAPS_SNAKE_CASE` constants | `PascalCase` | `MAX_SIZE` → `MaxSize` |

## Transformation Algorithm

The name mangling algorithm transforms identifiers as follows:

### Step 1: Handle Leading Underscores

Leading underscores are preserved for access modifier semantics:

| Pattern | Preservation | Purpose |
|---------|--------------|---------|
| `_name` | Keep single `_` | Private member |
| `__name` | Keep double `__` | Private (name-mangled in Python style if needed) |
| `__name__` | Special handling | Dunder method |

### Step 2: Detect Name Form

After stripping leading underscores and trailing underscores, the remaining body is classified into one of these forms:

| Form | Pattern | Example |
|------|---------|---------|
| `SnakeCase` | All lowercase + digits + single underscores | `get_user_name` |
| `ScreamingSnakeCase` | All uppercase + digits + single underscores | `MAX_SIZE` |
| `PascalCase` | Starts uppercase, no underscores | `HttpClient`, `XMLParser` |
| `CamelCase` | Starts lowercase, has uppercase, no underscores | `httpClient`, `iPhone` |
| `SingleWordLower` | All lowercase, no underscores | `hello` |
| `SingleWordUpper` | All uppercase, no underscores | `HTTP` |
| `Dunder` | `__name__` bookends (length > 4) | `__init__`, `__str__` |
| `Unrecognized` | Anything else (consecutive underscores, mixed case with underscores) | `foo__bar`, `Foo_bar` |

### Step 3: Transform Based on Form

The transformation depends on the detected form and the target convention:

**For PascalCase target** (functions, methods, types, constants):

| Form | Transformation | Example |
|------|----------------|---------|
| `SnakeCase` | Split on `_`, capitalize each segment | `get_user_name` → `GetUserName` |
| `ScreamingSnakeCase` | Split on `_`, title-case each segment | `MAX_SIZE` → `MaxSize` |
| `PascalCase` | Pass through | `HttpClient` → `HttpClient` |
| `CamelCase` | Pass through | `httpClient` → `httpClient` |
| `SingleWordLower` | Capitalize | `hello` → `Hello` |
| `SingleWordUpper` | Pass through | `HTTP` → `HTTP` |
| `Unrecognized` | Pass through (with SPY0453 warning) | `foo__bar` → `foo__bar` |

**For camelCase target** (parameters, local variables, fields):

| Form | Transformation | Example |
|------|----------------|---------|
| `SnakeCase` | Split on `_`, lowercase first, capitalize rest | `user_name` → `userName` |
| `ScreamingSnakeCase` | Split on `_`, fully lower first, title-case rest | `MAX_SIZE` → `maxSize` |
| `PascalCase` | Lowercase first char | `HttpClient` → `httpClient` |
| `CamelCase` | Pass through | `httpClient` → `httpClient` |
| `SingleWordLower` | Pass through | `hello` → `hello` |
| `SingleWordUpper` | Fully lowercase | `HTTP` → `http` |
| `Unrecognized` | Pass through (with SPY0453 warning) | `foo__bar` → `foo__bar` |

**For constant context** (module-level constants):

Constants use the same rules as PascalCase target, except `SingleWordUpper` is normalized: `HTTP` → `Http`.

### Step 4: Reattach Leading Underscores

Reattach any leading underscores that were preserved in Step 1:

```
_private_field → _PrivateField (for methods)
_private_field → _privateField (for fields)
__private_field → __PrivateField (for methods)
__private_count → __privateCount (for fields)
```

## Complete Examples

| Sharpy | Context | C# Output |
|--------|---------|-----------|
| `get_user_name` | Function/Method | `GetUserName` |
| `user_name` | Parameter | `userName` |
| `httpClient` | Variable | `httpClient` (preserved) |
| `_user_count` | Private field | `_userCount` |
| `__private_count` | Private field | `__privateCount` |
| `MAX_RETRY_COUNT` | Constant | `MaxRetryCount` |
| `HTTP_STATUS_CODE` | Constant | `HttpStatusCode` |
| `get_html_parser` | Function | `GetHtmlParser` |
| `XMLParser` | Class (already PascalCase) | `XMLParser` (preserved) |
| `parse_xml` | Function | `ParseXml` |
| `io_error` | Parameter | `ioError` |
| `__init__` | Constructor | Constructor (special) |
| `__str__` | Dunder | `ToString()` override |
| `_internal_helper` | Private method | `_InternalHelper` |
| `__private_field` | Private method | `__PrivateField` |

## Collision Handling

Name collisions can occur when different Sharpy names produce the same C# name. Note that `camelCase` identifiers are preserved as-is, so `fooBar` stays as `fooBar` and does not collide with `foo_bar` → `FooBar`.

Collisions are more likely with names that differ only in underscore placement:

```python
# ❌ Both single-word lowercase → same PascalCase
# (hypothetical — in practice, snake_case names with different segments are unlikely to collide)
```

**Resolution:** The compiler detects collisions and reports an error. Use **backtick escaping** to preserve the exact identifier:

```python
`fooBar` = 1      # Stays as 'fooBar' in C#
`foo_bar` = 2     # Stays as 'foo_bar' in C# (unusual but valid)
```

## CamelCase Passthrough

Identifiers already in `camelCase` form (start lowercase, contain uppercase letters, no underscores) are preserved as-is in both PascalCase and camelCase contexts:

```python
httpClient = create_client()   # httpClient stays as httpClient in C#
iPhone = get_device()          # iPhone stays as iPhone in C#
```

**Rationale:** `camelCase` names are already valid C# identifiers — mangling them would destroy the author's intended casing (e.g., `httpClient` would incorrectly become `Httpclient`). This preserves interop-friendly names and avoids surprising transformations.

Similarly, `PascalCase` identifiers (start uppercase, no underscores) are preserved as-is:

```python
XMLParser = create_parser()    # XMLParser stays as XMLParser in C#
HttpClient = get_client()      # HttpClient stays as HttpClient in C#
```

## Unrecognized Name Forms

Names that don't match any well-formed convention are passed through as-is:

- **Consecutive underscores**: `foo__bar` → `foo__bar` (with warning SPY0453)
- **Mixed case with underscores**: `Foo_bar` → `Foo_bar`

These names produce a compiler warning (SPY0453) because consecutive underscores can cause name mangling collisions — for example, `foo__bar` and `foo_bar` would both mangle to `FooBar` under a naive algorithm. The compiler avoids this by passing through unrecognized forms unchanged.

To suppress the warning, rename the identifier or use backtick escaping:

```python
`foo__bar`: int = 1    # Backtick escaping: no warning, name preserved exactly
```

## Dunder Method Mapping

Dunder methods have special mappings that don't follow the standard algorithm:

| Sharpy Dunder | C# Output |
|---------------|-----------|
| `__init__` | Constructor |
| `__str__` | `ToString()` override |
| `__repr__` | Custom or `ToString()` |
| `__eq__` | `operator ==` and `Equals()` |
| `__ne__` | `operator !=` |
| `__lt__` | `operator <` |
| `__le__` | `operator <=` |
| `__gt__` | `operator >` |
| `__ge__` | `operator >=` |
| `__hash__` | `GetHashCode()` |
| `__len__` | `Count` property or `Length` |
| `__iter__` | `GetEnumerator()` |
| `__getitem__` | Indexer `this[...]` |
| `__setitem__` | Indexer setter |
| `__contains__` | `Contains()` method |
| `__add__` | `operator +` |
| `__sub__` | `operator -` |
| `__mul__` | `operator *` |
| `__div__` | `operator /` |
| `__bool__` | `operator true`/`operator false` |

See [dunder_methods.md](dunder_methods.md) for complete dunder specifications.

## Acronym Handling

Sharpy follows a simplified acronym rule: all-caps segments are treated as single words and converted to title case:

```python
# All-caps become title case
HTTP_STATUS → HttpStatus
XML_PARSER → XmlParser
IO_ERROR → IoError
TCP_IP_ADDRESS → TcpIpAddress

# Not preserved as all-caps (unlike some .NET conventions)
# If you need HttpClient style, use: http_client → HttpClient
```

**Rationale:** This rule is simpler to implement and produces consistent results. For names that should preserve specific casing, use backtick escaping.

## Parameters vs Fields vs Methods

Different identifier types use different target conventions:

| Identifier Type | Convention | Example |
|-----------------|------------|---------|
| Function/Method | PascalCase | `get_user()` → `GetUser()` |
| Parameter | camelCase | `user_id: int` → `int userId` |
| Local variable | camelCase | `result_count` → `resultCount` |
| Private field (`_`) | _camelCase | `_data_store` → `_dataStore` |
| Private field (`__`) | __PascalCase | `__private_field` → `__PrivateField` |
| Public property | PascalCase | `property user_name` → `UserName` |
| Constant | PascalCase | `MAX_SIZE` → `MaxSize` |
| Class/Struct/Interface | Preserved | `UserService` → `UserService` |
| Enum value | PascalCase | `RED_COLOR` → `RedColor` |

## Interop with .NET Libraries

When calling .NET libraries from Sharpy, use the C# names directly (PascalCase):

```python
# Calling .NET
from system import Console
Console.WriteLine("Hello")  # Use actual C# name

# Sharpy method calling C#
def print_message(msg: str):  # Sharpy convention
    Console.WriteLine(msg)    # C# convention
```

When .NET code calls Sharpy-compiled code, it sees the mangled PascalCase names:

```csharp
// C# calling Sharpy-compiled code
var result = sharpyModule.GetUserName();  // Not get_user_name
```

*Implementation*
- Name mangling is performed during code generation by `NameMangler` (form detection via `NameFormDetector`)
- Dunder method mapping is handled by `DunderMapping` (codegen-owned, separate from `NameMangler`)
- Naming convention warnings (SPY0453) are emitted by `NamingConventionValidator` during semantic validation
- Backtick-escaped identifiers bypass mangling
