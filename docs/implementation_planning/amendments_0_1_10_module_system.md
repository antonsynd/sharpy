# Amendments for Phase 0.1.10: Module System

**Review Date:** 2026-01-14  
**Reviewed Against:** Language Specification (modules.md, imports.md, packages.md, grammar.ebnf.txt)  
**Axiom Priority:** Axiom 1 (.NET Runtime) > Axiom 2 (Python Surface) > Axiom 3 (Static & Null-Safe)

---

## Amendment 1: Namespace Generation from Module Path

**Section:** Task 0.1.10.5 (Namespace Generation)

**Issue:** The task list note (line 1232) mentions namespace from path but doesn't detail the algorithm.

**Task List Note:**
> "Namespace generation from path: `utils/helpers.spy` → `ProjectName.Utils.Helpers`"

**Detailed Algorithm:**
1. Project name from `.spyproj` becomes root namespace
2. Directory path segments become namespace segments
3. File name (without `.spy`) becomes final namespace segment or class container
4. Apply PascalCase transformation to each segment

**Examples:**
```
Project: MyApp.spyproj
File: src/models/user.spy

Namespace: MyApp.Src.Models.User
  OR
Namespace: MyApp.Models (if src is excluded)
Class: User (inside namespace)
```

**Required Clarification:**
- Should `src/` directory be included in namespace? (Common practice: no)
- How to handle nested classes within same file?
- Module-level functions → static class with file name?

**Recommended Approach:**
```python
# File: myproject/utils/helpers.spy
def format_text(s: str) -> str:
    return s.upper()

class TextProcessor:
    pass
```

**Generated C#:**
```csharp
namespace MyProject.Utils  // Or MyProject.Utils.Helpers
{
    public static class Helpers  // Module-level container
    {
        public static string FormatText(string s) => s.ToUpper();
    }
    
    public class TextProcessor { }
}
```

---

## Amendment 2: Import Namespace Transformation

**Section:** Task 0.1.10.7 (Import Code Generation)

**Issue:** Task list doesn't specify how Sharpy imports map to C# using statements.

**Import Mapping:**

1. **`import module`:**
   ```python
   import utils.helpers
   # Usage: utils.helpers.function()
   ```
   
   **C# Option A:** Fully qualified calls
   ```csharp
   // No using statement needed
   MyProject.Utils.Helpers.Function();
   ```
   
   **C# Option B:** Using alias
   ```csharp
   using utils_helpers = MyProject.Utils.Helpers;
   utils_helpers.Function();
   ```

2. **`import module as alias`:**
   ```python
   import utils.helpers as h
   # Usage: h.function()
   ```
   
   **C#:**
   ```csharp
   using h = MyProject.Utils.Helpers;
   h.Function();
   ```

3. **`from module import name`:**
   ```python
   from utils.helpers import format_text
   # Usage: format_text()
   ```
   
   **C#:**
   ```csharp
   using static MyProject.Utils.Helpers;  // For static class members
   FormatText();
   ```

4. **`from module import *`:**
   ```python
   from utils.helpers import *
   ```
   
   **C#:**
   ```csharp
   using static MyProject.Utils.Helpers;
   ```

**Decision (Per Axiom 1):**
- Use `using static` for `from ... import` of functions
- Use `using namespace` for class imports
- Use `using alias = ...` for aliased imports

---

## Amendment 3: Relative vs Absolute Imports

**Section:** Task 0.1.10.2 (Module Resolution)

**Issue:** Task list doesn't distinguish relative and absolute imports.

**Python-style Relative Imports:**
```python
from . import helper        # Current package
from .. import utils        # Parent package
from .sibling import func   # Sibling module
```

**Phase Scope Decision:**
- **Phase 0.1.10:** Absolute imports only
- Relative imports (`.`, `..`) deferred to future phase
- Add note: "Relative imports not supported in 0.1.10"

**Rationale (Axiom 1):** C# doesn't have relative `using` statements. Relative imports would require additional resolution logic.

---

## Amendment 4: Private Symbol Export Rules

**Section:** Task 0.1.10.3 (Import Symbol Resolution)

**Issue:** Task list mentions private symbols aren't exported by `import *`, but details needed.

**Export Rules:**

| Symbol Pattern | Exported by `import *` | Directly Importable |
|---------------|------------------------|---------------------|
| `public_func` | ✅ Yes | ✅ Yes |
| `_protected_func` | ❌ No | ✅ Yes |
| `__private_func` | ❌ No | ❌ No |

**Detailed Behavior:**
```python
# In utils.spy
def public_func(): pass
def _protected_func(): pass
def __private_func(): pass
```

