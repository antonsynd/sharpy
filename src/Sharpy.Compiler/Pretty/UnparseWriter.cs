using System.Text;

namespace Sharpy.Compiler.Pretty;

internal sealed class UnparseWriter
{
    private readonly StringBuilder _sb = new();
    private readonly string _indent;
    private readonly string _lineEnding;
    private int _indentLevel;
    private bool _atLineStart = true;

    public UnparseWriter(UnparseOptions options)
    {
        _indent = options.IndentString;
        _lineEnding = options.LineEnding;
    }

    public void Indent() => _indentLevel++;
    public void Dedent() => _indentLevel--;

    public void Write(string text)
    {
        if (_atLineStart && text.Length > 0)
        {
            for (int i = 0; i < _indentLevel; i++)
                _sb.Append(_indent);
            _atLineStart = false;
        }
        _sb.Append(text);
    }

    public void WriteLine()
    {
        _sb.Append(_lineEnding);
        _atLineStart = true;
    }

    public void WriteLine(string text)
    {
        Write(text);
        WriteLine();
    }

    public override string ToString() => _sb.ToString();
}
