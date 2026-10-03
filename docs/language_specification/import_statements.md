# Import Statements

## Import Statement

```python
# Import entire module
import math
result = math.sqrt(16.0)

# Import with alias
import math as m
result = m.sqrt(16.0)
```

*Implementation*
- *✅ Native - `using Namespace;` or `using Alias = Namespace;`*

## From-Import Statement

```python
# Import specific names
from math import sqrt, pi
result = sqrt(16.0)

# Import with alias
from math import sqrt as square_root

# Import all (use sparingly)
from math import *
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
