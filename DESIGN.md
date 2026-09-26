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
free-running beat coroutine. GPU vertex deformation animates explicitly authored
particle anatomy; URP HDR bloom integrates light. Target identity and queued
damage are independent of the renderer. No old scenes, code, VFX graphs or sound
assets are referenced. Original artwork and score only; this is an interpretation
of Area X's audiovisual design, not a distribution of the original game's assets.

## Boundaries

Score / MusicTransport own musical time. Encounter owns target lifecycle and
combat. ParticleWorld owns visual geometry. Flight owns camera and movement.
Hud draws interface only. RuntimeProof drives the same combat API as human input.
Editor/Production creates the reproducible scene, rendering assets and player.
