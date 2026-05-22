namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Creates <see cref="ICodeEmitter"/> instances for each compilation unit.
/// </summary>
internal interface ICodeEmitterFactory
{
    ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default);
}
