using PubTemplateEditor.Core.Search;
using Xunit;

namespace PubTemplateEditor.Core.Tests;

/// <summary>
/// Explorer search rules: names are case-sensitive anchored prefix searches with '*'
/// as the only wildcard; descriptions match anywhere, case-insensitively.
/// </summary>
public class ParameterSearchTests
{
    [Fact]
    public void EmptyQueryMatchesEverything()
    {
        Assert.True(ParameterSearchMatcher.Matches("any_name", "any text", ""));
        Assert.True(ParameterSearchMatcher.Matches("any_name", "any text", "   "));
    }

    [Fact]
    public void NameMatchesAtTheStartAndIsCaseSensitive()
    {
        Assert.True(ParameterSearchMatcher.Matches("max_users", "d", "max"));
        Assert.True(ParameterSearchMatcher.Matches("max_users", "d", "max_users"));
        Assert.False(ParameterSearchMatcher.Matches("max_users", "no match here", "MAX"));
        Assert.False(ParameterSearchMatcher.Matches("max_users", "no match here", "users"));
    }

    [Fact]
    public void StarSpansAnyPartOfAName()
    {
        Assert.True(ParameterSearchMatcher.Matches("server_max_users", "d", "server*users"));
        Assert.True(ParameterSearchMatcher.Matches("server_max_users", "d", "*users"));
        Assert.True(ParameterSearchMatcher.Matches("abc", "d", "a*c"));
        Assert.False(ParameterSearchMatcher.Matches("abc", "d", "a*x"));
    }

    [Fact]
    public void RegexCharactersInTheQueryAreLiteral()
    {
        Assert.True(ParameterSearchMatcher.Matches("a.b", "d", "a.b"));
        Assert.False(ParameterSearchMatcher.Matches("axb", "d", "a.b"));
        Assert.True(ParameterSearchMatcher.Matches("a[0]b", "d", "a*b"));
    }

    [Fact]
    public void DescriptionMatchesAnywhereCaseInsensitively()
    {
        // The name must not contain the query, otherwise the name region already
        // claimed the attempt (see AQueryInsideTheNameDoesNotFallBackToTheDescription).
        Assert.True(ParameterSearchMatcher.Matches("setting_x", "TCP port number", "port"));
        Assert.True(ParameterSearchMatcher.Matches("setting_x", "TCP PORT number", "PORT"));
        Assert.True(ParameterSearchMatcher.Matches("setting_x", "TCP port number", "p*t"));
        Assert.False(ParameterSearchMatcher.Matches("setting_x", "TCP port number", "udp"));
    }

    [Fact]
    public void AQueryInsideTheNameDoesNotFallBackToTheDescription()
    {
        // 'users' occurs inside the name but not at its start: the name region was
        // attempted, so the description must not rescue the row.
        Assert.False(ParameterSearchMatcher.Matches("max_users", "users appear in the description", "users"));
    }

    [Fact]
    public void MissingValuesAreMatchedAsTextOnly()
    {
        Assert.True(ParameterSearchMatcher.Matches(null, "description text", "des"));
        Assert.False(ParameterSearchMatcher.Matches(null, null, "anything"));
        Assert.True(ParameterSearchMatcher.Matches(null, null, ""));
    }

    [Fact]
    public void NewlinesInsideLongValuesAreSpanned()
    {
        Assert.True(ParameterSearchMatcher.Matches("multi", "first line\nsecond line", "first*second"));
    }
}
