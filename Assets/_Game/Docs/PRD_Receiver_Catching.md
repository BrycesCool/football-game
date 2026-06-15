# PRD — Receiver Catching & Throw Placement

Version 1.0 · Owner: Bryce · Scope: BallLauncher, BallisticsSolver, WRController, CatchZone, Football, MatchRules

## 1. Problem

Two related failures make completions look fake:

**P1 — Ball placement.** The throw frequently does not arrive where the receiver is actually running. The lead solve happens once at release against the route prediction, then noise is added and deep throws are shortened along the line. The receiver, meanwhile, switches to pursuit and may never reach that point. Result: ball and receiver converge on different spots.

**P2 — Teleport catch.** The catch is decided by the ball entering a 1.1 m trigger sphere — more than an arm's length from the body. When the roll succeeds, the ball is snatched out of the air a meter from the hands and attached (the 0.08 s lerp is too short and too far to read as a catch). The ball visibly jumps from its flight path into the chest.

The core rule this PRD establishes: **the ball never changes course; the receiver does.** A catch happens because the receiver's hands and the ball occupy the same place at the same time — never because a trigger fired somewhere near him.

## 2. Goals

A pass thrown to a receiver in stride arrives chest-high on his path so he runs through it. The ball stays on pure ballistic flight until the frame hands meet it. Catchable vs. uncatchable is determined by whether the receiver can physically get his hands to the ball's arrival point in time — and an uncatchable ball simply sails or is dropped, never magnetized.

Non-goals (v1): hand IK to the ball, diving catch animations, contested hand-fighting animations, ball spin physics. Catch probability rolls (PRD §8.2) stay — this PRD changes *where and when* the roll applies, not the dice.

## 3. Definitions

**Arrival point (A):** the position where the ball's flight crosses catch height (chest, 1.3 m) on its descent — computed from live ball velocity, not the original throw target.
**Reach envelope:** a sphere of radius `catchReach` (default **0.55 m**) centered on the receiver's chest. This replaces the 1.1 m catch sphere as the thing that secures a ball. (A receiver's standing reach with extended arms; anything outside it is not in his hands.)
**Catch window:** the time interval the ball spends inside the reach envelope. With a 19 m/s bullet this is short (~60 ms) — all timing rules below must work at that speed (50 Hz physics → keep the detection sphere at 1.1 m as an *anticipation* zone, see R4).

## 4. Requirements

**R1 — Throw placement on the path.** The lead solve must target a point ON the receiver's future route path at chest height (it does — `RoutePath.PredictPosition` walks the same accel-tuned motion as the runner; this consistency is mandatory and must be covered by a test whenever motion tuning changes). Accuracy noise must be applied **along the path tangent** (early/late), not as a horizontal disc: a quarterback misses in timing far more than sideways, and an on-path miss is still runnable. Lateral noise is capped at 30 % of `inaccuracyRadius`.

**R2 — Receiver converges on the arrival point, not the intent point.** From the moment of the throw, the receiver's pursuit target is the live-projected arrival point A (recomputed each frame from ball position/velocity — already implemented in `ProjectBallLanding`). The original `targetPoint` is only a seed for the first 20 % of flight. The receiver must arrive at A before the ball does whenever physically possible (arrive-brake only when he beats the ball there by >0.15 s; otherwise run through in stride).

**R3 — Catchability test replaces the magnet.** When the ball is one physics step from crossing catch height, compute the distance between the receiver's chest and A. The pass is *catchable* only if that distance ≤ `catchReach` + a small in-stride allowance (`catchReachStride`, default 0.25 m, applied only when the receiver's velocity points within 45° of A). If catchable → run the existing P(catch) roll. If not catchable → no roll, no attach: the ball continues its flight and resolves INCOMPLETE on ground contact. The receiver plays it out (slows, turns to the ball).

