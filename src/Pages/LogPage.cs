// HDRDisplayCalibrate — SDR/HDR display calibration for Windows.
// Copyright 2026 Andrii Orlov (DeadSue). Licensed under the Apache License, Version 2.0.
// Modified 2026 by DMD61 (Victory61) for DMD TV Calibrate: moved to its own file when the monitor pages were removed.
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using HDRDisplayCalibrate.Core;

namespace HDRDisplayCalibrate.Pages
{
    public sealed class LogPage : Page
    {
        readonly TextBox _box = new() { IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas"), FontSize = 12, TextWrapping = System.Windows.TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 600 };
        public LogPage() : base(S.T("Log", "Журнал"), null)
        {
            Body.Children.Add(_box);
            Body.Children.Add(Ui.Row(Ui.Btn(S.T("Open log file", "Відкрити файл журналу"), () => Process.Start(new ProcessStartInfo(Path.Combine(Paths.Home, "log.txt")) { UseShellExecute = true }), tip: S.T("Opens the program's detailed log, useful when reporting a problem.", "Відкриває детальний журнал програми — знадобиться, якщо повідомляєте про проблему.")),
                                     Ui.Btn(S.T("Measurements folder", "Тека замірів"), () => Process.Start("explorer.exe", Paths.Docs), tip: S.T("Opens the folder with all measurements, profiles, reports and LUTs.", "Відкриває теку з усіма замірами, профілями, звітами та LUT."))));
            Log.Line += s => Dispatcher.BeginInvoke(() => { _box.AppendText(s + "\n"); if (_box.LineCount > 4000) _box.Clear(); _box.ScrollToEnd(); });
        }
    }
}
