# Rejected Proposals

This directory documents language features that were proposed, evaluated, and deliberately rejected. Each proposal explains the feature, the motivation, and the reasons for rejection.

These serve as:
- **Negative examples** for the language axioms and design principles
- **Historical record** preventing re-proposal of settled questions
- **Design guidance** for future feature development

## Axioms (for reference)

Precedence: **Axiom 1 (.NET Runtime Compatibility) > Axiom 3 (Type Safety) > Axiom 2 (Python Syntax)**

## Design Anti-Patterns

| Pattern | Problem |
|---------|---------|
| "Add X because Python has it" | Feature creep — each feature must earn its complexity |
| Runtime type checking | Should be compile-time |
| Magic behavior | Unpredictable; prefer explicit |
| Multiple ways to do same thing | Consistency issue |
| Wrapper types for Pythonic API | Use extension methods instead |

## Index

| # | Proposal | Status | Primary Axiom Conflict |
|---|----------|--------|----------------------|
| SRP-0001 | [`@kwargs` decorator](SRP-0001-kwargs-decorator.md) | Rejected | Anti-pattern: magic behavior |
| SRP-0002 | [`@dynamic_kwargs` decorator](SRP-0002-dynamic-kwargs-decorator.md) | Rejected | Axiom 3: type safety |
| SRP-0003 | [Events with function type syntax](SRP-0003-events-function-type-syntax.md) | Rejected | Axiom 1: function types → `Action<T>`, not `EventHandler` |
| SRP-0004 | [Events with nested accessor syntax](SRP-0004-events-nested-accessor-syntax.md) | Rejected | Consistency: inconsistent with property accessor pattern |
| SRP-0005 | [`as` casting operator](SRP-0005-as-casting-operator.md) | Rejected | Anti-pattern: multiple ways to do same thing |
