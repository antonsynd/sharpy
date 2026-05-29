using System.Collections.Generic;
using System.IO;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class ConfigparserTests
{
    [Fact]
    public void ReadString_BasicSection()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nkey1 = value1\nkey2 = value2");

        config.HasSection("section1").Should().BeTrue();
        config.Get("section1", "key1").Should().Be("value1");
        config.Get("section1", "key2").Should().Be("value2");
    }

    [Fact]
    public void ReadString_MultipleSections()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nkey1 = value1\n\n[section2]\nkey2 = value2");

        config.Sections().Should().HaveCount(2);
        config.Get("section1", "key1").Should().Be("value1");
        config.Get("section2", "key2").Should().Be("value2");
    }

    [Fact]
    public void ReadString_DefaultSection()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[DEFAULT]\nbase_dir = /srv\n\n[section1]\npath = %(base_dir)s/data");

        config.Get("section1", "base_dir").Should().Be("/srv");
    }

    [Fact]
    public void ReadString_DefaultFallback()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[DEFAULT]\ncolor = red\n\n[section1]\nsize = large");

        // key from DEFAULT should be accessible in section1
        config.Get("section1", "color").Should().Be("red");
        config.Get("section1", "size").Should().Be("large");
    }

    [Fact]
    public void DictLikeAccess()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nkey1 = value1");

        config["section1"]["key1"].Should().Be("value1");
    }

    [Fact]
    public void DictLikeAccess_Set()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nkey1 = value1");

        config["section1"]["key1"] = "new_value";
        config.Get("section1", "key1").Should().Be("new_value");
    }

    [Fact]
    public void CaseInsensitiveKeys()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nMyKey = myvalue");

        config.Get("section1", "mykey").Should().Be("myvalue");
        config.Get("section1", "MYKEY").Should().Be("myvalue");
        config.Get("section1", "MyKey").Should().Be("myvalue");
    }

    [Fact]
    public void Comments_HashAndSemicolon()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\n# this is a comment\nkey1 = value1\n; another comment\nkey2 = value2");

        config.Get("section1", "key1").Should().Be("value1");
        config.Get("section1", "key2").Should().Be("value2");
        config.Options("section1").Should().HaveCount(2);
    }

    [Fact]
    public void MultilineValues()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nkey1 = line1\n  line2\n  line3");

        config.Get("section1", "key1").Should().Be("line1\nline2\nline3");
    }

    [Fact]
    public void HasSection_ReturnsFalseForDefault()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[DEFAULT]\nkey = val\n\n[section1]\nkey2 = val2");

        config.HasSection("DEFAULT").Should().BeFalse();
        config.HasSection("section1").Should().BeTrue();
    }

    [Fact]
    public void HasOption_ChecksBothSectionAndDefaults()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[DEFAULT]\ndefault_key = val\n\n[section1]\nsection_key = val2");

        config.HasOption("section1", "section_key").Should().BeTrue();
        config.HasOption("section1", "default_key").Should().BeTrue();
        config.HasOption("section1", "nonexistent").Should().BeFalse();
    }

    [Fact]
    public void AddSection_And_Set()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.AddSection("new_section");
        config.Set("new_section", "key", "value");

        config.Get("new_section", "key").Should().Be("value");
    }

    [Fact]
    public void AddSection_DuplicateThrows()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.AddSection("section1");

        var act = () => config.AddSection("section1");
        act.Should().Throw<DuplicateSectionError>();
    }

    [Fact]
    public void Get_NoSectionThrows()
    {
        var config = Sharpy.Configparser.ConfigParser();

        var act = () => config.Get("nonexistent", "key");
        act.Should().Throw<NoSectionError>();
    }

    [Fact]
    public void RemoveOption()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nkey1 = value1\nkey2 = value2");

        config.RemoveOption("section1", "key1").Should().BeTrue();
        config.HasOption("section1", "key1").Should().BeFalse();
        config.HasOption("section1", "key2").Should().BeTrue();
    }

    [Fact]
    public void RemoveSection()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nkey1 = value1\n\n[section2]\nkey2 = value2");

        config.RemoveSection("section1").Should().BeTrue();
        config.HasSection("section1").Should().BeFalse();
        config.HasSection("section2").Should().BeTrue();
    }

    [Fact]
    public void Write_RoundTrip()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.AddSection("section1");
        config.Set("section1", "key1", "value1");
        config.Set("section1", "key2", "value2");

        var writer = new StringWriter();
        config.Write(writer);
        var output = writer.ToString();

        // Parse again
        var config2 = Sharpy.Configparser.ConfigParser();
        config2.ReadString(output);

        config2.Get("section1", "key1").Should().Be("value1");
        config2.Get("section1", "key2").Should().Be("value2");
    }

    [Fact]
    public void Write_WithDefaults()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[DEFAULT]\nbase = /srv\n\n[section1]\nkey1 = value1");

        var writer = new StringWriter();
        config.Write(writer);
        var output = writer.ToString();

        output.Should().Contain("[DEFAULT]");
        output.Should().Contain("base = /srv");
        output.Should().Contain("[section1]");
    }

    [Fact]
    public void BasicInterpolation_PercentSyntax()
    {
        var config = Sharpy.Configparser.ConfigParser(new BasicInterpolation());
        config.ReadString("[section1]\nbase_dir = /srv\npath = %(base_dir)s/data");

        config.Get("section1", "path").Should().Be("/srv/data");
    }

    [Fact]
    public void BasicInterpolation_DefaultFallback()
    {
        var config = Sharpy.Configparser.ConfigParser(new BasicInterpolation());
        config.ReadString("[DEFAULT]\nbase_dir = /srv\n\n[section1]\npath = %(base_dir)s/data");

        config.Get("section1", "path").Should().Be("/srv/data");
    }

    [Fact]
    public void ExtendedInterpolation_DollarSyntax()
    {
        var config = Sharpy.Configparser.ConfigParser(new ExtendedInterpolation());
        config.ReadString("[paths]\nhome = /Users\n\n[section1]\nmy_dir = ${paths:home}/myapp");

        config.Get("section1", "my_dir").Should().Be("/Users/myapp");
    }

    [Fact]
    public void ExtendedInterpolation_SameSection()
    {
        var config = Sharpy.Configparser.ConfigParser(new ExtendedInterpolation());
        config.ReadString("[section1]\nbase = /srv\npath = ${base}/data");

        config.Get("section1", "path").Should().Be("/srv/data");
    }

    [Fact]
    public void GetInt_ParsesInteger()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nport = 8080");

        config.GetInt("section1", "port").Should().Be(8080);
    }

    [Fact]
    public void GetFloat_ParsesDouble()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nrate = 3.14");

        config.GetFloat("section1", "rate").Should().BeApproximately(3.14, 0.001);
    }

    [Fact]
    public void GetBoolean_RecognizesVariants()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\na = yes\nb = no\nc = true\nd = false\ne = 1\nf = 0\ng = on\nh = off");

        config.GetBoolean("section1", "a").Should().BeTrue();
        config.GetBoolean("section1", "b").Should().BeFalse();
        config.GetBoolean("section1", "c").Should().BeTrue();
        config.GetBoolean("section1", "d").Should().BeFalse();
        config.GetBoolean("section1", "e").Should().BeTrue();
        config.GetBoolean("section1", "f").Should().BeFalse();
        config.GetBoolean("section1", "g").Should().BeTrue();
        config.GetBoolean("section1", "h").Should().BeFalse();
    }

    [Fact]
    public void ColonDelimiter_Supported()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nkey1: value1\nkey2: value2");

        config.Get("section1", "key1").Should().Be("value1");
        config.Get("section1", "key2").Should().Be("value2");
    }

    [Fact]
    public void Items_IncludesDefaults()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[DEFAULT]\ncolor = red\n\n[section1]\nsize = large");

        var items = config.Items("section1");
        items.Should().ContainKey("color");
        items.Should().ContainKey("size");
        items["color"].Should().Be("red");
        items["size"].Should().Be("large");
    }

    [Fact]
    public void Defaults_ReturnsDefaultValues()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[DEFAULT]\nkey1 = val1\nkey2 = val2");

        var defaults = config.Defaults();
        defaults.Should().ContainKey("key1");
        defaults.Should().ContainKey("key2");
    }

    [Fact]
    public void InterpolationDepthError_OnCircularReference()
    {
        var config = Sharpy.Configparser.ConfigParser(new BasicInterpolation());
        config.ReadString("[section1]\na = %(b)s\nb = %(a)s");

        var act = () => config.Get("section1", "a");
        act.Should().Throw<InterpolationDepthError>();
    }

    [Fact]
    public void Read_MissingFile_SilentlyIgnored()
    {
        var config = Sharpy.Configparser.ConfigParser();
        // Should not throw
        config.Read("/tmp/nonexistent_config_file_12345.ini");
        config.Sections().Should().BeEmpty();
    }

    [Fact]
    public void SectionProxy_Keys()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nkey1 = val1\nkey2 = val2");

        var keys = config["section1"].Keys();
        keys.Should().Contain("key1");
        keys.Should().Contain("key2");
    }

    [Fact]
    public void SectionProxy_GetWithFallback()
    {
        var config = Sharpy.Configparser.ConfigParser();
        config.ReadString("[section1]\nkey1 = val1");

        config["section1"].Get("key1").Should().Be("val1");
        config["section1"].Get("missing", "default_val").Should().Be("default_val");
    }

    [Fact]
    public void NoSectionError_OnIndexer()
    {
        var config = Sharpy.Configparser.ConfigParser();

        var act = () => { var _ = config["nonexistent"]; };
        act.Should().Throw<NoSectionError>();
    }
}
