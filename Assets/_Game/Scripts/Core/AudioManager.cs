using UnityEngine;

namespace Gridiron
{
    /// <summary>SFX stub (PRD §13: no sourcing in v1). Hook points exist; clips can be assigned later.</summary>
    public class AudioManager : MonoBehaviour
    {
        public AudioSource source;
        public AudioClip snap;
        public AudioClip throwBall;
        public AudioClip catchBall;
        public AudioClip whistle;
        public AudioClip crowdTD;

        void Start()
        {
            var mm = MatchManager.Instance;
            if (mm == null) return;
            mm.OnSnap += () => Play(snap);
            mm.OnThrow += _ => Play(throwBall);
            mm.OnPlayResolved += (r, t, s) =>
            {
                if (r == PlayResult.Touchdown) Play(crowdTD);
                else if (r == PlayResult.Catch) Play(catchBall);
                else Play(whistle);
            };
        }

        void Play(AudioClip clip)
        {
            if (clip != null && source != null) source.PlayOneShot(clip);
        }
    }
}