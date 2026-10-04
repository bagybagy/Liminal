# Finale Visibility and VR Lock Feedback Hotfix

The full-clear ending mask was reaching Atlantis correctly. The previous fauna
meshes were active, but extra fish used only eleven dim points and hermit crabs
only thirty-seven points at a roughly two-metre span. The population diagnostics
therefore passed without establishing recognisable on-screen creatures.

The hotfix reuses the encounter's helical shell and jointed leg geometry for
peaceful hermits, increases readable fish silhouettes, and adjusts local particle
colour, radiance and minimum pixel coverage. It does not raise scene-wide Bloom.
Extra fauna remain exclusive to full completion, non-combatant, and bounded to
48,544 desktop points or 19,488 VR points.

VR acquired-target rings were removed along with ordinary gameplay HUD. Their
world-space rendering is restored independently of HP, counters and other HUD.
Passage labels and pause controls are preserved.

Acceptance uses silent offscreen rendering, comparing identical camera frames
with and without the actual fauna renderers. Desktop and reduced-density meshes
must both make measurable visible contributions. Lock feedback is tested across
acquisition, multi-lock, release, pause and VR exit. Captures do not claim a
physical Quest headset review.
