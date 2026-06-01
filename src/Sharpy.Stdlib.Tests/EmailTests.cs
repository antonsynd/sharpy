using System;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class EmailMessageTests
{
    [Fact]
    public void EmptyMessage_HasNoHeaders()
    {
        var msg = new EmailMessage();
        msg.Keys().Should().HaveCount(0);
        msg.GetContent().Should().Be("");
    }

    [Fact]
    public void SetItem_GetItem_RoundTrip()
    {
        var msg = new EmailMessage();
        msg.SetItem("Subject", "Hello");
        msg.GetItem("Subject").Should().Be("Hello");
    }

    [Fact]
    public void GetItem_CaseInsensitive()
    {
        var msg = new EmailMessage();
        msg.SetItem("Subject", "Hello");
        msg.GetItem("subject").Should().Be("Hello");
        msg.GetItem("SUBJECT").Should().Be("Hello");
    }

    [Fact]
    public void GetItem_Missing_ReturnsNull()
    {
        var msg = new EmailMessage();
        msg.GetItem("Missing").Should().BeNull();
    }

    [Fact]
    public void Contains_ExistingHeader_ReturnsTrue()
    {
        var msg = new EmailMessage();
        msg.SetItem("From", "a@b.com");
        msg.Contains("From").Should().BeTrue();
        msg.Contains("from").Should().BeTrue();
    }

    [Fact]
    public void Contains_MissingHeader_ReturnsFalse()
    {
        var msg = new EmailMessage();
        msg.Contains("Missing").Should().BeFalse();
    }

    [Fact]
    public void SetItem_ReplacesExisting()
    {
        var msg = new EmailMessage();
        msg.SetItem("Subject", "Old");
        msg.SetItem("Subject", "New");
        msg.GetItem("Subject").Should().Be("New");
        msg.Keys().Should().HaveCount(1);
    }

    [Fact]
    public void DelItem_RemovesAllOccurrences()
    {
        var msg = new EmailMessage();
        msg.AddHeader("Received", "from server1");
        msg.AddHeader("Received", "from server2");
        msg.DelItem("Received");
        msg.Contains("Received").Should().BeFalse();
    }

    [Fact]
    public void AddHeader_AllowsDuplicates()
    {
        var msg = new EmailMessage();
        msg.AddHeader("Received", "from server1");
        msg.AddHeader("Received", "from server2");
        var all = msg.GetAll("Received");
        all.Should().NotBeNull();
        all!.Should().HaveCount(2);
    }

    [Fact]
    public void GetAll_NonExistent_ReturnsNull()
    {
        var msg = new EmailMessage();
        msg.GetAll("Missing").Should().BeNull();
    }

    [Fact]
    public void ReplaceHeader_ReplacesFirst()
    {
        var msg = new EmailMessage();
        msg.AddHeader("Subject", "Old");
        msg.ReplaceHeader("Subject", "New");
        msg.GetItem("Subject").Should().Be("New");
    }

    [Fact]
    public void ReplaceHeader_AddsIfMissing()
    {
        var msg = new EmailMessage();
        msg.ReplaceHeader("Subject", "Added");
        msg.GetItem("Subject").Should().Be("Added");
    }

    [Fact]
    public void Keys_ReturnsAllNames()
    {
        var msg = new EmailMessage();
        msg.SetItem("From", "a@b.com");
        msg.SetItem("To", "c@d.com");
        msg.Keys().Should().HaveCount(2);
    }

    [Fact]
    public void Items_ReturnsAllPairs()
    {
        var msg = new EmailMessage();
        msg.SetItem("From", "a@b.com");
        msg.SetItem("To", "c@d.com");
        var items = msg.Items();
        items.Should().HaveCount(2);
    }
}

public class EmailContentTests
{
    [Fact]
    public void SetContent_SetsBody()
    {
        var msg = new EmailMessage();
        msg.SetContent("Hello world");
        msg.GetContent().Should().Be("Hello world");
    }

    [Fact]
    public void SetContent_Html_SetsContentType()
    {
        var msg = new EmailMessage();
        msg.SetContent("<b>HTML</b>", "html");
        msg.GetItem("Content-Type").Should().Contain("text/html");
    }

    [Fact]
    public void IsMultipart_SimplerMessage_ReturnsFalse()
    {
        var msg = new EmailMessage();
        msg.SetContent("plain text");
        msg.IsMultipart().Should().BeFalse();
    }

    [Fact]
    public void AddAttachment_MakesMultipart()
    {
        var msg = new EmailMessage();
        msg.SetContent("body text");
        msg.AddAttachment(new Bytes(new byte[] { 1, 2, 3 }), filename: "test.bin");
        msg.IsMultipart().Should().BeTrue();
    }

