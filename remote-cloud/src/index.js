const allowed = new Set(["fortnite", "spotify", "discord"]);
const headers = {
  "Content-Type": "text/plain; charset=utf-8",
  "Cache-Control": "no-store",
  "X-Content-Type-Options": "nosniff",
  "Referrer-Policy": "no-referrer",
  "Content-Security-Policy": "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
};
function reply(body, status = 200, type = "text/plain; charset=utf-8") {
  return new Response(body, { status, headers: { ...headers, "Content-Type": type } });
}
const page = `<!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>JARVIS Remote</title><style>body{font-family:system-ui;background:#091426;color:#e8f6ff;max-width:540px;margin:32px auto;padding:20px}button,input{font:inherit;padding:13px;border-radius:10px;margin:6px}button{background:#176a98;color:white;border:0}input{background:#192b42;color:white;border:1px solid #7c9cb2;width:90%}#output{white-space:pre-wrap}</style>
<h1>J.A.R.V.I.S.</h1><p>iPhone Remote Control</p><label>アクセスキー<input id="token" type="password" autocomplete="off"></label>
<p><button onclick="send('status')">PC状態を確認</button></p>
<p><button onclick="send('fortnite')">Fortnite起動</button><button onclick="send('spotify')">Spotify起動</button><button onclick="send('discord')">Discord起動</button></p>
<p id="output" role="status"></p>
<script>async function send(app){const o=document.getElementById('output');try{const r=await fetch(app==='status'?'/api/status':'/api/command',{method:app==='status'?'GET':'POST',headers:{Authorization:'Bearer '+document.getElementById('token').value,'Content-Type':'application/json'},body:app==='status'?undefined:JSON.stringify({app})});o.textContent=(r.ok?'':'エラー '+r.status+': ')+await r.text()}catch(e){o.textContent='接続できません: '+e.message}}</script></html>`;
export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (request.method === "GET" && url.pathname === "/") return reply(page, 200, "text/html; charset=utf-8");
    if (!env.JARVIS_REMOTE_TOKEN || env.JARVIS_REMOTE_TOKEN.length < 32) return reply("Server not configured", 503);
    const auth = request.headers.get("Authorization") || "";
    const expected = "Bearer " + env.JARVIS_REMOTE_TOKEN;
    // Compare all characters even on mismatch; no secrets in query strings or HTML.
    let mismatch = auth.length ^ expected.length;
    for (let i = 0; i < Math.max(auth.length, expected.length); i++)
      mismatch |= (auth.charCodeAt(i) || 0) ^ (expected.charCodeAt(i) || 0);
    if (mismatch !== 0) return reply("Unauthorized", 401);
    if (request.method === "POST" && request.headers.get("Origin") !== null && request.headers.get("Origin") !== url.origin)
      return reply("Origin forbidden", 403);
    const id = env.RELAY.idFromName("personal-jarvis");
    return env.RELAY.get(id).fetch(request);
  }
};
export class JarvisRelay {
  constructor(state) { this.state = state; }
  async fetch(request) {
    const url = new URL(request.url);
    const path = url.pathname;
    if (request.method === "POST" && path === "/api/command") {
      if (Number(request.headers.get("content-length") || 0) > 1024) return reply("Too large", 413);
      let data;
      try { data = await request.json(); } catch { return reply("Invalid JSON", 400); }
      if (!allowed.has(data?.app)) return reply("Unsupported app", 400);
      const queue = (await this.state.storage.get("queue")) || [];
      if (queue.length >= 20) return reply("Queue full", 429);
      queue.push({ app: data.app, id: crypto.randomUUID(), created: Date.now() });
      await this.state.storage.put("queue", queue);
      return reply("起動指示を受け付けました。PCがオンラインなら数秒以内に実行します。", 202);
    }
    if (request.method === "GET" && path === "/api/poll") {
      const queue = (await this.state.storage.get("queue")) || [];
      const now = Date.now();
      const fresh = queue.filter(c => now - c.created < 60000);
      const next = fresh.shift() || null;
      await this.state.storage.put("queue", fresh);
      return reply(JSON.stringify(next), 200, "application/json; charset=utf-8");
    }
    if (request.method === "POST" && path === "/api/heartbeat") {
      let data;
      try { data = await request.json(); } catch { return reply("Invalid JSON", 400); }
      await this.state.storage.put("heartbeat", { time: Date.now(), status: String(data?.status || "").slice(0, 200) });
      return reply("OK");
    }
    if (request.method === "GET" && path === "/api/status") {
      const h = await this.state.storage.get("heartbeat");
      if (!h || Date.now() - h.time > 30000) return reply("PCオフライン（またはJARVIS未接続）");
      return reply("PCオンライン / " + h.status);
    }
    return reply("Not found", 404);
  }
}
