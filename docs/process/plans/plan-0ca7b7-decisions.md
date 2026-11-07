# plan-0ca7b7 — decision brief for the five open rulings (3, 9, 10, 12, 14)

Companion to `plan-0ca7b7.md` (verified 2026-09-24 @ acd1d40a2). Every C# shape below is the measured emit at that sha (probes: verify-plan session scratchpad `probes/p14/`, `probes/chores-b/`) or the plan's proposed shape, labelled. Doors: **one-way** = C# consumers or committed snapshots depend on the spelling; **two-way** = reversible by a later diagnostic change only.

Rulings already given 2026-09-24 and recorded in the plan: 1 SPY0707 · 2 every host · 4 refuse by position · 5 include Formattable · 6 all `System.Enum` · 7 add the indexer · 8 deviation · 11 S-a · 13 backticks = literal spelling, escaped `main` is not an entry point · 15 SPY0523.

---

## 3. Backtick-escaped enum member (#2037, Decision 4)

**Repro (measured @ acd1d40a2):**

```python
enum Color:
    `red` = 1          # "emit exactly `red`"
    GREEN = 2

class H:
    enum Shade:
        myGreen = 3    # no escape at all

def main() -> None:
    print(Color.red.value)        # plain reference     → emits Color.Red   (works)
    print(Color.`red`.value)      # escaped reference   → emits Color.red   → CS0117 behind SPY0908
    print(H.Shade.myGreen.value)  # nested camelCase    → decl Mygreen, ref myGreen → CS0117 behind SPY0908
```

Today the **declaration ignores the escape** (the member is emitted `Red` by `ToEnumMemberName`) while an **escaped reference honours it** (`red`), so the two spellings disagree and the C# compiler refuses. Six sites spell an enum member (int decl, string decl + `Values`, `C.x` reference, `case C.x` pattern, qualified `H.C.x`, the collision checks); no two agree. The nested camelCase cell fails with no escape anywhere — the same class (two spellers), not an escape bug.

Spec today (`enums.md:88-92`): "an enum member's declaration cannot be backtick-escaped" — written under the emitter gap; it contradicts `name_mangling.md:250` ("this holds at every declaration position") and ruling 13 (backticks mean the literal spelling).

**Options**

