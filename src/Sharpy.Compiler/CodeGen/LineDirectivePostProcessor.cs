namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Post-processes generated C# source to:
/// 1. Fix up charOffset in enhanced #line directives (set to actual indentation)
/// 2. Insert #line hidden for multi-line C# constructs between #line directives
/// </summary>
/// <remarks>
/// Since #1108 this is a thin text applier over <see cref="LineDirectiveEditPlanner"/>. It stays as the
/// differential-testing oracle (D5): the zero-parse default emit path uses
/// <see cref="LineDirectiveTreeRewriter"/>, and a corpus-wide conformance test asserts the rewriter's
/// tree text is byte-identical to <see cref="Process"/>. Both share the one planner, so the #line
/// arithmetic cannot drift between them.
/// </remarks>
internal static class LineDirectivePostProcessor
{
    public static string Process(string csharpCode)
    {
        return LineDirectiveEditPlanner.Plan(csharpCode).ApplyToText(csharpCode);
    }
}
