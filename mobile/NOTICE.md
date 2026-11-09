# Mobile port notice

New files under `mobile/` implement a standalone iOS host, responsive local touch interface,
engine adapter, loopback development preview, tests and build instructions. Added 2026-11-09.
They are licensed under GNU AGPL version 3 or later, like SoloStone's own code.
The upstream additional permission covers `plugin/` and `installer/` only; it does not cover this directory.

Upstream: https://github.com/KeyStarr/SoloStone
Rules engine: SoloStone's modified SabberStone; see the root NOTICE and LICENSES directory.
NPC deck data is reused unchanged from `data/npc/decks-v1.json`, under the upstream data terms.
Card names and text remain Blizzard's material, as described in the root NOTICE.
This standalone client includes no Hearthstone client binaries, artwork, sound, BepInEx or Harmony.

This is an initial practice-match port, not the complete Windows conversion. No claim of
Blizzard affiliation or endorsement is made. Preserve the upstream notices and provide this
modified source alongside any build you distribute.
