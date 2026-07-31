namespace Sharpy.Compiler.Semantic;

/// <summary>
/// A scoped assignment to a field: sets the field on construction and restores the previous value on
/// disposal, so the restore happens on every exit path — including an exception thrown inside the
/// scope (#1218).
///
/// <para>This is the successor to the hand-rolled <c>save / set / restore</c> idiom the
/// <see cref="TypeChecker"/>'s <c>_current*</c> expression-context tracker family was written with.
/// Each of those sites saved the field into a local, assigned, and restored the local by hand with
/// no <c>try</c>/<c>finally</c>, so an exception thrown between the assignment and the restore left
/// the tracker stale. The scoped form cannot leave it stale.</para>
///
/// <para>It is a <c>ref struct</c> for two reasons. First, a <c>ref</c> field can only live in a
/// <c>ref struct</c> — C# cannot store a <c>ref T</c> in a class. Second, and the reason the
/// reference is worth having at all, these scopes are entered per checked call, index, and binding
/// node across an entire compilation: a class-based scope holding an <c>Action&lt;T&gt;</c> restore
/// delegate would allocate two objects per node on the checker's hottest path. This form allocates
/// nothing.</para>
///
/// <para>A <c>ref struct</c> cannot cross an <c>await</c> or a <c>yield</c>. That is intentional:
/// every tracker site is in a synchronous checker method, so the restriction costs nothing today and
/// is a desirable tripwire if someone later tries to make one of these paths async — a tracker
/// suspended across an await would restore into the wrong logical context.</para>
///
/// <para>The sibling idiom already in tree is <see cref="TypeNarrowingContext.EnterScope"/>, which
/// returns an <see cref="System.IDisposable"/> and is used the same way. This type needs no interface:
/// <c>using</c> binds to <see cref="Dispose"/> by pattern on a <c>ref struct</c>.</para>
///
/// <para><b>Prefer the <c>using (…) { … }</c> statement form to a <c>using var</c> declaration.</b> A
/// <c>using var</c> disposes at the end of the enclosing block; nearly every tracker site restores in
/// the middle of a long method, deliberately, with the rest of the method meant to run with the
/// tracker cleared.</para>
/// </summary>
/// <typeparam name="T">The field's type.</typeparam>
internal ref struct ScopedValue<T>
{
    // `readonly ref T`, NOT `ref readonly T`: the REFERENCE is fixed once the scope is constructed
    // while the REFERENT stays writable, which is exactly what Dispose() needs in order to assign the
    // saved value back. `ref readonly T` would make the referent read-only and would not compile here.
    private readonly ref T _field;

    private readonly T _saved;

    /// <summary>
    /// Saves <paramref name="field"/>'s current value and assigns <paramref name="value"/> to it.
    /// </summary>
    /// <param name="field">The field to assign for the duration of the scope.</param>
    /// <param name="value">The value to assign. Passing the field's current value is a deliberate
    /// no-op push, which is how a conditional site keeps the enclosing value when its condition does
    /// not hold.</param>
    public ScopedValue(ref T field, T value)
    {
        _field = ref field;
        _saved = field;
        field = value;
    }

    /// <summary>
    /// Restores the value the field held when the scope was constructed.
    /// </summary>
    public void Dispose() => _field = _saved;
}

/// <summary>
/// Factory for <see cref="ScopedValue{T}"/> so call sites never have to spell the type argument.
/// </summary>
internal static class ScopedValue
{
    /// <summary>
    /// Assigns <paramref name="value"/> to <paramref name="field"/> until the returned scope is
    /// disposed.
    /// </summary>
    /// <example>
    /// <code>
    /// SemanticType inferredType;
    /// using (ScopedValue.Push(ref _currentBindingValue, assignment.Value))
    ///     inferredType = CheckExpression(assignment.Value);
    /// </code>
    /// </example>
    public static ScopedValue<T> Push<T>(ref T field, T value) => new(ref field, value);
}
