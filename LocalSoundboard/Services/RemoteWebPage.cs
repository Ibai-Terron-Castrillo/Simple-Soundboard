namespace LocalSoundboard.Services;

public static class RemoteWebPage
{
    public const string Html = """
<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>LocalSoundboard Remote</title>
  <style>
    :root {
      color-scheme: light;
      --bg: #f6f7f9;
      --panel: #ffffff;
      --text: #20242a;
      --muted: #65707d;
      --line: #d8dee6;
      --accent: #108a8a;
      --danger: #b8323b;
      --favorite: #d97706;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
      background: var(--bg);
      color: var(--text);
    }
    header {
      position: sticky;
      top: 0;
      z-index: 5;
      background: rgba(255,255,255,.96);
      border-bottom: 1px solid var(--line);
      padding: 14px;
    }
    h1 {
      font-size: clamp(24px, 7vw, 38px);
      margin: 0 0 10px;
      letter-spacing: 0;
    }
    .toolbar {
      display: grid;
      gap: 10px;
      grid-template-columns: 1fr;
    }
    input, button {
      min-height: 48px;
      border: 1px solid var(--line);
      border-radius: 8px;
      font: inherit;
      font-size: 18px;
    }
    input {
      width: 100%;
      padding: 10px 12px;
      background: #fff;
    }
    button {
      padding: 10px 14px;
      background: #e8ecef;
      color: var(--text);
      font-weight: 700;
    }
    button.primary { background: var(--accent); color: #fff; border-color: var(--accent); }
    button.danger { background: var(--danger); color: #fff; border-color: var(--danger); }
    button.favorite { color: var(--favorite); }
    .actions {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 10px;
    }
    .pin {
      display: none;
      grid-template-columns: 1fr auto;
      gap: 10px;
    }
    .pin.visible { display: grid; }
    main {
      padding: 14px;
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(210px, 1fr));
      gap: 12px;
    }
    .sound {
      display: grid;
      gap: 10px;
      align-content: space-between;
      min-height: 150px;
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: 8px;
      padding: 12px;
    }
    .sound.unavailable {
      opacity: .55;
    }
    .name {
      font-size: 20px;
      font-weight: 800;
      overflow-wrap: anywhere;
    }
    .meta {
      color: var(--muted);
      font-size: 14px;
      overflow-wrap: anywhere;
    }
    .row {
      display: grid;
      grid-template-columns: 1fr 56px;
      gap: 8px;
    }
    .status {
      margin-top: 10px;
      color: var(--muted);
      font-size: 14px;
    }
    @media (min-width: 720px) {
      .toolbar {
        grid-template-columns: 1fr auto;
        align-items: end;
      }
      .actions { min-width: 360px; }
    }
  </style>
</head>
<body>
  <header>
    <h1>LocalSoundboard</h1>
    <div id="pinBox" class="pin">
      <input id="pin" placeholder="Remote PIN" autocomplete="one-time-code">
      <button id="savePin" class="primary">OK</button>
    </div>
    <div class="toolbar">
      <input id="search" placeholder="Search sounds">
      <div class="actions">
        <button id="favorites">Favorites</button>
        <button id="rescan">Rescan</button>
        <button id="stop" class="danger">Stop</button>
      </div>
    </div>
    <div id="status" class="status">Connecting...</div>
  </header>
  <main id="sounds"></main>
  <script>
    const state = {
      sounds: [],
      favoritesOnly: false,
      pin: localStorage.getItem('localsoundboard-pin') || ''
    };
    const $ = (id) => document.getElementById(id);
    const headers = () => state.pin ? { 'X-Soundboard-Pin': state.pin } : {};
    const setStatus = (text) => $('status').textContent = text;

    async function api(path, options = {}) {
      const response = await fetch(path, {
        ...options,
        headers: { ...(options.headers || {}), ...headers() }
      });
      if (response.status === 401) {
        $('pinBox').classList.add('visible');
        throw new Error('PIN required or incorrect');
      }
      if (!response.ok) throw new Error(await response.text());
      return response.headers.get('content-type')?.includes('application/json')
        ? response.json()
        : response.text();
    }

    async function loadStatus() {
      const status = await api('/api/status');
      $('pinBox').classList.toggle('visible', status.pinRequired);
      setStatus(`${status.availableSounds}/${status.totalSounds} sounds · ${status.library}`);
    }

    async function loadSounds() {
      const query = new URLSearchParams();
      const search = $('search').value.trim();
      if (search) query.set('search', search);
      if (state.favoritesOnly) query.set('favorites', 'true');
      state.sounds = await api(`/api/sounds?${query}`);
      renderSounds();
      await loadStatus();
    }

    function renderSounds() {
      const root = $('sounds');
      root.replaceChildren();
      for (const sound of state.sounds) {
        const card = document.createElement('article');
        card.className = `sound ${sound.available ? '' : 'unavailable'}`;
        card.innerHTML = `
          <div>
            <div class="name"></div>
            <div class="meta"></div>
          </div>
          <div class="row">
            <button class="primary play">Play</button>
            <button class="favorite fav">${sound.favorite ? 'Saved' : 'Fav'}</button>
          </div>`;
        card.querySelector('.name').textContent = sound.name;
        card.querySelector('.meta').textContent = `${sound.category} · ${sound.extension}`;
        card.querySelector('.play').disabled = !sound.available;
        card.querySelector('.play').addEventListener('click', () => play(sound.id));
        card.querySelector('.fav').addEventListener('click', () => favorite(sound.id));
        root.appendChild(card);
      }
    }

    async function play(id) {
      await api(`/api/play/${id}`, { method: 'POST' });
      setStatus('Playing');
    }
    async function favorite(id) {
      await api(`/api/favorite/${id}`, { method: 'POST' });
      await loadSounds();
    }
    async function stopAll() {
      await api('/api/stop', { method: 'POST' });
      setStatus('Playback stopped');
    }
    async function rescan() {
      setStatus('Scanning...');
      await api('/api/rescan', { method: 'POST' });
      await loadSounds();
    }

    $('savePin').addEventListener('click', () => {
      state.pin = $('pin').value.trim();
      localStorage.setItem('localsoundboard-pin', state.pin);
      loadSounds().catch(err => setStatus(err.message));
    });
    $('search').addEventListener('input', () => loadSounds().catch(err => setStatus(err.message)));
    $('favorites').addEventListener('click', () => {
      state.favoritesOnly = !state.favoritesOnly;
      $('favorites').classList.toggle('primary', state.favoritesOnly);
      loadSounds().catch(err => setStatus(err.message));
    });
    $('stop').addEventListener('click', () => stopAll().catch(err => setStatus(err.message)));
    $('rescan').addEventListener('click', () => rescan().catch(err => setStatus(err.message)));

    loadStatus()
      .then(loadSounds)
      .catch(err => setStatus(err.message));
  </script>
</body>
</html>
""";
}
