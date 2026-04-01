using System.Globalization;
using System.Text;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Playground.Models;

namespace Sharpy.Playground.Services;

public sealed class CompilerService
{
    private CompilerApi? _api;

    private CompilerApi Api => _api ??= new CompilerApi();

    public CompilerOutput Compile(string source)
    {
        try
        {
            var options = new CompilerOptions { OutputType = "library" };
            var result = Api.Compile(source, options, filePath: "playground.spy");

            var csharp = result.Success && result.GeneratedCSharp != null
                ? StripLineDirectives(result.GeneratedCSharp)
                : "";

            var ast = FormatAst(source);
            var tokens = FormatTokens(source);
            var diagnostics = FormatDiagnostics(result.Diagnostics, source);

            return new CompilerOutput(csharp, ast, tokens, diagnostics, result.Success);
        }
        catch (Exception ex)
        {
            return new CompilerOutput(
                "",
                "",
                "",
                $"Internal compiler error: {ex.Message}",
                false);
        }
    }

    private string FormatAst(string source)
    {
        try
        {
            var parseResult = Api.Parse(source);
            if (parseResult.Ast == null)
                return "Parse failed — no AST produced.";

            var dumper = new AstDumper();
            return dumper.Dump(parseResult.Ast);
        }
        catch (Exception ex)
        {
            return $"AST dump failed: {ex.Message}";
        }
    }

    private static string FormatTokens(string source)
    {
        try
        {
            var lexer = new Lexer(source);
            var tokens = lexer.TokenizeAll();

            var sb = new StringBuilder();
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                var value = string.IsNullOrEmpty(token.Value) ? "" : $" = '{token.Value}'";
                sb.AppendLine(CultureInfo.InvariantCulture, $"{i,4}: {token.Type,-20} @ L{token.Line}:C{token.Column}{value}");
            }
            sb.AppendLine(CultureInfo.InvariantCulture, $"Total tokens: {tokens.Count}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Tokenization failed: {ex.Message}";
        }
    }

    private string FormatDiagnostics(IReadOnlyList<CompilerDiagnostic> diagnostics, string source)
    {
        if (diagnostics.Count == 0)
            return "No diagnostics.";

        var sb = new StringBuilder();
        foreach (var diag in diagnostics)
        {
            sb.AppendLine(Api.FormatDiagnostic(diag, source));
        }
        return sb.ToString();
    }

    private static string StripLineDirectives(string csharpCode)
    {
        var lines = csharpCode.Split('\n');
        var filtered = lines.Where(line => !line.TrimStart().StartsWith("#line "));
        return string.Join('\n', filtered);
    }
}
