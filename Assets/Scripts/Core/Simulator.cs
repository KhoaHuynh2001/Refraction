// Laser simulation.
//
// The original game (LevelView.java) updated lasers incrementally: every time a piece moved it
// tried to "kill" the affected lasers and recompute them. That code depends on the order in
// which things happened, and leaves stale or missing lasers in some situations (this is where
// most of the in-game bugs came from, and why the original needed a per-frame garbageCollect()).
//
// This rewrite recomputes the whole board from scratch after every change, using the same rules:
//  * beams move cell by cell in 8 directions; a diagonal step squeezed between two blockers
//    (walls or closed doors) is stopped;
//  * mirrors are one-sided, reflect only the first beam that reaches them and absorb head-on hits;
//  * a prism splits the first beam it receives (secondary colours -> two primaries, primaries -> two
//    copies) and combines two beams into one; a third beam is absorbed. A beam that already passed
//    through a prism can never feed that same prism again (prevents loops);
//  * a switch hit by a beam of its colour opens all doors of that colour;
//  * a portal sends an incoming beam out of every other portal in the same direction;
//  * a target ("checkpoint") is lit by a beam of its colour. The level is won when all are lit.
// The result never depends on the order the player did things in:
//  * prisms: when a splitting prism gets a second beam, the trace restarts with that prism combining
//    (exactly what the original did by killing the split beams);
//  * doors: iterated to a fixed point, starting from the level's start state (a door whose switch is
//    fed *through* that door by an emitter starts open, like in the original; every other door starts
//    closed, so the player cannot build a beam that holds its own door open);
//  * a beam that reaches a prism through a door kept open by that prism's own output is refused
//    (the original's "tracedDoor" paradox rule).
// Verified against the original Java logic on 50,000 random boards (see tools/README).
using System;
using System.Collections.Generic;

namespace Refraction.Core
{
    public enum BeamEnd { HitObject, Blocked, LeftBoard, Squeezed, Limit }

    public sealed class Beam
    {
        public int Index;
        public string Key;
        public int StartX, StartY;
        public int Dir;
        public int Color;
        public int EndX, EndY;              // cell where the beam stopped
        public double EndPx, EndPy;         // exact end point in grid units (cell centres are integers)
        public BeamEnd End;
        public int HitPiece = -1;           // index in Board.Pieces, if the beam ended on a piece
        public int HitObject = -1;          // index in Level.Objects, if it ended on a static object
        public int ParentBeam = -1;
        public int SourcePiece = -1;        // mirror/prism that emitted this beam (-1 for emitters/portals)
        internal int[] Ancestry = Array.Empty<int>();   // prisms (piece indices) this beam descends from
        internal int PassedDoors;                       // bit mask of door colours this beam (or its ancestors) went through

        public double Length => Math.Sqrt((EndPx - StartX) * (EndPx - StartX) + (EndPy - StartY) * (EndPy - StartY));
    }

    public sealed class PrismInfo
    {
        public readonly List<int> Inputs = new List<int>();    // beam indices feeding the prism
        public readonly List<int> Outputs = new List<int>();   // beam indices it emits
        public bool Combining => Inputs.Count == 2;
    }

    public sealed class SimResult
    {
        public readonly List<Beam> Beams = new List<Beam>();
        public bool[] TargetLit;          // per Level.Objects index (true only for lit targets)
        public bool[] SwitchOn;           // per Level.Objects index
        public bool[] DoorOpenByColor = new bool[7];
        public bool[] PieceActive;        // false for a piece sitting on a closed door
        public int[] MirrorOutput;        // per piece: beam reflected by this mirror, or -1
        public PrismInfo[] Prisms;        // per piece (null for mirrors)
        public bool Won;
        public int Iterations;
        public bool Converged;
        public int TargetsTotal, TargetsLit;

        public bool IsDoorOpen(LevelObject door) => door.Color >= 0 && door.Color < 7 && DoorOpenByColor[door.Color];
    }

    public sealed class Simulator
    {
        public const int MaxBeams = 4000;
        public const int MaxIterations = 64;

        readonly Level level;
        readonly IList<Piece> pieces;
        readonly int w, h;
        readonly int[,] objAt;     // index into level.Objects or -1
        readonly int[,] pieceAt;   // index into pieces or -1

