# Sharpy Compiler Tooling Readiness Assessment

## Readiness Matrix

| Dimension | Rating | Status | Next Priority |
|-----------|--------|--------|----------------|
| **LSP** | PARTIAL | Position tracking ✓; incremental updates ✗ | Implement incremental SymbolTable |
| **Parallel Build** | PARTIAL | Per-file independence ✓; thread-safety ✗ | Thread-safe SemanticInfo wrapper |
| **REPL** | NOT READY | Module-only structure; no statement-level API | Design statement-level compilation |
| **Formatter** | PARTIAL | Comments preserved ✓; roundtripping ✗ | AST → source reconstruction |
| **Debugger** | READY | #line pragmas implemented ✓; source mapping ✓ | Ready to build with Roslyn |
| **Phase 8/10 Implications** | MEDIUM | Pattern matching; async types | Design ahead for LSP discovery |

---

## Detailed Analysis

### 1. LSP (Language Server Protocol) Readiness — **PARTIAL**

#### Current State
✅ **What's in place:**
- **Position tracking**: `TextSpan` struct (start, length) with O(log n) line/column lookup via `SourceText.GetLineAndColumn()`
- **Effective type queries**: `SemanticInfo.GetEffectiveType()` considers type narrowing (e.g., `T?` → `T` after `if x is not None`)
- **Identifier-to-symbol mapping**: `SemanticInfo.GetIdentifierSymbol()` enables "go to definition"
- **Function call targets**: `SemanticInfo.GetCallTarget()` for function resolution
- **Call site inference**: `SemanticInfo.GetInferredTypeArguments()` for generic functions without explicit type arguments

✅ **AST node locatability**:
- All `Node` records implement `ILocatable` with `TextSpan? Span` property (file: `src/Sharpy.Compiler/Parser/Ast/Node.cs`)
- Tokens implement `ILocatable` via `Token.GetSpan()` (file: `src/Sharpy.Compiler/Lexer/Token.cs`)
- Line/column properties on all nodes for backward compatibility

✅ **Trivia (comments) preservation**:
- `Token` record has `LeadingTrivia` and `TrailingTrivia` properties (`IReadOnlyList<Trivia>?`)
- `Trivia` contains `Kind`, `Text`, `Line`, `Column`, `Position`
- Lexer can produce `TokenType.Comment` tokens (skipped but available)

❌ **What's missing for LSP**:
- **Incremental SymbolTable updates**: `SymbolTable.Remove()` exists but only removes from current scope; no cross-file symbol invalidation
- **Per-file SemanticInfo**: Currently shared across compilation; would need per-file instances for parallel LSP analysis
- **Symbol tracking for hover**: No incremental caching of symbol metadata (e.g., docstrings, type information)
- **Workspace model**: No abstraction for multi-file document state (LSP needs to track open/unsaved buffers)

#### LSP Use Cases & Gaps
| Feature | Readiness | Blocker |
|---------|-----------|---------|
| Hover (type information) | 90% | None — `GetEffectiveType()` provides types |
| Go to definition | 80% | Need file-to-symbol index; currently O(n) lookup |
| Find references | 70% | `find_referencing_symbols` exists (Serena tool); needs LSP protocol binding |
| Diagnostics | 95% | `DiagnosticBag` is thread-safe; diagnostics track `TextSpan` |
| Completion | 20% | No scope-aware name table; `SymbolTable.GetVisibleSymbolNames()` exists but no type/kind info |
| Rename | 40% | Rename via Serena tools; needs LSP server integration |

**File References**:
- `src/Sharpy.Compiler/Semantic/SemanticInfo.cs` — Query methods (lines 85–145)
- `src/Sharpy.Compiler/Text/SourceText.cs` — Position tracking (lines 85–110)
- `src/Sharpy.Compiler/Semantic/SymbolTable.cs` — Symbol lookup (lines 28–51)

