using System;
using System.Collections.Generic;
using Refraction.Core;
using UnityEngine;

namespace Refraction.Game
{
    /// <summary>The play screen: board rendering, piece tray, input and win handling.</summary>
    public sealed class GameScreen : Layer
    {
        // layout (world units)
        const float TopBarH = 1f, TrayH = 1.67f;
        static readonly float GridTop = App.Top - TopBarH;          // 5.335
        static readonly float GridBottom = App.Bottom + TrayH;      // -4.665

        readonly LevelEntry entry;
        readonly Level level;
        readonly Board board;
        readonly SaveData save;

        float cell;
        Vector2 gridTL;     // world position of the grid's top-left corner
        Transform boardRoot, beamRoot, pieceRoot, fxRoot;

        // static object visuals
        sealed class ObjVis
        {
            public LevelObject O;
            public int Index;
            public SpriteRenderer Main, Glow, Ring, Top;
            public TextMesh Letter;
        }
        readonly List<ObjVis> objVis = new List<ObjVis>();

        sealed class BeamVis
        {
            public GameObject Go;
            public SpriteRenderer Body, CapStart, CapEnd;
            public float BaseAlpha = 1f;
        }
        readonly List<BeamVis> beamPool = new List<BeamVis>();
        int beamsShown;

        readonly List<GameObject> pieceVis = new List<GameObject>();
        readonly List<SpriteRenderer> pieceGlows = new List<SpriteRenderer>();
        SpriteRenderer selOverlay, selArrow, ghost;

        // top bar / tray
        TextMesh titleText, mirrorCount, prismCount, statusText;
        readonly List<SpriteRenderer> progressBits = new List<SpriteRenderer>();
        UIButton btnPrism, btnMirror, btnRotL, btnRotR, btnToggle, btnDelete, btnUndo;

        // interaction state
        int selected = -1;
        int selectedOutput;
        PieceKind? armed;
        PieceKind? trayDrag;
        enum Mode { None, PendingSelect, PendingToggle, Move, Rotate }
        Mode mode;
        Vector2 downWorld;
        int downCell = -1;
        float rotRef;          // reference angle (degrees) for drag-rotation
        float lastTapTime;
        int lastTapPiece = -1;

        readonly Stack<List<Piece>> undo = new Stack<List<Piece>>();
        bool won;
        float wonAt = -1;
        bool winShown;
        bool dirty;
        float saveTimer;
        float animT;

        public GameScreen(LevelEntry entry) : base("Game")
        {
            this.entry = entry;
            level = entry.Level;
            save = App.Instance.Save;
            List<Piece> saved = null;
            if (save.Boards.TryGetValue(entry.Id, out var json)) saved = Board.PiecesFromJson(json);
            board = new Board(level, saved);
            // drop saved pieces that no longer fit (level file edited since)
            for (int i = board.Pieces.Count - 1; i >= 0; i--)
            {
                var p = board.Pieces[i];
                if (!board.IsFreeCell(p.X, p.Y, i)) board.Pieces.RemoveAt(i);
            }
            while (board.Remaining(PieceKind.Mirror) < 0) board.Pieces.RemoveAt(board.Pieces.FindLastIndex(p => p.Kind == PieceKind.Mirror));
            while (board.Remaining(PieceKind.Prism) < 0) board.Pieces.RemoveAt(board.Pieces.FindLastIndex(p => p.Kind == PieceKind.Prism));
            board.Recompute();
            won = board.Result.Won;
            winShown = won;   // do not pop the win dialog for a board that was already solved

            BuildLayout();
            BuildStatic();
            Refresh();

            if (save.Hints && level.Tutorial.Count > 0 && !save.TutorialSeen.Contains(entry.Id))
            {
                save.TutorialSeen.Add(entry.Id);
                save.Save();
                App.Instance.Push(new TutorialLayer(level.Tutorial));
            }
        }

        // ================================================================== layout

        Vector2 CellCenter(int x, int y) => gridTL + new Vector2((x - 0.5f) * cell, -(y - 0.5f) * cell);
        Vector2 GridPoint(double gx, double gy) => gridTL + new Vector2((float)(gx - 0.5) * cell, -(float)(gy - 0.5) * cell);

        bool CellAt(Vector2 world, out int x, out int y)
        {
            x = Mathf.FloorToInt((world.x - gridTL.x) / cell) + 1;
            y = Mathf.FloorToInt((gridTL.y - world.y) / cell) + 1;
            return level.InBounds(x, y);
        }

