using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Equality_Same_Object()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        var setRef = s;
        Object objectRef = s;
        object objRef = s;

        // When/then
        (s == setRef).Should().BeTrue();
        (s == objectRef).Should().BeTrue();
        (s == objRef).Should().BeTrue();
        s.Equals(setRef).Should().BeTrue();
        s.Equals(objectRef).Should().BeTrue();
        s.Equals(objRef).Should().BeTrue();
        s.__Eq__(setRef).Should().BeTrue();
        s.__Eq__(objectRef).Should().BeTrue();
        s.__Eq__(objRef).Should().BeTrue();
    }

    [Fact]
    public void Set_Equality_Same_Object_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        var setRef = s;
        Object objectRef = s;
        object objRef = s;

        // When/then
        (setRef == s).Should().BeTrue();
        (objectRef == s).Should().BeTrue();
        (objRef == s).Should().BeTrue();
        setRef.Equals(s).Should().BeTrue();
        objectRef.Equals(s).Should().BeTrue();
        objRef.Equals(s).Should().BeTrue();
        setRef.__Eq__(s).Should().BeTrue();
        objectRef.__Eq__(s).Should().BeTrue();
    }

    [Fact]
    public void Set_Equality_Null()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When/then
        (s == (Set<int>)null).Should().BeFalse();
        (s == (Object)null).Should().BeFalse();
        (s == (object)null).Should().BeFalse();
        s.Equals((Set<int>)null).Should().BeFalse();
        s.Equals((Object)null).Should().BeFalse();
        s.Equals((object)null).Should().BeFalse();
        s.__Eq__((Set<int>)null).Should().BeFalse();
        s.__Eq__((Object)null).Should().BeFalse();
        s.__Eq__((object)null).Should().BeFalse();
    }

    [Fact]
    public void Set_Equality_Null_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When/then
        ((Set<int>)null == s).Should().BeFalse();
        ((Object)null == s).Should().BeFalse();
        ((object)null == s).Should().BeFalse();
    }

    [Fact]
    public void Set_Inequality_Same_Object()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        var setRef = s;
        Object objectRef = s;
        object objRef = s;

        // When/then
        (s != setRef).Should().BeFalse();
        (s != objectRef).Should().BeFalse();
        (s != objRef).Should().BeFalse();
        s.__Ne__(setRef).Should().BeFalse();
        s.__Ne__(objectRef).Should().BeFalse();
        s.__Ne__(objRef).Should().BeFalse();
    }

    [Fact]
    public void Set_Inequality_Same_Object_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        var setRef = s;
        Object objectRef = s;
        object objRef = s;

        (setRef != s).Should().BeFalse();
        (objectRef != s).Should().BeFalse();
        (objRef != s).Should().BeFalse();
        setRef.__Ne__(s).Should().BeFalse();
        objectRef.__Ne__(s).Should().BeFalse();
    }

    [Fact]
    public void Set_Inequality_Null()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When/then
        (s != (Set<int>)null).Should().BeTrue();
        (s != (Object)null).Should().BeTrue();
        (s != (object)null).Should().BeTrue();
        s.__Ne__((Set<int>)null).Should().BeTrue();
        s.__Ne__((Object)null).Should().BeTrue();
        s.__Ne__((object)null).Should().BeTrue();
    }

    [Fact]
    public void Set_Inequality_Null_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        ((Set<int>)null != s).Should().BeTrue();
        ((Object)null != s).Should().BeTrue();
        ((object)null != s).Should().BeTrue();
    }

    [Fact]
    public void Set_Native_Equality_Different_Object()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<int> m = [1, 3, 5, 7, 9];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (s == m).Should().BeFalse();
        (s == objectRef).Should().BeFalse();
        (s == objRef).Should().BeFalse();
        s.Equals(m).Should().BeFalse();
        s.Equals(objectRef).Should().BeFalse();
        s.Equals(objRef).Should().BeFalse();
        s.__Eq__(m).Should().BeFalse();
        s.__Eq__(objectRef).Should().BeFalse();
        s.__Eq__(objRef).Should().BeFalse();

        // When
        m.Pop();

        // Then
        (s == m).Should().BeTrue();
        (s == objectRef).Should().BeTrue();
        (s == objRef).Should().BeTrue();
        s.Equals(m).Should().BeTrue();
        s.Equals(objectRef).Should().BeTrue();
        s.Equals(objRef).Should().BeTrue();
        s.__Eq__(m).Should().BeTrue();
        s.__Eq__(objectRef).Should().BeTrue();
        s.__Eq__(objRef).Should().BeTrue();
    }

    [Fact]
    public void Set_Native_Equality_Different_Object_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<int> m = [1, 3, 5, 7, 9];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (m == s).Should().BeFalse();
        (objectRef == s).Should().BeFalse();
        (objRef == s).Should().BeFalse();
        m.Equals(s).Should().BeFalse();
        objectRef.Equals(s).Should().BeFalse();
        objRef.Equals(s).Should().BeFalse();
        m.__Eq__(s).Should().BeFalse();
        objectRef.__Eq__(s).Should().BeFalse();

        // When
        m.Pop();

        // Then
        (m == s).Should().BeTrue();
        (objectRef == s).Should().BeTrue();
        (objRef == s).Should().BeTrue();
        m.Equals(s).Should().BeTrue();
        objectRef.Equals(s).Should().BeTrue();
        s.Equals(objRef).Should().BeTrue();
        m.__Eq__(s).Should().BeTrue();
        objectRef.__Eq__(s).Should().BeTrue();
    }

    [Fact]
    public void Set_Inequality_Different_Object()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<int> m = [1, 3, 5, 7, 9];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (s != m).Should().BeTrue();
        (s != objectRef).Should().BeTrue();
        (s != objRef).Should().BeTrue();
        s.__Ne__(m).Should().BeTrue();
        s.__Ne__(objectRef).Should().BeTrue();
        s.__Ne__(objRef).Should().BeTrue();

        // When
        m.Pop();

        // Then
        (s != m).Should().BeFalse();
        (s != objectRef).Should().BeFalse();
        (s != objRef).Should().BeFalse();
        s.__Ne__(m).Should().BeFalse();
        s.__Ne__(objectRef).Should().BeFalse();
        s.__Ne__(objRef).Should().BeFalse();
    }

    [Fact]
    public void Set_Inequality_Different_Object_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<int> m = [1, 3, 5, 7, 9];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (m != s).Should().BeTrue();
        (objectRef != s).Should().BeTrue();
        (objRef != s).Should().BeTrue();
        m.__Ne__(s).Should().BeTrue();
        objectRef.__Ne__(s).Should().BeTrue();

        // When
        m.Pop();

        // Then
        (m != s).Should().BeFalse();
        (objectRef != s).Should().BeFalse();
        (objRef != s).Should().BeFalse();
        m.__Ne__(s).Should().BeFalse();
        objectRef.__Ne__(s).Should().BeFalse();
    }

    [Fact]
    public void Set_Equality_Different_Type()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<double> m = [1.0, 3.0, 5.0, 7.0];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (s == m).Should().BeFalse();
        (s == objectRef).Should().BeFalse();
        (s == objRef).Should().BeFalse();
        s.Equals(m).Should().BeFalse();
        s.Equals(objectRef).Should().BeFalse();
        s.Equals(objRef).Should().BeFalse();
        s.__Eq__(m).Should().BeFalse();
        s.__Eq__(objectRef).Should().BeFalse();
        s.__Eq__(objRef).Should().BeFalse();
    }

    [Fact]
    public void Set_Equality_Different_Type_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<double> m = [1.0, 3.0, 5.0, 7.0];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (m == s).Should().BeFalse();
        (objectRef == s).Should().BeFalse();
        (objRef == s).Should().BeFalse();
        m.Equals(s).Should().BeFalse();
        objectRef.Equals(s).Should().BeFalse();
        objRef.Equals(s).Should().BeFalse();
        m.__Eq__(s).Should().BeFalse();
        objectRef.__Eq__(s).Should().BeFalse();
    }

    [Fact]
    public void Set_Inequality_Different_Type()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<double> m = [1.0, 3.0, 5.0, 7.0];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (s != m).Should().BeTrue();
        (s != objectRef).Should().BeTrue();
        (s != objRef).Should().BeTrue();
        s.__Ne__(m).Should().BeTrue();
        s.__Ne__(objectRef).Should().BeTrue();
        s.__Ne__(objRef).Should().BeTrue();
    }

    [Fact]
    public void Set_Inequality_Different_Type_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<double> m = [1.0, 3.0, 5.0, 7.0];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (m != s).Should().BeTrue();
        (objectRef != s).Should().BeTrue();
        (objRef != s).Should().BeTrue();
        m.__Ne__(s).Should().BeTrue();
        objectRef.__Ne__(s).Should().BeTrue();
    }
}
