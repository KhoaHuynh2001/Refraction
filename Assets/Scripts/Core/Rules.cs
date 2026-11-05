// Colour/direction math ported from the original CalcTools.java.
// Coordinates: x = 1..Width (left -> right), y = 1..Height (top -> bottom).
// Directions are degrees, counter-clockwise, 0 = right, 90 = UP (towards y = 1).
using System;
using System.Globalization;

namespace Refraction.Core
{
    public static class LaserColor
    {
        public const int White = 0;   // "dead" colour produced by mixing incompatible colours
        public const int Blue = 1;
        public const int Yellow = 2;
        public const int Red = 3;
        public const int Green = 4;   // Blue + Yellow
        public const int Orange = 5;  // Yellow + Red
        public const int Purple = 6;  // Blue + Red

        public static readonly string[] Names = { "white", "blue", "yellow", "red", "green", "orange", "purple" };

        public static bool IsValid(int c) => c >= 0 && c <= 6;
        public static bool IsPrimary(int c) => c >= 1 && c <= 3;

        public static string Name(int c) => IsValid(c) ? Names[c] : "white";

        /// <summary>Accepts "red", "Red", "3".</summary>
        public static bool TryParse(string s, out int color)
        {
            color = -1;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().ToLowerInvariant();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) { color = n; return IsValid(n); }
            if (s == "teal") s = "green";
            for (int i = 0; i < Names.Length; i++) if (Names[i] == s) { color = i; return true; }
            return false;
        }

        /// <summary>CalcTools.splitColor: secondary colours split into their primaries, primaries duplicate.</summary>
        public static void Split(int color, out int a, out int b)
        {
            switch (color)
            {
                case Orange: a = Yellow; b = Red; break;
                case Green: a = Yellow; b = Blue; break;
                case Purple: a = Blue; b = Red; break;
                default: a = color; b = color; break;
            }
        }

