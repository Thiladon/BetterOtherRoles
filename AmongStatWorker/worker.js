// AmongStat upload for BetterOtherRoles (Cloudflare Worker).
//
// A host sends the AmongStat file of its lobby at the end of each game; the Worker adds the games it does not have yet
// to the same file in a private GitHub repository, in a folder per month: <yyyy-MM>/<file name>. It never changes nor
// removes a game already there, so a host code can only add games.
//
// Settings of the Worker (Cloudflare dashboard, Settings > Variables and Secrets / Bindings):
// - HOSTS: KV namespace, one entry per code: key = host code, value = its name (shown in the commit messages): the
//   code built into the mod, shared by every host, and the own codes of some hosts;
// - GITHUB_TOKEN: secret, fine-grained token with "Contents: Read and write" on the stats repository only;
// - GITHUB_REPO: variable, "owner/repository";
// - GITHUB_BRANCH: variable, optional, "main" by default.
//
// Request: POST, header "Authorization: Bearer <host code>", body {"FileName": "AmongStat_....json", "File": {...}}.
// Answer: 200 {"added": n, "games": total}; 400 bad request, 401 unknown host code, 413 too large, 502/503 GitHub.

const MaxBody = 512 * 1024;
const MaxGames = 200;
const FileNamePattern = /^AmongStat_[A-Za-z0-9._-]{1,150}\.json$/;
const DatePattern = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$/;

export default {
  async fetch(request, env) {
    if (request.method !== "POST") return answer(405, { error: "POST only" });

    const auth = request.headers.get("Authorization") ?? "";
    const code = auth.startsWith("Bearer ") ? auth.slice(7).trim() : "";
    const host = code ? await env.HOSTS.get(code) : null;
    if (!host) return answer(401, { error: "unknown host code" });

    const raw = await request.text();
    if (raw.length > MaxBody) return answer(413, { error: "too large" });

    let body;
    try {
      body = JSON.parse(raw);
    } catch {
      return answer(400, { error: "not JSON" });
    }

    const fileName = body?.FileName;
    const file = body?.File;
    if (typeof fileName !== "string" || !FileNamePattern.test(fileName)) return answer(400, { error: "bad file name" });
    if (!isAmongStatFile(file)) return answer(400, { error: "bad AmongStat file" });

    const path = `${file.Date.slice(0, 7)}/${fileName}`;

    // Someone else may write the same file at the same time: read it again and retry.
    for (let attempt = 0; attempt < 3; attempt++) {
      const current = await getFile(env, path);
      if (current.error) return answer(502, { error: current.error });

      const merged = merge(current.file, file);
      if (merged.added === 0) return answer(200, { added: 0, games: merged.file.Games.length });

      // The name of the lobby tells who sent it (the code of the mod is shared by every host).
      const message = `${merged.file.LobbyName}: game ${merged.file.Games.length - merged.added + 1}` +
        (merged.added > 1 ? ` to ${merged.file.Games.length}` : "") + ` (${fileName}, ${host})`;
      const status = await putFile(env, path, merged.file, current.sha, message);
      if (status === 200 || status === 201) return answer(200, { added: merged.added, games: merged.file.Games.length });
      if (status !== 409 && status !== 422) return answer(502, { error: `GitHub ${status}` });
    }

    return answer(503, { error: "busy, try again" });
  },
};

// The games of the host that come after the last game of the file, in order; the name and date of the file stay, the
// list of the players is the host's (made again with each game).
function merge(current, sent) {
  if (!current) return { file: sent, added: sent.Games.length };

  const games = [...current.Games];
  const next = [...sent.Games].sort((a, b) => a.GameNumber - b.GameNumber);
  let added = 0;
  for (const game of next) {
    if (game.GameNumber !== games.length + 1) continue;
    games.push(game);
    added++;
  }

  const players = new Set([...(current.LobbyPlayerNames ?? []), ...sent.LobbyPlayerNames]);
  const file = {
    FormatVersion: current.FormatVersion,
    LobbyName: current.LobbyName,
    LobbyPlayerNames: [...players].sort((a, b) => a.localeCompare(b, "en", { sensitivity: "base" })),
    Date: current.Date,
    Games: games,
  };
  return { file, added };
}

function isAmongStatFile(file) {
  if (!file || typeof file !== "object") return false;
  if (file.FormatVersion !== "2" || typeof file.LobbyName !== "string" || typeof file.Date !== "string") return false;
  if (!DatePattern.test(file.Date) || !isStringArray(file.LobbyPlayerNames)) return false;
  if (!Array.isArray(file.Games) || file.Games.length === 0 || file.Games.length > MaxGames) return false;

  return file.Games.every(
    (game) =>
      game &&
      Number.isInteger(game.GameNumber) &&
      game.GameNumber > 0 &&
      typeof game.ModName === "string" &&
      Array.isArray(game.Entries) &&
      Array.isArray(game.Meetings),
  );
}

function isStringArray(value) {
  return Array.isArray(value) && value.every((x) => typeof x === "string");
}

function contentsUrl(env, path) {
  const encoded = path.split("/").map(encodeURIComponent).join("/");
  return `https://api.github.com/repos/${env.GITHUB_REPO}/contents/${encoded}`;
}

function gitHubHeaders(env) {
  return {
    Authorization: `Bearer ${env.GITHUB_TOKEN}`,
    Accept: "application/vnd.github+json",
    "X-GitHub-Api-Version": "2022-11-28",
    "User-Agent": "bor-amongstat-worker",
  };
}

async function getFile(env, path) {
  const branch = env.GITHUB_BRANCH || "main";
  const response = await fetch(`${contentsUrl(env, path)}?ref=${encodeURIComponent(branch)}`, { headers: gitHubHeaders(env) });
  if (response.status === 404) return { file: null, sha: undefined };
  if (!response.ok) return { error: `GitHub ${response.status}` };

  const data = await response.json();
  try {
    return { file: JSON.parse(fromBase64(data.content)), sha: data.sha };
  } catch {
    return { error: "the file of the repository is not JSON" };
  }
}

async function putFile(env, path, file, sha, message) {
  const body = {
    message,
    content: toBase64(JSON.stringify(file, null, 2)),
    branch: env.GITHUB_BRANCH || "main",
  };
  if (sha) body.sha = sha;

  const response = await fetch(contentsUrl(env, path), {
    method: "PUT",
    headers: { ...gitHubHeaders(env), "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  return response.status;
}

// Base64 of UTF-8 text (player names may have accents).
function toBase64(text) {
  const bytes = new TextEncoder().encode(text);
  let binary = "";
  for (let i = 0; i < bytes.length; i += 0x8000) binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  return btoa(binary);
}

function fromBase64(base64) {
  const binary = atob(base64.replace(/\s/g, ""));
  const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

function answer(status, data) {
  return new Response(JSON.stringify(data), { status, headers: { "Content-Type": "application/json" } });
}
