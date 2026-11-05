using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Refraction.Game
{
    /// <summary>Mouse/touch/keyboard abstraction that works with either input backend.</summary>
    public static class PointerInput
    {
        public static bool Down { get; private set; }
        public static bool Held { get; private set; }
        public static bool Up { get; private set; }
        public static Vector2 Screen { get; private set; }
        public static float Scroll { get; private set; }
        static bool wasHeld;

        public static void Poll()
        {
            bool held = false;
            Vector2 pos = Screen;
            float scroll = 0;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                held = true;
                pos = ts.primaryTouch.position.ReadValue();
            }
            else if (Mouse.current != null)
            {
                held = Mouse.current.leftButton.isPressed;
                pos = Mouse.current.position.ReadValue();
                scroll = Mouse.current.scroll.ReadValue().y / 120f;
            }
#else
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                held = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
                pos = t.position;
            }
            else
            {
                held = Input.GetMouseButton(0);
                pos = Input.mousePosition;
                scroll = Input.mouseScrollDelta.y;
            }
#endif
            Down = held && !wasHeld;
            Up = !held && wasHeld;
            Held = held;
            Screen = pos;
            Scroll = scroll;
            wasHeld = held;
        }

        public enum Key { Q, E, R, Z, Tab, Delete, Escape, Space, Left, Right, M, P, N }

        public static bool KeyDown(Key k)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (k)
            {
                case Key.Q: return kb.qKey.wasPressedThisFrame;
                case Key.E: return kb.eKey.wasPressedThisFrame;
                case Key.R: return kb.rKey.wasPressedThisFrame;
                case Key.Z: return kb.zKey.wasPressedThisFrame;
                case Key.Tab: return kb.tabKey.wasPressedThisFrame;
                case Key.Delete: return kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
                case Key.Escape: return kb.escapeKey.wasPressedThisFrame;
                case Key.Space: return kb.spaceKey.wasPressedThisFrame;
                case Key.Left: return kb.leftArrowKey.wasPressedThisFrame;
                case Key.Right: return kb.rightArrowKey.wasPressedThisFrame;
                case Key.M: return kb.mKey.wasPressedThisFrame;
                case Key.P: return kb.pKey.wasPressedThisFrame;
                case Key.N: return kb.nKey.wasPressedThisFrame;
            }
            return false;
#else
            switch (k)
            {
                case Key.Q: return Input.GetKeyDown(KeyCode.Q);
                case Key.E: return Input.GetKeyDown(KeyCode.E);
                case Key.R: return Input.GetKeyDown(KeyCode.R);
                case Key.Z: return Input.GetKeyDown(KeyCode.Z);
                case Key.Tab: return Input.GetKeyDown(KeyCode.Tab);
                case Key.Delete: return Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace);
                case Key.Escape: return Input.GetKeyDown(KeyCode.Escape);   // also the Android back button
                case Key.Space: return Input.GetKeyDown(KeyCode.Space);
                case Key.Left: return Input.GetKeyDown(KeyCode.LeftArrow);
                case Key.Right: return Input.GetKeyDown(KeyCode.RightArrow);
                case Key.M: return Input.GetKeyDown(KeyCode.M);
                case Key.P: return Input.GetKeyDown(KeyCode.P);
                case Key.N: return Input.GetKeyDown(KeyCode.N);
            }
            return false;
