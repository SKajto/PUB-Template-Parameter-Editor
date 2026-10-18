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

## Status and compatibility

The native adapter is based on the observed ZIP-backed DTA variant and preserves unrelated source content within the tested format subset. Other DTA variants remain read-only until their preservation behavior is proven. PUB compatibility has not yet been verified by opening edited templates and generating output in PUB.

A Linux x64 package has been built and launched through WSLg on Ubuntu 26.04.1. A save-path issue on WSL's `/mnt/c` mount was corrected and passed a synthetic local save test. Ubuntu 24.04 runtime validation and a complete Review Save GUI check on the rebuilt package remain open. The current Windows development signing certificate is self-signed and is not a trusted public publisher; SignPath Foundation setup is planned.

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

## Proprietary templates

Do not add or upload proprietary `.dta` templates. The root `.gitignore` excludes `.dta` files at every directory depth; no template samples are distributed in this repository.

## License

This project is licensed under the GNU General Public License, version 3 only (GPL-3.0-only). See [`LICENSE`](LICENSE).
