# Security policy

## What is supported

| Version | Supported |
| --- | --- |
| latest release from the GitHub Releases page (the unsigned single-file executable, with published SHA-256 hashes) | yes |
| `main` branch (what CI builds) | yes, best effort |
| older releases | security fixes are applied to the newest release; older ones are rebuilt from the tag when a fix matters |

## The threat model this application is built around

The program is offline by design, so the interesting attack surface is *the files it opens*.

- **A template file is untrusted input.** It is parsed with `XmlReader` with
  `DtdProcessing.Prohibit` and `XmlResolver = null`, so a document type declaration, external
  entity or external parameter entity is refused rather than fetched. There is no network
  capability in the manifest, so "refused rather than fetched" is not the only line of defence.
- **Parameter names, descriptions and values are text.** They are never evaluated, never turned
  into XAML, never passed to a shell, and never used to construct a path on their own. Display
  paths are taken from the folder the user picked, not from file content.
- **Saving is the risky operation, and it is deliberately narrow.** Nothing writes unless the user
  opens Review Save and confirms the selected rows. Each file is fingerprint-checked against the
  bytes the program read, so an outside change is reported instead of silently overwritten; a
  timestamped backup and a recovery journal are kept in the user's own profile; a failure names the
  file that was not written rather than pretending the batch succeeded.
- **The ZIP container is handled with the built-in reader.** The program does not extract a `.dta`
  to disk; it reads the template entry in memory and rewrites the package only on a confirmed save.
  A container that is not a readable ZIP, or has no template entry, is reported as unreadable and
  the other files in the folder are still listed.
- **No elevated privileges, no service, no startup entry.** Settings, backups and the journal live
  under `%LOCALAPPDATA%\PubTemplateEditor` of the running user.
- **The distributed executable** is a single self-contained file that extracts its native
  parts to the user's temp directory - ordinary self-contained .NET behaviour, not an installer. It
  runs under normal Windows policies. The most it can carry is a **self-signed** signature,
  which no trust provider on your machine has any reason to believe: verify the SHA-256 published
  with the release before running it. A hash that does not match, or a signed copy that reports
  `HashMismatch`, is a supply-chain problem - please report it privately as described below.

## Reporting a problem

Please report security problems **privately**, not through a public issue:

1. Use the *Report a vulnerability* form on the
   [Security tab](https://github.com/SKajto/PUB-Template-Parameter-Editor/security/advisories/new)
   of this repository. It reaches only the maintainers and starts a private draft advisory.
2. If that is not possible, contact the repository owner through their GitHub profile at
   <https://github.com/SKajto>.

Include, as far as you can:

- the version you ran (the About view shows it, together with the .NET runtime and OS),
- what the program did and what you expected instead,
- whether a template file triggered it, and *how the file is shaped* — the offending element,
  attribute or container entry — rather than the file itself,
- the last lines of `%LOCALAPPDATA%\PubTemplateEditor\recovery-journal.jsonl` **with paths and
  values removed**, if a save was involved.

Please do not attach your real templates to any issue, public or private. They are your business
data, and the maintainer cannot learn anything from them that a synthetic file cannot show: a small
hand-made `.dta`/XML that reproduces the shape is worth more than a large real one.

## What to expect

This project has one maintainer working on it in their own time, so there is no SLA and no bug
bounty. What you can expect: an acknowledgement, a fix in the next release when the problem is real
and reproducible, and a credit in the release notes if you want one. What you should not expect:
support for constructs the format documentation does not define, and no promise about how
SmartScreen or antivirus products will treat an unsigned binary (see
[docs/RELEASE_PACKAGING.md](docs/RELEASE_PACKAGING.md)).
