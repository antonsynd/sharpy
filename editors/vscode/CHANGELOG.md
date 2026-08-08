# Change Log

All notable changes to the "sharpy-lang" extension will be documented in this file.

The extension version tracks the Sharpy toolchain version (it is bumped in lockstep
with `SharpyVersion`), so several releases below contain no extension-facing change.

## [Unreleased]

## [0.12.0] - 2026-08-07

### Changed
- Bumped two npm dependencies (dependabot). No other extension-package changes; version bumped in lockstep with the toolchain. Toolchain-side, the bundled language server gained the round-8 fixes visible in-editor: rename now works from an unreferenced declaration (#1232), the server honors the client's log level so per-stage analysis attribution is reachable (#1225), and diagnostics reflect the round's semantic changes — builtin type names are refused in type declarations (#1240, #1241) and unpinned constructor aliases draw SPY0342 with tier-3 guidance (#1248).

## [0.11.0] - 2026-08-05

### Changed
- No extension-package changes; version bumped in lockstep with the toolchain. Toolchain-side, the bundled language server gained the round-7 fixes visible in-editor: parameter-name inlay hints now appear for calls inside comprehensions and lambdas (#1223), and unreferenced function-local consts get their inferred-type inlay hint (#1222).

## [0.10.0] - 2026-07-30

### Changed
- Migrated to `vscode-languageclient` 10. The minimum supported VS Code version rises to 1.91, which the new client requires (#1178)
- `sharpy.lsp.maxNumberOfProblems` and `sharpy.inlayHints.typeAnnotations` are now honored by the language server. Both had been contributed since 0.1.0 but were read by nothing (#1165)
- The three client-side settings (`sharpy.serverPath`, `sharpy.trace.server`, `sharpy.debug.dotnetPath`) say so in their descriptions (#1165)

### Removed
- `sharpy.format.indentSize` and `sharpy.inlayHints.parameterNames`. Both were contributed in the initial release and never implemented — there is no formatting provider and no parameter-name inlay hint (#1165)

### Security
- Dependency refresh draining eight denial-of-service advisories across the build and packaging chain (brace-expansion, fast-uri, js-yaml, linkify-it). The last three required the `vscode-languageclient` major bump to reach a patched minimatch (#1177, #1178)

## [0.9.0] - 2026-07-29

### Added
- `sharpy.features` setting — the editor's counterpart of `sharpyc --enable-feature`, enabling experimental syntax for in-editor analysis. A workspace `.spyproj`'s `<Features>` is layered under it and cannot be disabled from settings (#1149)

### Fixed
- Settings now actually reach the language server. `synchronize.configurationSection: "sharpy"` was missing, so the client sent `didChangeConfiguration` with null settings and server-side settings — `sharpy.features` and `sharpy.transitionHints.enabled` — never arrived (#1149)

## [0.8.0] - 2026-07-24

No extension-facing changes; version bumped with the toolchain.

## [0.7.0] - 2026-07-22

No extension-facing changes; version bumped with the toolchain.

## [0.6.1] - 2026-06-30

No extension-facing changes; version bumped with the toolchain.

## [0.6.0] - 2026-06-29

No extension-facing changes; version bumped with the toolchain.

## [0.5.0] - 2026-06-21

### Added
- Syntax highlighting for the postfix `?` error-propagation operator (#982)

### Changed
- Dependency updates

## [0.4.2] - 2026-06-19

### Changed
- Dependency updates

## [0.4.1] - 2026-06-14

### Changed
- Dependency updates

## [0.4.0] - 2026-06-12

No extension-facing changes; version bumped with the toolchain.

## [0.3.0] - 2026-06-10

No extension-facing changes; version bumped with the toolchain.

## [0.2.0] - 2026-06-02

### Changed
- Dependency updates

## [0.1.4] - 2026-05-20

Covers everything after the initial release: 0.1.1 through 0.1.3 were toolchain-wide
version bumps published without separate extension notes.

### Added
- Debug Adapter Protocol integration for `.spy` files — launch configurations, the `Sharpy: Run File` snippet, and the `sharpy.debug.dotnetPath` setting
- Breakpoint gutter support for `.spy` files via the `breakpoints` contribution (#609)
- `sharpy.transitionHints.enabled` setting, controlling hints about Python/C# behavioral differences
- Marketplace icon and Sharpy branding
- Extension build support in `build_sharpy` (`--project vscode`)

### Changed
- Dual-licensed under Apache 2.0 and MIT (#645)
- Language server startup failures are now reported gracefully in the output channel and status bar instead of failing silently

### Security
- The manifest is hardened against workspace settings injection: the extension declares `untrustedWorkspaces.supported: false`, and `sharpy.serverPath` / `sharpy.debug.dotnetPath` are `machine`-scoped so a repository cannot point them at an attacker-supplied binary (#460)
- Executable paths are validated before being spawned; a configured `serverPath` that does not resolve to a file falls back to `sharpyc` with a warning (#461)
- Dependency updates

## [0.1.0] - 2026-03-06

### Added
- Initial release
- Syntax highlighting for `.spy` files via TextMate grammar
- Language server integration (diagnostics, hover, go-to-definition)
- Code snippets for common constructs (def, class, if, for, match, try, with, async def)
- Status bar indicator for language server state
- Commands: Restart Language Server, Show Output Channel
- Settings for server path, tracing, max problems, indent size, and inlay hints
- Language configuration: auto-closing pairs, bracket matching, comment toggling, indentation rules, on-enter rules
