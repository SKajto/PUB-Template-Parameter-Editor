# Code signing

The executable attached to a GitHub Release is signed. Everything CI builds is unsigned.

The private key lives in the signing service's own hardware and cannot be exported, so it can never be
a build secret: signing runs in a maintainer's Windows session with the vendor client connected to that
card, never in a pipeline. `ciSigning: false` in `packaging/CertumSigning.settings.json` records that,
neither workflow calls a signing tool, and `ChannelPolicyTests` keeps it that way.

**One key, and no other.** `packaging/CertumSigning.settings.json` records the issued certificate —
thumbprint, issuer, signer name and expiry — and its public half is committed at
`tools/signing/PUB-Template-Parameter-Editor-code-signing.cer`. `tools/signing/certum-sign.ps1` uses
that certificate and nothing else: it takes no key parameter, and it refuses a self-issued certificate,
a certificate without the code-signing purpose, a certificate whose issuer, name or expiry does not
match the recorded values, and a file already signed by another key.

`signtool` from the Windows SDK signs with SHA-256 file and timestamp digests and a mandatory RFC 3161
timestamp, because an untimestamped signature stops verifying the day the certificate expires. The
script then reads the result back and fails unless the platform reports `Valid` with a timestamp —
`Valid` on the maintainer's machine, where the chain and the revocation endpoints are reachable, is what
a user's machine will compute from the same chain. `tools/signing/verify-signature.ps1` reads a
signature and a hash and writes nothing.

A signature is appended to the file, so the SHA-256 published with a release is the one taken after
signing. A rebuilt copy — by CI, or by anyone exercising the GPL's rights — is unsigned.
