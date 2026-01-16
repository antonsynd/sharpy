# Module Access Code Generation - Quick Reference Examples

**For Task:** 0.1.10.CG2 - Define C# Emission Strategy for Module Access

This document provides concrete examples for implementers working on tasks CG3-CG7.

## Core Principle

**Sharpy modules compile to static classes with nested `Exports` class.**

```
Python import → C# using alias = Namespace.Module.Exports
Python module.member → C# moduleAlias.Member
```

## Example 1: Simple Module Variable Access

**Input Files:**

```python
# config.spy
MAX_SIZE: int = 100
MIN_SIZE: int = 10
```

```python
# main.spy
import config

def main():
    print(f"Max: {config.MAX_SIZE}")
    print(f"Min: {config.MIN_SIZE}")
```

**Expected C# Output:**

```csharp
// Config.cs
namespace MyProject
{
    public static class Config
    {
        public static class Exports
        {
            public static int MaxSize = 100;
            public static int MinSize = 10;
        }
    }
}
```

```csharp
// Main.cs
using config = MyProject.Config.Exports;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void Main()
            {
                Console.WriteLine($"Max: {config.MaxSize}");
                Console.WriteLine($"Min: {config.MinSize}");
            }
        }
    }
}
```

## Example 2: Module with Functions

**Input Files:**

```python
# math_utils.spy
def add(a: int, b: int) -> int:
    return a + b

def multiply(a: int, b: int) -> int:
    return a * b
```

```python
# main.spy
import math_utils

def main():
    result = math_utils.add(5, 3)
    print(result)
```

**Expected C# Output:**

```csharp
// MathUtils.cs
namespace MyProject
{
    public static class MathUtils
    {
        public static class Exports
        {
            public static int Add(int a, int b)
            {
                return a + b;
            }

            public static int Multiply(int a, int b)
            {
                return a * b;
            }
        }
    }
}
```

```csharp
// Main.cs
using math_utils = MyProject.MathUtils.Exports;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void Main()
            {
                var result = math_utils.Add(5, 3);
                Console.WriteLine(result);
            }
        }
    }
}
```

## Example 3: Aliased Import

**Input:**

```python
# main.spy
import math_utils as math

def main():
    result = math.add(10, 20)
    print(result)
```

**Expected C#:**

```csharp
// Main.cs
using math = MyProject.MathUtils.Exports;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void Main()
            {
                var result = math.Add(10, 20);
                Console.WriteLine(result);
            }
        }
    }
}
```

## Example 4: Nested Module Import

**Directory Structure:**
```
lib/
    math/
        operations.spy
main.spy
```

**Input Files:**

```python
# lib/math/operations.spy
def add(a: int, b: int) -> int:
    return a + b
```

```python
# main.spy
import lib.math.operations

def main():
    result = lib.math.operations.add(5, 3)
    print(result)
```

**Expected C# Output:**

```csharp
// Lib/Math/Operations.cs
namespace MyProject.Lib.Math
{
    public static class Operations
    {
        public static class Exports
        {
            public static int Add(int a, int b)
            {
                return a + b;
            }
        }
    }
}
```

```csharp
// Main.cs
using lib_math_operations = MyProject.Lib.Math.Operations.Exports;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void Main()
            {
                var result = lib_math_operations.Add(5, 3);
                Console.WriteLine(result);
            }
        }
    }
}
```

**Note:** Dotted module names in imports become underscore-separated aliases.

## Example 5: From-Import

**Input:**

```python
# main.spy
from math_utils import add, multiply

def main():
    result = add(5, 3)  # Direct access, no prefix
    product = multiply(2, 4)
    print(result, product)
```

**Expected C#:**

```csharp
// Main.cs
using static MyProject.MathUtils.Exports;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void Main()
            {
                var result = Add(5, 3);  // Direct access via using static
                var product = Multiply(2, 4);
                Console.WriteLine($"{result} {product}");
            }
        }
    }
}
```

## Example 6: From-Import with Alias

**Input:**

```python
# main.spy
from math_utils import add as sum_values

def main():
    result = sum_values(5, 3)
    print(result)
```

