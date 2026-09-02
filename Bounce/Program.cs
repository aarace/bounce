using System;
using System.Windows.Forms;

namespace Bounce
{
    internal static class Program
    {
        /// <summary>
        /// Standard Windows screensaver entry point. Windows (and the
        /// Screensaver Settings dialog) invoke a .scr with one of:
        ///   (no args)      run full-screen, same as /s
        ///   /s             run full-screen
        ///   /c  or /c:HWND show the configuration dialog
        ///   /p HWND        render into the small preview window
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var mode = ScreensaverMode.Run;
            var previewHandle = IntPtr.Zero;

            if (args.Length > 0)
            {
                string arg = args[0].Trim();
                string switchPart = arg.Length >= 2 ? arg.Substring(0, 2).ToLowerInvariant() : arg.ToLowerInvariant();

                if (switchPart == "/c" || switchPart == "-c")
                {
                    mode = ScreensaverMode.Configure;
                }
                else if (switchPart == "/p" || switchPart == "-p")
                {
                    mode = ScreensaverMode.Preview;

                    string handleText = arg.Length > 2 ? arg.Substring(2).TrimStart(':', ' ') : string.Empty;
                    if (string.IsNullOrEmpty(handleText) && args.Length > 1)
                    {
                        handleText = args[1];
                    }

                    long handleValue;
                    if (long.TryParse(handleText, out handleValue))
                    {
                        previewHandle = new IntPtr(handleValue);
                    }
                }
                else if (switchPart == "/s" || switchPart == "-s")
                {
                    mode = ScreensaverMode.Run;
                }
            }

            switch (mode)
            {
                case ScreensaverMode.Configure:
                    Application.Run(new ConfigForm());
                    break;

                case ScreensaverMode.Preview:
                    if (previewHandle != IntPtr.Zero)
                    {
                        Application.Run(new PreviewForm(previewHandle));
                    }
                    break;

                case ScreensaverMode.Run:
                default:
                    using (var session = new ScreensaverSession())
                    {
                        session.Start();
                        Application.Run();
                    }
                    break;
            }
        }

        private enum ScreensaverMode
        {
            Run,
            Configure,
            Preview
        }
    }
}
