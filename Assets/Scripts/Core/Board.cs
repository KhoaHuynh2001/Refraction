// Game board = level + player pieces + the latest simulation result.
using System;
using System.Collections.Generic;

namespace Refraction.Core
{
    public sealed class Board
    {
        public readonly Level Level;
        public readonly List<Piece> Pieces = new List<Piece>();
        public SimResult Result { get; private set; }

        public Board(Level level, IEnumerable<Piece> pieces = null)
        {
            Level = level;
            if (pieces != null) foreach (var p in pieces) Pieces.Add(p.Clone());
            Recompute();
        }

        public void Recompute() => Result = Simulator.Run(Level, Pieces);

        public int Count(PieceKind k)
        {
            int n = 0;
            foreach (var p in Pieces) if (p.Kind == k) n++;
            return n;
        }

        public int Remaining(PieceKind k) => (k == PieceKind.Mirror ? Level.MaxMirrors : Level.MaxPrisms) - Count(k);

        public int PieceIndexAt(int x, int y)
        {
            for (int i = 0; i < Pieces.Count; i++) if (Pieces[i].X == x && Pieces[i].Y == y) return i;
            return -1;
        }

        /// <summary>Empty cell, or a door cell (a piece on a door only works while the door is open).</summary>
        public bool IsFreeCell(int x, int y, int ignorePiece = -1)
        {
            if (!Level.InBounds(x, y)) return false;
            int pi = PieceIndexAt(x, y);
            if (pi >= 0 && pi != ignorePiece) return false;
            var o = Level.ObjectAt(x, y);
            return o == null || o.Kind == ObjKind.Door;
        }

        public bool CanAdd(PieceKind k, int x, int y) => Remaining(k) > 0 && IsFreeCell(x, y);

        public int Add(Piece p)
        {
            if (!CanAdd(p.Kind, p.X, p.Y)) return -1;
            Pieces.Add(p);
            Recompute();
            return Pieces.Count - 1;
        }

        public bool Remove(int index)
        {
            if (index < 0 || index >= Pieces.Count) return false;
            Pieces.RemoveAt(index);
            Recompute();
            return true;
        }

        public bool Move(int index, int x, int y)
        {
            if (index < 0 || index >= Pieces.Count || !IsFreeCell(x, y, index)) return false;
            var p = Pieces[index];
            if (p.X == x && p.Y == y) return false;
            p.X = x; p.Y = y;
            // a moved prism forgets its custom output directions (same as the original)
            if (p.Kind == PieceKind.Prism) { p.SplitDirs = null; p.SplitColors = null; p.CombineDir = null; }
            Recompute();
            return true;
        }

        public void Clear()
        {
            Pieces.Clear();
            Recompute();
        }

        /// <summary>Rotate a mirror by ±22.5° (direction +1 = counter-clockwise).</summary>
        public void RotateMirror(int index, int direction)
        {
            var p = Pieces[index];
            p.Angle = Dir.Norm(p.Angle + 22.5 * Math.Sign(direction));
            Recompute();
        }

        /// <summary>
        /// Rotate one output beam of a prism by 45°, skipping directions already used by the other
        /// output or pointing straight back at an input (LevelView.rotateOutput).
        /// Returns false when the prism has no output to rotate.
        /// </summary>
        public bool RotatePrismOutput(int index, int outputSlot, int direction)
        {
            var info = Result.Prisms[index];
            if (info == null || info.Outputs.Count == 0) return false;
            outputSlot = Math.Max(0, Math.Min(outputSlot, info.Outputs.Count - 1));
            var beams = Result.Beams;
            var outBeam = beams[info.Outputs[outputSlot]];
            int step = 45 * Math.Sign(direction == 0 ? 1 : direction);
            int nd = outBeam.Dir;
            for (int tries = 0; tries < 8; tries++)
            {
                nd = Dir.Norm(nd + step);
                bool conflict = false;
                foreach (var o in info.Outputs) if (o != outBeam.Index && beams[o].Dir == nd) conflict = true;
                foreach (var i in info.Inputs) if (beams[i].Dir == Dir.Opposite(nd)) conflict = true;
                if (!conflict) break;
            }
            var p = Pieces[index];
            if (info.Outputs.Count == 2)
            {
                var a = beams[info.Outputs[0]];
                var b = beams[info.Outputs[1]];
                p.SplitDirs = new[] { outputSlot == 0 ? nd : a.Dir, outputSlot == 1 ? nd : b.Dir };
                p.SplitColors = new[] { a.Color, b.Color };
            }
            else p.CombineDir = nd;
            Recompute();
            return true;
        }

        public List<Piece> Snapshot()
        {
            var l = new List<Piece>();
            foreach (var p in Pieces) l.Add(p.Clone());
            return l;
        }

        public static string PiecesToJson(IList<Piece> pieces)
        {
            var list = new List<object>();
            foreach (var p in pieces) list.Add(p.ToJson());
            return MiniJson.Serialize(list, false);
        }

        public static List<Piece> PiecesFromJson(string json)
        {
            var res = new List<Piece>();
            if (string.IsNullOrEmpty(json)) return res;
            try
            {
                if (MiniJson.Parse(json) is List<object> list)
                    foreach (var o in list)
                        if (o is Dictionary<string, object> d)
                        {
                            var p = Piece.FromJson(d, null, "");
                            if (p != null) res.Add(p);
                        }
            }
            catch (FormatException) { }
            return res;
        }
    }
}