#### Recommendation
**Priority 1 (High)**: Build LSP server as a layer over existing infrastructure:
1. Create `ILspWorkspace` to manage multi-file document state
2. Add per-file `SemanticInfo` instances with shared (thread-safe) `SymbolTable`
3. Implement incremental compilation trigger on document changes
4. Use `CompilerServices` adapter pattern to provide LSP-friendly type/symbol queries

**Phase 8/10 Note**: Pattern matching will need LSP support for go-to-definition on union case patterns. Design union case symbols (e.g., `Ok`, `Err` in `Result[T, E]`) with `DeclaringFilePath` for LSP navigation.

---

### 2. Parallel Build Readiness — **PARTIAL**

#### Current State

✅ **Per-file independence**:
- `ProjectCompiler.ParseAllFiles()` → produces independent `CompilationUnit` ASTs
- Files processed in dependency order (transitive closure computed upfront)
- `IncrementalCompilationCache` tracks file dependencies via `DependencyGraph`

✅ **Shared state is designed for sequential access**:
- `SymbolTable`: Stack-based scope; push/pop for function/class scopes
- `SemanticBinding`: Stores computed data separately from symbols; materialized at phase boundaries
- `DiagnosticBag`: Thread-safe via `lock (_lock)` on all public methods

❌ **What's NOT thread-safe**:

| Component | Issue | Evidence | File |
|-----------|-------|----------|------|
| `SemanticInfo` | Uses `Dictionary<AstNode, T>` without lock | Comments: *"This type is not thread-safe"* | `SemanticInfo.cs:5–6` |
| `SymbolTable` scope stack | Non-thread-safe `Stack<Scope>` | No locking; relies on sequential processing | `SymbolTable.cs` |
| `ProjectCompiler._symbolTableBacking` | Shared instance; no concurrent writer protection | Accessed from multiple phases | `ProjectCompiler.cs:27` |

**Critical Issue**: If `ProjectCompiler` parallelizes file processing (e.g., via `Parallel.ForEach`), all three components need wrapping.

#### Thread-Safety Audit
| Operation | Thread-Safe? | Recommendation |
|-----------|--------------|-----------------|
| `SymbolTable.Define()` | ❌ No | Freeze after name resolution; add read-only wrapper |
| `SemanticInfo.SetExpressionType()` | ❌ No | Wrap with `ConcurrentDictionary` or per-file instances |
| `DiagnosticBag.Add()` | ✅ Yes | Safe for concurrent writers (file: `DiagnosticBag.cs:71–82`) |
| `SemanticBinding.Set*()` | ❌ No | Document as write-once; add thread-safety checks |

**File References**:
- `src/Sharpy.Compiler/Semantic/SemanticInfo.cs` — Not thread-safe (lines 5–6 comment)
- `src/Sharpy.Compiler/Project/ProjectCompiler.cs` — Shared state initialization (lines 20–50)
- `src/Sharpy.Compiler/Diagnostics/DiagnosticBag.cs` — Thread-safe implementation (lines 71–82)

#### Recommendation
**Priority 2 (Medium)**: Enable parallel per-file semantic analysis:
1. Make `SemanticInfo` thread-safe (wrap dictionaries with `ConcurrentDictionary`)
2. Document `SymbolTable` and `SemanticBinding` as frozen after name resolution
3. Add CI test for parallel compilation (use `Parallel.ForEach` on type checking phase)
4. Target **2–5x speedup** on 8-core hardware (assuming typical projects have 10–100 files)

---

### 3. REPL Readiness — **NOT READY**

#### Current State

❌ **Fundamental architecture mismatch**:
- **Module = compilation unit**: Sharpy always compiles full modules (parse → name resolution → type checking → codegen)
- **Statement-level compilation**: Not supported; all statements are embedded in a `Module.Body` array
- **Symbol accumulation**: No API to add definitions to an existing symbol table without full re-parse