        void BuildLayout()
        {
            var t = Root.transform;
            cell = Mathf.Min(App.PageW / level.Width, (GridTop - GridBottom) / level.Height);
            float gw = cell * level.Width, gh = cell * level.Height;
            gridTL = new Vector2(-gw / 2, (GridTop + GridBottom) / 2 + gh / 2);

            boardRoot = new GameObject("Board").transform; boardRoot.SetParent(t, false);
            beamRoot = new GameObject("Beams").transform; beamRoot.SetParent(t, false);
            pieceRoot = new GameObject("Pieces").transform; pieceRoot.SetParent(t, false);
            fxRoot = new GameObject("Fx").transform; fxRoot.SetParent(t, false);

            // grid lines
            var lineCol = new Color(0.5f, 0.5f, 0.5f, 60f / 255f);
            for (int x = 0; x <= level.Width; x++)
                NewBox(boardRoot, "vline", gridTL + new Vector2(x * cell, -gh / 2), new Vector2(0.015f, gh), lineCol, 1);
            for (int y = 0; y <= level.Height; y++)
                NewBox(boardRoot, "hline", gridTL + new Vector2(gw / 2, -y * cell), new Vector2(gw, 0.015f), lineCol, 1);

            // top bar
            NewSprite(t, "TopBar", Res.Sprite("interface_layout_toptoolbar"), new Vector2(0, App.Top - 0.5f), 1f, 20);
            titleText = NewText(t, entry.Title, new Vector2(-3.85f, App.Top - 0.5f), 0.34f, Pal.Text, TextAnchor.MiddleLeft, 22);
            if (titleText.text.Length > 8) titleText.characterSize *= 8f / titleText.text.Length;
            if (level.Tutorial.Count > 0)
            {
                var q = ImageButton(t, "interface_object_questionmark", new Vector2(-2.05f, App.Top - 0.5f), 0.55f,
                    () => App.Push(new TutorialLayer(level.Tutorial)), 22);
                q.Size = new Vector2(0.8f, 0.9f);
            }
            var holder = NewSprite(t, "Targets", Res.Sprite("interface_layout_checkpointholder"), new Vector2(0.25f, App.Top - 0.5f), 0.72f, 21);
            int targets = 0;
            foreach (var o in level.Objects) if (o.Kind == ObjKind.Checkpoint) targets++;
            float barW = 3.6f - 0.3f, segW = targets > 0 ? barW / targets : barW;
            int k = 0;
            foreach (var o in level.Objects)
            {
                if (o.Kind != ObjKind.Checkpoint) continue;
                var sr = NewSprite(t, "bit", Res.Sprite($"markedcheckpoint_{Res.ColorSuffix(o.Color)}_1px"),
                    new Vector2(0.25f - barW / 2 + segW * (k + 0.5f), App.Top - 0.5f), 1f, 22);
                sr.transform.localScale = new Vector3((segW - 0.04f) / 0.01f, 1.6f, 1);
                progressBits.Add(sr);
                k++;
            }
            btnUndo = TextButton(t, "UNDO", new Vector2(2.75f, App.Top - 0.5f), new Vector2(1.05f, 0.62f), Undo, 22, 0.17f);
            var menu = ImageButton(t, "interface_button_menu", new Vector2(3.65f, App.Top - 0.5f), 1f, OpenMenu, 22);
            menu.Size = new Vector2(0.7f, 0.95f);

            // tray
            NewSprite(t, "Tray", Res.Sprite("interface_layout_bottomtoolbar"), new Vector2(0, App.Bottom + TrayH / 2), 1f, 20);
            float ty = App.Bottom + TrayH / 2, step = App.PageW / 6f, s = 0.86f;
            btnPrism = ImageButton(t, "interface_button_prism", new Vector2(-step * 2.5f, ty), s, () => Arm(PieceKind.Prism), 22);
            btnPrism.OnPress = () => trayDrag = PieceKind.Prism;
            btnMirror = ImageButton(t, "interface_button_mirror", new Vector2(-step * 1.5f, ty), s, () => Arm(PieceKind.Mirror), 22);
            btnMirror.OnPress = () => trayDrag = PieceKind.Mirror;
            btnRotL = ImageButton(t, "interface_button_rotate_left_new", new Vector2(-step * 0.5f, ty), s, () => RotateSelected(+1), 22);
            btnRotR = ImageButton(t, "interface_button_rotate_right_new", new Vector2(step * 0.5f, ty), s, () => RotateSelected(-1), 22);
            btnToggle = ImageButton(t, "interface_button_toggle_new", new Vector2(step * 1.5f, ty), s, ToggleOutput, 22);
            btnDelete = ImageButton(t, "interface_button_delete_new", new Vector2(step * 2.5f, ty), s, DeleteSelected, 22);
            prismCount = NewText(t, "", new Vector2(-step * 2.5f + 0.42f, ty + 0.55f), 0.2f, Pal.Text, TextAnchor.MiddleCenter, 24);
            mirrorCount = NewText(t, "", new Vector2(-step * 1.5f + 0.42f, ty + 0.55f), 0.2f, Pal.Text, TextAnchor.MiddleCenter, 24);
            statusText = NewText(t, "", new Vector2(0, GridBottom - 0.14f), 0.15f, Pal.Dim, TextAnchor.MiddleCenter, 24);

            selOverlay = NewSprite(fxRoot, "SelOverlay", Res.Sprite("interface_overlay_rotate_grey"), Vector2.zero, cell * 0.8f, 4);
            selOverlay.color = new Color(1, 1, 1, 0.55f);
            selArrow = NewSprite(fxRoot, "SelArrow", null, Vector2.zero, cell * 0.6f, 12);
            ghost = NewSprite(fxRoot, "Ghost", null, Vector2.zero, cell, 15);
        }

