using System;
using System.IO;
using Xunit;

namespace Sharpy.Core.Tests
{
    // CapturedOutput swaps the process-global Console.Out, so these tests must not
    // run concurrently with anything else that touches the console. A collection
    // definition with DisableParallelization runs in isolation from all other tests.
    [CollectionDefinition("ConsoleCapture", DisableParallelization = true)]
    public sealed class ConsoleCaptureCollection
    {
    }

    [Collection("ConsoleCapture")]
    public class UnittestCapturedOutputTests
    {
        [Fact]
        public void Constructor_RedirectsConsoleOut_DisposeRestores()
        {
            TextWriter original = Console.Out;

            var capture = new CapturedOutput();
            try
            {
                Assert.NotSame(original, Console.Out);
            }
            finally
            {
                capture.Dispose();
            }

            Assert.Same(original, Console.Out);
        }

        [Fact]
        public void Getvalue_ReturnsAccumulatedText()
        {
            TextWriter original = Console.Out;

            using (var capture = new CapturedOutput())
            {
                Console.Write("hello");
                Console.Write(" ");
                Console.Write("world");

                Assert.Equal("hello world", capture.Getvalue());
            }

            Assert.Same(original, Console.Out);
        }

        [Fact]
        public void Getvalue_CapturesWriteLineNewlines()
        {
            using (var capture = new CapturedOutput())
            {
                Console.WriteLine("line1");
                Console.WriteLine("line2");

                Assert.Equal("line1" + Environment.NewLine + "line2" + Environment.NewLine, capture.Getvalue());
            }
        }

        [Fact]
        public void Dispose_RestoresConsoleOut_EvenWhenBodyThrows()
        {
            TextWriter original = Console.Out;

            Action act = () =>
            {
                using (var capture = new CapturedOutput())
                {
                    Console.Write("partial");
                    throw new InvalidOperationException("boom");
                }
            };

            Assert.Throws<InvalidOperationException>(act);

            // The using statement's implicit Dispose must have restored Console.Out.
            Assert.Same(original, Console.Out);
        }

        [Fact]
        public void NestedCaptures_RestoreInLifoOrder()
        {
            TextWriter original = Console.Out;

            using (var outer = new CapturedOutput())
            {
                TextWriter outerWriter = Console.Out;
                Console.Write("outer-before;");

                using (var inner = new CapturedOutput())
                {
                    Assert.NotSame(outerWriter, Console.Out);
                    Console.Write("inner-only");
                    Assert.Equal("inner-only", inner.Getvalue());
                }

                // Disposing the inner capture restores the outer writer (LIFO).
                Assert.Same(outerWriter, Console.Out);
                Console.Write("outer-after");

                // The inner-only text never reached the outer buffer.
                Assert.Equal("outer-before;outer-after", outer.Getvalue());
            }

            Assert.Same(original, Console.Out);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            TextWriter original = Console.Out;

            var capture = new CapturedOutput();
            capture.Dispose();
            TextWriter afterFirst = Console.Out;

            // A second dispose must be a no-op and must not clobber the now-current writer.
            capture.Dispose();

            Assert.Same(original, afterFirst);
            Assert.Same(original, Console.Out);
        }

        [Fact]
        public void DoubleDispose_DoesNotRestoreOverANewerCapture()
        {
            TextWriter original = Console.Out;

            var first = new CapturedOutput();
            first.Dispose();

            using (var second = new CapturedOutput())
            {
                TextWriter secondWriter = Console.Out;

                // Re-disposing the already-disposed first capture must NOT restore
                // 'original' over the active second capture's writer.
                first.Dispose();

                Assert.Same(secondWriter, Console.Out);
                Console.Write("still-captured");
                Assert.Equal("still-captured", second.Getvalue());
            }

            Assert.Same(original, Console.Out);
        }

        [Fact]
        public void Factory_CapturedOutput_ReturnsUsableCapture()
        {
            TextWriter original = Console.Out;

            using (CapturedOutput capture = Sharpy.Unittest.CapturedOutput())
            {
                Console.Write("from-factory");
                Assert.Equal("from-factory", capture.Getvalue());
            }

            Assert.Same(original, Console.Out);
        }
    }
}