        // per-iteration state
        bool[] doorOpen = new bool[7];
        // Prism resolution, emulating the original's "first beam splits, second beam turns the prism into a
        // combiner" behaviour without history: when a splitting prism receives a second beam we record a
        // combine decision and restart the trace (equivalent to the original killing the split beams).
        readonly Dictionary<int, string[]> decisions = new Dictionary<int, string[]>();
        readonly HashSet<string> rejected = new HashSet<string>();
        Beam[] prismFirst;                 // beam currently split by the prism (no decision)
        bool restart;
        readonly List<Beam> switchFeeds = new List<Beam>();   // beams currently lighting a switch
        List<Beam> prevFeeds = new List<Beam>();              // same, from the previous door iteration
        SimResult cur;
        bool[] mirrorUsed;
        bool[,] portalSlot;                // [objectIndex, dir/45]
        Dictionary<int, Beam>[] prismPending;
        readonly Stack<Beam> work = new Stack<Beam>();

        Simulator(Level level, IList<Piece> pieces)
        {
            this.level = level; this.pieces = pieces;
            w = level.Width; h = level.Height;
            objAt = new int[w + 2, h + 2];
            pieceAt = new int[w + 2, h + 2];
            for (int x = 0; x < w + 2; x++) for (int y = 0; y < h + 2; y++) { objAt[x, y] = -1; pieceAt[x, y] = -1; }
            for (int i = 0; i < level.Objects.Count; i++)
            {
                var o = level.Objects[i];
                if (level.InBounds(o.X, o.Y) && objAt[o.X, o.Y] < 0) objAt[o.X, o.Y] = i;
            }
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (level.InBounds(p.X, p.Y) && pieceAt[p.X, p.Y] < 0) pieceAt[p.X, p.Y] = i;
            }
        }

        bool[] startDoors = new bool[7];

        public static SimResult Run(Level level, IList<Piece> pieces)
        {
            var sim = new Simulator(level, pieces);
            sim.startDoors = StartDoorState(level);
            return sim.Solve();
        }

        // Door state when the level starts. The original loaded a level with every door open, fired the
        // emitters and then closed the doors whose switch was not lit. So a door whose switch is fed
        // *through that very door* by an emitter starts open (level 6-3 relies on this). Doors that start
        // closed can never be held open by a beam that has to pass through them.
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Level, bool[]> startCache =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Level, bool[]>();

        public static bool[] StartDoorState(Level level)
        {
            if (startCache.TryGetValue(level, out var cached)) return cached;
            var sim = new Simulator(level, new List<Piece>());
            for (int c = 0; c < 7; c++) sim.startDoors[c] = true;
            var r = sim.Solve();
            var state = (bool[])r.DoorOpenByColor.Clone();
            startCache.Add(level, state);
            return state;
        }

        /// <summary>Call after editing a Level object in place (level editor).</summary>
        public static void InvalidateCache(Level level) => startCache.Remove(level);

        SimResult Solve()
        {
            var seen = new HashSet<string>();
            SimResult result = null;
            Array.Copy(startDoors, doorOpen, 7);
            for (int iter = 1; iter <= MaxIterations; iter++)
            {
                result = ResolvePrisms();
                result.Iterations = iter;
                prevFeeds = new List<Beam>(switchFeeds);

                var nextDoor = new bool[7];
                for (int i = 0; i < level.Objects.Count; i++)
                    if (level.Objects[i].Kind == ObjKind.Switch && result.SwitchOn[i]) nextDoor[level.Objects[i].Color] = true;
                bool changed = false;
                for (int c = 0; c < 7; c++) if (nextDoor[c] != doorOpen[c]) changed = true;
                if (!changed) { result.Converged = true; break; }
                if (!seen.Add(Bits(nextDoor))) break;   // doors oscillate: keep the last state
                doorOpen = nextDoor;
            }
            return result;
        }

        static string Bits(bool[] b) { var c = new char[b.Length]; for (int i = 0; i < b.Length; i++) c[i] = b[i] ? '1' : '0'; return new string(c); }