        void BuildStatic()
        {
            for (int i = 0; i < level.Objects.Count; i++)
            {
                var o = level.Objects[i];
                if (!level.InBounds(o.X, o.Y)) continue;
                var v = new ObjVis { O = o, Index = i };
                var pos = CellCenter(o.X, o.Y);
                string c = Res.ColorSuffix(o.Color);
                switch (o.Kind)
                {
                    case ObjKind.Wall:
                        v.Main = NewSprite(boardRoot, "wall", Res.Sprite("wall_standard"), pos, cell, 2); break;
                    case ObjKind.Door:
                        v.Main = NewSprite(boardRoot, "door", Res.Sprite("wall_" + c), pos, cell, 2); break;
                    case ObjKind.Emitter:
                        v.Main = NewSprite(boardRoot, "emitter", Res.Sprite("emitter_" + c), pos, cell, 7);
                        v.Main.transform.localRotation = Quaternion.Euler(0, 0, o.Dir - 90);
                        break;
                    case ObjKind.Checkpoint:
                        v.Glow = NewSprite(boardRoot, "glow", Res.Sprite("glow_" + c), pos, cell, 8);
                        v.Main = NewSprite(boardRoot, "target", Res.Sprite("chk_" + c), pos, cell, 9);
                        break;
                    case ObjKind.Switch:
                        v.Main = NewSprite(boardRoot, "switch", Res.Sprite("switch_" + c), pos, cell, 9); break;
                    case ObjKind.Portal:
                        v.Main = NewSprite(boardRoot, "portal", Res.Sprite("wormhole_base"), pos, cell, 3);
                        v.Ring = NewSprite(boardRoot, "ring", Res.Sprite("wormhole_ring"), pos, cell, 4);
                        v.Top = NewSprite(boardRoot, "top", Res.Sprite("wormhole_top"), pos, cell, 6);
                        break;
                }
                if (save.ColorLetters && (o.Kind == ObjKind.Door || o.Kind == ObjKind.Switch || o.Kind == ObjKind.Checkpoint || o.Kind == ObjKind.Emitter))
                    v.Letter = NewText(boardRoot, Res.Letter(o.Color), pos + new Vector2(cell * 0.3f, -cell * 0.3f), cell * 0.22f, Color.white, TextAnchor.MiddleCenter, 11);
                objVis.Add(v);
            }
        }

        // ================================================================== refresh after any change

        void Changed()
        {
            dirty = true;
            saveTimer = 0.8f;
            if (selected >= board.Pieces.Count) selected = -1;
            var r = board.Result;
            if (r.Won && !won)
            {
                won = true;
                wonAt = Time.time;
                RecordWin();
            }
            else if (!r.Won) { won = false; winShown = false; }
            Refresh();
        }

        void RecordWin()
        {
            save.Beaten.Add(entry.Id);
            int used = board.Pieces.Count;
            if (!save.BestPieces.TryGetValue(entry.Id, out int best) || used < best) save.BestPieces[entry.Id] = used;
            save.Boards[entry.Id] = Board.PiecesToJson(board.Pieces);
            save.Save();
            dirty = false;
        }

