using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Jarvis;

// Local-only server. Remote access must be provided by an authenticated Tailscale Serve tunnel.
internal sealed class RemoteControlServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly Func<string, Task<bool>> launch;
    private readonly Func<string> status;
    private readonly CancellationTokenSource stop = new();
    private readonly string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private Task? loop;
    public string AccessToken => token;
    public const string LocalUrl = "http://127.0.0.1:18741/";

    public RemoteControlServer(Func<string, Task<bool>> launch, Func<string> status)
    {
        this.launch = launch;
        this.status = status;
        listener.Prefixes.Add(LocalUrl);
    }
    public void Start()
    {
        listener.Start();
        loop = Task.Run(ListenAsync);
    }
    private async Task ListenAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await listener.GetContextAsync(); }
            catch (HttpListenerException) when (stop.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) { break; }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }
    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            ctx.Response.Headers["Cache-Control"] = "no-store";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["X-Frame-Options"] = "DENY";
            ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
            // Host and Origin checks prevent other websites from silently using localhost.
            var host = req.Url?.Host ?? "";
            if (host != "127.0.0.1" && !host.EndsWith(".ts.net", StringComparison.OrdinalIgnoreCase))
            { ctx.Response.StatusCode = 403; return; }
            var origin = req.Headers["Origin"];
            Uri? originUri = null;
            if (origin != null && !Uri.TryCreate(origin, UriKind.Absolute, out originUri))
            { ctx.Response.StatusCode = 403; return; }
            if (originUri != null && (originUri.Host != host || originUri.Scheme != (host == "127.0.0.1" ? "http" : "https")))
            { ctx.Response.StatusCode = 403; return; }

            if (req.HttpMethod == "GET" && req.Url?.AbsolutePath == "/")
            {
                const string html = """
                <!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
                <title>JARVIS Remote</title><style>body{font-family:system-ui;background:#0b1425;color:#eaf6ff;max-width:520px;margin:30px auto;padding:20px}button,input{padding:14px;margin:6px;border-radius:8px;font-size:16px}button{background:#147caa;color:white;border:0}input{width:90%;background:#1c2b41;color:white;border:1px solid #58718c}#result{white-space:pre-wrap}</style>
                <h1>J.A.R.V.I.S. Remote</h1><p>iPhoneから自宅PCを操作</p>
                <label>アクセスキー（JARVISのPC画面で確認）<input type="password" id="token" autocomplete="off"></label>
                <p><button onclick="send('status')">PC状態</button></p>
                <p><button onclick="send('fortnite')">Fortnite 起動</button><button onclick="send('spotify')">Spotify 起動</button><button onclick="send('discord')">Discord 起動</button></p>
                <p id="result" role="status"></p>
                <script>
                async function send(app){
                  const result=document.getElementById('result');
                  try{
                    const path=app==='status'?'/api/status':'/api/launch';
                    const response=await fetch(path,{method:app==='status'?'GET':'POST',headers:{'Authorization':'Bearer '+document.getElementById('token').value,'Content-Type':'application/json'},body:app==='status'?undefined:JSON.stringify({app})});
                    result.textContent=response.ok?await response.text():'エラー '+response.status+': '+await response.text();
                  }catch(e){result.textContent='通信エラー: '+e.message}
                }
                </script></html>
                """;
                await WriteAsync(ctx.Response, 200, html, "text/html; charset=utf-8");
                return;
            }
            var authorization = req.Headers["Authorization"] ?? "";
            var expected = "Bearer " + token;
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(authorization.PadRight(expected.Length)), Encoding.UTF8.GetBytes(expected)) || authorization.Length != expected.Length)
            { await WriteAsync(ctx.Response, 401, "アクセスキーが違います。"); return; }

            if (req.HttpMethod == "GET" && req.Url?.AbsolutePath == "/api/status")
            { await WriteAsync(ctx.Response, 200, status()); return; }
            if (req.HttpMethod == "POST" && req.Url?.AbsolutePath == "/api/launch")
            {
                if (req.ContentLength64 < 0 || req.ContentLength64 > 1024)
                { await WriteAsync(ctx.Response, 413, "リクエストが大きすぎます。"); return; }
                using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(body);
                var app = doc.RootElement.GetProperty("app").GetString() ?? "";
                if (app is not ("fortnite" or "spotify" or "discord"))
                { await WriteAsync(ctx.Response, 400, "未対応のアプリです。"); return; }
                var accepted = await launch(app);
                await WriteAsync(ctx.Response, accepted ? 200 : 503, accepted ? "PCに起動指示を送りました。" : "起動指示を送れませんでした。");
                return;
            }
            await WriteAsync(ctx.Response, 404, "Not found");
        }
        catch (Exception)
        {
            try { await WriteAsync(ctx.Response, 500, "処理できませんでした。"); } catch { }
        }
        finally { try { ctx.Response.Close(); } catch { } }
    }
    private static async Task WriteAsync(HttpListenerResponse response, int code, string body, string contentType = "text/plain; charset=utf-8")
    {
        response.StatusCode = code;
        response.ContentType = contentType;
        var bytes = Encoding.UTF8.GetBytes(body);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }
    public void Dispose()
    {
        stop.Cancel();
        listener.Close();
        stop.Dispose();
    }
}
