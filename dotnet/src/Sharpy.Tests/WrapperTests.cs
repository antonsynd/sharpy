using Xunit;
using FluentAssertions;

namespace Sharpy.Tests
{
    public class Wrapper_Tests
    {
        [Fact]
        public void Wrapper_Constructor()
        {
            // If
            Wrapper<int>.ResetId();
            var wrapper = new Wrapper<int>(1);

            // When/then
            wrapper.Id.Should().Be(0);
            wrapper.Value.Should().Be(1);
        }

        [Fact]
        public void Wrapper_Bool_Convertible()
        {
            // If/when/then
            typeof(Wrapper<int>).IsAssignableTo(typeof(BoolConvertible)).Should().BeTrue();

            // If
            var wrapper = new Wrapper<int>(1);
            // When/then
            wrapper.__Bool__().Should().BeTrue();

            // If
            wrapper = new Wrapper<int>(0);
            // When/then
            wrapper.__Bool__().Should().BeFalse();
        }

        [Fact]
        public void Wrapper_Implicit_True_False()
        {
            // If/when/then
            (new Wrapper<int>(0) ? true : false).Should().BeFalse();
            (new Wrapper<int>(1) ? true : false).Should().BeTrue();
        }

        [Fact]
        public void Wrapper_Hashable()
        {
            // If
            typeof(Wrapper<int>).IsAssignableTo(typeof(Hashable)).Should().BeTrue();
            var wrapper0 = new Wrapper<int>(0);
            var wrapper1 = new Wrapper<int>(1);

            // When/then
            wrapper0.__Hash__().Should().NotBe(wrapper1.__Hash__());
            wrapper0.__Hash__().Should().Be(wrapper0.GetHashCode());
            wrapper1.__Hash__().Should().Be(wrapper1.GetHashCode());
        }

        [Fact]
        public void Wrapper_Equatable()
        {
            // If
            typeof(Wrapper<int>).IsAssignableTo(typeof(Equatable<Object>)).Should().BeTrue();
            typeof(Wrapper<int>).IsAssignableTo(typeof(Equatable<Wrapper<int>>)).Should().BeTrue();
            var wrapper0 = new Wrapper<int>(0);
            var wrapper1_0 = new Wrapper<int>(1);
            var wrapper1_1 = new Wrapper<int>(1);

            wrapper1_0.__Hash__().Should().NotBe(wrapper1_1.__Hash__());

            // When/then
            wrapper0.__Eq__(wrapper1_0).Should().BeFalse();
            wrapper0.__Eq__(wrapper1_1).Should().BeFalse();

            // Identity
            wrapper0.__Eq__(wrapper0).Should().BeTrue();
            wrapper1_0.__Eq__(wrapper1_0).Should().BeTrue();
            wrapper1_1.__Eq__(wrapper1_1).Should().BeTrue();

            // Symmetric
            wrapper1_0.__Eq__(wrapper1_1).Should().BeTrue();
            wrapper1_1.__Eq__(wrapper1_0).Should().BeTrue();
        }
    }
}
