using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Pretty;

internal sealed partial class UnparseVisitor
{
    internal void WriteTypeAnnotation(TypeAnnotation type)
    {
        if (type.Name == "tuple" && !type.TupleElementNames.IsEmpty)
        {
            // Through WriteName: `tuple` lexes as an identifier, so `` `tuple`[x: int] `` keeps its escape.
            WriteName(type.Name, type.IsNameBacktickEscaped);
            _w.Write("[");
            for (int i = 0; i < type.TypeArguments.Length; i++)
            {
                if (i > 0)
                    _w.Write(", ");
                if (i < type.TupleElementNames.Length && type.TupleElementNames[i] != null)
                {
                    WriteName(type.TupleElementNames[i]!, IsPartEscaped(type.TupleElementNamesBacktickEscaped, i));
                    _w.Write(": ");
                }
                WriteTypeAnnotation(type.TypeArguments[i]);
            }
            _w.Write("]");
        }
        else
        {
            // A dotted name is written segment by segment so each segment's escape survives
            // (#2157). The parts are used only while they still spell Name — an annotation rebuilt
            // with a new Name and stale parts falls back to the joined spelling.
            if (type.NameParts.Length > 1 && string.Join(".", type.NameParts) == type.Name)
                WriteDottedName(type.NameParts, type.BacktickEscapedParts);
            else
                WriteName(type.Name, type.IsNameBacktickEscaped);
            if (!type.TypeArguments.IsEmpty)
            {
                _w.Write("[");
                for (int i = 0; i < type.TypeArguments.Length; i++)
                {
                    if (i > 0)
                        _w.Write(", ");
                    WriteTypeAnnotation(type.TypeArguments[i]);
                }
                _w.Write("]");
            }
        }

        if (type.ErrorType != null)
        {
            _w.Write(" !");
            WriteTypeAnnotation(type.ErrorType);
        }

        if (type.IsCSharpNullable)
        {
            _w.Write(" | None");
        }
        else if (type.IsOptional)
        {
            _w.Write("?");
        }
    }
}
