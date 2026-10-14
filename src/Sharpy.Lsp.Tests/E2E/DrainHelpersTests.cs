using FluentAssertions;
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

    [Fact]
    public async Task DrainUntilQuietAsync_CollectsEverythingThatArrives_ThenStopsAtTheQuietWindow()
    {
        var pending = new Queue<int>(new[] { 1, 2, 3 });

        var items = await DrainHelpers.DrainUntilQuietAsync(
            _ => pending.Count > 0 ? Task.FromResult(pending.Dequeue()) : Task.FromException<int>(new TimeoutException()),
            TimeSpan.FromMilliseconds(50));

        items.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task DrainUntilQuietAsync_NothingArrives_ReturnsEmpty()
    {
        var items = await DrainHelpers.DrainUntilQuietAsync(
            _ => Task.FromException<int>(new TimeoutException()),
            TimeSpan.FromMilliseconds(50));

        items.Should().BeEmpty();
    }
}
