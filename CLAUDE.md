# KiteWin

A native Windows app in WinUI 3. It runs on this PC only, with a local
SQLite database and no server.

## Layout

- `src/KiteWin/` the app. `App.xaml.cs` lists the agent flags and starts the window; `MainWindow.xaml` has the title bar and the navigation.
- `src/KiteWin.Core/` rules and data logic with no WinUI: the models and `ItemStore` (SQLite). Tests in `tests/KiteWin.Core.Tests/`.
- `KiteWin.slnx` the solution. There is no Visual Studio project to open: everything builds with the .NET SDK.
- `build.ps1` builds and installs `%LOCALAPPDATA%\Programs\KiteWin\KiteWin.exe` with a Start menu entry.
- `Icon/AppIcon.svg` the app icon. `build.ps1` renders it with `tools/make-icon.ps1`; `tools/make-icon.ps1 -Review` writes every size on light and dark into one PNG.
- `docs/` the brief, the screen index, and the user stories, when they exist.

## Build and check

- Build and install: `./build.ps1`. Add `-Open` to start the new build, `-Dev` for a faster debug build. The build shows warnings; fix them.
- Unit tests: `dotnet test tests/KiteWin.Core.Tests`.
- Self-test (the app's real functions on a seeded `%TEMP%` store): `tools/selftest.ps1`. Exit 0 when every check passes. Add a check for every story with logic in `SelfTest.cs`.
- After every UI change, take snapshots with sample data and look at them: `tools/snap.ps1`.
  It writes `%TEMP%\KiteWin-shots-<checkout>\<page>-light.png` and `-dark.png` for every navigation page and Settings, then quits. `tools/snap.ps1 -Pages all,settings` limits the pages.
  For a dialog, flyout, or second window, add a capture to `Snapshot.cs` like the page loop.
- In a git worktree, or with other agents building at the same time: `tools/dev.ps1 build|selftest|snap` builds this checkout without installing anything. Each checkout has its own build, test store, and snapshot folders.
- The app writes agent output (PASS lines, snapshot paths, crashes) to the file in `APP_LOG`; the tools print it. A WinUI app has no dependable stdout.
- Snapshots, seed data, and the self-test refuse to run without `APP_STORE` in `%TEMP%`.
- Never test against the real database in `%LOCALAPPDATA%\KiteWin\`.
- Then check the grid: `tools/grid-lint.ps1` and `tools/check-layout.ps1`. Both print PASS.
- A change is done when the build has no errors or warnings, the grid checks pass, and you looked at the light and dark snapshots.
- A build for other PCs: `tools/publish.ps1` (self-contained) or `tools/publish.ps1 -Aot` (NativeAOT, starts about 2.5x faster). It fails on trim warnings and runs the self-test on the result. JSON goes through `AppJson` (source generated), never reflection.

## Design rules

- Use WinUI's controls and theme resources. Only the accent is custom: `UI/Theme.cs` (`Theme.DefaultId` follows the Windows accent). People change it, and the app theme, in Settings.
- Colors come from theme brushes: `TextFillColorPrimaryBrush`, `TextFillColorSecondaryBrush`, `CardBackgroundFillColorDefaultBrush`, `AccentFillColorDefaultBrush`, `AccentTextFillColorPrimaryBrush`. Never a hex color, a fixed gray, or white for text.
- Spacing comes from `UI/Space.xaml` (`SpaceSm`, `SpaceLg`, `PageMargin`) in XAML and `Space` in code, on a 4 epx grid. Mark custom pages and cards with `ui:GridArea` and `ui:GridProbe`.
- The window: Mica backdrop, the `TitleBar` control with the pane toggle, a left `NavigationView` with Settings at the bottom. Pages open with a title in `TitleTextBlockStyle` and their commands in a `CommandBar` on the right.
- Settings is one page of `SettingsCard`s under group headers, like Windows Settings.
- Every command has a `KeyboardAccelerator` and a label; the self-test checks it. Delete and arrow keys are scoped to the list (`ScopeOwner`), so they never fire in a text box.
- Labels and buttons use sentence case: "New item", not "New Item".
- `FontIcon` with Segoe Fluent Icons glyphs for icons.
- Empty states: a glyph, one line on what the page is for, and the first action.
- Windows open calm: no text box has focus at launch. A new item focuses its title field.

## Skills

The bawa plugin has a skill for each step: `windows-design` (design rules),
`design-system` (accent and tokens), `layout-grid`, `ux-design`,
`csharp-winui`, `os-integrations`, `performance`, `build-and-verify`,
`test-windows-app`, `app-icon`, `user-stories`, and `improve-loop`. Use them.

## Git

- Commit after each working change, with a message that says what the user can now do.
