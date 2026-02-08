using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Adapts the internal <see cref="ImportResolver"/> to the public <see cref="IImportQuery"/> interface.
/// </summary>
internal class ImportQueryAdapter : IImportQuery
{
    private readonly ImportResolver _importResolver;

    public ImportQueryAdapter(ImportResolver importResolver)
    {
        _importResolver = importResolver;
    }

    public IReadOnlyCollection<string> LoadedModulePaths =>
        (IReadOnlyCollection<string>)_importResolver.LoadedSpyModules.Keys;
}
