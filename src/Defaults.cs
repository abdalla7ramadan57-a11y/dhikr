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

        /// <summary>The in-card interval stepper moves in 5-minute steps within this range.</summary>
        public const int IntervalStep = 5, IntervalMin = 5, IntervalMax = 240;

        /// <summary>
        /// No real mouse movement for this long = the user is away (or waiting on a render).
        /// A reminder then keeps gently peeking out and glowing until the widget is clicked.
        /// </summary>
        public const int IdleMinutes = 5;

        /// <summary>Reminder glow colours: key, Arabic name, colour.</summary>
        public static readonly string[,] GlowColors =
        {
            { "Green",  "أخضر",   "#4ADE80" },
            { "Blue",   "أزرق",   "#60A5FA" },
            { "White",  "أبيض",   "#F4F4F5" },
            { "Purple", "بنفسجي", "#A78BFA" },
            { "Red",    "أحمر",   "#F87171" },
            { "Yellow", "أصفر",   "#FACC15" },
        };

        public const string GlowColor = "Green";

        /// <summary>Font fallback chain: first installed font wins.</summary>
        public const string FontFamily = "Dubai, Segoe UI";
    }
}
