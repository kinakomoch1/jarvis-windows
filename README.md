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


## iPhoneからの遠隔操作（Ver.4.1 試験実装）

JARVISを起動すると、PCの **127.0.0.1:18741** でローカル専用Web画面が起動します。
JARVISの「iPhone連携」ボタンからアクセスキーを確認してください。キーは起動ごとに変わり、PC上でのみ表示されます。

1. Windows PCとiPhoneに公式Tailscaleアプリを導入し、同じTailnetにサインインします。
2. Windows PCのPowerShellで `tailscale serve --bg 18741` を実行します。Tailscaleが案内するHTTPS URLを確認します。
3. iPhoneでTailscale接続を有効にして、そのHTTPS URLをSafariで開きます。
4. JARVISのPC画面で表示されたアクセスキーを入力し、PC状態表示・Fortnite/Spotify/Discord起動を試します。

**安全上の注意:** ルーターのポート開放やTailscale Funnelは使用しないでください。ServeはTailnet内だけに公開する用途です。PCは電源オンでJARVISが起動中である必要があります。アクセスキーは共有しないでください。画面を閉じるとキー入力は保持されません。リモート操作は許可された3アプリの起動と状態表示だけに限定しています。起動ボタンの応答は起動指示の受付を意味し、ゲームの起動完了を保証しません。

停止するにはWindows PCで `tailscale serve reset` を実行してください（他のServe設定も削除されるため注意）。JARVISを終了するとローカルサーバーも停止します。

**開発状況:** Windows実機でのビルド・接続・操作テストは未完了です。GitHubの安定版mainにはまだ反映していません。


## Tailscale不要のiPhone遠隔操作（Cloudflare Workers、試験実装）

iPhone Safari → Cloudflare Worker HTTPS → Durable Objectキュー ← Windows JARVISのHTTPSポーリング。ルーターのポート開放は不要です。

### Cloudflareへのデプロイ
1. CloudflareアカウントとNode.jsを用意し、リポジトリの `remote-cloud` フォルダーを開きます。
2. `npx wrangler login` でログインします。
3. 32文字以上のランダムなアクセスキーを生成し、`npx wrangler secret put JARVIS_REMOTE_TOKEN` で登録します。キーをGitHubに書き込まないでください。
4. `npx wrangler deploy` を実行し、表示されたHTTPS URLを控えます。Durable Objectsの利用条件・料金はCloudflareで確認してください。

### PCとiPhoneの接続
1. 開発版JARVISの「クラウド連携」を開き、WorkerのHTTPS URLとアクセスキーを入力して接続します。
2. iPhoneのSafariで同じURLを開き、アクセスキーを入力します。
3. PC状態の確認、Fortnite・Spotify・Discordの起動ボタンを試します。
4. Safariの「共有」→「ホーム画面に追加」でホーム画面に配置できます。

### 制限とセキュリティ
- HTTPSとアクセスキーが必須。PC側の受信ポートは不要です。
- 実行できる操作は許可した3アプリの起動のみ。任意コマンドやファイル操作は受け付けません。
- キーはPCに保存されず、JARVIS再起動後は再入力が必要です。
- キーを知る人は遠隔操作できます。共有せず、漏れたらCloudflareのシークレットを更新してください。
- 個人利用向けの試験実装で、アカウント別認証・端末別失効・ログイン試行回数制限は未実装です。
- PCの電源とJARVISがオンである必要があります。Windows実機ビルド、Cloudflareデプロイ、iPhone接続は未検証です。mainには未反映です。
