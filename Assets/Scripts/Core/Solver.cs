// Brute-force solver (iterative deepening, only tries cells that a beam currently crosses)
// and a level validator used by the editor tool and the command-line checker.
using System;
using System.Collections.Generic;
using System.Text;

namespace Refraction.Core
{
    public static class Solver
    {
        public sealed class Options
        {
            public int MaxPieces = 4;
            public int NodeBudget = 300000;
            public bool TryPrismAngles = true;
        }

        public sealed class Outcome
        {
            public List<Piece> Solution;   // null when not found
            public int Nodes;
            public bool BudgetExhausted;
        }

        public static Outcome Solve(Level level, Options opt = null)
        {
            opt = opt ?? new Options();
            var outcome = new Outcome();
            int maxDepth = Math.Min(opt.MaxPieces, level.MaxMirrors + level.MaxPrisms);
            var pieces = new List<Piece>();
            if (Simulator.Run(level, pieces).Won) { outcome.Solution = pieces; return outcome; }
            for (int depth = 1; depth <= maxDepth; depth++)
            {
                var seen = new HashSet<string>();
                var found = Dfs(level, pieces, depth, opt, outcome, seen);
                if (found != null) { outcome.Solution = found; return outcome; }
                if (outcome.BudgetExhausted) break;
            }
            return outcome;
        }

        static string Sig(List<Piece> pieces)
        {
            var parts = new List<string>();
            foreach (var p in pieces)
            {
                var sb = new StringBuilder();
                sb.Append(p.Kind == PieceKind.Mirror ? 'M' : 'P').Append(p.X).Append(',').Append(p.Y).Append(',');
                if (p.Kind == PieceKind.Mirror) sb.Append(p.Angle);
                else
                {
                    if (p.SplitDirs != null) sb.Append(p.SplitDirs[0]).Append('/').Append(p.SplitColors[0]).Append(':').Append(p.SplitDirs[1]).Append('/').Append(p.SplitColors[1]);
                    if (p.CombineDir.HasValue) sb.Append('c').Append(p.CombineDir.Value);
                }
                parts.Add(sb.ToString());
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join(";", parts);
        }

        static List<Piece> Dfs(Level level, List<Piece> pieces, int depthLeft, Options opt, Outcome outcome, HashSet<string> seen)
        {
            if (outcome.Nodes >= opt.NodeBudget) { outcome.BudgetExhausted = true; return null; }
            outcome.Nodes++;
            var res = Simulator.Run(level, pieces);
            if (res.Won) return Clone(pieces);
            if (depthLeft == 0) return null;
            if (!seen.Add(Sig(pieces) + "#" + depthLeft)) return null;

            int mirrors = 0, prisms = 0;
            foreach (var p in pieces) if (p.Kind == PieceKind.Mirror) mirrors++; else prisms++;
            foreach (var cell in CandidateCells(level, pieces, res))
            {
                if (mirrors < level.MaxMirrors)
                {
                    for (int k = 0; k < 16; k++)
                    {
                        pieces.Add(Piece.Mirror(cell.Item1, cell.Item2, k * 22.5));
                        var r = Dfs(level, pieces, depthLeft - 1, opt, outcome, seen);
                        pieces.RemoveAt(pieces.Count - 1);
                        if (r != null || outcome.BudgetExhausted) return r;
                    }
                }
                if (prisms < level.MaxPrisms)
                {
                    var prism = Piece.Prism(cell.Item1, cell.Item2);
                    pieces.Add(prism);
                    var r = Dfs(level, pieces, depthLeft - 1, opt, outcome, seen);
                    if (r != null || outcome.BudgetExhausted) { pieces.RemoveAt(pieces.Count - 1); return r; }
                    if (opt.TryPrismAngles)
                    {
                        var pr = Simulator.Run(level, pieces);
                        var info = pr.Prisms[pieces.Count - 1];
                        foreach (var variant in PrismVariants(pr, info))
                        {
                            pieces[pieces.Count - 1] = variant(prism);
                            r = Dfs(level, pieces, depthLeft - 1, opt, outcome, seen);
                            if (r != null || outcome.BudgetExhausted) { pieces.RemoveAt(pieces.Count - 1); return r; }
                        }
                    }
                    pieces.RemoveAt(pieces.Count - 1);
                }
            }
            return null;
        }

        static IEnumerable<Func<Piece, Piece>> PrismVariants(SimResult r, PrismInfo info)
        {
            if (info == null || info.Outputs.Count == 0) yield break;
            var banned = new HashSet<int>();
            foreach (var i in info.Inputs) banned.Add(Dir.Opposite(r.Beams[i].Dir));
            if (info.Outputs.Count == 2)
            {
                int c0 = r.Beams[info.Outputs[0]].Color, c1 = r.Beams[info.Outputs[1]].Color;
                for (int a = 0; a < 360; a += 45)
                    for (int b = 0; b < 360; b += 45)
                    {
                        if (a == b || banned.Contains(a) || banned.Contains(b)) continue;
                        if (c0 == c1 && b < a) continue;
                        int da = a, db = b;
                        yield return src => { var p = src.Clone(); p.SplitDirs = new[] { da, db }; p.SplitColors = new[] { c0, c1 }; return p; };
                    }
            }
            else
            {
                for (int a = 0; a < 360; a += 45)
                {
                    if (banned.Contains(a)) continue;
                    int da = a;
                    yield return src => { var p = src.Clone(); p.CombineDir = da; return p; };
                }
            }
        }

        static List<Piece> Clone(List<Piece> l)
        {
            var r = new List<Piece>();
            foreach (var p in l) r.Add(p.Clone());
            return r;
        }

        /// <summary>Free cells crossed by at least one beam (a new piece anywhere else changes nothing).</summary>
        public static List<Tuple<int, int>> CandidateCells(Level level, List<Piece> pieces, SimResult res)
        {
            var list = new List<Tuple<int, int>>();
            var mark = new bool[level.Width + 2, level.Height + 2];
            foreach (var b in res.Beams)
            {
                Dir.Step(b.Dir, out int dx, out int dy);
                int x = b.StartX, y = b.StartY;
                for (int guard = 0; guard < 64; guard++)
                {
                    if (x == b.EndX && y == b.EndY) break;
                    x += dx; y += dy;
                    if (!level.InBounds(x, y) || mark[x, y]) continue;
                    mark[x, y] = true;
                    bool free = true;
                    foreach (var p in pieces) if (p.X == x && p.Y == y) free = false;
                    var o = level.ObjectAt(x, y);
                    if (o != null && o.Kind != ObjKind.Door) free = false;
                    if (free) list.Add(Tuple.Create(x, y));
                }
            }
            return list;
        }
    }

