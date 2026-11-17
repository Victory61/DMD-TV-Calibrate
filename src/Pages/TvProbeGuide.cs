// DMD TV Calibrate — TV calibration assistant.
// Copyright 2026 DMD61 (Victory61). Licensed under the Apache License, Version 2.0.
// Based on HDRDisplayCalibrate by Andrii Orlov (DeadSue); adapted from the sensor dialogs (ProbeGuide) for TVs.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HDRDisplayCalibrate.Pages
{
    /// <summary>How to put the colorimeter on a TV: hanging in contact with the counterweight, or on a tripod without contact.</summary>
    public sealed class TvProbeGuide : Window
    {
        static string T(string en) => S.T(en, en);

        /// <summary>Shows the guide with "Probe in place — start" / "Cancel"; returns false if cancelled.</summary>
        public static bool Ask() => new TvProbeGuide(true).ShowDialog() == true;

        /// <summary>Shows the guide for reading only.</summary>
        public static void Info() => new TvProbeGuide(false).ShowDialog();

        TvProbeGuide(bool ask)
        {
            Title = Core.AppInfo.Name; SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; Owner = Application.Current.MainWindow; Topmost = true;
            if (S.Rtl) FlowDirection = FlowDirection.RightToLeft;

            var body = new StackPanel { Margin = new Thickness(24, 20, 24, 20), MaxWidth = 760 };
            body.Children.Add(Ui.H2(T("Place the probe on the TV")));
            body.Children.Add(Diagram());

            var cols = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            cols.ColumnDefinitions.Add(new ColumnDefinition()); cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); cols.ColumnDefinitions.Add(new ColumnDefinition());
            var a = Ui.Stack(Ui.H2(T("A — Contact, with the counterweight")), Numbered(
                T("Turn the lens cover (diffuser) aside so the lens is uncovered."),
                T("Rest the probe flat on the glass, on the orange cross in the centre of the screen."),
                T("Hang the cable over the top edge of the TV with the counterweight behind it."),
                T("On thin OLED panels check that the cable rests well on the edge and the probe does not slide down.")));
            var b = Ui.Stack(Ui.H2(T("B — Tripod, without contact (recommended for OLED)")), Numbered(
                T("Screw the probe onto the tripod: the 1/4\" thread is on the rotating arm (lens cover)."),
                T("Turn the arm so the lens is uncovered and points at the screen."),
                T("Aim the lens at the orange cross, straight on, 2–5 cm from the glass, without touching it."),
                T("The room must be dark: without contact, room light reaches the lens.")));
            Grid.SetColumn(b, 2); cols.Children.Add(a); cols.Children.Add(b);
            body.Children.Add(cols);

            body.Children.Add(Note(T("Switch the TV on at least 30 minutes before measuring and plug the probe in a few minutes before. Do not move the probe until the program tells you it can be removed.")));
            body.Children.Add(Note(T("During the measurement the TV shows black, grey and coloured patches for a few minutes: do not touch the mouse, the keyboard or the probe.")));

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            if (ask)
            {
                var cancel = Ui.Btn(T("Cancel"), () => { DialogResult = false; });
                cancel.IsCancel = true; cancel.MinWidth = 100; cancel.Margin = new Thickness(0, 0, 8, 0);
                buttons.Children.Add(cancel);
                var ok = Ui.Btn(T("Probe in place — start"), () => { DialogResult = true; }, true);
                ok.IsDefault = true; ok.MinWidth = 160; ok.Margin = new Thickness(0);
                buttons.Children.Add(ok);
            }
            else
            {
                var ok = Ui.Btn("OK", () => { DialogResult = true; }, true);
                ok.IsDefault = true; ok.IsCancel = true; ok.MinWidth = 120; ok.Margin = new Thickness(0);
                buttons.Children.Add(ok);
            }
            body.Children.Add(buttons);
            Content = body;
        }

        static UIElement Numbered(params string[] lines)
        {
            var list = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            for (int i = 0; i < lines.Length; i++)
            {
                var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.Children.Add(new TextBlock { Text = (i + 1) + ".", Foreground = Ui.Accent, FontWeight = FontWeights.SemiBold });
                var t = Ui.Text(lines[i]); Grid.SetColumn(t, 1); g.Children.Add(t);
                list.Children.Add(g);
            }
            return list;
        }

        static UIElement Note(string text) => new Border
        {
            BorderBrush = Ui.Warm, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(10, 6, 6, 6), Margin = new Thickness(0, 8, 0, 0),
            Child = Ui.Text(text)
        };

        /// <summary>Front view (probe on the cross, cable over the top) and side view with a tripod.</summary>
        static UIElement Diagram()
        {
            var c = new Canvas { Width = 700, Height = 210, Margin = new Thickness(0, 6, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
            void At(UIElement e, double x, double y) { Canvas.SetLeft(e, x); Canvas.SetTop(e, y); c.Children.Add(e); }
            TextBlock L(string t, Brush br, double w = 0) => w > 0 ? new TextBlock { Text = t, FontSize = 12, Foreground = br, Width = w, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap } : new TextBlock { Text = t, FontSize = 12, Foreground = br };
            var orange = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00));

            // A: front view of the TV, probe hanging in the centre
            At(L(T("A — contact"), Ui.Muted), 120, 0);
            At(new Rectangle { Width = 290, Height = 160, Stroke = Ui.Muted, StrokeThickness = 3, RadiusX = 3, RadiusY = 3 }, 10, 20);
            At(new Rectangle { Width = 80, Height = 6, Fill = Ui.Muted }, 115, 186);
            c.Children.Add(new Line { X1 = 155, Y1 = 20, X2 = 155, Y2 = 84, Stroke = Ui.Accent, StrokeThickness = 2 });
            c.Children.Add(new Line { X1 = 130, Y1 = 100, X2 = 180, Y2 = 100, Stroke = orange, StrokeThickness = 2 });
            c.Children.Add(new Line { X1 = 155, Y1 = 75, X2 = 155, Y2 = 125, Stroke = orange, StrokeThickness = 2 });
            At(new Ellipse { Width = 32, Height = 32, Fill = Brushes.Black, Stroke = Ui.Accent, StrokeThickness = 3 }, 139, 84);
            At(L(T("probe on the orange cross"), Ui.Warm, 280), 15, 140);
            At(L(T("cable over the top, counterweight behind"), Ui.Muted, 280), 15, 192);

            // B: side view, tripod in front of the screen
            At(L(T("B — tripod, no contact"), Ui.Muted), 470, 0);
            c.Children.Add(new Line { X1 = 640, Y1 = 22, X2 = 640, Y2 = 180, Stroke = Ui.Muted, StrokeThickness = 6 });     // TV seen from the side
            At(new Rectangle { Width = 26, Height = 22, Fill = Brushes.Black, Stroke = Ui.Accent, StrokeThickness = 2 }, 602, 89);   // probe, lens towards the screen
            c.Children.Add(new Line { X1 = 615, Y1 = 111, X2 = 615, Y2 = 150, Stroke = Ui.Muted, StrokeThickness = 4 });   // tripod head and column
            c.Children.Add(new Line { X1 = 615, Y1 = 150, X2 = 585, Y2 = 195, Stroke = Ui.Muted, StrokeThickness = 3 });
            c.Children.Add(new Line { X1 = 615, Y1 = 150, X2 = 615, Y2 = 195, Stroke = Ui.Muted, StrokeThickness = 3 });
            c.Children.Add(new Line { X1 = 615, Y1 = 150, X2 = 645, Y2 = 195, Stroke = Ui.Muted, StrokeThickness = 3 });
            At(L(T("2–5 cm from the glass"), Ui.Warm, 150), 440, 92);
            return c;
        }
    }
}