        SimResult ResolvePrisms()
        {
            decisions.Clear();
            rejected.Clear();
            SimResult r = null;
            for (int attempt = 0; attempt < 256; attempt++)
            {
                restart = false;
                r = TraceOnce();
                if (restart) continue;
                bool bad = false;
                // a combine decision whose two beams did not both show up is dropped
                foreach (var kv in new List<KeyValuePair<int, string[]>>(decisions))
                {
                    if (r.Prisms[kv.Key].Inputs.Count == 2) continue;
                    rejected.Add(kv.Key + "|" + kv.Value[0] + "|" + kv.Value[1]);
                    decisions.Remove(kv.Key);
                    bad = true;
                }
                if (!bad) return r;
            }
            return r;
        }

        bool IsBlocker(int x, int y)
        {
            if (!level.InBounds(x, y)) return false;
            int oi = objAt[x, y];
            if (oi < 0) return false;
            var o = level.Objects[oi];
            if (o.Kind == ObjKind.Wall) return true;
            if (o.Kind == ObjKind.Door) return !doorOpen[o.Color];
            return false;
        }

        SimResult TraceOnce()
        {
            cur = new SimResult
            {
                TargetLit = new bool[level.Objects.Count],
                SwitchOn = new bool[level.Objects.Count],
                PieceActive = new bool[pieces.Count],
                MirrorOutput = new int[pieces.Count],
                Prisms = new PrismInfo[pieces.Count],
            };
            Array.Copy(doorOpen, cur.DoorOpenByColor, 7);
            mirrorUsed = new bool[pieces.Count];
            portalSlot = new bool[level.Objects.Count, 8];
            prismFirst = new Beam[pieces.Count];
            switchFeeds.Clear();
            prismPending = new Dictionary<int, Beam>[pieces.Count];
            work.Clear();
            for (int i = 0; i < pieces.Count; i++)
            {
                cur.MirrorOutput[i] = -1;
                prismPending[i] = new Dictionary<int, Beam>();
                if (pieces[i].Kind == PieceKind.Prism) cur.Prisms[i] = new PrismInfo();
                var p = pieces[i];
                bool active = level.InBounds(p.X, p.Y) && pieceAt[p.X, p.Y] == i;
                int oi = active ? objAt[p.X, p.Y] : -1;
                if (oi >= 0)
                {
                    var o = level.Objects[oi];
                    active = o.Kind == ObjKind.Door && doorOpen[o.Color];
                }
                cur.PieceActive[i] = active;
            }

            for (int i = 0; i < level.Objects.Count; i++)
            {
                var o = level.Objects[i];
                if (o.Kind != ObjKind.Emitter || o.Color == LaserColor.White) continue;
                Spawn(o.X, o.Y, o.Dir, o.Color, Array.Empty<int>(), "E" + i, -1, -1);
                Drain();
                if (restart) return cur;
            }

            int total = 0, lit = 0;
            for (int i = 0; i < level.Objects.Count; i++)
                if (level.Objects[i].Kind == ObjKind.Checkpoint) { total++; if (cur.TargetLit[i]) lit++; }
            cur.TargetsTotal = total; cur.TargetsLit = lit;
            cur.Won = total > 0 && lit == total;
            return cur;
        }

        void Spawn(int x, int y, int dir, int color, int[] ancestry, string key, int parent, int sourcePiece, int passedDoors = 0)
        {
            if (cur.Beams.Count >= MaxBeams) return;
            var b = new Beam
            {
                Index = cur.Beams.Count, Key = key, StartX = x, StartY = y, Dir = Dir.Norm(dir), Color = color,
                Ancestry = ancestry, ParentBeam = parent, SourcePiece = sourcePiece, PassedDoors = passedDoors,
            };
            cur.Beams.Add(b);
            work.Push(b);
        }

        // Depth-first like the original recursive laserCalcCollision: children are traced before siblings.
        void Drain()
        {
            while (work.Count > 0 && !restart)
            {
                var b = work.Pop();
                int before = cur.Beams.Count;
                Trace(b);
                // Re-order newly spawned beams so the first spawned is traced first (stack is LIFO).
                if (restart) { work.Clear(); return; }
                int spawned = cur.Beams.Count - before;
                if (spawned > 1)
                {
                    var tmp = new Beam[spawned];
                    for (int i = 0; i < spawned; i++) tmp[i] = work.Pop();
                    for (int i = 0; i < spawned; i++) work.Push(tmp[i]);
                }
            }
        }

