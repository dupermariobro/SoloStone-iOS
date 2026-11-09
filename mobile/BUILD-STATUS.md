# Build status — 2026-11-09

This is an initial standalone practice client, not a feature-complete port of the Windows mod.

## Passed

- Windows engine adapter: 256 assertions, all 56 admitted deck openings, a complete 127-move match,
  sideboards, rejected/stale input, opponent-hand filtering, snapshot roundtrips and cold host resume.
- The same engine tests on the macOS CI runner.
- Native iOS simulator application compilation with Xcode 26.0.1 / .NET 10.0.402.
- ARM64 Release device application compilation and unsigned IPA packaging.
- IPA inspection: iPhone and iPad device families, bundled local UI, no provisioning profile or signature.
- Live browser preview: start game, mulligan keep list, play Righteous Protector, end turn and bot response.
- Layout geometry checks: 320×568, 375×667, 390×844, 430×932, 568×320, 768×1024,
  834×1194, 1024×1366, 1194×834 and 932×430. Page width and height fit; footer controls remain reachable.
- Visual inspection at phone portrait, iPad portrait and compact landscape. Short screens scroll the arena.

## Not passed / not completed

The [CI run](https://github.com/dupermariobro/SoloStone-iOS/actions/runs/42622414323) is marked **failed** because
`simctl bootstatus` exceeded 240 seconds, followed by a shutdown timeout. This happened after the native
builds and packaging passed. The upload step preserved the simulator app, unsigned IPA and corresponding
source. No native screenshots were retained in this run. Browser screenshots validate the shared UI only;
they are not screenshots from Apple hardware. No physical-device gameplay or Release runtime validation
has been performed. Accessibility, exhaustive card interactions and all safe-area/device combinations
remain to be checked.

## Final local package

The final `mobile/out/SoloStone-iOS-Alpha-Unsigned.ipa` uses the compiled executable from that CI run, with
the final three bundled UI assets refreshed from `mobile/Web/`. These refinements keep board minions and
hand cards from stretching on tall iPads, remove formatting markers in card text, and hide spurious board
scrollbars. Native executable bytes and all other bundle entries are preserved and checked byte-for-byte.
The adjacent `.manifest.json` records the SHA-256 hashes and base build URL.

Reproduce the asset refresh after downloading the original unsigned CI artifact:

```sh
python3 mobile/refresh-ipa-assets.py path/to/SoloStone-Unsigned.ipa path/to/SoloStone-iOS-Alpha-Unsigned.ipa
```

The app still needs signing before installation. The fresh-source build path bundles these UI files directly
and does not need this refresh step. Builds and source remain available in this public fork; local `.tools/`
credentials and downloaded SDKs are excluded from Git and the source archives.
