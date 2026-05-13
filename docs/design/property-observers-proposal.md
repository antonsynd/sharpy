# Property Observers (`willset`/`didset`) — Proposal

**Issue:** [#416](https://github.com/antonsynd/sharpy/issues/416)
**Status:** Proposal (not committed to roadmap)
**Extracted from:** [feature_roadmap.md](../language_specification/feature_roadmap.md)

---

Swift-inspired property observers for side effects on auto-properties. This is a proposal — not yet part of the committed language design.

## Proposed Syntax

```python
class Character:
    property health: int
        didset:
            print(f"health changed from {oldvalue} to {self.health}")
        willset(new_value):
            assert new_value >= 0
```

## Open Questions

- Does this add enough value over converting to a function-style property with custom getter/setter?
- Should `oldvalue` be a contextual keyword or an explicit parameter?
- How does this interact with `@override` properties?
- Is this orthogonal to events, or does it overlap?

See [#416](https://github.com/antonsynd/sharpy/issues/416) for ongoing discussion.
