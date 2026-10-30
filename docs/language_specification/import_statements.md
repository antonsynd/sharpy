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

An import binds a module-level name, and it collides like every other module binding. Each of the four import spellings (`import m`, `import m as n`, `from m import x`, `from m import x as n`) is refused (`SPY0204`, at the import) when its name is already taken in the module by a class, def, `type` alias, `const` or module-level property, or by another import that binds a different module or symbol. Source order does not matter, because declarations are bound before imports. A refused import binds nothing, so the declaration keeps its meaning. A module variable is the one module binding declared after the imports bind, so against a module variable the import keeps the name and the variable is refused (`SPY0204`, at the variable), again in either order. Importing the same thing twice is harmless, and `import os` with `import os.path` share the one `os` binding. Python rebinds the name silently instead; Sharpy refuses because .NET has no later-binding-wins rule for these names (deviation `module-binding-collision-refused`).

<!-- spec-sweep: error SPY0204 -->
```python
import math as m          # ERROR SPY0204: 'm' is already defined in this module (function, line 3)

def m() -> int:
    return 1

def main() -> None:
    print(m())
```

<!-- spec-sweep: error SPY0204 -->
```python
from math import pi as k
k: float = 1.0            # ERROR SPY0204: Cannot bind 'k': it is already defined as an imported name in this scope

def main() -> None:
    print(k)
```

An import names the symbol it imports, not only its module. So two from-imports of one module that bind different symbols under one name collide: `from math import pi as a` followed by `from math import e as a` is refused at the second import. Importing the same symbol under the same name twice stays harmless.

<!-- spec-sweep: error SPY0204 -->
```python
from math import pi as a
from math import e as a   # ERROR SPY0204: 'a' already names 'math.pi' (imported at line 1); import 'math.e' under another name

def main() -> None:
    print(a)
```

The symbol an import names is its declaration, whichever module spelling reaches it. A package's `__init__.spy` that imports a submodule's name re-exports that very declaration, so importing the name through the package and directly from the submodule, under one name, is not a collision: the same declaration under two module spellings is one import. Two different declarations under one name still collide: with `from utils.helpers import other as fmt` in place of the second import below, that import is refused (`SPY0204`: 'fmt' already names 'utils.fmt' (imported at line 1); import 'utils.helpers.other' under another name).

<!-- spec-sweep: fragment -->
```python
# file: utils/helpers.spy
def fmt() -> str:
    return "f"

# file: utils/__init__.spy
from utils.helpers import fmt     # re-exports helpers' fmt as utils.fmt

# file: main.spy
from utils import fmt             # the declaration in utils/helpers.spy, through the package
from utils.helpers import fmt     # the same declaration, spelled directly: one import

def main() -> None:
    print(fmt())                  # f
```

A star import (`from m import *`) never refuses a module-level declaration. A class, def, enum, `type` alias, `const` or module-level property of the importing module that spells a star-imported name takes the name over, whatever its kind and source order, and so does a module variable that re-declares a star-imported variable. The module's own declaration is the one used.

<!-- spec-sweep: output "1.5" -->
```python
from math import *
pi: float = 1.5

def main() -> None:
    print(pi)
```

<!-- spec-sweep: output "5" -->
```python
from math import *
property e: int = 5       # the module's own property keeps the name; math.e is not used

def main() -> None:
    print(e)
```

An explicit import is a name the importer chose, so beside a star import it collides like any other import. An explicit import of a different source that spells a star-exported name is refused (`SPY0204`, at the explicit import), in either source order. Write the qualified name (`math.pi`), or drop the star import. An explicit import of the very symbol the star import binds is harmless.

<!-- spec-sweep: error SPY0204 -->
```python
import copy as pi         # ERROR SPY0204: 'pi' is imported explicitly ('import copy as pi', line 1) and also exported by 'from math import *' (line 2); write 'math.pi', or drop the star import
from math import *

def main() -> None:
    print(pi)
```

<!-- spec-sweep: output "3.141592653589793" -->
```python
from math import *
from math import pi       # the symbol the star import already binds: harmless

def main() -> None:
    print(pi)
```

An import alias that spells a builtin type name is refused (`SPY0212`), because every bare use of that name in the file would then mean the import. Write the alias backtick-escaped to keep the spelling. An import that binds a builtin *function* name (`import math as print`, `from os.path import join as len`) is honoured with a `SPY0484` warning, just as `def print` draws `SPY0483`. The warning is given once, by the import that binds the name: an import refused with `SPY0204` rebinds nothing and draws no `SPY0484`, and the same import repeated is not warned again. A plain `from m import x` whose library name spells a builtin type (`from mathutils import double`) gets the same warning, because the importer did not choose that name. A name imported out of the `builtins` module is the builtin itself and draws nothing.

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
