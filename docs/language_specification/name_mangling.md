# Name Mangling

Sharpy uses Pythonic `snake_case` naming conventions in source code, but generates C# code following .NET `PascalCase` conventions. This document specifies the complete name transformation algorithm.

## Overview

| Sharpy Convention | C# Output | Example |
|-------------------|-----------|---------|
| `snake_case` functions | `PascalCase` | `get_user_name()` → `GetUserName()` |
| `snake_case` parameters | `camelCase` | `user_name: str` → `string userName` |
| `_private` fields | `_camelCase` | `_user_count` → `_userCount` |
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

### Step 2: Split into Segments

Split the identifier on underscores (excluding leading/trailing underscores):

```
get_user_name → ["get", "user", "name"]
_private_field → ["private", "field"] (leading _ preserved separately)
HTTP_STATUS → ["HTTP", "STATUS"]
get_html_parser → ["get", "html", "parser"]
```

### Step 3: Normalize Segments

Each segment is normalized:

1. **All-caps segments**: Convert to lowercase, then capitalize first letter
   - `HTTP` → `Http`
   - `STATUS` → `Status`
   - `XML` → `Xml`

2. **Mixed-case segments**: Capitalize first letter, preserve rest
   - `user` → `User`
   - `html` → `Html`

3. **Single-character segments**: Capitalize
   - `x` → `X`
   - `i` → `I`

### Step 4: Join and Apply Target Convention

Join segments according to target convention:

| Target | Join Method | Example |
|--------|-------------|---------|
| PascalCase | Capitalize each segment | `GetUserName` |
| camelCase | Lowercase first, capitalize rest | `getUserName` |

### Step 5: Reattach Leading Underscores

Reattach any leading underscores that were preserved:

```
_private_field → _PrivateField (for methods)
_private_field → _privateField (for fields)
```

## Complete Examples

| Sharpy | Context | C# Output |
|--------|---------|-----------|
| `get_user_name` | Function/Method | `GetUserName` |
| `user_name` | Parameter | `userName` |
| `_user_count` | Private field | `_userCount` |
| `MAX_RETRY_COUNT` | Constant | `MaxRetryCount` |
| `HTTP_STATUS_CODE` | Constant | `HttpStatusCode` |
| `get_html_parser` | Function | `GetHtmlParser` |
| `XMLParser` | Class (already PascalCase) | `XMLParser` (unchanged) |
| `parse_xml` | Function | `ParseXml` |
| `io_error` | Parameter | `ioError` |
| `__init__` | Constructor | Constructor (special) |
| `__str__` | Dunder | `ToString()` override |
| `_internal_helper` | Private method | `_InternalHelper` |

## Collision Handling

Name collisions can occur when different Sharpy names produce the same C# name:

```python
# ❌ Collision: both become FooBar
foo_bar = 1
fooBar = 2    # ERROR: conflicts with foo_bar after mangling
```

**Resolution:** The compiler detects collisions and reports an error:

```
error: Name collision: 'fooBar' and 'foo_bar' both mangle to 'FooBar'.
       Use backtick escaping to preserve exact names: `foo_bar` or `fooBar`
```

**Backtick escaping** preserves the exact identifier:

```python
`fooBar` = 1      # Stays as 'fooBar' in C#
`foo_bar` = 2     # Stays as 'foo_bar' in C# (unusual but valid)
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
| Private field | _camelCase | `_data_store` → `_dataStore` |
| Public property | PascalCase | `property user_name` → `UserName` |
| Constant | PascalCase | `MAX_SIZE` → `MaxSize` |
| Class/Struct/Interface | Unchanged | `UserService` → `UserService` |
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
- *🔄 Lowered - Name mangling is performed during code generation by `NameMangler` utility*
- *Collisions are detected during semantic analysis*
- *Backtick-escaped identifiers bypass mangling*
