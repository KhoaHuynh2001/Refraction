// Command-line helper for Refraction levels.
//   refraction-cli validate <file.json|folder> [--budget N] [--depth N]
//   refraction-cli solve    <file.json> [--budget N] [--depth N] [--write]
//   refraction-cli show     <file.json>
//   refraction-cli convert  <apk-assets-folder> <out-folder>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Refraction.Core;

namespace Refraction.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 2) { Usage(); return 2; }
            var opt = new Solver.Options();
            bool write = false;
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--budget") opt.NodeBudget = int.Parse(args[++i]);
                else if (args[i] == "--depth") opt.MaxPieces = int.Parse(args[++i]);
                else if (args[i] == "--write") write = true;
            }
            switch (args[0])
            {
                case "validate": return Validate(args[1], opt);
                case "solve": return SolveCmd(args[1], opt, write);
                case "show": return Show(args[1]);
                case "convert": return Convert(args[1], args[2]);
                case "solveall": return SolveAll(args[1], args[2], opt);
                case "dump": return Dump(args[1]);
                case "diff": return DiffTest.Run(args[1], args[2], args.Length > 3 ? args[3] : null);
                default: Usage(); return 2;
            }
        }

        static int SolveAll(string dir, string outFile, Solver.Options opt)
        {
            var lines = new System.Collections.Concurrent.ConcurrentBag<string>();
            var files = Directory.GetFiles(dir, "*.json").OrderBy(x => x).ToArray();
            System.Threading.Tasks.Parallel.ForEach(files, f =>
            {
                var lvl = Level.FromJson(File.ReadAllText(f), null);
                var o = Solver.Solve(lvl, opt);
                if (o.Solution == null) return;
                var parts = new List<string> { lvl.Order.ToString() };
                foreach (var p in o.Solution)
                {
                    if (p.Kind == PieceKind.Mirror) parts.Add($"M,{p.X},{p.Y},{p.Angle.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    else if (p.SplitDirs != null) parts.Add($"S,{p.X},{p.Y},{p.SplitColors[0]},{p.SplitDirs[0]},{p.SplitColors[1]},{p.SplitDirs[1]}");
                    else if (p.CombineDir.HasValue) parts.Add($"C,{p.X},{p.Y},{p.CombineDir.Value}");
                    else parts.Add($"P,{p.X},{p.Y}");
                }
                lines.Add(string.Join(";", parts));
            });
            File.WriteAllLines(outFile, lines.OrderBy(l => int.Parse(l.Split(';')[0])));
            Console.WriteLine($"solved {lines.Count}/{files.Length}");
            return 0;
        }

        /// <summary>Simulation result as JSON (used by the preview renderer).</summary>
        static int Dump(string path)
        {
            var lvl = Load(path, new List<string>());
            var pieces = lvl.Solution ?? new List<Piece>();
            var r = Simulator.Run(lvl, pieces);
            var beams = new List<object>();
            foreach (var b in r.Beams)
                beams.Add(new Dictionary<string, object> { ["sx"] = b.StartX, ["sy"] = b.StartY, ["ex"] = b.EndPx, ["ey"] = b.EndPy, ["dir"] = b.Dir, ["color"] = b.Color, ["end"] = b.End.ToString() });
            var lit = new List<object>(); for (int i = 0; i < r.TargetLit.Length; i++) lit.Add(r.TargetLit[i] || r.SwitchOn[i]);
            var doors = new List<object>(); foreach (var d in r.DoorOpenByColor) doors.Add(d);
            var act = new List<object>(); for (int i = 0; i < pieces.Count; i++) act.Add(r.MirrorOutput[i] >= 0 || (r.Prisms[i] != null && r.Prisms[i].Inputs.Count > 0));
            var d2 = new Dictionary<string, object> { ["beams"] = beams, ["lit"] = lit, ["doorOpen"] = doors, ["pieceGlow"] = act, ["won"] = r.Won };
            Console.WriteLine(MiniJson.Serialize(d2, false));
            return 0;
        }

        static void Usage()
        {
            Console.WriteLine("usage:\n  refraction-cli validate <file.json|folder> [--budget N] [--depth N]\n  refraction-cli solve <file.json> [--budget N] [--depth N] [--write]\n  refraction-cli show <file.json>\n  refraction-cli convert <apk-assets-folder> <out-folder>");
        }

        static Level Load(string path, List<string> errors) => Level.FromJson(File.ReadAllText(path), errors);

        static int Validate(string path, Solver.Options opt)
        {
            var files = Directory.Exists(path) ? Directory.GetFiles(path, "*.json").OrderBy(f => f).ToArray() : new[] { path };
            int bad = 0;
            foreach (var f in files)
            {
                var errors = new List<string>();
                var lvl = Load(f, errors);
                LevelValidator.Report rep = null;
                if (lvl != null && errors.Count == 0) rep = LevelValidator.Validate(lvl, true, opt);
                bool ok = errors.Count == 0 && rep != null && rep.Ok;
                if (!ok) bad++;
                Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {Path.GetFileName(f)}");
                foreach (var e in errors) Console.WriteLine("  ERROR: " + e);
                if (rep != null) foreach (var line in rep.ToString().Split('\n')) if (line.Length > 0) Console.WriteLine("  " + line);
            }
            Console.WriteLine($"{files.Length - bad}/{files.Length} valid");
            return bad == 0 ? 0 : 1;
        }

        static int SolveCmd(string path, Solver.Options opt, bool write)
        {
            var errors = new List<string>();
            var lvl = Load(path, errors);
            if (lvl == null) { foreach (var e in errors) Console.WriteLine(e); return 1; }
            var o = Solver.Solve(lvl, opt);
            if (o.Solution == null) { Console.WriteLine($"no solution found ({o.Nodes} nodes{(o.BudgetExhausted ? ", budget exhausted" : "")})"); return 1; }
            Console.WriteLine($"solution with {o.Solution.Count} piece(s), {o.Nodes} nodes:");
            Console.WriteLine(Board.PiecesToJson(o.Solution));
            Console.WriteLine(lvl.ToAscii(o.Solution));
            if (write) { lvl.Solution = o.Solution; File.WriteAllText(path, lvl.ToJson()); Console.WriteLine("written to " + path); }
            return 0;
        }

        static int Show(string path)
        {
            var errors = new List<string>();
            var lvl = Load(path, errors);
            if (lvl == null) { foreach (var e in errors) Console.WriteLine(e); return 1; }
            var pieces = lvl.Solution ?? new List<Piece>();
            Console.WriteLine(lvl.ToAscii(pieces));
            var r = Simulator.Run(lvl, pieces);
            foreach (var b in r.Beams)
                Console.WriteLine($"beam {b.Index}: ({b.StartX},{b.StartY}) dir {b.Dir} {LaserColor.Name(b.Color)} -> ({b.EndX},{b.EndY}) {b.End}");
            Console.WriteLine($"targets {r.TargetsLit}/{r.TargetsTotal}, won={r.Won}, iterations={r.Iterations}, converged={r.Converged}");
            return 0;
        }

        static int Convert(string assets, string outDir)
        {
            Directory.CreateDirectory(outDir);
            var tutorials = new Dictionary<int, List<string>>();
            string tutDir = Path.Combine(assets, "tutorials");
            if (Directory.Exists(tutDir))
            {
                foreach (var f in Directory.GetFiles(tutDir, "level*.tut"))
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (!int.TryParse(name.Substring(5), out int n)) continue;
                    var imgs = File.ReadAllText(f).Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => Path.GetFileNameWithoutExtension(s.Trim())).Where(s => s.Length > 0).ToList();
                    tutorials[n] = imgs;
                }
            }
            int count = 0;
            for (int n = 1; n <= 999; n++)
            {
                string f = Path.Combine(assets, "level" + n);
                if (!File.Exists(f)) continue;
                var lvl = Level.FromLegacy(File.ReadAllText(f), n);
                if (tutorials.TryGetValue(n, out var t)) lvl.Tutorial.AddRange(t);
                File.WriteAllText(Path.Combine(outDir, $"level{n:000}.json"), lvl.ToJson());
                count++;
            }
            Console.WriteLine($"converted {count} levels");
            return 0;
        }
    }
}
