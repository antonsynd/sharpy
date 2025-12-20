# Assignment Operators

| Operator | Description |
|----------|-------------|
| `=` | Simple assignment |
| `+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=` | Augmented arithmetic |
| `&=`, `\|=`, `^=`, `<<=`, `>>=` | Augmented bitwise |

*Implementation*
- *✅ Native - Direct mapping (except `**=` which is lowered).*
- *🔄 Lowered to dunder method calls for Sharpy standard library and user types that implement the inplace dunder methods, e.g. `__iadd__()`. This will change in the future if Sharpy moves to C# 14 where in-place operators can be overridden and the direct mapping will be used instead.*
