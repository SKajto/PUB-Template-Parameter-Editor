# Initial task for Codex

Build a local Windows and Linux desktop application for editing declared variables in IBM PUB templates, following REQUIREMENTS.md, ARCHITECTURE.md and PLAN.md. Read AGENTS.md and inspect the current repository before making changes.

Start with milestone M0. Inspect the actual native DTA files in samples/ and confirm their structure instead of assuming that a .dta file is XML or a ZIP archive. Use docs/SAMPLE_FINDINGS.md as limited evidence about XML exports. Establish exact mappings for name, description, default value and internal/external access. Preserve the separate XML type property.

Verify the proposed Electron, React and TypeScript architecture against the supported operating systems and no-admin runtime requirements. Select and lock supported dependencies. Create a minimal packaged GUI and prove ordinary-user startup on the available target systems. Establish a source-preserving template editing approach with meaningful round-trip checks before building the editing interface.

Do the implementation and checks needed for M0, then update PLAN.md and AGENTS.md with findings, real commands, evidence, and open gates. If native DTA samples or a target platform are unavailable, complete useful independent work, report exactly what remains unverified, and request only the missing input needed to close that gate. Do not invent format details or claim tests you could not run.

Do not implement the entire application in one unverified pass. At the end of M0, provide the concrete result and the proposed next milestone. Later milestones must preserve explicit staging, field-level review, value-only bulk changes, no writes before review Save, source preservation, and accurate recovery behavior.
