using System.Text.RegularExpressions;

namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

internal sealed partial class AddRedundantTypeAnnotationTransform : IAstTransform
{
    public string Name => "AddRedundantTypeAnnotation";

    public string Apply(string source)
    {
        return UnannotatedIntAssign().Replace(source, match =>
        {
            var indent = match.Groups[1].Value;
            var name = match.Groups[2].Value;
            var value = match.Groups[3].Value;
            return $"{indent}{name}: int = {value}";
        });
    }

    [GeneratedRegex(@"^(\s+)(\w+)\s*=\s*(\d+)\s*$", RegexOptions.Multiline)]
    private static partial Regex UnannotatedIntAssign();
}
