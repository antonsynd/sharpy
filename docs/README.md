# Sharpy Documentation

This directory contains all documentation for the Sharpy language and compiler.

## Organization

Documentation is organized into the following subdirectories:

### 📋 specs/
Language specifications and type system definitions.
- `language_reference.md` - Complete Sharpy language specification (6,391 lines)
- `type_system.md` - Type system specification (863 lines)
- `builtins.md` - Built-in types reference (60 lines)

### 🏗️ architecture/
Architectural design documents for compiler components.
- `codegen-architecture.md` - Code generation architecture (1,093 lines)
- `semantic-analyzer-architecture.md` - Semantic analyzer architecture (2,961 lines)
- `logging-architecture.md` - Logging architecture (632 lines)
- `cached-overload-discovery.md` - Cached overload discovery design (4,316 lines)
- `sharpy-csharp-feature-enhancements.md` - C# feature enhancements (1,699 lines)

### 📝 planning/
Implementation plans and migration guides.
- `parser_implementation_plan.md` - Parser implementation plan (1,441 lines)
- `codegen-implementation-plan-v0.5.md` - Code generation plan for v0.5 (1,708 lines)
- `sharpy-core-migration-v0.5.md` - Sharpy.Core migration guide (1,096 lines)

### 📊 status/
Current implementation status and feature support.
- `stdlib-implementation-status.md` - Standard library status (855 lines)
- `cached-overload-discovery-status.md` - Overload discovery status (241 lines)
- `feature_support.md` - Feature support matrix (267 lines)

### ✅ validation/
Feature validation and verification guides.
- `v0.5-features-validation.md` - Feature validation checklist (2,129 lines)
- `v0.5-validation-status.md` - Validation status tracking (1,030 lines)
- `v0.5-verification-guide.md` - Detailed verification guide (1,092 lines)
- `v0.5-feature-list.md` - Complete feature list (1,033 lines)

### 📦 archived/
Historical session summaries and old documentation.
- `SESSION_SUMMARY.md` - Cached overload discovery session summary

### 🔧 manual/
User manual and guides (not reorganized).

### 📌 Root Level
- `ignore.md` - Internal notes for repository owner

## Quick Links

**Getting Started:**
- Language Reference: [specs/language_reference.md](specs/language_reference.md)
- Type System: [specs/type_system.md](specs/type_system.md)

**For Contributors:**
- Code Generation Plan: [planning/codegen-implementation-plan-v0.5.md](planning/codegen-implementation-plan-v0.5.md)
- Feature Validation: [validation/v0.5-features-validation.md](validation/v0.5-features-validation.md)
- Implementation Status: [status/stdlib-implementation-status.md](status/stdlib-implementation-status.md)

**Architecture:**
- Code Generation: [architecture/codegen-architecture.md](architecture/codegen-architecture.md)
- Semantic Analysis: [architecture/semantic-analyzer-architecture.md](architecture/semantic-analyzer-architecture.md)

## Current Status (as of 2025-11-19)

**Test Coverage:**
- Runtime tests: 716 passing ✅
- Compiler tests: 1,189 passing (43 skipped) ✅
- **Total: 1,905 passing tests**

**v0.5 Completion:**
- Language features: ~95% complete
- Code generation: v0.5 feature complete ✅
- Standard library: ~70-75% complete
- Validation: ~90% complete
