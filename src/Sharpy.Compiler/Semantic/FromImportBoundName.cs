namespace Sharpy.Compiler.Semantic;

/// <summary>
/// One name a <c>from m import …</c> bound in its file's module scope, beside the SYMBOL the scope bound
/// it to (#2385, P21i D1). Written by ProjectCompiler Phase 4 — the one place a from-import binds — for
/// a star import and for an explicit one alike, and read after type checking by
/// <c>CodeGenInfoComputer</c>, which keeps a pair iff the module scope still binds the name to
/// <see cref="Symbol"/> (a module variable of the file that takes the name over is a different object)
/// and computes the kept name's <see cref="CodeGenInfo"/>. An explicit import that merges into a name a
/// star already bound records the star's symbol, so a takeover drops the name from both statements'
/// records. Node-keyed on the import statement in <see cref="SemanticInfo"/>; the emitter reads only
/// the names (<see cref="SemanticInfo.GetFromImportBoundNames"/>).
/// </summary>
public sealed record FromImportBoundName(string Name, Symbol Symbol);
