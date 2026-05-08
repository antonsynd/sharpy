namespace Sharpy.Compiler.Tests.Properties.Metamorphic;

internal interface IAstTransform
{
    string Name { get; }
    string Apply(string source);
}