    public static class LevelValidator
    {
        public sealed class Report
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Info = new List<string>();
            public bool Ok => Errors.Count == 0;

            public override string ToString()
            {
                var sb = new StringBuilder();
                foreach (var e in Errors) sb.Append("ERROR: ").Append(e).Append('\n');
                foreach (var w in Warnings) sb.Append("WARN:  ").Append(w).Append('\n');
                foreach (var i in Info) sb.Append("INFO:  ").Append(i).Append('\n');
                return sb.ToString();
            }
        }

        public static Report Validate(Level lvl, bool runSolverIfNoSolution = true, Solver.Options solverOptions = null)
        {
            var rep = new Report();
            if (lvl.Width < 3 || lvl.Width > 12 || lvl.Height < 3 || lvl.Height > 14)
                rep.Errors.Add($"Board size {lvl.Width}x{lvl.Height} out of range (width 3..12, height 3..14; original is 8x10)");
            if (lvl.MaxMirrors < 0 || lvl.MaxPrisms < 0 || lvl.MaxMirrors > 30 || lvl.MaxPrisms > 30)
                rep.Errors.Add("maxMirrors / maxPrisms must be between 0 and 30");

            var occupied = new Dictionary<string, int>();
            int emitters = 0, targets = 0;
            var emitterColors = new HashSet<int>();
            var switchColors = new Dictionary<int, int>();
            var doorColors = new HashSet<int>();
            int portals = 0;
            for (int i = 0; i < lvl.Objects.Count; i++)
            {
                var o = lvl.Objects[i];
                string name = $"objects[{i}] {LevelObject.KindName(o.Kind)} at ({o.X},{o.Y})";
                if (!lvl.InBounds(o.X, o.Y)) rep.Errors.Add($"{name} is outside the {lvl.Width}x{lvl.Height} board (x 1..{lvl.Width}, y 1..{lvl.Height})");
                string key = o.X + "," + o.Y;
                if (occupied.TryGetValue(key, out int other)) rep.Errors.Add($"{name} overlaps objects[{other}]");
                else occupied[key] = i;
                switch (o.Kind)
                {
                    case ObjKind.Emitter:
                        emitters++; emitterColors.Add(o.Color);
                        if (!Dir.IsValidBeamDir(o.Dir)) rep.Errors.Add($"{name}: dir must be a multiple of 45");
                        break;
                    case ObjKind.Checkpoint: targets++; break;
                    case ObjKind.Switch:
                        switchColors.TryGetValue(o.Color, out int n); switchColors[o.Color] = n + 1; break;
                    case ObjKind.Door: doorColors.Add(o.Color); break;
                    case ObjKind.Portal: portals++; break;
                }
            }
            if (emitters == 0) rep.Errors.Add("Level needs at least one emitter");
            if (targets == 0) rep.Errors.Add("Level needs at least one target");
            foreach (var c in doorColors)
                if (!switchColors.ContainsKey(c)) rep.Errors.Add($"{LaserColor.Name(c)} door(s) have no {LaserColor.Name(c)} switch, they can never open");
            foreach (var kv in switchColors)
            {
                if (kv.Value > 1) rep.Warnings.Add($"{kv.Value} {LaserColor.Name(kv.Key)} switches: the original levels always use one switch per colour");
                if (!doorColors.Contains(kv.Key)) rep.Warnings.Add($"{LaserColor.Name(kv.Key)} switch has no matching door");
            }
            if (portals == 1) rep.Warnings.Add("Only one portal: a portal needs a partner to do anything");

            // colour reachability
            var reachable = new HashSet<int>(emitterColors);
            if (lvl.MaxPrisms > 0)
            {
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    foreach (var c in new List<int>(reachable))
                    {
                        LaserColor.Split(c, out int a, out int b);
                        grew |= reachable.Add(a); grew |= reachable.Add(b);
                    }
                    foreach (var a in new List<int>(reachable))
                        foreach (var b in new List<int>(reachable))
                        {
                            int m = LaserColor.Combine(a, b);
                            if (m != LaserColor.White) grew |= reachable.Add(m);
                        }
                }
            }
            foreach (var o in lvl.Objects)
                if ((o.Kind == ObjKind.Checkpoint || o.Kind == ObjKind.Switch) && !reachable.Contains(o.Color))
                    rep.Errors.Add($"{LevelObject.KindName(o.Kind)} at ({o.X},{o.Y}) is {LaserColor.Name(o.Color)} but that colour can never be produced");

