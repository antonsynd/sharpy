# Deferred Features

The following features require .NET 7+ runtime or C# 11+ and cannot be supported when targeting Unity or .NET 5/6:

| Feature | Required C# | Required .NET | Reason |
|---------|-------------|---------------|--------|
| `@file` access modifier | C# 11 | .NET 6+ | File-scoped types |
| List patterns `case [a, b]:` | C# 11 | Any | Compiler feature |
| Static abstract interface members | C# 11 | .NET 7 | Runtime support |
| Generic math constraints | C# 11 | .NET 7 | BCL interfaces |
| `required` members | C# 11 | .NET 7 | Attribute + compiler |
| Record structs | C# 10 | Any | Compiler feature |
| Decimal `m`/`M` literal suffix | C# 9.0 | Any | Requires `decimal` type support throughout the compiler |
| `field` keyword in properties | C# 13 | Any | Compiler feature |
| Extension properties/operators | C# 14 | Any | Compiler feature |
| User-defined `+=` operators | C# 14 | Any | Compiler feature |
