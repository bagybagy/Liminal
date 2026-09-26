# LIMINAL / ABYSSAL CHOIR

Fresh project. Unity 6000.3.13f1, Universal Render Pipeline. The repository-root
Unity project is a historical prototype, not a dependency of this project.

## Acceptance

- A playable, self-contained musical boss encounter, including defeat, victory,
  pause, restart, and a Windows executable.
- An original, authored 124 BPM score and a matching timeline share the audio DSP
  clock. Scheduled attack notes and logical impacts share the same timestamp.
- Sweeping locks across the creature and releasing creates up to eight curved
  homing rays. Moving targets cannot invalidate an accepted hit.
- A dark, deep environment; visible particulate anatomy rather than primitive
  meshes; bright but readable attacks, transformations, and dissipation.
- Runtime checks cover damage, phase transitions, complete playthrough, restart,
  scheduling error, exceptions, and screenshots from the built player.

## Decisions

The music and its event map are composed together. There is no BPM detector or
free-running beat coroutine. Persistent GPU particle state follows a shared spine;
a translucent membrane integrates the surface into a continuous volume.
Target identity and queued
damage are independent of the renderer. No old scenes, code, VFX graphs or sound
assets are referenced. Original artwork and score only; this is an interpretation
of Area X's audiovisual design, not a distribution of the original game's assets.

## Boundaries

Score / MusicTransport own musical time. Encounter owns target lifecycle and
combat. Anatomy owns the independent world-space head route and delayed spine.
ParticleWorld owns the environment; LeviathanVfx owns GPU buffers, particle lifetime,
curl advection and translucent surface rendering. Flight owns a separate moving rig,
never the root transform containing the environment. It preserves world orientation.
Hud draws interface only. RuntimeProof drives the same combat API as human input.
Editor/Production creates the reproducible scene, rendering assets and player.

## Free-Flight Revision

- Cruise 24 m/s, boost 52 m/s, persistent yaw/pitch, camera-relative translation.
- Soft outer arena at about 350 m, no small positional clamps or auto-recentering.
- The head travels roughly 200 m across the arena. Every vertebra samples the head
  route at an earlier musical time; turns propagate through the complete creature.
- New locks require 105 m range and front/frustum visibility. Existing locks and
  queued hits survive a range change. Acquisition radius is 9% of screen height,
  clamped to 64-110 pixels, and is represented in the reticle.
- 257 CPU spine samples are uploaded once per simulation step. Both target organs
  and GPU geometry use this anatomy. 262,144 persistent particles form four layers:
  skin flow, fins, interior illumination, and detached world-space wake.
- The custom compute approach keeps target anatomy and VFX on the same data contract.
  This implementation does not claim to contain a VFX Graph asset. Unity describes
  this URP compute-buffer rendering approach in its [compute shader recipe](https://learn.unity.com/tutorial/urp-recipe-compute-shaders).
- Runtime proof flies around and pursues the creature instead of keeping a static
  camera. It checks movement, range, retained locks, musical hits and full completion.