        void Refresh()
        {
            var r = board.Result;

            // static objects
            foreach (var v in objVis)
            {
                var o = v.O;
                switch (o.Kind)
                {
                    case ObjKind.Door:
                        v.Main.color = new Color(1, 1, 1, r.IsDoorOpen(o) ? 75f / 255f : 1f); break;
                    case ObjKind.Switch:
                        v.Main.color = new Color(1, 1, 1, r.SwitchOn[v.Index] ? 1f : 75f / 255f); break;
                    case ObjKind.Checkpoint:
                        bool lit = r.TargetLit[v.Index];
                        v.Glow.enabled = lit;
                        v.Main.color = new Color(1, 1, 1, lit ? 1f : 75f / 255f);
                        if (!lit) v.Main.transform.localRotation = Quaternion.identity;
                        break;
                }
            }
            int k = 0;
            for (int i = 0; i < level.Objects.Count; i++)
                if (level.Objects[i].Kind == ObjKind.Checkpoint && k < progressBits.Count)
                    progressBits[k++].color = new Color(1, 1, 1, r.TargetLit[i] ? 1f : 0.12f);

            RebuildPieces();
            RebuildBeams();
            RefreshTray();
        }

        void RebuildPieces()
        {
            foreach (var g in pieceVis) UnityEngine.Object.Destroy(g);
            pieceVis.Clear();
            pieceGlows.Clear();
            var r = board.Result;
            for (int i = 0; i < board.Pieces.Count; i++)
            {
                var p = board.Pieces[i];
                var pos = CellCenter(p.X, p.Y);
                var root = new GameObject("piece").transform;
                root.SetParent(pieceRoot, false);
                root.localPosition = pos;
                bool active = r.PieceActive[i];
                if (p.Kind == PieceKind.Mirror)
                {
                    if (r.MirrorOutput[i] >= 0) pieceGlows.Add(NewSprite(root, "glow", Res.Sprite("glow_white_small"), Vector2.zero, cell, 7));
                    var m = NewSprite(root, "mirror", Res.Sprite("interface_object_mirror3"), Vector2.zero, cell, 10);
                    m.transform.localRotation = Quaternion.Euler(0, 0, (float)p.Angle - 135f);
                    if (!active) m.color = new Color(1, 1, 1, 0.35f);
                }
                else
                {
                    var info = r.Prisms[i];
                    if (info != null && info.Inputs.Count > 0) pieceGlows.Add(NewSprite(root, "glow", Res.Sprite("glow_white"), Vector2.zero, cell, 7));
                    var pr = NewSprite(root, "prism", Res.Sprite("interface_object_prism"), Vector2.zero, cell, 10);
                    if (!active) pr.color = new Color(1, 1, 1, 0.35f);
                }
                pieceVis.Add(root.gameObject);
            }

            // selection
            bool sel = selected >= 0 && selected < board.Pieces.Count;
            selOverlay.enabled = sel;
            selArrow.enabled = false;
            if (sel)
            {
                var p = board.Pieces[selected];
                selOverlay.transform.localPosition = CellCenter(p.X, p.Y);
                var info = r.Prisms[selected];
                if (p.Kind == PieceKind.Prism && info != null && info.Outputs.Count > 0)
                {
                    selectedOutput = Mathf.Clamp(selectedOutput, 0, info.Outputs.Count - 1);
                    var b = r.Beams[info.Outputs[selectedOutput]];
                    Dir.Step(b.Dir, out int dx, out int dy);
                    var dirv = new Vector2(dx, -dy).normalized;
                    selArrow.enabled = true;
                    selArrow.sprite = Res.Sprite("arrow_" + Res.ColorSuffix(b.Color));
                    selArrow.transform.localPosition = CellCenter(p.X, p.Y) + dirv * cell * 0.72f;
                    selArrow.transform.localRotation = Quaternion.Euler(0, 0, b.Dir - 90);
                }
            }
        }

        void RebuildBeams()
        {
            var r = board.Result;
            beamsShown = 0;
            foreach (var b in r.Beams)
            {
                var start = GridPoint(b.StartX, b.StartY);
                var end = GridPoint(b.EndPx, b.EndPy);
                float len = Vector2.Distance(start, end);
                if (len < 0.01f) continue;
                var v = GetBeam(beamsShown++);
                string c = Res.ColorSuffix(b.Color);
                v.Body.sprite = Res.Sprite("laser_" + c);
                v.CapStart.sprite = v.CapEnd.sprite = Res.Sprite("laser_" + c + "_end");
                float cap = Mathf.Min(0.5f * cell, len / 2f);
                float body = Mathf.Max(0, len - 2 * cap);
                v.Go.SetActive(true);
                v.Go.transform.localPosition = start;
                v.Go.transform.localRotation = Quaternion.Euler(0, 0, b.Dir);
                // children are laid out along +x
                v.Body.transform.localPosition = new Vector3(cap + body / 2, 0);
                v.Body.transform.localScale = new Vector3(body / 0.01f, cell, 1);
                float capScale = cap / 0.5f;
                v.CapStart.transform.localPosition = new Vector3(cap / 2, 0);
                v.CapStart.transform.localScale = new Vector3(capScale, cell, 1);
                v.CapEnd.transform.localPosition = new Vector3(cap + body + cap / 2, 0);
                v.CapEnd.transform.localScale = new Vector3(capScale, cell, 1);
                bool hl = false;
                if (selected >= 0 && selected < board.Pieces.Count && r.Prisms[selected] != null)
                {
                    var info = r.Prisms[selected];
                    if (info.Outputs.Count > 0 && info.Outputs[Mathf.Clamp(selectedOutput, 0, info.Outputs.Count - 1)] == b.Index) hl = true;
                }
                v.BaseAlpha = hl ? 1.25f : 1f;
            }
            for (int i = beamsShown; i < beamPool.Count; i++) beamPool[i].Go.SetActive(false);
        }

