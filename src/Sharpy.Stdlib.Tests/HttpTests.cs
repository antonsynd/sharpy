using System;
using System.Net;
using System.Net.Http;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class HttpStatusTests
{
    [Fact]
    public void OK_HasCorrectValue()
    {
        HTTPStatus.OK.Value.Should().Be(200);
    }

    [Fact]
    public void OK_HasCorrectPhrase()
    {
        HTTPStatus.OK.Phrase.Should().Be("OK");
    }

    [Fact]
    public void NOT_FOUND_HasCorrectValue()
    {
        HTTPStatus.NOT_FOUND.Value.Should().Be(404);
    }

    [Fact]
    public void NOT_FOUND_HasCorrectPhrase()
    {
        HTTPStatus.NOT_FOUND.Phrase.Should().Be("Not Found");
    }

    [Fact]
    public void FromValue_ReturnsCorrectInstance()
    {
        HTTPStatus.FromValue(200).Should().BeSameAs(HTTPStatus.OK);
    }

    [Fact]
    public void FromValue_UnknownCode_ThrowsValueError()
    {
        Action act = () => HTTPStatus.FromValue(999);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void ImplicitIntConversion_ReturnsValue()
    {
        int code = HTTPStatus.OK;
        code.Should().Be(200);
    }

    [Fact]
    public void ToString_ReturnsNumericString()
    {
        HTTPStatus.OK.ToString().Should().Be("200");
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        HTTPStatus.OK.Equals(HTTPStatus.FromValue(200)).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameValue_Matches()
    {
        HTTPStatus.OK.GetHashCode().Should().Be(HTTPStatus.FromValue(200).GetHashCode());
    }

    [Fact]
    public void AllStatusCodes_HaveNonNullPhrase()
    {
        var fields = typeof(HTTPStatus).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        int count = 0;
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(HTTPStatus))
            {
                var status = (HTTPStatus)field.GetValue(null)!;
                status.Phrase.Should().NotBeNull();
                status.Name.Should().NotBeNullOrEmpty();
                count++;
            }
        }
        count.Should().Be(62);
    }

    [Theory]
    [InlineData(100, "Continue")]
    [InlineData(201, "Created")]
    [InlineData(301, "Moved Permanently")]
    [InlineData(403, "Forbidden")]
    [InlineData(500, "Internal Server Error")]
    [InlineData(503, "Service Unavailable")]
    public void FromValue_KnownCodes_HaveCorrectPhrase(int code, string expectedPhrase)
    {
        HTTPStatus.FromValue(code).Phrase.Should().Be(expectedPhrase);
    }
}

public class HttpConnectionTests
{
    [Fact]
    public void HTTPConnection_DefaultPort_Is80()
    {
        using var conn = new HTTPConnection("example.com");
        conn.Port.Should().Be(80);
        conn.Host.Should().Be("example.com");
    }

    [Fact]
    public void HTTPConnection_CustomPort()
    {
        using var conn = new HTTPConnection("example.com", 8080);
        conn.Port.Should().Be(8080);
    }

    [Fact]
    public void HTTPSConnection_DefaultPort_Is443()
    {
        using var conn = new HTTPSConnection("example.com");
        conn.Port.Should().Be(443);
    }

    [Fact]
    public void HTTPConnection_EmptyHost_ThrowsInvalidURL()
    {
        Action act = () => new HTTPConnection("");
        act.Should().Throw<InvalidURL>();
    }

    [Fact]
    public void Getresponse_BeforeRequest_ThrowsNotConnected()
    {
        using var conn = new HTTPConnection("example.com");
        Action act = () => conn.Getresponse();
        act.Should().Throw<NotConnected>();
    }
}

public class HttpResponseTests
{
    [Fact]
    public void Status_ReturnsStatusCode()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponse.Content = new StringContent("body");
        using var response = new HTTPResponse(httpResponse);
        response.Status.Should().Be(200);
    }

    [Fact]
    public void Reason_ReturnsReasonPhrase()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponse.ReasonPhrase = "OK";
        httpResponse.Content = new StringContent("body");
        using var response = new HTTPResponse(httpResponse);
        response.Reason.Should().Be("OK");
    }

    [Fact]
    public void Read_ReturnsBodyAsBytes()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponse.Content = new StringContent("hello");
        using var response = new HTTPResponse(httpResponse);
        var body = response.Read();
        System.Text.Encoding.UTF8.GetString(body.ToArray()).Should().Contain("hello");
    }

    [Fact]
    public void Read_CalledTwice_ReturnsSameData()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponse.Content = new StringContent("cached");
        using var response = new HTTPResponse(httpResponse);
        var first = response.Read();
        var second = response.Read();
        first.Equals(second).Should().BeTrue();
    }

    [Fact]
    public void Getheader_ExistingHeader_ReturnsValue()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponse.Content = new StringContent("body");
        httpResponse.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        using var response = new HTTPResponse(httpResponse);
        response.Getheader("Content-Type").Should().Contain("text/plain");
    }

    [Fact]
    public void Getheader_MissingHeader_ReturnsDefault()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponse.Content = new StringContent("body");
        using var response = new HTTPResponse(httpResponse);
        response.Getheader("X-Missing", "fallback").Should().Be("fallback");
    }

    [Fact]
    public void Getheader_MissingHeader_DefaultsToNull()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponse.Content = new StringContent("body");
        using var response = new HTTPResponse(httpResponse);
        response.Getheader("X-Missing").Should().BeNull();
    }

    [Fact]
    public void Getheaders_ReturnsAllHeaders()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponse.Headers.Add("X-Custom", "val");
        httpResponse.Content = new StringContent("body");
        using var response = new HTTPResponse(httpResponse);
        var headers = response.Getheaders();
        headers.Should().NotBeEmpty();
    }
}

public class HttpExceptionTests
{
    [Fact]
    public void HTTPException_InheritsFromException()
    {
        var ex = new HTTPException("test");
        ex.Should().BeAssignableTo<Exception>();
        ex.Message.Should().Be("test");
    }

    [Fact]
    public void InvalidURL_InheritsFromHTTPException()
    {
        var ex = new InvalidURL("bad url");
        ex.Should().BeAssignableTo<HTTPException>();
    }

    [Fact]
    public void NotConnected_InheritsFromHTTPException()
    {
        var ex = new NotConnected("no conn");
        ex.Should().BeAssignableTo<HTTPException>();
    }
}

public class HttpConstantsTests
{
    [Fact]
    public void HTTP_PORT_Is80()
    {
        HttpModule.HTTP_PORT.Should().Be(80);
    }

    [Fact]
    public void HTTPS_PORT_Is443()
    {
        HttpModule.HTTPS_PORT.Should().Be(443);
    }
}
