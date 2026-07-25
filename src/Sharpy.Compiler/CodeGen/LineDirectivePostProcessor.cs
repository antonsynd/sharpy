namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Post-processes generated C# source to:
/// 1. Fix up charOffset in enhanced #line directives (set to actual indentation)
/// 2. Insert #line hidden for multi-line C# constructs between #line directives
/// </summary>
/// <remarks>
/// Since #1108 this is a thin text applier over <see cref="LineDirectiveEditPlanner"/>. Since the
/// #1126 revert it is the <b>production</b> #line path again: the default emit seam post-processes
/// the emitted text with <see cref="Process"/> and reparses it (the #1108 zero-parse tree rewrite
/// lost at scale — 2.9× on directive-dense files, see the benchmark on #1126).
/// <see cref="LineDirectiveTreeRewriter"/> is the one retained without production callers, as the
/// corpus-differential subject and benchmark arm; a corpus-wide conformance test asserts its tree
/// text stays byte-identical to <see cref="Process"/>. Both share the one planner, so the #line
/// arithmetic cannot drift between them.
/// </remarks>
internal static class LineDirectivePostProcessor
{
    public static string Process(string csharpCode)
    {
        return LineDirectiveEditPlanner.Plan(csharpCode).ApplyToText(csharpCode);
    }
}
