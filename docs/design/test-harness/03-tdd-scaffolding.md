# Subsystem 3: Spec-Driven TDD Scaffolding Engine

> **Status:** Draft design — 2026-07-02
> **Priority:** 3 of 6 (accelerates all future feature work)
> **Index:** [README.md](README.md) · **Proposal:** [../test-harness-hardening-proposal.md](../test-harness-hardening-proposal.md)

## Goal

Given a language-spec page (or feature description), generate test skeletons across every compiler layer — lexer, parser, semantic, validation, codegen, integration fixtures, LSP — plus a tracking checklist, so a new feature starts with complete failing tests instead of ad-hoc partial coverage.

**Anti-hallucination stance:** the scaffolder generates *structure*, never *expected values it cannot source*. Expected outputs come only from (a) spec example blocks, (b) explicit `# ERROR SPYxxxx:` markers in the spec, or (c) `TODO` placeholders that force a human/`/verify-python` decision. A skeleton with a fabricated expected value is worse than no skeleton.

## Spec format (verified ground truth)

`docs/language_specification/` contains ~120 one-feature-per-page `.md` files plus a legacy grammar file `grammar.ebnf.txt` (43 KB, ISO EBNF). Conventions the reader can rely on:

- ATX headings: one `# Title` per page, `##`/`###` sections.
- **Sharpy code examples are in ` ```python ` fences** (775 occurrences corpus-wide); C# equivalents in ` ```csharp ` (92); grammar excerpts in ` ```ebnf ` (2 pages only).
- **No MkDocs admonitions.** Error semantics are conveyed by: inline `# ERROR SPY0134: message` comments inside code blocks, bold prose (`**Error**`, `**not** aliases`), and Sharpy↔.NET behavior tables.
- Diagnostic codes appear as `SPY\d{4}` tokens.

So the reader is a structural markdown parser + three targeted extractors — no NLP required, as the prompt anticipated.

**`grammar.ebnf.txt` is NOT ground truth.** It dates from initial development and has been neglected since; the implemented grammar is more complex than it states. The authoritative grammar is the parser itself (`src/Sharpy.Compiler/Parser/`, 6 partial files). Consequently the scaffolder treats EBNF-derived data as a *hint source only*: grammar-sourced checklist items are labeled `grammar-hint:` and require verification against actual compiler behavior (`/spy-emit tokens` / `/spy-emit ast`) before any expected value is filled in. Spec prose and example blocks — which are maintained — outrank the EBNF file whenever they disagree.

## Architecture

```
 spec page (.md) ──┐
 grammar.ebnf.txt ─┤     ┌────────────┐   FeatureSpec    ┌───────────────────────┐
 --feature name ───┼────▶│ SpecReader │─────────────────▶│ EdgeCaseDeriver        │
 --issue N ────────┘     │ (Markdig)  │                  │ (heuristic expansion)  │
                         └────────────┘                  └──────────┬────────────┘
                                                                    │ FeatureSpec + derived cases
                              ┌─────────────────────────────────────▼─────────┐
                              │ TestSkeletonGenerator                          │
                              │  ├─ LexerScaffolder      → Compiler.Tests/Lexer│
                              │  ├─ ParserScaffolder     → Compiler.Tests/Parser
                              │  ├─ SemanticScaffolder   → Compiler.Tests/Semantic
                              │  ├─ ValidationScaffolder → .error fixtures     │
                              │  ├─ IntegrationScaffolder→ TestFixtures/<feat>/│
                              │  └─ LspScaffolder        → Lsp.Tests           │
                              └─────────────────────┬───────────────────────────┘
                                                    │ GeneratedArtifact[] (never overwrites)
                                     ┌──────────────▼──────────────┐
                                     │ TddChecklist (markdown)      │──▶ GitHub issue / PR body
                                     └─────────────────────────────┘
```

## Interfaces