**R4 — Two-zone detection.** Keep the existing 1.1 m CatchZone as the **anticipation zone**: entering it triggers the Catch animation (arms up) and arms the catch logic. The **securing** of the ball requires the R3 reach test. This preserves reliable trigger detection at bullet speeds while ending the meter-away snatch.

**R5 — No teleport, ever.** The ball remains a ballistic projectile until the securing frame. On securing: the gap between ball and hand socket must be ≤ `maxSecureGap` (default 0.35 m). The attach lerp closes that gap over `catchAttachLerp` (raise default to **0.12 s**) while the ball keeps its flight orientation blending to hand orientation. If an animation event (`HandsOnBall`) fires within the window, secure on the event frame; otherwise secure at closest approach inside the reach envelope (fallback unchanged). It is acceptable — and desirable — for the ball to pass through hands-height one frame before sticking; it is never acceptable for the ball to translate against its velocity direction into the chest.

**R6 — Drops and deflections.** A failed roll inside the reach envelope deflects off the hands from the **ball's actual position** with velocity derived from incoming velocity (damped, perturbed), not a random pop — the current `DeflectOffHands` impulse must take `0.25 × incoming velocity` as its base. Uncatchable balls (R3 fail) are not "drops" in stats; they resolve INCOMPLETE without the Drop trigger.

**R7 — Catch-in-stride preserved.** When the receiver runs through A at speed, the catch must not halt him: the Catch one-shot stays upper-body, locomotion continues, and RAC begins with his current velocity (already supported by the locomotion handoff — must not regress).

## 5. Technical notes (mapping to current code)

`BallLauncher.LaunchNow`: rotate the noise sample into the path-tangent frame (R1); tangent = normalized `predict(T+0.1) − predict(T−0.1)`. The deep-throw shortfall clamp stays, but the shortened point must be re-projected onto the route path's nearest point so a shortened deep ball is still in front of the receiver, not beside him.

`WRController`: TickPursuit already tracks `ProjectBallLanding`; change the seed-blend from full-flight lerp to the first 20 % of flight only (R2). `HandleBallEnter` becomes the anticipation hook (trigger "Catch", arm pending state); move the P(catch) roll + resolver report into a per-frame check that runs while the ball is inside the anticipation zone: when the R3 catchability test passes (ball within reach or crossing catch height this step) → roll → report. The pending `HandsOnBall`/fallback securing flow stays as the final attach step with the R5 gap check.

`Football.DeflectOffHands(Vector3 incomingVelocity)`: new signature per R6.

New MatchRules fields: `catchReach = 0.55`, `catchReachStride = 0.25`, `maxSecureGap = 0.35`, `noiseLateralFactor = 0.3`; change `catchAttachLerp = 0.12`. The 1.1 m `catchRadius` is renamed in intent only (anticipation zone) — value unchanged.

## 6. Acceptance criteria

1. Slant, in stride, no noise (set radii to 0): ball arrives within 0.3 m of the receiver's chest path at catch height on 20/20 throws; receiver never stops before the catch.
2. Frame-step a completion in the editor: between release and secure, the ball's position delta never opposes its velocity (no backward/inward jump > 2 cm). Secure gap ≤ 0.35 m on every completion.
3. Throw deliberately mistimed (force `tMax` clamp on a Go route): ball lands ahead/behind ON the route line, receiver chases through A, and if his chest is > 0.8 m from A at arrival the play is INCOMPLETE with no Drop trigger and no roll.
4. Curl (hold route): receiver settles under A with arrive-braking, ball secures within the reach envelope, no visible teleport at any camera angle.
5. Existing edit-mode tests still pass; add one: `PredictPosition` vs `Advance` parity with the tuned (non-Instant) MotionTuning, ±0.05 m.

## 7. Out of scope / later

Hand IK pulling the palms to the ball's actual entry point; high-point jumps on lobs (`highBall` currently only penalizes the roll); diving catches at the edge of the reach envelope; defender hand-fighting through the catch window.
