using System.Drawing;
using Microsoft.Win32;

namespace Bounce
{
    /// <summary>
    /// User-configurable appearance/behavior, persisted under
    /// HKCU\Software\Bounce so the config dialog ("/c") and the running
    /// screensaver ("/s") agree on it. <see cref="ImagePath"/> is the hook
    /// for swapping the plain circle for a company logo later - just point
    /// it at an image file, no code changes required.
    /// </summary>
    internal sealed class Settings
    {
        private const string RegistryKeyPath = @"Software\Bounce";

        // Bounds for the config dialog's sliders.
        public const int MinSpeed = 1;
        public const int MaxSpeed = 60;
        public const int MinTrailAgeSeconds = 1;
        public const int MaxTrailAgeSeconds = 30;
        public const int MinBallCount = 1;
        public const int MaxBallCount = 5;

        /// <summary>Sentinel for <see cref="TrailMaxAgeSeconds"/> meaning "never fade".</summary>
        public const int ForeverTrailAge = -1;

        public Color BallColor { get; set; } = Color.OrangeRed;
        public int BallSize { get; set; } = 48;
        public int Speed { get; set; } = 6;
        public string ImagePath { get; set; } = string.Empty;
        public bool ShowTrail { get; set; } = false;
        public int TrailMaxAgeSeconds { get; set; } = 5;
        public bool CornerFlash { get; set; } = false;
        public int BallCount { get; set; } = 1;

        public static Settings Load()
        {
            var settings = new Settings();

            using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                if (key == null)
                {
                    return settings;
                }

                object colorValue = key.GetValue("BallColor");
                if (colorValue is int argb)
                {
                    settings.BallColor = Color.FromArgb(argb);
                }

                object sizeValue = key.GetValue("BallSize");
                if (sizeValue is int size)
                {
                    settings.BallSize = size;
                }

                object speedValue = key.GetValue("Speed");
                if (speedValue is int speed)
                {
                    settings.Speed = speed;
                }

                object imagePathValue = key.GetValue("ImagePath");
                if (imagePathValue is string path)
                {
                    settings.ImagePath = path;
                }

                object showTrailValue = key.GetValue("ShowTrail");
                if (showTrailValue is int showTrail)
                {
                    settings.ShowTrail = showTrail != 0;
                }

                object trailAgeValue = key.GetValue("TrailMaxAgeSeconds");
                if (trailAgeValue is int trailAge)
                {
                    settings.TrailMaxAgeSeconds = trailAge;
                }

                object cornerFlashValue = key.GetValue("CornerFlash");
                if (cornerFlashValue is int cornerFlash)
                {
                    settings.CornerFlash = cornerFlash != 0;
                }

                object ballCountValue = key.GetValue("BallCount");
                if (ballCountValue is int ballCount)
                {
                    settings.BallCount = ballCount;
                }
            }

            return settings;
        }

        public void Save()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
            {
                key.SetValue("BallColor", BallColor.ToArgb(), RegistryValueKind.DWord);
                key.SetValue("BallSize", BallSize, RegistryValueKind.DWord);
                key.SetValue("Speed", Speed, RegistryValueKind.DWord);
                key.SetValue("ImagePath", ImagePath ?? string.Empty, RegistryValueKind.String);
                key.SetValue("ShowTrail", ShowTrail ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("TrailMaxAgeSeconds", TrailMaxAgeSeconds, RegistryValueKind.DWord);
                key.SetValue("CornerFlash", CornerFlash ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("BallCount", BallCount, RegistryValueKind.DWord);
            }
        }
    }
}
