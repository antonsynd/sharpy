# Diagnostic Suppression (`@suppress`)

The `@suppress` decorator silences specific **warning**, **hint**, and **info** diagnostics within the scope of an annotated declaration. It is the fine-grained, in-source counterpart to the project-wide [`<NoWarn>`](#relationship-to-project-wide-nowarn) setting.

## Syntax

`@suppress` takes one or more diagnostic-code string literals:

```python
@suppress("SPY0451")
def scratch() -> None:
    x = 5          # SPY0451 (unused variable) would fire here — silenced by the decorator
    print("done")

@suppress("SPY0451", "SPY0450")   # several codes at once
def experimental() -> None:
    ...
```

## Scope

A suppressor covers the **entire annotated declaration, including its body** — the decorated function, class, struct, interface, enum, union, property, event, or field. A diagnostic is silenced only when its source location falls inside that span and its code is one of those listed.

```python
@suppress("SPY0451")
def a() -> None:
    unused = 1     # silenced — inside a()'s scope

def b() -> None:
    unused = 1     # SPY0451 still fires — outside any suppressor
```

### Statement scope

`@suppress` may also prefix an individual **expression statement or assignment** inside a body — *call-site* suppression, useful for silencing a single diagnostic without widening the scope to the whole enclosing function. This is the idiomatic way to opt out of the [must-use warning](tagged_unions_result.md#must-use-warning-spy0480) at one call site:

```python
def main() -> None:
    @suppress("SPY0480")
    try risky()          # this one discard is intentional; the rest of main() still warns
    result = try risky() # bound normally
```

Statement scope is limited to expression statements and assignments; `@suppress` on an `import`, or any non-`@suppress` decorator on a statement, keeps the ordinary "decorators can only be applied to …" parse error.

## What can be suppressed

Only **Warning**, **Hint**, and **Info** diagnostics are suppressible. **Errors are never silenceable** — regardless of their numeric code range — and must be fixed. This holds even under `--warnings-as-errors`: a warning that was *promoted* to an error by that flag remains suppressible (its original severity is remembered), mirroring C#'s `#pragma warning disable` under `/warnaserror`.

## Suppression-related diagnostics

| Code | Severity | When |
|---------|----------|------|
| `SPY0481` | Warning | An `@suppress` lists codes but silenced nothing in its scope (an ineffective suppression that can be removed). Not reported when the file already has errors. |
| `SPY0482` | Warning | A listed code cannot be suppressed: it is malformed (not `SPYnnnn`), unrecognized, or names an error-severity diagnostic. |

Argument *shape* errors — no arguments, keyword arguments, or a non-string-literal argument — are reported as `SPY0322` (invalid decorator usage), consistent with `@deprecated`.

```python
@suppress("SPY9999")       # ⚠ SPY0482: not a recognized diagnostic code
def f() -> None: ...

@suppress("SPY0201")       # ⚠ SPY0482: SPY0201 is an error and cannot be suppressed
def g() -> None: ...

@suppress("SPY0451")       # ⚠ SPY0481: nothing here is unused, so the suppression is ineffective
def h() -> None:
    x = 1
    print(x)
```

## Relationship to project-wide `<NoWarn>`

`@suppress` is deliberately **local**. To silence a diagnostic across an entire project, list its code in the `<NoWarn>` element of the `.spyproj` file instead:

```xml
<NoWarn>SPY0451;SPY0452</NoWarn>
```

Use `<NoWarn>` for a policy decision that applies everywhere; use `@suppress` for a deliberate, documented exception in one place.

## See Also

- [Must-Use Warning](tagged_unions_result.md#must-use-warning-spy0480) — `SPY0480`, a common `@suppress` target
- [Decorators](decorators.md) — general decorator syntax