        BeamVis GetBeam(int i)
        {
            while (beamPool.Count <= i)
            {
                var go = new GameObject("beam");
                go.transform.SetParent(beamRoot, false);
                var v = new BeamVis { Go = go };
                v.Body = NewSprite(go.transform, "body", null, Vector2.zero, 1, 5);
                v.CapStart = NewSprite(go.transform, "capS", null, Vector2.zero, 1, 5);
                v.CapStart.flipX = true;
                v.CapEnd = NewSprite(go.transform, "capE", null, Vector2.zero, 1, 5);
                beamPool.Add(v);
            }
            return beamPool[i];
        }

        void RefreshTray()
        {
            int pm = board.Remaining(PieceKind.Prism), mm = board.Remaining(PieceKind.Mirror);
            btnPrism.Enabled = pm > 0; btnMirror.Enabled = mm > 0;
            prismCount.text = level.MaxPrisms > 0 ? pm.ToString() : "";
            mirrorCount.text = level.MaxMirrors > 0 ? mm.ToString() : "";
            bool sel = selected >= 0;
            btnRotL.Enabled = btnRotR.Enabled = btnDelete.Enabled = sel;
            var info = sel ? board.Result.Prisms[selected] : null;
            btnToggle.Enabled = info != null && info.Outputs.Count == 2;
            btnUndo.Enabled = undo.Count > 0;
            btnPrism.Image.color = armed == PieceKind.Prism ? new Color(0.6f, 1f, 0.6f) : Color.white;
            btnMirror.Image.color = armed == PieceKind.Mirror ? new Color(0.6f, 1f, 0.6f) : Color.white;
            foreach (var b in new[] { btnPrism, btnMirror, btnRotL, btnRotR, btnToggle, btnDelete, btnUndo }) b.Refresh();
            var r = board.Result;
            string st = $"targets {r.TargetsLit}/{r.TargetsTotal}";
            if (armed.HasValue) st = $"tap an empty cell to place the {(armed == PieceKind.Prism ? "prism" : "mirror")}";
            else if (selected >= 0 && board.Pieces[selected].Kind == PieceKind.Prism && info != null && info.Outputs.Count == 0) st = "this prism gets no beam yet";
            statusText.text = st;
        }

        // ================================================================== actions

        void PushUndo()
        {
            undo.Push(board.Snapshot());
            if (undo.Count > 200)
            {
                var arr = undo.ToArray();
                undo.Clear();
                for (int i = 150; i >= 0; i--) undo.Push(arr[i]);
            }
        }

        void Undo()
        {
            if (undo.Count == 0) return;
            var prev = undo.Pop();
            board.Pieces.Clear();
            board.Pieces.AddRange(prev);
            board.Recompute();
            selected = -1;
            Changed();
        }

        void Arm(PieceKind k)
        {
            trayDrag = null;
            armed = armed == k ? (PieceKind?)null : k;
            selected = -1;
            Refresh();
        }

        bool Place(PieceKind k, int x, int y)
        {
            if (!board.CanAdd(k, x, y)) return false;
            PushUndo();
            var p = k == PieceKind.Mirror ? Piece.Mirror(x, y, 135) : Piece.Prism(x, y);
            selected = board.Add(p);
            selectedOutput = 0;
            App.PlaySound("dropobject");
            Changed();
            return true;
        }

        void RotateSelected(int dir) => RotateSelected(dir, true);