**File References**:
- `src/Sharpy.Compiler/Parser/Ast/Node.cs` — Module structure (lines 78–92)
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs` — All statements

#### What Would Be Needed

1. **Statement-level type checking**: Extract `TypeChecker.CheckStatement()` as public API
2. **Incremental name resolution**: Add symbols to `SymbolTable` without re-resolving inheritance
3. **Type context preservation**: Store narrowed types across REPL statements
4. **Code generation per-statement**: Emit C# for individual statements (currently generates full module class)

#### Implementation Estimate
- **Design**: 2–3 days (statement-level API, context preservation)
- **Implementation**: 1–2 weeks (extract statement handling, design symbol table increments)
- **Testing**: 1 week (REPL test suite, edge cases with nested scopes)
- **Risk**: **HIGH** — would require architectural changes to module/statement relationship

#### Recommendation
**Priority 4 (Low / Deferred)**: REPL is a v0.3+ feature. For now:
1. Design incremental statement API (RFC document)
2. Add `StatementCompiler` partial class to `Compiler` (placeholder)
3. Track as future work in roadmap

---

### 4. Formatter Readiness — **PARTIAL**

#### Current State

✅ **Trivia preservation**:
- Lexer captures comments via `LeadingTrivia` and `TrailingTrivia` on tokens
- `Trivia` record includes text, position, and location
- Comments are not stripped during parsing (preserved in token stream)

✅ **Indentation tracking**:
- `TokenType.Indent` and `TokenType.Dedent` track block structure
- Source locations preserved on all AST nodes

❌ **Roundtrip parsing**:
- **Parse → AST → Source**: AST lacks enough information to perfectly reconstruct source
- **Whitespace loss**: Blank lines, multiple spaces, and indentation details lost in AST
- **Comment placement**: Trivia is attached to tokens, not AST nodes; disconnected during parsing

**File References**:
- `src/Sharpy.Compiler/Lexer/Token.cs` — Trivia support (lines 168–211)
- `src/Sharpy.Compiler/Parser/Ast/Node.cs` — Node structure (no trivia field)

#### What Would Be Needed

1. **Whitespace-preserving AST**: Either:
   - Store trivia on AST nodes (not just tokens), or
   - Use a separate "source map" tracking whitespace positions
2. **Comment reattachment**: Map trivia from token stream back to AST nodes
3. **Pretty-printer**: Implement `AST → string` via Roslyn-style `SyntaxNode.ToFullString()`

#### Implementation Estimate
- **Design**: 3–5 days (AST annotation strategy)
- **Implementation**: 2–3 weeks (reattach trivia, pretty-printer)
- **Testing**: 1 week (roundtrip test suite)

#### Recommendation
**Priority 3 (Medium / Deferred)**: Formatter is useful but not blocking tooling:
1. Add `IsSynthetic` property to `Node` to distinguish user-written vs. generated code
2. Design trivia reattachment strategy (attach to AST nodes during parsing)
3. Implement pretty-printer incrementally
4. Start with simple cases (no complex nested expressions)

**Note**: For LSP, formatter is less critical than hover/completion. Defer until Phase 8+.

---

### 5. Debugger Readiness — **READY** ✅

#### Current State

✅ **Source mapping via #line pragmas**:
- `RoslynEmitter.AttachSourceMapping()` emits `#line N "file.spy"` directives
- Located in `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs:115–141`
- Example output:
```csharp
#line 5 "program.spy"
var x = 5;
```

✅ **AST nodes carry TextSpan**:
- All `Node` records have `Span` property with character offsets
- `SourceText.GetLineAndColumn()` provides O(log n) lookup
- Symbols track `DeclarationSpan` and `DeclaringFilePath`

✅ **Generated C# is deterministic**:
- `RoslynEmitter` uses `SyntaxFactory` exclusively (no string templating)
- Output is idempotent; same input produces same C#

#### Debugger Integration Path

1. **Existing infrastructure**:
   - C# compiler respects `#line` directives
   - Generated `.pdb` files contain source mappings
   - Standard .NET debuggers (Visual Studio, VS Code, WinDbg) understand #line pragmas

