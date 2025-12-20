# Import Statements **[v0.1.0]**

## Import Statement

```python
# Import entire module
import math
result = math.sqrt(16.0)

# Import with alias
import math as m
result = m.sqrt(16.0)
```

*Implementation: ✅ Native - `using Namespace;` or `using Alias = Namespace;`*

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

*Implementation: ✅ Native - `using static` or direct reference.*
