using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Pretty;

internal sealed partial class UnparseVisitor
{
    internal void WriteTypeAnnotation(TypeAnnotation type)
    {
        if (type.Name == "tuple" && !type.TupleElementNames.IsEmpty)
        {
            _w.Write("tuple[");
            for (int i = 0; i < type.TypeArguments.Length; i++)
            {
                if (i > 0)
                    _w.Write(", ");
                if (i < type.TupleElementNames.Length && type.TupleElementNames[i] != null)
                {
                    _w.Write(type.TupleElementNames[i]!);
                    _w.Write(": ");
                }
                WriteTypeAnnotation(type.TypeArguments[i]);
            }
            _w.Write("]");
        }
        else
        {
            _w.Write(type.Name);
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
