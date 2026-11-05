using System;
using DJMaxEditor.Preview;

namespace DJMaxEditor
{
    /// <summary>
    /// Central access to reversible editor features. Timeline V2 remains opt-in until its rollout
    /// gates pass; missing or malformed values always select the legacy editor.
    /// </summary>
    public static class FeatureFlags
    {
        public static bool UseTimelineV2
        {
            get { return Properties.Settings.Default.UseTimelineV2; }
        }

        public static bool ParseUseTimelineV2(string value)
        {
            bool enabled;
            return Boolean.TryParse(value, out enabled) && enabled;
        }

        public static void SetUseTimelineV2(bool enabled)
        {
            Properties.Settings.Default.UseTimelineV2 = enabled;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// The persisted SPRITE SET choice for the gameplay preview, as a catalog style id
        /// (<c>auto</c>, <c>packaged</c>, <c>t2local</c>, <c>t3:star:3</c>…). Missing or
        /// blank values fall back to <c>auto</c>; an id the catalog can no longer resolve
        /// (its directory was deleted, the settings file moved machines) falls back the same
        /// way when the catalog resolves it.
        /// </summary>
        public static string PreviewSpriteStyleId
        {
            get
            {
                string value = Properties.Settings.Default.PreviewSpriteStyle;
                return string.IsNullOrWhiteSpace(value)
                    ? TechnikaSpriteStyle.AutoId
                    : value;
            }
        }

        public static void SetPreviewSpriteStyleId(string id)
        {
            Properties.Settings.Default.PreviewSpriteStyle =
                string.IsNullOrWhiteSpace(id) ? TechnikaSpriteStyle.AutoId : id;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// An explicit DJMax Technika 3 <c>Resource\MainGame</c> folder (or any folder inside
        /// it), taking precedence over the environment variable, the Steam-library sweep and
        /// the portable mirror when the catalog probes. Empty means "probe everything", which
        /// is the default.
        /// </summary>
        public static string Technika3AssetPath
        {
            get
            {
                return Properties.Settings.Default.Technika3AssetPath ?? string.Empty;
            }
        }

        public static void SetTechnika3AssetPath(string path)
        {
            Properties.Settings.Default.Technika3AssetPath = path ?? string.Empty;
            Properties.Settings.Default.Save();
        }
    }
}
