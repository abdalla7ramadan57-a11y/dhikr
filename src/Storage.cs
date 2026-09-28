using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Dhikr
{
    [DataContract]
    public class DhikrItem
    {
        [DataMember(Order = 0)] public string Text;
        [DataMember(Order = 1)] public long Count;
        [DataMember(Order = 2)] public bool Custom;
    }

    [DataContract]
    public class AppState
    {
        [DataMember(Order = 0)] public List<DhikrItem> Adhkar = new List<DhikrItem>();
        [DataMember(Order = 1)] public int CurrentIndex;
        [DataMember(Order = 2)] public int ReminderMinutes = Defaults.ReminderMinutes;
        [DataMember(Order = 3)] public string Side = "Right";
        [DataMember(Order = 4)] public double VerticalRatio = 0.45;
        [DataMember(Order = 5)] public string Screen;
        [DataMember(Order = 6)] public bool? StartWithWindows;

        public DhikrItem Current
        {
            get
            {
                if (CurrentIndex < 0 || CurrentIndex >= Adhkar.Count) CurrentIndex = 0;
                return Adhkar[CurrentIndex];
            }
        }
    }

    /// <summary>Loads/saves state as JSON in %AppData%\Dhikr\data.json.</summary>
    public static class Storage
    {
        public static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dhikr");

        public static string FilePath { get { return Path.Combine(Folder, "data.json"); } }

        static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(AppState));

        public static AppState Load()
        {
            AppState state = null;
            try
            {
                if (File.Exists(FilePath))
                    using (var fs = File.OpenRead(FilePath))
                        state = (AppState)Serializer.ReadObject(fs);
            }
            catch (Exception ex)
            {
                Log(ex);
                try { File.Copy(FilePath, Path.Combine(Folder, "data.corrupt.json"), true); } catch { }
            }
            return Normalize(state ?? new AppState());
        }

        /// <summary>Built-in adhkar first (in code order, keeping saved counts), then user-added ones.</summary>
        static AppState Normalize(AppState s)
        {
            var saved = (s.Adhkar ?? new List<DhikrItem>()).Where(d => d != null && !string.IsNullOrWhiteSpace(d.Text)).ToList();
            string currentText = s.CurrentIndex >= 0 && s.CurrentIndex < saved.Count ? saved[s.CurrentIndex].Text : null;

            var list = new List<DhikrItem>();
            foreach (var text in Defaults.Adhkar)
            {
                var old = saved.FirstOrDefault(d => d.Text == text);
                list.Add(new DhikrItem { Text = text, Count = old != null ? old.Count : 0 });
            }
            foreach (var d in saved.Where(d => d.Custom && list.All(x => x.Text != d.Text)))
                list.Add(d);

            s.Adhkar = list;
            s.CurrentIndex = Math.Max(0, list.FindIndex(d => d.Text == currentText));
            if (s.StartWithWindows == null) s.StartWithWindows = Defaults.StartWithWindows;
            if (s.ReminderMinutes <= 0) s.ReminderMinutes = Defaults.ReminderMinutes;
            if (s.Side != "Left") s.Side = "Right";
            if (double.IsNaN(s.VerticalRatio) || s.VerticalRatio < 0 || s.VerticalRatio > 1) s.VerticalRatio = 0.45;
            return s;
        }

        public static void Save(AppState state)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string tmp = FilePath + ".tmp";
                using (var fs = File.Create(tmp))
                using (var w = JsonReaderWriterFactory.CreateJsonWriter(fs, Encoding.UTF8, false, true, "  "))
                {
                    Serializer.WriteObject(w, state);
                }
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch (Exception ex) { Log(ex); }
        }

        public static void Log(object msg)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllText(Path.Combine(Folder, "error.log"), DateTime.Now.ToString("s") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }
    }
}
