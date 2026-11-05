using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Game
{
    /// <summary>
    /// Entry point. Builds everything from code, so any scene works (an empty scene is created by the
    /// editor setup script). Coordinates: the playable "page" is 8 x 12.67 world units, centred on the origin
    /// (the original was drawn on an 800 x 1267 px portrait canvas with 100 px cells).
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        public static App Instance { get; private set; }

        public const float PageW = 8f;
        public const float PageH = 12.67f;
        public const float Top = PageH / 2f;       // 6.335
        public const float Bottom = -PageH / 2f;

        public Camera Cam { get; private set; }
        public SaveData Save { get; private set; }
        public Starfield Stars { get; private set; }

        readonly List<Layer> layers = new List<Layer>();
        AudioSource audioSource;
        int lastW, lastH;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance == null) new GameObject("Refraction").AddComponent<App>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
#if ENABLE_LEGACY_INPUT_MANAGER
            Input.multiTouchEnabled = false;
#endif

            Cam = Camera.main;
            if (Cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                Cam = go.AddComponent<Camera>();
            }
            Cam.orthographic = true;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Color.black;
            Cam.transform.position = new Vector3(0, 0, -10);
            Cam.nearClipPlane = 0.1f; Cam.farClipPlane = 100f;
            if (FindFirstObjectByType<AudioListener>() == null) Cam.gameObject.AddComponent<AudioListener>();
            FitCamera();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            Save = SaveData.Load();
            ApplySettings();
            LevelLibrary.Reload();

            Stars = new Starfield(transform);
            Stars.SetVisible(Save.Stars);
            ShowMainMenu();
        }

        public void ApplySettings()
        {
            Screen.sleepTimeout = Save.KeepScreenOn ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            Stars?.SetVisible(Save.Stars);
        }

        void FitCamera()
        {
            lastW = Screen.width; lastH = Screen.height;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 0.5625f;
            Cam.orthographicSize = Mathf.Max(PageH / 2f, PageW / (2f * aspect)) * 1.01f;
        }

        /// <summary>Visible world rectangle (useful for full-screen backgrounds on tall phones).</summary>
        public Rect ViewRect
        {
            get
            {
                float h = Cam.orthographicSize * 2, w = h * Cam.aspect;
                return new Rect(-w / 2, -h / 2, w, h);
            }
        }

        void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH) FitCamera();
            PointerInput.Poll();
            Stars?.Update(ViewRect);

            layers.RemoveAll(l => l.Closed);
            if (layers.Count > 0)
            {
                var top = layers[layers.Count - 1];
                if (PointerInput.KeyDown(PointerInput.Key.Escape)) top.OnBack();
                else
                {
                    Vector2 world = Cam.ScreenToWorldPoint(new Vector3(PointerInput.Screen.x, PointerInput.Screen.y, 10));
                    top.HandleInput(world);
                }
            }
            foreach (var l in layers.ToArray()) if (!l.Closed) l.Update();
        }

        // ------------------------------------------------------------------ layers

        public T Push<T>(T layer) where T : Layer
        {
            layers.Add(layer);
            return layer;
        }

        /// <summary>Close every layer and show this one.</summary>
        public T Replace<T>(T layer) where T : Layer
        {
            foreach (var l in layers) if (l != layer) l.Close();
            layers.Clear();
            layers.Add(layer);
            return layer;
        }

        public bool IsTop(Layer l) => layers.Count > 0 && layers[layers.Count - 1] == l;

        public void PlaySound(string name, float volume = 0.8f)
        {
            if (!Save.Sound) return;
            var clip = Res.Clip(name);
            if (clip != null) audioSource.PlayOneShot(clip, volume);
        }

        // ------------------------------------------------------------------ navigation

        public void ShowMainMenu() => Replace(new MainMenuScreen());
        public void ShowSectors() => Replace(new SectorScreen());
        public void ShowLevels(int sector) => Replace(new LevelSelectScreen(sector));
        public void ShowCustom() => Replace(new CustomLevelsScreen());
        public void ShowOptions() => Replace(new OptionsScreen());
        public void ShowHelp() => Replace(new TutorialLayer(HelpPages.All(), ShowMainMenu, true));
        public void ShowAbout() => Replace(new AboutScreen());

        public void Play(LevelEntry entry)
        {
            if (entry == null || !entry.Ok) return;
            Save.LastPlayed = entry.Id;
            Save.Save();
            Replace(new GameScreen(entry));
        }

        /// <summary>Back from a level to the list it came from.</summary>
        public void ShowListFor(LevelEntry entry)
        {
            if (entry == null) ShowMainMenu();
            else if (entry.Pack == "main") ShowLevels(entry.Index / LevelLibrary.SectorSize);
            else ShowCustom();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Save?.Save();
        }

        void OnApplicationQuit() => Save?.Save();
    }

    /// <summary>Three layers of drifting stars, like the original StarSystem.</summary>
    public sealed class Starfield
    {
        readonly Transform root;
        readonly List<Transform>[] stars = new List<Transform>[3];
        readonly float[] speed = { 0.12f, 0.3f, 0.6f };
        readonly string[] sprite = { "star1px", "star3px", "star5px" };
        readonly int[] count = { 70, 35, 15 };
        Rect area;
        bool visible = true;

        public Starfield(Transform parent)
        {
            root = new GameObject("Stars").transform;
            root.SetParent(parent, false);
            for (int l = 0; l < 3; l++)
            {
                stars[l] = new List<Transform>();
                for (int i = 0; i < count[l]; i++)
                {
                    var sr = Layer.NewSprite(root, "star", Res.Sprite(sprite[l]), new Vector2(Random.Range(-6f, 6f), Random.Range(-10f, 10f)), 1f, -100 + l);
                    sr.color = new Color(1, 1, 1, 0.35f + 0.25f * l);
                    stars[l].Add(sr.transform);
                }
            }
        }

        public void SetVisible(bool v)
        {
            visible = v;
            root.gameObject.SetActive(v);
        }

        public void Update(Rect view)
        {
            if (!visible) return;
            if (area.width < view.width || area.height < view.height || area.center != view.center)
            {
                area = view;
                foreach (var l in stars) foreach (var t in l)
                        if (!view.Contains(t.localPosition)) t.localPosition = new Vector2(Random.Range(view.xMin, view.xMax), Random.Range(view.yMin, view.yMax));
            }
            float dt = Time.deltaTime;
            for (int l = 0; l < 3; l++)
                foreach (var t in stars[l])
                {
                    var p = t.localPosition;
                    p.y -= speed[l] * dt;
                    if (p.y < area.yMin) { p.y = area.yMax; p.x = Random.Range(area.xMin, area.xMax); }
                    t.localPosition = p;
                }
        }
    }
}
