# Empty Set Literal

Sharpy implements the yet to be approved [PEP 802](https://peps.python.org/pep-0802/)
from Python which designates `{/}` as the empty set literal. It is still
possible to use the constructor function `set()`.

**C# Implementation:**
🔄 Lowered - `new HashSet<T>()` where `T` is inferred from the context, either
the assignment target or the return type of a function if it is part of a
return statement.
