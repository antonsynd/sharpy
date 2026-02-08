using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Discovery;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

public class ReverseNameManglerTests
{
    [Theory]
    [InlineData("GetUserName", "get_user_name")]
    [InlineData("XMLParser", "xml_parser")]
    [InlineData("HTTPSConnection", "https_connection")]
    [InlineData("Base64Encoder", "base64_encoder")]
    [InlineData("Int32Converter", "int32_converter")]
    [InlineData("SHA256Managed", "sha256_managed")]
    [InlineData("Utf8JsonReader", "utf8_json_reader")]
    [InlineData("X509Certificate", "x509_certificate")]
    [InlineData("Win32Error", "win32_error")]
    [InlineData("Log2Ceil", "log2_ceil")]
    [InlineData("H264Decoder", "h264_decoder")]
    [InlineData("Dx11Renderer", "dx11_renderer")]
    [InlineData("H2OParser", "h2o_parser")]
    [InlineData("CO2Level", "co2_level")]
    [InlineData("Vector3D", "vector3d")]
    [InlineData("Color4F", "color4f")]
    [InlineData("CRC32C", "crc32c")]
    [InlineData("Matrix4x4", "matrix4x4")]
    [InlineData("_4E", "_4e")]
    [InlineData("Base64", "base64")]
    [InlineData("Int32", "int32")]
    [InlineData("SHA256", "sha256")]
    [InlineData("ToString", "to_string")]
    [InlineData("ReadAllText", "read_all_text")]
    public void ToSnakeCase_ConvertsCorrectly(string input, string expected)
    {
        var result = ReverseNameMangler.ToSnakeCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Base64Encoder", "BASE64_ENCODER")]
    [InlineData("XMLParser", "XML_PARSER")]
    [InlineData("SHA256Managed", "SHA256_MANAGED")]
    public void ToScreamingSnakeCase_ConvertsCorrectly(string input, string expected)
    {
        var result = ReverseNameMangler.ToScreamingSnakeCase(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToSharpyName_EnumMember_ReturnsScreamingSnakeCase()
    {
        Assert.Equal("DARK_BLUE", ReverseNameMangler.ToSharpyName("DarkBlue", ReverseNameContext.EnumMember));
    }

    [Fact]
    public void ToSharpyName_Constant_ReturnsScreamingSnakeCase()
    {
        Assert.Equal("MAX_RETRY_COUNT", ReverseNameMangler.ToSharpyName("MaxRetryCount", ReverseNameContext.Constant));
    }

    [Fact]
    public void ToSharpyName_Method_ReturnsSnakeCase()
    {
        Assert.Equal("get_user_name", ReverseNameMangler.ToSharpyName("GetUserName", ReverseNameContext.Method));
    }

    [Fact]
    public void ToSharpyName_Type_PreservesName()
    {
        Assert.Equal("StringBuilder", ReverseNameMangler.ToSharpyName("StringBuilder", ReverseNameContext.Type));
    }

    [Fact]
    public void ToSharpyName_Interface_PreservesName()
    {
        Assert.Equal("IComparable", ReverseNameMangler.ToSharpyName("IComparable", ReverseNameContext.Interface));
    }

    [Theory]
    [InlineData("get_user_name")]
    [InlineData("max_size")]
    [InlineData("xml_parser")]
    [InlineData("read_all_text")]
    [InlineData("sha256_managed")]
    [InlineData("base64_encoder")]
    [InlineData("simple")]
    [InlineData("a")]
    [InlineData("is_valid")]
    public void RoundTrip_SnakeCase_IsIdentity(string input)
    {
        var forward = NameMangler.ToPascalCase(input);
        var reverse = ReverseNameMangler.ToSnakeCase(forward);
        Assert.Equal(input, reverse);
    }

    /// <summary>
    /// Documents known cases where the round-trip is NOT identity.
    /// These inputs are not snake_case, so ToPascalCase passes them through
    /// (or transforms differently), and the reverse mangling produces a
    /// different result than the original input.
    /// </summary>
    [Theory]
    [InlineData("httpClient", "http_client")]   // camelCase passes through ToPascalCase, then splits
    [InlineData("HTTP", "http")]                // all-caps passes through, lowercased without splits
    [InlineData("XMLParser", "xml_parser")]     // PascalCase with acronym passes through, acronym gets split
    [InlineData("x_y_z", "xyz")]                // single-letter segments fuse into acronym XYZ, can't be re-split
    public void RoundTrip_NonSnakeCase_MayNotBeIdentity(string input, string expectedOutput)
    {
        var forward = NameMangler.ToPascalCase(input);
        var reverse = ReverseNameMangler.ToSnakeCase(forward);
        Assert.Equal(expectedOutput, reverse);
    }
}
