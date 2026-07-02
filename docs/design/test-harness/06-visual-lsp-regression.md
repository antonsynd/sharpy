# Subsystem 6: Vision-Based LSP Visual Regression Harness

> **Status:** Draft design — 2026-07-02
> **Priority:** 6 of 6 (most novel, highest design complexity — deliberately last)
> **Index:** [README.md](README.md) · **Proposal:** [../test-harness-hardening-proposal.md](../test-harness-hardening-proposal.md)

## Goal

Catch editor-integration regressions that protocol-level tests cannot see: wrong syntax coloring, hover tooltips at the wrong position or with wrong content, broken completion popups, diagnostic squiggles on the wrong line. The existing LSP tests (`Sharpy.Lsp.Tests` — ~59 handler unit tests plus stdio JSON-RPC E2E tests via `LspTestClient`) verify *payloads*; this subsystem verifies *pixels*.

## Verified ground truth that shapes the design

- The Blazor WASM playground is `src/Sharpy.Playground` (in the solution, Monaco-based, `dotnet watch run` → `localhost:5000`, published to GitHub Pages by `docs.yml`).
- The VS Code extension is an npm project at `editors/vscode` — **outside `sharpy.sln`**, built by its own workflow. Driving VS Code from C# is therefore a cross-toolchain exercise, which is why it is Phase 2.
- E2E LSP tests already spawn the real server as a subprocess; the visual harness reuses that server, not a mock.
- CI is `ubuntu-latest` only; the dev machine is macOS. Screenshot bytes will differ across the two — the golden-file scheme must be platform-keyed from day one.

**Capability caveat (open item to resolve in Phase 0):** the playground's Monaco surface may not currently wire hover/completion/diagnostics the way VS Code does. Each `IEditorDriver` therefore advertises a *capability set*, and each test case declares the capabilities it requires; cases whose capabilities no available driver provides are reported `SKIPPED_NO_DRIVER`, never silently green. If a spike shows the playground lacks (say) signature help, those cases simply wait for the VS Code driver rather than blocking the subsystem.

## Architecture

```
 VisualTestCase (.yaml) ──┐
                          ▼
              ┌────────────────────┐    launch/navigate/act/settle   ┌─────────────────────┐
              │ VisualTestRunner    │────────────────────────────────▶│ IEditorDriver        │
              │ (xUnit theory,      │                                 │  ├ PlaygroundDriver  │── Playwright → headless Chromium → Blazor playground
              │  Category=Visual)   │◀────────────────────────────────│  └ VsCodeDriver (P2) │── Playwright-Electron → VS Code + editors/vscode ext
              └─────────┬──────────┘        Screenshot (region)       └─────────────────────┘
                        ▼
              ┌─────────────────────────────────────────────────────┐
              │ IVisualVerifier                                      │
              │  CompositeVerifier:                                  │
              │   1. GoldenFileVerifier (pixel diff; free, fast)     │──▶ pass ⇒ done, zero LLM cost
              │   2. on diff: VisionLlmVerifier                      │
              │       ├ OllamaVisionBackend (local, free)            │
              │       └ CloudVisionBackend  (Anthropic vision API)   │
              │      + VerificationCache (image-hash × prompt-hash)  │
              └─────────┬───────────────────────────────────────────┘
                        ▼
        VerificationResult {status, confidence, issues[]} → thresholds → pass/warn/fail
```

## Interfaces