            if (!rep.Ok) return rep;

            var empty = Simulator.Run(lvl, new List<Piece>());
            if (empty.Won) rep.Errors.Add("Level is already solved before the player places anything");
            if (!empty.Converged) rep.Warnings.Add("Simulation did not settle (doors/prisms oscillate) with an empty board");

            if (lvl.Solution != null)
            {
                int m = 0, p = 0;
                var seenCells = new HashSet<string>();
                foreach (var pc in lvl.Solution)
                {
                    if (pc.Kind == PieceKind.Mirror) m++; else p++;
                    var o = lvl.ObjectAt(pc.X, pc.Y);
                    if (!lvl.InBounds(pc.X, pc.Y)) rep.Errors.Add($"Solution piece at ({pc.X},{pc.Y}) is outside the board");
                    else if (o != null && o.Kind != ObjKind.Door) rep.Errors.Add($"Solution piece at ({pc.X},{pc.Y}) sits on a {LevelObject.KindName(o.Kind)}");
                    if (!seenCells.Add(pc.X + "," + pc.Y)) rep.Errors.Add($"Two solution pieces at ({pc.X},{pc.Y})");
                }
                if (m > lvl.MaxMirrors) rep.Errors.Add($"Solution uses {m} mirrors but maxMirrors is {lvl.MaxMirrors}");
                if (p > lvl.MaxPrisms) rep.Errors.Add($"Solution uses {p} prisms but maxPrisms is {lvl.MaxPrisms}");
                var sr = Simulator.Run(lvl, lvl.Solution);
                if (!sr.Won) rep.Errors.Add($"The included solution does NOT solve the level ({sr.TargetsLit}/{sr.TargetsTotal} targets lit)");
                else rep.Info.Add($"Included solution works ({m} mirror(s), {p} prism(s))");
                if (!sr.Converged) rep.Warnings.Add("Simulation did not settle with the included solution");
            }
            else if (runSolverIfNoSolution)
            {
                var o = Solver.Solve(lvl, solverOptions);
                if (o.Solution != null)
                    rep.Info.Add($"Solver found a solution with {o.Solution.Count} piece(s) ({o.Nodes} positions tried)");
                else
                    rep.Warnings.Add($"No solution included and the solver found none ({o.Nodes} positions tried{(o.BudgetExhausted ? ", budget exhausted" : "")}). Add a \"solution\" to prove it is solvable.");
            }
            return rep;
        }
    }
}
