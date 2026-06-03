# Manual Smoke Test

Use this checklist before demoing OpenSteps.

## Recording Flow

1. Run `dotnet run --project src/OpenSteps.App`.
2. Confirm the home screen shows the local-only privacy note.
3. Click **Start Recording**.
4. Confirm the floating toolbar appears, stays on top, shows elapsed time, and increments step count.
5. Drag the toolbar to another screen area and confirm it remains visible.
6. Open Notepad and click inside the editor area.
7. Open File Explorer and click a folder or navigation item.
8. Open Windows Settings and click a visible setting.
9. Click **Pause** on the toolbar, click in another app, and confirm no new step is added.
10. Click **Resume**, click in another app, and confirm one new step is added.
11. Click the toolbar itself several times and confirm toolbar clicks are excluded.
12. If the main OpenSteps window is visible while recording, click it and confirm those clicks are excluded.
13. Click **Stop**.

## Taskbar And OpenSteps Click Filtering

1. Start OpenSteps recording.
2. Leave OpenSteps visible or foreground.
3. Click a Notepad icon on the Windows taskbar to open or maximize Notepad.
4. Confirm no new recorded step is created for the taskbar click.
5. Confirm no screenshot of OpenSteps is created.
6. Open the editor and confirm **Skipped capture events** shows a skipped taskbar/shell click.
7. Click inside Notepad.
8. Confirm that click records normally.
9. Open Notepad and File Explorer.
10. Start recording and click the File Explorer taskbar icon.
11. Confirm the taskbar click is skipped and the next click inside File Explorer records normally.
12. Put Chrome or Notepad visible behind OpenSteps.
13. Start recording and click directly inside the background app window, not the taskbar.
14. Confirm the step records the clicked app/window, not OpenSteps.
15. With active-window screenshot mode enabled, confirm the screenshot captures the clicked app.
16. Start recording and click Pause, Resume, or Stop on the OpenSteps toolbar.
17. Confirm toolbar clicks are skipped and no steps are created from toolbar clicks.
18. If available, repeat the taskbar switch test from a secondary monitor taskbar.

## Editor And Export

1. Confirm the editor opens with one card per recorded click.
2. Expand **Capture debug/status** for each step.
3. Verify click coordinates, screenshot path, active window title, process name, UI Automation element name/control type, success flags, and any error text.
4. Edit a step title and description.
5. Click **Edit screenshot** on step 1.
6. Drag a rectangle over visible text.
7. Click **Save**.
8. Verify the step preview shows a pixelated redaction and a redacted/edited label.
9. Verify the original screenshot still exists locally beside the redacted image.
10. Delete a step.
11. Move a step up or down.
12. Click **Save Session**.
13. Close and reopen the app, then click **Open Previous Session**.
14. Confirm screenshots, edited text, and redactions still load.
15. Click **Export Markdown** and choose an export folder.
16. Confirm the success message shows the `guide.md` path.
17. Open the exported folder when prompted.
18. Open `guide.md` and verify relative image links under `images/` work.
19. Verify exported redacted steps use the redacted image.

## Multi-Monitor Capture

1. Connect two monitors.
2. Put Notepad on monitor 1.
3. Put File Explorer or Settings on monitor 2.
4. Start recording with screenshot mode set to **MonitorContainingClick**.
5. Click Notepad on monitor 1.
6. Click File Explorer or Settings on monitor 2.
7. Stop recording.
8. Verify step 1 screenshot only shows monitor 1.
9. Verify step 2 screenshot only shows monitor 2.
10. Verify the click highlight appears in the correct place for click steps.
11. Test a secondary monitor placed left of the primary if possible.
12. Test different DPI scaling on the monitors if possible.
13. Expand **Capture debug/status** and verify monitor device, bounds, screenshot size, and local click coordinates match the captured image.

## Known Capture Edge Cases

- Some apps expose little or no UI Automation metadata; screenshots and click coordinates should still be captured.
- Full virtual desktop capture is available as a legacy/debug screenshot mode and can create large images.
- High-DPI scaling can affect app and toolbar positioning; verify on scaled displays.
- Elevated/admin apps may block metadata or hook behavior when OpenSteps is not also elevated.
- Full-screen apps may hide the toolbar or interfere with global hooks.
- Screenshots can include private data. Review steps before sharing or exporting.