2. **Next steps**:
   - Generate `.pdb` files with embedded `.spy` sources (optional but helpful)
   - Create VS Code extension that:
     - Recognizes `.spy` source locations in stack traces
     - Maps breakpoints from `.spy` → generated `.cs` → `.dll`
   - Consider `DebuggerNonUserCodeAttribute` on synthetic code

#### Implementation Estimate
- **MVP (VS Code extension)**: 1–2 days (parse stack traces, source mapping)
- **Full integration (PDB embedding)**: 1 week
- **Risk**: **LOW** — builds on existing infrastructure

#### Recommendation
**Priority 1 (High / Could start now)**: Debugger is ready for immediate implementation:
1. Start with CLI: add `--debug` flag to emit `.pdb` files
2. Build VS Code extension for `.spy` support
3. Document source mapping in dev guide
4. **Timeline**: Ship by v0.1.7 (next release after foundational work)

#### Phase 8/10 Note
- **Pattern matching** (Phase 8): No special debugger support needed (patterns lower to C# `switch` expressions)
- **Async** (Phase 10): `Task<T>` appears in hover info; may need custom visualizer for `AsyncIterator<T>`

---

### 6. Phase 8 (Pattern Matching) Implications

#### What Phase 8 Adds
- Type patterns with binding: `case int() as n:`
- Or patterns: `case "a" | "b":`
- Property patterns: `case Point(x=0):`
- Positional patterns: `case Point(0, y):`
- Relational patterns: `case > 0:`

See `docs/language_specification/match_statement.md` for details.

#### Tooling Requirements
| Pattern Form | LSP Requirement | Impact |
|--------------|-----------------|--------|
| **Type pattern** | Go-to-definition on type name | Existing infrastructure handles this |
| **Property pattern** | Hover on property name → property type | Needs property symbol resolution |
| **Positional pattern** | Hover on positional argument → parameter type | Needs `Deconstruct` method lookup |
| **Or pattern** | Completion for alternative cases | Context-aware name table |
| **Exhaustiveness** | Diagnostic: missing cases | New `ExhaustivenessValidator` (planned per spec) |

#### Risk Assessment
**LOW**: Pattern matching is syntactic sugar over existing type checking. Requires:
1. Extend `SemanticInfo` to track pattern-to-type mappings
2. Add pattern symbol resolution in `TypeChecker.Expressions.cs`
3. Pattern symbols are temporary (not stored in SymbolTable)

---

### 7. Phase 10 (Async) Implications

#### What Phase 10 Adds
- `async def` function definitions
- `await` expressions
- `async for` loops
- `async with` context managers
- `AsyncIterator[T]` return type

See `docs/language_specification/async_programming.md` for details.

#### Tooling Requirements
| Feature | LSP Requirement | Complexity |
|---------|-----------------|-----------|
| **Type display** | Hover shows `Task<int>` for async function | Requires `TaskType` in type system |
| **Await validation** | Diagnostic: await outside async function | Contextual analysis (scope tracking) |
| **Async iterator completion** | Suggest `async for` in completion | Context-aware keyword filtering |
| **Debugger stepper** | Step over async boundaries | VS Code async debugging already handles this |

#### Risk Assessment
**MEDIUM**: Async adds a new type family (`Task[T]`, `AsyncIterator[T]`) with special lowering:
1. `AsyncIterator[T]` → `IAsyncEnumerable<T>` (C# 8+)
2. `Task[T]` → `System.Threading.Tasks.Task<T>`
3. Await expressions must lower to `.ConfigureAwait(false).GetAwaiter().GetResult()`

**Design concern**: Type narrowing in async context (e.g., `if x is not None:` inside `async for`) needs interaction with scope tracking.

#### Recommendation
- Design `TaskType` and `AsyncIteratorType` in `SemanticType` hierarchy now (Phase 8)
- Add infrastructure in Phase 9 (reserved)
- Implement Phase 10 with confidence

---

## Recommendations Summary

### Tier 1 (Start Now)
1. **Debugger (Priority 1)**: Build VS Code extension; ship by v0.1.7
   - **Effort**: 1 week
   - **Impact**: Professional debugging experience
2. **LSP Foundation (Priority 1)**: Implement hover/go-to-definition/diagnostics
   - **Effort**: 2 weeks (architecture + initial features)
   - **Impact**: IDE integration (VS Code, Vim, Emacs)

### Tier 2 (Phase 8 Prep)
3. **Parallel Build (Priority 2)**: Make `SemanticInfo` thread-safe
   - **Effort**: 3 days
   - **Impact**: 2–5x faster compilation on multi-core
4. **Pattern Matching Types (Priority 2)**: Design union case symbols for LSP
   - **Effort**: 1 day (design), 3 days (implementation)
   - **Impact**: Pattern matching debuggable/discoverable in IDE

### Tier 3 (Phase 9+, Optional)
5. **Formatter (Priority 3)**: Implement pretty-printer with trivia reattachment
   - **Effort**: 2–3 weeks
   - **Impact**: Auto-formatting support
6. **REPL (Priority 4)**: Design statement-level compilation API
   - **Effort**: RFC + 1 week design
   - **Impact**: Interactive development (future feature)

---

## Architectural Recommendations

### For LSP
- Create `ILspWorkspace` interface managing multi-file state
- Implement `WorkspaceAnalyzer` running semantic analysis on open files
- Use existing `CompilerServices` as query layer

### For Parallel Build
- Wrap `SemanticInfo` in `ConcurrentSemanticInfo` using `ConcurrentDictionary<K, V>`
- Document `SymbolTable` as frozen after name resolution
- Add CI test: `ProjectCompiler` with `Parallel.ForEach`

### For Formatter
- Add `Trivia reattachmentPass` between parsing and semantic analysis
- Store `IReadOnlyList<Trivia>` on each `Node`
- Implement `PrettyPrinter` visitor pattern

### For Debugger
- Already ready; just needs VS Code extension

---

## Key File References Summary

| Component | File | Readiness |
|-----------|------|-----------|
| Position tracking | `src/Sharpy.Compiler/Text/SourceText.cs` | ✅ Ready |
| Semantic queries | `src/Sharpy.Compiler/Semantic/SemanticInfo.cs` | ✅ Ready (not thread-safe) |
| Symbol management | `src/Sharpy.Compiler/Semantic/SymbolTable.cs` | ✅ Ready (sequential only) |
| Source mapping | `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs:115–141` | ✅ Ready |
| Trivia handling | `src/Sharpy.Compiler/Lexer/Token.cs:168–211` | ✅ Ready |
| Diagnostics | `src/Sharpy.Compiler/Diagnostics/DiagnosticBag.cs:71–82` | ✅ Ready (thread-safe) |
| Incremental caching | `src/Sharpy.Compiler/Project/ProjectCompiler.IncrementalCache.cs` | ✅ Ready |
| Parallel compilation | `src/Sharpy.Compiler/Project/ProjectCompiler.cs` | ⚠️ Sequential only |
| Module structure | `src/Sharpy.Compiler/Parser/Ast/Node.cs:78–92` | ⚠️ No REPL API |

---

## Conclusion

Sharpy has a **solid foundation for tooling**:
- ✅ **Debugger**: Ready now (just needs VS Code extension)
- ✅ **LSP**: 80% ready (needs incremental updates and workspace model)
- ⚠️ **Parallel build**: 70% ready (thread-safety gaps in `SemanticInfo`)
- ⚠️ **Formatter**: 50% ready (trivia attached to tokens, not AST)
- ❌ **REPL**: 0% ready (architectural mismatch with module-level compilation)

**Recommended first project**: VS Code extension with debugger + LSP hover/completion. Builds on existing infrastructure and delivers immediate user value.

**Timeline**: Debugger (1 week) → LSP foundation (2 weeks) → Parallel build (1 week) = **4 weeks to professional tooling baseline**.
