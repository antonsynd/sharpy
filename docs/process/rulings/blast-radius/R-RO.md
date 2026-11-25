# Blast radius of R-RO

**Rule:** A derived __init__ assigns its own fields before super().__init__(...) (Swift order).

Measured at `ce4e6beb1` over 5853 programs (4085 analysed). One site is one __init__ of a class that has a base class; 292 sites were found.

## Sites by verdict

| area | programs | analysed | sites | refused | refused-if-let | conforms | out-of-scope | not-ruled |
|---|---|---|---|---|---|---|---|---|
| fixtures | 3948 | 2783 | 254 | 36 | 83 | 84 | 50 | 1 |
| conformance | 168 | 115 | 0 | 0 | 0 | 0 | 0 | 0 |
| stdlib | 61 | 61 | 9 | 1 | 2 | 6 | 0 | 0 |
| stdlib-tests | 375 | 358 | 8 | 3 | 0 | 5 | 0 | 0 |
| benchmarks | 6 | 6 | 1 | 0 | 1 | 0 | 0 | 0 |
| spec | 1295 | 762 | 20 | 5 | 1 | 9 | 4 | 1 |
| **all** | 5853 | 4085 | 292 | 45 | 87 | 104 | 54 | 2 |

Programs holding at least one site, by the worst verdict among their sites: 43 refused, 52 refused-if-let, 63 conforms, 37 out-of-scope, 2 not-ruled (197 programs).

## What each verdict means

- **refused**: A slot with no default (a field of non-nullable reference type, a reference-type auto-property, a get-only or init auto-property) is assigned after the call. The rule refuses this constructor however the class is migrated to let / var.
- **refused-if-let**: Only fields whose type has a default (value type, T | None, T?) are assigned after the call, and at least one of them is never written again, so it could be declared let. Refused if it is; accepted if every such field is declared var.
- **conforms**: Nothing the rule asks to be moved.
- **out-of-scope**: The constructor writes no super().__init__(...) call (it chains to self.__init__, or calls neither). R-RO has nothing to order; R-RS decides whether the call is owed.
- **not-ruled**: A @dataclass with a base class. The owner has not ruled it; `as-if` says what the rule's text would give.

## Sites by coordinates

| verdict | coordinates | sites | programs |
|---|---|---|---|
| refused | base: sharpy, fix: move, slots: fields | 32 | 31 |
| refused | base: dotnet, fix: move, slots: fields | 13 | 12 |
| refused-if-let | base: sharpy, fix: move, slots: fields | 84 | 61 |
| refused-if-let | base: dotnet, fix: move, slots: fields | 3 | 2 |
| conforms | base: sharpy, why: no-own-slots | 90 | 62 |
| conforms | base: dotnet, why: no-own-slots | 12 | 8 |
| conforms | base: sharpy, why: var-only | 2 | 2 |
| out-of-scope | base: sharpy, base-has-init: none, call: omitted | 47 | 35 |
| out-of-scope | base: sharpy, base-has-init: declared, call: chained-to-own-init | 4 | 3 |
| out-of-scope | base: dotnet, base-has-init: dotnet, call: omitted | 3 | 3 |
| not-ruled | as-if: refused-if-let, base: sharpy | 2 | 2 |

The axes:

- **base**: What the class derives from.
  - `sharpy`: a class written in Sharpy
  - `dotnet`: a .NET class, which includes the built-in exception types
- **fix**: The largest edit any one assignment needs to get ahead of the call.
  - `move`: a top-level `self.f = <value>` whose value reads only parameters, literals, module names and own fields assigned before it: cut and paste above the call
  - `restructure`: the assignment sits under control flow, or its value reads a local computed in the constructor: the value has to become one expression or a @static helper
  - `uses-self`: the value reads `self` beyond own fields already assigned (an inherited member, a method call, `self` passed on): it depends on state the base constructor sets, so the constructor needs a redesign
- **slots**: What kind of member has to move.
  - `fields`: fields only
  - `properties`: auto-properties only
  - `both`: fields and auto-properties
- **why**: For a conforming site, why nothing moves.
  - `no-own-slots`: the class declares no field or auto-property without an initializer
  - `none-after-call`: it declares some, and this constructor assigns none of them after the call
  - `var-only`: the ones assigned after the call have a default and are written again, so they are var
- **call**: For an out-of-scope site, what the constructor writes in place of the call.
  - `omitted`: no chaining call
  - `chained-to-own-init`: self.__init__(...)
- **base-has-init**: For an out-of-scope site, whether a base class declares an __init__. With `call: omitted` this is R-RS's question: R-RS asks for the call where a base has one.
  - `declared`: a Sharpy base class, at some depth, declares __init__
  - `none`: no base class declares one
  - `dotnet`: the chain reaches a .NET class first; R-RS does not rule that case
- **as-if**: For a not-ruled site, the verdict the rule's text would give.

## Examples

**refused** (base: sharpy, fix: move, slots: fields), 3 of 32:

- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/class_with_init/class_with_init.spy:39` Teacher.__init__: subject: str (move)
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/classes/constructor_chaining_0073.spy:22` ColoredPoint.__init__: color: str (move)
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/classes/dunder_eq_hash_inherited.spy:21` Derived.__init__: label: str (move)

**refused** (base: dotnet, fix: move, slots: fields), 3 of 13:

- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/decorators/bracket_attr_custom_class.spy:7` MyTagAttribute.__init__: label: str (move)
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/decorators/bracket_attr_custom_multi_param.spy:8` InfoAttribute.__init__: author: str (move); version: int32 (move)
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/decorators/bracket_attr_custom_on_struct.spy:7` TagAttribute.__init__: label: str (move)

**refused-if-let** (base: sharpy, fix: move, slots: fields), 3 of 84:

- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/access_modifiers/access_modifiers.spy:41` SavingsAccount.__init__: withdrawal_limit: float64 (move)
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/access_modifiers/access_modifiers.spy:59` CheckingAccount.__init__: overdraft_limit: float64 (move)
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/bool_variables_0003.spy:17` StrictPolicy.__init__: require_both: bool (move)

**refused-if-let** (base: dotnet, fix: move, slots: fields), 3 of 3:

- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/decorators/bracket_attr_custom_on_field.spy:8` RangeAttribute.__init__: min_val: int32 (move); max_val: int32 (move)
- `src/Sharpy.Stdlib/spy/socket_module.spy:87` error.__init__: errno: int32 | None (move)
- `src/Sharpy.Stdlib/spy/socket_module.spy:92` error.__init__: errno: int32 | None (move)

**conforms** (base: sharpy, why: no-own-slots), 3 of 90:

- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/access_modifiers/access_naming_convention_fields.spy:18` Child.__init__
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/access_modifiers/access_naming_convention_methods.spy:25` Child.__init__
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/bool_variables_0003.spy:30` PermissivePolicy.__init__

**conforms** (base: dotnet, why: no-own-slots), 3 of 12:

- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/decorators/bracket_attr_custom_on_method.spy:5` TraceAttribute.__init__
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/exception_fstring_is_the_message_1480.spy:28` MyErr.__init__
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/except_tuple_bare_and_single_1235.spy:9` NotFound.__init__

**conforms** (base: sharpy, why: var-only), 2 of 2:

- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/inheritance/virtual_override_0001.spy:55` Mage.__init__
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/properties/property_with_validation_0011.spy:54` SavingsAccount.__init__

**out-of-scope** (base: sharpy, base-has-init: none, call: omitted), 3 of 47:

- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/abstract_class_interface_stubs.spy:11` Circle.__init__: no chaining call
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/builtins/constructor_reference_annotated_rebind.spy:24` Cat.__init__: no chaining call
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/builtins/constructor_reference_annotated_rebind.spy:33` Dog.__init__: no chaining call

**out-of-scope** (base: sharpy, base-has-init: declared, call: chained-to-own-init), 3 of 4:

- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/classes/constructor_chaining_0073.spy:26` ColoredPoint.__init__: chains to self.__init__
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/classes/constructor_chaining_0073.spy:29` ColoredPoint.__init__: chains to self.__init__
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/inheritance/super_init_call_0020.spy:30` RollingAverage.__init__: chains to self.__init__

**out-of-scope** (base: dotnet, base-has-init: dotnet, call: omitted), 3 of 3:

- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/events/event_auto_clr_generic_subclass_1613.spy:12` ProgressEventArgs.__init__: no chaining call
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/events/event_spec_downloader_1613.spy:12` ProgressEventArgs.__init__: no chaining call
- `docs/language_specification/events.md:159 (line 166)` ProgressEventArgs.__init__: no chaining call

**not-ruled** (as-if: refused-if-let, base: sharpy), 2 of 2:

- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/dataclass_inheritance.spy:6` Child: generated constructor assigns y: int32 after the base call
- `docs/language_specification/dataclass.md:211 (line 217)` Child: generated constructor assigns y: int32 after the base call

## Programs not analysed

A probe reads only programs that compile today. The rest are counted here and say nothing about the rule.

| area | reason | programs |
|---|---|---|
| fixtures | a fixture that is expected to fail | 1161 |
| fixtures | does not compile today | 4 |
| conformance | does not compile today | 53 |
| stdlib-tests | a fixture that is expected to fail | 17 |
| spec | a spec block marked as a fragment or an error example | 459 |
| spec | does not compile today | 74 |

First error of the programs that do not compile, most common first: SPY0200 × 51, SPY0103 × 5, SPY0104 × 5, SPY0220 × 5, SPY0401 × 5, SPY0203 × 4, SPY0204 × 4, SPY0224 × 4.

## What this probe cannot see

- A write to a field from another file is not seen, so a field counted as possibly `let` may in fact have to be `var`: `refused-if-let` is an upper bound.
- A base class in another file is resolved only when the file compiles on its own.
- The probe does not check that moving an assignment keeps the program's meaning; `move` says the edit is mechanical, not that it was tried.
- Statements other than own-field assignments stay illegal ahead of the call (R-ML); a `restructure` site may need more than the one value rewritten.
- Only the programs in this repository are read. They lean towards what the tests exercise.
