using Xunit;

namespace Sharpy.Lsp.Tests.E2E;

public class DrainHelpersTests
{
    [Fact]
    public async Task WaitFirstLoudlyThenDrainAsync_NeverProduces_ThrowsTimeoutOnFirstWait()
    {
        await Assert.ThrowsAsync<TimeoutException>(
            () => DrainHelpers.WaitFirstLoudlyThenDrainAsync(
                () => new TaskCompletionSource<int>().Task,
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(50)));
    }
}