#endif
        }
    }

    /// <summary>A clickable rectangle in world space with an optional sprite and label.</summary>
    public sealed class UIButton
    {
        public GameObject Go;
        public SpriteRenderer Image;
        public TextMesh Label;
        public Vector2 Size;
        public Action OnClick;
        public Action OnPress;            // pointer went down on the button
        public bool Enabled = true;
        public bool Visible = true;
        public float Dim = 0.35f;         // alpha multiplier when disabled
        public bool Pressed;
        public float BaseScale = 1f;

        public Vector2 Center => Go.transform.position;

        public bool Contains(Vector2 world)
        {
            if (!Visible || Go == null || !Go.activeInHierarchy) return false;
            var c = Center;
            return Mathf.Abs(world.x - c.x) <= Size.x / 2 && Mathf.Abs(world.y - c.y) <= Size.y / 2;
        }

        public void Refresh()
        {
            if (Go == null) return;
            Go.SetActive(Visible);
            float a = Enabled ? 1f : Dim;
            float press = Pressed && Enabled ? 0.92f : 1f;
            Go.transform.localScale = new Vector3(press * BaseScale, press * BaseScale, 1);
            if (Image != null) { var c = Image.color; c.a = a; Image.color = c; }
            if (Label != null) { var c = Label.color; c.a = a; Label.color = c; }
        }
    }

    /// <summary>
    /// A screen or a modal overlay. Layers are stacked; only the top layer receives input.
    /// </summary>
    public abstract class Layer
    {
        public GameObject Root;
        public readonly List<UIButton> Buttons = new List<UIButton>();
        public bool Closed;
        protected App App => App.Instance;
        UIButton pressed;

        protected Layer(string name, Transform parent = null)
        {
            Root = new GameObject(name);
            if (parent != null) Root.transform.SetParent(parent, false);
        }

        public virtual void Update() { }
        /// <summary>Pointer events not consumed by a button. Return true when handled.</summary>
        public virtual void OnPointerDown(Vector2 world) { }
        public virtual void OnPointerDrag(Vector2 world) { }
        public virtual void OnPointerUp(Vector2 world) { }
        public virtual void OnBack() { }
        public virtual void OnClose() { }

        public void HandleInput(Vector2 world)
        {
            if (PointerInput.Down)
            {
                pressed = null;
                for (int i = Buttons.Count - 1; i >= 0; i--)
                {
                    var b = Buttons[i];
                    if (b.Enabled && b.Contains(world)) { pressed = b; break; }
                }
                if (pressed != null) { pressed.Pressed = true; pressed.Refresh(); pressed.OnPress?.Invoke(); }
                else OnPointerDown(world);
            }
            else if (PointerInput.Held)
            {
                if (pressed != null)
                {
                    bool inside = pressed.Contains(world);
                    if (pressed.Pressed != inside) { pressed.Pressed = inside; pressed.Refresh(); }
                    if (!inside && pressed.OnPress != null)
                    {
                        // a "press" button (piece tray) hands the drag over to the layer
                        pressed.Pressed = false; pressed.Refresh();
                        pressed = null;
                        OnPointerDrag(world);
                    }
                }
                else OnPointerDrag(world);
            }
            if (PointerInput.Up)
            {
                if (pressed != null)
                {
                    var b = pressed;
                    pressed = null;
                    bool inside = b.Contains(world);
                    b.Pressed = false; b.Refresh();
                    if (inside && b.Enabled)
                    {
                        App.PlaySound("menubeep");
                        b.OnClick?.Invoke();
                    }
                }
                else OnPointerUp(world);
            }
        }

        public void Close()
        {
            if (Closed) return;
            Closed = true;
            OnClose();
            if (Root != null) UnityEngine.Object.Destroy(Root);
        }

        // ------------------------------------------------------------------ builders

        public static SpriteRenderer NewSprite(Transform parent, string name, Sprite sprite, Vector2 pos, float scale = 1f, int order = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(scale, scale, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        public static SpriteRenderer NewBox(Transform parent, string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            var sr = NewSprite(parent, name, Res.White, pos, 1, order);
            sr.transform.localScale = new Vector3(size.x, size.y, 1);
            sr.color = color;
            return sr;
        }

        public static SpriteRenderer NewPanel(Transform parent, string name, Vector2 pos, Vector2 size, int order, Color? tint = null)
        {
            var sr = NewSprite(parent, name, Res.Panel, pos, 1, order);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            if (tint.HasValue) sr.color = tint.Value;
            return sr;
        }

        public static TextMesh NewText(Transform parent, string text, Vector2 pos, float height, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, int order = 50)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = Res.Font;
            tm.fontSize = 64;
            tm.characterSize = height * 10f / 64f;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft ? TextAlignment.Left
                : anchor == TextAnchor.MiddleRight || anchor == TextAnchor.UpperRight || anchor == TextAnchor.LowerRight ? TextAlignment.Right : TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = order;
            return tm;
        }

        /// <summary>Button drawn with an image from the original game.</summary>
        public UIButton ImageButton(Transform parent, string sprite, Vector2 pos, float scale, Action onClick, int order = 30)
        {
            var sr = NewSprite(parent, "Btn_" + sprite, Res.Sprite(sprite), pos, scale, order);
            var b = new UIButton { Go = sr.gameObject, Image = sr, OnClick = onClick, BaseScale = scale };
            var s = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            b.Size = s * scale;
            Buttons.Add(b);
            return b;
        }

        /// <summary>Generic text button on a dark panel.</summary>
        public UIButton TextButton(Transform parent, string text, Vector2 pos, Vector2 size, Action onClick, int order = 30, float textHeight = 0.32f)
        {
            var sr = NewPanel(parent, "Btn_" + text, pos, size, order);
            var tm = NewText(sr.transform, text, Vector2.zero, textHeight, new Color(0.88f, 0.88f, 0.9f), TextAnchor.MiddleCenter, order + 1);
            var b = new UIButton { Go = sr.gameObject, Image = sr, Label = tm, Size = size, OnClick = onClick };
            Buttons.Add(b);
            return b;
        }
    }
}
