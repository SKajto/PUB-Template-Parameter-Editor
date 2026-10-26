using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
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
        Assert.Equal(4, window.NoticeTabCount);

        // About view: identity, licence, runtime and bundled components, offline.
        Assert.Equal("PUB Template Parameter Editor", window.AboutName);
        Assert.False(string.IsNullOrWhiteSpace(window.AboutVersion));
        Assert.Contains("GPL-3.0-only", window.AboutLicense);
        Assert.Contains("Copyright (C)", window.AboutCopyright);
        Assert.Contains(".NET", window.AboutRuntime);
        Assert.Contains(".NET runtime and WPF", window.AboutBundled);
        Assert.DoesNotContain("REPLACE_", window.AboutBundled);
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

    /// <summary>
    /// The window, taskbar and Alt+Tab picture. A wrong assembly part in the pack URI does not fail
    /// the build - it fails at runtime, in a XamlParseException on the first window - so the resource
    /// is resolved here, and a real window is constructed to prove the same URI works from XAML.
    /// </summary>
    [Fact]
    public void TheApplicationIconIsEmbeddedAndResolvesForEveryWindow() => _host.Dispatcher.Invoke(() =>
    {
        var uri = new Uri("pack://application:,,,/PUB_Template_Parameter_Editor;component/Assets/app.ico");
        StreamResourceInfo? resource = Application.GetResourceStream(uri);
        Assert.NotNull(resource);
        Assert.True(resource!.Stream.Length > 1000, "the icon resource looks truncated");

        string temp = Path.Combine(Path.GetTempPath(), "pub-icon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var vm = new WorkspaceViewModel(new SettingsService(temp), new EditorSettings());
            var window = new MainWindow(vm);
            Assert.NotNull(window.Icon);
            // An .ico decodes to a frame of the multi-frame image, not to a BitmapImage.
            var frame = Assert.IsAssignableFrom<BitmapSource>(window.Icon);
            Assert.True(frame.PixelWidth >= 16 && frame.PixelHeight >= 16,
                $"the icon must decode to a usable frame, got {frame.PixelWidth}x{frame.PixelHeight}");
            window.Close();
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    });

    /// <summary>
    /// The About view has to name where the source of this exact build is, because that is a GPL
    /// obligation and not a marketing line.
    /// </summary>
    [Fact]
    public void AboutNamesTheRepositoryAndTheSourceOfThisVersion() => _host.Dispatcher.Invoke(() =>
    {
        var window = new LicensesWindow();
        window.Show();

        Assert.Contains("github.com/SKajto/PUB-Template-Parameter-Editor", window.AboutSource);
        Assert.Contains("/releases/tag/v", window.AboutSource);
        window.Close();
    });
}
