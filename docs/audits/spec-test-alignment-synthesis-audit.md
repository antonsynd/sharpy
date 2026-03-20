Synthesized Spec-vs-Test Alignment Audit

Below is a unified, case-by-case review of every finding across both audit
passes. I've deduplicated overlapping items, resolved contradictions between
the two reports, and added my engineering assessment for each.

---
DIVERGENCES — Spec contradicts implementation

These are the highest-priority items. Each requires a decision: fix spec or
fix implementation.

DIV-1. Decimal Literal Suffix m/M [HIGH]

Pass 1 D1. Spec (float_literals.md) documents 3.14m creating a decimal type.
Compiler rejects it with an error. The decimal type in primitive_types.md also
has zero positive test coverage.

Assessment: This is a prematurely documented feature. The decimal type isn't
wired through the type system or codegen. Fix: move m/M suffix and decimal 
type to deferred_features.md, add a note in primitive_types.md that decimal is
reserved but not yet available. Don't implement — there's no user demand
signal.

ANTON: Let's do this. It aligns with C# and someone will want it.

---
DIV-2. Variadic Args Position — Spec self-contradiction [HIGH]

Pass 1 D2. function_variadic_arguments.md says *args must be last.
flexible_arguments.md (correctly) says *args acts as keyword-only separator.
Tests confirm keyword-only params after *args work.

Assessment: Straightforward spec bug. function_variadic_arguments.md line
50-65 was likely written before keyword-only args were designed. Fix: update 
function_variadic_arguments.md to say *args can precede keyword-only
parameters, cross-reference flexible_arguments.md. No implementation change
needed.

ANTON: Agree, let's fix the spec bug.

---
DIV-3. Float Leading Decimal .5 [HIGH]

Pass 2 D1. Spec (float_literals.md) says .5 is valid (matching Python). Lexer
rejects it. Lexer test comment says "v0.1 spec requires digit before decimal
point."

Assessment: This is a policy question. Python allows .5, C# allows .5, but
requiring a leading digit (0.5) is arguably better for readability and avoids
ambiguity with the member-access dot. The implementation chose the stricter
rule. Fix: update spec to require leading digit (0.5 not .5), document this as
a deliberate divergence from Python with rationale. The implementation is
correct here.

ANTON: We should align with Python and C#, they both allow `.5` so we should too.

---
DIV-4. divmod() → div_mod() Name Mangling [MEDIUM]

Pass 2 D2. Spec documents divmod(a, b). Tests call div_mod(17, 5). The name
mangling is silent and undocumented — a user reading the spec would write
divmod() and get a compile error.

