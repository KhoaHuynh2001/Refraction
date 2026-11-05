using System;
using System.Collections.Generic;
using System.Linq;
using Refraction.Core;
using UnityEngine;

namespace Refraction.Game
{
    static class Pal
    {
        public static readonly Color Text = new Color(0.86f, 0.87f, 0.9f);
        public static readonly Color Dim = new Color(0.55f, 0.56f, 0.6f);
        public static readonly Color Good = new Color(0.35f, 0.85f, 0.3f);
        public static readonly Color Bad = new Color(1f, 0.4f, 0.35f);
        public static readonly Color Shade = new Color(0, 0, 0, 0.8f);
    }

    public sealed class MainMenuScreen : Layer
    {
        public MainMenuScreen() : base("MainMenu")
        {
            var t = Root.transform;
            NewSprite(t, "TitleBg", Res.Sprite("menu_title_background"), new Vector2(0, App.Top - 1.6f), 1f, 1);
            NewSprite(t, "Title", Res.Sprite("menu_title_refraction"), new Vector2(0, App.Top - 1.6f), 1f, 2);

            var save = App.Instance.Save;
            var cont = ContinueTarget(save);
            float y = 2.45f, step = 1.42f, s = 0.85f;
            ImageButton(t, "menu_button_startgame", new Vector2(0, y), s, () => App.Play(ContinueTarget(App.Save)));
            ImageButton(t, "menu_button_levelselect", new Vector2(0, y - step), s, () => App.ShowSectors());
            ImageButton(t, "menu_button_levelpacks", new Vector2(0, y - step * 2), s, () => App.ShowCustom());
            ImageButton(t, "menu_button_help", new Vector2(0, y - step * 3), s, () => App.ShowHelp());
            ImageButton(t, "menu_button_options", new Vector2(0, y - step * 4), s, () => App.ShowOptions());
            ImageButton(t, "menu_button_about", new Vector2(0, y - step * 5), s, () => App.ShowAbout());

            int beaten = LevelLibrary.Main.Count(e => save.Beaten.Contains(e.Id));
            string sub = cont != null ? $"continue: {cont.Title}   -   solved {beaten}/{LevelLibrary.Main.Count}" : "";
            NewText(t, sub, new Vector2(0, y + 0.95f), 0.2f, Pal.Dim);
        }

        static LevelEntry ContinueTarget(SaveData save)
        {
            var last = LevelLibrary.Find(save.LastPlayed);
            if (last != null && LevelLibrary.IsPlayable(save, last) && !save.Beaten.Contains(last.Id)) return last;
            // first unsolved playable main level
            foreach (var e in LevelLibrary.Main)
                if (!save.Beaten.Contains(e.Id) && LevelLibrary.IsPlayable(save, e)) return e;
            return last ?? LevelLibrary.Main.FirstOrDefault();
        }

        public override void OnBack() => Application.Quit();
    }

    public sealed class SectorScreen : Layer
    {
        public SectorScreen() : base("Sectors")
        {
            var t = Root.transform;
            var save = App.Instance.Save;
            NewText(t, "SELECT SECTOR", new Vector2(0, App.Top - 0.6f), 0.45f, Pal.Text);
            int n = LevelLibrary.SectorCount;
            float step = Mathf.Min(1.45f, 9.6f / Mathf.Max(1, n));
            float y0 = App.Top - 1.7f;
            for (int i = 0; i < n; i++)
            {
                int sector = i;
                bool open = LevelLibrary.SectorUnlocked(save, i);
                Vector2 pos = new Vector2(-0.4f, y0 - step * i);
                UIButton b;
                string img = $"sector{i + 1}_{(open ? "dark" : "light")}";
                if (i < 6) b = ImageButton(t, img, pos, step / 1.5f, () => App.ShowLevels(sector));
                else b = TextButton(t, $"SECTOR {i + 1}", pos, new Vector2(6f * step / 1.5f, 1.4f * step / 1.5f), () => App.ShowLevels(sector), 30, 0.55f);
                b.Enabled = open;
                b.Dim = 0.55f;
                b.Refresh();
                int beaten = LevelLibrary.BeatenInSector(save, i);
                int total = Mathf.Min(LevelLibrary.SectorSize, LevelLibrary.Main.Count - i * LevelLibrary.SectorSize);
                NewText(t, open ? $"{beaten}/{total}" : "locked", pos + new Vector2(3.55f, 0), 0.24f, beaten == total ? Pal.Good : Pal.Dim);
            }
            TextButton(t, "BACK", new Vector2(0, App.Bottom + 0.6f), new Vector2(3f, 0.8f), () => App.ShowMainMenu());
            if (!save.UnlockAll)
                NewText(t, "solve 11 levels to open the next sector", new Vector2(0, App.Bottom + 1.3f), 0.17f, Pal.Dim);
        }

