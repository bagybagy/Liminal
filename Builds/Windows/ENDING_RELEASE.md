# LIMINAL v0.1.14 / Conditional Endings

## Ending Selection

The whale freezes `RunProgress.EndingMask` on defeat. Count distinct optional
boss IDs, not visits, hits, repeated defeats, or the whale itself.

| Optional bosses defeated | Recording | Runtime theme |
| --- | --- | --- |
| 0 | Still Light | 5 |
| 1 | Ao No Yohaku / 青の余白 | 6 |
| 2 | Hitoiki No Uta / ひと息のうた | 7 |
| 3 | Mwanga wa Bahari | 8 |

`Assets/Liminal/Resources/StageAudio/EndingTracks.json` documents the approved
source hashes, generation models, audio processing, and measured timeline
estimates. The old Ending_BreathingLine is archived outside Unity Resources in
`MusicReview/Archive-EndingBreathingLine` and is not selectable or packaged.

The original ending recordings keep their full duration and tail. No tempo
stretch, pitch shift, recomposition, or three-minute audio crop is applied.
Playback starts at an outgoing four-bar boundary, with the existing smooth
equal-power crossfade lasting eight incoming measured beats. Ending sources do
not loop, and their musical phase stops at the original recording's end.
The three-minute roll completes before the final musical tail; free flight
continues afterward. Beat and harmony analysis is an estimate, not a verified
human score or a guarantee of sample-perfect musical interpretation.

The first cave and whale still use TidalMemory. Serpent and hermit keep their
approved harp bank and folded note order; only the hermit retains its approved
+3 dB SE gain. Submarine keeps its existing instrument.

## Credits

The roll lasts 180 seconds independently of recording length. It includes tete,
music, actual rendering/audio/PCVR technologies, and named contributors recovered
from the LIMINAL parent session and its worker records. `CONTRIBUTORS.md` and
`ProductionCredits.json` preserve minimal evidence and any recovery limitations.
Private conversations and machine-specific paths are not published.

Several rows form simultaneously. Particle identities are reused as glyphs
gather, rise, disperse, and return to the next line. Desktop adds the same roll
on the right; PCVR retains only world-space credits. Pausing suspends progression.

## Full-Clear Celebration

Only the all-three-optional-boss mask enables extra peaceful fauna. Fish inhabit
the land above and around the continent, rather than merging into temple roofs.
Extra fauna have no combat targets, HP, attacks, or boss flags. Desktop and VR
use different prebuilt meshes, with additional budgets of 50,000 and 20,000
points respectively. Existing city brightness and Bloom are not increased.

The desktop full-clear scene has 2,352 fish in total (784 existing plus 1,568
added), 48 small peaceful hermit crabs and 28 pufferfish. The additional meshes
contain 19,840 points on desktop and 13,616 in VR. Crabs follow terrain height
with alternating limb motion; fish use the established circling-school paths.
No additional independent serpent or separate tornado creature is introduced
in this release. Point budgets bound added geometry, but do not substitute for
a physical-headset frame-time measurement.

The recovered roster contains 75 assignment-backed participants. Reported task
status is 69 completed, two stopped and four unreported; this is participation
credit, not a claim that every prototype shipped. The roll contains 165 lines,
uses 6,325 pooled points, and displays roughly eight concurrent formed rows.

## Release Acceptance

`StageAudioReport.json` verifies the four ending recordings, all eight masks,
sample metadata, non-looping playback, DSP transitions, fades, and unchanged
approved stage instruments. `EndingReleaseReport.json` verifies finale layers,
fauna density switching, stable credit pools, all rows, reset, and 180-second
completion. Silent batch-mode captures are retained in Devlog. These checks do
not claim a new physical headset or audible mixing review.
