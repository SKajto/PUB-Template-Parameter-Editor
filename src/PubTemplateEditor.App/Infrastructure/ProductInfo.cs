using System.Reflection;
using System.Runtime.InteropServices;

namespace PubTemplateEditor.App.Infrastructure;

/// <summary>
/// Single source of truth for the product identity shown in the shell, About view and
/// Store metadata. Version comes from the assembly, so a build never reports a stale value.
/// </summary>
public static class ProductInfo
{
    public const string Name = "PUB Template Parameter Editor";
    public const string Tagline = "Local template workspace";

    public const string Description =
        "Offline editor for the variable declarations of PUB template (.dta) files. " +
        "Edits are staged in memory and written only through Review Save.";

    public const string LicenseId = "GPL-3.0-only";
    public const string CopyrightLine = "Copyright (C) PUB Template Parameter Editor contributors";

    /// <summary>
    /// The GitHub location is fixed when the repository is first published; the exact-version
    /// source link is part of the release/Store metadata, not a compiled-in guess.
    /// </summary>
    public const string SourceNote =
        "The complete corresponding source for this exact version is published with the " +
        "release of the same version under GPL-3.0-only.";

    public static string Version =>
        typeof(ProductInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

    public static string RuntimeDescription => RuntimeInformation.FrameworkDescription;

    public static string OperatingSystemDescription => RuntimeInformation.OSDescription;

    public static string ProcessArchitecture => RuntimeInformation.ProcessArchitecture.ToString();
}
