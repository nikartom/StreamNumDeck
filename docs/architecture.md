# Architecture

## Goals

StreamNumDeck must remain responsive while it globally observes keyboard input,
executes user actions, plays audio and maintains an OBS WebSocket connection.
The low-level keyboard callback must never perform file, network, audio or UI
work directly.

## Dependency direction

```text
StreamNumDeck.Wpf ──────────────┐
        │                       │
        ▼                       ▼
StreamNumDeck.Infrastructure → StreamNumDeck.Core
```

`StreamNumDeck.Core` has no dependency on WPF, Win32, audio libraries or OBS
client implementations. Infrastructure implements contracts owned by Core. The
WPF project composes services and owns presentation concerns only.

## Planned modules

### Core

- Profiles, layers, physical key identifiers and action definitions.
- Validation and schema-version migration contracts.
- Action execution pipeline contracts.
- Audio, OBS, process-launch and keyboard-capture interfaces.

### Infrastructure

- Atomic JSON persistence with backups and explicit migrations.
- Windows low-level keyboard hook isolated on a dedicated message-loop thread.
- Bounded channel between the hook and the action dispatcher.
- Audio playback engine and device enumeration.
- OBS WebSocket client with reconnect and observable connection state.
- Secure secret storage for the OBS password.

Audio playback uses the native Windows `AudioGraph` pipeline with a shared
low-latency graph per selected render device. Every file receives its own input
node so overlap, same-file restart and stop-others policies remain explicit.
The graph is recreated after an unrecoverable endpoint error or a default-device
change.

Configuration files live under `%LOCALAPPDATA%\StreamNumDeck\Configuration`.
Portable and installer builds therefore use the same predictable storage location.
OBS passwords are never written to the JSON document; only a secure credential
identifier is stored.

### WPF desktop application

- WPF windows, dialogs and view models.
- Dependency injection and application lifetime.
- Tray integration, notifications and error presentation.

## Keyboard invariants

- Physical keys are identified by scan code and extended-key information, not
  by the character produced by the current keyboard layout.
- NumLock is the layer selector and is not an ordinary action key.
- Both layers contain assignments for the other 22 keys, for 44 action slots.
- Injected input is tagged and never recursively processed as physical input.
- The hook callback performs constant-time classification and queueing only.
- `Ctrl+Alt+F12` toggles an emergency pause independently of user profiles.
- The native hook owns a dedicated Win32 message-loop thread and writes only to
  a bounded channel; configuration lookup and action execution happen on the
  channel consumer.

## Reliability rules

- User configuration writes are atomic and retain a last-known-good backup.
- Stored data is versioned; incompatible data is never silently discarded.
- External operations support cancellation and return structured failures.
- OBS and audio outages do not terminate the keyboard-capture process.
- No exception may escape a native callback boundary.
- Long-running work never executes on the UI thread.

## Testing strategy

- Core models and migrations are covered by deterministic unit tests.
- Infrastructure adapters are tested behind contracts where practical.
- Win32 hook translation is tested separately from hook installation.
- OBS protocol behavior is tested against recorded protocol messages and a
  controllable local test server.
- Every delivery stage ends with restore, build, tests and a launch smoke test.