| | (a) Declaration governs — RECOMMENDED | (a′) Per-site, like the #478 class-member cell | (b) Refuse the escaped declaration |
|---|---|---|---|
| Emitted member | `red` (the declaration's spelling, materialized once as `CodeGenInfo.CSharpName` on the member symbol) | `red` | n/a — SPY0525-style "rename" |
| `Color.red` (plain ref) | binds → `Color.red` ✔ (python-name semantics: same member) | emits `Color.Red` → **CS0117 behind SPY0908** (as #478 pins CS1061 for class members) | ✔ (member is `Red`) |
| `` Color.`red` `` (escaped ref) | binds → `Color.red` ✔ (use-site escape is redundant) | ✔ | must ALSO be refused or it is CS0117 |
| `case Color.red:` | ✔ (reads the symbol fact) | needs a per-part escape flag on `MemberAccessPattern.Parts` (parser change) | ✔ |
| Nested camelCase `myGreen` | cured by the same read (one speller) | cured by the same read | still needs (a)'s fact — (b) alone fixes nothing |
| Spec | rewrite `enums.md:88-92`; SPY0525's steer offers the escape | same | keep the sentence |
| Authorities | ONE (the symbol fact) | TWO (declaration vs each use) | ONE, but the escape is forbidden in one position only |
| Door | two-way (a later per-site rule can only add refusals) | two-way | two-way |

**Why (a):** it is the only option where "backticks mean the literal spelling" holds at the declaration AND a plain python-name reference still binds — the escape changes the C# spelling, never the python name. (a′) keeps SPY0908 as the refusal channel by design (the meta-class the verification contract forbids). (b) contradicts ruling 13 and leaves the nested camelCase bug in place.

**Consequence to record, not fold:** the closed #478 class-member cell (`` `q`: int `` declared, `v.q` used → CS1061 behind SPY0908, pinned by `EscapedDeclarationOnly_StaysCs1061_TheClosed478Contract`) becomes the outlier — the same "plain reference to an escaped declaration" question with the opposite answer. File it as a sibling ("one materialized CSharpName per member, read at every use") for a later round; do not change it in this plan.

---

## 9. `<X>` — the module-members class name (Decision 28 (b))

**The situation.** Under P14b + P14c a Sharpy module becomes ONE C# namespace; its functions, module variables and `Main` need a static class to live in (C# has no free functions); its types become namespace siblings. `<X>` is that class's name. It is **one-way**: every C# consumer of a Sharpy library and every `.expected.cs` snapshot spells it; the `__init__` module class takes it in P14b.

**Worked example.** `src/geometry/shapes.spy`, RootNamespace `Merge`:

```python
class Box:
    def __init__(self, w: int) -> None: self.w = w
def area(b: Box) -> int: return b.w * b.w
PI: float = 3.14
```

Today (measured):

```csharp
namespace Merge {
  public static partial class Geometry {                       // directory → wrapper class
    [SharpyModule("geometry.shapes")]
    public static partial class Shapes {
      public class Box { … }                                    // type NESTED in the module class
      public static int Area(Box b) => …;
      public static double PI = 3.14;
} } }
// C# consumer:  Merge.Geometry.Shapes.Area(new Merge.Geometry.Shapes.Box(1));
```

After P14b + P14c, per option (the `Box` line is identical in all three):

```csharp
namespace Merge.Geometry.Shapes {
  [SharpyModuleType("geometry.shapes", "Box")] public class Box { … }

  // X1                                   // X2                                     // X3
  [SharpyModule("geometry.shapes")]       [SharpyModule("geometry.shapes")]          [SharpyModule("geometry.shapes")]
  public static partial class Module {    public static partial class Shapes {       public static partial class ShapesModule {
    public static int Area(Box b) …         public static int Area(Box b) …            public static int Area(Box b) …
    public static double PI …               public static double PI …                  public static double PI …
  }                                       }                                          }
}
```

C# consumer under each:

```csharp
using Merge.Geometry.Shapes;
Module.Area(new Box(1));            // X1  — but see the ambiguity rows below
Shapes.Area(new Box(1));            // X2
ShapesModule.Area(new Box(1));      // X3
```

**How other languages that compile free functions to a CLR/JVM class solved the same thing**

| Language | Where free functions land | Types | Same-name clash (type named like its module/file) |
|---|---|---|---|
| **F#** | static class named after the `module` | a type declared INSIDE a module is a NESTED class (today's Sharpy layout); a type in a `namespace` is a sibling | `[<CompilationRepresentation(ModuleSuffix)>]` → `ThingModule`; **applied automatically since F# 4.1** when a type and module share a name. F# hit exactly #1919/#2039 and chose a suffixed stem |
| **Kotlin/JVM** | file facade class `<Stem>Kt` (per file), `@file:JvmName("X")` to rename, `@JvmMultifileClass` to merge files | siblings in the package | never merges: `Thing` + `ThingKt` |
| **Scala 3** | top-level defs → `<Stem>$package`; `object Thing` → `Thing$` + static forwarders | siblings | suffix `$package` |
| **Boo** (.NET, Python-like) | `<Stem>Module` static class | siblings in the namespace | suffix `Module` — the closest ancestor to Sharpy |
| **Nemerle** | explicit `module X` → static class | siblings | user-named |
| **C# top-level statements** | `Program` (fixed name) | — | safe only because there is ONE such class per assembly — the property X1 lacks (one `Module` per namespace) |
| **Python** | the module object holds both `thing.Box` and `thing.area` | — | no split; ergonomically closest to X2 (`Shapes.Area`) |

Every precedent that faced the clash chose a **suffixed stem**; none chose a fixed word.

**Comparison**

| | X1 `Module` | X2 `<Stem>` | X3 `<Stem>Module` |
|---|---|---|---|
| FQN of a function | `Merge.Geometry.Shapes.Module.Area` | `Merge.Geometry.Shapes.Shapes.Area` (doubled segment) | `Merge.Geometry.Shapes.ShapesModule.Area` |
| `using` two Sharpy module namespaces in one C# file | **CS0104 ambiguous `Module`** — consumers must alias (`using Shapes = Merge.Geometry.Shapes.Module;` gives `Shapes.Area(...)`, Kotlin-like) | distinct names, no ambiguity | distinct names, no ambiguity |
| `using System.Reflection;` in the same file | **ambiguous with `System.Reflection.Module`** | — | — |
| IDE completion | dozens of classes named `Module` | natural | natural |
| SPY0523 (user name = `<X>`) | only a user `def module`/`class Module` (ruling 15: SPY0523) | **every arity-0 `class Shapes` in `shapes.spy`** — the #1919/#2039 family stays refused for the arity-0 twin; only the generic twin is cured (`Shapes` + `Shapes<T>` can coexist in C#) | only a user type literally named `ShapesModule` |
| Analyzer noise on the emitted library | none | CA1724 (type name equals a namespace name) on every module | none |
| `__init__.spy` (P14b) | `namespace Merge.Pkg { class Module }` | `namespace Merge.Pkg { class Pkg }` → CA1724, and `Merge.Pkg.Pkg` | `namespace Merge.Pkg { class PkgModule }` |
| Stdlib (S-a, 18 spy-sourced modules; today `Sharpy.MathModule.Sqrt`) | `Sharpy.MathModule.Module.Sqrt` (stem is `math_module.spy`) | `Sharpy.MathModule.MathModule.Sqrt` | `Sharpy.MathModule.MathModuleModule.Sqrt` — ugly ONLY because those stems already end in `_module`; renaming the spy stems (`math_module.spy` → `math.spy`; the module name comes from `[SharpyModule("math")]` in `__Init__.cs`, not the stem) yields `Sharpy.Math.MathModule.Sqrt` = today's class name one namespace deeper |
| Precedent | C# `Program` (one per assembly — not analogous) | Python | F#, Boo |
| Door | one-way | one-way | one-way |

**Recommendation revised by this comparison: X3 `<Stem>Module`.** The plan recommended X1 for brevity; the two ambiguity rows (`System.Reflection.Module`, and any C# file that `using`s two Sharpy modules) are consumer-facing costs a library format should not carry, and every language precedent went the suffix route. X3's only real cost is the Stdlib spelling, which is a stem-naming artefact fixable by renaming 13 spy files (a separate, mechanical task; the 5 bare-stem modules `textwrap`, `statistics`, `heapq`, `itertools`, `functools` are already right). X2 keeps the SPY0523 refusal for the very cell (#2039's arity-0 sibling) this phase exists to cure and trips CA1724 everywhere.

If brevity of consumer code matters more than the ambiguity rows, X1 with a documented alias idiom is the fallback; do not pick X2.

---

## 10. Keep the arity-0 merge? (Decision 28 (c), M1 vs M2)

**Repro.** `thing.spy`:

```python
class Thing:
    def __init__(self, v: int) -> None: self.v = v
def helper() -> int: return 7
```

Today (measured): the class named like its file is **merged** with the module class —

```csharp
namespace Merge {
  public class Thing {            // NOT partial, NO [SharpyModule]
    public int V; …
    public static int Helper() => 7;   // module function became a static on the user's class
} }
```

Consequences measured: `class Thing[T]` in `thing.spy` → SPY0520 (arity ≠ 0 cannot merge — #1919/#2039); a referenced library's merged module has no `[SharpyModule]` → `from thing import helper` is **SPY0300** (libref cell c); the merge is decided in THREE places (`ModuleShape`, `GenerateModuleMembers`, F1's string compare). Python: `thing.Thing` (class) and `thing.helper` (function) are distinct attributes — the merge is a Sharpy-only "magic behaviour" (design anti-pattern table).

**Options**

| | M2 no merge — RECOMMENDED | M1 keep the merge for arity-0 |
|---|---|---|
| Emit | `namespace Merge.Thing { static class <X> { Helper } class Thing { V } }` | as today for arity-0; `<X>` layout for everything else |
| Layouts to maintain | ONE | TWO, chosen by "does the file happen to hold an arity-0 class with its stem name" |
| Adding `class Thing` to an existing `thing.spy` | nothing else moves | **moves every function's FQN** (`Merge.Thing.<X>.Helper` → `Merge.Thing.Thing.Helper`) — fragile for C# consumers |
| `class Thing[T]` | runs (the #2039 cure); `Thing` and `Thing<T>` may even coexist | still the special case |
| Library discovery | `[SharpyModule]` on `<X>` always → libref c resolves | merged class needs its own `[SharpyModule]` stamp (separate fix) and `DiscoverNestedModuleTypes` keeps a second path |
| `ModuleClassMergeMatrixTests` 8 kinds × 2 modes | all 12 refused cells run | 2 run |
| Precedent | Kotlin (`Thing` + `ThingKt`), F# (`Thing` + `ThingModule`) — neither merges | none |
| Cost | 21 fixture files' FQNs move ONCE (`Merge.Thing.Helper` → `Merge.Thing.<X>.Helper`); C# consumers of a merged module re-spell once | none now; the two-layout tax forever |
| Door | one-way, paid once, in the same commit as P14c's snapshot regen | keeps the door open at the price of two layouts |

**Why M2:** one layout, python-faithful, and the F#/Kotlin precedents both refused to merge for the same reason (a type and a bag of functions are different things). M1 is the "multiple ways to do the same thing" anti-pattern with a consumer-visible fragility.

---

## 12. Single-file library mode (Decision 28 (a))

**How many layout rules exist today — four** (all measured):

| Mode | Emit for `things.spy` = `class Foo` + `def mk()` | Type FQN |
|---|---|---|
| 1. `project` (RootNamespace `Merge`, subdir `pkg/`) | `namespace Merge { static partial class Pkg { [SharpyModule] static partial class Things { class Foo; static Mk } } }` (+ the merge rule when a class is named like the file) | `Merge.Pkg.Things+Foo` |
| 2. single-file `run` | global namespace: `static partial class Things { class Foo; static Mk; static Main }` | `Things+Foo` |
| 3. single-file library, no `-n` | global namespace: `static partial class Things { Mk }` **+ `[SharpyModuleType("things","Foo")] class Foo` extracted beside it** (`DecorateExtractedType`) | `Foo` |
| 4. single-file library with `-n X` | `namespace X { static partial class Things { class Foo; Mk } }` (extraction gated OFF by `ProjectNamespace`) | `X.Things+Foo` |

**Options**

| | Uniform module-as-namespace — RECOMMENDED | Keep rule 3 as a third layout |
|---|---|---|
| Rule | ONE: `namespace [<Root>.]<Segments>.<Stem> { <X> { members, Main } ; sibling types }`, `<Root>` empty when no `-n`/RootNamespace | TWO: the uniform rule + "single-file library without `-n` extracts types into the GLOBAL namespace" |
| `run` | `namespace Things { static class <X> { Main; Mk } class Foo }` — C# accepts `Main` in any class in any namespace; `print(Foo())` → `<__main__.Foo object>` (P11f) either way | same |
| single-file library, no `-n` | `namespace Things { <X> { Mk } class Foo }` → C# consumers write `Things.Foo` | `global::Foo` stays |
| Emitter code | `DecorateExtractedType` becomes THE type path in every mode; the `ProjectNamespace` gates at `ModuleClass.cs:423-425/548-550` go | two arms stay, keyed on "library && no -n" |
| Tests | `NamespaceLevelTypesTests` (10 Facts) re-pinned to `Things.Foo` | unchanged |
| Consumers | one-way once for single-file libraries built without `-n` (`Foo` → `Things.Foo`) | no move |
| Precedent | Kotlin: the package is the namespace in every compilation mode; F#: a file's `namespace`/`module` line, never the build mode, decides | none — build mode deciding layout is unusual |

**Why uniform:** the mode axis (run / project / library / `-n`) should never change WHERE a type lives; today it does four different ways. Rule count goes 4 → 1 (vs 4 → 2). The only cost is a one-time FQN move for single-file libraries compiled without `-n`, which are the rarest consumer shape.

---

## 14. The `position` parameter on `ResolveTypeAnnotation` (#2004, Decision 16)

**Context.** Ruling 4 makes `x: None` (value position) SPY0614 while `-> None` stays `void`. The resolver must therefore know WHERE the annotation sits. Today:

```csharp
// TypeResolver.cs:88 (+ ITypeResolver.cs:17, TypeResolverAdapter.cs:20)
SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation, bool bareGenericFillsFromContext = false)
```

73 call sites across the checker, resolver, name resolver, module loader, validators and services; ~10 are return-position (`ReturnType`).

**Options**

| | (A) No default — RECOMMENDED | (B) Default `Value` |
|---|---|---|
| Signature | `ResolveTypeAnnotation(TypeAnnotation? a, AnnotationPosition position, bool bareGenericFillsFromContext = false)` | `…, AnnotationPosition position = AnnotationPosition.Value, …` |
| Diff | every one of the 73 sites states `Return` or `Value` (mechanical; the interface + adapter too) | ~10 return-position sites change |
| Totality | **enforced by the C# compiler** — a new caller cannot compile without deciding; the plan's "totality assertion over the callers" is the build itself, plus the 9-position matrix test | not enforced: a new/overlooked return-position caller silently gets `Value` |
| Failure mode of a miss | none (won't build) | `-> None` on THAT path becomes **SPY0614 — a false refusal of valid code**. Hidden return-position callers exist outside the checker: `ModuleLoader` resolving an imported function's signature, lambda/function-type annotations, protocol/dunder signature checks, cache-served signatures. The `-> None` fixture corpus catches common paths, not these |
| Guard under (B) | a source scan "every `ResolveTypeAnnotation(` call on a line mentioning `ReturnType` passes `Return`" — heuristic; passes vacuously when the call is spelled through a local | — |
| Door | two-way: a default can be added later in one line | two-way but the reverse costs the same 73-site touch later, after the misses have shipped |

**Why (A):** the C# compiler is the cheapest and only complete totality instrument here; the 73-site diff is a one-hour mechanical edit and the cost of a miss under (B) is a false refusal in a rarely-exercised path (warm cache, imported signatures) — exactly the class this round's #2027 is cleaning up.

Sketch of the enum:

```csharp
public enum AnnotationPosition { Value, Return }   // room for TypeArgument etc. later
```
