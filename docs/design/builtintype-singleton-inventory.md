# `BuiltinType` singleton inventory (#1356)

Measured at `37baaff2a`. This is the census the issue requires *before* the collapse: which
producer emits which spelling, which readers compare by name, and what the display surface costs.
Nothing here changes behaviour.

## The defect, as a live program

One CLR type, two user-visible spellings:

```python
def takes_str(v: str) -> None: ...

def main() -> None:
    a: float = 1.5
    b: float64 = 2.5
    takes_str(a)   # error[SPY0220]: Cannot pass argument of type 'float' to parameter of type 'str'
    takes_str(b)   # error[SPY0220]: Cannot pass argument of type 'float64' ...
```

`BuiltinType` is a record, so record equality includes `Name`, and `Float != Double` despite both
carrying `ClrType = typeof(double)`. `GetDisplayName() => Name` (`SemanticType.cs:199`), so every
singleton's name is user-visible verbatim.

## 1. Singletons

`SemanticType.cs:59-72` — twelve numeric/text singletons, name → CLR type:

| singleton | `Name` | `ClrType` | | singleton | `Name` | `ClrType` |
|---|---|---|---|---|---|---|
| `Int` | `int` | `int` | | `SByte` | `int8` | `sbyte` |
| `Long` | `int64` | `long` | | `Byte` | `uint8` | `byte` |
| `Float` | `float` | `double` | | `Short` | `int16` | `short` |
| `Double` | `float64` | `double` | | `UShort` | `uint16` | `ushort` |
| `Float32` | `float32` | `float` | | `UInt` | `uint32` | `uint` |
| `Str` | `str` | `string` | | `ULong` | `uint64` | `ulong` |

Two irregularities, both cited by the issue and both confirmed:

- **`Float` and `Double` are double-backed** — same `ClrType`, different `Name`, therefore unequal.
- **`Int` is named `int` while its siblings use catalog spellings** (`int64`, `uint32`, `uint64`).
  This is what cost #1304 twice: `bt.Name == "int32"` was written at three emitter sites and was
  dead at every one. All three are fixed and carry comments
  (`RoslynEmitter.Expressions.Operators.cs:441`, `:581`; `RoslynEmitter.Expressions.cs:498-499`).

## 2. The catalog, and a second split the plan did not name

`PrimitiveCatalog` registers **28 `PrimitiveInfo` entries over 16 distinct CLR types** — each type
twice, once Sharpy-style and once C#-style, except where a spelling collides. `Register` writes both
maps with `byClr[info.ClrType] = info`, so **`_byClrType` is last-write-wins** and the C#-style
aliases are registered second.

Result, computed by replaying the registration order:

| CLR type | `_byClrType` → `SharpyName` | | CLR type | → |
|---|---|---|---|---|
| `sbyte`/`short`/`int`/`long` | `sbyte`/`short`/`int`/`long` | | `double` | `double` |
| `byte`/`ushort`/`uint`/`ulong` | `byte`/`ushort`/`uint`/`ulong` | | `string` | `string` |
| `decimal`/`bool`/`char`/`object` | same | | `void` | `void` |
| **`float`** | **`float32`** | | | |

**15 of 16 CLR types canonicalize to the C#-style spelling**, while `_bySharpyName` treats the
Sharpy-style names as primary "per spec". `GetPrimitiveInfo` consults the CLR map *first*
(`PrimitiveCatalog.cs:121-131`), so the two disagree today and the CLR map wins.

`typeof(float)` is the **sole exception**, and structurally so: Sharpy has no C#-style `float` alias
for it, because the name `float` is already taken by `double`. Any collapse that picks "the C#-style
spelling is canonical" therefore has one type that cannot follow the rule.

`CSharpName` is unaffected by this: for the 14 C#-style registrations `SharpyName == CSharpName`, and
for the 14 Sharpy-style ones they differ as expected (`int8`→`sbyte`, `str`→`string`, `None`→`void`).
No entry has a wrong `CSharpName`.

## 3. Producers

Three families, and they do not agree.

**(a) Singletons — Sharpy spelling by construction.** `TypeResolver` maps annotations to singletons
(`"float64" => SemanticType.Double`, so the *name* `float64` survives); `IntegerLiteralClassifier`
types literals as `SemanticType.Int`/`.Long`/`.UInt`/`.ULong`; `ConstFoldPass` and the emitter read
singletons.

**(b) CLR bridge — C#-style spelling, via the reverse map.** `ClrTypeBridge.cs:198` and
`CachedModuleDiscovery.cs:773` both build `new BuiltinType { Name = primitiveInfo.SharpyName }` from
`GetByClrType`. Per §2 that yields `byte`, `string`, `long`, `double` — *not* `uint8`, `str`,
`int64`, `float`. **This is where the second spelling of every primitive enters the type system.**

**(c) A third family the census must not miss: `BuiltinType` is also used for NON-primitives.**
`ClrTypeBridge.cs:235-237` builds `BuiltinType { Name = clrType.Name }` for CLR iterator types
(e.g. `RangeIterator`), and `CachedModuleDiscovery.cs:906-908` from a cached signature name. So
"`is BuiltinType`" does not imply "is a primitive", and a name-comparison guard must not assume it.

## 4. Readers — the guard's real surface

**15 sites across 8 files** compare a `BuiltinType` by name (non-test), not the three the plan's
Phase 2 is framed around. Three of them are **hand-written alias hedges that exist because of this
split** and are load-bearing today, because producers (a) and (b) both reach them:

| site | hedge |
|---|---|
| `RoslynEmitter.Expressions.Access.Calls.cs:416` | `bt.Name == "uint8" \|\| bt.Name == "byte"` |
| `SemanticType.cs:430` | same `uint8`/`byte` pair |
| `TypeUtils.cs:66` | `BuiltinType { Name: "str" or "string" }` |

**These cannot be deleted before the collapse.** Deleting a disjunct removes support for whichever
producer emits that spelling — for the CLR-derived side that is interop, which lands in
`Sharpy.Stdlib.Tests`. They become removable exactly when one spelling is canonical, which is what
the collapse decides; until then the hedge is the workaround, not the defect.

## 5. Display surface

`GetDisplayName() => Name`, so the choice of canonical spelling is user-visible in every diagnostic.
**14 `.error` fixtures** assert an affected spelling in diagnostic text, **7** of them `float64`
specifically. (This was 10 when the plan was written; #1355 added 3 of the 4 new ones, asserting
`'uint8'` and `'int64'`.) `IsFloat32LiteralNarrowing`
(`TypeChecker.Statements.cs:1783-1793`) routes around the `Float`/`Double` split explicitly, by
comparing `ClrType` instead of `Name`, and its comment records why.

## 6. What the collapse must decide

1. **Which map is the spelling authority** — `_bySharpyName` (Sharpy-style, "per spec") or
   `_byClrType` (C#-style, and what `GetPrimitiveInfo` returns today). They disagree on 15 of 16
   types. This is a second instance of #1356's own class and was not in the plan's census list.
2. **What `float32` does**, since it is the one type where the two maps already agree and it cannot
   take a C#-style alias.
3. **Whether `Int` renames to `int32`** or the family renames to `int` — either way the three fixed
   #1304 sites and the guard's allowlist move together.
4. **Which of the 14 `.error` fixtures shift**, which follows mechanically from (1).

The standing guard (ban `Name ==` on `BuiltinType` outside the spelling layer) is regression
prevention for #1304's failure mode, not a fix, and must be mutation-tested by reintroducing
`"int32"` at one of the three former sites. Its allowlist is the 15 sites above minus whatever the
collapse deletes.
