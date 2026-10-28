# J.A.R.V.I.S. — Windows GitHub Actions build

This is an **early functional build**, not a finished autonomous computer-use assistant. No Python, Node.js or Rust installation is needed on the user's PC.

## Build entirely on GitHub

1. Create a **private** GitHub repository, e.g. `jarvis-windows`.
2. Upload the **contents** of this ZIP to the repository root, preserving `.github/workflows/windows-build.yml` and `src/`.
3. Open **Actions** → **Build JARVIS for Windows** → **Run workflow**.
4. Once successful, open the workflow run and download artifact **JARVIS-Windows-x64**.
5. Extract the artifact and run `JARVIS.exe` on Windows 10/11 x64.
6. Windows SmartScreen may warn about an unsigned app. Only run software you trust. No code signing is included.

## Features

- WinForms dark UI, task-tray presence and opt-in Windows autostart.
- Japanese wake-word via Windows' **installed Japanese System.Speech recognizer**. Not guaranteed on all Windows installations. The recognition grammar is intentionally limited; free-form speech transcription is not yet implemented.
- Japanese speech synthesis using a locally installed Windows voice (male if available, otherwise Japanese fallback).
- Text chat with OpenAI or Anthropic via API keys entered **per session**, not persisted.
- A few explicit local commands: launch Notepad, browser, Discord URL scheme; basic memory estimate.
- No arbitrary shell command execution, file deletion or unconstrained autonomous PC control.

## Security & privacy

- AI requests are sent to the selected provider, subject to their API terms and billing. No API key is required for local commands.
- Do **not** put API keys in GitHub repository files or workflow secrets for this app. Enter them in the app UI when needed.
- Speech recognition listens to the default microphone when enabled; disable it via the checkbox. Local recognizer availability varies.
- Windows autostart uses HKCU Run; toggle off in the UI to remove it.
- Clicking X minimizes to tray; choose **終了** from tray menu to exit.

## Development roadmap

1. Robust local wake-word engine and full Japanese dictation.
2. Real hardware metrics (CPU/GPU/RAM).
3. AI planner with typed, allowlisted tools, explicit approval and audit log.
4. UI Automation accessibility tree, screen understanding and bounded actions.
5. Stop button, timeout, rollback and tests, signed releases.

## Notes

- `System.Speech` NuGet package is restored by GitHub Actions on Windows.
- Requires Windows 10/11 x64. GitHub builds the .NET runtime into the published output.
- API model identifiers and availability can change; update `src/Program.cs` if a provider retires a model.
- The GitHub workflow is provided but has **not** been executed or validated on a Windows runner in this environment.
