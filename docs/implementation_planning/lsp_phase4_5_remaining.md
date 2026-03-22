# Sharpy LSP Maturity Plan — Remaining Phases

> Extracted from `lsp_maturity_plan.md` (Phases 0-3 completed, archived to `docs/completed_plans/`).

---

## Phase 4: The 2026 Edge — Agentic Interoperability

**Scope: Medium-Large | Priority: Medium | Depends on: Phase 0-1 complete**

### 4.1 — MCP Server Integration

Expose the LSP's semantic model as an MCP server that AI assistants can query:

```
tools:
  - get_symbol_at_position(file, line, col) -> Symbol + Type + References
  - get_diagnostics(file?) -> structured diagnostic list
  - get_type_hierarchy(symbol) -> tree
  - get_call_graph(symbol, depth) -> directed graph
  - search_symbols(query, kind?) -> matches
  - get_semantic_context(file, line_range) -> annotated code with types
```

This is relatively straightforward — the data already exists in `SemanticInfo`, `SymbolTable`, and the handlers. You're building a JSON API on top of the same queries the handlers use. The compiler already has `ISemanticQuery` as a clean read-only interface.

### 4.2 — Diagnostic Quick-Fix Suggestions for AI

Enrich diagnostics with structured "fix hints" — not just "type mismatch: expected int, got str" but also `{ suggestedFix: "wrap with int()", confidence: 0.9 }`. This lets AI tools auto-apply fixes without understanding the language.

### 4.3 — Semantic Code Graph Export

Export the project's full symbol graph (types, functions, imports, call relationships) as a queryable structure. This is the "structural context" that modern AI tools consume. The `DependencyGraph` + `SymbolTable` + `SemanticInfo.GetReferences()` give you all the edges.

---

## Phase 5: Polish & Parity

**Scope: Medium | Priority: Lower | Ongoing**

### 5.1 — Range Formatting

Currently only full-document formatting. Add `textDocument/rangeFormatting` to format a selection. The lexer-based formatter needs to handle partial indentation contexts.

### 5.2 — Selection Range

`textDocument/selectionRange` — expand selection to next syntactic scope (expression -> statement -> block -> function -> class -> module). Walk the AST containment hierarchy using `AstPositionService.FindAllContainingNodes()`.

### 5.3 — Linked Editing Ranges

For string interpolation, f-strings, or paired delimiters — highlight both ends simultaneously. Low priority but nice UX.

### 5.4 — Notebook Support

If Sharpy ever targets notebook-style execution (like Jupyter for Python), the LSP would need `NotebookDocumentSyncHandler`. Low priority.

---

## Phasing Summary

```
Phase 4  [2026 Edge]
  4.1 MCP server               **  (medium, data exists)
  4.2 AI fix suggestions       **  (medium)
  4.3 Semantic graph export    **  (medium)

Phase 5  [Polish]                             <- ongoing
  5.1-5.4 Range fmt, selection, linked editing, notebooks
```
