# Faithful SoloStone iOS port: scope and current evidence

## Correct target

Port the work in the upstream repository: SoloStone's plugin, rules, offline client-message integration,
NPC behavior, profiles, collection/deck building, packs, economy, ladder and progression, retaining the
Hearthstone presentation and assets. Adapt the actual game interface to iPhone and iPad dimensions.
The standalone text interface under `mobile/` is an engine experiment and does not satisfy this target.
Its successful compilation does not establish that the actual SoloStone plugin runs on iOS.

## Established from the checked-out source

- `plugin/OfflineProbe.csproj` targets .NET Framework 4.8 and references proprietary Hearthstone and
  Unity managed assemblies plus BepInEx and Harmony. These reference assemblies are not in this repository.
- `plugin/OfflineProbe.cs` loads inside the client, installs Harmony patches, and currently uses Windows
  identity/process assumptions. `plugin/OfflineGuard.cs` includes Windows token APIs.
- `plugin/ClientChecks.cs` checks the running client's assemblies, types and methods, then checks its
  card records before releasing saves. `plugin/client-bindings.txt` contains 3,496 non-comment rows.
  These are bindings of several kinds, not 3,496 distinct patches.
- `installer/tested-client.json` identifies the tested Windows client as 37.0.0 build 254477.
  That is evidence about the Windows target, not proof that an equivalent iOS build is available.
- `installer/loader-inputs.json` pins Windows x64 Doorstop 4.5.0, BepInEx 5.4.23.5 and a Windows Mono
  class-library set. These are not iOS loader components.
- The repository supplies neither the full Hearthstone Unity project nor its client binaries, art or sound.
  An iOS export of the complete original game cannot be built from this repository alone.

## Platform evidence and limits

BepInEx's official installation page lists Windows, Linux and macOS, and separate Mono/IL2CPP paths;
it does not establish an iOS-supported loader for this mod:
https://docs.bepinex.dev/master/articles/user_guide/installation/

Unity documents iOS/AOT restrictions and the role of IL2CPP:
https://docs.unity.com/en-us/engine/6000.0/manual/programming-environment/scripting-backends/il2cpp/scripting-restrictions

Inference: the Windows loader/patch arrangement cannot be treated as a drop-in iOS dependency.
The actual Hearthstone iOS runtime, executable layout, exports, metadata, patchability and asset format
must be established from an identified client. No such client has been inspected in this investigation.
There is no verified iOS hook/bootstrap implementation, matching binding map, or in-client test result yet.
This does not establish impossibility; it means feasibility remains unproven.

## Next evidence needed

A locally supplied, legitimately obtained Hearthstone iOS package and its downloaded game data, with
their version/build identified. First perform read-only package/runtime inventory: version, architecture,
Unity/runtime identity, code/metadata availability, encryption state and asset locations. Do not assume
the Windows version's assemblies or assets can substitute for these inputs.

Then prove the smallest in-client integration before porting the rest: load the local backend, establish
client-compatible messages, display an offline menu and complete an offline match in the real interface.
Preserve the upstream isolation and save-validation intent when replacing Windows-specific mechanisms.
Keep all proprietary client material out of this public repository and any public CI artifacts.

Only after that proof should collection/progression, packs, NPC parity, saves and native phone/tablet
layout acceptance be called an iOS SoloStone port. Adding artwork to the existing text interface would
not by itself establish this integration.

## Local availability

A filename search of the Games workspace did not locate a Hearthstone IPA/client. The two standard
Windows Hearthstone installation paths checked were absent. This is a limited local search, not proof
that no client exists elsewhere on the machine or on the user's devices.
