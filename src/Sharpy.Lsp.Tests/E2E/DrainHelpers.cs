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
}