        void Trace(Beam b)
        {
            Dir.Step(b.Dir, out int dx, out int dy);
            bool diag = dx != 0 && dy != 0;
            int x = b.StartX, y = b.StartY;
            int guard = (w + h) * 4;
            while (guard-- > 0)
            {
                if (diag && IsBlocker(x + dx, y) && IsBlocker(x, y + dy))
                {
                    Finish(b, x, y, x + dx * 0.5, y + dy * 0.5, BeamEnd.Squeezed);
                    return;
                }
                int nx = x + dx, ny = y + dy;
                if (!level.InBounds(nx, ny))
                {
                    Finish(b, x, y, x + dx * 0.5, y + dy * 0.5, BeamEnd.LeftBoard);
                    return;
                }
                x = nx; y = ny;

                int pi = pieceAt[x, y];
                if (pi >= 0 && cur.PieceActive[pi])
                {
                    Finish(b, x, y, x, y, BeamEnd.HitObject);
                    b.HitPiece = pi;
                    if (pieces[pi].Kind == PieceKind.Mirror) HitMirror(b, pi);
                    else HitPrism(b, pi);
                    return;
                }

                int oi = objAt[x, y];
                if (oi < 0) continue;
                var o = level.Objects[oi];
                switch (o.Kind)
                {
                    case ObjKind.Door:
                        if (doorOpen[o.Color]) { b.PassedDoors |= 1 << o.Color; continue; }
                        Finish(b, x, y, x - dx * 0.5, y - dy * 0.5, BeamEnd.Blocked);
                        b.HitObject = oi;
                        return;
                    case ObjKind.Wall:
                        Finish(b, x, y, x - dx * 0.5, y - dy * 0.5, BeamEnd.Blocked);
                        b.HitObject = oi;
                        return;
                    case ObjKind.Emitter:
                        Finish(b, x, y, x, y, BeamEnd.HitObject);
                        b.HitObject = oi;
                        return;
                    case ObjKind.Checkpoint:
                        Finish(b, x, y, x, y, BeamEnd.HitObject);
                        b.HitObject = oi;
                        if (b.Color == o.Color) cur.TargetLit[oi] = true;
                        return;
                    case ObjKind.Switch:
                        Finish(b, x, y, x, y, BeamEnd.HitObject);
                        b.HitObject = oi;
                        if (b.Color == o.Color) { cur.SwitchOn[oi] = true; switchFeeds.Add(b); }
                        return;
                    case ObjKind.Portal:
                        Finish(b, x, y, x, y, BeamEnd.HitObject);
                        b.HitObject = oi;
                        HitPortal(b, oi);
                        return;
                }
            }
            Finish(b, x, y, x, y, BeamEnd.Limit);
        }

        static void Finish(Beam b, int cx, int cy, double px, double py, BeamEnd end)
        {
            b.EndX = cx; b.EndY = cy; b.EndPx = px; b.EndPy = py; b.End = end;
        }

        void HitMirror(Beam b, int pi)
        {
            if (mirrorUsed[pi]) return;                         // a mirror reflects a single beam
            int outDir = Dir.Reflect(pieces[pi].Angle, b.Dir);
            if (outDir < 0) return;                             // back side or head-on: absorbed
            mirrorUsed[pi] = true;
            var p = pieces[pi];
            cur.MirrorOutput[pi] = cur.Beams.Count;
            Spawn(p.X, p.Y, outDir, b.Color, b.Ancestry, b.Key + "|M" + pi, b.Index, pi, b.PassedDoors);
        }

        static bool Contains(int[] a, int v)
        {
            foreach (var x in a) if (x == v) return true;
            return false;
        }

        static int[] Append(int[] a, int v)
        {
            if (Contains(a, v)) return a;
            var r = new int[a.Length + 1];
            Array.Copy(a, r, a.Length);
            r[a.Length] = v;
            return r;
        }

        static int[] Union(int[] a, int[] b)
        {
            var r = a;
            foreach (var v in b) r = Append(r, v);
            return r;
        }