**Expected C# (Option 1 - using static + local alias):**

```csharp
// Main.cs
using static MyProject.MathUtils.Exports;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void Main()
            {
                // Create local delegate/alias
                var sum_values = Add;
                var result = sum_values(5, 3);
                Console.WriteLine(result);
            }
        }
    }
}
```

**Expected C# (Option 2 - type alias):**

```csharp
// Main.cs
using sum_values = MyProject.MathUtils.Exports.Add;  // Won't work for methods
// Better: use Option 1 or fully qualified name
```

**Recommendation:** For simplicity, `from X import Y as Z` should:
1. Use `using static Namespace.Module.Exports;`
2. Generate local variable alias if needed: `var Z = Y;`

## Example 7: Module with Mixed Declarations

**Input:**

```python
# utils.spy
VERSION: str = "1.0.0"
_private_data: int = 42

def get_version() -> str:
    return VERSION

def _internal_helper() -> int:
    return _private_data

class Helper:
    def do_something(self) -> None:
        pass
```

**Expected C#:**

```csharp
// Utils.cs
namespace MyProject
{
    public static class Utils
    {
        public static class Exports
        {
            // Public module variable
            public static string Version = "1.0.0";

            // Public function
            public static string GetVersion()
            {
                return Version;
            }

            // Public class
            public class Helper
            {
                public void DoSomething()
                {
                }
            }
        }

        // Private (outside Exports class)
        private static int _privateData = 42;

        private static int _InternalHelper()
        {
            return _privateData;
        }
    }
}
```

**Key Point:** Private members (starting with `_`) should be outside the `Exports` class.

## Example 8: Package with __init__.spy

**Directory Structure:**
```
mypackage/
    __init__.spy
    module_a.spy
    module_b.spy
main.spy
```

**Input Files:**

```python
# mypackage/module_a.spy
def func_a() -> str:
    return "A"
```

```python
# mypackage/module_b.spy
def func_b() -> str:
    return "B"
```

```python
# mypackage/__init__.spy
from mypackage.module_a import func_a
from mypackage.module_b import func_b

PACKAGE_VERSION: str = "1.0.0"
```

```python
# main.spy
import mypackage

def main():
    print(mypackage.func_a())
    print(mypackage.func_b())
    print(mypackage.PACKAGE_VERSION)
```

**Expected C# Output:**

```csharp
// Mypackage/ModuleA.cs
namespace MyProject.Mypackage
{
    public static class ModuleA
    {
        public static class Exports
        {
            public static string FuncA()
            {
                return "A";
            }
        }
    }
}
```

```csharp
// Mypackage/ModuleB.cs
namespace MyProject.Mypackage
{
    public static class ModuleB
    {
        public static class Exports
        {
            public static string FuncB()
            {
                return "B";
            }
        }
    }
}
```

```csharp
// Mypackage/__init__.cs
using static MyProject.Mypackage.ModuleA.Exports;
using static MyProject.Mypackage.ModuleB.Exports;

namespace MyProject.Mypackage
{
    public static class Mypackage
    {
        public static class Exports
        {
            // Re-export functions from submodules
            public static Func<string> func_a = FuncA;
            public static Func<string> func_b = FuncB;

            // Package-level variable
            public static string PackageVersion = "1.0.0";
        }
    }
}
```

```csharp
// Main.cs
using mypackage = MyProject.Mypackage.Mypackage.Exports;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void Main()
            {
                Console.WriteLine(mypackage.func_a());
                Console.WriteLine(mypackage.func_b());
                Console.WriteLine(mypackage.PackageVersion);
            }
        }
    }
}
```

## Example 9: .NET Framework Import (Special Case)

**Input:**

```python
# main.spy
import system.io
from system.collections.generic import List

def main():
    path = system.io.File.ReadAllText("test.txt")
    items: List[str] = List[str]()
```

**Expected C#:**

```csharp
// Main.cs
using System.IO;
using System.Collections.Generic;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void Main()
            {
                var path = File.ReadAllText("test.txt");
                List<string> items = new List<string>();
            }
        }
    }
}
```

**Note:** .NET framework imports don't use `.Exports` - they're direct namespace imports.

