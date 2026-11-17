// DMD TV Calibrate — TV calibration assistant for LG TVs.
// Copyright 2026 DMD61 (Victory61). Licensed under the Apache License, Version 2.0.
// Based on HDRDisplayCalibrate by Andrii Orlov (DeadSue).
namespace HDRDisplayCalibrate.Core
{
    /// <summary>Program identity: the visible name and the folder name used for settings, logs and documents.
    /// Kept apart from HDRDisplayCalibrate so both programs can be installed side by side.</summary>
    public static class AppInfo
    {
        public const string Name = "DMD TV Calibrate";
        public const string Folder = "DMDTVCalibrate";
        public const string Version = "0.1.0";
        public const string Credit = "Based on HDRDisplayCalibrate by Andrii Orlov (DeadSue)";
    }
}
