// DMD TV Calibrate — TV calibration assistant.
// Copyright 2026 DMD61 (Victory61). Licensed under the Apache License, Version 2.0.
// Based on HDRDisplayCalibrate by Andrii Orlov (DeadSue).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HDRDisplayCalibrate.Core;
using HDRDisplayCalibrate.Tv;

namespace HDRDisplayCalibrate.Pages
{
    /// <summary>
    /// TV calibration: settings, probe placement and the initial measurement (grey scale and colours against
    /// Rec.709 / D65). The guided adjustment steps (OLED light, 2-point, multi-point, CMS) follow in the next version.
    /// </summary>
    public sealed class TvPage : Page
    {
        static string T(string en) => S.T(en, en);
        static string Tf(string en, params object[] a) => S.F(en, en, a);

        static readonly Brush RedB = new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x5A));
        static readonly Brush GreenB = new SolidColorBrush(Color.FromRgb(0x5A, 0xD2, 0x5A));
        static readonly Brush BlueB = new SolidColorBrush(Color.FromRgb(0x5A, 0x9A, 0xFF));

        readonly ComboBox _model, _panel, _levels, _window, _curve, _greys, _gen, _multi;
        readonly CheckBox _two, _cms;
        readonly TextBox _nits = Ui.Box("100", 80);
        readonly StackPanel _controls = new();
        readonly TextBlock _levelsHint = Ui.Caption("");
        readonly StackPanel _status = new();
        readonly StackPanel _results = new();
        readonly List<UIElement> _reportCards = new();
        readonly Button _aimBtn, _exportBtn;
        TvPatchWindow _aimWin;
        bool _loading = true;

        public TvPage() : base(T("TV calibration"),
            T("Measures your TV with the colorimeter and tells you, step by step, the values to enter in the TV menu. Nothing is written into the TV: you change the settings yourself with the remote."))
        {
            var cfg = St.Cfg;
            // a TV normally receives video levels: the first time this page opens, switch the patches to 16–235
            if (!cfg.Bool("tv_levels_init", false)) { St.Setup.VideoRange = true; cfg.Set("tv_levels_init", true); St.SaveSetup(); }

            Body.Children.Add(Ui.Guide(T("Before you start"),
                T("Connect the PC to the TV with HDMI and set Windows to Extend these displays. In Display & sensor choose the TV as the display and check that the probe is found."),
                T("On the TV choose the PC input and the most accurate picture mode (Cinema, Expert / ISF, Filmmaker); switch off every processing (dynamic contrast, vivid colours, sharpness, energy saving, light sensor)."),
                T("Windows HDR must be off for the TV: this is the SDR calibration."),
                T("Switch the TV on at least 30 minutes before measuring. With the probe in contact soft light is enough, with no direct light on the screen; with the tripod the room must be dark.")));
            Body.Children.Add(Ui.Card(Ui.H2(T("Check")), _status));

            // ---- 1. settings
            var model = TvModels.Find(cfg.Str("tv_model", TvModels.All[0].Id));
            _model = Ui.Combo(TvModels.All.Select(m => m.Generic ? T(m.Name) : m.Name), TvModels.All.IndexOf(model), _ => ModelChanged(), 380);
            _panel = Ui.Combo(new[] { "OLED", T("LCD / LED") }, (int)cfg.Num("tv_panel", model.Oled ? 0 : 1), _ => Save(), 380);
            _levels = Ui.Combo(new[] { T("Video levels 16–235 (TV, recommended)"), T("PC levels 0–255") }, St.Setup.VideoRange ? 0 : 1,
                i => { if (_loading) return; St.Setup.VideoRange = i == 0; St.SaveSetup(); OnShow(); }, 380);
            _window = Ui.Combo(TvTargets.Windows.Select(w => $"{w * 100:F0}%"), (int)cfg.Num("tv_window", 0), _ => Save(), 120);
            _curve = Ui.Combo(TvTargets.CurveNames, (int)cfg.Num("tv_curve", 0), _ => Save(), 160);
            _greys = Ui.Combo(new[] { T("11 points (every 10 %)"), T("21 points (every 5 %)") }, (int)cfg.Num("tv_greys", model.MultiPoint >= 20 ? 1 : 0), _ => Save(), 260);
            _nits.Text = Ui.F(cfg.Num("tv_white", 100), 0);
            _nits.LostFocus += (_, _) => Save();
            _gen = new ComboBox { MinWidth = 380, HorizontalAlignment = HorizontalAlignment.Left };
            _gen.Items.Add(T("PC — HDMI output"));
            _gen.Items.Add(new ComboBoxItem { Content = T("Raspberry Pi with PGenerator — coming in a next version"), IsEnabled = false });
            _gen.SelectedIndex = 0;
            // the TV's menu controls: filled in from the TV choice (or the user's own ticks for Other TV), always visible
            // a listed TV always starts from its own controls; the saved ticks are used for Other TV (generic) only
            bool own = !model.Generic;
            _two = Ui.Check(T("2-point white balance (gain and offset)"), own ? model.TwoPoint : cfg.Bool("tv_gen_2p", true));
            _multi = Ui.Combo(new[] { T("none"), "10", "20" }, own ? MultiIndex(model.MultiPoint) : (int)cfg.Num("tv_gen_mp", 0), _ => MultiChanged(), 120);
            _cms = Ui.Check(T("Colour management (CMS)"), own ? model.Cms : cfg.Bool("tv_gen_cms", false));
            _two.Click += (_, _) => Save(); _cms.Click += (_, _) => Save();
            _controls.Children.Add(_two);
            _controls.Children.Add(Ui.Row(Ui.Text(T("Multi-point white balance")), _multi, Ui.Caption(T("points"))));
            _controls.Children.Add(_cms);
            _controls.Children.Add(Ui.Caption(T("Filled in from the TV choice. Untick a control your TV does not have in its picture menu.")));

            Body.Children.Add(Ui.Card(Ui.H2(T("1 · Settings")),
                Ui.Text(T("Choose your TV. The choice only tells the program which controls the TV picture menu has, so that in the next steps it suggests only those; the measurement is the same for every TV. For a TV not in the list choose Other TV (generic) and tick the controls it has. When you change the choice, the recommended values below are set for you: you can still change them.")),
                Ui.Field(T("TV"), _model, tip: T("LG OLED and LG LCD / LED fill in the controls of the LG picture menu. Other TV (generic) works with any brand.")),
                Ui.Field(T("Controls in the TV menu"), _controls, tip: T("The white balance and colour settings of the TV picture menu. The program suggests values only for the controls ticked here.")),
                Ui.Field(T("Panel"), _panel, tip: T("OLED or LCD / LED. OLED and sets with local dimming are measured with a small window (10 %).")),
                Ui.Field(T("Signal levels"), Ui.Stack(_levels, _levelsHint), tip: T("Video levels 16–235: set the TV black level to Low / Limited. PC levels 0–255: set it to High / Full. They must match, otherwise black and white are wrong.")),
                Ui.Field(T("Measurement window"), _window, T("share of the screen"), tip: T("Size of the measured patch. 10 % for OLED and local dimming (the TV does not dim it); 18–25 % or full screen for other LCDs.")),
                Ui.Field(T("Target gamma"), _curve, tip: T("BT.1886 or 2.4 for a dark room; 2.2 for a lit room.")),
                Ui.Field(T("Target white"), Ui.Row(_nits, Ui.Text("cd/m²")), T("about 100 in a dark room"), tip: T("Luminance of 100 % white you will set with OLED light / backlight in the next step.")),
                Ui.Field(T("Grey scale"), _greys, tip: T("Number of grey levels measured. 21 points match the 20-point white balance of many TVs.")),
                Ui.Field(T("Pattern generator"), _gen, tip: T("Who shows the patches on the TV. For now the PC through HDMI; the Raspberry Pi (more precise, needed for HDR) will be added."))));

            // ---- 2. probe
            _aimBtn = Ui.Btn(T("Show aiming mark on the TV"), ToggleAim, false, "", tip: T("Shows a grey patch with an orange cross in the centre of the TV, to place the probe."));
            Body.Children.Add(Ui.Card(Ui.H2(T("2 · Place the probe")),
                Ui.Text(T("Put the probe in the centre of the TV: hanging in contact with the counterweight, or on a tripod a few centimetres from the screen (no contact, recommended for OLED).")),
                Ui.Row(Ui.Btn(T("How to place the probe"), TvProbeGuide.Info, false, ""), _aimBtn)));

            // ---- 3. initial measurement
            _exportBtn = Ui.Btn(T("Export report"), Export, false, "", tip: T("Saves the results as PDF or HTML."));
            _exportBtn.IsEnabled = false;
            Body.Children.Add(Ui.Card(Ui.H2(T("3 · Initial measurement")),
                Ui.Text(T("Measures the grey scale and the colours as they are now, before any change: it is the reference to compare with at the end. It takes a few minutes.")),
                Ui.Row(Ui.Btn(T("Start initial measurement"), StartInitial, true, ""), _exportBtn),
                _results));

            // ---- next steps
            Body.Children.Add(Ui.Card(Ui.H2(T("Next steps (next version)")),
                Ui.Text(T("4 · OLED light / backlight to the target white")),
                Ui.Text(T("5 · 2-point white balance")),
                Ui.Text(T("6 · Multi-point white balance")),
                Ui.Text(T("7 · Colours (CMS)")),
                Ui.Text(T("8 · Final check and report")),
                Ui.Caption(T("For each control the program will tell you the value to set in the TV menu, then measure again."))));

            _loading = false;
        }

        TvModel Model => TvModels.All[Math.Max(0, _model.SelectedIndex)];
        double Area => TvTargets.Windows[Math.Max(0, _window.SelectedIndex)];
        string Curve => TvTargets.Curves[Math.Max(0, _curve.SelectedIndex)];

        static int MultiIndex(int points) => points >= 20 ? 2 : points >= 10 ? 1 : 0;

        /// <summary>A new TV choice fills in its menu controls and the recommended panel, window and grey scale.
        /// Other TV (generic) keeps what the user ticked and chose.</summary>
        void ModelChanged()
        {
            if (_loading) return;
            var m = Model;
            _loading = true;
            if (!m.Generic)
            {
                _two.IsChecked = m.TwoPoint;
                _multi.SelectedIndex = MultiIndex(m.MultiPoint);
                _cms.IsChecked = m.Cms;
                _panel.SelectedIndex = m.Oled ? 0 : 1;
                _window.SelectedIndex = m.Oled ? 0 : 1;        // 10 % for OLED, 18 % for LCD (10 % if it has local dimming)
                _greys.SelectedIndex = m.MultiPoint >= 20 ? 1 : 0;
            }
            _loading = false;
            Save();
        }

        /// <summary>20-point white balance is checked with 21 grey levels, the others with 11.</summary>
        void MultiChanged()
        {
            if (_loading) return;
            _loading = true;
            _greys.SelectedIndex = _multi.SelectedIndex == 2 ? 1 : 0;
            _loading = false;
            Save();
        }

        void Save()
        {
            if (_loading) return;
            var c = St.Cfg;
            c.Set("tv_model", Model.Id); c.Set("tv_panel", _panel.SelectedIndex); c.Set("tv_window", _window.SelectedIndex);
            c.Set("tv_curve", _curve.SelectedIndex); c.Set("tv_greys", _greys.SelectedIndex); c.Set("tv_white", Ui.Num(_nits, 100));
            c.Set("tv_gen_2p", _two.IsChecked == true); c.Set("tv_gen_mp", _multi.SelectedIndex); c.Set("tv_gen_cms", _cms.IsChecked == true);
            c.Save();
        }

        public override void OnShow()
        {
            _loading = true;
            _levels.SelectedIndex = St.Setup.VideoRange ? 0 : 1;
            _loading = false;
            _status.Children.Clear();
            var d = St.Display;
            TextBlock Line(string text, Brush b) => new() { Text = text, Foreground = b, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
            _status.Children.Add(Line(d == null ? "✗ " + T("No display found.") : "✓ " + Tf("Display: {0} ({1}×{2}). Change it in Display & sensor if this is not the TV.", d.Name, d.Bounds.w, d.Bounds.h), d == null ? Ui.Bad : Brushes.White));
            if (Argyll.Bin == null) _status.Children.Add(Line("✗ " + T("ArgyllCMS not found: the probe cannot be read."), Ui.Bad));
            if (d != null && d.HdrOn)
            {
                _status.Children.Add(Line("⚠ " + T("Windows HDR is on for this display: turn it off for the SDR calibration."), Ui.Warm));
                _status.Children.Add(Ui.Row(Ui.Btn(T("Turn HDR off"), async () => { Displays.SetHdr(d, false); await Task.Delay(6000); St.RefreshDisplays(); OnShow(); }, false, "")));
            }
            else if (d != null) _status.Children.Add(Line("✓ " + T("Windows HDR is off (SDR)."), Brushes.White));
            string levels = St.Setup.VideoRange
                ? T("Video levels 16–235: set the TV black level to Low / Limited.")
                : T("PC levels 0–255: set the TV black level to High / Full.");
            _status.Children.Add(Line("ⓘ " + levels, Ui.Muted));
            _levelsHint.Text = levels;              // the same advice right under the choice, where it is made
        }

        void ToggleAim()
        {
            if (_aimWin != null) { CloseAim(); return; }
            if (St.Display == null) { MessageBox.Show(T("No display found."), AppInfo.Name); return; }
            if (!OtherScreenOk(St.Display)) return;
            _aimWin = new TvPatchWindow(St.Display, Math.Max(Area, 0.18), St.Setup.VideoRange);
            _aimWin.Closed += (_, _) => { _aimWin = null; _aimBtn.Content = T("Show aiming mark on the TV"); };
            _aimWin.Show();
            _aimWin.Paint(0.5, 0.5, 0.5);
            _aimWin.ShowAim(true, T("Put the probe on the orange cross. Then press Esc or click to close this screen."));
            _aimBtn.Content = T("Hide aiming mark");
        }

        /// <summary>The patches cover the whole chosen screen. If that is the screen showing this program (TV not
        /// connected or not chosen), say so and let the user decide; Esc always closes the patches.</summary>
        bool OtherScreenOk(DisplayTarget d)
        {
            var w = Window.GetWindow(this);
            if (w == null) return true;
            var c = w.PointToScreen(new Point(w.ActualWidth / 2, w.ActualHeight / 2));   // physical pixels, like d.Bounds
            var (x, y, bw, bh) = d.Bounds;
            if (c.X < x || c.X >= x + bw || c.Y < y || c.Y >= y + bh) return true;
            return MessageBox.Show(Tf("The selected display ({0}) is the one showing this program: the patches would cover this window.\n\nConnect the TV with HDMI, set Windows to Extend these displays and choose the TV in Display & sensor.\n\nContinue anyway? Press Esc to close the patches.", d.Name),
                AppInfo.Name, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        void CloseAim()
        {
            _aimWin?.Close(); _aimWin = null;
            _aimBtn.Content = T("Show aiming mark on the TV");
        }

        void StartInitial()
        {
            var d = St.Display;
            if (d == null) { MessageBox.Show(T("No display found."), AppInfo.Name); return; }
            if (Argyll.Bin == null) { MessageBox.Show(T("ArgyllCMS not found: the probe cannot be read."), AppInfo.Name); return; }
            if (d.HdrOn && MessageBox.Show(T("Windows HDR is on for this display. The SDR calibration should be done with HDR off. Continue anyway?"), AppInfo.Name, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            if (!OtherScreenOk(d)) return;
            CloseAim();
            if (!TvProbeGuide.Ask()) return;

            double area = Area; bool video = St.Setup.VideoRange; string curve = Curve;
            var levels = TvTargets.GreyLevels(_greys.SelectedIndex == 1 ? 21 : 11);
            var colours = TvTargets.Colours();
            RunJob(async ct =>
            {
                var greys = new List<(double, V3)>();
                var cols = new List<(TvPatch, V3)>();
                using (var m = new TvMeter(d, area, video))
                {
                    int total = levels.Count + colours.Count, done = 0;
                    St.Report(T("Starting the probe…"), 0);
                    await m.Read(1, 1, 1, ct, 1500);                  // first reading after opening the probe is discarded
                    foreach (var lv in levels)
                    {
                        St.Report(Tf("Grey {0}% ({1}/{2})", Math.Round(lv * 100), ++done, total), done / (double)total);
                        greys.Add((lv, await m.Read(lv, lv, lv, ct, lv < 0.1 ? 1200 : 600)));
                    }
                    foreach (var p in colours)
                    {
                        St.Report(Tf("{0} {1}% ({2}/{3})", T(p.Colour), p.Percent, ++done, total), done / (double)total);
                        cols.Add((p, await m.Read(p.R, p.G, p.B, ct)));
                    }
                }
                var res = TvAnalysis.Analyze(greys, cols, curve);
                string csv = null;
                try { csv = TvAnalysis.SaveCsv(res, Path.Combine(Paths.Docs, "TV"), $"{DateTime.Now:yyyy-MM-dd_HHmm}_initial"); }
                catch (Exception e) { Log.Write("tv csv: " + e.Message); }
                ShowResults(res, csv);
                St.Report(T("Initial measurement done. Leave the probe in place if you go on calibrating."), 1);
            });
        }

        static Brush Grade(double de) => de < 1 ? Ui.Good : de < 3 ? Ui.Warm : Ui.Bad;

        void ShowResults(TvResult r, string csv)
        {
            _results.Children.Clear(); _reportCards.Clear();
            string curveName = TvTargets.CurveNames[Math.Max(0, Array.IndexOf(TvTargets.Curves, r.Curve))];

            // summary tiles
            var tiles = new WrapPanel();
            void Tile(string label, string value, Brush b = null)
            {
                var t = Ui.Stack(Ui.Caption(label), new TextBlock { Text = value, FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = b ?? Brushes.White });
                tiles.Children.Add(new Border { Child = t, Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 10, 10), CornerRadius = new CornerRadius(8), Background = Ui.CardBg, MinWidth = 150 });
            }
            Tile(T("White"), $"{r.WhiteNits:F1} cd/m²");
            Tile(T("Black / contrast"), r.Contrast > 0 ? $"{r.BlackNits:F4} · {r.Contrast:F0}:1" : $"{r.BlackNits:F4}");
            Tile(T("White point"), $"{r.WhiteCct:F0} K · ΔE {r.WhiteDeltaE:F2}", Grade(r.WhiteDeltaE));
            Tile(T("Average gamma (10–90 %)"), Ui.F(r.GammaAvg) + "  (" + curveName + ")");
            Tile(T("Grey ΔE avg / max"), $"{r.GreyMeanDE:F2} / {r.GreyMaxDE:F2}", Grade(r.GreyMeanDE));
            Tile(T("Colour ΔE avg / max"), $"{r.ColourMeanDE:F2} / {r.ColourMaxDE:F2}", Grade(r.ColourMeanDE));
            var summary = Ui.Card(Ui.H2(Tf("Initial measurement · {0:g}", r.When)), tiles,
                Ui.Caption(T("ΔE2000: below 1 excellent, below 3 hard to see, above 3 visible.")));

            // RGB balance and gamma
            var greysUsed = r.Greys.Where(g => g.Level >= 0.1).ToList();
            var bal = new LineChart { XLabel = T("grey level, %"), YLabel = T("RGB balance, %"), XMin = 0, XMax = 100, YMin = 80, YMax = 120, Height = 260 };
            bal.Data.Add(new LineChart.Series(T("target"), new List<(double, double)> { (0, 100), (100, 100) }, Ui.Muted, true));
            bal.Data.Add(new LineChart.Series(T("red"), greysUsed.Select(g => (g.Level * 100, g.R)).ToList(), RedB, false, true));
            bal.Data.Add(new LineChart.Series(T("green"), greysUsed.Select(g => (g.Level * 100, g.G)).ToList(), GreenB, false, true));
            bal.Data.Add(new LineChart.Series(T("blue"), greysUsed.Select(g => (g.Level * 100, g.B)).ToList(), BlueB, false, true));
            var gam = new LineChart { XLabel = T("grey level, %"), YLabel = T("gamma"), XMin = 0, XMax = 100, YMin = 1.6, YMax = 2.8, Height = 220 };
            double tg = r.Curve == "2.2" ? 2.2 : 2.4;
            gam.Data.Add(new LineChart.Series(T("target") + " (" + curveName + ")", new List<(double, double)> { (0, tg), (100, tg) }, Ui.Muted, true));
            gam.Data.Add(new LineChart.Series(T("measured"), r.Greys.Where(g => g.Gamma != null).Select(g => (g.Level * 100, g.Gamma.Value)).ToList(), Ui.Warm, false, true));
            var greyCard = Ui.Card(Ui.H2(T("Grey scale")),
                Ui.Caption(T("RGB balance: the three lines should stay close to 100 % at every level.")), bal,
                Ui.Caption(T("Gamma at each level")), gam,
                Ui.Table(new[] { T("Level"), "cd/m²", T("target"), T("gamma"), "R %", "G %", "B %", "K", "ΔE" },
                    r.Greys.Select(g => new[] { $"{g.Level * 100:F0}%", Ui.F(g.Nits, 3), Ui.F(g.TargetNits, 3), g.Gamma is double v ? Ui.F(v) : "—",
                        Ui.F(g.R, 1), Ui.F(g.G, 1), Ui.F(g.B, 1), g.Cct > 0 ? Ui.F(g.Cct, 0) : "—", g.Level == 0 ? "—" : Ui.F(g.DeltaE) })));

            // colours
            var xy = new ChromaticityChart { Height = 380 };
            xy.Triangles.Add(new ChromaticityChart.Tri("Rec.709", TvTargets.Red, TvTargets.Green, TvTargets.Blue, Ui.Muted, false, true));
            ColourPoint P(string c) => r.Colours.FirstOrDefault(x => x.Patch.Colour == c && x.Patch.Percent == 100);
            var pr = P("Red"); var pg = P("Green"); var pb = P("Blue");
            if (pr != null && pg != null && pb != null)
                xy.Triangles.Add(new ChromaticityChart.Tri(T("measured"), pr.Xyz.Xy, pg.Xyz.Xy, pb.Xyz.Xy, Ui.Accent, true));
            foreach (var c in new[] { "Cyan", "Magenta", "Yellow" }) { var p = P(c); if (p != null) xy.Points.Add((p.Xyz.Xy, Ui.Accent, T(c))); }
            xy.Points.Add((TvTargets.D65, Brushes.White, "D65"));
            xy.Points.Add((r.WhiteXy, Ui.Warm, T("white")));
            var de = new DeltaEChart();
            foreach (var g in r.Greys.Where(g => g.Level > 0)) { byte v = (byte)Math.Round(g.Level * 255); de.Bars.Add((Color.FromRgb(v, v, v), g.DeltaE)); }
            foreach (var c in r.Colours) de.Bars.Add((Color.FromRgb((byte)(c.Patch.R * 255), (byte)(c.Patch.G * 255), (byte)(c.Patch.B * 255)), c.DeltaE));
            var colourCard = Ui.Card(Ui.H2(T("Colours")),
                Ui.Caption(T("Primaries and white (CIE 1931 xy): dashed triangle = Rec.709 target")), xy,
                Ui.Caption(T("ΔE2000 for each patch (bar colour = patch colour)")), de,
                Ui.Table(new[] { T("Colour"), "cd/m²", T("target"), "x", "y", "ΔE" },
                    r.Colours.Select(c => { var (cx, cy) = c.Xyz.Sum > 0 ? c.Xyz.Xy : (0.0, 0.0); return new[] { $"{T(c.Patch.Colour)} {c.Patch.Percent}%", Ui.F(c.Xyz.Y, 2), Ui.F(c.Target.Y, 2), Ui.F(cx, 4), Ui.F(cy, 4), Ui.F(c.DeltaE) }; })));

            foreach (var card in new UIElement[] { summary, greyCard, colourCard }) { _results.Children.Add(card); _reportCards.Add(card); }
            if (csv != null) _results.Children.Add(Ui.Caption(Tf("Readings saved in {0}", csv)));
            _exportBtn.IsEnabled = true;
        }

        void Export() => ReportExport.Export(T("TV measurement"), $"TV_{DateTime.Now:yyyy-MM-dd_HHmm}", _reportCards);
    }
}