        void RotateSelected(int dir, bool recordUndo)
        {
            if (selected < 0) return;
            var p = board.Pieces[selected];
            if (recordUndo) PushUndo();
            if (p.Kind == PieceKind.Mirror)
            {
                board.RotateMirror(selected, dir);
            }
            else if (!board.RotatePrismOutput(selected, selectedOutput, dir))
            {
                if (recordUndo) undo.Pop();
                return;
            }
            App.PlaySound("rotate", 0.6f);
            Changed();
        }

        void ToggleOutput()
        {
            if (selected < 0) return;
            var info = board.Result.Prisms[selected];
            if (info == null || info.Outputs.Count < 2) return;
            selectedOutput = (selectedOutput + 1) % info.Outputs.Count;
            Refresh();
        }

        void DeleteSelected()
        {
            if (selected < 0) return;
            PushUndo();
            board.Remove(selected);
            selected = -1;
            Changed();
        }

        void ResetBoard()
        {
            if (board.Pieces.Count == 0) return;
            PushUndo();
            board.Clear();
            selected = -1;
            Changed();
        }

        void LoadSolution()
        {
            if (level.Solution == null) return;
            PushUndo();
            board.Pieces.Clear();
            foreach (var p in level.Solution) board.Pieces.Add(p.Clone());
            board.Recompute();
            selected = -1;
            Changed();
        }

        void OpenMenu()
        {
            App.Push(new GameMenuLayer(this));
        }

        // ================================================================== input

        public override void OnPointerDown(Vector2 world)
        {
            downWorld = world;
            mode = Mode.None;
            if (!CellAt(world, out int x, out int y))
            {
                if (world.y < GridTop && world.y > GridBottom) { selected = -1; armed = null; Refresh(); }
                return;
            }
            int pi = board.PieceIndexAt(x, y);
            if (armed.HasValue && pi < 0)
            {
                var k = armed.Value;
                if (Place(k, x, y)) { armed = null; Refresh(); }
                return;
            }
            if (pi >= 0)
            {
                // double tap removes a piece (as in the original)
                if (pi == lastTapPiece && Time.time - lastTapTime < 0.3f && pi == selected)
                {
                    lastTapPiece = -1;
                    DeleteSelected();
                    return;
                }
                lastTapPiece = pi; lastTapTime = Time.time;
                mode = pi == selected && board.Pieces[pi].Kind == PieceKind.Prism ? Mode.PendingToggle : Mode.PendingSelect;
                if (pi != selected) { selected = pi; selectedOutput = 0; armed = null; Refresh(); }
                downCell = pi;
                return;
            }
            if (selected >= 0)
            {
                var sp = board.Pieces[selected];
                var c = CellCenter(sp.X, sp.Y);
                if (Vector2.Distance(world, c) < cell * 2.3f)
                {
                    mode = Mode.Rotate;
                    rotRef = Mathf.Atan2(world.y - c.y, world.x - c.x) * Mathf.Rad2Deg;
                    PushUndo();
                    return;
                }
            }
            selected = -1;
            armed = null;
            Refresh();
        }

        public override void OnPointerDrag(Vector2 world)
        {
            if (trayDrag.HasValue)
            {
                ShowGhost(trayDrag.Value, world, -1);
                return;
            }
            switch (mode)
            {
                case Mode.PendingSelect:
                case Mode.PendingToggle:
                    if (Vector2.Distance(world, downWorld) > cell * 0.45f && selected >= 0)
                    {
                        mode = Mode.Move;
                        ShowGhost(board.Pieces[selected].Kind, world, selected);
                    }
                    break;
                case Mode.Move:
                    ShowGhost(board.Pieces[selected].Kind, world, selected);
                    break;
                case Mode.Rotate:
                    {
                        if (selected < 0) break;
                        var sp = board.Pieces[selected];
                        var c = CellCenter(sp.X, sp.Y);
                        if (Vector2.Distance(world, c) < cell * 0.25f) break;
                        float a = Mathf.Atan2(world.y - c.y, world.x - c.x) * Mathf.Rad2Deg;
                        float delta = Mathf.DeltaAngle(rotRef, a);
                        float step = sp.Kind == PieceKind.Mirror ? 22.5f : 45f;
                        if (Mathf.Abs(delta) >= step * 0.8f)
                        {
                            RotateSelected(delta > 0 ? +1 : -1, false);
                            rotRef = a;
                        }
                        break;
                    }
            }
        }

