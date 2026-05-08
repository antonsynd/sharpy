using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class SemanticFilter
{
    public static Gen<Module> WellTypedProgram(Gen<Module> source) =>
        source.Where(module =>
        {
            try
            {
                var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Analyze(unparsed, "filter_test.spy");
                return result.Success;
            }
            catch
            {
                return false;
            }
        });

    public static Gen<Module> CompilableProgram(Gen<Module> source) =>
        source.Where(module =>
        {
            try
            {
                var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Compile(unparsed, "filter_test.spy");
                return result.Success;
            }
            catch
            {
                return false;
            }
        });
}