```python
# In main.spy
from utils import *          # Only public_func imported
from utils import _protected_func  # OK: explicit import allowed
from utils import __private_func   # ERROR: Cannot import private
```

**Required Validation:**
- `import *` skips names starting with `_`
- Explicit import of `_name` is allowed (protected)
- Explicit import of `__name` is an error (private)

**Error Message:**
- "Cannot import private symbol '__private_func' from module 'utils'"

---

## Amendment 5: `__init__.spy` Semantics

**Section:** Task 0.1.10.8 (`__init__.spy` Support)

**Issue:** Task list shows `__init__.spy` but doesn't detail semantics.

**`__init__.spy` Purpose:**
1. Marks directory as a package
2. Defines package-level exports (re-exports)
3. Executes when package is imported

**Example Structure:**
```
mypackage/
├── __init__.spy      # Package initialization
├── module_a.spy
└── module_b.spy
```

**`__init__.spy` Content:**
```python
# mypackage/__init__.spy
from mypackage.module_a import ClassA, func_a
from mypackage.module_b import ClassB

# Now these are available at package level:
# from mypackage import ClassA, ClassB, func_a
```

**Required Implementation:**
1. When `import mypackage`, load and execute `__init__.spy`
2. Symbols re-exported in `__init__.spy` become package-level symbols
3. Direct submodule access: `from mypackage.module_a import ...` bypasses `__init__.spy`

**Generated C# Namespace:**
```csharp
namespace MyPackage
{
    // Re-exports not needed in C# - types are directly accessible
    // using statements resolve to original definitions
}
```

---

## Amendment 6: Circular Import Detection Algorithm

**Section:** Task 0.1.10.6 (Circular Import Handling)

**Issue:** Task list describes allowed/disallowed patterns but not detection algorithm.

**Two-Phase Resolution Algorithm:**

**Phase 1: Type Collection**
- Parse all files
- Collect type declarations (classes, structs, enums, interfaces)
- Do NOT resolve type bodies yet
- This allows forward references

**Phase 2: Body Resolution**
- Resolve imports and type bodies
- Check for circular dependencies that require resolution

**Circular Import Categories:**

| Category | Example | Allowed |
|----------|---------|---------|
| Type annotation only | `field: OtherClass` | ✅ Yes |
| Default value | `field: OtherClass = OtherClass()` | ⚠️ Order dependent |
| Inheritance | `class A(B)` where B imports A | ❌ No |
| Constructor body | `super().__init__()` | ✅ Yes (runtime) |

**Detection Logic:**
```python
# module_a.spy
from module_b import ClassB
class ClassA(ClassB):  # Needs ClassB fully resolved
    pass

# module_b.spy
from module_a import ClassA
class ClassB(ClassA):  # Needs ClassA fully resolved → CYCLE
    pass
```

**Error Message:**
"Circular inheritance detected: ClassA inherits from ClassB, which inherits from ClassA"

---

## Amendment 7: .NET Assembly Reference Support

**Section:** Task 0.1.10.9 (.spyproj Support)

**Issue:** Task list mentions project files but not external assembly references.

**Future Requirement:**
For importing .NET types (System, Collections, etc.), need assembly references.

**`.spyproj` Example:**
```xml
<Project>
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System.Collections" />
    <ProjectReference Include="../MyLibrary/MyLibrary.spyproj" />
  </ItemGroup>
</Project>
```

**Phase Scope Decision:**
- Phase 0.1.10: Sharpy source imports only
- .NET interop (System imports) → Phase 0.1.12
- Add note: "External assembly references not supported in 0.1.10"

---

## Amendment 8: Import Statement Ordering

**Section:** Task 0.1.10.1 (Import Statement Parsing)

**Issue:** Spec may have import ordering requirements not in task list.

**Python Convention (PEP 8):**
1. Standard library imports
2. Third-party imports
3. Local application imports

**Sharpy Decision:**
- No enforced ordering (for now)
- Imports can appear anywhere at module top-level
- All imports must be at file beginning before other statements

**Validation:**
```python
def func():  # Some code
    pass

import utils  # ERROR: Imports must be at top of file
```

**Error Message:**
"Import statements must appear at the beginning of the file, before any other statements"

---

## Amendment 9: Test Fix - Project Compilation API

**Section:** Task 0.1.10.10 (Integration Tests)

**Issue:** Tests use `CompileProject(_tempDir, "main.spy")` but API isn't defined.

**Required Test Infrastructure:**
```csharp
private CompileResult CompileProject(string directory, string entryPoint)
{
    // 1. Find all .spy files in directory
    // 2. Parse all files
    // 3. Resolve imports across files
    // 4. Generate C# for all files
    // 5. Compile C# assembly
    return result;
}

private CompileResult CompileProject(
    string directory, 
    string entryPoint, 
    string[] sourceFiles)
{
    // Explicit file list compilation
}
```

