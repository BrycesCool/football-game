namespace Gridiron
{
    /// <summary>Master game flow states (PRD §5). Only MatchManager may advance these.</summary>
    public enum GameState
    {
        Boot,
        MainMenu,
        MatchStart,
        PlaySelect,
        PreSnap,
        RouteRunning,
        BallInAir,
        PlayResult,
        MatchEnd
    }

    /// <summary>Outcome of a single snap (PRD §9).</summary>
    public enum PlayResult
    {
        None,
        Catch,
        Touchdown,
        Incomplete,
        Swat,
        Intercept,
        OutOfBounds,
        SackTimerExpired
    }

    public enum ThrowType { Bullet, Lob }

    /// <summary>UI-only hint on play cards (PRD §6.1).</summary>
    public enum ThrowProfileHint { BulletFriendly, LobFriendly, Either }
}