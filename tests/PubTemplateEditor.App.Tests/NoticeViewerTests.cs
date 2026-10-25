using System;
using System.IO;
using PubTemplateEditor.App;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.App.ViewModels;
using PubTemplateEditor.Infrastructure;
using Xunit;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// The GPL text and the bundled-component notices have to travel inside the binary
/// and stay readable without a network connection. These tests fail if the build
/// stops embedding them or if the viewer window stops filling its text panes.
/// </summary>
[Collection("WPF shell")]
public class NoticeViewerTests
{
    private readonly WpfHost _host;

    public NoticeViewerTests(WpfHost host) => _host = host;

    [Fact]
    public void GplAndNoticesAreEmbeddedAndReadable() => _host.Dispatcher.Invoke(() =>
    {
        string gpl = EmbeddedNotices.Read(EmbeddedNotices.License);
        Assert.Contains("GNU GENERAL PUBLIC LICENSE", gpl);
        Assert.Contains("Version 3", gpl);

        string notice = EmbeddedNotices.Read(EmbeddedNotices.CopyrightNotice);
        Assert.Contains("GPL-3.0-only", notice);
        Assert.Contains("Copyright (C)", notice);

        string thirdParty = EmbeddedNotices.Read(EmbeddedNotices.ThirdParty);
        Assert.Contains(".NET", thirdParty);
        Assert.DoesNotContain("REPLACE_", thirdParty);
    });

    [Fact]
    public void LicensesWindowShowsTheTextsAndNamesTheExactVersion() => _host.Dispatcher.Invoke(() =>
    {
        LicensesWindow window = new();
        Assert.Contains("GNU GENERAL PUBLIC LICENSE", window.LicenseContent);
        Assert.Contains("Copyright (C)", window.NoticeContent);
        Assert.Contains("Third-party notices", window.ThirdPartyContent);
        Assert.Contains(".NET", window.ThirdPartyContent);
        Assert.Contains("Version ", window.SourceContent);
        Assert.Equal(3, window.NoticeTabCount);
        window.Close();
    });

    [Fact]
    public void LicensesCommandIsRaisedByTheViewModelWithoutTouchingWindows() => _host.Dispatcher.Invoke(() =>
    {
        string temp = Path.Combine(Path.GetTempPath(), "pub-notices-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            SettingsService settings = new(temp);
            WorkspaceViewModel vm = new(settings, new EditorSettings());

            int raised = 0;
            vm.LicensesRequested += () => raised++;
            vm.OpenLicensesCommand.Execute(null);

            Assert.Equal(1, raised);
            Assert.True(vm.OpenLicensesCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    });
}
