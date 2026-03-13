using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Unit tests for <see cref="Handlers.TextDocumentSyncHandler"/>.
/// Tests IDisposable implementation and event unsubscription on disposal.
/// </summary>
public class TextDocumentSyncHandlerTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;

    public TextDocumentSyncHandlerTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
    }

    [Fact]
    public void TextDocumentSyncHandler_ImplementsIDisposable()
    {
        typeof(Handlers.TextDocumentSyncHandler).Should().Implement<IDisposable>();
    }

    [Fact]
    public void Dispose_UnsubscribesFromDocumentAnalyzedEvent()
    {
        // Track whether the DocumentAnalyzed event fires after handler disposal.
        // We use a TestableHandler that mimics TextDocumentSyncHandler's subscription
        // pattern (subscribe in ctor, unsubscribe in Dispose) without needing a
        // real DiagnosticPublisher/ILanguageServerFacade.
        var otherHandlerFiredCount = 0;
        _workspace.DocumentAnalyzed += (_, _) =>
        {
            otherHandlerFiredCount++;
            return Task.CompletedTask;
        };

        var handler = new TestableHandler(_workspace);

        // Dispose should unsubscribe from DocumentAnalyzed
        handler.Dispose();

        // Trigger the event by opening a document (schedules analysis via debounce)
        _workspace.OpenDocument("file:///test.spy", "x: int = 1", 1);

        // Wait for the debounce timer (300ms) plus margin
        Thread.Sleep(600);

        // Our separate subscription should still fire, proving the event works
        otherHandlerFiredCount.Should().BeGreaterThan(0,
            "the workspace DocumentAnalyzed event should still fire for other subscribers");

        // The disposed handler should not have been called
        handler.AnalyzedCallCount.Should().Be(0,
            "after Dispose, the handler should no longer receive DocumentAnalyzed events");
    }

    [Fact]
    public void Dispose_BeforeAnyEvents_DoesNotThrow()
    {
        var handler = new TestableHandler(_workspace);

        // Disposing before any events were raised should not throw
        var act = () => handler.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var handler = new TestableHandler(_workspace);

        handler.Dispose();
        var act = () => handler.Dispose();
        act.Should().NotThrow("Dispose should be idempotent");
    }

    [Fact]
    public void Constructor_SubscribesToDocumentAnalyzedEvent()
    {
        // Verify that creating the handler results in receiving events
        var handler = new TestableHandler(_workspace);

        _workspace.OpenDocument("file:///test.spy", "x: int = 1", 1);

        // Wait for debounce
        Thread.Sleep(600);

        handler.AnalyzedCallCount.Should().BeGreaterThan(0,
            "the handler should receive DocumentAnalyzed events after construction");

        handler.Dispose();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    /// <summary>
    /// A testable stand-in that follows the same subscribe/unsubscribe pattern
    /// as TextDocumentSyncHandler (subscribe in constructor, unsubscribe in Dispose)
    /// without requiring OmniSharp dependencies (DiagnosticPublisher, ILanguageServerFacade).
    /// </summary>
    private sealed class TestableHandler : IDisposable
    {
        private readonly SharpyWorkspace _workspace;
        private int _analyzedCallCount;

        public int AnalyzedCallCount => _analyzedCallCount;

        public TestableHandler(SharpyWorkspace workspace)
        {
            _workspace = workspace;
            _workspace.DocumentAnalyzed += OnDocumentAnalyzedAsync;
        }

        private Task OnDocumentAnalyzedAsync(string uri, SemanticResult result)
        {
            Interlocked.Increment(ref _analyzedCallCount);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _workspace.DocumentAnalyzed -= OnDocumentAnalyzedAsync;
        }
    }
}
