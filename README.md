# OpenSteps

OpenSteps is a modern open-source Steps Recorder for Windows. It records desktop workflows and exports clean Markdown guides with screenshots.

![OpenSteps screenshot placeholder](docs/screenshot-placeholder.svg)

## Features

- No cloud, no account, no telemetry.
- Local-first recording with screenshots and click metadata.
- Global left-click capture across desktop apps.
- UI Automation metadata for useful editable step titles.
- Screenshot click highlights.
- Editable step list with delete and move controls.
- Markdown export with local `images/step-001.png` assets.

## Why It Exists

Windows Steps Recorder is useful but dated. OpenSteps is built for IT documentation, tutorials, onboarding, and support guides where teams need simple local workflow capture without a SaaS account or browser extension.

## Build And Run

Prerequisites:

- Windows 10 or later.
- .NET 8 SDK with Windows Desktop runtime.

Commands:

```powershell
dotnet build OpenSteps.sln
dotnet test OpenSteps.sln
dotnet run --project src/OpenSteps.App
```

## Privacy

OpenSteps records screenshots and click metadata locally. It does not upload anything. The MVP does not record typed text, use AI features, or require internet access.

## Roadmap

- Save and reopen recording sessions.
- Better toolbar placement and multi-monitor polish.
- Richer UI Automation captions.
- Optional redaction tools before export.
- Additional export formats after the Markdown workflow is solid.

## Contributing

See `AGENTS.md` for repository guidelines. Keep changes focused, add tests for Core behavior, and include manual verification notes for capture or WPF UI changes.
