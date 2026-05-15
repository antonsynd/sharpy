namespace Sharpy.Generators
{
    /// <summary>
    /// Base class for Sharpy source generators. Subclasses produce Sharpy source code
    /// from a <see cref="GeneratorContext"/> describing the decorated declaration.
    /// </summary>
    public abstract class SourceGenerator
    {
        /// <summary>Generate Sharpy source for the target described by <paramref name="context"/>.</summary>
        public abstract GeneratorOutput Generate(GeneratorContext context);
    }
}
