using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// <c>str()</c> of an <c>Optional</c> can run on any thread. <see cref="Optional"/> caches its
/// reflection accessors per closed <c>Optional&lt;T&gt;</c> type in a static table; that table must
/// survive concurrent first use. With a plain <c>Dictionary</c> the table corrupts itself under
/// concurrent inserts and every later <c>str(Some(x))</c> in the process throws
/// <see cref="InvalidOperationException"/> (seen as 12 unrelated reds in one Core.Tests run).
/// </summary>
public class OptionalFormatConcurrencyTests
{
    /// <summary>
    /// Many closed <c>Optional&lt;T&gt;</c> types the cache has never seen, each formatted for the
    /// first time from several threads at once, so the cache takes concurrent inserts. The types are
    /// private to this test (nested in <see cref="Marker{T}"/>) so no other test warms them first.
    /// </summary>
    [Fact]
    public async Task Str_OfManyUnseenOptionalTypes_FromManyThreads_NeverThrows()
    {
        var arguments = typeof(object).Assembly.GetExportedTypes()
            .Where(t => !t.IsGenericTypeDefinition && !t.IsByRefLike && !t.IsPointer
                        && !(t.IsAbstract && t.IsSealed) && t != typeof(void))
            .Select(t => typeof(Marker<>).MakeGenericType(t))
            .ToArray();
        Assert.True(arguments.Length > 500, $"positive control: only {arguments.Length} fresh type arguments");

        var absent = arguments
            .Select(t => Activator.CreateInstance(typeof(Optional<>).MakeGenericType(t))!)
            .ToArray();

        using var start = new Barrier(Math.Max(4, Environment.ProcessorCount));
        var failures = 0;
        var workers = Enumerable.Range(0, start.ParticipantCount).Select(_ => Task.Factory.StartNew(() =>
        {
            start.SignalAndWait();
            foreach (var optional in absent)
            {
                try
                {
                    if (Builtins.Str(optional) != "None")
                        Interlocked.Increment(ref failures);
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Increment(ref failures);
                }
            }
        }, TaskCreationOptions.LongRunning)).ToArray();
        await Task.WhenAll(workers);

        Assert.Equal(0, failures);
        // The table still answers afterwards (a corrupted table throws on every later lookup).
        Assert.Equal("7", Builtins.Str(Optional<int>.Some(7)));
    }

    private sealed class Marker<T>
    {
    }
}