        public override void OnBack() => App.ShowMainMenu();
    }

    public sealed class LevelSelectScreen : Layer
    {
        readonly int sector;

        public LevelSelectScreen(int sector) : base("Levels")
        {
            this.sector = sector;
            var t = Root.transform;
            var save = App.Instance.Save;
            NewText(t, $"SECTOR {sector + 1}", new Vector2(0, App.Top - 0.6f), 0.5f, Pal.Text);
            int start = sector * LevelLibrary.SectorSize;
            float cell = 1.68f;
            for (int k = 0; k < LevelLibrary.SectorSize && start + k < LevelLibrary.Main.Count; k++)
            {
                var e = LevelLibrary.Main[start + k];
                int col = k % 4, row = k / 4;
                var pos = new Vector2((col - 1.5f) * cell, App.Top - 2.2f - row * cell);
                bool done = save.Beaten.Contains(e.Id);
                string img = $"levelselect_{(done ? "completed" : "notcompleted")}_lvl{k + 1:00}";
                var b = ImageButton(t, img, pos, 1f, () => App.Play(e));
                b.Enabled = LevelLibrary.IsPlayable(save, e);
                b.Refresh();
                if (save.BestPieces.TryGetValue(e.Id, out int best))
                    NewText(t, $"{best} pcs", pos + new Vector2(0, -0.58f), 0.14f, Pal.Dim, TextAnchor.MiddleCenter, 40);
            }
            float by = App.Bottom + 0.6f;
            var prev = TextButton(t, "<", new Vector2(-2.9f, by), new Vector2(1.2f, 0.8f), () => App.ShowLevels(sector - 1));
            prev.Enabled = sector > 0 && LevelLibrary.SectorUnlocked(save, sector - 1); prev.Refresh();
            TextButton(t, "SECTORS", new Vector2(0, by), new Vector2(3.4f, 0.8f), () => App.ShowSectors());
            var next = TextButton(t, ">", new Vector2(2.9f, by), new Vector2(1.2f, 0.8f), () => App.ShowLevels(sector + 1));
            next.Enabled = sector + 1 < LevelLibrary.SectorCount && LevelLibrary.SectorUnlocked(save, sector + 1); next.Refresh();
        }

        public override void OnBack() => App.ShowSectors();
    }

    public sealed class CustomLevelsScreen : Layer
    {
        const int PerPage = 12;
        int page;
        Transform listRoot;
        readonly List<UIButton> pageButtons = new List<UIButton>();

        public CustomLevelsScreen(int page = 0) : base("CustomLevels")
        {
            this.page = page;
            var t = Root.transform;
            NewText(t, "LEVEL PACKS", new Vector2(0, App.Top - 0.6f), 0.45f, Pal.Text);
            NewText(t, "your own / AI-made levels", new Vector2(0, App.Top - 1.15f), 0.2f, Pal.Dim);
            float by = App.Bottom + 0.6f;
            TextButton(t, "<", new Vector2(-3.2f, by), new Vector2(1.0f, 0.8f), () => { this.page--; Build(); });
            TextButton(t, "BACK", new Vector2(-1.1f, by), new Vector2(2.6f, 0.8f), () => App.ShowMainMenu());
            TextButton(t, "RELOAD", new Vector2(1.45f, by), new Vector2(2.2f, 0.8f), () => { LevelLibrary.ReloadCustom(); Build(); });
            TextButton(t, ">", new Vector2(3.2f, by), new Vector2(1.0f, 0.8f), () => { this.page++; Build(); });
            Build();
        }

