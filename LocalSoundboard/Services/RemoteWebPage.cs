namespace LocalSoundboard.Services;

public static class RemoteWebPage
{
    public const string Html = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>LocalSoundboard Remote</title>
  <style>
    :root {
      color-scheme: light;
      --bg: #f4f6f8;
      --surface: #ffffff;
      --surface-soft: #eef3f8;
      --surface-strong: #e2e9f2;
      --text: #131920;
      --muted: #596675;
      --line: #d4dce7;
      --accent: #4f98eb;
      --accent-hover: #397ed2;
      --accent-text: #ffffff;
      --danger: #ef5e67;
      --danger-hover: #d94a54;
      --favorite: #d7861f;
      --shadow: 0 18px 42px rgba(26, 39, 57, .10);
      --focus: 0 0 0 3px rgba(79, 152, 235, .28);
    }

    html[data-theme="dark"] {
      color-scheme: dark;
      --bg: #0f1722;
      --surface: #162333;
      --surface-soft: #203247;
      --surface-strong: #2a3d54;
      --text: #f7fbff;
      --muted: #c7d7e9;
      --line: #314258;
      --accent: #5ba4f4;
      --accent-hover: #74b4ff;
      --accent-text: #06121f;
      --danger: #ff6873;
      --danger-hover: #ff7e88;
      --favorite: #f2b15b;
      --shadow: 0 20px 44px rgba(0, 0, 0, .28);
      --focus: 0 0 0 3px rgba(91, 164, 244, .34);
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      min-height: 100vh;
      font-family: "Segoe UI", system-ui, -apple-system, BlinkMacSystemFont, sans-serif;
      background: var(--bg);
      color: var(--text);
      letter-spacing: 0;
    }

    button,
    input {
      font: inherit;
      letter-spacing: 0;
    }

    button {
      border: 1px solid var(--line);
      border-radius: 8px;
      min-height: 48px;
      padding: 0 16px;
      background: var(--surface-soft);
      color: var(--text);
      font-weight: 700;
      cursor: pointer;
      transition: background .16s ease, border-color .16s ease, transform .16s ease;
      touch-action: manipulation;
    }

    button:hover { background: var(--surface-strong); }
    button:active { transform: translateY(1px); }
    button:focus-visible,
    input:focus-visible {
      outline: none;
      box-shadow: var(--focus);
      border-color: var(--accent);
    }
    button:disabled {
      cursor: not-allowed;
      opacity: .52;
      transform: none;
    }

    .primary {
      background: var(--accent);
      border-color: var(--accent);
      color: var(--accent-text);
    }
    .primary:hover { background: var(--accent-hover); }

    .danger {
      background: var(--danger);
      border-color: var(--danger);
      color: #ffffff;
    }
    .danger:hover { background: var(--danger-hover); }

    .app-shell {
      min-height: 100vh;
      display: grid;
      grid-template-rows: auto 1fr;
    }

    .topbar {
      position: sticky;
      top: 0;
      z-index: 5;
      background: color-mix(in srgb, var(--surface) 94%, transparent);
      border-bottom: 1px solid var(--line);
      box-shadow: var(--shadow);
      backdrop-filter: blur(18px);
    }

    .topbar-inner {
      width: min(1180px, 100%);
      margin: 0 auto;
      padding: 18px clamp(14px, 3vw, 28px);
      display: grid;
      gap: 14px;
    }

