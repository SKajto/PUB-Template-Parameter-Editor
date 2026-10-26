using PubTemplateEditor.Core.Editing;
using Xunit;

namespace PubTemplateEditor.Core.Tests;

/// <summary>
/// The review's "what actually changed" strip. It has to be exact about whitespace and letter case,
/// because those are the things a template value keeps and a reviewer cannot see in a table cell.
/// </summary>
public class TextDeltaTests
{
    [Fact]
    public void AnEditInTheMiddleKeepsTheTextAroundIt()
    {
        var delta = new TextDelta(
            "Sets how many units the player collects per level",
            "Sets how many units the player collects per town");

        Assert.False(delta.IsEqual);
        Assert.False(delta.IsCompleteReplacement);
        Assert.Equal("Sets how many units the player collects per ", delta.Prefix);
        Assert.Equal("level", delta.Removed);
        Assert.Equal("town", delta.Added);
        Assert.Equal("", delta.Suffix);
    }

    [Fact]
    public void AppendingToALongDescriptionReportsNoRemoval()
    {
        var delta = new TextDelta("alpha", "alpha and more");

        Assert.Equal("alpha", delta.Prefix);
        Assert.Equal("", delta.Removed);
        Assert.Equal(" and more", delta.Added);
        Assert.False(delta.HasRemoved);
    }

    [Fact]
    public void LetterCaseAndWhitespaceAreDifferencesLikeAnyOther()
    {
        var spacing = new TextDelta("Alpha", "Alpha ");
        Assert.Equal("Alpha", spacing.Prefix);
        Assert.Equal("", spacing.Removed);
        Assert.Equal(" ", spacing.Added);
        Assert.True(spacing.HasChange);

        var casing = new TextDelta("Alpha", "alpha");
        Assert.Equal("", casing.Prefix);
        Assert.Equal("lpha", casing.Suffix);
        Assert.Equal("A", casing.Removed);
        Assert.Equal("a", casing.Added);
    }

    [Fact]
    public void NothingSharedMeansACompleteReplacement()
    {
        var delta = new TextDelta("old value", "brand new text");

        Assert.True(delta.HasChange);
        Assert.True(delta.IsCompleteReplacement);
        Assert.Equal("old value", delta.Removed);
        Assert.Equal("brand new text", delta.Added);
    }

    [Fact]
    public void EqualValuesAreReportedAsEqual()
    {
        var delta = new TextDelta("same", "same");

        Assert.True(delta.IsEqual);
        Assert.False(delta.HasChange);
        Assert.Equal("same", delta.Prefix);
        Assert.False(delta.HasRemoved);
        Assert.False(delta.HasAdded);
    }

    [Fact]
    public void MissingAndEmptyValuesAreTreatedApart()
    {
        var added = new TextDelta(null, "now here");
        Assert.Equal("", added.Prefix);
        Assert.Equal("now here", added.Added);
        Assert.True(added.IsCompleteReplacement);

        var cleared = new TextDelta("was here", string.Empty);
        Assert.Equal("was here", cleared.Removed);
        Assert.Equal("", cleared.Added);
        Assert.True(cleared.HasChange);
    }

    [Fact]
    public void LongContextIsShortenedSoTheChangeStaysOnScreen()
    {
        string context = new('a', 200);
        var delta = new TextDelta(context + "old", context + "new");

        Assert.StartsWith("…", delta.PrefixShown);
        Assert.Equal(33, delta.PrefixShown.Length); // ellipsis plus the 32 characters of context
        Assert.Equal("old", delta.RemovedShown);
        Assert.Equal("new", delta.AddedShown);
        Assert.True(delta.PrefixShown.Length < delta.Prefix.Length);
    }

    [Fact]
    public void ALongChangedStretchIsShortenedAtBothEnds()
    {
        var delta = new TextDelta(new string('x', 200), "keep");

        Assert.StartsWith("x", delta.RemovedShown);
        Assert.Contains("…", delta.RemovedShown);
        Assert.True(delta.RemovedShown.Length < delta.Removed.Length);
    }

    [Fact]
    public void ASharedTailIsKeptOutOfTheChange()
    {
        var delta = new TextDelta("level bonus multiplier", "level coin multiplier");

        Assert.Equal("level ", delta.Prefix);
        Assert.Equal("bonus", delta.Removed);
        Assert.Equal("coin", delta.Added);
        Assert.Equal(" multiplier", delta.Suffix);
        Assert.Equal("level ", delta.PrefixShown);
        Assert.Equal(" multiplier", delta.SuffixShown);
    }
}
