# AmongStat Worker

Receives the AmongStat statistics of BetterOtherRoles hosts and stores them in a private GitHub repository.

At the end of each game, when the host turned on the lobby option **Send Stats Online** (Lobby group of the BetterOtherRoles settings, off by default, seen by every player), the mod sends the AmongStat file of the lobby. The Worker adds the games it does not have yet to the same file in the repository, in a folder per month (`2026-11/AmongStat_lobby_Name_2026-11-27.json`). It never changes nor removes a game already there.

The GitHub token stays in the Worker: the mod only has a host code, which can only add games.

## Setup (Cloudflare dashboard)

1. Create a private repository for the statistics, with a first commit (a README) on `main`.
2. Create a fine-grained GitHub token: only that repository, permission **Contents: Read and write**.
3. In Cloudflare, create a Worker and paste `worker.js` into it.
4. Create a KV namespace (for example `amongstat-hosts`) and bind it to the Worker as `HOSTS`.
5. In the Worker settings, add:
   - the secret `GITHUB_TOKEN`: the token;
   - the variable `GITHUB_REPO`: `owner/repository`;
   - the variable `GITHUB_BRANCH` (optional): `main` by default.
6. Deploy.

## Host codes

Each entry of the KV namespace is a code: a long random key, and a name as value (shown in the commit messages).

- **Code of the mod**: shared by every host, built into the DLL from the `.env` file next to the solution (never committed):

  ```ini
  AMONGSTAT_SERVER=https://<worker>.workers.dev
  AMONGSTAT_CODE=<code>
  ```

  Without `.env`, the mod builds and works, but sends nothing. To change this code: add a new entry, build a new version of the mod with it, then delete the old entry (the old versions stop sending).
- **Own code of a host** (optional): it keeps working when the code of the mod changes. The host puts it in `BepInEx/config/thilladon.bor.mira.cfg`:

  ```ini
  [AmongStat]
  HostCode = <code>
  ```

The statistics also stay in `AmongStat/` in the game folder of the host.