    [Fact]
    public void IterAttachments_ReturnsAttachments()
    {
        var msg = new EmailMessage();
        msg.AddAttachment(new Bytes(new byte[] { 1, 2, 3 }), filename: "a.bin");
        msg.AddAttachment(new Bytes(new byte[] { 4, 5 }), filename: "b.bin");
        msg.IterAttachments().Should().HaveCount(2);
    }

    [Fact]
    public void GetPayload_ReturnsBody()
    {
        var msg = new EmailMessage();
        msg.SetContent("payload");
        msg.GetPayload().Should().Be("payload");
    }

    [Fact]
    public void GetPayload_EmptyMessage_ReturnsNull()
    {
        var msg = new EmailMessage();
        msg.GetPayload().Should().BeNull();
    }
}

public class EmailSerializationTests
{
    [Fact]
    public void AsString_SimpleMessage_ProducesRfc5322()
    {
        var msg = new EmailMessage();
        msg.SetItem("Subject", "Test");
        msg.SetItem("From", "sender@example.com");
        msg.SetContent("Hello");
        var result = msg.AsString();
        result.Should().Contain("Subject: Test\r\n");
        result.Should().Contain("From: sender@example.com\r\n");
        result.Should().Contain("Hello");
    }

    [Fact]
    public void AsString_MultipartMessage_HasBoundaries()
    {
        var msg = new EmailMessage();
        msg.SetContent("body");
        msg.AddAttachment(new Bytes(new byte[] { 1, 2, 3 }), filename: "test.bin");
        var result = msg.AsString();
        result.Should().Contain("--");
        result.Should().Contain("Content-Transfer-Encoding: base64");
    }

    [Fact]
    public void AsBytes_ReturnsUtf8()
    {
        var msg = new EmailMessage();
        msg.SetItem("Subject", "Test");
        msg.SetContent("Hello");
        var bytes = msg.AsBytes();
        bytes.Length.Should().BeGreaterThan(0);
    }
}

public class EmailParsingTests
{
    [Fact]
    public void MessageFromString_ParsesHeadersAndBody()
    {
        var msg = EmailModule.MessageFromString("Subject: test\nFrom: a@b.com\n\nBody text");
        msg.GetItem("Subject").Should().Be("test");
        msg.GetItem("From").Should().Be("a@b.com");
        msg.GetContent().Should().Be("Body text");
    }

    [Fact]
    public void MessageFromString_ContinuationLines()
    {
        var msg = EmailModule.MessageFromString("Subject: long\n subject\n\nBody");
        msg.GetItem("Subject").Should().Be("long subject");
    }

    [Fact]
    public void MessageFromString_EmptyString_ReturnsEmpty()
    {
        var msg = EmailModule.MessageFromString("");
        msg.Keys().Should().HaveCount(0);
        msg.GetContent().Should().Be("");
    }

    [Fact]
    public void MessageFromString_HeadersOnly()
    {
        var msg = EmailModule.MessageFromString("Subject: test\nFrom: a@b.com");
        msg.GetItem("Subject").Should().Be("test");
        msg.GetContent().Should().Be("");
    }

    [Fact]
    public void MessageFromString_CrLfLineEndings()
    {
        var msg = EmailModule.MessageFromString("Subject: test\r\nFrom: a@b.com\r\n\r\nBody");
        msg.GetItem("Subject").Should().Be("test");
        msg.GetContent().Should().Be("Body");
    }

    [Fact]
    public void MessageFromBytes_ParsesUtf8()
    {
        var text = "Subject: test\n\nHello";
        var data = new Bytes(System.Text.Encoding.UTF8.GetBytes(text));
        var msg = EmailModule.MessageFromBytes(data);
        msg.GetItem("Subject").Should().Be("test");
        msg.GetContent().Should().Be("Hello");
    }

    [Fact]
    public void Roundtrip_CreateAndParse()
    {
        var original = new EmailMessage();
        original.SetItem("Subject", "Round Trip");
        original.SetItem("From", "sender@example.com");
        original.SetContent("Test body");

        var parsed = EmailModule.MessageFromString(original.AsString());
        parsed.GetItem("Subject").Should().Be("Round Trip");
        parsed.GetItem("From").Should().Be("sender@example.com");
        parsed.GetContent().Should().Contain("Test body");
    }
}

public class EmailErrorTests
{
    [Fact]
    public void MessageError_InheritsFromException()
    {
        new MessageError("test").Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void MessageParseError_InheritsFromMessageError()
    {
        new MessageParseError("test").Should().BeAssignableTo<MessageError>();
    }

    [Fact]
    public void HeaderParseError_InheritsFromMessageError()
    {
        new HeaderParseError("test").Should().BeAssignableTo<MessageError>();
    }
}
