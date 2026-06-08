You are continuing the OpenSteps project.

Current context:
OpenSteps is a Windows-first, local-only, open-source desktop app built with C#/.NET 8 WPF. It records desktop workflows, captures screenshots with click highlights, lets users edit steps/screenshots, saves sessions locally, and exports Markdown/HTML guides.

User feedback to address:

1. Floating capture window only shows `ScreenshotModeOp` instead of a readable screenshot mode.
2. Generated descriptive titles should differentiate click, right-click, and double-click.
3. Editing an automatically generated descriptive title appears to auto-save and lose focus after about 1 second of typing.
4. Generated titles should use single quotes around named buttons instead of double quotes.
   Example: `Click the 'Save' button` instead of `Click the "Save" button`.
5. Screenshot editor should support drawing rectangles, arrows, semi-transparent highlight areas, and sequential numbers/letters.
6. User should be able to import or paste an alternative screenshot in the editor when active-window capture gets the wrong image.
7. Export options screen is cut off. It shows Markdown but cuts off around “renders in most docs too,” and the HTML option may be hidden.

Only implement these fixes/features. Do not add AI, OCR, cloud sync, accounts, video recording, PDF export, browser extension support, or unrelated features.

Goal:
Make OpenSteps feel more polished and usable based on real beta feedback.

---

## PART 1: Fix floating capture window screenshot mode text

Bug:
The floating capture/recording toolbar currently shows text like:

ScreenshotModeOp

This should display a readable mode label.

Expected labels:

- Full Desktop
- Active Window

or:

- Capturing: Full Desktop
- Capturing: Active Window

Tasks:

1. Inspect the toolbar ViewModel/XAML binding.
2. Find where `ScreenshotModeOp` is coming from.
   It is likely an enum value, binding placeholder, property name, or bad converter.
3. Add a user-facing display property:

public string ScreenshotModeDisplayText =>
CurrentScreenshotMode == ScreenshotMode.ActiveWindow
? "Active Window"
: "Full Desktop";

or:

public string CaptureModeLabel =>
$"Capturing: {ScreenshotModeDisplayText}";

4. Bind the toolbar text to this display property instead of the raw enum/property name.
5. Ensure the toolbar updates if screenshot mode changes before or during a recording session.
6. Add a simple unit test for the display-label mapping if the ViewModel is testable.

Acceptance:

- Toolbar never shows `ScreenshotModeOp`.
- Toolbar clearly shows Full Desktop or Active Window.

---

## PART 2: Differentiate click, right-click, and double-click

Current issue:
Generated titles mostly say “Click...” and do not distinguish normal click, right-click, and double-click.

Expected examples:

- Left click on Save button:
  Click the 'Save' button
- Right click in File Explorer:
  Right-click in File Explorer
- Right click named item:
  Right-click 'Documents'
- Double-click file:
  Double-click 'Report.docx'
- Double-click generic window:
  Double-click in File Explorer

Tasks:

1. Extend action type / mouse action model.

If there is already StepActionType, update it to support mouse click variants:

public enum StepActionType
{
Click,
RightClick,
DoubleClick,
TextEntry,
Shortcut,
SpecialKey,
Manual
}

If StepActionType is not the best place, add a separate field:

public enum MouseActionType
{
LeftClick,
RightClick,
DoubleClick
}

But keep title generation simple.

2. Update mouse hook handling.

Detect:

- left click
- right click
- double click

For right-click:

- handle WM_RBUTTONDOWN or equivalent existing event path.

For double-click:
Low-level hooks may not reliably provide a dedicated double-click message in all cases, so implement detection by grouping two left-clicks that occur close together:

- same or nearby coordinates
- within system double-click time
- use SystemParameters.DoubleClickTime / DoubleClickWidth / DoubleClickHeight if accessible, or a sensible fallback like 500ms and small pixel threshold.
- If second click qualifies as double-click, mark the resulting action as DoubleClick.
- Avoid creating two separate single-click steps for one double-click if possible.
- Simple MVP acceptable approach:
  - delay finalizing a left-click step briefly to see whether another left click follows
  - if a second click follows within threshold, create/update one DoubleClick step
  - if not, create a normal Click step

- If implementing delayed finalization is too invasive, detect double-click as best as possible and document any rough edge.

3. Store the action type on RecordedStep.

RecordedStep should persist:

- StepActionType or MouseActionType
- click button if useful
- click count if useful

Older sessions should still load.

4. Update StepTitleGenerator.

Title generation rules:

- StepActionType.Click:
  `Click the 'Save' button`
  `Click in Notepad`

