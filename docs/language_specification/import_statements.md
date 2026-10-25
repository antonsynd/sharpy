# Import Statements

## Import Statement

<!-- spec-sweep: output "4.0\n4.0" -->
```python
# Import entire module
import math

# Import with alias
import math as m

def main() -> None:
    print(math.sqrt(16.0))
    print(m.sqrt(16.0))
```

*Implementation*
- *✅ Native - `using Namespace;` or `using Alias = Namespace;`*

An import is a module-level statement: it binds a name for the whole module. An `import` or `from … import` inside a function, a block or a class body is refused (`SPY0708`); write it at the top of the file.

<!-- spec-sweep: error SPY0708 -->
```python
def area(r: float) -> float:
    import math           # ERROR SPY0708: an import is not allowed inside a function
    return 3.14159 * r * r

def main() -> None:
    print(area(1.0))
```

### An Import Binds a Module-Level Name

An import binds a module-level name, and it collides like every other module binding. Every import spelling (`import m`, `import m as n`, `from m import x`, `from m import x as n`) is refused (`SPY0204`, at the import) when its name is already taken in the module by a class, def, `type` alias, `const`, module-level property or module variable, or by another import that binds a different module or symbol. Source order does not matter, because declarations are bound before imports. Importing the same thing twice is harmless, and `import os` with `import os.path` share the one `os` binding. A refused import binds nothing, so the declaration keeps its meaning. Python rebinds the name silently instead; Sharpy refuses because .NET has no later-binding-wins rule for these names (deviation `module-binding-collision-refused`).

<!-- spec-sweep: error SPY0204 -->
```python
import math as m          # ERROR SPY0204: 'm' is already defined in this module (function, line 3)

def m() -> int:
    return 1

def main() -> None:
    print(m())
```

An import alias that spells a builtin type name is refused (`SPY0212`), because every bare use of that name in the file would then mean the import. Write the alias backtick-escaped to keep the spelling. An import that binds a builtin *function* name (`import math as print`, `from os.path import join as len`) is honoured with a `SPY0484` warning, just as `def print` draws `SPY0483`. A plain `from m import x` whose library name spells a builtin type (`from mathutils import double`) gets the same warning, because the importer did not choose that name. A name imported out of the `builtins` module is the builtin itself and draws nothing.

<!-- spec-sweep: error SPY0212 -->
```python
import math as str        # ERROR SPY0212: 'str' is a builtin type name

def main() -> None:
    print(str.pi)
```

<!-- spec-sweep: output "3.141592653589793" -->
```python
import math as `str`

def main() -> None:
    print(`str`.pi)
```

## From-Import Statement

<!-- spec-sweep: output "4.0\n5.141592653589793" -->
```python
# Import specific names
from math import sqrt, pi

# Import with alias
from math import sqrt as square_root

# Import all (use sparingly)
from math import *

def main() -> None:
    print(sqrt(16.0))
    print(square_root(4.0) + pi)
```

*Implementation*
- *✅ Native — every imported module-level member is emitted fully `global::`-qualified through its module's class — for a Sharpy module, its members class in its namespace (`from lib import f` in root namespace `App` → `global::App.Lib.LibModule.F(...)`); there is no `using static` directive. See [module_system.md](module_system.md#name-qualification-in-generated-c).*

### Parenthesized Names

As in Python (PEP 328), the imported names may be wrapped in parentheses. Only then may the list
span several lines and end with a trailing comma; comments may sit between the names, and `as`
aliases work as usual. `sharpyc format` keeps the parentheses.

```python
from math import (
    sqrt,
    floor as round_down,  # aliases work inside the parentheses
    pi,
)

def main():
    print(sqrt(16.0), round_down(2.7), pi > 3.0)   # 4.0 2.0 True
```

`from math import sqrt,` (a trailing comma without parentheses), `from math import (*)` and
`from math import ()` are refused, as in Python.

### CLR Import Names

When importing from a .NET namespace, type and namespace names are resolved from the reflected CLR metadata — the Pythonic→PascalCase mangler that transforms `snake_case` Sharpy identifiers does not apply to import targets:

```python
from system import Guid    # → using Guid = global::System.Guid;
from system import Uri      # → using Uri = global::System.Uri;
from system.net.http import HttpClient  # → using HttpClient = global::System.Net.Http.HttpClient;
```
