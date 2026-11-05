// Compares this simulator against the original Java logic (output of tools/javaharness).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Refraction.Core;

namespace Refraction.Cli
{
    public static class DiffTest
    {
        static List<string> Norm(List<object> lasers)
        {
            var r = new List<string>();
            foreach (List<object> l in lasers)
            {
                string hit = (string)l[4];
                if (hit != "none")
                {
                    // the original reports the side door/wall of a diagonal squeeze as the collider
                    int sx = Convert.ToInt32(l[0]), sy = Convert.ToInt32(l[1]), dir = Convert.ToInt32(l[2]);
                    int hx = Convert.ToInt32(l[5]), hy = Convert.ToInt32(l[6]);
                    Dir.Step(dir, out int dx, out int dy);
                    bool onLine = false;
                    for (int k = 1; k < 12; k++) if (sx + dx * k == hx && sy + dy * k == hy) onLine = true;
                    if (!onLine) hit = "none";
                }
                string s = $"{l[0]},{l[1]},{l[2]},{l[3]},{hit}";
                if (hit != "none") s += $"@{l[5]},{l[6]}";
                r.Add(s);
            }
            r.Sort(StringComparer.Ordinal);
            return r;
        }

        static List<string> Mine(Level lvl, SimResult res, List<Piece> pieces)
        {
            var r = new List<string>();
            foreach (var b in res.Beams)
            {
                string hit = "none"; int hx = 0, hy = 0;
                if (b.HitPiece >= 0) { hit = pieces[b.HitPiece].Kind == PieceKind.Mirror ? "mirror" : "prism"; hx = pieces[b.HitPiece].X; hy = pieces[b.HitPiece].Y; }
                else if (b.HitObject >= 0)
                {
                    var o = lvl.Objects[b.HitObject]; hx = o.X; hy = o.Y;
                    hit = o.Kind switch { ObjKind.Wall => "wall", ObjKind.Door => "door", ObjKind.Checkpoint => "target", ObjKind.Switch => "switch", ObjKind.Portal => "portal", _ => "emitter" };
                }
                string s = $"{b.StartX},{b.StartY},{b.Dir},{b.Color},{hit}";
                if (hit != "none") s += $"@{hx},{hy}";
                r.Add(s);
            }
            r.Sort(StringComparer.Ordinal);
            return r;
        }

        public static int Run(string runsFile, string levelsDir, string dumpDir)
        {
            int n = 0, freshVsInc = 0, mineVsFresh = 0, mineVsInc = 0, wonMineFresh = 0, both = 0, errs = 0;
            int wonFreshNotMine = 0, wonMineNotFresh = 0;
            var examples = new List<string>();
            foreach (var line in File.ReadLines(runsFile))
            {
                var d = (Dictionary<string, object>)MiniJson.Parse(line);
                if (d["err"] != null || d["errB"] != null)
                {
                    errs++;
                    int ln = d.GetInt("level", 0);
                    var lv = Level.FromJson(File.ReadAllText(Path.Combine(levelsDir, $"level{ln:000}.json")), null);
                    var ps = new List<Piece>();
                    foreach (Dictionary<string, object> pd in d.GetList("pieces")) ps.Add(Piece.FromJson(pd, null, ""));
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var rr = Simulator.Run(lv, ps);
                    Console.WriteLine($"  original crashed ({d["err"] ?? d["errB"]}) on level {ln}; new engine: {rr.Beams.Count} beams, converged={rr.Converged}, {sw.Elapsed.TotalMilliseconds:F1} ms");
                    continue;
                }
                n++;
                int levelNo = d.GetInt("level", 0);
                var lvl = Level.FromJson(File.ReadAllText(Path.Combine(levelsDir, $"level{levelNo:000}.json")), null);
                var pieces = new List<Piece>();
                foreach (Dictionary<string, object> pd in d.GetList("pieces")) pieces.Add(Piece.FromJson(pd, null, ""));
                var res = Simulator.Run(lvl, pieces);
                var fresh = Norm(d.GetList("freshLasers"));
                var inc = Norm(d.GetList("incLasers"));
                var mine = Mine(lvl, res, pieces);
                bool fi = fresh.SequenceEqual(inc), mf = mine.SequenceEqual(fresh), mi = mine.SequenceEqual(inc);
                if (!fi) freshVsInc++;
                if (!mf) mineVsFresh++;
                if (!mi) mineVsInc++;
                if (!mf && !mi) both++;
                bool fw = d.GetBool("freshWon", false);
                if (fw != res.Won) { wonMineFresh++; if (fw) wonFreshNotMine++; else wonMineNotFresh++; }
                if (!mf && !mi && examples.Count < 400)
                    examples.Add($"seed {d.GetInt("seed", 0)} level {levelNo} pieces {Board.PiecesToJson(pieces)}\n  fresh: {string.Join(" | ", fresh)}\n  inc:   {string.Join(" | ", inc)}\n  mine:  {string.Join(" | ", mine)}");
            }
            Console.WriteLine($"runs {n} (skipped {errs} with Java exceptions)");
            Console.WriteLine($"original incremental != original fresh reload : {freshVsInc}");
            Console.WriteLine($"new engine != original fresh reload          : {mineVsFresh}");
            Console.WriteLine($"new engine != original incremental           : {mineVsInc}");
            Console.WriteLine($"new engine differs from BOTH                  : {both}");
            Console.WriteLine($"win flag differs (mine vs fresh)             : {wonMineFresh} (fresh won/mine not: {wonFreshNotMine}, mine won/fresh not: {wonMineNotFresh})");
            if (dumpDir != null) File.WriteAllLines(Path.Combine(dumpDir, "diff_examples.txt"), examples);
            return 0;
        }
    }
}