- StepActionType.RightClick:
  `Right-click the 'Save' button`
  `Right-click in File Explorer`

- StepActionType.DoubleClick:
  `Double-click the 'Report.docx' item`
  `Double-click in File Explorer`

Use the best available UI Automation metadata:

- control type
- element name
- window title
- process name
- fallback

Avoid bad titles:

- Click unknown
- Click Pane
- Click Microsoft.UI.Content.DesktopChildSiteBridge
- Click at screenshot position unless there is truly no window/process context

5. Update debug panel.

Show:

- action type
- mouse button
- click count/double-click detection reason if available

6. Tests:
   Add title-generation tests for:

- left click button
- right-click button
- double-click item
- right-click generic window
- double-click generic window

Acceptance:

- Generated titles clearly distinguish click, right-click, and double-click.
- Normal single clicks are not incorrectly labeled as double-clicks.
- Right-clicks are recorded correctly.

---

## PART 3: Fix title editing losing focus during autosave

Bug:
When editing the generated/user title, if the user pauses for about 1 second, autosave appears to run and the textbox loses focus.

Likely cause:

- autosave reloads/replaces the session object
- step list is rebuilt
- view model collection is replaced
- WPF binding updates cause the TextBox to re-render
- property change notifications are too aggressive
- selected step/card is recreated after save

Goal:
Autosave should not steal focus or interrupt typing.

Tasks:

1. Inspect editor title/description bindings and autosave logic.

Look for:

- rebuilding ObservableCollection after every save
- reloading session from disk after saving
- replacing StepViewModel instances
- resetting DataContext
- calling RefreshSessions/LoadSession from inside save
- UpdateSourceTrigger causing too frequent full updates

2. Fix autosave to save in-place.

Expected behavior:

- User edits title.
- TextBox remains focused.
- Autosave writes current model to disk in background.
- UI is not rebuilt.
- Cursor position is not lost.
- StepViewModel instances remain stable.

3. Add debounced autosave.

Use a debounce around 750-1500ms, but do not reload/rebuild UI after save.

Pseudo-behavior:

- On text change, mark session dirty.
- Restart debounce timer.
- When timer fires, serialize current session to disk.
- Show small “Saved” status if available.
- Do not replace CurrentSession.
- Do not replace Steps collection.
- Do not change focused control.

4. Use explicit save on lost focus if simpler.

Acceptable hybrid:

- while typing: do not save every character
- after debounce: save silently without UI rebuild
- on LostFocus: save immediately
- on window close: flush pending save

5. Guard against concurrent saves.

If multiple saves happen quickly:

- serialize them
- or use a simple SemaphoreSlim
- latest state should win

6. Tests:
   Add tests only for pure autosave/debounce logic if practical.
   Do not unit test WPF focus directly unless existing framework supports it.

Acceptance:

- User can pause while typing a title and focus is not lost.
- Text cursor stays in the title box.
- Edits still save correctly.
- Closing/reopening the session preserves edits.

---

## PART 4: Use single quotes in generated titles

Wish list:
Generated titles should use single quotes around named controls/buttons/items.

Change:
`Click the "Save" button`

To:
`Click the 'Save' button`

Tasks:

1. Update title generation formatting globally.
2. Use single quotes for UI element names:
   - 'Save'
   - 'File'
   - 'Search'
   - 'Documents'

3. If the name itself contains a single quote, either:
   - escape/sanitize it
   - or fall back to double quotes for that rare case
     Choose the simplest safe approach.

4. Update tests to expect single quotes.

Preferred title style:

- Click the 'Save' button
- Right-click the 'Documents' item
- Double-click the 'Report.docx' item
- Type into the 'Search' field
- Select the 'Settings' tab
- Open the 'File' menu

Acceptance:

- New generated titles use single quotes.
- Existing user-edited titles are not forcibly changed.

---

## PART 5: Add screenshot annotation tools

User wants screenshot editing tools:

- draw rectangles
- draw arrows
- highlight areas with semi-transparent rectangles
- add sequential numbers and/or letters
- use one screenshot for several steps

Goal:
Enhance the screenshot editor so users can annotate screenshots after recording.

Important design:
Keep this simple and local. Do not add a heavy image editor dependency unless absolutely necessary.

Preferred implementation:
Use WPF Canvas overlay on top of the screenshot for editing. When the user clicks Save/Apply, render the screenshot plus annotations into a new PNG file in the session images folder and update the step screenshot path.

Do not overwrite the original screenshot. Save an edited copy.

Example:
images/
step-001.png
step-001-annotated-20260608-142022.png