        void HitPrism(Beam b, int pi)
        {
            if (Contains(b.Ancestry, pi)) return;               // own descendant: absorbed
            if (IsParadox(b, pi)) return;                       // would hold its own door open: absorbed
            var info = cur.Prisms[pi];
            var p = pieces[pi];

            if (decisions.TryGetValue(pi, out var keys))
            {
                int slot = Array.IndexOf(keys, b.Key);
                if (slot < 0 || prismPending[pi].ContainsKey(slot)) return;   // a third beam: absorbed
                prismPending[pi][slot] = b;
                info.Inputs.Add(b.Index);
                if (prismPending[pi].Count < 2) return;          // wait for the partner beam
                var b1 = prismPending[pi][0];
                var b2 = prismPending[pi][1];
                int color = LaserColor.Combine(b1.Color, b2.Color);
                int dir = p.CombineDir ?? Dir.PrismCombineDefault(p.X, p.Y, w, h, b.Dir, (b == b1 ? b2 : b1).Dir);
                var anc = Append(Union(b1.Ancestry, b2.Ancestry), pi);
                info.Outputs.Add(cur.Beams.Count);
                Spawn(p.X, p.Y, dir, color, anc, "C" + pi + "(" + b1.Key + "+" + b2.Key + ")", b.Index, pi, b1.PassedDoors | b2.PassedDoors);
                return;
            }

            var first = prismFirst[pi];
            if (first != null)
            {
                // second beam: this prism becomes a combiner (unless that combination already failed)
                if (first.Key == b.Key || rejected.Contains(pi + "|" + first.Key + "|" + b.Key)) return;
                decisions[pi] = new[] { first.Key, b.Key };
                restart = true;
                return;
            }

            prismFirst[pi] = b;
            info.Inputs.Add(b.Index);
            LaserColor.Split(b.Color, out int ca, out int cb);
            int da, db;
            if (p.SplitDirs != null && p.SplitColors != null &&
                ((ca == p.SplitColors[0] && cb == p.SplitColors[1]) || (cb == p.SplitColors[0] && ca == p.SplitColors[1])))
            {
                if (ca == p.SplitColors[0]) { da = p.SplitDirs[0]; db = p.SplitDirs[1]; ca = p.SplitColors[0]; cb = p.SplitColors[1]; }
                else { da = p.SplitDirs[1]; db = p.SplitDirs[0]; ca = p.SplitColors[1]; cb = p.SplitColors[0]; }
            }
            else Dir.PrismSplitDefault(p.X, p.Y, w, h, b.Dir, out da, out db);

            var ancestry = Append(b.Ancestry, pi);
            info.Outputs.Add(cur.Beams.Count);
            Spawn(p.X, p.Y, da, ca, ancestry, b.Key + "|P" + pi + "a", b.Index, pi, b.PassedDoors);
            info.Outputs.Add(cur.Beams.Count);
            Spawn(p.X, p.Y, db, cb, ancestry, b.Key + "|P" + pi + "b", b.Index, pi, b.PassedDoors);
        }

        // Paradox guard, the original's "tracedDoor": a beam that reached the prism through an open door may
        // not feed a prism whose own output beams are what keeps that door open (now, or in the previous
        // door iteration).
        bool IsParadox(Beam b, int pi)
        {
            if (b.PassedDoors == 0) return false;
            foreach (var f in switchFeeds)
                if ((b.PassedDoors & (1 << f.Color)) != 0 && Contains(f.Ancestry, pi)) return true;
            foreach (var f in prevFeeds)
                if ((b.PassedDoors & (1 << f.Color)) != 0 && Contains(f.Ancestry, pi)) return true;
            return false;
        }

        void HitPortal(Beam b, int oi)
        {
            int slot = b.Dir / 45;
            if (portalSlot[oi, slot]) return;
            portalSlot[oi, slot] = true;
            int opp = Dir.Opposite(b.Dir) / 45;
            for (int i = 0; i < level.Objects.Count; i++)
            {
                if (i == oi || level.Objects[i].Kind != ObjKind.Portal) continue;
                portalSlot[i, opp] = true;
                var q = level.Objects[i];
                Spawn(q.X, q.Y, b.Dir, b.Color, b.Ancestry, b.Key + "|O" + i, b.Index, -1, b.PassedDoors);
            }
        }
    }
}
