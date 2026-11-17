// DMD TV Calibrate — TV calibration assistant.
// Copyright 2026 DMD61 (Victory61). Licensed under the Apache License, Version 2.0.
// Based on HDRDisplayCalibrate by Andrii Orlov (DeadSue).
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HDRDisplayCalibrate.Core;
using HDRDisplayCalibrate.Tv;

namespace HDRDisplayCalibrate.Pages
{
    /// <summary>What a guided step needs from the TV page when it starts.</summary>
    public sealed class TvStepContext
    {
        public DisplayTarget Display;
        public double Area;
        public bool Video;
        public double TargetNits;
        public M3 Basis;
        public V3 Black;
        public int Min, Max;
        public bool Oled;
        public bool Lg;
    }

    /// <summary>
    /// One guided step with continuous readings: the TV shows a grey, the program reads it again and again and
    /// tells which value to set in the TV menu. The user types in the boxes the values set in the menu, so the
    /// program learns how much one click changes the picture and the next suggestion is closer.
    /// </summary>
    public sealed class TvAdjustPanel
    {
        public enum Kind { Light, Gain, Offset }

        static string T(string en) => S.T(en, en);
        static string Tf(string en, params object[] a) => S.F(en, en, a);
        static readonly Brush[] ChannelBrush =
        {
            new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x5A)), new SolidColorBrush(Color.FromRgb(0x5A, 0xD2, 0x5A)), new SolidColorBrush(Color.FromRgb(0x5A, 0x9A, 0xFF)),
        };

        readonly Kind _kind;
        readonly double _level;
        readonly Func<TvStepContext> _context;
        readonly Action<Func<CancellationToken, Task>> _runJob;
        readonly TextBox[] _values;
        readonly ClickLearner[] _learn;
        readonly TextBlock _reading = new() { FontSize = 22, FontWeight = FontWeights.SemiBold, Text = "—", Margin = new Thickness(0, 8, 0, 4) };
        readonly StackPanel _advice = new();
        readonly TextBlock _verdict = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
        readonly Button _start;
        bool _running;
        TvStepContext _ctx;

        public UIElement View { get; }

        string Key(int i) => $"tv_adj_{_kind.ToString().ToLowerInvariant()}_{i}";
        static string[] ChannelNames => new[] { T("Red"), T("Green"), T("Blue") };

        public TvAdjustPanel(Kind kind, double level, Func<TvStepContext> context, Action<Func<CancellationToken, Task>> runJob, params UIElement[] intro)
        {
            _kind = kind; _level = level; _context = context; _runJob = runJob;
            var cfg = AppState.I.Cfg;
            int n = kind == Kind.Light ? 1 : 3;
            _values = new TextBox[n]; _learn = new ClickLearner[n];
            // first guess of one click (light change): checked against the readings as soon as a value is changed
            double guess = kind == Kind.Light ? 0.015 : kind == Kind.Gain ? 0.008 : 0.02;
            var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            for (int i = 0; i < n; i++)
            {
                _values[i] = Ui.Box(Ui.F(cfg.Num(Key(i), kind == Kind.Light ? 80 : 0), 0), 70);
                int k = i;
                _values[i].LostFocus += (_, _) => { cfg.Set(Key(k), Value(k)); cfg.Save(); };
                _learn[i] = new ClickLearner(guess);
                string label = kind == Kind.Light ? T("Value now in the TV menu") : ChannelNames[i];
                row.Children.Add(Ui.Stack(Ui.Caption(label), _values[i]));
                ((FrameworkElement)row.Children[i]).Margin = new Thickness(0, 0, 18, 0);
            }
            _start = Ui.Btn(T("Start readings"), Toggle, true, "", tip: T("The TV shows the patch and the probe reads it continuously. Press again, or Esc, to stop."));
            var body = Ui.Stack(intro);
            body.Children.Add(Ui.Caption(T("Write here the values that are set now in the TV menu (if you never changed them: 0, or the value you read in the menu).")));
            body.Children.Add(row);
            body.Children.Add(Ui.Row(_start));
            body.Children.Add(_reading);
            body.Children.Add(_advice);
            body.Children.Add(_verdict);
            body.Children.Add(Ui.Caption(T("Each time: set the suggested value on the TV with the remote, then write the same value in the box above and wait for the next reading. From the change the program learns how much one step of your TV is worth, and the suggestions get more precise.")));
            View = body;
        }

        int Value(int i) => (int)Math.Round(Ui.Num(_values[i], 0));

        void SetButton(bool running)
        {
            if (_start.Content is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is TextBlock tb) tb.Text = running ? T("Stop readings") : T("Start readings");
        }

        void Toggle()
        {
            if (_running) { _running = false; return; }
            _ctx = _context();
            if (_ctx == null) return;
            foreach (var l in _learn) l.Reset();
            _running = true; SetButton(true);
            _runJob(async ct =>
            {
                try
                {
                    using var m = new TvMeter(_ctx.Display, _ctx.Area, _ctx.Video, fast: _level >= 0.4);
                    AppState.I.Report(T("Starting the probe…"), 0);
                    await m.Read(_level, _level, _level, ct, 1200);
                    int count = 0;
                    while (_running && !ct.IsCancellationRequested)
                    {
                        var xyz = await m.Read(_level, _level, _level, ct, 300);
                        Show(xyz);
                        AppState.I.Report(Tf("Live reading {0} · press Esc or Stop readings when done", ++count));
                    }
                }
                finally { _running = false; SetButton(false); }
            });
        }

        void Show(V3 xyz)
        {
            _advice.Children.Clear();
            var (x, y) = xyz.Sum > 0 ? xyz.Xy : (0.0, 0.0);
            double de = TvAdjust.GreyDeltaE(xyz);
            _reading.Text = $"{xyz.Y:F2} cd/m²   ·   {Cm.Cct(xyz):F0} K   ·   ΔE {de:F2}   ·   x {x:F4}  y {y:F4}";

            if (_kind == Kind.Light)
            {
                int v = Value(0);
                _learn[0].Add(v, xyz.Y);
                double ratio = _ctx.TargetNits / Math.Max(xyz.Y, 1e-6);
                double pct = (xyz.Y / _ctx.TargetNits - 1) * 100;
                int to = Math.Clamp(v + _learn[0].Clicks(ratio), 0, 100);
                if (Math.Abs(pct) < 2) { _verdict.Text = "✓ " + Tf("White is at the target ({0:F0} cd/m²). Go on with the next step.", _ctx.TargetNits); _verdict.Foreground = Ui.Good; }
                else
                {
                    _verdict.Text = Tf("White {0:+0.0;-0.0} % from the target {1:F0} cd/m²: set the value to {2}", pct, _ctx.TargetNits, to) + (_learn[0].Learned ? "" : "  (" + T("estimate") + ")");
                    _verdict.Foreground = Ui.Warm;
                    if (to == v) _verdict.Text += " — " + T("the change is smaller than one step: keep this value.");
                }
                return;
            }

            // Gain: only lower channels, so none clips. Offset: green stays, red and blue move both ways.
            var r = TvAdjust.Ratios(xyz, _ctx.Black, _ctx.Basis, _kind == Kind.Gain ? -1 : 1);
            var amounts = TvAdjust.Amounts(xyz, _ctx.Black, _ctx.Basis);
            bool ok = true;
            for (int c = 0; c < 3; c++)
            {
                int v = Value(c);
                _learn[c].Add(v, amounts[c]);
                double pct = (r[c] - 1) * 100;
                int to = Math.Clamp(v + _learn[c].Clicks(r[c]), _ctx.Min, _ctx.Max);
                bool chOk = Math.Abs(pct) < 0.5 || to == v;
                ok &= chOk;
                string text = chOk
                    ? $"{ChannelNames[c]}:  {v}   ✓"
                    : Tf("{0}:  {1}  →  set {2}   ({3:+0.0;-0.0} %)", ChannelNames[c], v, to, pct) + (_learn[c].Learned ? "" : "  (" + T("estimate") + ")");
                _advice.Children.Add(new TextBlock { Text = text, FontSize = 15, Foreground = chOk ? Brushes.White : ChannelBrush[c], Margin = new Thickness(0, 2, 0, 2) });
            }
            if (ok && de < 1.5) { _verdict.Text = "✓ " + T("The grey is neutral: go on with the next step."); _verdict.Foreground = Ui.Good; }
            else if (ok) { _verdict.Text = T("Each channel is within one step of the target: this is as close as these controls get."); _verdict.Foreground = Ui.Good; }
            else { _verdict.Text = T("Change one value at a time on the TV, write it here and wait for the next reading."); _verdict.Foreground = Ui.Warm; }
        }
    }
}