        public override void OnPointerUp(Vector2 world)
        {
            ghost.enabled = false;
            if (trayDrag.HasValue)
            {
                var k = trayDrag.Value;
                trayDrag = null;
                if (CellAt(world, out int x, out int y)) Place(k, x, y);
                return;
            }
            switch (mode)
            {
                case Mode.Move:
                    if (selected >= 0 && CellAt(world, out int mx, out int my) && board.IsFreeCell(mx, my, selected))
                    {
                        var p = board.Pieces[selected];
                        if (p.X != mx || p.Y != my)
                        {
                            PushUndo();
                            board.Move(selected, mx, my);
                            App.PlaySound("dropobject");
                            Changed();
                        }
                    }
                    else Refresh();
                    break;
                case Mode.PendingToggle:
                    ToggleOutput();
                    break;
                case Mode.Rotate:
                    // drop the undo snapshot if nothing actually rotated
                    if (undo.Count > 0 && SamePieces(undo.Peek(), board.Pieces)) undo.Pop();
                    RefreshTray();
                    break;
            }
            mode = Mode.None;
        }

        static bool SamePieces(List<Piece> a, List<Piece> b)
        {
            if (a.Count != b.Count) return false;
            return Board.PiecesToJson(a) == Board.PiecesToJson(b);
        }

        void ShowGhost(PieceKind k, Vector2 world, int ignore)
        {
            if (!CellAt(world, out int x, out int y)) { ghost.enabled = false; return; }
            ghost.enabled = true;
            ghost.sprite = Res.Sprite(k == PieceKind.Mirror ? "interface_object_mirror3" : "interface_object_prism");
            ghost.transform.localPosition = CellCenter(x, y);
            ghost.transform.localRotation = k == PieceKind.Mirror && ignore >= 0
                ? Quaternion.Euler(0, 0, (float)board.Pieces[ignore].Angle - 135f) : Quaternion.identity;
            bool ok = ignore >= 0 ? board.IsFreeCell(x, y, ignore) : board.CanAdd(k, x, y);
            ghost.color = ok ? new Color(1, 1, 1, 0.6f) : new Color(1, 0.3f, 0.3f, 0.5f);
        }

        public override void OnBack() => OpenMenu();

        // ================================================================== per-frame

        public override void Update()
        {
            animT += Time.deltaTime;
            float pulse = (205f + Mathf.Sin(Time.time * 5f) * 50f) / 255f;
            for (int i = 0; i < beamsShown; i++)
            {
                var v = beamPool[i];
                float a = Mathf.Clamp01(pulse * v.BaseAlpha);
                var c = new Color(1, 1, 1, a);
                v.Body.color = v.CapStart.color = v.CapEnd.color = c;
            }
            foreach (var g in pieceGlows) if (g != null) g.color = new Color(1, 1, 1, pulse);
            var r = board.Result;
            foreach (var v in objVis)
            {
                if (v.Ring != null) { v.Ring.transform.localRotation = Quaternion.Euler(0, 0, -animT * 60f); v.Ring.color = new Color(1, 1, 1, pulse); }
                if (v.O.Kind == ObjKind.Checkpoint && r.TargetLit[v.Index])
                {
                    v.Main.transform.localRotation = Quaternion.Euler(0, 0, -animT * 300f);
                    v.Glow.color = new Color(1, 1, 1, pulse);
                }
            }

            // keyboard shortcuts (only while no overlay is open)
            if (App.IsTop(this)) HandleKeys();

            if (won && !winShown && wonAt >= 0 && Time.time - wonAt > 0.8f)
            {
                winShown = true;
                App.Push(new WinLayer(this));
            }

            if (dirty)
            {
                saveTimer -= Time.deltaTime;
                if (saveTimer <= 0) Persist();
            }
        }

        void HandleKeys()
        {
            if (PointerInput.KeyDown(PointerInput.Key.Q)) RotateSelected(+1);
            if (PointerInput.KeyDown(PointerInput.Key.E)) RotateSelected(-1);
            if (PointerInput.KeyDown(PointerInput.Key.Tab)) ToggleOutput();
            if (PointerInput.KeyDown(PointerInput.Key.Delete)) DeleteSelected();
            if (PointerInput.KeyDown(PointerInput.Key.Z)) Undo();
            if (PointerInput.KeyDown(PointerInput.Key.R)) ResetBoard();
            if (PointerInput.KeyDown(PointerInput.Key.M)) Arm(PieceKind.Mirror);
            if (PointerInput.KeyDown(PointerInput.Key.P)) Arm(PieceKind.Prism);
            if (PointerInput.KeyDown(PointerInput.Key.N) && won) GoNext();
            if (selected >= 0 && Mathf.Abs(PointerInput.Scroll) > 0.01f) RotateSelected(PointerInput.Scroll > 0 ? +1 : -1);
        }

        void Persist()
        {
            dirty = false;
            if (board.Pieces.Count == 0) save.Boards.Remove(entry.Id);
            else save.Boards[entry.Id] = Board.PiecesToJson(board.Pieces);
            save.Save();
        }

