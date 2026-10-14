namespace Sharpy.Lsp.Tests.E2E;

internal static class DrainHelpers
{
    internal static async Task<List<T>> WaitFirstLoudlyThenDrainAsync<T>(
        Func<Task<T>> waitForOne,
        TimeSpan firstTimeout,
        TimeSpan drainTimeout)
    {
        var items = new List<T>();
        items.Add(await waitForOne().WaitAsync(firstTimeout));

        while (true)
        {
            try
            {
                items.Add(await waitForOne().WaitAsync(drainTimeout));
            }
            catch (TimeoutException)
            {
                break;
            }
        }

        return items;
    }

    /// <summary>
    /// Collects items until none arrives for <paramref name=quiet/> — for a route after which
    /// NOTHING may be the right answer, so there is no first item to wait for loudly. The quiet
    /// window is handed to <paramref name=waitForOne/>, which must throw
    /// <see cref="TimeoutException"/> when it expires: abandoning a still-pending wait would let
    /// it consume an item nobody records.
    /// </summary>
    internal static async Task<List<T>> DrainUntilQuietAsync<T>(Func<TimeSpan, Task<T>> waitForOne, TimeSpan quiet)
    {
        var items = new List<T>();
        while (true)
        {
            try
            {
                items.Add(await waitForOne(quiet));
            }
            catch (TimeoutException)
            {
                return items;
            }
        }
    }
}
