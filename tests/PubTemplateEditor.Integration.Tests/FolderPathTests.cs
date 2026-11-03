using System;
using System.IO;
using PubTemplateEditor.Infrastructure;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// Folder paths as both the file system and the Windows shell accept them. A remembered folder travels
/// through a user-editable settings file, and the two consumers disagree: '/' separators open fine on
/// disk but are refused by the shell item API behind the folder dialog, which is how one entry in a
/// settings file came to close the application on another machine.
/// </summary>
public class FolderPathTests
{
    [Theory]
    [InlineData("C:/Users/x/templates", @"C:\Users\x\templates")]
    [InlineData(@"C:\Users\x\templates\", @"C:\Users\x\templates")]
    [InlineData("  C:\\Users\\x\\templates  ", @"C:\Users\x\templates")]
    [InlineData("\"C:\\Program Files\\x\"", @"C:\Program Files\x")]
    [InlineData("C:/a/b/../c", @"C:\a\c")]
    [InlineData(@"\\server\share\templates", @"\\server\share\templates")]
    [InlineData(@"C:\", @"C:\")]
    [InlineData(@"D:\", @"D:\")]
    public void ARememberedPathIsNormalisedToTheFormBothConsumersAccept(string input, string expected) =>
        Assert.Equal(expected, FolderPaths.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/templates")]
    [InlineData("templates")]
    [InlineData(@"\\?\C:\Users\x")]           // device-namespace form: the file system takes it, the shell item API does not
    [InlineData(@"\\.\C:\Users\x")]
    [InlineData("C:\\Users\\\0x")]            // a NUL in the middle of the path
    public void ANotPathIsRefusedRatherThanGuessedAt(string? input) =>
        Assert.Null(FolderPaths.Normalize(input));

    [Fact]
    public void NormalisingTwiceChangesNothing()
    {
        string once = FolderPaths.Normalize("C:/Users/x/templates/")!;
        Assert.Equal(once, FolderPaths.Normalize(once));
    }

    [Fact]
    public void AFolderThatExistsInNormalisedFormMayStartTheShellDialog()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"pubte-foldernormalise-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Assert.True(FolderPaths.IsShellAddressable(directory));

            // The same folder, in every form the shell refuses.
            Assert.False(FolderPaths.IsShellAddressable(directory.Replace('\\', '/')));
            Assert.False(FolderPaths.IsShellAddressable(directory + "\\"));
            Assert.False(FolderPaths.IsShellAddressable(null));

            Directory.Delete(directory);
            Assert.False(FolderPaths.IsShellAddressable(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void AFolderNameWithSpacesAndAPunctuationCharacterSurvives()
    {
        string baseDirectory = Path.Combine(Path.GetTempPath(), $"pubte-foldernormalise-{Guid.NewGuid():N}");
        string nested = Path.Combine(baseDirectory, "My Templates (2026)");
        Directory.CreateDirectory(nested);
        try
        {
            string written = nested.Replace('\\', '/');
            Assert.Equal(nested, FolderPaths.Normalize(written));
            Assert.True(FolderPaths.IsShellAddressable(FolderPaths.Normalize(written)));
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }
}
