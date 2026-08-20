extern alias SharpyRT;
using System;
using System.IO;
using Sharpy.Compiler.Discovery;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

public class BuiltinExceptionSurfaceTests
{
    // --- IsBuiltinExceptionType ---

    [Fact]
    public void SystemException_IsBuiltin()
    {
        Assert.True(BuiltinExceptionSurface.IsBuiltinExceptionType(typeof(Exception)));
    }

    [Fact]
    public void ValueError_IsBuiltin()
    {
        Assert.True(BuiltinExceptionSurface.IsBuiltinExceptionType(typeof(SharpyRT::Sharpy.ValueError)));
    }

    [Fact]
    public void KeyError_IsBuiltin()
    {
        Assert.True(BuiltinExceptionSurface.IsBuiltinExceptionType(typeof(SharpyRT::Sharpy.KeyError)));
    }

    [Fact]
    public void ExceptionGroup_IsBuiltin()
    {
        Assert.True(BuiltinExceptionSurface.IsBuiltinExceptionType(typeof(SharpyRT::Sharpy.ExceptionGroup)));
    }

    [Fact]
    public void IOException_NotBuiltin()
    {
        Assert.False(BuiltinExceptionSurface.IsBuiltinExceptionType(typeof(IOException)));
    }

    [Fact]
    public void ArgumentException_NotBuiltin()
    {
        Assert.False(BuiltinExceptionSurface.IsBuiltinExceptionType(typeof(ArgumentException)));
    }

    [Fact]
    public void NonException_NotBuiltin()
    {
        Assert.False(BuiltinExceptionSurface.IsBuiltinExceptionType(typeof(string)));
    }

    // --- IsRefusedMember: ValueError (declares nothing — all System.Exception members refused) ---

    [Theory]
    [InlineData("message")]
    [InlineData("Message")]
    [InlineData("stack_trace")]
    [InlineData("StackTrace")]
    [InlineData("inner_exception")]
    [InlineData("InnerException")]
    [InlineData("help_link")]
    [InlineData("HelpLink")]
    [InlineData("source")]
    [InlineData("Source")]
    [InlineData("h_result")]
    [InlineData("HResult")]
    [InlineData("target_site")]
    [InlineData("TargetSite")]
    [InlineData("data")]
    [InlineData("Data")]
    public void ValueError_SystemProperty_IsRefused(string memberName)
    {
        Assert.True(BuiltinExceptionSurface.IsRefusedMember(typeof(SharpyRT::Sharpy.ValueError), memberName));
    }

    [Theory]
    [InlineData("get_base_exception")]
    [InlineData("GetBaseException")]
    [InlineData("get_type")]
    [InlineData("GetType")]
    [InlineData("to_string")]
    [InlineData("ToString")]
    public void ValueError_SystemMethod_IsRefused(string memberName)
    {
        Assert.True(BuiltinExceptionSurface.IsRefusedMember(typeof(SharpyRT::Sharpy.ValueError), memberName));
    }

    [Fact]
    public void ValueError_NoMatchingMember_NotRefused()
    {
        Assert.False(BuiltinExceptionSurface.IsRefusedMember(typeof(SharpyRT::Sharpy.ValueError), "nonexistent_member"));
    }

    // --- IsRefusedMember: ExceptionGroup (declares Message, Exceptions, Subgroup, Split, Derive) ---

    [Theory]
    [InlineData("message")]
    [InlineData("Message")]
    public void ExceptionGroup_Message_IsAllowed(string memberName)
    {
        Assert.False(BuiltinExceptionSurface.IsRefusedMember(typeof(SharpyRT::Sharpy.ExceptionGroup), memberName));
    }

    [Theory]
    [InlineData("exceptions")]
    [InlineData("Exceptions")]
    public void ExceptionGroup_Exceptions_IsAllowed(string memberName)
    {
        Assert.False(BuiltinExceptionSurface.IsRefusedMember(typeof(SharpyRT::Sharpy.ExceptionGroup), memberName));
    }

    [Theory]
    [InlineData("subgroup")]
    [InlineData("Subgroup")]
    [InlineData("split")]
    [InlineData("Split")]
    [InlineData("derive")]
    [InlineData("Derive")]
    public void ExceptionGroup_PythonMethod_IsAllowed(string memberName)
    {
        Assert.False(BuiltinExceptionSurface.IsRefusedMember(typeof(SharpyRT::Sharpy.ExceptionGroup), memberName));
    }

    [Theory]
    [InlineData("to_string")]
    [InlineData("ToString")]
    public void ExceptionGroup_OverriddenToString_IsAllowed(string memberName)
    {
        Assert.False(BuiltinExceptionSurface.IsRefusedMember(typeof(SharpyRT::Sharpy.ExceptionGroup), memberName));
    }

    [Theory]
    [InlineData("stack_trace")]
    [InlineData("StackTrace")]
    [InlineData("inner_exception")]
    [InlineData("InnerException")]
    public void ExceptionGroup_InheritedSystemProperty_IsRefused(string memberName)
    {
        Assert.True(BuiltinExceptionSurface.IsRefusedMember(typeof(SharpyRT::Sharpy.ExceptionGroup), memberName));
    }

    // --- IsRefusedMember: IOException (non-builtin — nothing is refused) ---

    [Theory]
    [InlineData("message")]
    [InlineData("Message")]
    [InlineData("stack_trace")]
    public void IOException_NeverRefused(string memberName)
    {
        Assert.False(BuiltinExceptionSurface.IsRefusedMember(typeof(IOException), memberName));
    }

    // --- IsRefusedMember: System.Exception itself ---

    [Theory]
    [InlineData("message")]
    [InlineData("Message")]
    [InlineData("stack_trace")]
    [InlineData("get_base_exception")]
    public void SystemException_SystemMember_IsRefused(string memberName)
    {
        Assert.True(BuiltinExceptionSurface.IsRefusedMember(typeof(Exception), memberName));
    }

    // --- IsRefusedMember: SystemExit (declares Code) ---

    [Theory]
    [InlineData("code")]
    [InlineData("Code")]
    public void SystemExit_DeclaredProperty_IsAllowed(string memberName)
    {
        Assert.False(BuiltinExceptionSurface.IsRefusedMember(typeof(SharpyRT::Sharpy.SystemExit), memberName));
    }

    // --- RefusalSteer ---

    [Theory]
    [InlineData("message")]
    [InlineData("Message")]
    public void Steer_ForMessage_SuggestsStr(string memberName)
    {
        var steer = BuiltinExceptionSurface.RefusalSteer(memberName);
        Assert.Contains("str(e)", steer);
    }

    [Theory]
    [InlineData("stack_trace")]
    [InlineData("inner_exception")]
    [InlineData("HelpLink")]
    public void Steer_ForOtherMember_GenericMessage(string memberName)
    {
        var steer = BuiltinExceptionSurface.RefusalSteer(memberName);
        Assert.Contains("Python's exception surface", steer);
    }
}
