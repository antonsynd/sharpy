using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Pretty;

public static class Unparser
{
    public static string Unparse(Module module, UnparseOptions? options = null)
    {
        options ??= new UnparseOptions();
        var writer = new UnparseWriter(options);
        var visitor = new UnparseVisitor(writer, options);
        visitor.UnparseModule(module);
        return writer.ToString();
    }

    public static string Unparse(Node node, UnparseOptions? options = null)
    {
        options ??= new UnparseOptions();
        var writer = new UnparseWriter(options);
        var visitor = new UnparseVisitor(writer, options);
        visitor.Visit(node);
        return writer.ToString();
    }
}
