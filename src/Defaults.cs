namespace Dhikr
{
    /// <summary>
    /// Everything you might want to tweak lives here.
    /// To add a built-in dhikr, just add a line to <see cref="Adhkar"/>.
    /// </summary>
    public static class Defaults
    {
        public static readonly string[] Adhkar =
        {
            "الله أكبر الله أكبر",
            "سبحان الله وبحمده، سبحان الله العظيم",
            "أستغفر الله العظيم",
            "لا إله إلا أنت سبحانك إني كنت من الظالمين",
        };

        /// <summary>Reminder interval choices shown in the menus (minutes).</summary>
        public static readonly int[] ReminderOptions = { 30, 60, 120 };

        public const int ReminderMinutes = 60;

        /// <summary>How long a reminder stays expanded before folding back (seconds).</summary>
        public const int ReminderVisibleSeconds = 6;

        /// <summary>Idle time before the open widget folds back to the edge (seconds).</summary>
        public const int CollapseAfterSeconds = 4;

        public const bool StartWithWindows = true;

        /// <summary>Font fallback chain: first installed font wins.</summary>
        public const string FontFamily = "Dubai, Segoe UI";
    }
}
