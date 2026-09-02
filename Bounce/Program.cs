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
        ///   /p HWND        render into the small preview window - deliberately
        ///                  a no-op here; see the note on ScreensaverMode.Preview.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var mode = ScreensaverMode.Run;

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
                    // Embedding into the Screensaver Settings dialog's tiny
                    // preview area requires reparenting this process's window
                    // under a HWND owned by that (separate) process via
                    // classic Win32 SetParent/SetWindowLong calls. That
                    // legacy dialog is rarely used on modern Windows, and
                    // getting the reparenting to reliably render wasn't
                    // worth the complexity/risk - notably, an orphaned
                    // preview process that failed to embed had no way to
                    // notice its host was gone and exit. Recognizing /p and
                    // exiting immediately (rather than falling through to
                    // full-screen mode on top of the user's open dialog, or
                    // creating a window nothing will ever destroy) is the
                    // deliberate, low-risk choice here.
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
