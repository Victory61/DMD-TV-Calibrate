// DMD TV Calibrate — TV calibration assistant.
// Copyright 2026 DMD61 (Victory61). Licensed under the Apache License, Version 2.0.
// Based on HDRDisplayCalibrate by Andrii Orlov (DeadSue).
using System.Collections.Generic;

namespace HDRDisplayCalibrate.Tv
{
    /// <summary>
    /// What a TV offers in its picture menu: which white balance and colour controls exist and their range.
    /// The assistant only suggests values for controls listed here. Ranges marked as "to be confirmed" are
    /// checked on a real set before they are relied on.
    /// </summary>
    public sealed class TvModel
    {
        public string Id = "", Name = "";
        public bool Generic;
        /// <summary>2-point white balance: gain (high) and offset / bias (low) for red, green and blue.</summary>
        public bool TwoPoint = true;
        public int TwoPointMin = -50, TwoPointMax = 50;
        /// <summary>Multi-point white balance: 0 (none), 10 or 20 points.</summary>
        public int MultiPoint;
        public bool MultiPointLuminance;
        public int MultiMin = -50, MultiMax = 50;
        /// <summary>Colour management (CMS): hue, saturation and luminance for R, G, B, C, M, Y.</summary>
        public bool Cms = true;
        public int CmsMin = -30, CmsMax = 30;
        public bool Oled;
        public string Note = "";
    }

    public static class TvModels
    {
        public const string GenericId = "generic";

        // One entry per group of TVs whose picture-menu controls differ (brand / panel type), not one per model:
        // a new entry is added only when a real difference in controls or ranges is confirmed.
        const string LgNote = "2-point and 20-point white balance, colour management. Ranges to be confirmed on the TV.";

        static TvModel Lg(string id, string name, bool oled) => new()
        {
            Id = id, Name = name, Oled = oled, TwoPoint = true, MultiPoint = 20, MultiPointLuminance = false, Cms = true, Note = LgNote,
        };

        public static readonly List<TvModel> All = new()
        {
            Lg("lg-oled", "LG OLED", true),
            Lg("lg-lcd", "LG LCD / LED", false),
            new TvModel
            {
                Id = GenericId, Name = "Other TV (generic)", Generic = true,
                TwoPoint = true, MultiPoint = 0, Cms = false,
                Note = "Tell the program which controls your TV has.",
            },
        };

        public static TvModel Find(string id) => All.Find(m => m.Id == id) ?? All[0];
    }
}
