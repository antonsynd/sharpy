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
- *✅ Native - `using static` or direct reference.*

### CLR Import Names

When importing from a .NET namespace, type and namespace names are resolved from the reflected CLR metadata — the Pythonic→PascalCase mangler that transforms `snake_case` Sharpy identifiers does not apply to import targets:

```python
from system import Guid    # → using Guid = global::System.Guid;
from system import Uri      # → using Uri = global::System.Uri;
from system.net.http import HttpClient  # → using HttpClient = global::System.Net.Http.HttpClient;
```