    .title-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 14px;
    }

    .brand {
      min-width: 0;
    }

    h1 {
      margin: 0;
      font-size: clamp(26px, 5vw, 40px);
      line-height: 1.05;
      color: var(--text);
    }

    .subtitle {
      margin: 6px 0 0;
      color: var(--muted);
      font-size: 15px;
      overflow-wrap: anywhere;
    }

    .theme-toggle {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 10px;
      min-width: 116px;
      white-space: nowrap;
    }

    .theme-toggle svg {
      width: 20px;
      height: 20px;
      flex: 0 0 auto;
      stroke: currentColor;
    }

    .theme-toggle .moon-icon { display: none; }
    html[data-theme="dark"] .theme-toggle .sun-icon { display: none; }
    html[data-theme="dark"] .theme-toggle .moon-icon { display: block; }

    .pin-panel {
      display: none;
      grid-template-columns: 1fr auto;
      gap: 10px;
      padding: 12px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: var(--surface-soft);
    }

    .pin-panel.visible { display: grid; }

    .controls {
      display: grid;
      gap: 10px;
    }

    .search-box {
      position: relative;
    }

    .search-icon {
      position: absolute;
      left: 14px;
      top: 50%;
      width: 20px;
      height: 20px;
      transform: translateY(-50%);
      color: var(--muted);
      pointer-events: none;
    }

    .clear-search {
      position: absolute;
      right: 8px;
      top: 50%;
      display: none;
      align-items: center;
      justify-content: center;
      width: 36px;
      min-width: 36px;
      min-height: 36px;
      padding: 0;
      border-radius: 999px;
      transform: translateY(-50%);
      color: var(--muted);
    }

    .clear-search.visible {
      display: inline-flex;
    }

    .clear-search:hover {
      color: var(--text);
      transform: translateY(-50%);
    }

    .clear-search:active {
      transform: translateY(calc(-50% + 1px));
    }

    .clear-search svg {
      width: 18px;
      height: 18px;
      stroke: currentColor;
      pointer-events: none;
    }

    input {
      width: 100%;
      min-height: 52px;
      padding: 12px 14px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: var(--surface);
      color: var(--text);
      font-size: 17px;
    }

    input::placeholder {
      color: var(--muted);
      opacity: 1;
    }

    .search-input {
      padding-left: 44px;
      padding-right: 52px;
    }

    .actions {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 10px;
    }

    .status-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 10px;
      flex-wrap: wrap;
    }

    .status-pill,
    .counter-pill {
      display: inline-flex;
      align-items: center;
      min-height: 34px;
      border: 1px solid var(--line);
      border-radius: 999px;
      background: var(--surface-soft);
      color: var(--muted);
      padding: 6px 12px;
      font-size: 14px;
      font-weight: 700;
      overflow-wrap: anywhere;
    }

    .counter-pill {
      color: var(--text);
    }

    main {
      width: min(1180px, 100%);
      margin: 0 auto;
      padding: 18px clamp(14px, 3vw, 28px) 28px;
    }

    .sound-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(238px, 1fr));
      gap: 12px;
    }

    .sound {
      display: grid;
      grid-template-rows: 1fr auto;
      gap: 14px;
      min-height: 168px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: var(--surface);
      padding: 14px;
      box-shadow: 0 1px 0 rgba(255, 255, 255, .04) inset;
    }

    .sound.unavailable {
      opacity: .58;
    }

    .sound-header {
      display: grid;
      gap: 8px;
      min-width: 0;
    }

    .sound-name {
      color: var(--text);
      font-size: 19px;
      font-weight: 800;
      line-height: 1.22;
      overflow-wrap: anywhere;
    }

    .sound-meta {
      color: var(--muted);
      font-size: 14px;
      line-height: 1.35;
      overflow-wrap: anywhere;
    }

    .tag-row {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }

    .tag {
      display: inline-flex;
      align-items: center;
      min-height: 26px;
      border-radius: 999px;
      background: var(--surface-soft);
      color: var(--muted);
      padding: 4px 9px;
      font-size: 12px;
      font-weight: 800;
      text-transform: uppercase;
    }

    .sound-actions {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 8px;
    }

    .favorite {
      min-width: 72px;
      color: var(--favorite);
      border-color: color-mix(in srgb, var(--favorite) 45%, var(--line));
    }

    .favorite.active {
      background: color-mix(in srgb, var(--favorite) 18%, var(--surface));
      color: var(--favorite);
    }

    .empty-state {
      display: grid;
      place-items: center;
      min-height: 260px;
      border: 1px dashed var(--line);
      border-radius: 8px;
      background: var(--surface);
      color: var(--muted);
      text-align: center;
      padding: 28px;
      font-size: 17px;
      font-weight: 700;
    }

    @media (min-width: 900px) {
      .controls {
        grid-template-columns: minmax(0, 1fr) auto;
        align-items: center;
      }

      .actions {
        min-width: 498px;
      }
    }

    @media (max-width: 520px) {
      .title-row {
        align-items: stretch;
        flex-direction: column;
      }

      .theme-toggle {
        width: 100%;
      }

      .actions {
        grid-template-columns: 1fr;
      }

      .sound-grid {
        grid-template-columns: 1fr;
      }
    }
  </style>
