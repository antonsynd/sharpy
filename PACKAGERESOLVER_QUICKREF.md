# PackageResolver Quick Reference

## Overview
`PackageResolver` handles package-level symbol resolution from `__init__.spy` files in Sharpy. It enables Python-style package initialization and re-exports.

## Basic Usage

### Create a Package Resolver
```csharp
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;

var logger = new ConsoleLogger();
var packageResolver = new PackageResolver(logger);
```

### Resolve a Package
```csharp
var packageInfo = packageResolver.ResolvePackage(
    packageName: "myapp.utils",
    initPath: "/path/to/myapp/utils/__init__.spy"
);

if (packageInfo != null)
{
    Console.WriteLine($"Package: {packageInfo.Name}");
    Console.WriteLine($"Exports {packageInfo.ExportedSymbols.Count} symbols");
    
    foreach (var (name, symbol) in packageInfo.ExportedSymbols)
    {
        Console.WriteLine($"  - {name} ({symbol.Kind})");
    }
}
```

## Package Structure Example

### Directory Layout
```
myapp/
  utils/
    __init__.spy      ← Package initialization
    helpers.spy       ← Module
    validators.spy    ← Module
```

### `helpers.spy`
```python
def format_string(s: str) -> str:
    return s.strip()

def _internal_helper() -> None:
    pass
```

### `validators.spy`
```python
def validate_email(email: str) -> bool:
    return "@" in email

def validate_url(url: str) -> bool:
    return url.startswith("http")
```

### `__init__.spy` (Re-exports)
```python
# Re-export specific symbols
from myapp.utils.helpers import format_string
from myapp.utils.validators import validate_email, validate_url

# Re-export with alias
from myapp.utils.helpers import format_string as format_text

# Re-export all public symbols
from myapp.utils.validators import *

# Define package-level symbols
VERSION: str = "1.0.0"

def package_info() -> str:
    return "Utils package"
```

## Symbol Types

### Exported Symbols
- **Functions**: `def func_name(...)`
- **Classes**: `class ClassName:`
- **Structs**: `struct StructName:`
- **Interfaces**: `interface IName:`
- **Enums**: `enum EnumName:`
- **Constants**: `const NAME: type = value`

### Access Levels
- `public_name` → Public
- `_protected_name` → Protected  
- `__private_name` → Private

## Re-export Behavior

### `from X import Y`
```python
from utils.helpers import format_string
```
**Result**: `format_string` available at package level

### `from X import Y as Z`
```python
from utils.helpers import format_string as format_text
```
**Result**: `format_text` available (not `format_string`)

### `from X import *`
```python
from utils.validators import *
```
**Result**: Only public symbols (no `_` or `__` prefix) are re-exported

## Caching

### Auto-caching
```csharp
var resolver = new PackageResolver(logger);

// First call - parses and caches
var pkg1 = resolver.ResolvePackage("myapp.utils", initPath);

// Second call - returns cached result
var pkg2 = resolver.ResolvePackage("myapp.utils", initPath);

Assert.Same(pkg1, pkg2); // Same instance
```

### Clear Cache
```csharp
resolver.ClearCache();

// Next call will re-parse
var pkg3 = resolver.ResolvePackage("myapp.utils", initPath);
```

## Error Handling

### File Not Found
```csharp
var packageInfo = resolver.ResolvePackage("nonexistent", "/bad/path/__init__.spy");
// Returns null
```

### Invalid Syntax
```csharp
// __init__.spy contains syntax errors
var packageInfo = resolver.ResolvePackage("broken_pkg", initPath);
// Returns null, logs error
```

### Failed Import
```csharp
// __init__.spy: from nonexistent_module import something
var packageInfo = resolver.ResolvePackage("partial_pkg", initPath);
// Returns PackageInfo with other symbols, skips failed import
```

## Integration with ImportResolver

### Pass Existing ImportResolver
```csharp
var importResolver = new ImportResolver(logger);
var packageResolver = new PackageResolver(logger, importResolver);

// Uses shared ImportResolver for consistent module resolution
```

## PackageInfo Properties

```csharp
public class PackageInfo
{
    public string Name { get; init; }              // "myapp.utils"
    public string InitPath { get; init; }          // "/path/to/__init__.spy"
    public Module Module { get; init; }            // Parsed AST
    public Dictionary<string, Symbol> ExportedSymbols { get; init; }
}
```

### Accessing Symbols
```csharp
if (packageInfo.ExportedSymbols.TryGetValue("format_string", out var symbol))
{
    if (symbol is FunctionSymbol funcSymbol)
    {
        Console.WriteLine($"Function: {funcSymbol.Name}");
        Console.WriteLine($"Access: {funcSymbol.AccessLevel}");
    }
}
```

## Common Patterns

### Check if Symbol Exists
```csharp
bool hasFormatter = packageInfo.ExportedSymbols.ContainsKey("format_string");
```

### Filter by Symbol Type
```csharp
var functions = packageInfo.ExportedSymbols.Values
    .OfType<FunctionSymbol>()
    .ToList();

var classes = packageInfo.ExportedSymbols.Values
    .OfType<TypeSymbol>()
    .Where(t => t.TypeKind == TypeKind.Class)
    .ToList();
```

### Filter by Access Level
```csharp
var publicSymbols = packageInfo.ExportedSymbols
    .Where(kvp => kvp.Value.AccessLevel == AccessLevel.Public)
    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
```

## Testing

### Unit Test Example
```csharp
[Fact]
public void ResolvePackage_WithReExports_IncludesImportedSymbols()
{
    // Arrange
    var testDir = CreateTempDir();
    CreateModule(testDir, "helpers.spy", "def helper() -> None: pass");
    CreatePackage(testDir, "__init__.spy", "from helpers import helper");
    
    var resolver = new PackageResolver();
    
    // Act
    var pkg = resolver.ResolvePackage("test_pkg", Path.Combine(testDir, "__init__.spy"));
    
    // Assert
    Assert.NotNull(pkg);
    Assert.Contains("helper", pkg.ExportedSymbols.Keys);
}
```

## Performance Considerations

### Caching Benefits
- Avoids re-parsing same package
- Reduces file I/O
- Improves compilation speed

### When to Clear Cache
- After file system changes
- During development/debugging
- Between test runs

## Troubleshooting

### Symbol Not Exported
**Problem**: Symbol defined in module but not in package exports
**Solution**: Add to `__init__.spy`:
```python
from mymodule import symbol_name
```

### Import Fails Silently
**Problem**: `from X import Y` doesn't error but symbol missing
**Solution**: Check logger output for import errors

### Cache Stale Data
**Problem**: Changes to `__init__.spy` not reflected
**Solution**: Call `resolver.ClearCache()` before re-resolving

## See Also
- `ModuleResolver` - Finds `__init__.spy` files
- `ImportResolver` - Handles import statements
- `SymbolTable` - Stores resolved symbols
- Module System Specification - docs/language_specification/module_system.md