```csharp
namespace Sharpy.TestHarness.Scaffold;

/// <summary>
/// Parses a language-spec page into a structured FeatureSpec.
/// Purely structural (Markdig AST + regex extractors); no inference.
/// Contract: parsing any page under docs/language_specification/ must
/// succeed — unknown structures degrade to prose sections, never throw.
/// </summary>
public interface ISpecReader
{
    FeatureSpec Read(string specMarkdown, string specPath);

    /// <summary>Resolves EBNF productions referenced by the feature from
    /// grammar.ebnf.txt, by production-name match. CAUTION: that file is a
    /// neglected initial-development artifact and understates the implemented
    /// grammar — resolved rules are HINTS for test naming/case discovery, never
    /// a source of expected values. The parser source is authoritative.</summary>
    IReadOnlyList<GrammarRule> ResolveGrammar(FeatureSpec spec, string grammarEbnfText);
}

/// <summary>
/// Generates test skeletons for one compiler layer. Implementations must
/// follow the existing conventions of their target project (namespaces,
/// base classes, trait usage — see src/Sharpy.Compiler.Tests for patterns).
/// </summary>
public interface ILayerScaffolder
{
    ScaffoldLayer Layer { get; }

    /// <summary>May return an empty list when the feature doesn't touch this
    /// layer (e.g. no new tokens ⇒ no lexer tests) — emptiness is recorded in
    /// the checklist as "n/a", not silently dropped.</summary>
    IReadOnlyList<GeneratedArtifact> Generate(FeatureSpec spec, ScaffoldOptions options);
}

public enum ScaffoldLayer { Lexer, Parser, Semantic, Validation, CodeGen, Integration, Lsp }

/// <summary>
/// A file the scaffolder wants to create. The writer NEVER overwrites:
/// if the path exists, content is written to "<path>.scaffold.new" and the
/// checklist flags the conflict. Idempotent re-runs are therefore safe.
/// </summary>
public sealed record GeneratedArtifact
{
    public required string RelativePath { get; init; }     // repo-relative
    public required string Content { get; init; }
    public required ScaffoldLayer Layer { get; init; }
    public required ArtifactKind Kind { get; init; }
    /// <summary>Checklist line this artifact satisfies.</summary>
    public required string ChecklistItem { get; init; }
}

public enum ArtifactKind { TestClass, SpyFixture, ExpectedFile, ErrorFile, Checklist }
```

## Data models

```csharp
namespace Sharpy.TestHarness.Scaffold;

public sealed record FeatureSpec
{
    public required string Title { get; init; }             // H1
    public required string SpecPath { get; init; }
    public required string FeatureSlug { get; init; }        // kebab-case, for file naming
    public IReadOnlyList<SpecSection> Sections { get; init; } = [];
    /// <summary>```python blocks — these are SHARPY examples (spec convention).</summary>
    public IReadOnlyList<SpecExample> Examples { get; init; } = [];
    /// <summary>Extracted from `# ERROR SPYxxxx:` markers inside code blocks
    /// and SPY\d{4} tokens in bold-Error prose.</summary>
    public IReadOnlyList<SpecErrorCase> ErrorCases { get; init; } = [];
    public IReadOnlyList<GrammarRule> GrammarRules { get; init; } = [];
    /// <summary>Links to other spec pages → interaction-test candidates.</summary>
    public IReadOnlyList<string> CrossReferences { get; init; } = [];
    /// <summary>Sentences the reader couldn't classify but that contain
    /// normative markers ("must", "cannot", "only"). Become
    /// needs-clarification checklist items, NOT guessed tests.</summary>
    public IReadOnlyList<string> Unclassified { get; init; } = [];
}

public sealed record SpecExample(string Code, string? PrecedingProse, int SourceLine,
    IReadOnlyList<SpecErrorCase> InlineErrors);

/// <summary>An expected-error case with its diagnostic code when the spec names one.</summary>
public sealed record SpecErrorCase(string? DiagnosticCode, string Description, string? ExampleCode);

