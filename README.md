# Bounce

A Windows screensaver (.NET 10 / WinForms) that bounces a ball around your
**real multi-monitor layout** — the same arrangement you see under
Settings → System → Display — rather than treating your desktop as one
rectangle. It correctly bounces off the seam where two monitors of
different resolution or offset meet, including the concave "notch" corners
that creates. Falls back cleanly to a single screen when there's only one.

> This only makes sense with monitors set to **Extend** (not Duplicate) —
> that's what puts each monitor at a distinct position in the coordinate
> space `Screen.AllScreens` reports, which is what the whole simulation is
> built on.

## Opening it

Open `Bounce.sln` in Visual Studio 2022 (17.8+, for .NET 10 SDK support).
Press **F5** — the default launch profile runs with the `/s` argument, so
it goes full-screen across all your monitors immediately, exactly like a
real screensaver would. Move the mouse, click, or press a key to exit.

To test the configuration dialog instead, use the dropdown next to the
Start button (▶ **Bounce (Run /s)** ▾) and pick **Bounce (Configure /c)**,
then F5. These are defined in `Properties/launchSettings.json`. There's no
profile for `/p <hwnd>` (preview) - see the note on it below.

## How it works

- **`Simulation/MonitorRegion.cs`** — builds a `System.Drawing.Region` that
  is the union of every `Screen.Bounds` rectangle: the actual "walkable"
  area, matching whatever layout you've dragged into in Display Settings.
  It also measures the real gap between every pair of monitors that face
  each other and bridges exactly that much (up to a sanity cap) - two
  monitors Display Settings shows snapped together can still have a small
  real gap between their raw pixel bounds (mixed DPI, or just imprecise
  dragging), which would otherwise read as a solid wall.
- **`Simulation/BouncingBall.cs`** — each tick, checks whether moving on
  the X axis alone, Y axis alone, and both together stay inside that
  region. That's what makes it bounce correctly off a real edge, slide
  along a wall when only one axis is blocked, and correctly handle the
  concave corner where two differently sized monitors meet (where a
  diagonal step would cut through empty space even though each axis alone
  looks fine). It also tracks `FlashIntensity`, which spikes to 1 on a true
  corner hit (both axes reflected at once) and decays over ~20 ticks -
  always tracked, but only rendered as a flash when **Flash on corner hit**
  is checked in Configure.
- **`MonitorWindow.cs`** — one borderless, topmost window *per physical
  monitor*, sized and positioned to exactly match that monitor's own
  bounds. This matters: Windows applies a single DPI scale factor to a
  top-level window based on whichever monitor it's on, so one giant window
  spanning monitors with different resolutions/DPI gets the parts outside
  its "home" monitor bitmap-stretched by the OS instead of drawn natively
  — that's what caused a squashed-looking ball and a wrongly-sized/blank
  area on other monitors in mixed-DPI setups. A native window per monitor
  avoids that.
- **`ScreensaverSession.cs`** — owns the shared simulation (one or more
  `BouncingBall`s, per the **Number of balls** setting - each spawned on a
  random monitor) and the whole set of `MonitorWindow`s, runs the single
  animation timer that steps every ball and repaints every window each
  tick, and wires exit-on-input (mouse move/click, key press) across all
  of them.
- **`Program.cs`** — parses the standard `.scr` arguments: `/s` (run),
  `/c` (configure), `/p <hwnd>` (preview - deliberately a no-op; see below).
- **`Settings.cs`** / **`ConfigForm.cs`** — ball color, size, count, speed,
  an optional image file, corner-flash, and trail settings, persisted to
  `HKCU\Software\Bounce`.
