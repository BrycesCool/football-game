# TODO_ANIMS.md — animation substitutions (PRD 12.2, v1 rough draft)

All gameplay logic keys off animation EVENTS (ReleaseBall / HandsOnBall / SwatContact / PlantFoot),
never normalized time. To upgrade a clip later: replace the Motion in the Animator state, keep the
state name and re-add the event. No code changes required.

## Substitutions in use
| Need | v1 substitute | Where | Event |
|---|---|---|---|
| QB throw (bullet AND lob) | `QB Pass Throw` sub-clip of `Quarterback Pass` (frames 30-66) | QB.controller > Actions > ThrowState | ReleaseBall @ 0.30 normalized |
| QB shotgun snap-catch | `Football Catch` on upper-body Actions layer | QB.controller > CatchState | — |
| WR hard plant/cut | `Running Right Turn` | WR.controller > PlantState | PlantFoot @ 0.25 |
| WR standing catch | `Football Catch` | WR.controller > CatchState | HandsOnBall @ 0.30 |
| WR over-shoulder / moving catch | SKIPPED — standing catch plays | — | — |
| WR jumping catch | SKIPPED — standing catch plays; highBallPenalty still applies | — | — |
| WR diving catch | SKIPPED — standing catch plays | — | — |
| WR drop/bobble | No clip — physics deflect impulse sells it; cross-fade to idle | WRController.HandleBallEnter | — |
| CB swat | `Swat` sub-clip = first 0.4 s of `Receiver Catch` (arm raise) | CB.controller > SwatState | SwatContact @ 0.75 |
| CB intercept | `Receiver Catch` | CB.controller > CatchState | HandsOnBall @ 0.30 |
| CB hip-flip | Cross-fade Backpedal -> Locomotion (0.15 s) | CB.controller base layer | — |

## Needs review when real clips arrive
1. **`QB Pass Throw` trim window is a first-pass guess** (frames 30-66 of a 7.67 s clip, 30 fps).
   Scrub `Quarterback Pass` and move firstFrame/lastFrame so the release pose lands at the
   ReleaseBall event. BallLauncher also has a 0.35 s fallback launch (MatchRules.throwReleaseFallback),
   so gameplay is correct even if the event timing is off.
2. `Football Catch (1).fbx` is a duplicate — ignored (safe to delete).
3. Upper-body mask (`Assets/_Game/Art/UpperBody.mask`) powers all action-layer substitutes.
4. Wanted clips for the upgrade pass: lob throw variant, over-shoulder catch, jumping catch,
   diving catch, drop/bobble, real swat, hip-flip turn, tackle/collision.

## 2026-06-12 update — root motion + Defender fix
- All clips re-imported with Root Transform Position (XZ) **Bake Into Pose OFF** (motion extracted
  to root curves and discarded via applyRootMotion=false). The previous baked setting made meshes
  lurch away from their roots and snap back each loop — this was the 'glitchy running'.
- `Defender` (3.6 s) turned out to be a dive-to-ground animation, not a stance. CB idle now uses a
  trimmed loop `Defender Stance` (frames 0-24). Review the window; if the first second still dips,
  swap CB idle to `Offensive Idle` in CB.controller.
