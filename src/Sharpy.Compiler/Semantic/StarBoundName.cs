namespace Sharpy.Compiler.Semantic;

/// <summary>
/// One name a <c>from m import *</c> bound in its file's module scope, beside the SYMBOL it bound
/// there (#2385, P21i D1). Written by ProjectCompiler Phase 4 — the one place a star binds — and read
/// after type checking by <c>CodeGenInfoComputer</c>, which keeps a pair iff the module scope still
/// binds the name to <see cref="Symbol"/> (a module variable of the file that takes the name over is a
/// different object) and computes the kept name's <see cref="CodeGenInfo"/> as an explicit
/// from-import's. Node-keyed on the star statement in <see cref="SemanticInfo"/>; the emitter reads
/// only the names (<see cref="SemanticInfo.GetStarImportBoundNames"/>).
/// </summary>
public sealed record StarBoundName(string Name, Symbol Symbol);
