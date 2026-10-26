# Reference implementation review (functionality and UI)

Reviewed on 2026-10-25.

- Upstream project: **SKajto/PUB-Template-Parameter-Editor** —
  https://github.com/SKajto/PUB-Template-Parameter-Editor
- Reviewed revision: `7aa497d61f37fd4f7430eba266c5f6b18bf2ef42` (branch `main`,
  last push 2026-10-20, commit `chore: bundle third-party license notices`)
- Upstream licence: GPL-3.0 (`GPL-3.0-only` in this project's terms is the same
  licence family, so studying that implementation is compatible with this
  repository's licence).
- Upstream stack: offline Electron desktop app (React/TypeScript, webpack), with a
  `src/core` layer for parsing/search/compare/drafts/patching and a main process
  for reading, saving and settings.

## What this review is, and what it is not

This project is a C#/WPF/.NET 10 implementation. The upstream project is treated
as a **functional and interaction reference**: it shows which screens, controls,
labels and workflow guarantees a user of the tool expects.

- **No upstream source file is copied, translated or vendored into this
  repository.** Implementation, XAML and tests are written from the behaviour
  described here and from REQUIREMENTS.md/ARCHITECTURE.md.
- Downloaded upstream files were kept only in the gitignored `.reference/`
  directory for reading; they are not part of the source distribution, the
  portable payload or the MSIX payload.
- Upstream test results say nothing about native PUB compatibility of *this*
  application. The S5 native gate stays open here.
- Provenance is recorded in `NOTICE` so that downstream builders see the design
  lineage.

## Screen map taken from the reference (as implemented in `MainWindow.xaml`)

| Area | Reference structure | This project |
| --- | --- | --- |
| Top bar | brand mark + product title + "Local template workspace" subtitle, theme buttons (Light/Dark/System), Review-and-save (count) shown only when drafts exist, Discard all (count), **Choose folder** | implemented except Review/Discard (S3/S4) |
| Explorer (left) | `TEMPLATES` eyebrow, folder name + file count badge, *Filter files* box with `Filename…` placeholder, **Select visible** / **Clear visible**, "n selected for reading", file rows with read checkbox + name + `size · status`, empty-state text, read-scope sentence + **Read parameters** | implemented |
| Parameter area (centre) | `ACTIVE TEMPLATE` eyebrow + focused file name, search box `Search name or description…`, access select (`All access` / `Internal` / `External`), Compare and "Apply value to templates" buttons, notices, "x matched of y parameters" summary, table `Name / Access / Default value / Description` with `Unsaved` and `Read-only shape` badges | table, search, access filter, notices and summary implemented; Compare / bulk buttons arrive with S3 |
| Details (right, resizable) | cards: *Parameter name* (read-only), *Default value*, *Access*, *Description*, *XML type property* with the note "Separate from Internal / External access", unsupported-shape notice listing reasons, `Apply edits` / `Reset unapplied form`, safety card "Review-gated saving" | cards and the XML-type note implemented read-only with explanations; editors enabled in S3 |
| Footer | "n templates in folder", offline/progress line "Offline · Review Save writes selected changes", `Version x` button opening About | implemented; About opens the offline Licenses window |
| Overlays | unsaved-work navigation dialog (Apply editor forms / Save for review / Discard and continue / Keep editing), Compare panel, bulk value staging panel, review overlay with per-change "Deliver this field change" checkboxes and `Back` / `Save n changes` | implemented. Deliberate difference: the reference bulk panel copies the default value only; here bulk editing offers the default value, the access (internal/external) and the description as three switches, because the owner asked for the same parameter to be aligned across templates field by field |

## Behaviour rules mirrored

- **Read scope vs focus.** The read checkbox and the active (focused) template are
  independent; focusing a row never changes read or bulk selection, and filtering
  never changes selection. With no checkboxes set, reading falls back to the
  visible files rather than reading nothing.
- **Folder listing.** Non-recursive, `.dta` only; a file that cannot be stat'ed is
  listed with its error and disabled for reading instead of hiding the rest.
  Listing never parses (FILE-04).
- **Search semantics** (`Core/Search/ParameterSearchMatcher.cs`, covered by
  `ParameterSearchTests`): names are **case-sensitive anchored prefix** matches
  where `*` is the only wildcard; descriptions match **anywhere,
  case-insensitively**. If the query occurs inside the name but not at its start,
  the name region counts as attempted and the description must not rescue the row.
  Regex metacharacters in the query stay literal, and `*` spans newlines inside
  long multi-line values.
- **Values are text.** Empty and missing values render as visibly distinct
  `(empty)` / `(missing)` markers; nothing is trimmed or type-coerced on display.
- **Access vs XML type.** The UI Access field maps to the `access` property; the
  separate `type` property is displayed read-only with the explicit note that it
  is not the Internal/External control.
- **Save wording.** The footer and the details safety card state that saving is
  review-gated and that a multi-file save is not one global atomic operation.
- **Theme.** Light / Dark / System segmented control; System follows the Windows
  app-colour registry value and is stored as `System`.

## Recorded differences

- The reference keeps About in a modal dialog with a bundled-component table
  (name/version/licence). This project has an **About tab** in the offline
  `LicensesWindow` (opened by the footer `Version …` button or Ctrl+L) that shows
  product name, tagline, description, version, licence id, copyright, runtime and
  OS/architecture, the corresponding-source note and the bundled-component
  statement, plus the no-warranty paragraph. The component list is rendered as a
  paragraph rather than a sortable table.
- Palette: the reference CSS values are used for the light theme — accent
  `#315FCA`, border `#D8E0EB`, text `#364150`, muted `#8792A2`, window surface
  `#F3F5F8`, panel surface `#FFFFFF`, plus the accent-soft/selection/hover and
  error/success/warning background pairs. The upstream project ships one light
  palette only, so the **dark values are derived** in this project: they keep the
  same role structure (cool grey surfaces, blue accent) with contrast adjusted for
  dark surfaces. A test (`ThemeTests.ShellXamlCarriesNoHardCodedColours`) forbids
  colour literals outside the two theme dictionaries.
- The reference uses HTML `<pre>` diff blocks; this project uses read-only WPF
  text controls with monospace fonts.
- Upstream panels are resizable through a custom pointer-driven resizer; here the
  built-in `GridSplitter` with `MinWidth`/`MaxWidth` gives the same 340–520 px
  details range.
- Upstream search runs over already-read declarations in React state; here the same
  semantics live in `PubTemplateEditor.Core/Search/ParameterSearchMatcher.cs` and
  are covered by Core tests independent of any UI.
- Upstream recovery notices for interrupted saves are a startup feature; the
  recovery journal itself is implemented in S4 (`TemplateSaveService`), the
  startup notice over it is still open.

## Open verification gates for this UI

- The rendered layout is covered by structural assertions and PNG snapshots
  written by `ShellSnapshotTests` (`artifacts/screenshots/`, gitignored).
  Human visual review of those snapshots is still required.
- Interactive GUI behaviour (mouse/keyboard on a real desktop session) is
  separately recorded in PLAN.md; software-rendered snapshots do not prove it.

## Parameter tools and the button layer (2026-10-26)

The reference puts a `parameter-actions` row between its main toolbar and the
parameter table, holding "Compare" and "Apply value to templates" (`.reference/
desktop_src_App.tsx`, `<div className="parameter-actions">`). Both act on the
parameter selected in the table, which is why they hang on that panel rather than
on the details form. The same placement is used here: `ParameterActionsPanel` is
the row directly under the active-template/search/access toolbar and above the
notices and the table.

Differences that follow from how this project works:

- There is no per-parameter review button. The review always lists the staged
  changes of the whole session, so it appears exactly once - in the top bar next
  to the staged-change count - and through Ctrl+S. `ButtonChromeTests.
  TheSaveReviewHasASingleSessionWideEntryPoint` fails if a second entry point
  appears anywhere in the shell.
- Form actions ("Apply edits", "Undo unapplied typing", "Discard staged for this
  parameter") stay with the details form they apply to.

Button styling follows **Microsoft Fluent UI** rather than Material or Tailwind:
it is the convention a Windows desktop user expects, it matches the stock WPF
control set, and it needs no toolkit package. Concretely: a command button is a
16 px glyph at the left of a real text label, minimum 31 px target height, 6 px
corner radius, subtle hover fill, a separate pressed fill and an accent focus
ring; the accent variant keeps its own hover/pressed shades
(`AccentHoverBrush`, `AccentPressedBrush` in both theme dictionaries). Link-style
actions are Fluent subtle buttons, where the glyph is optional so plain text
links do not gain a hollow gap.

The glyph is supplied by the `Iconics.Icon` attached property
(`Infrastructure/Iconics.cs`), read by the button templates in
`Themes/Controls.xaml`, so a command button stays one line of XAML. The geometries
remain the project's own hand-drawn paths; no icon set was copied or vendored.
Interaction fills are read from `Iconics.HoverBrush` / `Iconics.PressedBrush`
because a control template trigger cannot be overridden by a `BasedOn` style.
