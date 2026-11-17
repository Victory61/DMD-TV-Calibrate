// DMD TV Calibrate — TV calibration assistant.
// Copyright 2026 DMD61 (Victory61). Licensed under the Apache License, Version 2.0.
// Based on HDRDisplayCalibrate by Andrii Orlov (DeadSue).
using System;
using System.Linq;
using HDRDisplayCalibrate.Core;

namespace HDRDisplayCalibrate.Tv
{
    /// <summary>
    /// Maths behind the guided steps: how much each channel of the TV must change, and how many menu steps
    /// ("clicks") that is. The size of one click is not known in advance for a given TV, so it starts from an
    /// estimate and is learned from the readings each time the user changes a value in the TV menu.
    /// </summary>
    public static class TvAdjust
    {
        /// <summary>RGB basis of the TV: its measured primaries (columns R, G, B) when an initial measurement exists,
        /// otherwise the Rec.709 primaries. Only the directions matter, not the scale.</summary>
        public static M3 Basis(TvResult r)
        {
            if (r != null)
            {
                ColourPoint P(string c) => r.Colours.FirstOrDefault(x => x.Patch.Colour == c && x.Patch.Percent == 100);
                var pr = P("Red"); var pg = P("Green"); var pb = P("Blue");
                if (pr != null && pg != null && pb != null && pr.Xyz.Y > 0.05 && pg.Xyz.Y > 0.05 && pb.Xyz.Y > 0.01)
                {
                    var black = Black(r);
                    var m = M3.FromColumns(pr.Xyz - black, pg.Xyz - black, pb.Xyz - black);
                    try { m.Inverse(); return m; } catch (InvalidOperationException) { }
                }
            }
            return Cm.SrgbToXyz;
        }

        /// <summary>The black level read in the initial measurement (0 % grey), or zero.</summary>
        public static V3 Black(TvResult r)
        {
            var g = r?.Greys.FirstOrDefault(x => x.Level == 0);
            return g != null && g.Xyz.Y >= 0 ? g.Xyz : new V3(0, 0, 0);
        }

        /// <summary>
        /// Factor by which each channel (R, G, B, in linear light) must be multiplied to make the grey read as D65
        /// at the same luminance. <paramref name="keep"/>: -1 = only lower channels (the highest stays at 1, so none
        /// clips, used for gain), 0/1/2 = that channel stays as it is (green for offset).
        /// </summary>
        public static V3 Ratios(V3 xyz, V3 black, M3 basis, int keep)
        {
            var inv = basis.Inverse();
            var meas = xyz - black;
            var now = inv * meas;
            var target = Cm.XyToXyz(TvTargets.D65.x, TvTargets.D65.y, Math.Max(meas.Y, 1e-6));
            var want = inv * target;
            double R(int i) => now[i] > 1e-9 ? want[i] / now[i] : 1;
            var r = new V3(R(0), R(1), R(2));
            return keep < 0 ? r / r.Max : r / r[keep];
        }

        /// <summary>Amount of each channel in linear light (for learning the click size).</summary>
        public static V3 Amounts(V3 xyz, V3 black, M3 basis) => basis.Inverse() * (xyz - black);

        /// <summary>ΔE2000 of a grey against D65 at the same luminance.</summary>
        public static double GreyDeltaE(V3 xyz)
        {
            var t = Cm.XyToXyz(TvTargets.D65.x, TvTargets.D65.y, Math.Max(xyz.Y, 1e-6));
            return Cm.DeltaE2000(Cm.Lab(t, t), Cm.Lab(xyz, t));
        }
    }

    /// <summary>
    /// Learns how much one click of a menu control changes the light (in natural-log units, ~ fraction of change).
    /// Each reading is stored with the value the user says is set in the menu; when the value changes, the change
    /// in light between the last reading at the old value and the new reading gives the size of a click.
    /// </summary>
    public sealed class ClickLearner
    {
        readonly double _default;
        int? _lastVal, _anchorVal;
        double _lastLn, _anchorLn;
        public double Size { get; private set; }
        public bool Learned { get; private set; }

        public ClickLearner(double defaultSize) { _default = defaultSize; Size = defaultSize; }

        /// <summary>Records a reading: <paramref name="value"/> = value set in the menu, <paramref name="amount"/> = light measured.</summary>
        public void Add(int value, double amount)
        {
            if (amount <= 0) return;
            double ln = Math.Log(amount);
            if (_lastVal is int lv && lv != value) { _anchorVal = lv; _anchorLn = _lastLn; }
            if (_anchorVal is int av && av != value)
            {
                double s = (ln - _anchorLn) / (value - av);
                // keep only sensible values: the light must move the same way as the control, by 0.1–20 % per click
                if (s >= 0.001 && s <= 0.2) { Size = s; Learned = true; }
            }
            _lastVal = value; _lastLn = ln;
        }

        /// <summary>Clicks needed for a change of light by the factor <paramref name="ratio"/> (rounded, 0 when within half a click).</summary>
        public int Clicks(double ratio) => ratio <= 0 ? 0 : (int)Math.Round(Math.Log(ratio) / Size);

        public void Reset() { _lastVal = _anchorVal = null; Size = _default; Learned = false; }
    }
}