Tools to implement:

A. Rectangle tool

- User selects Rectangle.
- Drag on screenshot to draw outlined rectangle.
- Config:
  - stroke thickness default 3
  - simple color default red or app accent color

- Save annotation into rendered image.

B. Highlight tool

- User selects Highlight.
- Drag on screenshot to draw semi-transparent filled rectangle.
- Default:
  - yellow fill
  - opacity around 30-40%
  - optional border

- Used to draw attention to areas without hiding content.

C. Arrow tool

- User selects Arrow.
- Click-drag from start to end point.
- Draw line with arrowhead.
- Default stroke thickness around 3.
- Needs to render correctly into final PNG.

D. Number / Letter marker tool

- User selects Number Marker or Letter Marker.
- On click, place a circular marker with text inside.
- Number mode:
  - first marker = 1
  - second = 2
  - third = 3

- Letter mode:
  - first marker = A
  - second = B
  - third = C

- Add UI to choose Number or Letter mode.
- Add Reset sequence button if easy.
- Marker should be visible against most backgrounds.
- Use simple circle with text inside.

E. Selection/Undo
Minimum:

- Add Undo last annotation.
- Add Clear annotations.
  Better:
- Select/move/delete individual annotations, but do not overbuild if too costly.

Coordinate conversion:
The screenshot may be displayed scaled. Store annotation coordinates normalized or convert display coordinates to bitmap coordinates.

Preferred:

- Store annotations in image pixel coordinates, not WPF display coordinates.
- On mouse actions, convert from displayed image coordinates to actual bitmap pixel coordinates.

Scale:
scaleX = actualBitmapWidth / displayedImageWidth
scaleY = actualBitmapHeight / displayedImageHeight

When rendering, use actual pixel coordinates.

Data model:
Create annotation models:

public enum ScreenshotAnnotationType
{
Rectangle,
Highlight,
Arrow,
Marker
}

public class ScreenshotAnnotation
{
public Guid Id { get; set; }
public ScreenshotAnnotationType Type { get; set; }
public double X1 { get; set; }
public double Y1 { get; set; }
public double X2 { get; set; }
public double Y2 { get; set; }
public string? Text { get; set; } // for marker labels
public string Color { get; set; }
public double Opacity { get; set; }
public double StrokeThickness { get; set; }
}

Implementation options:
Option 1:

- Keep annotations only inside ScreenshotEditorWindow while editing.
- On Apply, bake them into a new image.
- Do not persist annotation layers separately.

Option 2:

- Persist annotations in session.json and render them at export time.

For this sprint, Option 1 is acceptable and simpler:

- Edit screenshot
- Add annotations
- Apply
- Save annotated image copy
- Step points to annotated image
- Original image remains available on disk but not necessarily tracked as a separate layer.

Add a future TODO for non-destructive annotation layers.

Acceptance:

- User can open screenshot editor for a step.
- User can draw a rectangle.
- User can draw a semi-transparent highlight.
- User can draw an arrow.
- User can place number/letter markers.
- User can undo last annotation.
- User can apply/save annotations.
- Edited screenshot appears in the step card.
- Original screenshot is not overwritten.
- Export uses annotated screenshot.

---

## PART 6: Import or paste alternative screenshot in editor

Problem:
Some apps do not report active window correctly, so OpenSteps can capture the wrong screenshot. User wants to replace the screenshot manually by importing or pasting an alternative screenshot.

Goal:
In the editing screen, each step should allow replacing its screenshot from:

1. File import
2. Clipboard paste

Tasks:

A. Import screenshot from file
Add button on each step card:

- Replace screenshot
  or
- Import screenshot

Use OpenFileDialog.
Allow:

- .png
- .jpg
- .jpeg
- .bmp maybe

Behavior:

- User chooses image file.
- App copies the image into the current session images folder.
- App converts/saves to PNG if easy; otherwise preserve extension if current export supports it.
- Update step.ScreenshotRelativePath.
- Refresh preview.
- Autosave session.
- Do not link directly to the original file path.

B. Paste screenshot from clipboard
Add button:

- Paste screenshot

Also support Ctrl+V when a step is selected/focused if simple.

Behavior:

- If Clipboard.ContainsImage():
  - get image from clipboard
  - save as PNG into session images folder
  - update step.ScreenshotRelativePath
  - refresh preview
  - autosave session

- If clipboard has no image:
  - show friendly message: “Clipboard does not contain an image.”

Potential clipboard APIs:

- WPF Clipboard.GetImage()
- Save BitmapSource using PngBitmapEncoder

