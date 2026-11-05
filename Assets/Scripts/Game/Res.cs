using System.Collections.Generic;
using Refraction.Core;
using UnityEngine;

namespace Refraction.Game
{
    /// <summary>Loads and caches sprites, fonts and sounds from Resources/.</summary>
    public static class Res
    {
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static Font font;
        static Sprite white, panel, circle;

        public const float PixelsPerUnit = 100f;

        public static Sprite Sprite(string name)
        {
            if (sprites.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>("Sprites/" + name);
            if (s == null)
            {
                // Fallback when the texture was not imported as a Sprite (e.g. the import hook did not run).
                var tex = Resources.Load<Texture2D>("Sprites/" + name);
                if (tex != null)
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
                }
                else Debug.LogWarning("Refraction: missing sprite " + name);
            }
            sprites[name] = s;
            return s;
        }

        public static Sprite Tutorial(string name)
        {
            string key = "tut:" + name;
            if (sprites.TryGetValue(key, out var s)) return s;
            s = Resources.Load<Sprite>("Tutorials/" + name);
            if (s == null)
            {
                var tex = Resources.Load<Texture2D>("Tutorials/" + name);
                if (tex != null) s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            }
            sprites[key] = s;
            return s;
        }

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>("Fonts/xirod");
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Sounds/" + name);
            clips[name] = c;
            return c;
        }

        /// <summary>1x1 white sprite (lines, fades, solid boxes).</summary>
        public static Sprite White
        {
            get
            {
                if (white != null) return white;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px); tex.Apply();
                white = UnityEngine.Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                return white;
            }
        }

        /// <summary>Dark rounded panel with a light border, 9-sliced (generic buttons and dialogs).</summary>
        public static Sprite Panel
        {
            get
            {
                if (panel != null) return panel;
                const int n = 48, border = 12;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(border - x - 0.5f, x + 0.5f - (n - border)));
                        float dy = Mathf.Max(0, Mathf.Max(border - y - 0.5f, y + 0.5f - (n - border)));
                        float r = Mathf.Sqrt(dx * dx + dy * dy);          // distance into the rounded corner
                        float edge = border - r;                          // distance to the outside
                        float a = Mathf.Clamp01(edge);
                        bool rim = edge < 2.5f;
                        byte v = rim ? (byte)150 : (byte)38;
                        px[y * n + x] = new Color32(v, v, (byte)(v + 6), (byte)(a * 255));
                    }
                tex.SetPixels32(px); tex.Apply();
                panel = UnityEngine.Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                return panel;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(n / 2f - d) * 255));
                    }
                tex.SetPixels32(px); tex.Apply();
                circle = UnityEngine.Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return circle;
            }
        }

        // ------------------------------------------------------------------ colour helpers

        /// <summary>Sprite name suffix for a laser colour ("Pigment" palette of the original).</summary>
        public static string ColorSuffix(int c)
        {
            switch (c)
            {
                case LaserColor.Blue: return "blue";
                case LaserColor.Yellow: return "yellow";
                case LaserColor.Red: return "red";
                case LaserColor.Green: return "green";
                case LaserColor.Orange: return "orange";
                case LaserColor.Purple: return "purple";
                default: return "white";
            }
        }

        public static Color Tint(int c)
        {
            switch (c)
            {
                case LaserColor.Blue: return new Color(0.25f, 0.55f, 1f);
                case LaserColor.Yellow: return new Color(1f, 0.92f, 0.2f);
                case LaserColor.Red: return new Color(1f, 0.2f, 0.2f);
                case LaserColor.Green: return new Color(0.2f, 0.9f, 0.3f);
                case LaserColor.Orange: return new Color(1f, 0.6f, 0.1f);
                case LaserColor.Purple: return new Color(0.75f, 0.3f, 1f);
                default: return Color.white;
            }
        }

        public static string Letter(int c)
        {
            switch (c)
            {
                case LaserColor.Blue: return "B";
                case LaserColor.Yellow: return "Y";
                case LaserColor.Red: return "R";
                case LaserColor.Green: return "G";
                case LaserColor.Orange: return "O";
                case LaserColor.Purple: return "P";
                default: return "W";
            }
        }
    }
}
