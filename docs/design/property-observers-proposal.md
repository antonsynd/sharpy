# Property Observers (`before_set`/`after_set`) — Proposal

**Issue:** [#416](https://github.com/antonsynd/sharpy/issues/416)
**Status:** Experimental (as `property_observers`) — shipped disabled-by-default per [feature-lifecycle.md](feature-lifecycle.md); carries no stability promise until it graduates.
**Extracted from:** [feature_roadmap.md](../language_specification/feature_roadmap.md)

---

Swift-inspired property observers for side effects on auto-properties. Shipped experimental behind the
`property_observers` flag (Parser scope): enable compilation-wide with
`--enable-feature=property_observers` or `<Features>property_observers</Features>` in a `.spyproj`.
Ungated use of an observer clause reports **SPY0331**.

## Syntax

Observer clauses are indented under a settable auto-property. Each takes an explicit, user-named
parameter — there is no magic `oldvalue` contextual keyword (Design Decision 9; the anti-magic
anti-pattern). `before_set` and `after_set` are contextual identifiers, special only in observer
position.

```python
class Character:
    property health: int = 100
        before_set(new_value):
            assert new_value >= 0          # incoming value
        after_set(old_value):
            print(f"{old_value} -> {self.health}")   # previous value; self.health is the new value
```

## Semantics

- **Valid target:** a settable auto-property only (`property name: type`, optionally with a default).
  Observers on function-style, `@readonly`/get-only/init-only, `@abstract`, `@override`, or interface
  properties are errors (**SPY0490**). A duplicate observer of the same kind is **SPY0491**.
- **Parameter types:** the `before_set` parameter is the property type (the incoming value); the
  `after_set` parameter is the property type (the previous value).
- **Lowering:** the auto-property becomes a private backing field plus an expanded setter:

  ```csharp
  private int _health = 100;
  public int Health
  {
      get { return _health; }
      set
      {
          var __old = _health;      // only when after_set exists
          { /* before_set body, parameter → value */ }
          _health = value;
          { /* after_set body, parameter → __old */ }
      }
  }
  ```

- **Every store runs the observers, including constructor assignments** (Design Decision 9 — no
  Swift-style init exemption; explicit over magic). The field-initializer default does *not* route
  through the setter, so it does not fire observers. A fixture pins constructor-assignment firing
  (`TestFixtures/experimental/property_observers_gated.*`).

## Open Questions (for graduation)

- Does this earn its place over converting to a function-style property with a custom setter?
- Should observers ever be allowed on `@override` properties (relaxed from the v1 conservative error)?
- Is this orthogonal to events, or does it overlap?

Graduation (flag → no-op, then removed) follows the [feature lifecycle](feature-lifecycle.md) once the
exit criteria are met. See [#416](https://github.com/antonsynd/sharpy/issues/416) for discussion.
