# C# 14 user-defined compound assignment — feasibility for alias-mutating augmented assignment

**Issue:** #1428 · **Status:** feasibility note only — no product code, no emitter work.
**Gates:** an owner decision on semantics. This note stops there deliberately.

## Why this exists

`xs += [1]` on a Sharpy `list` rebinds. In CPython it mutates in place, so every other name
bound to the same list observes the change. #1394 shipped a *transition hint* for exactly the
shapes where that difference is observable — `AugmentedCollectionAssignment.IsAliasObservable`
(`src/Sharpy.Compiler/Semantic/AugmentedCollectionAssignment.cs:42`), consumed by
`TransitionWarningValidator` (`:377`).

A hint is a warning about a semantic gap, not a closing of it. C# 14 shipped a language feature
that could close it, and #1428 asks the prior question: *is the mechanism actually available to
us, and what would adopting it cost?* This note answers the mechanism half with executed probes.
It does not propose adopting anything.

## Probe 1 — does the shape work on `net10.0`?

Hand-written C# control, `net10.0`, no explicit `LangVersion` (so the SDK default applies):

```csharp
public class Counter
{
    public int Value;
    public Counter(int v) => Value = v;

    public void operator +=(int x) => Value += x;   // the C# 14 shape
}

var c = new Counter(10);
var alias = c;
c += 5;
// value=15 aliasSeesIt=15 sameObject=True
```

**Result: compiles and runs.** Output: `value=15 aliasSeesIt=15 sameObject=True`.

The `aliasSeesIt` and `sameObject` columns are the load-bearing ones, and they are why this
feature is relevant to #1394 at all: the operator mutates the receiver **in place** rather than
producing a new object and rebinding. That is CPython's `list.__iadd__` semantics, expressed in
C# without a wrapper type. A `static operator +` returning a new instance — the pre-C#-14 shape —
cannot express it, which is what made the gap look closed-by-necessity before.

## Probe 2 — does the *generated* compilation admit it?

Sharpy parses its emitted C# with `CSharpParseOptions.Default` at three sites —
`Project/ProjectCompiler.CodeGen.cs:132`, `:141`, and `Project/ProjectCompiler.Generators.cs:155`
— against `Microsoft.CodeAnalysis.CSharp` **5.6.0** (`Sharpy.Compiler.csproj:34`).

Measured directly against that Roslyn version:

```
CSharpParseOptions.Default.LanguageVersion = CSharp14
Default  : 0 error(s)
CSharp14 : 0 error(s)
CSharp9  : 1 error(s)
    CS8773: Feature 'user-defined compound assignment operators' is not available
            in C# 9.0. Please use language version 14.0 or greater.
```

**Result: admitted with no parse-option change.** `LanguageVersion.Default` resolves to `CSharp14`
under the Roslyn we ship against, so the emitter could emit this shape today without touching
parse options.

### A methodological warning for whoever picks this up

The first version of this probe tested at the **parse** level and reported `0 errors` for *both*
`Default` and the `CSharp9` control. That reading is worthless: the C# parser accepts the operator
declaration syntax at every language version, and the version gate is a **binding** diagnostic.
A parse-level probe cannot distinguish "admitted" from "not yet rejected", and would have recorded
a confident false positive.

The numbers above come from `CSharpCompilation.GetDiagnostics()`, where the `CSharp9` control
**does** fire CS8773. Run the control that is supposed to fail; if it passes, the instrument is
measuring nothing.

## Probe 3 — the `netstandard2.1` story

`Sharpy.Core` multi-targets, with a deliberate language-version split
(`src/Sharpy.Core/Sharpy.Core.csproj:4-6`):

```xml
<TargetFrameworks>net10.0;netstandard2.1</TargetFrameworks>
<LangVersion Condition="'$(TargetFramework)' == 'netstandard2.1'">9.0</LangVersion>
<LangVersion Condition="'$(TargetFramework)' == 'net10.0'">14</LangVersion>
```

Probe 2's control **is** the netstandard2.1 measurement: the `netstandard2.1` leg compiles at
`LangVersion 9.0`, which is exactly the configuration that produced CS8773. So an
`operator +=` on `Sharpy.List<T>` / `Set<T>` / `Dict<K,V>` cannot be declared unconditionally —
it must sit behind `#if NET10_0_OR_GREATER`, and Critical Rule 5 applies.

The consequence to weigh is not build breakage but **semantic bifurcation**: `xs += [1]` would
mutate in place for a `net10.0` consumer and rebind for a `netstandard2.1` one. Same source, same
Sharpy version, two different answers depending on the consumer's TFM. That is a heavier cost
than the usual `#if` — those normally gate *availability*, not *meaning*.

## Semantics options

Mechanism is settled; semantics are not. Three options, none endorsed here:

1. **Adopt as THE semantics.** `+=` on a Sharpy collection mutates in place, matching CPython.
   Closes the #1394 gap rather than warning about it. Cost: the TFM bifurcation above, plus it is
   a behaviour change for existing `net10.0` programs that rely on rebinding.
2. **Experimental feature flag.** Register in `FeatureFlags.KnownFeatures`, gate through
   `FeatureGateChecker` (ungated use → SPY0331), graduate or delete per
   [feature-lifecycle.md](feature-lifecycle.md). Lets the semantics be measured on real programs
   before committing, and confines the bifurcation to opted-in code.
3. **Decline.** Keep #1394's hint as the permanent answer. The gap stays open and documented, and
   Axiom 1 (.NET semantics) continues to win over Axiom 2 (Python syntax) here.

## Where the lowering would key off

If any option other than (3) is chosen, the decision point already exists.
`AugmentedCollectionAssignment.IsAliasObservable` (`AugmentedCollectionAssignment.cs:42`) is
already the shared semantic query that decides whether a given augmented assignment is one where
the difference is observable — the seam Phase 3 of the batch built as a shared query rather than
validator-private logic, precisely so a future lowering could consume it.

Per Critical Rule 2 the emitter may not re-derive that judgement: it would have to arrive as
materialized state — node-keyed in `SemanticInfo` (and therefore **added to
`SemanticInfo.MergeFrom`**, or its entries vanish in the per-file→project merge) or symbol-keyed
on `CodeGenInfo`.

One further consistency note: `Services/CompilerInvariants.cs:403` independently pins
`new CSharpParseOptions(LanguageVersion.Latest)`. It agrees with `Default` today — both resolve to
`CSharp14` — but it is a second site to keep in step if explicit version pinning is ever
introduced.

## Probe reproduction

Both probes are a few lines each and are cheap to re-run when the SDK or Roslyn version moves.
Probe 1 is a `net10.0` console app declaring `public void operator +=(int x)` and printing whether
an alias observes the mutation. Probe 2 is a console app referencing
`Microsoft.CodeAnalysis.CSharp` 5.6.0 that builds a `CSharpCompilation` over the same declaration
at `Default`, `CSharp14` and `CSharp9`, and reports error counts per version — **with the
`CSharp9` control asserted to fail.**

Executed 2026-08-13 against .NET 10.0.400 SDK and Roslyn 5.6.0.
