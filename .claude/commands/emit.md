# Emit Intermediate Representations

Compile a Sharpy file and inspect intermediate representations for debugging.

## Source File

$ARGUMENTS

## Commands

```bash
# Emit generated C# code (most common)
dotnet run --project src/Sharpy.Cli -- emit csharp $ARGUMENTS

# Emit parsed AST (for debugging parser)
dotnet run --project src/Sharpy.Cli -- emit ast $ARGUMENTS

# Emit lexer tokens (for debugging lexer)
dotnet run --project src/Sharpy.Cli -- emit tokens $ARGUMENTS
```

## Use Cases

1. **Debug CodeGen issues** - See what C# is generated for a Sharpy construct
2. **Verify name mangling** - Check `snake_case` → `PascalCase` conversion
3. **Inspect type mappings** - See `list[T]` → `List<T>` transformations
4. **Validate dunder methods** - Verify `__str__` → `ToString()` mappings
5. **Debug parser issues** - Inspect AST structure with `emit ast`
6. **Debug lexer issues** - Inspect token stream with `emit tokens`

## Key Mappings

| Sharpy | C# |
|--------|-----|
| `list[T]` | `global::Sharpy.Core.List<T>` |
| `dict[K, V]` | `global::Sharpy.Core.Dict<K, V>` |
| `snake_case` | `PascalCase` |
| `__str__` | `ToString()` |
| `__add__` | `operator+` |
| `__eq__` | `Equals()` + `operator==` |

## Troubleshooting

If the generated C# looks wrong:
1. Check `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` for type mappings
2. Check `src/Sharpy.Compiler/CodeGen/NameMangler.cs` for name conversions
3. Check `RoslynEmitter*.cs` files for emission logic