Assessment: This is a documentation gap with real user impact. The name
mangler converts divmod → DivMod (PascalCase for C#), but the Sharpy-facing
name becomes div_mod due to the compound-word splitting heuristic. Fix: 
document in builtin_functions.md that the callable name is div_mod, and add a
row in name_mangling.md's special-case table. Consider whether the name
mangler should have an exception list for well-known Python builtins where the
split is wrong.

ANTON: The function as implemented should be `Divmod()` then, I don't think it's
maintainable to have an exception list. Every standard library function in
Sharpy should adhere to the naming convention to yield the correct snake casing.

---
DIV-5. Lambda Default Params vs Function Type Spec [MEDIUM]

Pass 1 D3. function_types.md says "Function types cannot specify optional
parameters." But lambda expressions successfully use default params.

Assessment: The spec conflates two distinct concepts: function type 
annotations (the type signature (int, int) -> int) vs lambda expressions (the
value lambda x, y=0: x + y). The restriction correctly applies to type
annotations only — you can't write (int, int=0) -> int as a type. Fix: reword 
function_types.md to clarify the restriction applies to type annotation 
syntax, not to lambda/function definitions. Add a cross-reference to lambda
docs.

ANTON: Let's fix the specs. Also, let's create a github issue for type annotations
on args in lambda definitions. I'm having second thoughts about those because
it looks weird to have two colon-groups, `lambda x: int: x + 1`. It makes
parsing ugly and it's one reason why I opted to not do the same thing for
custom properties which would've looked like `property name: str: ...`
and instead went with `property get name(self) -> str: ...`. (Well, one of the
reasons).

---
DIV-6. Integer Literal Overflow Range [MEDIUM]

Pass 1 D4. Spec says integers are inferred as int (32-bit). Error test says
"too large for a 64-bit integer."

Assessment: The compiler's actual behavior is: literals default to int
(32-bit), auto-promote to long (64-bit) if too large for 32-bit, and error if
too large for 64-bit. The spec only describes step 1. Fix: document the 
promotion chain in integer_literals.md: int → long → error. This is standard
behavior (C# does the same) and worth making explicit.

ANTON: Agree, let's fix the spec.

---
DIV-7. Scientific Notation Output Format [LOW]

Pass 1 D5. .NET outputs 1E+20 (uppercase E), Python uses 1e+20. Not documented
anywhere.

Assessment: Axiom 1 trade-off, already annotated in the test fixture comment.
Fix: add one line to extended_numeric_literals.md noting uppercase E in
output. Trivial.

ANTON: I disagree. This is where Pythonic syntax should take over, because
in reality, it's not a feature disagreement, it's just a cosmetic thing. I think
Axiom 1 wins for feature/semantics rather than cosmetic things.

---
DIV-8. Exception Hierarchy Incomplete in Spec [LOW]

Pass 2 D3. Spec documents 10 exception aliases, implementation provides 17+.

Assessment: The spec was written early and never updated. Fix: update 
exception_handling.md with the full alias table from Sharpy.Core. Mechanical
update.

ANTON: Agree, let's update it.

---
OUTDATED SPEC — Stale passages

OUT-1. Optional/Result "Planned for Later" [HIGH]

Pass 2 O1. tagged_unions_optional.md and tagged_unions_result.md say "planned
for a later phase." Both are fully implemented with 20+ passing tests.

Assessment: Misleading for anyone reading the spec to evaluate Sharpy's
capabilities. Fix: remove the "planned" language, replace with implementation
notes. Straightforward.

ANTON: Agree, let's update it.

---
OUT-2. __getitem__/__setitem__ "Not Yet Implemented" Banner [MEDIUM]

Pass 2 O2. operator_overloading.md has a known-gaps banner referencing
#276/#277, but class_indexer.spy and class_readonly_indexer.spy pass.

Assessment: Check whether issues #276/#277 are closed. If so, remove the 
banner. If the issues track remaining edge cases, narrow the banner to
describe what's actually missing.

ANTON: Agree, let's check. I think it is implemented though.

---
CRITICAL GAPS — Defined behavior with zero test coverage

These are the items where regressions would be invisible. I'm prioritizing by
blast radius.

GAP-1. Comparison Chaining — ZERO tests [CRITICAL]

Both passes flagged this. comparison_chaining.md defines a < b < c semantics
including single-evaluation guarantee and short-circuit behavior. No test
fixture, no unit test.

Assessment: This is a parser + codegen feature with meaningful lowering
complexity (temp variable for middle expression, short-circuit &&). If the
lowering breaks, nothing catches it. Priority: immediate. Add 4-5 fixtures:
- Basic: 1 < 2 < 3 → True
- Failure: 1 < 3 < 2 → False
- Mixed ops: 1 <= 2 < 3 != 4
- Single eval: counter class proving middle evaluated once
- Short-circuit: second comparison not evaluated when first is false

ANTON: Agree, let's add tests, and implement it if it's not finished or correct.

---
GAP-2. UTF-16 String Semantics — ZERO tests [CRITICAL]

Pass 1 G1. string_type.md defines len("😀") = 2 (UTF-16 code units). No tests.

Assessment: This is the single most likely source of user confusion for anyone
coming from Python. The Axiom 1 decision is already made (UTF-16 wins), but
there's zero regression coverage. Priority: high. Add 3 fixtures:
- len("😀") → 2
- "😀"[0] → surrogate half (or whatever the actual behavior is)
- Slicing across surrogate boundaries

Caveat: Before writing tests, verify the actual implementation behavior with
/quick-check. The spec may describe aspirational behavior that isn't
implemented.

ANTON: Agree, let's check, add tests, and implement it if it's not finished or correct.

---
GAP-3. Comprehension Variable Scoping — ZERO leak tests [CRITICAL]

Both passes flagged this. comprehensions.md defines that comprehension
variables don't leak. No test validates that accessing a comprehension
variable outside produces a compile error.

Assessment: This is a fundamental scope boundary. If it breaks, variables
silently leak into enclosing scope (or the reverse — break user code that was
working). Priority: high. Add 2 fixtures:
- Negative: access comprehension variable after comprehension → compile error
- Positive: same-named variable in enclosing scope not clobbered by
comprehension

ANTON: Agree, let's check, add tests, and implement it if it's not finished or correct.

---
GAP-4. Walrus Operator in Comprehensions — ZERO tests [CRITICAL]

Both passes flagged this. Sharpy deliberately differs from Python 3.8+ here
(walrus variables are comprehension-local, not leaking to enclosing scope).
Zero validation.

Assessment: This is a Sharpy-specific semantic choice that would be extremely
confusing if it regressed to Python behavior. Priority: high. Add 1-2 fixtures
demonstrating comprehension-local walrus.

ANTON: Agree, let's check, add tests, and implement it if it's not finished or correct.

---
GAP-5. class/struct Type Constraints — ZERO tests [CRITICAL]

Pass 1 G2. generics.md documents T: class, T: struct, compound constraints
like T: class & IFoo. Only interface constraints tested.

Assessment: Need to verify implementation status first. If implemented, add
positive + negative tests. If not, move to deferred. The & multi-constraint
syntax (Pass 1 G4) is the same issue — verify before testing.

ANTON: Agree, let's check, add tests, and implement it if it's not finished or correct.

---
GAP-6. array[T] Type — ZERO tests [CRITICAL]

Pass 1 G3. Spec documents array[T], int[] postfix, Array[int](lst)
constructor. No tests anywhere.

Assessment: Almost certainly not implemented. Arrays are a .NET interop
concern that list[T] covers for most cases. Fix: move to deferred_features.md
with a note about when/why arrays would be needed (perf-sensitive code, .NET
API interop requiring T[]).

ANTON: Agree. To be honest, we should actually implement this so it works, and add tests of course.

---
GAP-7. Named Tuple Pattern Matching — ZERO tests [HIGH]

Pass 2 G5. Entire spec section on case (x=0.0, y=0.0) syntax for named tuple
matching. Zero tests.

Assessment: Verify implementation status. Named tuples themselves likely work,
but pattern matching integration with named field syntax may not be wired
through the match lowering. Priority: verify, then either add tests or move to
deferred.

ANTON: Agree, let's check, add tests, and implement it if it's not finished or correct.

---
GAP-8. Circular Import with Type Annotations [HIGH]

Both passes flagged this. Spec says circular references are allowed for type
annotations but not base classes. Implementation rejects ALL circular imports
unconditionally.

Assessment: This is a spec-vs-implementation divergence disguised as a gap.
The spec promises functionality the implementation doesn't provide. Decision 
needed: Is type-annotation-only circular import worth implementing? It
requires import resolution to distinguish "importing for type use" vs
"importing for runtime use." If not worth it now, update spec to say all
circular imports are rejected, add a deferred note.

ANTON: Create a github issue for this. Let's discuss it later.

---
GAP-9. Type Narrowing End-to-End [HIGH]

Pass 2 G7. TypeNarrowingContext infrastructure is tested at unit level, but no
integration test shows real narrowing (e.g., if x is not None: x.upper()).

Assessment: The unit tests cover the mechanism, but an end-to-end test catches
wiring issues between the narrowing context and the type checker's branch
handling. Add 2-3 integration fixtures covering is not None, isinstance(), and
match-case narrowing.

ANTON: Agree, let's add tests, and implement it if it's not finished or correct.

---
GAP-10. Comprehension Duplicate Variable Error [HIGH]

Both passes flagged this. [x for x in range(3) for x in range(3)] should be a
compile error per spec. No test.

Assessment: Quick negative test to add. Add 1 .error fixture.

ANTON: Agree, let's add the tests, and fix it if it's not finished or correct.

---
GAP-11. Try/Maybe Expression Precedence [HIGH]

Pass 2 G8. try foo() if cond else bar() has a spec-defined precedence rule. No
test.

Assessment: This is a parser ambiguity landmine. If precedence changes,
expressions silently get different semantics. Add 1-2 fixtures validating the
parse order.

ANTON: Agree, let's add the tests, and fix it if it's not finished or correct.

---
GAP-12. Loop-Else with return and raise [HIGH]

Both passes flagged this. Spec says else clause doesn't run on return or
raise. Tests only verify break.

Assessment: The break case tests the boolean-flag lowering, but return and
raise exercise different control flow paths (return exits the method, raise
unwinds the stack). Add 2 fixtures: loop-else with return, loop-else with
raise.

ANTON: Agree, let's add the tests, and fix it if it's not finished or correct.

---
MEDIUM-PRIORITY GAPS

GAP-13. list.get() Method [MEDIUM]

Pass 2 G10. dict.get() well-tested, list.get() with safe Optional return —
zero tests.

Assessment: If list.get() is implemented, add a test. If not, either implement
(it's in the spec) or remove from spec.

ANTON: Agree, let's check, add a test, and fix it if it's not finished or correct.

---
GAP-14. type() and hash() Builtins [MEDIUM]

Pass 2 G13-G14. Both documented in spec, zero test coverage.

Assessment: Verify implementation, add tests or defer. hash() has an
interesting edge case with unhashable types (lists) that should be a
compile-time error per spec.

ANTON: Agree, let's check, add tests, and fix it if it's not finished or correct.

---
GAP-15. isinstance() with Generic Type Rejection [MEDIUM]

Pass 2 G12. isinstance(x, list[int]) should error due to generic erasure. No
test.

Assessment: Good negative test to add. This catches a common Python-to-Sharpy
migration mistake.

ANTON: Agree, let's add a test, and fix it if it's not finished or correct (i.e. the rejection should occur).

---
GAP-16. Struct Parameter Modifiers (in[], mut[], out[]) [MEDIUM]

Pass 2 G11. Detailed spec sections, zero tests.

Assessment: Almost certainly not implemented. Move to deferred if so.

ANTON: Agree, let's check, add tests, and create github issues for what isn't implemented.

---
GAP-17. type(None) Compile Error [MEDIUM]

Pass 1 G11. Spec says this should error. No test.

Assessment: Quick negative test. Add 1 .error fixture.

ANTON: Agree, let's add a test, and implement/fix it if it's not started/finished or not correct (i.e. the rejection should occur).

---
GAP-18. Type Alias at Class/Function Scope [MEDIUM]

Both passes flagged. Only module-level aliases tested.

Assessment: Verify that class-level and function-level type aliases actually
work. If they do, add tests. If not, narrow the spec.

ANTON: Agree, let's add a test, and implement/fix it if it's not started/finished or not correct.

---
GAP-19. Constraint Reordering [MEDIUM]

Pass 1 G13. Spec says compiler reorders constraints to match C# requirements.
All tests manually write correct order.

Assessment: Add a test where constraints are written in "wrong" order and
verify the compiler silently reorders them.

ANTON: Agree, let's add a test, and implement/fix it if it's not started/finished or not correct (i.e. the rejection should occur).

---
GAP-20. Pipe Operator Negative Tests [MEDIUM]

Pass 1 G9. Can't pipe to constructors or operators — no negative tests.

Assessment: Add 2 .error fixtures for these cases.

ANTON: Agree, let's add tests, and implement/fix it if it's not started/finished or not correct (i.e. the rejection should occur).

---
GAP-21. Tuple Rest-Patterns and Count Mismatch [MEDIUM]

Pass 2 G17-G18. Star-unpack only tested with lists, never tuples. SPY0239
count-mismatch error untested.

Assessment: Add 2-3 fixtures covering tuple rest patterns and count mismatch
error.

ANTON: Agree, let's add tests, and implement/fix it if it's not started/finished or not correct.

---
GAP-22. Match Expressions in Complex Contexts [MEDIUM]

Pass 2 G19. Match as function argument, in string concat, in ternary — none
tested.

Assessment: Add 1-2 fixtures showing match expression in non-trivial
positions.

ANTON: Agree, let's add tests, and implement/fix it if it's not started/finished or not correct.

---
GAP-23. Mixed Variance Delegates [MEDIUM]

Pass 2 G22. delegate Transformer[in TIn, out TOut] — covariant and
contravariant never combined.

Assessment: Add 1 fixture.

ANTON: Agree, let's add a test, and implement/fix it if it's not started/finished or not correct.

---
GAP-24. Base Class vs Interface Default Precedence [MEDIUM]

Pass 2 G23. Interface-vs-interface conflict tested, but
base-class-vs-interface not.

Assessment: Important for diamond-inheritance-like scenarios. Add 1 fixture.

ANTON: Agree, let's add a test, and implement/fix it if it's not started/finished or not correct.

---
GAP-25. Context Manager __exit__ Exception Parameters [MEDIUM]

Pass 2 G25, Pass 1 A1. Spec shows full Python signature __exit__(self, 
exc_type, exc_val, exc_tb). Tests use __exit__(self). These contradict.

Assessment: Since Sharpy maps with to C# using (which has no exception
parameter passing), the implementation is correct. Fix spec to document that
__exit__(self) is the Sharpy signature (Axiom 1 trade-off). The Python-style
signature should be removed from the spec.

ANTON: Let's create a github issue for this. I think the user should be able to define either `__exit__` with no args, or `__exit__` with the 3 exception args, to customize how they want to handle resources/exceptions. This needs some scoping/planning later.

---
GAP-26. Async Features (Comprehensions, Context Managers) [MEDIUM]

Pass 2 G27-G29. async for in comprehensions rejected, await in sync
comprehension rejected, async with — all documented, none tested.

Assessment: Async is a large feature area. Verify what's implemented. At
minimum, add negative tests for the documented rejections.

ANTON: Agree, let's add tests, and document gaps in github issues.

---
GAP-27. global/nonlocal Rejection [MEDIUM]

Pass 2 G30. Spec says these keywords are errors (C# scoping wins via Axiom 1).
No test.

Assessment: Quick negative test. Add 2 .error fixtures.

ANTON: Agree, let's add tests and make sure that they pass (i.e. rejection) by fixing the implementation.

---
GAP-28. Module-Level Variable Without Type Annotation [MEDIUM]

Pass 2 G31. x = 5 at module level should error, requiring x: int = 5. No test.

Assessment: Add 1 .error fixture.

ANTON: Agree, let's add the test, and implement/fix it if it's not started/finished or not correct (i.e. the rejection should occur).

---
LOW-PRIORITY GAPS

┌────────┬──────────────────────────┬────────┬────────────────────────────┐
│   #    │           Item           │ Source │         Assessment         │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-29 │ Float trailing decimal   │ P2 G32 │ Verify behavior, add test  │
│        │ 5.                       │        │                            │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-30 │ \b/\f escape sequences   │ P2 G34 │ Lexer handles these; add   │
│        │                          │        │ isolated test              │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-31 │ Octal string escapes in  │ P2 G35 │ Tests exist, spec needs    │
│        │ spec                     │        │ update — add to spec       │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-32 │ Backtick identifier E2E  │ P2 G36 │ Lexer tested, add          │
│        │                          │        │ integration fixture        │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│        │ Lambda loop closure      │        │ Document gotcha, add test  │
│ GAP-33 │ capture                  │ P2 G37 │ showing all-same-var       │
│        │                          │        │ capture                    │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│        │ Yield in nested          │        │ Add test showing inner     │
│ GAP-34 │ functions                │ P2 G38 │ generator doesn't          │
│        │                          │        │ propagate to outer         │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│        │ Positional-only +        │        │ Combinatorial edge case,   │
│ GAP-35 │ keyword-only + partial   │ P2 G39 │ low urgency                │
│        │ combined                 │        │                            │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│        │                          │        │ [[i*j for j in range(3)]   │
│ GAP-36 │ Nested comprehensions    │ P2 G40 │ for i in range(3)] — add   │
│        │                          │        │ test                       │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-37 │ Tuple spread literal     │ P2 G41 │ Add .error fixture         │
│        │ error                    │        │                            │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-38 │ del statement rejection  │ P2 G42 │ Add .error fixture         │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-39 │ Chained identity         │ P1 G20 │ a is b is c — verify +     │
│        │ operators                │        │ test                       │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-40 │ Mixed comparison chains  │ P1 G21 │ a < b in c — verify + test │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-41 │ Walrus type annotation   │ P1 G22 │ x: int := val → error —    │
│        │ error                    │        │ add test                   │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-42 │ Future keywords reserved │ P1 G18 │ defer, do — add reserved   │
│        │                          │        │ keyword tests              │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-43 │ @classmethod rejection   │ P1 G15 │ Add negative test          │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-44 │ Enum value mangling      │ P2 G44 │ CAPS_SNAKE_CASE →          │
│        │                          │        │ PascalCase — add test      │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-45 │ Struct constructor must  │ P2 G26 │ Add negative test          │
│        │ init all fields          │        │                            │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-46 │ Partial type argument    │ P2 G20 │ Must specify all or none — │
│        │ spec error               │        │  add negative test         │
├────────┼──────────────────────────┼────────┼────────────────────────────┤
│ GAP-47 │ Covariant return types   │ P2 G24 │ Verify + test              │
│        │ in properties            │        │                            │
└────────┴──────────────────────────┴────────┴────────────────────────────┘

ANTON: For the table above, I agree with all of them. One addition is that `@classmethod`
should yield an error message that tells the user that they should use `@static` instead.
Something similar should happen for `@staticmethod` if it doesn't already. Add a test for
this too if it doesn't exist already.

---
SPEC AMBIGUITIES — Need clarification

#: AMB-1
Area: is with value types
Issue: Spec says ReferenceEquals() but doesn't address boxing
My Assessment: Document: is on value types boxes and compares references
(always False for value types unless interned). Consider adding a
compile-time warning.

ANTON: Create a github issue for this. Let's discuss later.

────────────────────────────────────────
#: AMB-2
Area: Pipe _ placeholder
Issue: Tests use 5 |> multiply(_, 3) but spec doesn't document _ syntax
My Assessment: Document the syntax. This is a real user-facing feature.

ANTON: Agree, let's document it.

────────────────────────────────────────
#: AMB-3
Area: Comparison chain short-circuit
Issue: Spec says single-eval but doesn't say short-circuit
My Assessment: Document explicitly — it's a correctness property users rely
on.

ANTON: Agree, let's document it.

────────────────────────────────────────
#: AMB-4
Area: Function type contravariance
Issue: Spec shows commented-out examples
My Assessment: Clarify status. If not enforced, mark as aspirational.

ANTON: Agree, let's check and create a github issue if it's not implemented fully nor correct yet.

────────────────────────────────────────
#: AMB-5
Area: Null-conditional double-wrapping
Issue: ?. on method returning nullable — wrap again?
My Assessment: Document: return type is T? regardless of nesting depth
(flatten).

ANTON: Create a github issue to check this later.

────────────────────────────────────────
#: AMB-6
Area: Try expression uncaught types
Issue: try[ValueError] foo() when TypeError thrown
My Assessment: Document: uncaught exceptions propagate (runtime behavior).

ANTON: I agree, document this. We should also consider a nice syntax to capture other exception types or combine them somehow. Create a github issue for the latter.

────────────────────────────────────────
#: AMB-7
Area: maybe on already-optional
Issue: maybe opt where opt: str?
My Assessment: Document: no double-wrapping, stays str?.

ANTON: I agree, document this. There should be a test too.

────────────────────────────────────────
#: AMB-8
Area: Membership operator dispatch
Issue: __contains__ vs .Contains() priority
My Assessment: Document: __contains__ takes priority (Python semantics).

ANTON: Create a github issue for this. Maybe there should be an error that you should only pick one.

────────────────────────────────────────
#: AMB-9
Area: .NET snake_case method access
Issue: system.Console.write_line() works but undocumented
My Assessment: Add to dotnet_interop.md — this is a core interop feature.

ANTON: Agree, let's document it (and make sure there is a test).

────────────────────────────────────────
#: AMB-10
Area: None vs None()
Issue: No test for using None where None() required
My Assessment: Add test — this distinction trips up users.

ANTON: Agree, let's test it and document the difference. There should also be compiler errors
that say something like `Did you mean None?` or `Did you mean None()?`

---
EXTENSIONS — Tests beyond spec (all compatible)

These are well-covered by existing tests and represent natural extrapolations.
The spec should be updated to document them, but there's no implementation
risk.

Highest-value spec enrichments (features users will look for in the spec):
1. next() builtin — completely absent from builtin_functions.md

ANTON: Agree, add to the spec.

2. min()/max() with default parameter — undocumented

ANTON: Agree, add to the spec.

3. enumerate() positional start argument — spec shows keyword-only

ANTON: Agree, update the spec.

4. Operator sections with two placeholders (_ + _) — spec shows unary only

ANTON: Agree, update the spec.

5. Yield restrictions in try/except/finally — C# iterator limitation
undocumented

ANTON: Agree, update the spec.

6. .NET overloaded import behavior — from_import_overloads.spy tests it

ANTON: Agree, update the spec.

7. Abstract body-less methods — only documented for interfaces, works in
classes

ANTON: From docs/implementation_planning/syntax_consolidation_plan.md, bodyless
methods are planned to never be allowed. Methods should either have ellipsis (indicating
abtractness) or `pass` for no-op concrete body.

8. Keyword-only arguments in constructors — not in constructors.md

ANTON: Agree, update the spec. There should also be a test.

9. Bare raise outside except → error — not explicitly prohibited in spec

ANTON: Agree, update the spec, it should be an error. There should also be a test.

---
Recommended Action Plan

ANTON: My recommendations above override the below if there are conflicts.

Tier 1 — Fix now (spec accuracy, zero-test critical paths):
1. Resolve 8 divergences (mostly spec fixes)
2. Remove 2 stale "planned/not implemented" banners
3. Add comparison chaining tests (GAP-1)
4. Add comprehension scoping tests (GAP-3, GAP-4, GAP-10)
5. Add type narrowing E2E tests (GAP-9)

Tier 2 — Fix soon (high-impact gaps):
6. UTF-16 string behavior tests (GAP-2)
7. Verify + test or defer: class/struct constraints, array[T], named tuple
matching, struct modifiers
8. Add loop-else with return/raise tests (GAP-12)
9. Document _ pipe placeholder, next(), min/max default

Tier 3 — Batch with regular work (medium gaps):
10. All remaining medium-priority negative tests (GAP-13 through GAP-28)
11. Spec clarifications for all 10 ambiguities

Tier 4 — Low priority (cleanup):
12. Low-priority negative tests (GAP-29 through GAP-47)
13. Spec enrichment for all documented extensions
