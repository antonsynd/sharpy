# Assignment Operators

| Operator | Description |
|----------|-------------|
| `=` | Simple assignment |
| `+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=` | Augmented arithmetic |
| `&=`, `\|=`, `^=`, `<<=`, `>>=` | Augmented bitwise |

In the current version of Sharpy, user definitions of assignment operators like `+=` via dunder methods (e.g. `__iadd__`) are not supported.

*Implementation*
- *✅ Native - Direct mapping (except `**=` and `//=` which are lowered).*