C. Create session images folder if missing.

D. File naming:
Use unique names:

- replacement-{stepId}-{timestamp}.png
- pasted-{stepId}-{timestamp}.png

E. Error handling:
Handle:

- invalid/corrupt image file
- file copy failure
- missing session folder
- clipboard access failure
- no image in clipboard

Acceptance:

- User can replace a bad screenshot with an image file.
- User can paste a screenshot from clipboard.
- Replacement image persists after reopening session.
- Export uses replacement image.
- App never links to the external original path.

---

## PART 7: Fix export options screen cut-off

Bug:
The export options screen only visibly shows a ticked option for Markdown files and cuts off near text like “renders in most docs too.”
HTML option may be hidden even though HTML is exported.

Goal:
Make the export options screen layout reliable and fully visible.

Tasks:

1. Inspect export options window/dialog XAML.
2. Fix layout so all options are visible:
   - Markdown
   - HTML
   - any other existing option

3. Use a ScrollViewer if content may exceed window height.
4. Set reasonable MinWidth/MinHeight.
5. Avoid fixed heights that clip content.
6. Make the window resizable if appropriate.
7. Ensure buttons are visible:
   - Export
   - Cancel
   - Browse/select folder if present

8. Ensure option descriptions wrap properly.
9. Make sure HTML option is actually shown and accurately reflects what export does.

Possible UI structure:

- Window with Grid rows:
  - Header: Auto
  - Options ScrollViewer: \*
  - Footer buttons: Auto

- Export options inside StackPanel.
- TextBlocks with TextWrapping="Wrap".
- Checkboxes aligned with descriptions.

Acceptance:

- Export options screen does not cut off text.
- Markdown option is visible.
- HTML option is visible if HTML export exists.
- User can clearly choose export formats.
- Export buttons are visible without resizing.
- Works at common Windows scaling levels.

---

## PART 8: Tests

Add/update tests where practical.

Good tests:

- ScreenshotMode display label maps enum to readable text.
- Title generator uses single quotes.
- Title generator differentiates Click, RightClick, DoubleClick.
- Manual/replacement screenshot paths are relative.
- Markdown/HTML export uses replaced/annotated screenshot path.
- Clipboard/file replacement helper creates unique relative path if testable.
- Export options ViewModel exposes Markdown and HTML options.
- Autosave service does not require reloading/replacing the session object.

Do not unit test:

- live global hooks
- actual clipboard if hard in CI
- WPF drag drawing if not supported
- actual visual focus behavior unless existing UI tests support it

---

## PART 9: README / release notes update

Update README or changelog with:

- clearer capture toolbar mode label
- click/right-click/double-click title improvements
- screenshot annotation tools
- import/paste screenshot replacement
- export options UI fix
- note that screenshot annotations are baked into a new image copy for now

---

## PART 10: Acceptance criteria

After implementation:

- dotnet build OpenSteps.sln passes
- dotnet test OpenSteps.sln passes
- floating capture window shows readable screenshot mode, not `ScreenshotModeOp`
- generated titles distinguish click, right-click, and double-click
- generated titles use single quotes around named controls
- editing a title no longer loses focus after pausing briefly
- screenshot editor supports rectangle annotations
- screenshot editor supports arrow annotations
- screenshot editor supports semi-transparent highlight annotations
- screenshot editor supports sequential number/letter markers
- annotations save into a new image file
- original screenshot is not overwritten
- edited step preview updates after annotation save
- user can import an alternative screenshot from file
- user can paste an alternative screenshot from clipboard
- replacement screenshots persist after session reload
- export uses annotated/replaced screenshots
- export options screen shows Markdown and HTML options without clipping
- no AI/OCR/video/cloud/account/PDF/browser-extension features were added

Implementation order:

1. Inspect current toolbar, title generator, editor autosave, screenshot editor, replacement screenshot support, and export options UI.
2. Fix toolbar screenshot mode display text.
3. Update action type detection/title generation for click/right-click/double-click.
4. Change generated title quote style to single quotes.
5. Fix autosave so title editing does not lose focus.
6. Add import screenshot from file.
7. Add paste screenshot from clipboard.
8. Add screenshot annotation tools:
   - rectangle
   - highlight
   - arrow
   - number/letter markers
   - undo last
   - apply/save as new image

9. Fix export options layout clipping.
10. Add/update tests.
11. Update README/changelog.
12. Run release verification commands.
13. Report:

- commands run
- files changed
- what works
- known rough edges
- what still needs manual testing

Keep the changes targeted and do not rewrite the whole app.
