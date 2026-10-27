# Licensing and source availability

**GPL-3.0-only.** Not legal advice - an engineering checklist of what this repository has to carry so
that the four freedoms it grants are real for a recipient.

| Requirement | How it is met here |
| --- | --- |
| The licence text, unmodified | `LICENSE` is the canonical FSF GNU GPL v3 document, byte for byte |
| A recorded SPDX choice | `GPL-3.0-only` in `Directory.Build.props`, `NOTICE`, the About view and the release notes; switching to `-or-later` would need an explicit decision by the copyright holder |
| Copyright and provenance notice | `NOTICE` (contributors, artwork provenance, status of bundled components) |
| Notices for bundled components | `THIRD-PARTY-NOTICES.txt`; the .NET runtime and WPF are bundled self-contained and keep their own MIT/.NET Runtime terms - bundling them does not relicense them, and they are the kind of system-library inclusion GPL v3 §1 allows |
| Complete corresponding source next to every binary | each GitHub Release carries the source archive (`PUB_Template_Parameter_Editor-<version>-source.tar.gz`) and is also tagged in Git; the in-app About view names the repository and the source URL of the running version |
| A recipient can actually rebuild it | build, test and publish commands in [README.md](../README.md) and [PROJECT_SETUP.md](PROJECT_SETUP.md); the CI workflow files are in the repository; nothing needed to build requires the maintainer's secrets, accounts or signing keys |
| No restrictions added on top | no app EULA, no licence key, no trial or expiry check, no feature gating, no requirement that the program only runs when obtained from one place |
| Modification is possible in practice | parameter names stay read-only and unsupported file constructs stay read-only for safety, which is a product decision about not corrupting files - not a legal restriction on modifying the program |

## Data that is not source

Users' PUB template files are their content, not this program's source. They are excluded from the
repository (`.gitignore` + `SamplePolicyTests`), and the format knowledge gained from them is written
up in [SAMPLE_FINDINGS.md](SAMPLE_FINDINGS.md).

## Channel

Distribution is a GitHub Release. No app-store terms apply today. If another channel is added later,
review that channel's terms before using it - in particular whether they would conflict with the
rights GPL grants - and record the outcome here.