**Test Helper Methods Needed:**
- `WriteFile(string path, string content)` - Write test file
- `CompileProject(...)` - Compile multi-file project
- `CompileAndExecute(...)` - Compile and run (for single-file)

---

## Amendment 10: Symbol Shadowing/Collision

**Section:** Task 0.1.10.3 (Import Symbol Resolution)

**Issue:** Task list doesn't address import shadowing.

**Shadowing Scenarios:**

1. **Local shadows import:**
   ```python
   from utils import helper
   
   def helper():  # ERROR: Shadows imported 'helper'
       pass
   ```

2. **Later import shadows earlier:**
   ```python
   from module_a import func
   from module_b import func  # ERROR: 'func' already imported
   ```

3. **Import shadows builtin:**
   ```python
   from mymodule import print  # WARNING: Shadows builtin 'print'
   ```

**Validation Rules:**
- Same-name import → Error
- Local definition shadows import → Error
- Import shadows builtin → Warning

**Error Messages:**
- "Cannot import 'func': already imported from module_a"
- "Local definition 'helper' shadows imported symbol from 'utils'"
- "Imported 'print' shadows builtin function" (warning)

---

## Amendment 11: Import All (`*`) Namespace Pollution Warning

**Section:** Task 0.1.10.3 (Import Symbol Resolution)

**Issue:** Task list doesn't warn about `import *` issues.

**Best Practice Warning:**
`from module import *` can cause:
1. Namespace pollution (many unwanted symbols)
2. Unclear symbol origins
3. Potential name collisions

**Compiler Behavior:**
- Allow `import *` but emit warning
- Warning message: "Wildcard import 'from utils import *' may pollute namespace. Consider importing specific symbols."

**Suppress Option:**
```python
# Intentional wildcard import
from utils import *  # noqa: wildcard
```

---

## Amendment 12: Module Initialization Order

**Section:** Task 0.1.10.4 (Multi-File Compilation)

**Issue:** Task list doesn't specify initialization order for module-level code.

**Module-Level Code:**
```python
# config.spy
print("config loading")  # Module-level statement
CONFIG = {"debug": True}
```

**Initialization Order:**
1. Dependencies loaded first (imported modules)
2. Module-level statements execute in file order
3. `__init__.spy` executes when package imported

**C# Equivalent:**
```csharp
// Static constructor or module initializer
public static class Config
{
    static Config()
    {
        Console.WriteLine("config loading");
    }
    
    public static readonly Dictionary<string, object> CONFIG = 
        new() { {"debug", true} };
}
```

**Required Clarification:**
- Module-level statements run once when module first imported
- Import order determines initialization order
- Circular initialization can cause issues

---

## Amendment 13: Re-export Syntax

**Section:** Task 0.1.10.8 (`__init__.spy` Support)

**Issue:** Need explicit re-export support for packages.

**Re-export Pattern:**
```python
# mypackage/__init__.spy

# Re-export specific symbols
from mypackage.submodule import ClassA, ClassB

# Re-export with alias
from mypackage.internal import _HelperClass as PublicHelper

# Re-export all (rare, use carefully)
from mypackage.utils import *
```

**Public API Definition:**
The symbols available via `from mypackage import ...` are:
1. Symbols defined in `__init__.spy`
2. Symbols imported/re-exported in `__init__.spy`

**NOT automatically available:**
- Submodule contents (must be explicitly re-exported)
- Private symbols (`_name`, `__name`)

---

## Summary of Required Changes

| Amendment | Type | Priority | Effort |
|-----------|------|----------|--------|
| 1. Namespace Generation | Clarification | High | 2h |
| 2. Import to Using Mapping | Feature | High | 2h |
| 3. Relative Imports | Scope Note | Medium | - |
| 4. Private Export Rules | Validation | Medium | 1h |
| 5. `__init__.spy` Semantics | Feature | High | 3h |
| 6. Circular Detection | Algorithm | High | 2h |
| 7. Assembly References | Scope Note | Low | - |
| 8. Import Ordering | Validation | Low | 0.5h |
| 9. Test Infrastructure | Testing | High | 2h |
| 10. Symbol Shadowing | Validation | Medium | 1h |
| 11. Wildcard Warning | Warning | Low | 0.5h |
| 12. Init Order | Clarification | Medium | 1h |
| 13. Re-export Syntax | Feature | Medium | 1h |

**Total Additional Effort Estimate:** ~16 hours