public sealed record GrammarRule(string ProductionName, string EbnfBody);

public sealed record ScaffoldOptions
{
    /// <summary>GitHub issue for the feature. REQUIRED: generated skeletons carry
    /// `[Fact(Skip = "TODO(#N): …")]` and repo rule 8 demands TODO ⇒ issue.</summary>
    public required int IssueNumber { get; init; }
    public required string FeatureName { get; init; }
    public string FixtureRoot { get; init; } =
        "src/Sharpy.Compiler.Tests/Integration/TestFixtures";
    public bool IncludeLsp { get; init; } = true;
}
```

## Per-layer generation rules

| Layer | Output location | What gets generated | Expected values from |
|-------|-----------------|--------------------|--------------------|
| Lexer | `src/Sharpy.Compiler.Tests/Lexer/<Feature>LexerTests.cs` | Token-type assertions for new keywords/operators found in spec examples/grammar hints; span checks; EOF/line-boundary edge cases | Spec examples; grammar terminals as `grammar-hint:` (verify via `/spy-emit tokens`); `TODO` for spans |
| Parser | `src/Sharpy.Compiler.Tests/Parser/<Feature>ParserTests.cs` | Parse-success shape assertions (node type + child count) per spec example; error-recovery test per malformed variant | Spec examples; node type left `TODO` until AST record exists |
| Semantic | `src/Sharpy.Compiler.Tests/Semantic/<Feature>SemanticTests.cs` | Type-inference assertions per typed example; rejection tests per error case with the spec's `SPYxxxx` code | Spec examples + `SpecErrorCase.DiagnosticCode` |
| Validation | `.error` fixtures under `TestFixtures/<feature>/` | One fixture per `SpecErrorCase` carrying the code and, where the spec example pins one, `@line:col` | Spec error markers only |
| CodeGen | *(none auto)* | A checklist item reminding to add an `.expected.cs` snapshot **after** implementation via `UPDATE_SNAPSHOTS` — snapshots of unimplemented features can't exist, and the update flow only rewrites *existing* files by design | n/a |
| Integration | `TestFixtures/<feature>/<case>.spy` + `.expected` | One fixture per spec example. `.expected` content: the example's documented output if the spec prints it; otherwise the literal line `TODO(#N): fill via /verify-python or spec` — which makes the test fail loudly, never pass vacuously. A `.skip` file is placed alongside until implementation starts | Spec output or explicit TODO |
| LSP | `src/Sharpy.Lsp.Tests/<Feature>LspTests.cs` | Hover-type assertion at the feature's binding site; semantic-token coverage entry; completion presence check | `TODO` placeholders |

All generated test classes compile immediately: `[Fact(Skip = "TODO(#123): <feature> not implemented")]` / `[Theory(Skip = ...)]`, matching the prompt's requirement and repo rule 8 (TODO must reference an issue — hence `IssueNumber` is required, not optional).

## Edge-case derivation (heuristics, not magic)

`EdgeCaseDeriver` expands the spec's cases mechanically:

- **Boundary values** — examples containing collections get empty/single/nested variants; integer literals get `0`, `-1`, `int.MaxValue` variants when the example's types allow.
- **Unicode** — identifier-bearing features get a non-ASCII identifier case; string-bearing features get an astral-plane (`𝔘+1D518`) and zero-width-joiner case (string indexing is a documented deviation area).
- **Nesting** — recursive grammar productions (self-referencing EBNF) get a depth-8 nesting case.
- **Interaction candidates** — one checklist item (not a generated test) per cross-referenced spec page: "does `<feature>` compose with `<other>`? (decorators/generics/async are the usual suspects)". Interactions need human judgment; the deriver only ensures they're not forgotten.

Every derived case is labeled `derived:` in the checklist so reviewers can distinguish spec-sourced from heuristic cases; grammar-sourced cases are labeled `grammar-hint:` (see the `grammar.ebnf.txt` caveat above) so reviewers know they carry the extra verification obligation.

## TDD checklist

Generated at `TestFixtures/<feature>/CHECKLIST.md` (and printable to a GitHub issue body):

```markdown
## Feature: walrus operator (:=) — #123
Spec: docs/language_specification/assignment_expressions.md

### Lexer
- [ ] TokenType for `:=` recognized (WalrusLexerTests.RecognizesWalrusToken)
### Parser
- [ ] AssignmentExpression AST shape (WalrusParserTests.ParsesSimpleWalrus)
- [ ] derived: depth-8 nested walrus (WalrusParserTests.ParsesNestedWalrus)
### Semantic
- [ ] type flows through walrus binding (WalrusSemanticTests.InfersBindingType)
- [ ] SPY0217 walrus at module level rejected (fixture: walrus_module_level.spy)
### Integration
- [ ] walrus_in_if.spy → .expected (TODO: output unfilled)
### CodeGen
- [ ] add .expected.cs snapshot post-implementation (UPDATE_SNAPSHOTS)
### LSP
- [ ] hover at binding shows inferred type (WalrusLspTests.HoverShowsType)
### Needs clarification
- [ ] "walrus **cannot** appear as a statement" — spec prose unclassified; confirm intended diagnostic
```

`harness scaffold status --feature walrus` re-derives checkbox state: a box is checked when the artifact exists **and** (for tests) the `Skip` attribute has been removed; `--run` additionally executes the feature's tests through `.claude/scripts/dotnet-serialized` and reports pass/fail per item.

## Configuration

```jsonc
{
  "scaffold": {
    "specRoot": "docs/language_specification",
    "grammarFile": "docs/language_specification/grammar.ebnf.txt",  // legacy artifact; hints only, never expected values
    "fixtureRoot": "src/Sharpy.Compiler.Tests/Integration/TestFixtures",
    "skipAttributeFormat": "TODO(#{issue}): {feature} not implemented",
    "deriveEdgeCases": true
  }
}
```

## CLI & skill

```
harness scaffold generate --spec <page.md> --feature <slug> --issue <N> [--layers lexer,parser,...]
harness scaffold status  --feature <slug> [--run]
harness scaffold checklist --feature <slug> [--to-issue]   # posts/updates via gh
```

### Skill definition — `.claude/skills/scaffold-tests/SKILL.md`

```markdown
---
name: scaffold-tests
description: Generate multi-layer test skeletons and a TDD checklist from a language-spec page
argument-hint: "<feature description or spec page> [--issue N]"
---

Scaffold tests across lexer/parser/semantic/validation/integration/LSP layers from the spec.

**Usage:** /scaffold-tests <feature or spec page> [--issue N]

**Behavior:**
- Locates the spec page (search docs/language_specification/ by the feature description if not given)
- If no GitHub issue exists for the feature, creates one first (`gh issue create`, run with
  dangerouslyDisableSandbox per project convention) — the scaffolder requires an issue number
- Runs `dotnet run --project src/Sharpy.TestHarness -- scaffold generate ...`
- Reviews generated skeletons and fills ONLY expected values that are sourced from the spec
  or verified via /verify-python — never invent expected outputs
- Existing files are never overwritten; conflicts appear as `.scaffold.new` files to reconcile

**Log location:** `.claude/tmp/last-scaffold.log`

## Steps
1. Resolve spec page + issue number (create the issue if missing)
2. `dotnet run --project src/Sharpy.TestHarness -- scaffold generate --spec <page> --feature <slug> --issue <N> 2>&1 | tee .claude/tmp/last-scaffold.log`
3. Read the generated CHECKLIST.md; present it to the user
4. For each `.expected` marked TODO: derive the value from spec text, or verify with /verify-python, or leave the TODO
5. Remind: remove `.skip` files and `Skip=` attributes as implementation lands; add the codegen snapshot last
```

## CI integration

Deliberately light — scaffolding is a dev-time tool:

- **Skeleton-rot check** (in the weekly harness workflow): `harness scaffold status --all --json` reports tests still carrying `Skip = "TODO(#N)"` whose issue `N` is closed → these are implemented features with un-enabled tests; surfaced in the weekly health report.
- Generated fixtures with `.skip` files are already excluded by the existing discovery logic, so scaffolding a feature never reddens CI.

## Test plan (for the scaffolder itself)

- **SpecReader conformance sweep** — parse *all* ~120 spec pages; assert zero exceptions and, for a golden subset (`exception_handling.md`, `inheritance.md`, `flexible_arguments.md` — the EBNF-bearing pages), snapshot the extracted `FeatureSpec` JSON.
- **Extractor unit tests** — `# ERROR SPY0134:` marker parsing (with/without message), SPY-code prose extraction, EBNF production resolution against the real `grammar.ebnf.txt`.
- **Generator snapshot tests** — fixed `FeatureSpec` → generated artifacts compared as snapshots; assert every generated `.cs` file parses with Roslyn and carries the `Skip` attribute with the issue reference.
- **Idempotency test** — generate twice into a temp dir; second run produces zero writes and zero `.scaffold.new` files.
- **Conflict test** — pre-existing file at a target path → `.scaffold.new` written, original untouched.
- **Checklist round-trip** — `status` correctly reads back checked/unchecked state after simulated implementation (Skip removed).

## Risks & mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Spec format drift breaks extractors | Silent under-extraction | Conformance sweep runs in `Sharpy.TestHarness.Tests` on every PR touching `docs/language_specification/**` (path-filtered job); `Unclassified` bucket makes under-extraction visible in the checklist |
| `grammar.ebnf.txt` is stale (abandoned after initial development; implemented grammar is richer) | Grammar-derived skeletons test the wrong syntax or miss real productions | Grammar is a hint source only: `grammar-hint:` labeling, verification via `/spy-emit` mandatory before filling expectations, spec prose/examples outrank EBNF; optionally file an issue to regenerate or retire the file rather than letting tooling relegitimize it |
| Skeletons rot as permanently-skipped tests | Dead weight, false sense of coverage | Weekly skeleton-rot check keyed to issue state; checklist items are the tracked unit |
| Generated tests encode a misreading of the spec | Wrong tests with authoritative air | Expected values only from spec text/verify-python; `derived:` labeling; `needs-clarification` lane for ambiguous prose (the prompt's open question — answered: ambiguity becomes a checklist item, never a guess) |
| Scaffolder generates non-compiling code as test-project conventions evolve | Friction | Generator snapshot tests parse output with Roslyn; templates live beside the conventions they mirror |
| Scope creep toward generating implementation stubs (AST records, emitter methods) | Complexity | Out of scope v1 (the prompt's open question — answered: tests only; the checklist's "n/a" lines tell implementers what production code to touch, which `/create-plan` already handles) |

## Implementation roadmap

| Phase | Work | Exit criteria |
|-------|------|---------------|
| 3a (week 1) | Markdig-based `SpecReader` + extractors + conformance sweep | All spec pages parse; golden FeatureSpecs snapshotted |
| 3b (week 1–2) | Layer scaffolders (lexer/parser/semantic/validation/integration), artifact writer with no-overwrite policy | Skeletons compile; idempotency + conflict tests green |
| 3c (week 2) | Checklist + `status`/`checklist` verbs + edge-case deriver + LSP scaffolder | End-to-end dry run on a real upcoming feature |
| 3d (week 2) | `/scaffold-tests` skill; weekly skeleton-rot check | Skill produces a reviewed checklist on a real issue |

Dependencies: `Sharpy.TestHarness` skeleton (subsystem 1). New NuGet: `Markdig` (test-harness project only; no production-code dependency). Synergy: scaffolded fixtures immediately enter the oracle universe (subsystem 2) once un-skipped.