- **`Simulation/BallTrail.cs`** / **`TrailRenderer.cs`** — an optional
  fading "shadow" trail behind the ball(s) (checkbox + length slider in the
  config dialog). Points are recorded in the same absolute coordinates as
  the ball(s), tagged with a ball id so segments never connect one ball's
  point to a different ball's. A finite (fading) trail's segment count is
  naturally bounded by its max age, so `MonitorWindow` just redraws it each
  frame like the ball. A "Forever" trail never stops growing and never
  changes once drawn, though, so each `MonitorWindow` instead paints each
  new segment exactly once onto its own persistent per-monitor bitmap as
  it's recorded, and every frame just blits that - redrawing an
  ever-growing trail from scratch every frame was what made long
  forever-mode runs get progressively choppier. Also doubles as a
  coverage-testing tool - see below.

## Using a logo instead of a plain ball

Run with `/c` (the **Bounce (Configure /c)** launch profile in VS, the
**Configure** button from Screensaver Settings, or `Bounce.exe /c`
directly) and browse to an image file. No code changes needed —
`MonitorWindow` already draws that image scaled to the configured ball
size instead of a circle whenever one is set.

## Installing it as a real screensaver

Windows just runs whatever `.scr` you give it. Two ways to get one:

- **Quick/local use**: every build already copies `Bounce.exe` to
  `Bounce.scr` in the output folder (see the `CopyExeAsScr` target in
  `Bounce.csproj`). Right-click that `.scr` → **Install**, or copy it (and
  the rest of the output folder, since it's framework-dependent by
  default) into `C:\Windows\System32`. Requires the .NET 10 Desktop
  Runtime on the machine.
- **Self-contained (no runtime dependency, for deploying to other
  machines)**:

  ```
  dotnet publish Bounce\Bounce.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
  ren publish\Bounce.exe Bounce.scr
  ```

  Then install `publish\Bounce.scr` the same way. All the other files in
  the `publish` folder must stay alongside it.
- **No admin rights (`Install-Screensaver.ps1`)**: the Screen Saver
  Settings dropdown only lists `.scr` files in System32, which needs admin
  rights to copy into - but Windows will run whatever screensaver is named
  in the per-user registry value `HKCU\Control Panel\Desktop\SCRNSAVE.EXE`
  regardless of where it lives. Run `.\Install-Screensaver.ps1` from the
  repo root (auto-finds a build under `Bounce\bin\`, or pass `-Path`), and
  `.\Install-Screensaver.ps1 -Uninstall` to revert. It just won't appear in
  the Settings dropdown, since that only reads System32.

## Testing multi-monitor coverage

To visually confirm the ball can reach every part of your layout (useful
after rearranging monitors): open **Configure**, turn the **Speed** slider
up, check **Show trail behind the ball**, and drag **Trail length** all the
way to the right to **Forever**. Run the screensaver and let it go for a
while - the accumulating trail should eventually paint over every screen.
Any patch of a screen that never lights up points at a gap in
`MonitorRegion` (see `MaxBridgeableGap` in `Simulation/MonitorRegion.cs` if
two monitors that should be adjacent aren't getting bridged).

## Notes

- The small thumbnail in the legacy Screensaver Settings dialog (`/p`) is
  intentionally left blank rather than rendered into. Embedding into it
  requires reparenting this process's window under a HWND owned by that
  (separate) process via classic Win32 `SetParent`/`SetWindowLong` calls;
  that dialog is rarely used on modern Windows, and getting it to reliably
  render wasn't worth the complexity or the risk of an orphaned process
  that fails to embed and has no way to notice its host is gone.
- Ball size/position math is in physical pixels; per-monitor DPI awareness,
  declared in `app.manifest`, keeps `Screen.AllScreens` reporting real,
  unscaled monitor geometry even with mixed-DPI monitors. This has to be
  the manifest specifically, not the `ApplicationHighDpiMode` MSBuild
  property - that property only takes effect through the SDK-generated
  `ApplicationConfiguration.Initialize()` call, which this project doesn't
  use (`Program.cs` has custom `/s /c /p` argument handling instead of the
  generated entry point).
- Each `MonitorWindow` fully clears and redraws its own client area every
  frame rather than invalidating a small dirty rect. Each window only
  covers one monitor's worth of pixels, so this is cheap, and it avoids a
  class of partial-repaint clipping bugs.
