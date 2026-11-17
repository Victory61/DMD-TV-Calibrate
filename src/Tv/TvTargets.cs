// DMD TV Calibrate — TV calibration assistant.
// Copyright 2026 DMD61 (Victory61). Licensed under the Apache License, Version 2.0.
// Based on HDRDisplayCalibrate by Andrii Orlov (DeadSue).
using System.Collections.Generic;
using System.Linq;

namespace HDRDisplayCalibrate.Tv
{
    /// <summary>A test colour: English colour name (translated when shown), level in %, stimulus 0..1 per channel (before video-level coding).</summary>
    public sealed record TvPatch(string Colour, int Percent, double R, double G, double B);

    /// <summary>SDR targets (Rec.709 primaries, D65 white) and the patch sets used by the measurements.</summary>
    public static class TvTargets
    {
        public static readonly (double x, double y) D65 = (0.3127, 0.3290);
        public static readonly (double x, double y) Red = (0.64, 0.33), Green = (0.30, 0.60), Blue = (0.15, 0.06);

        /// <summary>Response curves offered as target: Argyll / ColorMath names.</summary>
        public static readonly string[] Curves = { "bt1886", "2.4", "2.2" };
        public static readonly string[] CurveNames = { "BT.1886", "2.4", "2.2" };

        /// <summary>Measurement window as a fraction of the screen area.</summary>
        public static readonly double[] Windows = { 0.10, 0.18, 0.25, 1.0 };

        /// <summary>Grey levels 0..1: 11 steps (every 10 %) or 21 steps (every 5 %).</summary>
        public static List<double> GreyLevels(int steps) =>
            Enumerable.Range(0, steps).Select(i => i / (double)(steps - 1)).ToList();

        /// <summary>Primaries and secondaries at 100 % and 75 %.</summary>
        public static List<TvPatch> Colours()
        {
            var l = new List<TvPatch>();
            foreach (var (lev, pc) in new[] { (1.0, 100), (0.75, 75) })
            {
                l.Add(new TvPatch("Red", pc, lev, 0, 0));
                l.Add(new TvPatch("Green", pc, 0, lev, 0));
                l.Add(new TvPatch("Blue", pc, 0, 0, lev));
                l.Add(new TvPatch("Cyan", pc, 0, lev, lev));
                l.Add(new TvPatch("Magenta", pc, lev, 0, lev));
                l.Add(new TvPatch("Yellow", pc, lev, lev, 0));
            }
            return l;
        }
    }
}