```csharp
namespace Sharpy.TestHarness.Visual;

/// <summary>
/// Drives one editor surface. Implementations own process lifecycle
/// (browser/Electron), determinism setup (viewport, fonts, animation
/// suppression — see "Stability contract"), and region resolution.
/// </summary>
public interface IEditorDriver : IAsyncDisposable
{
    /// <summary>Capabilities this driver can render (hover, completion,
    /// diagnostics, semanticTokens, signatureHelp). Checked against each
    /// case's requirements before running it.</summary>
    IReadOnlySet<EditorCapability> Capabilities { get; }

    /// <summary>Launch with a pinned theme. Must be called once per fixture
    /// class, not per case — editor startup is seconds, cases are many.</summary>
    Task LaunchAsync(EditorTheme theme, CancellationToken ct);

    Task OpenDocumentAsync(string spySource, string fileName, CancellationToken ct);

    /// <summary>Performs the case's action (move cursor + hover / invoke
    /// completion / wait for diagnostics) and waits for the UI to settle.
    /// Settling contract: LSP response observed AND two consecutive frames
    /// hash-identical (see "Stability contract"). Throws TimeoutException
    /// after options.SettleTimeout.</summary>
    Task PerformAsync(EditorAction action, Position position, CancellationToken ct);

    /// <summary>Captures the requested region. Region.Element resolves a UI
    /// part (tooltip, completion widget) to its bounding box + padding;
    /// Region.Editor captures the code area; Region.Full the whole surface.</summary>
    Task<Screenshot> CaptureAsync(CaptureRegion region, CancellationToken ct);
}

public enum EditorCapability { Hover, Completion, Diagnostics, SemanticTokens, SignatureHelp }
public enum EditorAction { Hover, TriggerCompletion, AwaitDiagnostics, RenderOnly }
public enum EditorTheme { Light, Dark }

public sealed record Screenshot(byte[] PngBytes, int Width, int Height, string ContentHash);
```

```csharp
namespace Sharpy.TestHarness.Visual;

/// <summary>
/// Judges a screenshot against an expectation. Implementations must be
/// side-effect free w.r.t. the editor; they see only bytes + expectation.
/// </summary>
public interface IVisualVerifier
{
    Task<VerificationResult> VerifyAsync(
        Screenshot actual, VisualExpectation expectation, CancellationToken ct);
}

public sealed record VisualExpectation
{
    /// <summary>Natural-language expectation from the case file, e.g.
    /// "Hover tooltip shows type `int` for variable `x`".</summary>
    public required string Description { get; init; }
    public required EditorAction Action { get; init; }
    /// <summary>Golden image path for this (platform, theme); null ⇒ vision-only case.</summary>
    public string? GoldenPath { get; init; }
    /// <summary>Structured content assertions the vision prompt embeds, e.g.
    /// mustContain: ["int"], mustNotContain: ["error"].</summary>
    public IReadOnlyList<string> MustContain { get; init; } = [];
    public IReadOnlyList<string> MustNotContain { get; init; } = [];
}

public sealed record VerificationResult
{
    public required VerificationStatus Status { get; init; }
    /// <summary>[0,1]. Pixel-diff verifiers report 1.0 on exact-pass and
    /// map diff ratio to confidence on failure; vision backends report the
    /// model's self-assessed confidence. Thresholds: fail &lt; failBelow,
    /// warn &lt; warnBelow (config).</summary>
    public required double Confidence { get; init; }
    public IReadOnlyList<string> Issues { get; init; } = [];
    /// <summary>Which verifier produced the verdict ("pixel", "ollama:llava",
    /// "anthropic:claude-…") and whether it came from cache — required for
    /// auditability of every green checkmark.</summary>
    public required string VerdictSource { get; init; }
    public bool FromCache { get; init; }
}

public enum VerificationStatus { Pass, Fail, NeedsHuman /* budget exhausted or low confidence */ }

/// <summary>
/// A vision model behind a uniform contract. Backends MUST return the JSON
/// schema requested (pass/confidence/issues); non-conforming output is one
/// retry then NeedsHuman — never a parse-by-vibes fallback.
/// </summary>
public interface IVisionBackend
{
    string Id { get; }                       // "ollama:llava", "anthropic:claude-…"
    Task<VisionJudgment> EvaluateAsync(byte[] pngBytes, string prompt, CancellationToken ct);
}

public sealed record VisionJudgment(bool Pass, double Confidence, IReadOnlyList<string> Issues);
```

`CompositeVerifier` policy: golden diff first (`Pass` ⇒ done). On pixel failure, consult `VerificationCache`; on miss, escalate to the configured vision backend chain (ollama first if reachable, cloud second) within budget; record verdict + source. Pixel-diff *pass* never escalates — vision cost is paid only for changed pixels.

## Declarative test cases

`src/Sharpy.Lsp.Tests/Visual/cases/*.yaml`:

```yaml
name: hover_on_int_variable
capabilities: [hover]
source: |
  def main() -> None:
      x: int = 42
      print(x)
action: hover
position: { line: 3, col: 11 }      # on `x` in print(x)
region: tooltip
expect:
  description: "Hover tooltip shows the type `int` for variable `x`"
  mustContain: ["int"]
  mustNotContain: ["Unknown", "error"]
themes: [light, dark]
golden: true                        # baselines/{platform}/{theme}/hover_on_int_variable.png
```

Loader validates against a JSON schema; unknown keys are errors (case files are contracts, not suggestions). Cases run as one xUnit theory per (case × theme) in `Sharpy.Lsp.Tests/Visual/VisualRegressionTests.cs`, `[Trait("Category", "Visual")]`, `[Collection("VisualEditor")]` (serialized — one editor at a time).

## Vision prompt templates

One template per action, stored beside the code (`Visual/Prompts/hover.txt` etc.), versioned — the **prompt hash is part of the cache key**, so editing a prompt invalidates exactly the right cache entries. Hover template:

```
You are verifying a screenshot of a code editor's hover tooltip in an automated test.

Context: file {filename}; hover triggered at line {line}, column {col}.
Expected: {description}
The tooltip MUST contain each of: {mustContain}
The tooltip MUST NOT contain any of: {mustNotContain}

Judge ONLY:
1. Is a hover tooltip visible and anchored near line {line}?
2. Does its text satisfy the MUST/MUST NOT lists? (Exact wording beyond
   those lists does not matter.)
3. Any rendering defects (clipped text, overlapping widgets, empty tooltip)?

IGNORE: theme colors, font rendering/anti-aliasing, scrollbar state,
minimap contents, cursor visibility. These vary legitimately.

Respond with ONLY this JSON, no prose:
{ "pass": boolean, "confidence": number (0..1), "issues": [string] }
```

The IGNORE block is the false-positive calibration; the MUST lists are the false-negative calibration — the model checks *content*, not aesthetics. Completion and diagnostics templates differ in the element and position checks (completion: list visible + expected entry present; diagnostics: squiggle on the expected line, judged against the line-number gutter).

## Stability contract (anti-flake)

All drivers must implement, in this order:

1. **Fixed environment:** viewport 1280×800, `deviceScaleFactor: 1`, pinned color scheme per theme, `prefers-reduced-motion: reduce`.
2. **CSS kill-switch injected at launch:** `* { animation: none !important; transition: none !important; caret-color: transparent !important; }` plus Monaco options (`cursorBlinking: "solid"`, `minimap.enabled: false`, `renderLineHighlight: "none"`).
3. **Pinned font:** the playground gains a test-mode query flag (`?visualtest=1`) that loads a bundled monospace webfont (committed WOFF2) instead of system fonts — the single biggest cross-platform variable eliminated at the source.
4. **Settle = event + quiescence:** await the LSP response (the driver watches the same JSON-RPC stream `LspTestClient` does, or Monaco's API events), then require two captures 100 ms apart with identical `ContentHash`. Not-settling within `settleTimeout` (10 s) fails the case as `Flaky` — a distinct, tracked status.
5. **Retry policy:** one retry per case, and a case that passes on retry is *recorded* as flaky-pass in the report; three flaky-passes for the same case in a window opens a flake issue. No infinite retries, no `Thread.Sleep`-and-hope.
6. **CI pinning:** the visual job runs in the pinned `mcr.microsoft.com/playwright/dotnet` container (exact tag committed), not raw `ubuntu-latest` — freezes fonts/Chromium against runner-image drift.

## Golden files & platforms

- Layout: `src/Sharpy.Lsp.Tests/Visual/baselines/{linux|macos}/{light|dark}/{case}.png`. CI compares only `linux/`; `macos/` baselines are optional developer convenience (generated locally, committed if the developer wants local pixel-diffing; otherwise macOS runs are vision-only).
- Pixel diff: per-channel tolerance 2/255, anti-aliasing detection on, fail when >0.1% of pixels differ (configurable). Diff images written to `artifacts/visual/diffs/` on failure and uploaded.
- **Update workflow** mirrors snapshots: `UPDATE_VISUAL_BASELINES=true dotnet test --filter "Category=Visual"` rewrites baselines for *failing* cases only, or `harness visual approve <case>` for one. The PR containing baseline changes gets the before/after/diff triptych in the uploaded HTML report — golden churn is reviewable, not a blind binary blob swap.

## Configuration & cost control

```jsonc
{
  "visual": {
    "driver": "playground",                    // "playground" | "vscode" (P2)
    "playgroundUrl": "http://localhost:5000",  // CI starts it; local reuses /playground skill
    "casesRoot": "src/Sharpy.Lsp.Tests/Visual/cases",
    "baselinesRoot": "src/Sharpy.Lsp.Tests/Visual/baselines",
    "confidence": { "failBelow": 0.8, "warnBelow": 0.9 },
    "verifiers": ["pixel", "ollama", "cloud"], // escalation order; drop entries to disable
    "ollama": { "endpoint": "http://localhost:11434", "model": "llava:13b" },
    "cloud": { "provider": "anthropic", "model": "claude-sonnet-5", "apiKeyEnv": "ANTHROPIC_API_KEY" },
    "budget": { "visionCallsPerRun": 50, "onExhausted": "needsHuman" },
    "cache": { "path": "artifacts/visual-cache", "maxEntries": 5000 }
  }
}
```

- **Cache:** key = `sha256(pngBytes) ⊕ sha256(promptTemplate+params) ⊕ backendId`; value = `VisionJudgment` JSON. Persisted via `actions/cache` across CI runs — an unchanged screenshot re-verified against an unchanged prompt costs zero calls forever (answers the prompt's caching question: yes, and it's the primary cost lever after the composite strategy).
- **Budget:** hard per-run cap; exhaustion ⇒ remaining escalations return `NeedsHuman` (report lists them for manual review) rather than silently passing or burning money. Monthly spend estimate at defaults: ≤50 calls/run × nightly ≈ 1,500 calls/month worst case, realistically ~a tenth of that once the cache warms — within the shared budget system (README: Cost Management).

## CI integration

Nightly + path-filtered PR job (`src/Sharpy.Lsp/**`, `src/Sharpy.Playground/**`, `editors/vscode/**`, `Visual/**`):

```yaml
  visual-regression:
    runs-on: ubuntu-latest
    container: mcr.microsoft.com/playwright/dotnet:v1.47.0-noble   # pinned tag
    timeout-minutes: 45
    steps:
      - uses: actions/checkout@v7
      - name: Restore verification cache
        uses: actions/cache@v4
        with: { path: artifacts/visual-cache, key: "visual-cache-${{ hashFiles('src/Sharpy.Lsp.Tests/Visual/**') }}", restore-keys: visual-cache- }
      - run: dotnet build src/Sharpy.Playground src/Sharpy.Lsp.Tests -c Release
      - name: Start playground
        run: dotnet run --project src/Sharpy.Playground -c Release --no-build &
      - name: Run visual tests
        run: dotnet test src/Sharpy.Lsp.Tests --no-build -c Release --filter "Category=Visual"
        env: { ANTHROPIC_API_KEY: "${{ secrets.ANTHROPIC_API_KEY }}" }
      - name: Upload visual report (screenshots, diffs, verdicts)
        if: always()
        uses: actions/upload-artifact@v7
        with: { name: visual-report, path: artifacts/visual/, retention-days: 30 }
```

`Category=Visual` is excluded from the standard `dotnet10.yml` test filters (README: trait taxonomy) — ordinary CI and local `dotnet test` never launch browsers by surprise. Ollama is unavailable in CI; the verifier chain simply skips unreachable backends, so CI escalation is pixel → cloud, local is pixel → ollama → (optional) cloud.

## Skill definition — `.claude/skills/visual-review/SKILL.md`

```markdown
---
name: visual-review
description: Run visual LSP regression cases locally, review failures, approve intentional baseline changes
argument-hint: "[case-name] [--approve] [--theme light|dark]"
---

Run the vision-based LSP visual regression harness against the local playground.

**Usage:** /visual-review [case] [--approve]

**Behavior:**
- Starts the playground if not running (reuse /playground skill flow)
- Runs `dotnet test src/Sharpy.Lsp.Tests --filter "Category=Visual[&DisplayName~case]"` via dotnet-serialized
- On failures: open the diff triptych paths, summarize the verifier's issues[] verbatim,
  and state which verifier judged it (pixel vs vision, cached or fresh)
- `--approve` reruns with UPDATE_VISUAL_BASELINES=true for the named case ONLY after the user
  confirms the change is intentional — never approve to silence a red test
- NeedsHuman results are listed for the user; do not resolve them yourself

**Log location:** `.claude/tmp/last-visual.log`
```

## Test plan (for the harness itself)

- **Verifier tests with synthetic images** (`Sharpy.TestHarness.Tests/Visual/`): golden vs identical (pass, conf 1.0); shifted-by-3px tooltip (pixel fail → escalation path invoked); recolored theme variant (pixel fail, vision pass with `FakeVisionBackend`); wrong tooltip text (both fail). Synthetic PNGs generated in-test, no binary fixtures.
- **`FakeVisionBackend`** — scripted judgments; drives all composite/threshold/budget/cache logic tests deterministically. Includes a malformed-JSON-response case (retry-once-then-NeedsHuman contract).
- **Cache tests** — hit/miss on each key component (image, prompt, backend); eviction at maxEntries.
- **Prompt-template tests** — placeholder substitution complete (no `{...}` residue); templates ship with schema-conforming few-shot response.
- **Driver smoke** (`[Trait("Category","Visual")]`, so CI-only): launch playground, open document, capture, non-empty stable hash — the minimal end-to-end.
- **Case-schema validation test** — all committed YAML cases parse; unknown keys rejected.

## Risks & mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Flakiness (the defining risk) | Trust collapse; team ignores red | Stability contract above; `Flaky` as a first-class tracked status; nightly-first rollout — the subsystem must demonstrate <2% flake rate over two weeks before any PR-blocking role |
| Vision model wrong verdicts (both directions) | False confidence / noise | Confidence thresholds; MUST-lists constrain judgment to content; verdict source recorded on every result; periodic audit sample (weekly report lists 5 random vision-passes with screenshots for human spot-check) |
| Playground lacks LSP UI parity with VS Code | Blind spots against the real editor | Capability declarations + `SKIPPED_NO_DRIVER` visibility; VS Code driver is Phase 2 with Playwright-Electron; the E2E protocol tests still cover payload correctness meanwhile |
| Cloud cost drift | Budget overrun | Composite + cache + hard per-run cap + `NeedsHuman` fail-soft; spend reported per run in `SubsystemReport` |
| Baseline churn on intentional UI work | Review fatigue | `--approve` flow with triptych review; baselines are per-platform so a Monaco upgrade is one reviewed PR, not scattered noise |
| Container/Chromium pin rots | Eventually forced migration | Pin is explicit + dated; quarterly bump PR regenerates linux baselines in one shot |

## Implementation roadmap

| Phase | Work | Exit criteria |
|-------|------|---------------|
| 6-0 (spike, 2–3 days) | Playground capability audit (hover? completion? diagnostics markers?); test-mode flag + pinned font PR | Capability matrix documented; go/no-go per action type |
| 6a (week 1) | Models, case loader + schema, `GoldenFileVerifier`, `FakeVisionBackend`, composite/threshold/budget/cache logic + tests | All harness-logic tests green without any real editor |
| 6b (week 2) | `PlaygroundDriver` (Playwright), stability contract, 5 pilot hover/diagnostic cases, linux baselines from CI | Pilot cases stable across 20 consecutive CI runs |
| 6c (week 3) | Vision backends (ollama + Anthropic), prompt templates, escalation live, `/visual-review` skill, approve flow | A deliberate content regression is caught by vision after pixel-diff fires |
| 6d (week 4+) | Case corpus growth (completion, semantic tokens), nightly reporting, flake tracking; Phase 2: `VsCodeDriver` via Playwright-Electron against `editors/vscode` | 25+ cases nightly at <2% flake; VS Code driver spike report |

Dependencies: harness config/reporting (1); benefits from `LspTestClient` patterns. New packages (test-side only): `Microsoft.Playwright`, an image-diff library (evaluate `Codeuctivity.ImageSharpCompare` vs a small in-house diff over `SkiaSharp` — decision in 6a; license check first).
