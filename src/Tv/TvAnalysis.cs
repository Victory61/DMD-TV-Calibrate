// DMD TV Calibrate — TV calibration assistant.
// Copyright 2026 DMD61 (Victory61). Licensed under the Apache License, Version 2.0.
// Based on HDRDisplayCalibrate by Andrii Orlov (DeadSue).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HDRDisplayCalibrate.Core;

namespace HDRDisplayCalibrate.Tv
{
    public sealed class GreyPoint
    {
        public double Level;            // stimulus 0..1
        public V3 Xyz;                  // measured, cd/m²
        public double TargetNits;
        public double R, G, B;          // RGB balance, % (100 = neutral D65 at this luminance)
        public double? Gamma;           // point gamma relative to black and white
        public double Cct, DeltaE;
        public double Nits => Xyz.Y;
    }

    public sealed class ColourPoint
    {
        public TvPatch Patch;
        public V3 Xyz, Target;
        public double DeltaE;
    }

    /// <summary>Results of one measurement pass against Rec.709 / D65 and the chosen response curve.</summary>
    public sealed class TvResult
    {
        public DateTime When = DateTime.Now;
        public string Curve = "bt1886";
        public List<GreyPoint> Greys = new();
        public List<ColourPoint> Colours = new();
        public double WhiteNits, BlackNits, Contrast, WhiteCct, WhiteDeltaE, GammaAvg;
        public double GreyMeanDE, GreyMaxDE, ColourMeanDE, ColourMaxDE;
        public (double x, double y) WhiteXy;
    }

    public static class TvAnalysis
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        /// <summary>Linear Rec.709 RGB → XYZ (white D65 with Y = 1). Rec.709 and sRGB share primaries and white.</summary>
        static readonly M3 ToXyz = Cm.SrgbToXyz;
        static readonly M3 ToRgb = Cm.SrgbToXyz.Inverse();

        /// <summary>Linear RGB of a measured colour in Rec.709, scaled so that a neutral D65 grey of luminance Y gives (Y, Y, Y).</summary>
        public static V3 Rgb(V3 xyz) => ToRgb * xyz;

        /// <summary>RGB balance in %: each channel relative to the luminance (100 / 100 / 100 = neutral D65).</summary>
        public static (double r, double g, double b) Balance(V3 xyz)
        {
            var c = Rgb(xyz); double y = Math.Max(xyz.Y, 1e-9);
            return (c.X / y * 100, c.Y / y * 100, c.Z / y * 100);
        }

        public static TvResult Analyze(List<(double level, V3 xyz)> greys, List<(TvPatch p, V3 xyz)> colours, string curve)
        {
            var r = new TvResult { Curve = curve };
            var sorted = greys.OrderBy(g => g.level).ToList();
            var white = sorted.Last().xyz; var black = sorted.First().xyz;
            r.WhiteNits = white.Y; r.BlackNits = Math.Max(black.Y, 0);
            r.Contrast = r.BlackNits > 1e-6 ? r.WhiteNits / r.BlackNits : 0;
            r.WhiteXy = white.Xy; r.WhiteCct = Cm.Cct(white);
            double kRel = r.BlackNits / Math.Max(r.WhiteNits, 1e-9);
            var refWhite = Cm.XyToXyz(TvTargets.D65.x, TvTargets.D65.y, r.WhiteNits);
            V3 Lab(V3 v) => Cm.Lab(v, refWhite);

            foreach (var (level, xyz) in sorted)
            {
                double tgtY = r.WhiteNits * Cm.Eotf(curve, level, kRel);
                var tgt = Cm.XyToXyz(TvTargets.D65.x, TvTargets.D65.y, Math.Max(tgtY, 1e-6));
                var (br, bg, bb) = Balance(xyz);
                var gp = new GreyPoint { Level = level, Xyz = xyz, TargetNits = tgtY, R = br, G = bg, B = bb, Cct = xyz.Y > 0.05 ? Cm.Cct(xyz) : 0 };
                double rel = (xyz.Y - r.BlackNits) / Math.Max(r.WhiteNits - r.BlackNits, 1e-9);
                if (level > 0.04 && level < 0.999 && rel > 0) gp.Gamma = Math.Log(rel) / Math.Log(level);
                gp.DeltaE = level == 0 ? 0 : Cm.DeltaE2000(Lab(tgt), Lab(xyz));
                r.Greys.Add(gp);
            }
            var counted = r.Greys.Where(g => g.Level >= 0.1).ToList();
            if (counted.Count > 0) { r.GreyMeanDE = counted.Average(g => g.DeltaE); r.GreyMaxDE = counted.Max(g => g.DeltaE); }
            var gam = r.Greys.Where(g => g.Gamma != null && g.Level >= 0.1 && g.Level <= 0.9).Select(g => g.Gamma.Value).ToList();
            r.GammaAvg = gam.Count > 0 ? gam.Average() : 0;
            r.WhiteDeltaE = Cm.DeltaE2000(Lab(Cm.XyToXyz(TvTargets.D65.x, TvTargets.D65.y, r.WhiteNits)), Lab(white));

            foreach (var (p, xyz) in colours)
            {
                var lin = new V3(Cm.Eotf(curve, p.R, kRel), Cm.Eotf(curve, p.G, kRel), Cm.Eotf(curve, p.B, kRel));
                var tgt = ToXyz * lin * r.WhiteNits;
                r.Colours.Add(new ColourPoint { Patch = p, Xyz = xyz, Target = tgt, DeltaE = Cm.DeltaE2000(Lab(tgt), Lab(xyz)) });
            }
            if (r.Colours.Count > 0) { r.ColourMeanDE = r.Colours.Average(c => c.DeltaE); r.ColourMaxDE = r.Colours.Max(c => c.DeltaE); }
            return r;
        }

        /// <summary>Saves the raw readings and the computed values as a CSV file (opens in Excel or any text editor).</summary>
        public static string SaveCsv(TvResult r, string dir, string name)
        {
            Directory.CreateDirectory(dir);
            var f = Path.Combine(dir, name + ".csv");
            var sb = new StringBuilder();
            sb.AppendLine("type;name;level;X;Y;Z;x;y;target_Y;R%;G%;B%;gamma;CCT;dE2000");
            foreach (var g in r.Greys)
            {
                var (x, y) = g.Xyz.Sum > 0 ? g.Xyz.Xy : (0.0, 0.0);
                sb.AppendLine(string.Join(";", "grey", "", F(g.Level), F(g.Xyz.X), F(g.Xyz.Y), F(g.Xyz.Z), F(x), F(y), F(g.TargetNits),
                    F(g.R), F(g.G), F(g.B), g.Gamma is double gm ? F(gm) : "", F(g.Cct, 0), F(g.DeltaE)));
            }
            foreach (var c in r.Colours)
            {
                var (x, y) = c.Xyz.Sum > 0 ? c.Xyz.Xy : (0.0, 0.0);
                sb.AppendLine(string.Join(";", "colour", c.Patch.Colour, c.Patch.Percent.ToString(Inv), F(c.Xyz.X), F(c.Xyz.Y), F(c.Xyz.Z), F(x), F(y), F(c.Target.Y),
                    "", "", "", "", "", F(c.DeltaE)));
            }
            File.WriteAllText(f, sb.ToString(), new UTF8Encoding(true));
            return f;
        }

        static string F(double v, int d = 4) => v.ToString("F" + d, Inv);
    }
}
