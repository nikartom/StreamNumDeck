# Delivery roadmap

Each stage ends in a buildable application and has explicit acceptance criteria.
A later stage does not compensate for an unstable earlier one.

## Stage 1 — Foundation and native shell

Status: complete.

- Pin the .NET and NuGet toolchain.
- Create Core, Infrastructure, WPF and test projects.
- Establish dependency direction and architecture rules.
- Build the WPF desktop shell, navigation and global settings.
- Render the six-key block above the numeric keypad.
- Represent both NumLock editing layers in the interface.
- Verify restore, zero-warning build, tests and packaged debug launch.

## Stage 2 — Domain model and persistence

Status: complete.

- Define the complete physical-key set and two-layer invariants.
- Define profiles, assignments, icon references and typed action definitions.
- Validate models at construction boundaries.
- Store versioned configuration as atomic JSON with backup recovery.
- Add unit tests for defaults, validation, serialization and recovery.
- Bind the shell to persisted profiles and global settings.

## Stage 3 — Key and action editor

Status: complete.

- Open a key editor from the deck.
- Select built-in icons or import a user image.
- Select an action group, action type and strongly typed parameters.
- Validate file paths and action-specific settings.
- Save, cancel, reset and copy assignments between layers.

## Stage 4 — Keyboard capture and action execution

Status: complete.

- Install and remove `WH_KEYBOARD_LL` safely.
- Translate scan codes into the domain key identifiers.
- Track NumLock state and suppress configured physical keys.
- Dispatch actions outside the native callback.
- Implement emergency pause and injected-input protection.
- Add process, URL, file and keyboard-sequence actions.

## Stage 5 — Audio engine

Status: complete.

- Enumerate Windows output devices.
- Play supported files with low start latency.
- Apply global and per-action volume.
- Define overlap, restart and stop-all behavior.
- Recover from device removal and default-device changes.

## Stage 6 — OBS integration

Status: complete.

- Connect to obs-websocket 5.x with protected credentials.
- Reconnect without blocking the UI.
- Discover scenes and sources for editor lists.
- Execute scene, source, audio, recording and streaming actions.
- Surface OBS state and structured errors in the application.

## Stage 7 — Stabilization and distribution

Status: in progress.

- Add tray lifecycle and startup behavior. Complete.
- Apply system, light and dark themes at runtime. Complete.
- Add profile duplication and guarded deletion. Complete.
- Surface background action failures in the main interface. Complete.
- Complete accessibility, localization and keyboard navigation.
- Run long-duration and rapid-input tests.
- Produce portable ZIP and Inno Setup packages and verify upgrades.
- Finalize user and developer documentation.
