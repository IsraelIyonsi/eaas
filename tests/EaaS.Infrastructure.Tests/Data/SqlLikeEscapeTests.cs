using EaaS.Infrastructure.Data;
using FluentAssertions;
using Xunit;

namespace EaaS.Infrastructure.Tests.Data;

/// <summary>
/// H4 regression guard: user-supplied values embedded in LIKE/ILIKE patterns
/// must have '%', '_', and '\' prefixed with '\' so callers cannot widen a
/// bounded search into an unanchored wildcard scan or probe unrelated rows.
/// </summary>
public sealed class SqlLikeEscapeTests
{
    [Fact]
    public void Escape_PercentSign_IsEscapedWithBackslash()
    {
        var result = SqlLikeEscape.Escape("foo%bar");

        result.Should().Be("foo\\%bar");
    }

    [Fact]
    public void Escape_Underscore_IsEscapedWithBackslash()
    {
        var result = SqlLikeEscape.Escape("foo_bar");

        result.Should().Be("foo\\_bar");
    }

    [Fact]
    public void Escape_Backslash_IsEscapedWithBackslash()
    {
        var result = SqlLikeEscape.Escape("foo\\bar");

        result.Should().Be("foo\\\\bar");
    }

    [Fact]
    public void Escape_PlainAscii_IsUnchanged()
    {
        var result = SqlLikeEscape.Escape("user@example.com");

        result.Should().Be("user@example.com");
    }

    [Fact]
    public void Escape_AllMetacharactersTogether_AreEscapedInOrder()
    {
        var result = SqlLikeEscape.Escape("a%b_c\\d");

        result.Should().Be("a\\%b\\_c\\\\d");
    }

    [Fact]
    public void Escape_Null_ReturnsEmpty()
    {
        var result = SqlLikeEscape.Escape(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Escape_Empty_ReturnsEmpty()
    {
        var result = SqlLikeEscape.Escape(string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void EscapeCharacter_IsBackslash()
    {
        SqlLikeEscape.EscapeCharacter.Should().Be("\\");
    }
}
