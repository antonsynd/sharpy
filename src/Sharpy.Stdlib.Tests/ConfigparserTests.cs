using System.IO;
using FluentAssertions;
using Sharpy;
using Xunit;

namespace Sharpy.Stdlib.Tests;

public class ConfigparserTests
{
    [Fact]
    public void ReadString_BasicKeyValue()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nkey = value");
        config.Get("section", "key").Should().Be("value");
    }

    [Fact]
    public void ReadString_ColonDelimiter()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nkey : value");
        config.Get("section", "key").Should().Be("value");
    }

    [Fact]
    public void ReadString_NoSpaces()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nkey=value");
        config.Get("section", "key").Should().Be("value");
    }

    [Fact]
    public void ReadString_MultilineValue()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nkey = line1\n  line2\n  line3");
        config.Get("section", "key").Should().Be("line1\nline2\nline3");
    }

    [Fact]
    public void ReadString_HashComments()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\n# comment\nkey = value");
        config.Get("section", "key").Should().Be("value");
        config.Options("section").Should().HaveCount(1);
    }

    [Fact]
    public void ReadString_SemicolonComments()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\n; comment\nkey = value");
        config.Get("section", "key").Should().Be("value");
    }

    [Fact]
    public void ReadString_MultipleSections()
    {
        var config = new ConfigParser();
        config.ReadString("[section1]\nkey1 = val1\n\n[section2]\nkey2 = val2");
        config.Sections().Should().HaveCount(2);
        config.Get("section1", "key1").Should().Be("val1");
        config.Get("section2", "key2").Should().Be("val2");
    }

    [Fact]
    public void ReadString_EmptyValue()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nkey =");
        config.Get("section", "key").Should().Be("");
    }

    [Fact]
    public void ReadString_WhitespaceInSectionName()
    {
        var config = new ConfigParser();
        config.ReadString("[ section ]\nkey = value");
        config.Get(" section ", "key").Should().Be("value");
    }

    [Fact]
    public void Default_Fallback()
    {
        var config = new ConfigParser();
        config.ReadString("[DEFAULT]\nfallback = yes\n\n[section]\nkey = value");
        config.Get("section", "fallback").Should().Be("yes");
        config.Get("section", "key").Should().Be("value");
    }

    [Fact]
    public void Default_OverriddenBySection()
    {
        var config = new ConfigParser();
        config.ReadString("[DEFAULT]\nkey = default\n\n[section]\nkey = override");
        config.Get("section", "key").Should().Be("override");
    }

    [Fact]
    public void Defaults_ReturnsDefaultValues()
    {
        var config = new ConfigParser();
        config.ReadString("[DEFAULT]\nkey1 = val1\nkey2 = val2");
        var defaults = config.Defaults();
        defaults.Should().ContainKey("key1");
        defaults.Should().ContainKey("key2");
    }

    [Fact]
    public void CaseInsensitiveKeys()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nMyKey = myvalue");
        config.Get("section", "mykey").Should().Be("myvalue");
        config.Get("section", "MYKEY").Should().Be("myvalue");
    }

    [Fact]
    public void CaseSensitiveSections()
    {
        var config = new ConfigParser();
        config.ReadString("[Section]\nkey = val");
        config.HasSection("Section").Should().BeTrue();
        config.HasSection("section").Should().BeFalse();
    }

    [Fact]
    public void HasSection_ReturnsFalseForDefault()
    {
        var config = new ConfigParser();
        config.ReadString("[DEFAULT]\nkey = val\n\n[section]\nkey2 = val2");
        config.HasSection("DEFAULT").Should().BeFalse();
        config.HasSection("section").Should().BeTrue();
    }

    [Fact]
    public void HasOption_ChecksSectionAndDefault()
    {
        var config = new ConfigParser();
        config.ReadString("[DEFAULT]\ndefault_key = val\n\n[section]\nsection_key = val2");
        config.HasOption("section", "section_key").Should().BeTrue();
        config.HasOption("section", "default_key").Should().BeTrue();
        config.HasOption("section", "nonexistent").Should().BeFalse();
    }

    [Fact]
    public void AddSection_And_Set()
    {
        var config = new ConfigParser();
        config.AddSection("new_section");
        config.Set("new_section", "key", "value");
        config.Get("new_section", "key").Should().Be("value");
    }

    [Fact]
    public void AddSection_DuplicateThrows()
    {
        var config = new ConfigParser();
        config.AddSection("section");
        var act = () => config.AddSection("section");
        act.Should().Throw<DuplicateSectionError>();
    }

    [Fact]
    public void AddSection_DefaultThrows()
    {
        var config = new ConfigParser();
        var act = () => config.AddSection("DEFAULT");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Get_NoSectionThrows()
    {
        var config = new ConfigParser();
        var act = () => config.Get("nonexistent", "key");
        act.Should().Throw<NoSectionError>();
    }

    [Fact]
    public void Get_NoOptionThrows()
    {
        var config = new ConfigParser();
        config.AddSection("section");
        var act = () => config.Get("section", "missing");
        act.Should().Throw<NoOptionError>();
    }

    [Fact]
    public void Get_FallbackReturned()
    {
        var config = new ConfigParser();
        config.AddSection("section");
        config.Get("section", "missing", fallback: "default").Should().Be("default");
    }

    [Fact]
    public void Set_NoSectionThrows()
    {
        var config = new ConfigParser();
        var act = () => config.Set("nonexistent", "key", "value");
        act.Should().Throw<NoSectionError>();
    }

    [Fact]
    public void RemoveOption()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nkey1 = value1\nkey2 = value2");
        config.RemoveOption("section", "key1").Should().BeTrue();
        config.HasOption("section", "key1").Should().BeFalse();
        config.HasOption("section", "key2").Should().BeTrue();
    }

    [Fact]
    public void RemoveSection()
    {
        var config = new ConfigParser();
        config.ReadString("[section1]\nkey = val\n\n[section2]\nkey = val");
        config.RemoveSection("section1").Should().BeTrue();
        config.HasSection("section1").Should().BeFalse();
        config.HasSection("section2").Should().BeTrue();
    }

    [Fact]
    public void Options_IncludesDefaults()
    {
        var config = new ConfigParser();
        config.ReadString("[DEFAULT]\nd = 1\n\n[section]\ns = 2");
        var options = config.Options("section");
        options.Should().Contain("d");
        options.Should().Contain("s");
    }

    [Fact]
    public void Items_IncludesDefaults()
    {
        var config = new ConfigParser();
        config.ReadString("[DEFAULT]\ncolor = red\n\n[section]\nsize = large");
        var items = config.Items("section");
        items.Should().ContainKey("color");
        items.Should().ContainKey("size");
        items["color"].Should().Be("red");
        items["size"].Should().Be("large");
    }

    [Fact]
    public void MissingSectionHeaderError_BeforeAnySection()
    {
        var config = new ConfigParser();
        var act = () => config.ReadString("key = value");
        act.Should().Throw<MissingSectionHeaderError>();
    }

    [Fact]
    public void DictLikeAccess()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nkey = value");
        config["section"]["key"].Should().Be("value");
    }

    [Fact]
    public void DictLikeAccess_Set()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nkey = value");
        config["section"]["key"] = "new_value";
        config.Get("section", "key").Should().Be("new_value");
    }

    [Fact]
    public void DictLikeAccess_NoSectionThrows()
    {
        var config = new ConfigParser();
        var act = () => { var _ = config["nonexistent"]; };
        act.Should().Throw<NoSectionError>();
    }

    [Fact]
    public void SectionProxy_Keys()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nkey1 = val1\nkey2 = val2");
        var keys = config["section"].Keys();
        keys.Should().Contain("key1");
        keys.Should().Contain("key2");
    }

    [Fact]
    public void SectionProxy_GetWithFallback()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nkey1 = val1");
        config["section"].Get("key1").Should().Be("val1");
        config["section"].Get("missing", "default_val").Should().Be("default_val");
    }

    // Interpolation tests

    [Fact]
    public void BasicInterpolation_PercentSyntax()
    {
        var config = new ConfigParser(new BasicInterpolation());
        config.ReadString("[section]\nbase_dir = /srv\npath = %(base_dir)s/data");
        config.Get("section", "path").Should().Be("/srv/data");
    }

    [Fact]
    public void BasicInterpolation_FromDefault()
    {
        var config = new ConfigParser(new BasicInterpolation());
        config.ReadString("[DEFAULT]\nroot = /\n\n[section]\npath = %(root)setc");
        config.Get("section", "path").Should().Be("/etc");
    }

    [Fact]
    public void BasicInterpolation_Recursive()
    {
        var config = new ConfigParser(new BasicInterpolation());
        config.ReadString("[section]\na = 1\nb = %(a)s2\nc = %(b)s3");
        config.Get("section", "c").Should().Be("123");
    }

    [Fact]
    public void BasicInterpolation_CircularThrows()
    {
        var config = new ConfigParser(new BasicInterpolation());
        config.ReadString("[section]\na = %(b)s\nb = %(a)s");
        var act = () => config.Get("section", "a");
        act.Should().Throw<InterpolationError>();
    }

    [Fact]
    public void ExtendedInterpolation_CrossSection()
    {
        var config = new ConfigParser(new ExtendedInterpolation());
        config.ReadString("[paths]\nhome = /Users\n\n[section]\nmy_dir = ${paths:home}/myapp");
        config.Get("section", "my_dir").Should().Be("/Users/myapp");
    }

    [Fact]
    public void ExtendedInterpolation_SameSection()
    {
        var config = new ConfigParser(new ExtendedInterpolation());
        config.ReadString("[section]\nbase = /srv\npath = ${base}/data");
        config.Get("section", "path").Should().Be("/srv/data");
    }

    [Fact]
    public void RawGet_SkipsInterpolation()
    {
        var config = new ConfigParser(new BasicInterpolation());
        config.ReadString("[section]\nbase = /srv\npath = %(base)s/data");
        config.Get("section", "path", raw: true).Should().Be("%(base)s/data");
    }

    // Typed getter tests

    [Fact]
    public void GetInt_ParsesInteger()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nport = 8080");
        config.GetInt("section", "port").Should().Be(8080);
    }

    [Fact]
    public void GetInt_InvalidThrows()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nval = notint");
        var act = () => config.GetInt("section", "val");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void GetFloat_ParsesDouble()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nrate = 3.14");
        config.GetFloat("section", "rate").Should().BeApproximately(3.14, 0.001);
    }

    [Fact]
    public void GetBoolean_AllVariants()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\na = yes\nb = no\nc = true\nd = false\ne = 1\nf = 0\ng = on\nh = off");
        config.GetBoolean("section", "a").Should().BeTrue();
        config.GetBoolean("section", "b").Should().BeFalse();
        config.GetBoolean("section", "c").Should().BeTrue();
        config.GetBoolean("section", "d").Should().BeFalse();
        config.GetBoolean("section", "e").Should().BeTrue();
        config.GetBoolean("section", "f").Should().BeFalse();
        config.GetBoolean("section", "g").Should().BeTrue();
        config.GetBoolean("section", "h").Should().BeFalse();
    }

    [Fact]
    public void GetBoolean_InvalidThrows()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nval = maybe");
        var act = () => config.GetBoolean("section", "val");
        act.Should().Throw<ValueError>();
    }

    // Write tests

    [Fact]
    public void Write_RoundTrip()
    {
        var config = new ConfigParser();
        config.AddSection("section");
        config.Set("section", "key1", "value1");
        config.Set("section", "key2", "value2");

        var writer = new StringWriter();
        config.Write(writer);

        var config2 = new ConfigParser();
        config2.ReadString(writer.ToString());
        config2.Get("section", "key1").Should().Be("value1");
        config2.Get("section", "key2").Should().Be("value2");
    }

    [Fact]
    public void Write_DefaultSection()
    {
        var config = new ConfigParser();
        config.ReadString("[DEFAULT]\nbase = /srv\n\n[section]\nkey = val");

        var writer = new StringWriter();
        config.Write(writer);
        var output = writer.ToString();

        output.Should().Contain("[DEFAULT]");
        output.Should().Contain("base = /srv");
        output.Should().Contain("[section]");
    }

    [Fact]
    public void Write_NoSpaceAroundDelimiters()
    {
        var config = new ConfigParser();
        config.AddSection("section");
        config.Set("section", "key", "value");

        var writer = new StringWriter();
        config.Write(writer, spaceAroundDelimiters: false);
        writer.ToString().Should().Contain("key=value");
    }

    [Fact]
    public void Read_MissingFile_SilentlyIgnored()
    {
        var config = new ConfigParser();
        config.Read("/tmp/nonexistent_config_file_12345.ini");
        config.Sections().Should().BeEmpty();
    }

    // Edge cases

    [Fact]
    public void EmptyIniFile()
    {
        var config = new ConfigParser();
        config.ReadString("");
        config.Sections().Should().BeEmpty();
    }

    [Fact]
    public void SectionWithNoKeys()
    {
        var config = new ConfigParser();
        config.ReadString("[section]");
        config.HasSection("section").Should().BeTrue();
        config.Options("section").Should().BeEmpty();
    }

    [Fact]
    public void ValueContainsDelimiter()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nkey = value = with = equals");
        config.Get("section", "key").Should().Be("value = with = equals");
    }

    [Fact]
    public void InlineCommentsDisabledByDefault()
    {
        var config = new ConfigParser();
        config.ReadString("[section]\nkey = foo # bar");
        config.Get("section", "key").Should().Be("foo # bar");
    }
}
