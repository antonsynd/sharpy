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
        object objectRef = s;
        object objRef = s;

        // When/then
        (s == setRef).Should().BeTrue();
        (s == objectRef).Should().BeTrue();
        (s == objRef).Should().BeTrue();
        s.Equals(setRef).Should().BeTrue();
        s.Equals(objectRef).Should().BeTrue();
        s.Equals(objRef).Should().BeTrue();
    }

    [Fact]
    public void Set_Equality_Same_Object_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        var setRef = s;
        object objectRef = s;
        object objRef = s;

        // When/then
        (setRef == s).Should().BeTrue();
        (objectRef == s).Should().BeTrue();
        (objRef == s).Should().BeTrue();
        setRef.Equals(s).Should().BeTrue();
        objectRef.Equals(s).Should().BeTrue();
        objRef.Equals(s).Should().BeTrue();
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
        object objectRef = s;
        object objRef = s;

        // When/then
        (s != setRef).Should().BeFalse();
        (s != objectRef).Should().BeFalse();
        (s != objRef).Should().BeFalse();
        (!s.Equals(setRef)).Should().BeFalse();
        (!s.Equals(objectRef)).Should().BeFalse();
        (!s.Equals(objRef)).Should().BeFalse();
    }

    [Fact]
    public void Set_Inequality_Same_Object_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        var setRef = s;
        object objectRef = s;
        object objRef = s;

        (setRef != s).Should().BeFalse();
        (objectRef != s).Should().BeFalse();
        (objRef != s).Should().BeFalse();
        (!setRef.Equals(s)).Should().BeFalse();
        (!objectRef.Equals(s)).Should().BeFalse();
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
        (!s.Equals((Set<int>)null)).Should().BeTrue();
        (!s.Equals((Object)null)).Should().BeTrue();
        (!s.Equals((object)null)).Should().BeTrue();
    }

    [Fact]
    public void Set_Inequality_Null_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        ((Set<int>?)null != s).Should().BeTrue();
        ((object?)null != s).Should().BeTrue();
    }

    [Fact]
    public void Set_Native_Equality_Different_Object()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<int> m = [1, 3, 5, 7, 9];
        object objRef = m;

        // When/then
        (s == m).Should().BeFalse();
        // Note: s == objRef would do reference comparison (different objects = false)
        // Use Equals for value comparison when one operand is object
        s.Equals(m).Should().BeFalse();
        s.Equals(objRef).Should().BeFalse();

        // When
        m.Pop();

        // Then
        (s == m).Should().BeTrue();
        s.Equals(m).Should().BeTrue();
        s.Equals(objRef).Should().BeTrue();
    }

    [Fact]
    public void Set_Native_Equality_Different_Object_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<int> m = [1, 3, 5, 7, 9];
        object objRef = m;

        // When/then
        (m == s).Should().BeFalse();
        // Note: objRef == s would do reference comparison (different objects = false)
        // Use Equals for value comparison when one operand is object
        m.Equals(s).Should().BeFalse();
        objRef.Equals(s).Should().BeFalse();

        // When
        m.Pop();

        // Then
        (m == s).Should().BeTrue();
        m.Equals(s).Should().BeTrue();
        objRef.Equals(s).Should().BeTrue();
        s.Equals(objRef).Should().BeTrue();
    }

    [Fact]
    public void Set_Inequality_Different_Object()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<int> m = [1, 3, 5, 7, 9];
        object objRef = m;

        // When/then
        (s != m).Should().BeTrue();
        // Note: s != objRef would do reference comparison (different objects = true)
        // Use !Equals for value comparison when one operand is object
        (!s.Equals(m)).Should().BeTrue();
        (!s.Equals(objRef)).Should().BeTrue();

        // When
        m.Pop();

        // Then
        (s != m).Should().BeFalse();
        (!s.Equals(m)).Should().BeFalse();
        (!s.Equals(objRef)).Should().BeFalse();
    }

    [Fact]
    public void Set_Inequality_Different_Object_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<int> m = [1, 3, 5, 7, 9];
        object objRef = m;

        // When/then
        (m != s).Should().BeTrue();
        // Note: objRef != s would do reference comparison (different objects = true)
        // Use !Equals for value comparison when one operand is object
        (!m.Equals(s)).Should().BeTrue();
        (!objRef.Equals(s)).Should().BeTrue();

        // When
        m.Pop();

        // Then
        (m != s).Should().BeFalse();
        (!m.Equals(s)).Should().BeFalse();
        (!objRef.Equals(s)).Should().BeFalse();
    }

    [Fact]
    public void Set_Equality_Different_Type()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<double> m = [1.0, 3.0, 5.0, 7.0];
        object objRef = m;

        // When/then
        // Note: Cannot compare Set<int> == Set<double> with operators (different generic types)
        // Must use Equals method for cross-type comparison
        s.Equals(m).Should().BeFalse();
        s.Equals(objRef).Should().BeFalse();
    }

    [Fact]
    public void Set_Equality_Different_Type_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<double> m = [1.0, 3.0, 5.0, 7.0];
        object objRef = m;

        // When/then
        // Note: Cannot compare Set<double> == Set<int> with operators (different generic types)
        // Must use Equals method for cross-type comparison
        m.Equals(s).Should().BeFalse();
        objRef.Equals(s).Should().BeFalse();
    }

    [Fact]
    public void Set_Inequality_Different_Type()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<double> m = [1.0, 3.0, 5.0, 7.0];
        object objRef = m;

        // When/then
        // Note: Cannot compare Set<int> != Set<double> with operators (different generic types)
        // Must use Equals method for cross-type comparison
        (!s.Equals(m)).Should().BeTrue();
        (!s.Equals(objRef)).Should().BeTrue();
    }

    [Fact]
    public void Set_Inequality_Different_Type_Reverse()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        Set<double> m = [1.0, 3.0, 5.0, 7.0];
        object objRef = m;

        // When/then
        // Note: Cannot compare Set<double> != Set<int> with operators (different generic types)
        // Must use Equals method for cross-type comparison
        (!m.Equals(s)).Should().BeTrue();
        (!objRef.Equals(s)).Should().BeTrue();
    }
}
