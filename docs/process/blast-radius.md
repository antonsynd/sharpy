# Blast radius

How many existing programs would a proposed language rule touch, and how hard is each to fix?

A ruling used to answer that with a text count: a regular expression over the fixtures. A text
count cannot tell a class's own field from an inherited one, a field with an initializer from one
without, or `(super().__init__)(x)` from no call at all. `sharpy-blast` answers from the compiler's
own typed tree instead.

```bash
bash build_tools/blast_radius.sh list                 # the rules that have a probe
bash build_tools/blast_radius.sh run R-RO             # Markdown report on standard output
bash build_tools/blast_radius.sh run R-RO --md report.md --json report.json
bash build_tools/blast_radius.sh run R-RO --area mine=some/dir --examples 20
```

The whole corpus takes about half a minute.

## How it works

1. **The rule is written as a probe.** A probe is a pass over a type-checked program that only
   reports. It never refuses anything and it changes no output. It lives in
   `src/Sharpy.BlastRadius/Probes/` and implements `IRuleProbe`.
2. **The corpus is every Sharpy program in the repository.** That is each `.spy` file under the
   test fixtures, the conformance tests, the Stdlib sources and tests and the benchmarks, and each
   fenced Sharpy block of the language specification. Each is analysed with the compiler's front
   end (`CompilerApi.Analyze`), with every experimental feature on. No code is generated.
3. **Only programs that compile today are read.** A fixture with an `.error` or `.skip` sidecar, a
   specification block marked as a fragment or an error example, and anything analysis refuses are
   counted in the report's last table and say nothing about the rule.
4. **The probe reports sites, not only hits.** A site is one place the rule applies: for R-RO, one
   `__init__` of a class that has a base class. Each site gets a verdict (`refused`, `conforms`
   and so on) and coordinates on the probe's axes. The denominator is therefore counted the same
   way as the numerator.

## Reading a report

- **Sites by verdict**, per corpus area. This is the headline: how many sites the rule refuses,
  and out of how many.
- **Sites by coordinates.** The same sites grouped by the axes the probe declares. For R-RO the
  `fix` axis says whether a refused constructor needs a cut and paste (`move`), a rewrite of one
  value (`restructure`) or a redesign (`uses-self`).
- **Examples**, a few per group, as `file:line`.
- **Programs not analysed**, with the reason.
- **What the probe cannot see.** Every probe states its limits and the report prints them.

The JSON form (`--json`) lists every site, for a script or a second opinion.

## Writing a probe

1. Add a class under `src/Sharpy.BlastRadius/Probes/` that implements `IRuleProbe`, and register
   it in `ProbeRegistry.All`.
2. Declare its verdicts and its axes with a sentence for each value. A report prints them, and a
   test checks that the probe emits nothing it did not declare.
3. Emit a site wherever the rule applies, including where the program already conforms.
4. Read facts from the semantic model (`SemanticInfo`, the symbol table), not from spelling. Where
   the compiler already has a predicate for the construct, use it:
   `AstHelper.TryGetConstructorInitializerCall` is what the checker and the emitter use to find a
   chaining call.
5. Where the rule depends on syntax that does not parse yet, say what the probe infers in its
   place and give the uncertain cases their own verdict. R-RO depends on `let` / `var` fields
   (#1974); the probe infers "has to be `var`" from a second write, and reports the rest as
   `refused-if-let`.
6. Test it with a generated matrix whose expected values come from the rule stated as a table
   (`src/Sharpy.Compiler.Tests/BlastRadius/`), and record a mutation of the probe that turns the
   test red.

A probe is most of the eventual check. When the owner adopts the rule, the same walk becomes the
refusal, in the checker or a validator.

## What it does not do

- **It does not measure a rule that changes output.** A rule such as R-QY (`str` of an Optional)
  refuses nothing; its radius is every program whose printed text changes. That needs the new
  behaviour behind a flag and every fixture run both ways. Not built.
- **It reads this repository only.** The fixtures lean towards what the tests exercise.
- **It does not try the fix.** `move` says an edit is mechanical; nothing applies it and reruns
  the program.

## Measurements on record

| Rule | Report | Measured at |
|---|---|---|
| R-RO, the Swift order | [rulings/blast-radius/R-RO.md](rulings/blast-radius/R-RO.md) | `aa21bedb2` |

Regenerate a report with
`bash build_tools/blast_radius.sh run <rule> --md docs/process/rulings/blast-radius/<rule>.md --examples 3`.

### R-RO against the text counts in its record

The record of R-RO says, by a text count, that 123 of the 218 fixture `__init__` methods that call
`super().__init__` assign a field after the call. Measured at `aa21bedb2`, over the fixtures that
compile today:

- 204 constructors call `super().__init__(...)`.
- 36 are refused however the class is migrated: a field with no default is assigned after the call.
- 83 are refused only if a field that has a default and is never written again is declared `let`.
- 84 have nothing to move.
- Every one of the 119 is a `move`: a top-level assignment whose value reads only parameters. No
  site needs a rewrite or reads state the base constructor sets.

So the text count was close as an upper bound (119 against 123), and seven in ten of those sites
depend on how fields are migrated to `let` / `var`.

The same run answers R-RS's question, since the probe reports constructors with no chaining call:
50 fixture constructors write none, and in none of them does a Sharpy base class declare an
`__init__` (47 have no base `__init__` at all; 3 derive from a .NET class). R-RS's record says 2 of
186 by a text count; the fixtures a text search finds are expected-error fixtures, and one that
writes the call as `(super().__init__)(value)`.
