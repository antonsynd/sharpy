using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// <c>email.Attachment</c> belongs to the <c>email</c> module (#2016): before it carried
/// <c>[SharpyModuleType("email", "Attachment")]</c>, discovery filed the un-annotated
/// <c>namespace Sharpy</c> type under <c>builtins</c>, so <c>from email import Attachment</c> was
/// SPY0301. The other un-annotated public Stdlib types are #2053. A bare <c>Attachment</c>
/// annotation with no import still resolves — through the CLR fallback, like every annotated
/// Stdlib type (<c>EmailMessage</c>, <c>Fraction</c>) — which is #2064, not this attribute.
/// </summary>
public class EmailAttachmentModuleTypeTests : StdlibIntegrationTestBase
{
    public EmailAttachmentModuleTypeTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void FromEmailImportAttachment_Runs()
    {
        var result = CompileAndExecute(@"from email import EmailMessage, Attachment

def describe(a: Attachment) -> str:
    return a.content_type

def main() -> None:
    m = EmailMessage()
    m.set_content(""hi"")
    m.add_attachment(b""abc"", ""application"", ""octet-stream"", ""x.bin"")
    for a in m.iter_attachments():
        print(describe(a), a.filename, len(a.data))
");

        Assert.True(result.Success,
            $"did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        Assert.Equal("application/octet-stream x.bin 3", result.StandardOutput.TrimEnd('\n', '\r'));
    }
}