## Example 10: Multiple Imports in One File

**Input:**

```python
# main.spy
import config
import math_utils as math
from utils import format_text, parse_input
import system.io

def main():
    max_val = config.MAX_SIZE
    sum_val = math.add(1, 2)
    text = format_text("hello")
    data = parse_input("world")
    file_text = system.io.File.ReadAllText("test.txt")
```

**Expected C#:**

```csharp
// Main.cs
using config = MyProject.Config.Exports;
using math = MyProject.MathUtils.Exports;
using static MyProject.Utils.Exports;
using System.IO;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void Main()
            {
                var max_val = config.MaxSize;
                var sum_val = math.Add(1, 2);
                var text = FormatText("hello");
                var data = ParseInput("world");
                var file_text = File.ReadAllText("test.txt");
            }
        }
    }
}
```

## Name Mangling Reference

| Sharpy Name | C# Name | Context |
|-------------|---------|---------|
| `MAX_SIZE` | `MaxSize` | Constant/Variable |
| `add_numbers` | `AddNumbers` | Function |
| `user_name` | `userName` | Parameter |
| `_private_field` | `_privateField` | Private field |
| `get_html_content` | `GetHtmlContent` | Function |
| `HTTP_STATUS` | `HttpStatus` | Constant |

## Module Alias Rules

| Import Statement | Using Alias | Reason |
|------------------|-------------|--------|
| `import config` | `config` | Simple name |
| `import math_utils` | `math_utils` | Simple name |
| `import lib.math` | `lib_math` | Dots → underscores |
| `import a.b.c` | `a_b_c` | Dots → underscores |
| `import config as cfg` | `cfg` | User-specified alias |

## Testing Checklist

For implementers working on CG3-CG7, verify these scenarios:

- [ ] Module variable generates as static field in Exports
- [ ] Module function generates as static method in Exports
- [ ] Module class generates inside Exports (public) or outside (private)
- [ ] Import generates correct using alias
- [ ] From-import generates using static
- [ ] Nested import (a.b.c) generates alias with underscores
- [ ] Member access applies name mangling (snake_case → PascalCase)
- [ ] Private members (starting with _) are outside Exports
- [ ] .NET framework imports don't include .Exports
- [ ] Package __init__.spy re-exports work correctly

## Implementation Notes

**Current State (as of analysis):**

✅ **Already Working:**
- Import → using alias conversion
- Member access with name mangling
- .NET framework detection
- Basic module structure

⚠️ **Needs Verification:**
- Module variables as static fields
- Nested module access (lib.math.func)
- Package re-exports
- Private vs. public member placement

**Key Files to Modify:**

1. `RoslynEmitter.cs:245-339` - Import handling (mostly done)
2. `RoslynEmitter.cs:GenerateCompilationUnit()` - Module class structure
3. `RoslynEmitter.cs:GenerateMemberAccess()` - Already works via aliases

**Algorithm Summary:**

```
For each module file:
  1. Create namespace: ProjectNamespace.ModulePath
  2. Create static class: ModuleName
  3. Create nested static class: Exports
  4. Place public declarations in Exports
  5. Place private declarations outside Exports

For each import:
  1. If .NET framework → using Namespace;
  2. If Sharpy module → using alias = Namespace.Module.Exports;
  3. Generate alias by replacing dots with underscores (unless user-specified)

For each from-import:
  1. If .NET framework → using Namespace;
  2. If Sharpy module → using static Namespace.Module.Exports;

For member access:
  1. Apply name mangling to member name
  2. Generate: moduleAlias.MangledMember
```

---

**Quick Decision Reference:**

| Question | Answer |
|----------|--------|
| Where do module variables go? | Static fields in `Exports` class |
| Where do module functions go? | Static methods in `Exports` class |
| Where do private members go? | Outside `Exports`, in parent module class |
| How to handle `lib.math`? | Using alias `lib_math = Lib.Math.Exports` |
| How to handle from-import? | `using static Module.Exports` |
| How to handle member access? | Apply PascalCase mangling to member |
| What about .NET imports? | Direct namespace, no `.Exports` |
