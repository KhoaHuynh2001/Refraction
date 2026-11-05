using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Refraction.Core;
using UnityEngine;

namespace Refraction.Game
{
    public sealed class LevelEntry
    {
        public string Id;            // "main:001" or "custom:my_level"
        public string Pack;          // "main" | "custom"
        public int Index;            // position inside its pack (0-based)
        public string Title;         // shown in menus
        public string Source;        // where it came from (for error messages)
        public Level Level;          // null when it failed to load
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Level != null && Errors.Count == 0;
    }

    /// <summary>
    /// All playable levels.
    ///  * main pack:   Resources/Levels/*.json (the 140 original levels, 7 sectors of 20)
    ///  * custom pack: Resources/CustomLevels/*.json  +  &lt;persistentDataPath&gt;/CustomLevels/*.json
    /// Drop new JSON files in either custom location; they show up under "Level packs".
    /// </summary>
    public static class LevelLibrary
    {
        public const int SectorSize = 20;
        public static readonly List<LevelEntry> Main = new List<LevelEntry>();
        public static readonly List<LevelEntry> Custom = new List<LevelEntry>();

        public static string CustomFolder => Path.Combine(Application.persistentDataPath, "CustomLevels");

        public static int SectorCount => (Main.Count + SectorSize - 1) / SectorSize;

        public static void Reload()
        {
            Main.Clear();
            foreach (var ta in Resources.LoadAll<TextAsset>("Levels").OrderBy(t => t.name, StringComparer.Ordinal))
                Main.Add(Parse("main", ta.name, ta.text, "Resources/Levels/" + ta.name));
            SortAndIndex(Main);
            for (int i = 0; i < Main.Count; i++)
                Main[i].Title = $"{i / SectorSize + 1}-{i % SectorSize + 1}";
            ReloadCustom();
        }

        public static void ReloadCustom()
        {
            Custom.Clear();
            foreach (var ta in Resources.LoadAll<TextAsset>("CustomLevels"))
                Custom.Add(Parse("custom", ta.name, ta.text, "Resources/CustomLevels/" + ta.name));
            try
            {
                Directory.CreateDirectory(CustomFolder);
                foreach (var f in Directory.GetFiles(CustomFolder, "*.json"))
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (Custom.Any(e => e.Id == "custom:" + name)) continue;   // same file name: Resources copy wins
                    Custom.Add(Parse("custom", name, File.ReadAllText(f), f));
                }
            }
            catch (Exception e) { Debug.LogWarning("Refraction: cannot read custom level folder: " + e.Message); }
            SortAndIndex(Custom);
            foreach (var e in Custom)
                if (string.IsNullOrEmpty(e.Title)) e.Title = e.Id.Substring("custom:".Length);
        }

        static LevelEntry Parse(string pack, string name, string text, string source)
        {
            var e = new LevelEntry { Id = pack + ":" + name, Pack = pack, Source = source };
            var errors = new List<string>();
            var lvl = Level.FromJson(text, errors);
            if (lvl != null && errors.Count == 0)
            {
                var rep = LevelValidator.Validate(lvl, false);
                errors.AddRange(rep.Errors);
            }
            e.Errors.AddRange(errors);
            e.Level = lvl;
            if (lvl != null && pack == "custom") e.Title = string.IsNullOrEmpty(lvl.Name) ? name : lvl.Name;
            if (lvl != null && string.IsNullOrEmpty(lvl.Id)) lvl.Id = name;
            if (errors.Count > 0) Debug.LogWarning($"Refraction: level {source} has problems:\n  " + string.Join("\n  ", errors));
            return e;
        }

        static void SortAndIndex(List<LevelEntry> list)
        {
            list.Sort((a, b) =>
            {
                int oa = a.Level?.Order ?? 0, ob = b.Level?.Order ?? 0;
                if (oa != ob) return (oa == 0 ? int.MaxValue : oa).CompareTo(ob == 0 ? int.MaxValue : ob);
                return string.CompareOrdinal(a.Id, b.Id);
            });
            for (int i = 0; i < list.Count; i++) list[i].Index = i;
        }

        public static LevelEntry Find(string id)
        {
            foreach (var e in Main) if (e.Id == id) return e;
            foreach (var e in Custom) if (e.Id == id) return e;
            return null;
        }

        public static List<LevelEntry> PackOf(LevelEntry e) => e.Pack == "main" ? Main : Custom;

        public static LevelEntry Next(LevelEntry e)
        {
            var list = PackOf(e);
            return e.Index + 1 < list.Count ? list[e.Index + 1] : null;
        }

        public static int BeatenInSector(SaveData save, int sector)
        {
            int n = 0;
            for (int i = sector * SectorSize; i < Math.Min(Main.Count, (sector + 1) * SectorSize); i++)
                if (save.Beaten.Contains(Main[i].Id)) n++;
            return n;
        }

        /// <summary>Original rule: a sector opens once 11 levels of the previous sector are solved.</summary>
        public static bool SectorUnlocked(SaveData save, int sector)
        {
            if (sector <= 0 || save.UnlockAll) return true;
            return BeatenInSector(save, sector - 1) >= 11;
        }

        public static bool IsPlayable(SaveData save, LevelEntry e)
        {
            if (e == null || !e.Ok) return false;
            if (e.Pack != "main") return true;
            return SectorUnlocked(save, e.Index / SectorSize);
        }
    }
}
