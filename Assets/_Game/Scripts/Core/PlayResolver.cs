using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Single source of truth for play outcomes (PRD §9). All resolution reports are buffered
    /// within the frame and the winner is picked by precedence (INTERCEPT &gt; SWAT &gt; CATCH &gt; GROUND)
    /// in LateUpdate, so simultaneous same-tick events resolve correctly. Physical consequences
    /// (parenting the ball, swat impulse) are applied only for the winning event.
    /// </summary>
    public class PlayResolver : MonoBehaviour
    {
        struct Pending
        {
            public ResolutionEventType type;
            public Vector3 spot;
            public bool inBounds;
            public bool inEndZone;
            public Action apply;   // physical consequence, executed only if this event wins
        }

        readonly List<Pending> pending = new List<Pending>();
        bool resolvedThisPlay;

        public bool ResolvedThisPlay => resolvedThisPlay;

        public void BeginPlay()
        {
            pending.Clear();
            resolvedThisPlay = false;
        }

        public void ReportCatch(Vector3 spot, bool inBounds, bool inEndZone, Action apply)
        {
            Add(new Pending { type = ResolutionEventType.Catch, spot = spot, inBounds = inBounds, inEndZone = inEndZone, apply = apply });
        }

        public void ReportSwat(Vector3 spot, Action apply)
        {
            Add(new Pending { type = ResolutionEventType.Swat, spot = spot, apply = apply });
        }

        public void ReportIntercept(Vector3 spot, Action apply)
        {
            Add(new Pending { type = ResolutionEventType.Intercept, spot = spot, apply = apply });
        }

        public void ReportGround(Vector3 spot)
        {
            Add(new Pending { type = ResolutionEventType.Ground, spot = spot });
        }

        /// <summary>Failed WR catch roll — drop, resolves INCOMPLETE (§8.2). Ground precedence so a same-tick swat outranks it.</summary>
        public void ReportDrop(Vector3 spot, Action apply)
        {
            Add(new Pending { type = ResolutionEventType.Ground, spot = spot, apply = apply });
        }

        void Add(Pending p)
        {
            if (resolvedThisPlay) return;
            pending.Add(p);
        }

        void LateUpdate()
        {
            if (resolvedThisPlay || pending.Count == 0) return;

            var types = new List<ResolutionEventType>(pending.Count);
            for (int i = 0; i < pending.Count; i++) types.Add(pending[i].type);
            int winner = ResolutionLogic.PickWinner(types);
            Pending w = pending[winner];
            pending.Clear();
            resolvedThisPlay = true;

            w.apply?.Invoke();

            var mm = MatchManager.Instance;
            switch (w.type)
            {
                case ResolutionEventType.Intercept:
                    mm.ResolvePlay(PlayResult.Intercept, w.spot);
                    break;
                case ResolutionEventType.Swat:
                    mm.ResolvePlay(PlayResult.Swat, w.spot);
                    break;
                case ResolutionEventType.Catch:
                    if (!w.inBounds) mm.ResolvePlay(PlayResult.OutOfBounds, w.spot);
                    else if (w.inEndZone) mm.ResolvePlay(PlayResult.Touchdown, w.spot);
                    else mm.BeginRunAfterCatch(w.spot);
                    break;
                default:
                    mm.ResolvePlay(PlayResult.Incomplete, w.spot);
                    break;
            }
        }
    }
}