</head>
<body>
  <div class="app-shell">
    <header class="topbar">
      <div class="topbar-inner">
        <div class="title-row">
          <div class="brand">
            <h1>LocalSoundboard</h1>
            <p id="library" class="subtitle">Remote control for the PC app</p>
          </div>
          <button id="theme" class="theme-toggle" type="button" aria-label="Toggle color theme">
            <svg class="sun-icon" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <circle cx="12" cy="12" r="4"></circle>
              <path d="M12 2v2"></path>
              <path d="M12 20v2"></path>
              <path d="m4.93 4.93 1.41 1.41"></path>
              <path d="m17.66 17.66 1.41 1.41"></path>
              <path d="M2 12h2"></path>
              <path d="M20 12h2"></path>
              <path d="m6.34 17.66-1.41 1.41"></path>
              <path d="m19.07 4.93-1.41 1.41"></path>
            </svg>
            <svg class="moon-icon" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M20.99 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 20.99 12.79z"></path>
            </svg>
            <span id="themeLabel">Light</span>
          </button>
        </div>

        <div id="pinBox" class="pin-panel">
          <input id="pin" placeholder="Remote PIN" autocomplete="one-time-code">
          <button id="savePin" class="primary" type="button">OK</button>
        </div>

        <div class="controls">
          <div class="search-box">
            <svg class="search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <circle cx="11" cy="11" r="8"></circle>
              <path d="m21 21-4.35-4.35"></path>
            </svg>
            <input id="search" class="search-input" placeholder="Search sounds" autocomplete="off" aria-label="Search sounds">
            <button id="clearSearch" class="clear-search" type="button" aria-label="Clear search">
              <svg viewBox="0 0 24 24" fill="none" stroke-width="2.25" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M18 6 6 18"></path>
                <path d="m6 6 12 12"></path>
              </svg>
            </button>
          </div>
          <div class="actions">
            <button id="favorites" type="button">Favorites</button>
            <button id="pause" type="button">Pause</button>
            <button id="rescan" type="button">Rescan</button>
            <button id="stop" class="danger" type="button">Stop All</button>
          </div>
        </div>

        <div class="status-row">
          <div id="status" class="status-pill">Connecting...</div>
          <div id="counts" class="counter-pill">0 sounds</div>
        </div>
      </div>
    </header>

    <main>
      <section id="sounds" class="sound-grid" aria-live="polite"></section>
    </main>
  </div>

  <script>
    const state = {
      sounds: [],
      favoritesOnly: false,
      playbackPaused: false,
      pin: localStorage.getItem('localsoundboard-pin') || '',
      themeOverride: localStorage.getItem('localsoundboard-remote-theme') || ''
    };

    const $ = (id) => document.getElementById(id);
    const headers = () => state.pin ? { 'X-Soundboard-Pin': state.pin } : {};

    function currentTheme() {
      return document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light';
    }

    function applyTheme(theme) {
      document.documentElement.dataset.theme = theme;
      $('themeLabel').textContent = theme === 'dark' ? 'Dark' : 'Light';
      $('theme').setAttribute('aria-pressed', theme === 'dark' ? 'true' : 'false');
    }

    function setStatus(text) {
      $('status').textContent = text;
    }

    function setCounts(available, total) {
      const label = total === 1 ? 'sound' : 'sounds';
      $('counts').textContent = `${available}/${total} ${label}`;
    }

    function setLoading(isLoading) {
      $('rescan').disabled = isLoading;
      $('search').disabled = isLoading;
      $('clearSearch').disabled = isLoading;
    }

    function updateClearSearchButton() {
      $('clearSearch').classList.toggle('visible', $('search').value.trim().length > 0);
    }

    function updatePauseButton() {
      $('pause').textContent = state.playbackPaused ? 'Resume' : 'Pause';
      $('pause').classList.toggle('primary', state.playbackPaused);
    }

    function normalizeTags(tags) {
      if (Array.isArray(tags)) {
        return tags;
      }

      if (typeof tags === 'string') {
        return tags.split(',');
      }

      return [];
    }

    async function api(path, options = {}) {
      const response = await fetch(path, {
        ...options,
        headers: { ...(options.headers || {}), ...headers() }
      });

      if (response.status === 401) {
        $('pinBox').classList.add('visible');
        throw new Error('PIN required or incorrect');
      }

      if (!response.ok) {
        let message = await response.text();
        try {
          const body = JSON.parse(message);
          message = body.error || message;
        } catch { }
        throw new Error(message || 'Request failed');
      }

      return response.headers.get('content-type')?.includes('application/json')
        ? response.json()
        : response.text();
    }

    async function loadStatus() {
      const status = await api('/api/status');
      $('pinBox').classList.toggle('visible', status.pinRequired);
      $('library').textContent = status.library || 'No folder loaded';
      state.playbackPaused = Boolean(status.playbackPaused);
      updatePauseButton();
      setCounts(status.availableSounds, status.totalSounds);

      if (!state.themeOverride) {
        applyTheme(status.isDarkMode ? 'dark' : 'light');
      }

      const warning = status.pinRequired ? 'PIN protected' : 'Private LAN only';
      setStatus(`${warning} - ${status.availableSounds} available`);
    }

    async function loadSounds() {
      const query = new URLSearchParams();
      const search = $('search').value.trim();
      if (search) query.set('search', search);
      if (state.favoritesOnly) query.set('favorites', 'true');

      state.sounds = await api(`/api/sounds?${query.toString()}`);
      renderSounds();
      await loadStatus();
    }

    function renderSounds() {
      const root = $('sounds');
      root.replaceChildren();

      if (state.sounds.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'empty-state';
        empty.textContent = state.favoritesOnly
          ? 'No favorite sounds match this view.'
          : 'No sounds match this search.';
        root.appendChild(empty);
        return;
      }

      for (const sound of state.sounds) {
        const card = document.createElement('article');
        card.className = `sound ${sound.available ? '' : 'unavailable'}`;
        card.innerHTML = `
          <div class="sound-header">
            <div class="sound-name"></div>
            <div class="sound-meta"></div>
            <div class="tag-row"></div>
          </div>
          <div class="sound-actions">
            <button class="primary play" type="button">Play</button>
            <button class="favorite fav" type="button"></button>
          </div>`;

        card.querySelector('.sound-name').textContent = sound.name;
        card.querySelector('.sound-meta').textContent = `${sound.category} - ${sound.extension.toUpperCase()}`;

        const tagRow = card.querySelector('.tag-row');
        for (const tag of normalizeTags(sound.tags).map(value => value.trim()).filter(Boolean).slice(0, 3)) {
            const tagElement = document.createElement('span');
            tagElement.className = 'tag';
            tagElement.textContent = tag;
            tagRow.appendChild(tagElement);
        }

        if (!sound.available) {
          const unavailable = document.createElement('span');
          unavailable.className = 'tag';
          unavailable.textContent = 'Unavailable';
          tagRow.appendChild(unavailable);
        }

        const playButton = card.querySelector('.play');
        playButton.disabled = !sound.available;
        playButton.addEventListener('click', () => play(sound.id));

        const favoriteButton = card.querySelector('.fav');
        favoriteButton.textContent = sound.favorite ? 'Saved' : 'Fav';
        favoriteButton.classList.toggle('active', sound.favorite);
        favoriteButton.addEventListener('click', () => favorite(sound.id));

        root.appendChild(card);
      }
    }

    async function play(id) {
      await api(`/api/play/${id}`, { method: 'POST' });
      setStatus('Playing on the PC');
    }

    async function favorite(id) {
      await api(`/api/favorite/${id}`, { method: 'POST' });
      await loadSounds();
    }

    async function stopAll() {
      await api('/api/stop', { method: 'POST' });
      state.playbackPaused = false;
      updatePauseButton();
      setStatus('Playback stopped');
    }

    async function pauseOrResume() {
      const nextPaused = !state.playbackPaused;
      const result = await api(nextPaused ? '/api/pause' : '/api/resume', { method: 'POST' });
      state.playbackPaused = Boolean(result.playbackPaused);
      updatePauseButton();
      if (nextPaused && !state.playbackPaused) {
        setStatus('No playback to pause');
      } else if (!nextPaused && state.playbackPaused) {
        setStatus('Could not resume playback');
      } else {
        setStatus(state.playbackPaused ? 'Playback paused' : 'Playback resumed');
      }
    }

    async function rescan() {
      setLoading(true);
      setStatus('Scanning library...');
      try {
        await api('/api/rescan', { method: 'POST' });
        await loadSounds();
      } finally {
        setLoading(false);
      }
    }

    $('theme').addEventListener('click', () => {
      const nextTheme = currentTheme() === 'dark' ? 'light' : 'dark';
      state.themeOverride = nextTheme;
      localStorage.setItem('localsoundboard-remote-theme', nextTheme);
      applyTheme(nextTheme);
    });

    $('savePin').addEventListener('click', () => {
      state.pin = $('pin').value.trim();
      localStorage.setItem('localsoundboard-pin', state.pin);
      loadSounds().catch(err => setStatus(err.message));
    });

    $('pin').addEventListener('keydown', event => {
      if (event.key === 'Enter') {
        $('savePin').click();
      }
    });

    let searchTimeout = 0;
    $('search').addEventListener('input', () => {
      updateClearSearchButton();
      clearTimeout(searchTimeout);
      searchTimeout = window.setTimeout(() => {
        loadSounds().catch(err => setStatus(err.message));
      }, 120);
    });

    $('clearSearch').addEventListener('click', () => {
      if (!$('search').value) {
        return;
      }

      $('search').value = '';
      $('search').focus();
      updateClearSearchButton();
      clearTimeout(searchTimeout);
      loadSounds().catch(err => setStatus(err.message));
    });

    $('favorites').addEventListener('click', () => {
      state.favoritesOnly = !state.favoritesOnly;
      $('favorites').classList.toggle('primary', state.favoritesOnly);
      loadSounds().catch(err => setStatus(err.message));
    });

    $('stop').addEventListener('click', () => stopAll().catch(err => setStatus(err.message)));
    $('pause').addEventListener('click', () => pauseOrResume().catch(err => setStatus(err.message)));
    $('rescan').addEventListener('click', () => rescan().catch(err => setStatus(err.message)));

    const preferredTheme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    applyTheme(state.themeOverride || preferredTheme);
    updateClearSearchButton();
    updatePauseButton();

    loadStatus()
      .then(loadSounds)
      .catch(err => setStatus(err.message));
  </script>
</body>
</html>
""";
}
