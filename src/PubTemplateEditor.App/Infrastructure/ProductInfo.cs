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
        "Editor for the variable declarations of PUB template (.dta) files. " +
        "Edits are staged in memory and written only through Review Save.";

    // Shown to people; the machine-readable SPDX expression in Directory.Build.props, packaging and
    // NOTICE stays GPL-3.0-only, which is the same licence: version 3, no later versions.
    public const string LicenseId = "GPL-3.0";
    public const string CopyrightLine = "Copyright (C) 2026 Suad Kajtazovic";

    /// <summary>The public repository this project is developed in.</summary>
    public const string RepositoryUrl = "https://github.com/SKajto/PUB-Template-Parameter-Editor";

    /// <summary>Where the published releases and their downloads are listed.</summary>
    public static string ReleasesUrl => RepositoryUrl + "/releases";

    /// <summary>Where problems are reported and answered.</summary>
    public static string IssuesUrl => RepositoryUrl + "/issues";

    /// <summary>
    /// The project address as a label rather than a URL: the same text a link shows in a browser, and
    /// derived from RepositoryUrl so the two cannot drift apart.
    /// </summary>
    public static string RepositoryLabel
    {
        get
        {
            Uri uri = new(RepositoryUrl);
            return $"{uri.Host}/{uri.AbsolutePath.Trim('/')}";
        }
    }

    /// <summary>Where the source archive of the running version sits, named after its tag.</summary>
    public static string ReleaseSourceUrl => RepositoryUrl + "/releases/tag/v" + Version;

    public static string Version => TrimBuildMetadata(
        typeof(ProductInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0");

    /// <summary>
    /// The version exactly as the assembly carries it. A build that embeds its source revision shows
    /// "1.0.0+&lt;commit&gt;" here, which is useful to a bug report and useless as a version number: the
    /// published payload is built with that metadata off, so <see cref="Version"/> is the clean number.
    /// </summary>
    public static string FullVersion =>
        typeof(ProductInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

    private static string TrimBuildMetadata(string informationalVersion) =>
        informationalVersion.Split('+')[0].Trim();

    public static string RuntimeDescription => RuntimeInformation.FrameworkDescription;

    public static string OperatingSystemDescription => RuntimeInformation.OSDescription;

    public static string ProcessArchitecture => RuntimeInformation.ProcessArchitecture.ToString();
}
