# PUB Template Parameter Editor

An offline Electron desktop application for inspecting, comparing, and editing declared variables in IBM PUB `.dta` templates.

The editor stages default-value, access-classification, and supported description changes locally. Parameter names are read-only. Bulk operations copy values only. Changes are written only after review, and unsupported native container variants fail closed rather than being rewritten speculatively.

## Features

- Browse local template folders and explicitly read selected `.dta` files.
- Search and filter declarations by name, description, and access classification.
- Compare matching declarations across loaded templates.
- Stage bulk value changes for explicitly selected targets.
- Review each proposed field change before saving; excluded edits revert only after a successful save.
- Detect concurrent changes and keep backups and a recovery journal for save batches.
- Work offline; template processing stays on the local computer.

## Project documentation

This project was created with assistance from Hermes Agent and GPT-6-Luna.

- `AGENTS.md`, `START_HERE.md`, and `CODEX_START_PROMPT.md` provide coding-agent instructions and onboarding context.
- `REQUIREMENTS.md` defines product behavior and acceptance criteria; `ARCHITECTURE.md` describes the technical design; `PLAN.md` records milestone structure and verification notes.
- `docs/TEST_MATRIX.md` lists verification scenarios. `docs/NATIVE_DTA_FINDINGS.md` and `docs/SAMPLE_FINDINGS.md` summarize format observations and preservation limits.
- `desktop/README.md` contains application development and packaging instructions.

## Development

The Electron application is in `desktop/`. Run these commands from that directory:

```sh
npm ci
npm start
npm test
npm run typecheck
npm run lint
```

Create the Windows package on Windows with `npm run make`. On Ubuntu x64, use this command to package the application:

```sh
npm run make -- --platform=linux --arch=x64
```

The Ubuntu packaging host needs the `zip` utility. From the repository root, run the sample-inspector unit tests with `python -m unittest discover -s tools/tests -v`.

See [`desktop/README.md`](desktop/README.md) for detailed development and packaging notes, [`PLAN.md`](PLAN.md) for milestone status, and [`docs/TEST_MATRIX.md`](docs/TEST_MATRIX.md) for verification scenarios.

## License

This project is licensed under the GNU General Public License, version 3 only (GPL-3.0-only). See [`LICENSE`](LICENSE).