        public override void OnClose()
        {
            if (dirty) Persist();
        }

        // ================================================================== used by overlays

        public LevelEntry Entry => entry;
        public Board Board => board;
        public bool HasSolution => level.Solution != null && level.Solution.Count > 0;
        public bool HasTutorial => level.Tutorial.Count > 0;
        public LevelEntry NextEntry
        {
            get
            {
                var n = LevelLibrary.Next(entry);
                return n != null && LevelLibrary.IsPlayable(save, n) ? n : null;
            }
        }
        public void DoReset() => ResetBoard();
        public void DoUndo() => Undo();
        public void DoShowSolution() => LoadSolution();
        public void DoTutorial() => App.Push(new TutorialLayer(level.Tutorial));
        public void GoNext()
        {
            var n = NextEntry;
            if (n != null) App.Play(n);
            else App.ShowListFor(entry);
        }
    }

    public sealed class GameMenuLayer : Layer
    {
        public GameMenuLayer(GameScreen game) : base("GameMenu")
        {
            var t = Root.transform;
            var view = App.Instance.ViewRect;
            NewBox(t, "Shade", view.center, view.size * 1.1f, new Color(0, 0, 0, 0.82f), 300);
            NewText(t, "LEVEL " + game.Entry.Title, new Vector2(0, App.Top - 1.6f), 0.42f, Pal.Text, TextAnchor.MiddleCenter, 302);
            var items = new List<(string, Action)>
            {
                ("RESUME", Close),
                ("UNDO", () => { Close(); game.DoUndo(); }),
                ("RESET LEVEL", () => App.Push(new ConfirmLayer("Reset level?", "Remove all mirrors and prisms.", () => { Close(); game.DoReset(); }))),
            };
            if (game.HasTutorial) items.Add(("TUTORIAL", () => { Close(); game.DoTutorial(); }));
            if (game.HasSolution) items.Add(("SHOW SOLUTION", () => App.Push(new ConfirmLayer("Show solution?", "Your pieces will be replaced\n(UNDO brings them back).", () => { Close(); game.DoShowSolution(); }))));
            if (game.NextEntry != null && App.Save.Beaten.Contains(game.Entry.Id)) items.Add(("NEXT LEVEL", () => { Close(); game.GoNext(); }));
            items.Add(("LEVEL LIST", () => App.ShowListFor(game.Entry)));
            items.Add(("MAIN MENU", () => App.ShowMainMenu()));
            float y = App.Top - 2.9f;
            foreach (var (label, act) in items)
            {
                TextButton(t, label, new Vector2(0, y), new Vector2(5.6f, 0.85f), act, 303, 0.3f);
                y -= 1.08f;
            }
        }

        public override void OnBack() => Close();
    }

    public sealed class WinLayer : Layer
    {
        public WinLayer(GameScreen game) : base("Win")
        {
            var t = Root.transform;
            var view = App.Instance.ViewRect;
            NewBox(t, "Shade", view.center, view.size * 1.1f, new Color(0, 0, 0, 0.6f), 300);
            NewPanel(t, "Panel", new Vector2(0, 0.2f), new Vector2(7.2f, 5.6f), 301);
            NewText(t, "LEVEL COMPLETE", new Vector2(0, 2.3f), 0.42f, Pal.Good, TextAnchor.MiddleCenter, 302);
            int used = game.Board.Pieces.Count;
            App.Save.BestPieces.TryGetValue(game.Entry.Id, out int best);
            NewText(t, $"pieces used: {used}   best: {best}", new Vector2(0, 1.4f), 0.22f, Pal.Text, TextAnchor.MiddleCenter, 302);
            var next = game.NextEntry;
            if (next != null)
                TextButton(t, "NEXT LEVEL", new Vector2(0, 0.3f), new Vector2(5.2f, 0.9f), () => { Close(); game.GoNext(); }, 303, 0.32f);
            else
                NewText(t, game.Entry.Pack == "main" ? "solve more levels in this sector\nto open the next one" : "last level of the pack", new Vector2(0, 0.3f), 0.2f, Pal.Dim, TextAnchor.MiddleCenter, 302);
            TextButton(t, "STAY", new Vector2(-1.4f, -1.0f), new Vector2(2.4f, 0.8f), Close, 303);
            TextButton(t, "LEVELS", new Vector2(1.4f, -1.0f), new Vector2(2.4f, 0.8f), () => App.ShowListFor(game.Entry), 303);
        }

        public override void OnBack() => Close();
    }
}
