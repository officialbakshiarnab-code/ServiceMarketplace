using System.Text.Json;
using FluentAssertions.Collections;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace FluentAssertions;

public static class TestAssertionCompatibilityExtensions
{
    public static AndConstraint<ObjectAssertions> HaveProperty(
        this ObjectAssertions assertions,
        string propertyName,
        string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(assertions.Subject is JsonElement element && element.TryGetProperty(propertyName, out _))
            .FailWith("Expected JSON object to have property {0}{reason}.", propertyName);

        return new AndConstraint<ObjectAssertions>(assertions);
    }

    public static AndConstraint<GenericCollectionAssertions<T>> AllBe<T>(
        this GenericCollectionAssertions<T> assertions,
        T expected,
        string because = "",
        params object[] becauseArgs)
    {
        return assertions.OnlyContain(
            item => EqualityComparer<T>.Default.Equals(item, expected),
            because,
            becauseArgs);
    }

    public static AndConstraint<DateTimeAssertions> BeGreaterThan(
        this DateTimeAssertions assertions,
        DateTime expected,
        string because = "",
        params object[] becauseArgs)
    {
        return assertions.BeAfter(expected, because, becauseArgs);
    }

    public static AndConstraint<StringAssertions> Contain(
        this StringAssertions assertions,
        string expected,
        StringComparison comparison,
        string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(assertions.Subject?.IndexOf(expected, comparison) >= 0)
            .FailWith("Expected string to contain {0}{reason}.", expected);

        return new AndConstraint<StringAssertions>(assertions);
    }
}
