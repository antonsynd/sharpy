using System.Text;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Total renderer for <see cref="TypeAnnotation"/>: every distinguishing field
/// (Name, backtick flag, TypeArguments, IsOptional, IsCSharpNullable, ErrorType,
/// TupleElementNames) contributes to the key. Two annotations produce the same
/// key iff they denote the same declared type.
/// </summary>
internal static class TypeAnnotationKey
{
    internal static string Of(TypeAnnotation? annotation)
    {
        if (annotation is null)
            return "_";

        var sb = new StringBuilder();

        if (annotation.IsNameBacktickEscaped)
            sb.Append('`');

        sb.Append(annotation.Name);

        if (annotation.TypeArguments.Length > 0)
        {
            sb.Append('[');
            for (var i = 0; i < annotation.TypeArguments.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(Of(annotation.TypeArguments[i]));
            }
            sb.Append(']');
        }

        if (annotation.IsOptional)
            sb.Append('?');

        if (annotation.IsCSharpNullable)
            sb.Append("|None");

        if (annotation.ErrorType is not null)
        {
            sb.Append('!');
            sb.Append(Of(annotation.ErrorType));
        }

        if (!annotation.TupleElementNames.IsDefaultOrEmpty)
        {
            sb.Append('{');
            for (var i = 0; i < annotation.TupleElementNames.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(annotation.TupleElementNames[i] ?? "_");
            }
            sb.Append('}');
        }

        return sb.ToString();
    }
}
