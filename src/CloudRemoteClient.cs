using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Jarvis;

// Polls an HTTPS-only Cloudflare Worker; no inbound PC port is required.
internal sealed class CloudRemoteClient : IDisposable
{
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly CancellationTokenSource cancel = new();
    private readonly Func<string, Task<bool>> launch;
    private readonly Func<string> status;
    private readonly Action<string> log;
    private Task? task;

    public CloudRemoteClient(string url, string secret, Func<string, Task<bool>> launch, Func<string> status, Action<string> log)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("HTTPSのURLを入力してください。");
        if (secret.Length < 32 || secret.Any(char.IsControl))
            throw new ArgumentException("アクセスキーは32文字以上必要です。");
        client.BaseAddress = new Uri(uri.GetLeftPart(UriPartial.Authority) + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        this.launch = launch;
        this.status = status;
        this.log = log;
    }
    public void Start() => task = Task.Run(RunAsync);
    private async Task RunAsync()
    {
        while (!cancel.IsCancellationRequested)
        {
            try
            {
                using (var heartbeat = await client.PostAsJsonAsync("api/heartbeat", new { status = status() }, cancel.Token))
                    heartbeat.EnsureSuccessStatusCode();
                using var response = await client.GetAsync("api/poll", cancel.Token);
                response.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancel.Token));
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("app", out var app))
                {
                    var name = app.GetString() ?? "";
                    if (name is "fortnite" or "spotify" or "discord")
                    {
                        var ok = await launch(name);
                        log(ok ? "iPhoneからの起動指示: " + name : "リモート起動に失敗: " + name);
                    }
                }
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { break; }
            catch (Exception ex) { log("クラウド連携エラー: " + ex.Message); }
            try { await Task.Delay(TimeSpan.FromSeconds(3), cancel.Token); }
            catch (OperationCanceledException) { break; }
        }
    }
    public void Dispose()
    {
        cancel.Cancel();
        client.Dispose();
        // Don't dispose the cancellation source while a request is still unwinding.
        _ = task?.ContinueWith(_ => cancel.Dispose());
        if (task == null) cancel.Dispose();
    }
}
