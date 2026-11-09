# SoloStone standalone iOS port — initial implementation

This adds an iPhone/iPad app that runs SoloStone's existing engine locally, with a new touch interface.
It does not load or patch the Hearthstone iOS client. Source is under `mobile/`; the upstream engine,
card facts and NPC deck data are reused directly. The upstream Windows plugin remains untouched.

## Current scope

- 56 admitted upstream decks, including their sideboard selections, selectable for either seat.
- Offline matches against a basic public-state heuristic bot.
- Engine-validated moves, explicit targets, minion positions, card choices and mulligan keep lists.
- Opponent hand contents excluded from UI responses.
- Snapshot autosave after each move and cold-start resume. Save errors appear on screen.
- Universal iPhone/iPad configuration, portrait and landscape, edge-to-edge background, safe-area controls.
- Layout uses the available viewport, not a fixed 16:9 canvas. Hand/board rows scroll when needed.
- WKWebView renders bundled local UI; C# runs all rules in-process. The iOS build uses no localhost
  server, remote webpage, account, injected game client, downloaded executable code or runtime JIT.
- Licenses and notices are readable in the app.

**This is not yet a device-validated port or a complete SoloStone feature port.** The original art,
audio and animations, progression, collection/deck editing, packs, shop, ranked ladder and advanced NPC
personalities/search are not implemented. Practice decks are unlocked; no rewards are awarded.
The original upstream project's legally obtained-client requirement and notices remain in the root docs;
this standalone implementation does not consume any client files.

## Validation performed on Windows

- Engine + adapter build: .NET SDK 10.0.402, Debug, zero compiler warnings/errors.
- 256 assertions: one complete 127-move match, all 56 deck openings, sideboards, snapshot roundtrips,
  cold host resume, private opponent hand filtering, stale/duplicate input rejection and invalid picks.
- Native wrapper C# compiled against Microsoft's iOS 26.0.11017 reference assembly, zero warnings/errors.
  This checks API signatures only; it is **not** an iOS workload/Xcode build.
- JavaScript syntax check passes.
- Visual and simulator/device validation is outstanding: browser automation could not open the local preview.

The tests exercise the adapter, not every card interaction. Upstream includes a 20-card Thief Priest deck;
it is preserved verbatim rather than padded with arbitrary cards.

## Build on a Mac

Install the exact .NET SDK pinned in `../global.json` (**10.0.402**) and compatible Xcode. Select that Xcode
with `xcode-select` if needed, then run from the repository root:

```sh
dotnet workload install ios --skip-manifest-update
dotnet run --project mobile/SoloStone.Mobile.Tests -p:GeneratePackageOnBuild=false
bash mobile/build-ios.sh simulator
```

The bundled SDK manifest uses iOS workload 26.0.11017. Match Xcode to the workload's stated requirement
if the build reports a version mismatch. Do not silently change the engine SDK pin to fix Xcode selection.
The manual GitHub Actions workflow at `.github/workflows/mobile-ios.yml` selects Xcode 26.0.1 on macOS 15,
builds the simulator app and unsigned device IPA, captures first-launch screenshots on an iPhone and iPad
simulator, and includes corresponding source. The unsigned IPA needs your own signing before installation.
It has not been run yet; execution requires GitHub authorization and a fork with the workflow enabled.

Find the `.app` under `mobile/SoloStone.iOS/bin/Debug/net10.0-ios/<simulator-rid>/`.
Boot an iPhone or iPad simulator, then install and launch it:

```sh
xcrun simctl install booted mobile/SoloStone.iOS/bin/Debug/net10.0-ios/iossimulator-arm64/SoloStone.iOS.app
xcrun simctl launch booted org.solostone.mobile
```

Use `iossimulator-x64` on an Intel Mac. For a signed device IPA, configure your own certificate/profile:

```sh
export SOLOSTONE_CODESIGN_KEY='Apple Development: Your Name (TEAMID)'
export SOLOSTONE_PROVISION='your installed profile UUID or name'
export SOLOSTONE_BUNDLE_ID='your.registered.bundle.id'
bash mobile/build-ios.sh device
```

The profile must include your target devices and match the bundle ID and signing identity. There is no
signed IPA in this checkout. App icon design, App Store metadata and distribution are still outstanding.
The conservative interpreter/no-trimming configuration preserves reflection-dependent engine handlers and
snapshot fields; measure startup, memory and move latency on real devices before changing it.

Build-property reference: https://learn.microsoft.com/dotnet/ios/building-apps/build-properties

## Local preview and tests

With .NET 10.0.402 on PATH, from the repository root:

```sh
dotnet run --project mobile/SoloStone.Preview -p:GeneratePackageOnBuild=false
```

Open `http://127.0.0.1:5187`. This development-only loopback host runs the real engine with the same UI and
bridge protocol as iOS. Preview saves are separate from the iOS sandbox. On Windows the source was built
using a workspace-local SDK under `.tools/dotnet/` (ignored by Git).

```powershell
$env:DOTNET_CLI_HOME = "$PWD\.tools\cli"
.\.tools\dotnet\dotnet.exe build mobile/SoloStone.Mobile.Tests -p:TargetFrameworks=net10.0 -p:NuGetAudit=false -p:GeneratePackageOnBuild=false -p:UseSharedCompilation=false -m:1
.\.tools\dotnet\dotnet.exe mobile/SoloStone.Mobile.Tests/bin/Debug/net10.0/SoloStone.Mobile.Tests.dll
```

## Required acceptance checks on Apple hardware

Verify home, mulligan, ten-card hand, full seven-minion boards, long card text and legal-move sheets at:
320×568, 375×667, 390×844, 430×932, 768×1024, 834×1194 and 1024×1366 logical points, plus landscape.
Use actual simulator/device sizes available in your Xcode version. Check notch/Dynamic Island/home-indicator
safe areas, rotation mid-match, app suspension/termination and cold resume, VoiceOver, enlarged text and
reduced motion. Complete matches on at least one phone and one iPad, including discover/targeted choices,
locations, hero powers and sideboard decks. Check Release builds as well as Debug.

## Source and notices

See `mobile/NOTICE.md`, root `NOTICE`, `LICENSE`, `LICENSE-ADDITIONAL-PERMISSION.md` and `LICENSES/`.
Preserve upstream notices and provide the complete modified corresponding source alongside distributed builds.
