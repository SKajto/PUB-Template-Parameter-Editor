# Code signing

The code is currently **not signed**.

If that changes: the key stays in the signing service's own hardware, so signing runs in a
maintainer's Windows session, never in CI. `tools/signing/certum-sign.ps1` signs with SHA-256 digests
and a timestamp, and refuses until `packaging/CertumSigning.settings.json` records a real key. Signing
appends to the file, so its SHA-256 changes and the release hash is taken afterwards.
`tools/signing/verify-signature.ps1` reads a signature back and writes nothing.
