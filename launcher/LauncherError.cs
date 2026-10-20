using System;

namespace VHOLTAT.Launcher
{
    /// <summary>A problem the user can fix, with a one-line suggestion. Shown in the status line, the log and the CLI.</summary>
    public class LauncherError : Exception
    {
        public LauncherError(string problem, string fix, Exception inner = null) : base(problem, inner)
        {
            Fix = fix;
        }

        public string Fix { get; }

        public string OneLine => string.IsNullOrEmpty(Fix) ? Message : $"{Message} {Fix}";
    }

    /// <summary>STRAFTAT is running from the folder the launcher would change or start.</summary>
    public sealed class GameRunningError : LauncherError
    {
        public GameRunningError() : base("STRAFTAT is running.", "Close the game, then try again.") { }
    }

    /// <summary>Steam is installed but not running, so Play can't hand STRAFTAT to it.</summary>
    public sealed class SteamNotRunningError : LauncherError
    {
        public SteamNotRunningError() : base("Steam isn't running.", "Start Steam and sign in, then try Play again.") { }
    }
}
