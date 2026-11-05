// Level + player-piece data model, JSON (new format) and legacy (original APK) parsing.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Refraction.Core
{
    public enum ObjKind { Wall, Emitter, Checkpoint, Switch, Door, Portal }

    public sealed class LevelObject
    {
        public ObjKind Kind;
        public int X, Y;
        public int Color;      // emitter / checkpoint / switch / door
        public int Dir;        // emitter only

        public LevelObject Clone() => (LevelObject)MemberwiseClone();

        public static string KindName(ObjKind k)
        {
            switch (k)
            {
                case ObjKind.Wall: return "wall";
                case ObjKind.Emitter: return "emitter";
                case ObjKind.Checkpoint: return "target";
                case ObjKind.Switch: return "switch";
                case ObjKind.Door: return "door";
                default: return "portal";
            }
        }

        public static bool TryParseKind(string s, out ObjKind k)
        {
            k = ObjKind.Wall;
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "wall": case "8": k = ObjKind.Wall; return true;
                case "emitter": case "laser": case "7": k = ObjKind.Emitter; return true;
                case "target": case "checkpoint": case "3": k = ObjKind.Checkpoint; return true;
                case "switch": case "4": k = ObjKind.Switch; return true;
                case "door": case "5": k = ObjKind.Door; return true;
                case "portal": case "wormhole": case "6": k = ObjKind.Portal; return true;
            }
            return false;
        }
    }

    public enum PieceKind { Mirror, Prism }

    /// <summary>A mirror or prism placed by the player.</summary>
    public sealed class Piece
    {
        public PieceKind Kind;
        public int X, Y;
        /// <summary>Mirror surface angle in degrees, multiple of 22.5 (default 135 = "\").</summary>
        public double Angle = 135;
        /// <summary>Prism: player-chosen directions for the two split beams (null = automatic).</summary>
        public int[] SplitDirs;     // length 2
        public int[] SplitColors;   // length 2, colour that each SplitDirs entry belongs to
        /// <summary>Prism: player-chosen direction of the combined beam (null = automatic).</summary>
        public int? CombineDir;

        public Piece Clone()
        {
            var p = (Piece)MemberwiseClone();
            p.SplitDirs = SplitDirs == null ? null : (int[])SplitDirs.Clone();
            p.SplitColors = SplitColors == null ? null : (int[])SplitColors.Clone();
            return p;
        }

        public static Piece Mirror(int x, int y, double angle) => new Piece { Kind = PieceKind.Mirror, X = x, Y = y, Angle = Dir.Norm(angle) };
        public static Piece Prism(int x, int y) => new Piece { Kind = PieceKind.Prism, X = x, Y = y };

        public Dictionary<string, object> ToJson()
        {
            var d = new Dictionary<string, object>();
            d["type"] = Kind == PieceKind.Mirror ? "mirror" : "prism";
            d["x"] = X; d["y"] = Y;
            if (Kind == PieceKind.Mirror) d["angle"] = Angle;
            else
            {
                if (SplitDirs != null)
                {
                    var outs = new List<object>();
                    for (int i = 0; i < 2; i++)
                        outs.Add(new Dictionary<string, object> { { "color", LaserColor.Name(SplitColors[i]) }, { "dir", SplitDirs[i] } });
                    d["split"] = outs;
                }
                if (CombineDir.HasValue) d["combineDir"] = CombineDir.Value;
            }
            return d;
        }

        public static Piece FromJson(Dictionary<string, object> d, List<string> errors, string where)
        {
            string type = d.GetString("type", "").ToLowerInvariant();
            int x = d.GetInt("x", -1), y = d.GetInt("y", -1);
            if (type == "mirror")
            {
                double a = d.GetDouble("angle", 135);
                if (Math.Abs(a / 22.5 - Math.Round(a / 22.5)) > 1e-6) errors?.Add($"{where}: mirror angle {a} is not a multiple of 22.5");
                return Mirror(x, y, a);
            }
            if (type == "prism")
            {
                var p = Prism(x, y);
                var split = d.GetList("split");
                if (split != null)
                {
                    if (split.Count != 2) errors?.Add($"{where}: prism 'split' must have exactly 2 entries");
                    else
                    {
                        p.SplitDirs = new int[2]; p.SplitColors = new int[2];
                        for (int i = 0; i < 2; i++)
                        {
                            var o = split[i] as Dictionary<string, object>;
                            if (!LaserColor.TryParse(o.GetString("color", ""), out p.SplitColors[i])) errors?.Add($"{where}: bad split colour");
                            p.SplitDirs[i] = Dir.Norm(o.GetInt("dir", 0));
                        }
                    }
                }
                if (d.ContainsKey("combineDir")) p.CombineDir = Dir.Norm(d.GetInt("combineDir", 0));
                return p;
            }
            errors?.Add($"{where}: unknown piece type '{type}' (use \"mirror\" or \"prism\")");
            return null;
        }
    }

    public sealed class Level
    {
        public string Id = "";
        public string Name = "";
        public string Author = "";
        public int Width = 8, Height = 10;
        public int MaxMirrors, MaxPrisms;
        public List<LevelObject> Objects = new List<LevelObject>();
        public List<string> Tutorial = new List<string>();
        /// <summary>Optional known solution (used by the validator and the "show solution" button).</summary>
        public List<Piece> Solution;
        /// <summary>Sort key inside the level list (e.g. 1..140 for the original levels).</summary>
        public int Order;

        public IEnumerable<LevelObject> OfKind(ObjKind k)
        {
            foreach (var o in Objects) if (o.Kind == k) yield return o;
        }

        public LevelObject ObjectAt(int x, int y)
        {
            foreach (var o in Objects) if (o.X == x && o.Y == y) return o;
            return null;
        }

        public bool InBounds(int x, int y) => x >= 1 && y >= 1 && x <= Width && y <= Height;

        // ------------------------------------------------------------------ legacy format
        /// <summary>Parses the original APK format: "maxMirror,maxPrism;x,y,type,angle,color;..."</summary>
        public static Level FromLegacy(string text, int number)
        {
            var lvl = new Level { Id = number.ToString("000"), Order = number, Name = $"{(number - 1) / 20 + 1}-{(number - 1) % 20 + 1}" };
            var parts = text.Trim().Split(';');
            var head = parts[0].Split(',');
            lvl.MaxMirrors = int.Parse(head[0], CultureInfo.InvariantCulture);
            lvl.MaxPrisms = int.Parse(head[1], CultureInfo.InvariantCulture);
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;
                var f = parts[i].Split(',');
                if (!LevelObject.TryParseKind(f[2], out var kind)) continue;
                var o = new LevelObject { Kind = kind, X = int.Parse(f[0], CultureInfo.InvariantCulture), Y = int.Parse(f[1], CultureInfo.InvariantCulture) };
                if (kind == ObjKind.Emitter) o.Dir = Dir.Norm((int)Math.Round(double.Parse(f[3], CultureInfo.InvariantCulture)));
                if (kind == ObjKind.Emitter || kind == ObjKind.Checkpoint || kind == ObjKind.Switch || kind == ObjKind.Door)
                    o.Color = int.Parse(f[4], CultureInfo.InvariantCulture);
                lvl.Objects.Add(o);
            }
            return lvl;
        }

        // ------------------------------------------------------------------ JSON format
        public static Level FromJson(string json, List<string> errors)
        {
            object root;
            try { root = MiniJson.Parse(json); }
            catch (FormatException e) { errors?.Add(e.Message); return null; }
            var d = root as Dictionary<string, object>;
            if (d == null) { errors?.Add("Root must be a JSON object"); return null; }

            var lvl = new Level
            {
                Id = d.GetString("id", ""),
                Name = d.GetString("name", ""),
                Author = d.GetString("author", ""),
                Width = d.GetInt("width", 8),
                Height = d.GetInt("height", 10),
                MaxMirrors = d.GetInt("maxMirrors", 0),
                MaxPrisms = d.GetInt("maxPrisms", 0),
                Order = d.GetInt("order", 0),
            };
            var objs = d.GetList("objects");
            if (objs == null) errors?.Add("Missing \"objects\" array");
            else
            {
                for (int i = 0; i < objs.Count; i++)
                {
                    var o = objs[i] as Dictionary<string, object>;
                    string where = $"objects[{i}]";
                    if (o == null) { errors?.Add($"{where}: must be an object"); continue; }
                    if (!LevelObject.TryParseKind(o.GetString("type", ""), out var kind))
                    { errors?.Add($"{where}: unknown type '{o.GetString("type", "")}'"); continue; }
                    var lo = new LevelObject { Kind = kind, X = o.GetInt("x", -1), Y = o.GetInt("y", -1) };
                    if (kind == ObjKind.Emitter || kind == ObjKind.Checkpoint || kind == ObjKind.Switch || kind == ObjKind.Door)
                    {
                        if (!LaserColor.TryParse(o.GetString("color", ""), out lo.Color) || lo.Color == LaserColor.White)
                            errors?.Add($"{where}: {LevelObject.KindName(kind)} needs a colour (blue, yellow, red, green, orange, purple)");
                    }
                    if (kind == ObjKind.Emitter)
                    {
                        lo.Dir = o.GetInt("dir", -1);
                        if (!Dir.IsValidBeamDir(lo.Dir)) errors?.Add($"{where}: emitter \"dir\" must be one of 0,45,90,...,315");
                        lo.Dir = Dir.Norm(lo.Dir);
                    }
                    lvl.Objects.Add(lo);
                }
            }
            var tut = d.GetList("tutorial");
            if (tut != null) foreach (var t in tut) if (t is string s) lvl.Tutorial.Add(s);
            var sol = d.GetList("solution");
            if (sol != null)
            {
                lvl.Solution = new List<Piece>();
                for (int i = 0; i < sol.Count; i++)
                {
                    var p = Piece.FromJson(sol[i] as Dictionary<string, object>, errors, $"solution[{i}]");
                    if (p != null) lvl.Solution.Add(p);
                }
            }
            return lvl;
        }

        public string ToJson()
        {
            var d = new Dictionary<string, object>();
            d["id"] = Id;
            if (!string.IsNullOrEmpty(Name)) d["name"] = Name;
            if (!string.IsNullOrEmpty(Author)) d["author"] = Author;
            if (Order != 0) d["order"] = Order;
            d["width"] = Width;
            d["height"] = Height;
            d["maxMirrors"] = MaxMirrors;
            d["maxPrisms"] = MaxPrisms;
            if (Tutorial.Count > 0) d["tutorial"] = new List<object>(Tutorial);
            var objs = new List<object>();
            foreach (var o in Objects)
            {
                var od = new Dictionary<string, object> { { "type", LevelObject.KindName(o.Kind) }, { "x", o.X }, { "y", o.Y } };
                if (o.Kind == ObjKind.Emitter || o.Kind == ObjKind.Checkpoint || o.Kind == ObjKind.Switch || o.Kind == ObjKind.Door)
                    od["color"] = LaserColor.Name(o.Color);
                if (o.Kind == ObjKind.Emitter) od["dir"] = o.Dir;
                objs.Add(od);
            }
            d["objects"] = objs;
            if (Solution != null)
            {
                var sol = new List<object>();
                foreach (var p in Solution) sol.Add(p.ToJson());
                d["solution"] = sol;
            }
            return MiniJson.Serialize(d) + "\n";
        }

        /// <summary>ASCII picture of the board, handy for debugging and for AI prompts.</summary>
        public string ToAscii(IList<Piece> pieces = null)
        {
            var sb = new StringBuilder();
            sb.Append("   ");
            for (int x = 1; x <= Width; x++) sb.Append(x % 10).Append(' ');
            sb.Append('\n');
            for (int y = 1; y <= Height; y++)
            {
                sb.Append(y.ToString().PadLeft(2)).Append(' ');
                for (int x = 1; x <= Width; x++)
                {
                    char c = '.';
                    var o = ObjectAt(x, y);
                    if (o != null)
                    {
                        switch (o.Kind)
                        {
                            case ObjKind.Wall: c = '#'; break;
                            case ObjKind.Emitter: c = 'E'; break;
                            case ObjKind.Checkpoint: c = 'T'; break;
                            case ObjKind.Switch: c = 'S'; break;
                            case ObjKind.Door: c = 'D'; break;
                            case ObjKind.Portal: c = 'O'; break;
                        }
                    }
                    if (pieces != null)
                        foreach (var p in pieces) if (p.X == x && p.Y == y) c = p.Kind == PieceKind.Mirror ? 'M' : 'P';
                    sb.Append(c).Append(' ');
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
