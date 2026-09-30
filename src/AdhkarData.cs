using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Dhikr
{
    /// <summary>A built-in dhikr with its virtue/evidence and source (shown behind the ⓘ button).</summary>
    [DataContract]
    public class DhikrDef
    {
        [DataMember(Name = "t")] public string Text;
        [DataMember(Name = "f")] public string Fadl;
        [DataMember(Name = "s")] public string Source;
    }

    /// <summary>
    /// The built-in adhkar live in assets/adhkar.json (edit that file to add or change them).
    /// The build embeds it gzipped to keep the program small.
    /// </summary>
    public static class AdhkarData
    {
        static List<DhikrDef> _all;

        public static List<DhikrDef> All
        {
            get
            {
                if (_all == null)
                {
                    try
                    {
                        using (var raw = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dhikr.adhkar.json.gz"))
                        using (var gz = new GZipStream(raw, CompressionMode.Decompress))
                            _all = (List<DhikrDef>)new DataContractJsonSerializer(typeof(List<DhikrDef>)).ReadObject(gz);
                    }
                    catch (System.Exception ex)
                    {
                        Storage.Log(ex);
                        _all = new List<DhikrDef>();
                    }
                    if (_all.Count == 0)
                        foreach (var t in new[] { "الله أكبر", "الحمد لله", "سبحان الله وبحمده، سبحان الله العظيم", "أستغفر الله العظيم", "لا إله إلا أنت سبحانك إني كنت من الظالمين" })
                            _all.Add(new DhikrDef { Text = t });
                }
                return _all;
            }
        }

        /// <summary>Info for a dhikr, or null (user-added adhkar have none).</summary>
        public static DhikrDef Find(string text)
        {
            var d = All.FirstOrDefault(x => x.Text == text);
            return d != null && !string.IsNullOrEmpty(d.Fadl) ? d : null;
        }
    }
}
