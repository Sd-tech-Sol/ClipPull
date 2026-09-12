using System.ComponentModel;
using ClipPull.ViewModels;

namespace ClipPull.Tests;

public sealed class QueueItemViewModelTests
{
    [Fact]
    public void SubtitleStatusTextIsDerivedLiveFromResultStateNotFrozenAtAssignmentTime()
    {
        var item = new QueueItemViewModel("https://example.com", "Web", "example", 0, 0)
        {
            SubtitleResultState = SubtitleResultState.NoSubtitlesAvailable
        };
        var unavailableText = item.SubtitleStatusText;

        item.SubtitleResultState = SubtitleResultState.Saved;
        var savedText = item.SubtitleStatusText;

        // Regression for storing an already-localized string: if the text were captured
        // once and cached, changing the state afterward would not change what reads back.
        Assert.NotEqual(unavailableText, savedText);
    }

    [Fact]
    public void RefreshLocalizationNotifiesSubtitleStatusText()
    {
        var item = new QueueItemViewModel("https://example.com", "Web", "example", 0, 0)
        {
            SubtitleResultState = SubtitleResultState.ConversionRequiresFfmpeg
        };
        var raisedProperties = new List<string?>();
        item.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        item.RefreshLocalization();

        Assert.Contains(nameof(QueueItemViewModel.SubtitleStatusText), raisedProperties);
    }

    [Fact]
    public void HasSubtitleStatusIsFalseOnlyWhenStateIsNone()
    {
        var item = new QueueItemViewModel("https://example.com", "Web", "example", 0, 0);

        Assert.False(item.HasSubtitleStatus);

        item.SubtitleResultState = SubtitleResultState.Saved;
        Assert.True(item.HasSubtitleStatus);

        item.ResetProgress();
        Assert.False(item.HasSubtitleStatus);
    }

    [Fact]
    public void ResetProgressClearsSubtitleResultState()
    {
        var item = new QueueItemViewModel("https://example.com", "Web", "example", 0, 0)
        {
            SubtitleResultState = SubtitleResultState.NoSubtitlesAvailableLanguage
        };

        item.ResetProgress();

        Assert.Equal(SubtitleResultState.None, item.SubtitleResultState);
    }
}
