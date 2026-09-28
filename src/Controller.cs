using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Dhikr
{
    /// <summary>Owns the state and wires the widget, menus, tray icon, reminder timer and startup setting.</summary>
    public class Controller
    {
        public AppState State { get; private set; }

        BubbleWindow _widget;
        Forms.NotifyIcon _tray;
        Forms.ContextMenuStrip _menu;
        Window _popup;
        readonly DispatcherTimer _reminder = new DispatcherTimer();
        readonly DispatcherTimer _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        TimeSpan? _intervalOverride;

        readonly DispatcherTimer _activityProbe = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        System.Drawing.Point _anchor;
        DateTime _lastActive = DateTime.Now;
        TimeSpan _idleAfter = TimeSpan.FromMinutes(Defaults.IdleMinutes);

        bool UserAway { get { return DateTime.Now - _lastActive >= _idleAfter; } }

        public void Start(string[] args)
        {
            State = Storage.Load();
            Startup.Apply(State.StartWithWindows == true); // also refreshes the path if the exe moved

            // Testing aid: --reminder-seconds 30  (not saved; the real default stays 60 minutes)
            int i = Array.IndexOf(args, "--reminder-seconds");
            int sec;
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out sec) && sec > 0)
                _intervalOverride = TimeSpan.FromSeconds(sec);
            // Testing aid: --idle-seconds 20  (how long without real mouse movement counts as "away")
            i = Array.IndexOf(args, "--idle-seconds");
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out sec) && sec > 0)
                _idleAfter = TimeSpan.FromSeconds(sec);

            // Away detection: only a real mouse move counts; small jitter (e.g. while a render runs) doesn't.
            _anchor = Forms.Cursor.Position;
            _activityProbe.Tick += (s, e) =>
            {
                var p = Forms.Cursor.Position;
                int dx = p.X - _anchor.X, dy = p.Y - _anchor.Y;
                if (dx * dx + dy * dy > 30 * 30) { _anchor = p; _lastActive = DateTime.Now; }
            };
            _activityProbe.Start();

            _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); Storage.Save(State); };
            _reminder.Tick += (s, e) => Remind();
            RestartReminder();

            _widget = new BubbleWindow(this);
            _widget.Place();
            _widget.Show();

            _menu = new DarkMenu();
            // Items are built on demand; WinForms pre-cancels opening an empty menu, so un-cancel after building.
            _menu.Opening += (s, e) => { BuildMenu(_trayOpening); e.Cancel = false; };
            CreateTray();

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            SystemParameters.StaticPropertyChanged += (s, e) => { if (e.PropertyName == "WorkArea") OnDisplayChanged(s, e); };
            Application.Current.SessionEnding += (s, e) => Storage.Save(State);
            Storage.Save(State);
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { if (_widget != null) _widget.Place(); }));
        }

        // ---------- actions ----------

        public void Increment()
        {
            State.Current.Count++;
            _widget.Counted();
            SaveSoon();
        }

        public void Move(int delta)
        {
            int n = State.Adhkar.Count;
            State.CurrentIndex = ((State.CurrentIndex + delta) % n + n) % n;
            _widget.Refresh(delta);
            _widget.Touched();
            SaveSoon();
        }

        public void ResetCurrent()
        {
            State.Current.Count = 0;
            _widget.Refresh(0);
            _widget.Expand(false);
            SaveSoon();
        }

        public void AddDhikr(string text)
        {
            text = string.Join(" ", text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            if (text.Length == 0) return;
            int idx = State.Adhkar.FindIndex(d => d.Text == text);
            if (idx < 0)
            {
                State.Adhkar.Add(new DhikrItem { Text = text, Custom = true });
                idx = State.Adhkar.Count - 1;
            }
            State.CurrentIndex = idx;
            Storage.Save(State);
            ShowWidget();
            _widget.Expand(false);
        }

        public void DeleteCurrent() { DeleteAt(State.CurrentIndex); _widget.Expand(false); }

        /// <summary>Remove a user-added dhikr (built-in ones stay).</summary>
        public void DeleteAt(int index)
        {
            if (index < 0 || index >= State.Adhkar.Count || !State.Adhkar[index].Custom) return;
            State.Adhkar.RemoveAt(index);
            if (State.CurrentIndex > index) State.CurrentIndex--;
            if (State.CurrentIndex >= State.Adhkar.Count) State.CurrentIndex = 0;
            Storage.Save(State);
            _widget.Refresh(0);
        }

        public void Select(int index)
        {
            if (index < 0 || index >= State.Adhkar.Count) return;
            State.CurrentIndex = index;
            _widget.Refresh(0);
            SaveSoon();
        }

        public void SetInterval(int minutes)
        {
            State.ReminderMinutes = minutes;
            _intervalOverride = null;
            RestartReminder();
            SaveSoon();
        }

        /// <summary>In-card stepper: ±5 minutes.</summary>
        public void StepInterval(int direction)
        {
            int m = State.ReminderMinutes + direction * Defaults.IntervalStep;
            m = Math.Max(Defaults.IntervalMin, Math.Min(Defaults.IntervalMax, m));
            SetInterval(m);
            _widget.Refresh(0);
            _widget.Touched();
        }

        public void SetGlowColor(string key)
        {
            State.GlowColor = key;
            SaveSoon();
            _widget.PreviewGlow();
        }

        public void SetStartWithWindows(bool on)
        {
            State.StartWithWindows = on;
            Startup.Apply(on);
            SaveSoon();
        }

        public void SetSide(string side)
        {
            if (State.Side == side) return;
            State.Side = side;
            _widget.Place();
            SaveSoon();
        }

        public void ShowAddDialog() { ShowPopup(new AddDhikrWindow(_widget.EdgeAnchor, _widget.OnRightSide, AddDhikr)); }

        public void ShowSettings() { ShowPopup(new SettingsWindow(this, _widget.EdgeAnchor, _widget.OnRightSide)); }

        void ShowPopup(Window w)
        {
            if (_popup != null) _popup.Close();
            ShowWidget();
            _popup = w;
            w.Closed += (s, e) => { if (_popup == w) _popup = null; };
            w.Show();
        }

        void ShowWidget()
        {
            if (_widget.IsVisible) return;
            _widget.Place();
            _widget.Show();
        }

        public void Exit()
        {
            Storage.Save(State);
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            Application.Current.Shutdown();
        }

        public void SaveSoon()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        // ---------- reminder ----------

        void RestartReminder()
        {
            _reminder.Stop();
            _reminder.Interval = _intervalOverride ?? TimeSpan.FromMinutes(State.ReminderMinutes);
            _reminder.Start();
        }

        /// <summary>Every interval: move on to a different dhikr and let the widget stretch out to show it.</summary>
        void Remind()
        {
            if (!_widget.IsVisible) return; // hidden = quiet
            State.CurrentIndex = (State.CurrentIndex + 1) % State.Adhkar.Count;
            SaveSoon();
            if (_widget.InAttention) { _widget.Refresh(0); return; } // still calling from last time
            if (UserAway) _widget.RemindUntilClicked();
            else _widget.Remind();
        }

        // ---------- menus (one dark menu, shared by the widget and the tray) ----------

        bool _trayOpening;

        public void OpenMenu()
        {
            _trayOpening = false;
            SetForegroundWindow(_menu.Handle); // the widget never takes focus; without this the menu closes at once
            _menu.Show(Forms.Cursor.Position);
        }

        void BuildMenu(bool forTray)
        {
            _menu.Items.Clear();
            if (forTray) _menu.Items.Add("إظهار الذكر", null, (s, e) => { ShowWidget(); _widget.Expand(false); });
            _menu.Items.Add("إضافة ذكر…", null, (s, e) => ShowAddDialog());

            var interval = new Forms.ToolStripMenuItem("مدة التذكير");
            foreach (int m in Defaults.ReminderOptions)
            {
                int minutes = m;
                interval.DropDownItems.Add(new Forms.ToolStripMenuItem(IntervalLabel(m), null, (s, e) => SetInterval(minutes))
                {
                    Checked = _intervalOverride == null && State.ReminderMinutes == m,
                });
            }
            DarkMenu.Style(interval.DropDown);
            _menu.Items.Add(interval);

            _menu.Items.Add(new Forms.ToolStripMenuItem("التشغيل مع Windows", null, (s, e) => SetStartWithWindows(State.StartWithWindows != true))
            {
                Checked = State.StartWithWindows == true,
            });
            if (!forTray)
            {
                _menu.Items.Add("تصفير عداد الذكر الحالي", null, (s, e) => ResetCurrent());
                if (State.Current.Custom) _menu.Items.Add("حذف هذا الذكر", null, (s, e) => DeleteCurrent());
            }
            _menu.Items.Add("الإعدادات…", null, (s, e) => ShowSettings());
            _menu.Items.Add(new Forms.ToolStripSeparator());
            if (!forTray) _menu.Items.Add("إخفاء", null, (s, e) => _widget.Hide());
            _menu.Items.Add("خروج", null, (s, e) => Exit());
        }

        static string IntervalLabel(int m) { return "كل " + IntervalShortLabel(m); }

        public static string IntervalShortLabel(int m)
        {
            if (m == 60) return "ساعة";
            if (m == 120) return "ساعتين";
            if (m % 60 == 0) return (m / 60) + " ساعات";
            return m + (m <= 10 ? " دقائق" : " دقيقة");
        }

        void CreateTray()
        {
            _tray = new Forms.NotifyIcon { Icon = LoadIcon(), Text = "ذكر", Visible = true };
            _tray.MouseUp += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left) { ShowWidget(); _widget.Expand(false); }
                else if (e.Button == Forms.MouseButtons.Right)
                {
                    _trayOpening = true;
                    SetForegroundWindow(_menu.Handle); // lets the menu close when clicking elsewhere
                    _menu.Show(Forms.Cursor.Position);
                }
            };
        }

        static System.Drawing.Icon LoadIcon()
        {
            var stream = typeof(Controller).Assembly.GetManifestResourceStream("Dhikr.dhikr.ico");
            if (stream == null) return System.Drawing.SystemIcons.Application;
            using (stream) return new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