        /// <summary>CalcTools.combineColor.</summary>
        public static int Combine(int a, int b)
        {
            if (a == b) return a;
            if ((a == Red && b == Yellow) || (a == Yellow && b == Red)) return Orange;
            if ((a == Blue && b == Yellow) || (a == Yellow && b == Blue)) return Green;
            if ((a == Red && b == Blue) || (a == Blue && b == Red)) return Purple;
            return White;
        }
    }

    public static class Dir
    {
        public static int Norm(int deg) { deg %= 360; return deg < 0 ? deg + 360 : deg; }
        public static double Norm(double deg) { deg %= 360.0; return deg < 0 ? deg + 360.0 : deg; }
        public static int Opposite(int deg) => Norm(deg + 180);
        public static bool IsValidBeamDir(int deg) => deg >= 0 && deg < 360 && deg % 45 == 0;

        /// <summary>Grid step for a beam direction (y grows downwards).</summary>
        public static void Step(int deg, out int dx, out int dy)
        {
            switch (Norm(deg))
            {
                case 0: dx = 1; dy = 0; break;
                case 45: dx = 1; dy = -1; break;
                case 90: dx = 0; dy = -1; break;
                case 135: dx = -1; dy = -1; break;
                case 180: dx = -1; dy = 0; break;
                case 225: dx = -1; dy = 1; break;
                case 270: dx = 0; dy = 1; break;
                case 315: dx = 1; dy = 1; break;
                default: dx = 1; dy = 0; break; // original falls back to "right"
            }
        }

        public static bool IsDiagonal(int deg) => Norm(deg) % 90 != 0;

        /// <summary>
        /// Mirror reflection (LevelView.laserCalcCollision, mirror branch).
        /// A mirror only reflects from one side: the beam direction a must satisfy 0 &lt; (θ − a) mod 360 &lt; 180.
        /// A perpendicular (head-on) hit is absorbed. Returns -1 when the beam is absorbed.
        /// </summary>
        public static int Reflect(double mirrorAngle, int beamDir)
        {
            double delta = Norm(mirrorAngle - beamDir);
            if (delta <= 1e-9 || delta >= 180 - 1e-9) return -1;   // back side or parallel
            if (Math.Abs(delta - 90) < 1e-9) return -1;              // head-on
            int outDir = (int)Math.Round(Norm(2 * mirrorAngle - beamDir));
            return Norm(outDir);
        }

        // ---------------- prism angle rules (CalcTools.prismOutputAngle / combineAngles) -------------

        static double AngleCorrect(double raw)
        {
            if (raw > 315.0) return raw - 360.0;
            if (raw < 0.0) return raw + 360.0;
            return raw;
        }

        static double AngleRound(double a, double step) => Math.Floor(a / step) * step;

        public static double CombineAngles(double a1, double a2)
        {
            double final;
            if (a1 > a2)
            {
                double diff = Math.Abs(a1 - a2);
                double add = diff > 180 ? diff - 180 : diff;
                add /= 2.0;
                final = diff >= 180 ? a1 + add : a2 + add;
            }
            else
            {
                double diff = Math.Abs(a1 - a2);
                double add = diff > 180 ? diff - 180 : diff;
                add /= 2.0;
                final = diff >= 180 ? a2 + add : a1 + add;
            }
            return AngleRound(AngleCorrect(final), 45.0);
        }

        /// <summary>Default directions of the two beams leaving a prism that splits one incoming beam.
        /// Beams are steered away from the board edge when the prism sits on the border.</summary>
        public static void PrismSplitDefault(int x, int y, int w, int h, int inDir, out int out1, out int out2)
        {
            double angle = inDir;
            double o0, o1;
            if (y == 1)
            {
                if (x == 1)
                {
                    if (angle == 90) { o0 = 315; o1 = 0; }
                    else if (angle == 135) { o0 = 270; o1 = 0; }
                    else { o0 = 270; o1 = 315; }
                }
                else if (x == w)
                {
                    if (angle == 90) { o0 = 180; o1 = 225; }
                    else if (angle == 45) { o0 = 180; o1 = 270; }
                    else { o0 = 225; o1 = 270; }
                }
                else if (angle == 0) { o0 = 0; o1 = 315; }
                else if (angle == 180) { o0 = 180; o1 = 225; }
                else { o0 = 0; o1 = 180; }
            }
            else if (y == h)
            {
                if (x == 1)
                {
                    if (angle == 270) { o0 = 45; o1 = 0; }
                    else if (angle == 225) { o0 = 90; o1 = 0; }
                    else { o0 = 90; o1 = 45; }
                }
                else if (x == w)
                {
                    if (angle == 270) { o0 = 135; o1 = 180; }
                    else if (angle == 315) { o0 = 90; o1 = 180; }
                    else { o0 = 90; o1 = 135; }
                }
                else if (angle == 0) { o0 = 0; o1 = 45; }
                else if (angle == 180) { o0 = 180; o1 = 135; }
                else { o0 = 0; o1 = 180; }
            }
            else if (x == 1)
            {
                if (angle == 270) { o0 = 270; o1 = 315; }
                else if (angle == 90) { o0 = 90; o1 = 45; }
                else { o0 = 90; o1 = 270; }
            }
            else if (x != w)
            {
                o0 = AngleCorrect(45 + angle);
                o1 = AngleCorrect(angle - 45);
            }
            else if (angle == 270) { o0 = 270; o1 = 225; }
            else if (angle == 90) { o0 = 90; o1 = 135; }
            else { o0 = 90; o1 = 270; }
            out1 = Norm((int)Math.Round(o0));
            out2 = Norm((int)Math.Round(o1));
        }

        /// <summary>Default direction of the beam leaving a prism that combines two incoming beams.</summary>
        public static int PrismCombineDefault(int x, int y, int w, int h, int dir1, int dir2)
        {
            double a1 = dir1, a2 = dir2;
            double r;
            if (y == 1)
            {
                if (x == 1)
                {
                    if ((a1 == 90 && a2 == 135) || (a1 == 135 && a2 == 90)) r = 0;
                    else if ((a1 == 90 && a2 == 180) || (a1 == 180 && a2 == 90)) r = 315;
                    else r = 270;
                }
                else if (x == w)
                {
                    if ((a1 == 90 && a2 == 45) || (a1 == 45 && a2 == 90)) r = 180;
                    else if ((a1 == 90 && a2 == 0) || (a1 == 0 && a2 == 90)) r = 225;
                    else r = 270;
                }
                else
                {
                    double o = CombineAngles(a1, a2);
                    if (o >= 90 && o <= 180) r = (a1 == 0 || a2 == 0) ? 225 : 180;
                    else if (o != 45 && o != 0) r = o;
                    else r = (a1 == 180 || a2 == 180) ? 315 : 0;
                }
            }
            else if (y == h)
            {
                if (x == 1)
                {
                    if ((a1 == 270 && a2 == 225) || (a1 == 225 && a2 == 270)) r = 0;
                    else if ((a1 == 270 && a2 == 180) || (a1 == 180 && a2 == 270)) r = 45;
                    else r = 90;
                }
                else if (x == w)
                {
                    if ((a1 == 270 && a2 == 315) || (a1 == 315 && a2 == 270)) r = 180;
                    else if ((a1 == 270 && a2 == 0) || (a1 == 0 && a2 == 270)) r = 135;
                    else r = 90;
                }
                else
                {
                    double o = CombineAngles(a1, a2);
                    if (o >= 180 && o <= 270) r = (a1 == 0 || a2 == 0) ? 135 : 180;
                    else if (o != 315 && o != 0) r = o;
                    else r = (a1 == 180 || a2 == 180) ? 45 : 0;
                }
            }
            else if (x == 1)
            {
                double o = CombineAngles(a1, a2);
                if (o >= 90 && o <= 180) r = (a1 == 270 || a2 == 270) ? 45 : 90;
                else if (o != 225 && o != 270) r = o;
                else r = (a1 == 90 || a2 == 90) ? 315 : 270;
            }
            else if (x == w)
            {
                double o = CombineAngles(a1, a2);
                if (o >= 0 && o <= 90) r = (a1 == 270 || a2 == 270) ? 135 : 90;
                else if (o != 315 && o != 270) r = o;
                else r = (a1 == 90 || a2 == 90) ? 225 : 270;
            }
            else r = CombineAngles(a1, a2);
            return Norm((int)Math.Round(r));
        }
    }
}