        void Build()
        {
            foreach (var b in pageButtons) Buttons.Remove(b);
            pageButtons.Clear();
            if (listRoot != null) UnityEngine.Object.Destroy(listRoot.gameObject);
            listRoot = new GameObject("List").transform;
            listRoot.SetParent(Root.transform, false);

            var list = LevelLibrary.Custom;
            int pages = Mathf.Max(1, (list.Count + PerPage - 1) / PerPage);
            page = Mathf.Clamp(page, 0, pages - 1);
            Buttons[0].Enabled = page > 0; Buttons[0].Refresh();
            Buttons[3].Enabled = page < pages - 1; Buttons[3].Refresh();

            if (list.Count == 0)
            {
                NewText(listRoot, "No custom levels yet.\n\nPut level .json files in\nAssets/Resources/CustomLevels\n(rebuild) or in this folder\non the device:", new Vector2(0, 2.4f), 0.22f, Pal.Text);
                NewText(listRoot, Wrap(LevelLibrary.CustomFolder, 34), new Vector2(0, -0.2f), 0.16f, Pal.Dim);
                NewText(listRoot, "then press RELOAD.\nSee docs/LEVEL_FORMAT.md", new Vector2(0, -1.6f), 0.2f, Pal.Text);
                return;
            }
            var save = App.Save;
            for (int k = 0; k < PerPage && page * PerPage + k < list.Count; k++)
            {
                var e = list[page * PerPage + k];
                var pos = new Vector2(0, App.Top - 2.0f - k * 0.76f);
                string mark = !e.Ok ? "  (!)" : "";
                var b = TextButton(listRoot, Trim(e.Title, 22) + mark, pos, new Vector2(7.2f, 0.66f), () =>
                {
                    if (e.Ok) App.Play(e);
                    else App.Push(new MessageLayer("Level has errors", Trim(e.Source, 60) + "\n\n" + string.Join("\n", e.Errors.Take(6).Select(x => Wrap(x, 40)))));
                }, 30, 0.24f);
                if (!e.Ok) b.Label.color = Pal.Bad;
                else if (save.Beaten.Contains(e.Id)) b.Label.color = Pal.Good;
                pageButtons.Add(b);
            }
            NewText(listRoot, $"page {page + 1}/{pages}  -  {list.Count} level(s)", new Vector2(0, App.Bottom + 1.35f), 0.18f, Pal.Dim);
        }

        static string Trim(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 3) + "...";

        public static string Wrap(string s, int width)
        {
            var sb = new System.Text.StringBuilder();
            int col = 0;
            foreach (char c in s)
            {
                if (col >= width && (c == ' ' || c == '/' || c == '\\' || c == ',')) { sb.Append(c).Append('\n'); col = 0; continue; }
                sb.Append(c); col++;
                if (c == '\n') col = 0;
            }
            return sb.ToString();
        }

        public override void OnBack() => App.ShowMainMenu();
    }

    public sealed class OptionsScreen : Layer
    {
        readonly List<(UIButton btn, string name, Func<bool> get, Action<bool> set)> toggles = new List<(UIButton, string, Func<bool>, Action<bool>)>();

