# DEVIATIONS.md — implementation deviations from PRD v1.0

Per PRD closing note: 'where this doc is silent, prefer the simplest implementation that satisfies
section 16, and log deviations'.

1. **Menus**: built as UI panels inside Main.unity instead of a separate Menu.unity. Simplest flow
   that satisfies section 11.4/16; MenuController is self-contained if a split is wanted later.
2. **Same-tick precedence (section 9)**: resolution reports are buffered per frame and the winner
   (INTERCEPT > SWAT > CATCH > GROUND) is applied in PlayResolver.LateUpdate. Physical consequences
   (ball parenting, swat impulse) only run for the winning event — one frame of deferral, invisible at 60 fps.
3. **RAC window**: after an in-bounds, non-end-zone catch the MatchManager runs the 1.0 s RAC
   coroutine while the master state remains BALL_IN_AIR, then transitions to PLAY_RESULT with the
   final spot. Section 5.1 lists CATCH as an immediate resolution event; behavior follows 8.2/9.4.
4. **Deep-throw shortfall (section 7.3/7.4)**: when the lead solve clamps at Tmax and the target
   would need more horizontal speed than the throw type has, the ball is aimed short along the
   throw line (lands behind the WR). This implements the stated design intent that deep bullets
   are uncatchable; a literal reading of the formula would always deliver the ball.
5. **WR drop**: resolves INCOMPLETE at the moment of the failed catch roll (Ground precedence),
   not when the deflected ball lands.
6. **Speed multipliers**: a waypoint's speedMultiplier applies while pursuing that waypoint
   (slowing INTO the cut), documented in RouteDefinition.
7. **Prefabs**: Football saved as a prefab. QB/WR/CB are scene objects (heavily cross-wired refs);
   the scene builder (Gridiron > 3. Build Main Scene) regenerates them deterministically.
8. **UI**: HUD/play-select/banner/menus are generated at runtime from code (legacy uGUI Text) —
   no Sprite assets; play art is procedural textures (PlayArtGenerator). PlayDefinition.playArt
   exists for hand-authored art later. Mirror toggle flips card art via UV rect.
9. **OOB check**: capsule-center XZ at catch time per the v1 simplification in 8.2 (foot-bone
   check is a stretch goal).
10. **EndZoneTrigger** exists as a trigger volume per 4.3 but TD/OOB logic is purely mathematical
    via FieldBounds (also per 4.3).
11. **Out route**: authored exactly per 6.2 table; 'mirrored' lives on the play-select toggle
    (flips route + alignment X) rather than as a stored bool on each play asset.
12. **PlayerSettings.runInBackground = true** enabled so the editor keeps simulating without focus
    (testing convenience; harmless for the game).
13. **Cinemachine**: vcam motion is script-driven on the vcam transforms (CameraDirector) with the
    3.x CinemachineCamera/CinemachineBrain handling priority blends. No CinemachineFollow components —
    fewer moving parts, same section 11.1 behavior.

## Milestone status
M0–M5 complete and verified (scene, solver, routes, lead throws, CB AI, full loop, tests 23/23).
M6 (polish) complete to rough-draft level per the section 12.2 'substitutes only' directive.

## 2026-06-12 update
14. Stadium swapped to `Assets/Stadium/fsupastadium.fbx` (Y=90, exact regulation, verified
    X +/-24.38 / Z +/-54.86). importCameras/importLights disabled on both stadium FBXs — the old
    FBX-embedded camera was rendering over the game view (the 'glitching across the screen').
15. Movement hardening: Time.maximumDeltaTime=0.05, per-controller dt clamps (0.04), CB arrive
    easing at the cover point, smoothed CB read of WR direction, ball trail cleared on re-attach.
