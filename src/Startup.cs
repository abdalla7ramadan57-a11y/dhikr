using Microsoft.Win32;

namespace Dhikr
{
    /// <summary>"Start with Windows" via the per-user Run key (no admin rights, easy to undo).</summary>
    public static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", Name = "Dhikr";

        public static string Command
        {
            get { return "\"" + System.Windows.Forms.Application.ExecutablePath + "\" --autostart"; }
        }

        /// <summary>Write (or refresh, if the exe moved) / remove the Run entry.</summary>
        public static void Apply(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (enabled)
                    {
                        if ((key.GetValue(Name) as string) != Command) key.SetValue(Name, Command);
                    }
                    else if (key.GetValue(Name) != null) key.DeleteValue(Name, false);
                }
            }
            catch (System.Exception ex) { Storage.Log(ex); }
        }
    }
}