        public OptionsScreen() : base("Options")
        {
            var t = Root.transform;
            var s = App.Instance.Save;
            NewText(t, "OPTIONS", new Vector2(0, App.Top - 0.6f), 0.5f, Pal.Text);
            float y = App.Top - 2f;
            AddToggle(t, "SOUND", () => s.Sound, v => s.Sound = v, ref y);
            AddToggle(t, "STARS", () => s.Stars, v => s.Stars = v, ref y);
            AddToggle(t, "TUTORIALS", () => s.Hints, v => s.Hints = v, ref y);
            AddToggle(t, "COLOUR LETTERS", () => s.ColorLetters, v => s.ColorLetters = v, ref y);
            AddToggle(t, "UNLOCK ALL SECTORS", () => s.UnlockAll, v => s.UnlockAll = v, ref y);
            AddToggle(t, "KEEP SCREEN ON", () => s.KeepScreenOn, v => s.KeepScreenOn = v, ref y);
            y -= 0.4f;
            TextButton(t, "RESET PROGRESS", new Vector2(0, y), new Vector2(6.4f, 0.8f), () =>
                App.Push(new ConfirmLayer("Reset progress?", "All solved levels and saved boards\nwill be erased.", () => { App.Save.ResetProgress(); App.ShowOptions(); })));
            TextButton(t, "BACK", new Vector2(0, App.Bottom + 0.6f), new Vector2(3f, 0.8f), () => App.ShowMainMenu());
        }

        void AddToggle(Transform t, string name, Func<bool> get, Action<bool> set, ref float y)
        {
            UIButton b = null;
            b = TextButton(t, "", new Vector2(0, y), new Vector2(6.4f, 0.8f), () =>
            {
                set(!get());
                App.Save.Save();
                App.ApplySettings();
                Refresh();
            }, 30, 0.28f);
            toggles.Add((b, name, get, set));
            y -= 1.0f;
            Refresh();
        }

        void Refresh()
        {
            foreach (var (btn, name, get, _) in toggles)
            {
                btn.Label.text = name + ": " + (get() ? "ON" : "OFF");
                btn.Label.color = get() ? Pal.Text : Pal.Dim;
            }
        }

        public override void OnBack() => App.ShowMainMenu();
    }

    /// <summary>Paged viewer for the original tutorial images.</summary>
    public class TutorialLayer : Layer
    {
        readonly List<string> pages;
        int index;
        SpriteRenderer image;
        TextMesh counter;
        UIButton prev, next;
        readonly Action onDone;

        public TutorialLayer(List<string> pages, Action onDone = null, bool fullScreenScreen = false) : base("Tutorial")
        {
            this.pages = pages;
            this.onDone = onDone;
            var t = Root.transform;
            var view = App.Instance.ViewRect;
            NewBox(t, "Shade", view.center, view.size * 1.1f, new Color(0, 0, 0, fullScreenScreen ? 1f : 0.88f), 200);
            NewSprite(t, "Title", Res.Sprite("interface_help_title"), new Vector2(0, App.Top - 0.75f), 1f, 201);
            image = NewSprite(t, "Page", null, new Vector2(0, 0.25f), 1f, 201);
            prev = ImageButton(t, "interface_help_left", new Vector2(-3.2f, 0.25f), 0.6f, () => Go(-1), 202);
            next = ImageButton(t, "interface_help_right", new Vector2(3.2f, 0.25f), 0.6f, () => Go(1), 202);
            ImageButton(t, "interface_help_dismiss", new Vector2(0, App.Bottom + 0.85f), 0.9f, Done, 202);
            counter = NewText(t, "", new Vector2(0, App.Bottom + 1.95f), 0.22f, Pal.Dim, TextAnchor.MiddleCenter, 203);
            Show();
        }

        void Go(int d) { index = Mathf.Clamp(index + d, 0, pages.Count - 1); Show(); }

        void Show()
        {
            var sp = pages.Count > 0 ? Res.Tutorial(pages[index]) : null;
            image.sprite = sp;
            if (sp != null)
            {
                float s = Mathf.Min(5.6f / sp.bounds.size.x, 7.6f / sp.bounds.size.y);
                image.transform.localScale = new Vector3(s, s, 1);
            }
            prev.Enabled = index > 0; prev.Refresh();
            next.Enabled = index < pages.Count - 1; next.Refresh();
            counter.text = pages.Count > 1 ? $"{index + 1} / {pages.Count}" : "";
        }

        void Done() { Close(); onDone?.Invoke(); }

        public override void OnPointerUp(Vector2 world)
        {
            // tapping the right/left half of the picture also turns pages
            if (Mathf.Abs(world.y - 0.25f) < 3.8f && Mathf.Abs(world.x) < 2.8f) Go(world.x > 0 ? 1 : -1);
        }

