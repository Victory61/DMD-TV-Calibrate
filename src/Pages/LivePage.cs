// HDRDisplayCalibrate — SDR/HDR display calibration for Windows.
// Copyright 2026 Andrii Orlov (DeadSue). Licensed under the Apache License, Version 2.0.
// Modified 2026 by DMD61 (Victory61) for DMD TV Calibrate: the live meter works on the TV patch window (window size
// and signal levels of the TV page), reads greys and colours; the monitor functions (DDC/CI auto OSD, room light,
// channel hints for the monitor menu) were removed — the guided TV steps are on the TV calibration page.
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HDRDisplayCalibrate.Core;
using HDRDisplayCalibrate.Tv;

namespace HDRDisplayCalibrate.Pages
{
    /// <summary>Continuous reading of one patch on the TV: grey or colour, with its values against the target.</summary>
    public sealed class LivePage : Page
    {
        static string T(string en) => S.T(en, en);
        static string Tf(string en, params object[] a) => S.F(en, en, a);

        static readonly string[] Levels = { "100", "90", "80", "75", "70", "60", "50", "40", "30", "20", "10", "5" };
        static readonly (string name, double r, double g, double b)[] Patches =
        {
            ("Grey", 1, 1, 1), ("Red", 1, 0, 0), ("Green", 0, 1, 0), ("Blue", 0, 0, 1), ("Cyan", 0, 1, 1), ("Magenta", 1, 0, 1), ("Yellow", 1, 1, 0),
        };

        readonly ComboBox _patch, _level;
        readonly TextBlock _big = new() { FontSize = 26, FontWeight = FontWeights.SemiBold, Text = "—" };
        readonly TextBlock _sub = new() { FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
        readonly Button _start;
        bool _running;

        public LivePage() : base(T("Live meter"),
            T("Continuous readings of one patch on the TV, while you change the settings yourself. The guided steps with suggested values are on the TV calibration page."))
        {
            _patch = Ui.Combo(Patches.Select(p => T(p.name)), 0, null, 200);
            _level = Ui.Combo(Levels.Select(l => l + " %"), 0, null, 120);
            _start = Ui.Btn(T("Start"), Toggle, true, "", tip: T("The TV shows the patch and the probe reads it continuously. Press again, or Esc, to stop."));
            Body.Children.Add(Ui.Card(Ui.H2(T("Patch")),
                Ui.Field(T("Colour"), _patch, tip: T("Grey for white balance and luminance; a colour to check it against Rec.709.")),
                Ui.Field(T("Level"), _level, tip: T("Signal level of the patch, in % of white.")),
                Ui.Caption(T("Window size and signal levels are the ones set on the TV calibration page.")),
                Ui.Row(_start), _big, _sub));
        }

        void SetButton(bool running)
        {
            if (_start.Content is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is TextBlock tb) tb.Text = running ? T("Stop") : T("Start");
        }

        void Toggle()
        {
            if (_running) { _running = false; return; }
            var d = St.Display;
            if (d == null) { MessageBox.Show(T("No display found."), AppInfo.Name); return; }
            if (Argyll.Bin == null) { MessageBox.Show(T("ArgyllCMS not found: the probe cannot be read."), AppInfo.Name); return; }
            var windows = TvTargets.Windows;
            double area = windows[Math.Clamp((int)St.Cfg.Num("tv_window", 0), 0, windows.Length - 1)];
            bool video = St.Setup.VideoRange;
            _running = true; SetButton(true);
            RunJob(async ct =>
            {
                TvMeter m = null;
                try
                {
                    int count = 0; bool? fast = null;
                    while (_running && !ct.IsCancellationRequested)
                    {
                        var p = Patches[Math.Max(0, _patch.SelectedIndex)];
                        double lev = double.Parse(Levels[Math.Max(0, _level.SelectedIndex)], CultureInfo.InvariantCulture) / 100;
                        // quick fixed-time readings for bright patches, the slower adaptive mode for dark greys
                        bool f = lev >= 0.4;
                        if (m == null || fast != f) { m?.Dispose(); m = new TvMeter(d, area, video, f); fast = f; await m.Read(p.r * lev, p.g * lev, p.b * lev, ct, 1200); }
                        var xyz = await m.Read(p.r * lev, p.g * lev, p.b * lev, ct, 300);
                        Show(xyz, p.name, lev);
                        St.Report(Tf("Live reading {0} · press Esc or Stop when done", ++count));
                    }
                }
                finally { m?.Dispose(); _running = false; SetButton(false); }
            });
        }

        void Show(V3 xyz, string patch, double lev)
        {
            var (x, y) = xyz.Sum > 0 ? xyz.Xy : (0.0, 0.0);
            _big.Text = $"{xyz.Y:F2} cd/m²   ·   x {x:F4}  y {y:F4}";
            if (patch == "Grey")
            {
                var (r, g, b) = TvAnalysis.Balance(xyz);
                _sub.Text = Tf("{0:F0} K   ·   ΔE to D65 {1:F2}   ·   RGB balance {2:F1} / {3:F1} / {4:F1} %", Cm.Cct(xyz), TvAdjust.GreyDeltaE(xyz), r, g, b);
            }
            else
            {
                // target chromaticity of the colour in Rec.709 (the same at any level)
                var p = Patches.First(q => q.name == patch);
                var t = Cm.SrgbToXyz * new V3(p.r, p.g, p.b);
                var (tx, ty) = t.Xy;
                _sub.Text = Tf("Target Rec.709: x {0:F4}  y {1:F4}   ·   difference x {2:+0.0000;-0.0000}  y {3:+0.0000;-0.0000}", tx, ty, x - tx, y - ty);
            }
        }
    }
}
