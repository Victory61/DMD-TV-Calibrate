// DMD TV Calibrate — TV calibration assistant.
// Copyright 2026 DMD61 (Victory61). Licensed under the Apache License, Version 2.0.
// Based on HDRDisplayCalibrate by Andrii Orlov (DeadSue); placement code modelled on its PatchWindow.
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using HDRDisplayCalibrate.Core;

namespace HDRDisplayCalibrate.Tv
{
    /// <summary>
    /// Full-screen pattern window for the TV: a centred patch covering a chosen fraction of the screen
    /// (10 % for OLED and local-dimming sets) on black, with optional aiming marks for placing the probe.
    /// Values are coded either as video levels (16–235) or PC levels (0–255).
    /// </summary>
    public sealed class TvPatchWindow : Window
    {
        readonly Border _patch = new() { Background = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        readonly Canvas _aim = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        readonly TextBlock _msg = new()
        {
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)), FontSize = 26, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(40, 0, 40, 50), Visibility = Visibility.Collapsed,
        };
        readonly DisplayTarget _d;
        readonly bool _video;
        double _area;

        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        static readonly IntPtr HwndTopmost = new(-1);
        const uint SWP_SHOWWINDOW = 0x0040;

        public TvPatchWindow(DisplayTarget d, double area, bool video)
        {
            _d = d; _video = video; _area = area;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false;
            Title = AppInfo.Name + " patches";
            WindowStartupLocation = WindowStartupLocation.Manual; Left = -32000; Top = -32000; Width = 200; Height = 200;
            byte k = Code(0);
            Background = new SolidColorBrush(Color.FromRgb(k, k, k));
            var g = new Grid();
            g.Children.Add(_patch); g.Children.Add(_aim); g.Children.Add(_msg);
            Content = g;
            SizeChanged += (_, _) => LayoutPatch();
            SourceInitialized += (_, _) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                var (x, y, w, hh) = _d.Bounds;
                SetWindowPos(h, HwndTopmost, x, y, w, hh, SWP_SHOWWINDOW);
            };
            Cursor = Cursors.None;
            // the window covers the whole screen: Esc always gets out (during a measurement it cancels it),
            // and with the aiming mark a mouse click closes it too
            Loaded += (_, _) => { Activate(); Focus(); };
            KeyDown += (_, e) =>
            {
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                if (AppState.I.Job != null) AppState.I.Cancel(); else Close();
            };
            MouseDown += (_, _) => { if (AppState.I.Job == null && _aim.Visibility == Visibility.Visible) Close(); };
        }

        /// <summary>8-bit code for a stimulus 0..1: video levels 16–235 or PC levels 0–255.</summary>
        public byte Code(double v)
        {
            v = Math.Clamp(v, 0, 1);
            return (byte)Math.Round(_video ? 16 + 219 * v : 255 * v);
        }

        public void SetArea(double area) { _area = area; LayoutPatch(); }

        void LayoutPatch()
        {
            double k = Math.Sqrt(Math.Clamp(_area, 0.01, 1));
            _patch.Width = ActualWidth * k; _patch.Height = ActualHeight * k;
            DrawAim();
        }

        public void Paint(double r, double g, double b) => _patch.Background = new SolidColorBrush(Color.FromRgb(Code(r), Code(g), Code(b)));

        /// <summary>Shows or hides the aiming circle and cross in the centre, with an optional message at the bottom.</summary>
        public void ShowAim(bool on, string message = null)
        {
            _aim.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            _msg.Text = message ?? "";
            _msg.Visibility = on && !string.IsNullOrEmpty(message) ? Visibility.Visible : Visibility.Collapsed;
            DrawAim();
        }

        void DrawAim()
        {
            _aim.Children.Clear();
            if (_aim.Visibility != Visibility.Visible || ActualWidth <= 0) return;
            double cx = ActualWidth / 2, cy = ActualHeight / 2, r = Math.Min(ActualWidth, ActualHeight) * 0.05;
            var b = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00));
            var ring = new Ellipse { Width = 2 * r, Height = 2 * r, Stroke = b, StrokeThickness = 4 };
            Canvas.SetLeft(ring, cx - r); Canvas.SetTop(ring, cy - r);
            _aim.Children.Add(ring);
            _aim.Children.Add(new Line { X1 = cx - 2.2 * r, Y1 = cy, X2 = cx + 2.2 * r, Y2 = cy, Stroke = b, StrokeThickness = 3 });
            _aim.Children.Add(new Line { X1 = cx, Y1 = cy - 2.2 * r, X2 = cx, Y2 = cy + 2.2 * r, Stroke = b, StrokeThickness = 3 });
        }

        /// <summary>Waits until WPF has rendered the new colour and a few display refreshes have passed.</summary>
        public async Task Rendered()
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(80);
        }
    }

    /// <summary>One spotread session plus the TV pattern window: paint a colour, wait, read XYZ (cd/m²).</summary>
    public sealed class TvMeter : IDisposable
    {
        readonly SpotSession _spot;
        public readonly TvPatchWindow Win;

        readonly int? _lutDisplay;

        /// <param name="fast">Fixed short integration (live readings of bright patches); off = adaptive, better for dark greys.</param>
        public TvMeter(DisplayTarget d, double area, bool video, bool fast = false)
        {
            // a calibration curve loaded in the graphics card for this display would change what the TV receives:
            // it is set to linear while measuring and put back afterwards
            _lutDisplay = d.ArgyllNumber;
            if (_lutDisplay is int n) try { Argyll.LutLinear(n); } catch (Exception e) { Log.Write("tv lut linear: " + e.Message); }
            Win = Application.Current.Dispatcher.Invoke(() => { var w = new TvPatchWindow(d, area, video); w.Show(); return w; });
            _spot = new SpotSession(AppState.I.Setup, "-e", fast);
        }

        public async Task<V3> Read(double r, double g, double b, System.Threading.CancellationToken ct, int settleMs = 600)
        {
            await Application.Current.Dispatcher.InvokeAsync(() => Win.Paint(r, g, b));
            await Application.Current.Dispatcher.Invoke(() => Win.Rendered());
            await Task.Delay(settleMs, ct);
            return await _spot.ReadAsync(ct);
        }

        public void Dispose()
        {
            Application.Current.Dispatcher.Invoke(() => { if (Win.IsLoaded) Win.Close(); });
            _spot.Dispose();
            if (_lutDisplay is int n) try { Argyll.LoadInstalled(n); } catch (Exception e) { Log.Write("tv lut restore: " + e.Message); }
        }
    }
}