        public override void OnBack() => Done();
    }

    public static class HelpPages
    {
        /// <summary>All tutorial pictures shipped with the original game, in play order.</summary>
        public static List<string> All()
        {
            var pages = new List<string>();
            foreach (var n in new[] { "1", "2", "3", "4", "21", "41" })
                for (int k = 1; k <= 3; k++)
                    if (Res.Tutorial($"level{n}.{k}") != null) pages.Add($"level{n}.{k}");
            return pages;
        }
    }

    public sealed class AboutScreen : Layer
    {
        public AboutScreen() : base("About")
        {
            var t = Root.transform;
            NewSprite(t, "Title", Res.Sprite("menu_title_refraction"), new Vector2(0, App.Top - 1.2f), 0.9f, 2);
            string text =
                "Original game: Refraction 3.0\n" +
                "by Textual Indices (Android)\n\n" +
                "Unity rebuild for personal use:\n" +
                "rules ported from the original code,\n" +
                "new laser engine (no stale beams,\n" +
                "no crashes), JSON levels, level packs.\n\n" +
                "HOW TO PLAY\n" +
                "Place mirrors and prisms so every\n" +
                "target is hit by a beam of its colour.\n" +
                "Prism: splits orange / green / purple\n" +
                "into two primaries, or mixes two beams.\n" +
                "Switches open doors of their colour.\n" +
                "Portals carry a beam to the others.\n\n" +
                "Tap a piece to select it, drag it to\n" +
                "move, drag around it to rotate.\n" +
                "Keys: Q/E rotate, Tab toggle beam,\n" +
                "Del remove, Z undo, R reset, Esc menu.";
            NewText(t, text, new Vector2(0, 0.0f), 0.2f, Pal.Text);
            TextButton(t, "BACK", new Vector2(0, App.Bottom + 0.6f), new Vector2(3f, 0.8f), () => App.ShowMainMenu());
        }

        public override void OnBack() => App.ShowMainMenu();
    }

    /// <summary>Simple modal message box.</summary>
    public class MessageLayer : Layer
    {
        public MessageLayer(string title, string body, string button = "OK", Action onOk = null) : base("Message")
        {
            var t = Root.transform;
            var view = App.Instance.ViewRect;
            NewBox(t, "Shade", view.center, view.size * 1.1f, Pal.Shade, 300);
            NewPanel(t, "Panel", new Vector2(0, 0.3f), new Vector2(7.4f, 6.4f), 301);
            NewText(t, title, new Vector2(0, 2.9f), 0.32f, Pal.Text, TextAnchor.MiddleCenter, 302);
            NewText(t, body, new Vector2(0, 0.7f), 0.19f, Pal.Text, TextAnchor.MiddleCenter, 302);
            TextButton(t, button, new Vector2(0, -2.2f), new Vector2(2.6f, 0.8f), () => { Close(); onOk?.Invoke(); }, 303);
        }

        public override void OnBack() => Close();
    }

    public sealed class ConfirmLayer : Layer
    {
        public ConfirmLayer(string title, string body, Action onYes) : base("Confirm")
        {
            var t = Root.transform;
            var view = App.Instance.ViewRect;
            NewBox(t, "Shade", view.center, view.size * 1.1f, Pal.Shade, 300);
            NewPanel(t, "Panel", new Vector2(0, 0.3f), new Vector2(7f, 4.2f), 301);
            NewText(t, title, new Vector2(0, 1.7f), 0.34f, Pal.Text, TextAnchor.MiddleCenter, 302);
            NewText(t, body, new Vector2(0, 0.6f), 0.2f, Pal.Text, TextAnchor.MiddleCenter, 302);
            TextButton(t, "CANCEL", new Vector2(-1.6f, -0.9f), new Vector2(2.6f, 0.8f), Close, 303);
            TextButton(t, "OK", new Vector2(1.6f, -0.9f), new Vector2(2.6f, 0.8f), () => { Close(); onYes(); }, 303);
        }

        public override void OnBack() => Close();
    }
}
