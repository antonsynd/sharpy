namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Default <see cref="ICodeEmitterFactory"/> that creates <see cref="RoslynEmitter"/> instances.
/// </summary>
internal class RoslynEmitterFactory : ICodeEmitterFactory
{
    public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default) =>
        new RoslynEmitter(context, cancellationToken);
}
