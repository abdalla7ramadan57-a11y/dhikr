using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace DhikrSetup
{
    /// <summary>
    /// Tiny per-user installer (no admin): copies the embedded Dhikr.exe to %LocalAppData%\Programs\Dhikr,
    /// adds Desktop + Start Menu shortcuts and an "Installed apps" entry, then starts the app
    /// (which registers itself to start with Windows). The installed Uninstall.exe removes it again;
    /// user data in %AppData%\Dhikr is kept so counters survive a reinstall.
    /// </summary>
    static class Setup
    {
        const string AppName = "Dhikr", DisplayName = "Dhikr - ذكر", Version = "1.2.0";
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Dhikr";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static readonly string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);
        static readonly string AppExe = Path.Combine(InstallDir, "Dhikr.exe");
        static readonly string UninstallExe = Path.Combine(InstallDir, "Uninstall.exe");
        static readonly string[] Shortcuts =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Dhikr.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Dhikr.lnk"),
        };

        [STAThread]
        static int Main(string[] args)
        {
            bool uninstall = Array.Exists(args, a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase));
            bool silent = Array.Exists(args, a => a.Equals("/silent", StringComparison.OrdinalIgnoreCase));
            try
            {
                if (silent) { if (uninstall) Uninstall(); else Install(); return 0; }
                // Software rendering keeps the transparent card crisp and opaque on every GPU.
                System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                return new Application().Run(new SetupWindow(uninstall));
            }
            catch (Exception ex)
            {
                if (!silent) MessageBox.Show(ex.Message, DisplayName, MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }

        public static bool IsInstalled { get { return File.Exists(AppExe); } }
        public static string Location { get { return InstallDir; } }

        public static void Install()
        {
            StopRunningApp();
            Directory.CreateDirectory(InstallDir);

            using (var src = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dhikr.exe"))
            using (var dst = File.Create(AppExe))
                src.CopyTo(dst);

            string self = Process.GetCurrentProcess().MainModule.FileName;
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(UninstallExe), StringComparison.OrdinalIgnoreCase))
                File.Copy(self, UninstallExe, true);

            foreach (var lnk in Shortcuts) CreateShortcut(lnk, AppExe);

            using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", DisplayName);
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", "Dhikr");
                k.SetValue("DisplayIcon", AppExe);
                k.SetValue("InstallLocation", InstallDir);
                k.SetValue("UninstallString", "\"" + UninstallExe + "\" /uninstall");
                k.SetValue("QuietUninstallString", "\"" + UninstallExe + "\" /uninstall /silent");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)Math.Max(1, new FileInfo(AppExe).Length / 1024), RegistryValueKind.DWord);
            }

            // Start it; the app itself points "Start with Windows" at this installed copy.
            Process.Start(new ProcessStartInfo(AppExe) { UseShellExecute = true, WorkingDirectory = InstallDir });
        }

        public static void Uninstall()
        {
            StopRunningApp();
            using (var run = Registry.CurrentUser.OpenSubKey(RunKey, true))
                if (run != null && run.GetValue(AppName) != null) run.DeleteValue(AppName, false);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            foreach (var lnk in Shortcuts) if (File.Exists(lnk)) File.Delete(lnk);
            if (File.Exists(AppExe)) File.Delete(AppExe);

            // Uninstall.exe can't delete itself while running: remove the folder a moment after we exit.
            Process.Start(new ProcessStartInfo("cmd.exe", "/c timeout /t 2 /nobreak >nul & rmdir /s /q \"" + InstallDir + "\"")
            {
                CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden,
            });
        }

        static void StopRunningApp()
        {
            foreach (var p in Process.GetProcessesByName(AppName))
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { }
            }
            Thread.Sleep(200);
        }

        static void CreateShortcut(string lnk, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lnk));
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            var t = link.GetType();
            t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
            t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(target) });
            t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { target + ",0" });
            t.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "Dhikr" });
            t.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        }
    }

    /// <summary>One small dark card: what will happen, one button, done.</summary>
    class SetupWindow : Window
    {
        static readonly Brush Text = Hex("#EDEDED"), Dim = Hex("#9EA3A1"), Accent = Hex("#8CCFB9");
        readonly TextBlock _status, _buttonText;
        readonly bool _uninstall;
        bool _done;

        public SetupWindow(bool uninstall)
        {
            _uninstall = uninstall;
            Title = uninstall ? "إزالة ذكر" : "تثبيت ذكر";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Dubai, Segoe UI");
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            var close = new TextBlock { Text = "✕", FontSize = 13, Foreground = Dim, Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left };
            close.MouseLeftButtonDown += (s, e) => e.Handled = true; // don't start a window drag
            close.MouseLeftButtonUp += (s, e) => Close();

            var handle = new Border
            {
                Width = 12, Height = 44, CornerRadius = new CornerRadius(6), VerticalAlignment = VerticalAlignment.Center,
                Background = new LinearGradientBrush(Color("#3A3A40"), Color("#232326"), 90),
                BorderBrush = Hex("#40FFFFFF"), BorderThickness = new Thickness(1),
            };
            var titles = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock { Text = "ذكر", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Text });
            titles.Children.Add(new TextBlock { Text = "ذكر لطيف على حافة الشاشة", FontSize = 13, Foreground = Dim });
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(handle);
            header.Children.Add(titles);

            _status = new TextBlock
            {
                FontSize = 13, Foreground = Dim, TextWrapping = TextWrapping.Wrap, Width = 300,
                Margin = new Thickness(0, 16, 0, 18), LineHeight = 21,
                Text = uninstall
                    ? "سيتم إزالة البرنامج وإيقاف تشغيله مع Windows.\nأذكارك وعداداتك تبقى محفوظة."
                    : (Setup.IsInstalled ? "سيتم تحديث البرنامج المثبت في:" : "سيتم التثبيت في:") + "\n" + Setup.Location,
            };

            _buttonText = new TextBlock
            {
                Text = uninstall ? "إزالة" : (Setup.IsInstalled ? "تحديث" : "تثبيت"),
                FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Hex("#111614"),
            };
            var button = new Border
            {
                CornerRadius = new CornerRadius(10), Background = Accent, Padding = new Thickness(26, 5, 26, 7),
                Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left, Child = _buttonText,
            };
            button.MouseEnter += (s, e) => button.Opacity = 0.88;
            button.MouseLeave += (s, e) => button.Opacity = 1;
            button.MouseLeftButtonDown += (s, e) => e.Handled = true; // don't start a window drag
            button.MouseLeftButtonUp += (s, e) => Run();

            var panel = new StackPanel();
            panel.Children.Add(close);
            panel.Children.Add(header);
            panel.Children.Add(_status);
            panel.Children.Add(button);

            Content = new Border
            {
                Margin = new Thickness(20), Padding = new Thickness(22, 14, 22, 20), CornerRadius = new CornerRadius(18),
                Background = Hex("#FF222226"),
                BorderBrush = new LinearGradientBrush(Color("#38FFFFFF"), Color("#14FFFFFF"), 90), BorderThickness = new Thickness(1),
                Child = panel,
            };

            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) Close();
                else if (e.Key == Key.Enter) Run();
            };
        }

        void Run()
        {
            if (_done) { Close(); return; }
            try
            {
                if (_uninstall) Setup.Uninstall(); else Setup.Install();
                _done = true;
                _status.Text = _uninstall
                    ? "تمت الإزالة ✓"
                    : "تم التثبيت ✓\nالبرنامج يعمل الآن على حافة الشاشة، وسيبدأ تلقائيًا مع Windows.\nتجد اختصاره على سطح المكتب.";
                _status.Foreground = Text;
                _buttonText.Text = "تم";
            }
            catch (Exception ex)
            {
                _status.Text = "حدث خطأ: " + ex.Message;
                _status.Foreground = Hex("#E8A0A0");
            }
        }

        static Color Color(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        static Brush Hex(string hex) { var b = new SolidColorBrush(Color(hex)); b.Freeze(); return b; }
    }
}
