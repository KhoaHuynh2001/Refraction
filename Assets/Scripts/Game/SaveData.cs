using System;
using System.Collections.Generic;
using System.IO;
using Refraction.Core;
using UnityEngine;

namespace Refraction.Game
{
    /// <summary>Progress, per-level boards and settings, stored as JSON in Application.persistentDataPath.</summary>
    public sealed class SaveData
    {
        public readonly HashSet<string> Beaten = new HashSet<string>();
        public readonly Dictionary<string, int> BestPieces = new Dictionary<string, int>();
        public readonly Dictionary<string, string> Boards = new Dictionary<string, string>();
        public readonly HashSet<string> TutorialSeen = new HashSet<string>();
        public string LastPlayed = "";

        // settings
        public bool Sound = true;
        public bool Stars = true;
        public bool Hints = true;
        public bool ColorLetters = false;
        public bool UnlockAll = false;
        public bool KeepScreenOn = true;

        static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static SaveData Load()
        {
            var s = new SaveData();
            try
            {
                if (!File.Exists(FilePath)) return s;
                var d = MiniJson.Parse(File.ReadAllText(FilePath)) as Dictionary<string, object>;
                if (d == null) return s;
                foreach (var o in d.GetList("beaten") ?? new List<object>()) if (o is string id) s.Beaten.Add(id);
                foreach (var o in d.GetList("tutorialSeen") ?? new List<object>()) if (o is string id) s.TutorialSeen.Add(id);
                var best = d.GetObject("best");
                if (best != null) foreach (var kv in best) if (kv.Value is double v) s.BestPieces[kv.Key] = (int)v;
                var boards = d.GetObject("boards");
                if (boards != null) foreach (var kv in boards) if (kv.Value is string b) s.Boards[kv.Key] = b;
                s.LastPlayed = d.GetString("lastPlayed", "");
                var st = d.GetObject("settings");
                s.Sound = st.GetBool("sound", true);
                s.Stars = st.GetBool("stars", true);
                s.Hints = st.GetBool("hints", true);
                s.ColorLetters = st.GetBool("colorLetters", false);
                s.UnlockAll = st.GetBool("unlockAll", false);
                s.KeepScreenOn = st.GetBool("keepScreenOn", true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Refraction: could not read save file, starting fresh. " + e.Message);
            }
            return s;
        }

        public void Save()
        {
            try
            {
                var d = new Dictionary<string, object>
                {
                    ["version"] = 1,
                    ["lastPlayed"] = LastPlayed,
                    ["beaten"] = new List<object>(Beaten),
                    ["tutorialSeen"] = new List<object>(TutorialSeen),
                };
                var best = new Dictionary<string, object>();
                foreach (var kv in BestPieces) best[kv.Key] = kv.Value;
                d["best"] = best;
                var boards = new Dictionary<string, object>();
                foreach (var kv in Boards) boards[kv.Key] = kv.Value;
                d["boards"] = boards;
                d["settings"] = new Dictionary<string, object>
                {
                    ["sound"] = Sound, ["stars"] = Stars, ["hints"] = Hints,
                    ["colorLetters"] = ColorLetters, ["unlockAll"] = UnlockAll, ["keepScreenOn"] = KeepScreenOn,
                };
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, MiniJson.Serialize(d));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Refraction: could not write save file. " + e.Message);
            }
        }

        public void ResetProgress()
        {
            Beaten.Clear(); BestPieces.Clear(); Boards.Clear(); TutorialSeen.Clear(); LastPlayed = "";
            Save();
        }
    }
}
