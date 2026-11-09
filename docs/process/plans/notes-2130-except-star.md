# Notes for #2130 — `except*` (PEP 654), split out of P33

NOT a plan. Research carried over from the P33 draft (`plan-638e8b`) when the owner split #2130 into its own group on 2026-11-09 ("split"). The new group has no charter row yet: it is placed and lettered at the next `/batch-issues`, and its plan is written by `/create-plan` at full tier.

## Tier facts for the charter row

- **Tier:** full (b, c). (c): the cure seam is `CodeGen/` — the catch-clause shape in `GenerateExceptStarCatchClauses` (`RoslynEmitter.Statements.ControlFlow.cs:1896`); no Core change can make `catch (AggregateException)` see a naked exception. (b): the matrix below is ≥2 axes × ≥3 values, generator required first.
- **Alone:** no snapshot moves — zero `.expected.cs` files contain `AggregateException` (grep @ 2c803eb32). Printed output changes only for programs that are wrong today.
- **Lane / reach:** silent-wrong · program. **Fidelity-scope row:** COMPARED row 3 (exception TYPE raised, and the hierarchy).
- **Rulings in force:** R-MD (`ExceptionGroup : AggregateException` is the one bridge left); R-MO (a CodeGen cell does not ride P33); ledger row `exception-group-aggregate-exception-bridge` (no `BaseExceptionGroup`).
- **Trackers:** #2363 keeps the except-handler ordering cells (P13b).

## Measured at base @ 2c803eb32, python3 3.13.18, under `run`

Of 6 cells {naked, group, nested group} × {matching, non-matching}, 5 diverge:

| Cell | python3 | Sharpy |
|---|---|---|
| naked, matching (#2130) | handler runs, `eg.message` is `""` | unhandled by `except*`; the outer `except ValueError` sees it |
| group, matching | handler runs, `eg.message` is `g`; rest escapes as `ExceptionGroup` `g` | handler runs, `eg.message` is `""`; rest escapes as `System.AggregateException`, missed by `except ExceptionGroup` |
| nested group, matching | handler runs on the nested leaf; rest keeps the `outer` message | not matched; rethrown as a new `System.AggregateException` |
| naked, non-matching | propagates naked | the same (the one agreeing cell) |
| group, non-matching | the same group propagates | rethrown as a new `System.AggregateException` |
| nested group, non-matching | the same group propagates | rethrown as a new `System.AggregateException` |

Seam as read: every `except*` try lowers to one `catch (System.AggregateException)`, filters `InnerExceptions.OfType<T>()` one level deep, binds `as` to a fresh `ExceptionGroup("", …)` and rethrows the rest as a plain `System.AggregateException`. The `as` binding's type is set at `TypeChecker.Statements.cs:1758`.

## Matrix (generator axes, as drafted)

**C1 `except*` (G2).** Axes: *raised shape* = a closed grammar `naked | group[leaf+] | group[group[leaf+], leaf*]` (depth ≤ 2; deeper nesting adds no new arm of the recursive split); *leaf relation to the handler type* = closed 3-value {exact, subclass, unrelated}; *handler list* = {one handler, tuple type, two handlers in both orders}; *handler action* = an enum {Pass, RaiseNew, BareRaise}; *binding* = {`as eg`, none}. **Rule:** stdout of the generated Sharpy program equals python3's for the same cell, where each cell prints which handlers ran, `len(eg.exceptions)` and `eg.message`, and how an outer `try` classifies what escapes (naked type / group message + leaf count / nothing). Excluded axes: `BaseExceptionGroup` (ledger row `exception-group-aggregate-exception-bridge`), `return`/`break`/`continue` in a handler (already refused), user subclasses of `ExceptionGroup` (`Derive` exists; one hand cell, not an axis).

## Design as drafted

**D1 — `except*` semantics live in Core; the emitter emits one call sequence (rung 2 + one CodeGen arm; §12 c).** `ExceptionGroup.Split` in `src/Sharpy.Core/ExceptionGroup.cs` already implements PEP 654's recursive, structure-preserving split through `Derive` (which keeps the message). Core gains one small runtime helper beside it (the implementer names it; the contract is the three operations): *begin* — take any caught `System.Exception`; an `ExceptionGroup` is the group, a plain .NET `AggregateException` is bridged to a group over its inner exceptions, anything else is wrapped as `ExceptionGroup("", [ex])` and remembered as naked; *match by type* — split the REMAINING group on a type test, hand back the matched subgroup (or nothing) and keep the rest; *end* — nothing matched and nothing raised → rethrow the ORIGINAL object with its stack (`ExceptionDispatchInfo`), so a naked non-match stays naked and a group non-match stays the same group; otherwise raise the rest (derived from the original), merged with anything a handler raised into `ExceptionGroup("", …)` as python does; nothing left → fall through. The emitter's `GenerateExceptStarCatchClauses` changes shape once: `catch (System.Exception)`, one *match* per handler with the handler body inside a `try` that reports a raised exception to the helper, then *end*. No type decision moves into the emitter (Rule 2: it already maps the handler's written type; the `as` binding's type stays `ExceptionGroup`, set at `TypeChecker.Statements.cs:1758`). Why not lower: rung 1 cannot express a catch clause; the catch type itself is emitted C#. Predicate the adequacy reader checks: after D1 the emitter contains no `OfType`, no `InnerExceptions` and no `new AggregateException` for `except*`.

## Generator acceptance as drafted

**A2 — G2, `except*` differential (Compiler.Tests, beside `FormatSpecDifferentialTests`).** Generates the C1 cells, compiles and RUNS each through the fixture harness, compares stdout with python3. Allowlist rows cite #2130; drains to 0 with D1. **Mutations:** (a) make *begin* stop wrapping a naked exception → every naked-match cell red; (b) make *end* always raise a new group → naked-non-match and group-non-match cells red (identity of what escapes); (c) make *match* one level deep → nested cells red. **Positive controls:** `except_star_basic` and `except_star_multiple` keep their `.expected` unchanged; a fixture where .NET throws a plain `System.AggregateException` into `except*` is caught before and after.

## Docs deliverable

`docs/language_specification/exception_handling.md`, section "Exception Groups and `except*`": state the naked-exception rule, the recursive match and what escapes, each with an executed example whose output discriminates (the naked case prints `caught`; the non-match case prints the outer handler's line). The spec today says "inspired by Python PEP 654" and is silent on all three.

## Side cell for the ledger

`[ValueError("v"), TypeError("t")]` is SPY0227 (no best common type) @ 2c803eb32, so every group literal in a generated cell needs a `list[Exception]` annotation.

## Probe program (python3; delete the last line and save as `.spy` for Sharpy)

```python
def cell(name: str, k: int) -> None:
    try:
        try:
            if k == 0:
                raise ValueError("v")
            elif k == 1:
                a: list[Exception] = [ValueError("v"), TypeError("t")]
                raise ExceptionGroup("g", a)
            elif k == 2:
                i: list[Exception] = [ValueError("v")]
                b: list[Exception] = [ExceptionGroup("inner", i), TypeError("t")]
                raise ExceptionGroup("outer", b)
            elif k == 3:
                raise KeyError("k")
            elif k == 4:
                c: list[Exception] = [KeyError("k")]
                raise ExceptionGroup("g", c)
            else:
                j: list[Exception] = [KeyError("k")]
                d: list[Exception] = [ExceptionGroup("inner", j)]
                raise ExceptionGroup("outer", d)
        except* ValueError as eg:
            print(name, "star caught", len(eg.exceptions), eg.message)
    except ValueError:
        print(name, "outer ValueError")
    except KeyError:
        print(name, "outer naked KeyError")
    except ExceptionGroup as rest:
        print(name, "outer group", rest.message, len(rest.exceptions))
    except Exception as e:
        print(name, "outer other", type(e))

def main() -> None:
    cell("naked-match", 0)
    cell("group-match", 1)
    cell("nested-match", 2)
    cell("naked-nomatch", 3)
    cell("group-nomatch", 4)
    cell("nested-nomatch", 5)

main()
```
