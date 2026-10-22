const $ = id => document.getElementById(id);
const stored = key => { try { return localStorage.getItem(key) || ''; } catch { return ''; } };
const remember = (key, value) => { try { localStorage.setItem(key, value); } catch { } };
const state = {
  sounds: [], playback: { sessions: [], hasPlayback: false, isPaused: false },
  favoritesOnly: false, pin: stored('localsoundboard-pin'),
  themeOverride: stored('localsoundboard-remote-theme'), revision: -1,
  stream: null, listRequest: 0, connected: false
};
const icons = {
  play: '<path d="m9 5 11 7-11 7z"/>',
  pause: '<path d="M9 4H5v16h4zM19 4h-4v16h4z"/>',
  stop: '<rect x="5" y="5" width="14" height="14" rx="1"/>'
};
const icon = name => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round" aria-hidden="true">${icons[name]}</svg>`;
const setStatus = text => { $('status').textContent = text; $('status').title = text; };
const report = error => { if (error.name !== 'AbortError') setStatus(error.message); };

function applyTheme(theme) {
  document.documentElement.dataset.theme = theme;
  $('themeLabel').textContent = theme === 'dark' ? 'Dark' : 'Light';
  $('theme').setAttribute('aria-pressed', String(theme === 'dark'));
}

function connection(connected, text) {
  state.connected = connected;
  $('connection').textContent = text;
  $('connection').classList.toggle('offline', !connected);
  renderPlayback();
  $('bank').disabled = !connected;
  $('favorites').disabled = !connected;
  $('rescan').disabled = !connected;
  for (const button of document.querySelectorAll('.play, .fav')) button.disabled = !connected || button.dataset.unavailable === 'true';
}

async function api(path, options = {}) {
  const response = await fetch(path, {
    ...options, cache: 'no-store',
    headers: { ...(state.pin ? { 'X-Soundboard-Pin': state.pin } : {}), ...options.headers }
  });
  if (response.status === 401) {
    $('pinBox').classList.add('visible');
    connection(false, 'PIN required');
    throw new Error('PIN required or incorrect');
  }
  if (!response.ok) {
    const body = await response.text();
    let error = body;
    try { error = JSON.parse(body).error || body; } catch { }
    throw new Error(error || `Request failed (${response.status})`);
  }
  return response;
}
async function json(path, options) { return (await api(path, options)).json(); }
const post = (path, body) => json(path, {
  method: 'POST', ...(body ? { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : {})
});

async function loadStatus() {
  const status = await json('/api/status');
  $('library').textContent = status.library || 'No folder loaded';
  $('counts').textContent = `${status.availableSounds}/${status.totalSounds} sounds`;
  if (!state.themeOverride) applyTheme(status.isDarkMode ? 'dark' : 'light');
  state.playback = status.playback;
  renderPlayback();
  return status;
}

async function loadBanks() {
  const selected = $('bank').value;
  const banks = await json('/api/banks');
  $('bank').replaceChildren(new Option('All sounds', ''));
  for (const bank of banks) $('bank').appendChild(new Option(bank.name, bank.id));
  if (banks.some(bank => bank.id === selected)) $('bank').value = selected;
}

async function loadSounds() {
  const request = ++state.listRequest;
  const query = new URLSearchParams();
  if ($('search').value.trim()) query.set('search', $('search').value.trim());
  if (state.favoritesOnly) query.set('favorites', 'true');
  if ($('bank').value) query.set('bank', $('bank').value);
  const sounds = await json(`/api/sounds?${query}`);
  if (request !== state.listRequest) return;
  state.sounds = sounds;
  renderSounds();
}

function renderSounds() {
  $('sounds').replaceChildren();
  if (!state.sounds.length) {
    const empty = document.createElement('div');
    empty.className = 'empty-state';
    empty.textContent = $('search').value ? 'No sounds match this search.' : 'No sounds in this view.';
    $('sounds').appendChild(empty);
    return;
  }
  for (const sound of state.sounds) {
    const card = document.createElement('article');
    card.dataset.soundId = sound.id;
    card.className = `sound ${sound.available ? '' : 'unavailable'}`;
    card.innerHTML = '<div class="sound-header"><div class="sound-name"></div><div class="sound-meta"></div><div class="tag-row"></div></div>'
      + '<div class="sound-actions"><button class="primary play" type="button">Play</button><button class="favorite fav" type="button"></button></div>';
    card.querySelector('.sound-name').textContent = sound.name;
    card.querySelector('.sound-meta').textContent = `${sound.category} - ${sound.extension}`;
    for (const tag of [...(sound.tags || []).slice(0, 3), ...(sound.available ? [] : ['Unavailable'])]) {
      const element = document.createElement('span');
      element.className = 'tag';
      element.textContent = tag;
      card.querySelector('.tag-row').appendChild(element);
    }
    const play = card.querySelector('.play');
    play.dataset.unavailable = String(!sound.available);
    play.disabled = !sound.available || !state.connected;
    play.addEventListener('click', () => post(`/api/play/${sound.id}`).then(() => setStatus('Playing on the PC')).catch(report));
    const favorite = card.querySelector('.fav');
    favorite.textContent = sound.favorite ? 'Saved' : 'Fav';
    favorite.classList.toggle('active', sound.favorite);
    favorite.setAttribute('aria-pressed', String(sound.favorite));
    favorite.disabled = !state.connected;
    favorite.addEventListener('click', () => post(`/api/favorite/${sound.id}`).then(loadSounds).catch(report));
    $('sounds').appendChild(card);
  }
  highlightPlaying();
}

function updateTransport() {
  const playback = state.playback;
  $('pause').innerHTML = icon(playback.isPaused ? 'play' : 'pause');
  $('pause').setAttribute('aria-label', playback.isPaused ? 'Resume all' : 'Pause all');
  $('pause').title = playback.isPaused ? 'Resume all' : 'Pause all';
  $('pause').disabled = !state.connected || !playback.hasPlayback;
  $('stop').innerHTML = icon('stop');
  $('stop').disabled = !state.connected || !playback.hasPlayback;
  $('nowPlaying').textContent = playback.sessions.length ? `Now playing (${playback.sessions.length})` : 'Nothing playing';
}
function time(seconds) {
  const rounded = Math.max(0, Math.floor(seconds));
  const hours = Math.floor(rounded / 3600);
  const minutes = Math.floor(rounded / 60) % 60;
  return `${hours ? `${hours}:${String(minutes).padStart(2, '0')}` : Math.floor(rounded / 60)}:${String(rounded % 60).padStart(2, '0')}`;
}

function renderPlayback() {
  updateTransport();
  const sessions = state.playback.sessions;
  const ids = sessions.map(session => session.id).join(',');
  if (state.activeIds !== undefined && state.activeIds !== ids)
    setStatus(sessions.length ? 'Playing on the PC' : 'No active playback');
  state.activeIds = ids;
  for (const row of [...$('sessions').children]) if (!sessions.some(session => session.id === row.dataset.sessionId)) row.remove();
  for (const session of sessions) {
    let row = [...$('sessions').children].find(row => row.dataset.sessionId === session.id);
    if (!row) {
      row = document.createElement('div');
      row.className = 'session';
      row.dataset.sessionId = session.id;
      row.innerHTML = '<span class="session-name"></span><div class="session-controls"><button class="session-pause icon-button" type="button"></button><button class="session-stop icon-button" type="button"></button></div>'
        + '<div class="session-seek"><input type="range" min="0" step="0.1" aria-label="Playback position"><span class="session-time"></span></div>';
      row.querySelector('.session-stop').innerHTML = icon('stop');
      row.querySelector('.session-stop').title = 'Stop sound';
      row.querySelector('.session-stop').setAttribute('aria-label', 'Stop sound');
      row.querySelector('.session-stop').addEventListener('click', () => post(`/api/sessions/${session.id}/stop`).catch(report));
      row.querySelector('.session-pause').addEventListener('click', () => {
        const current = state.playback.sessions.find(item => item.id === session.id);
        if (current) post(`/api/sessions/${session.id}/${current.isPaused ? 'resume' : 'pause'}`).catch(report);
      });
      const range = row.querySelector('input');
      range.addEventListener('pointerdown', () => { range.dataset.editing = 'true'; });
      range.addEventListener('input', () => {
        range.dataset.editing = 'true';
        row.querySelector('.session-time').textContent = `${time(Number(range.value))} / ${time(Number(range.max))}`;
      });
      range.addEventListener('change', async () => {
        try { await post(`/api/sessions/${session.id}/seek`, { seconds: Number(range.value) }); }
        catch (error) { report(error); }
        finally { delete range.dataset.editing; }
      });
      range.addEventListener('pointerup', () => { if (range.dataset.editing) setTimeout(() => { delete range.dataset.editing; }, 600); });
      range.addEventListener('pointercancel', () => { delete range.dataset.editing; });
      $('sessions').appendChild(row);
    }
    row.querySelector('.session-name').textContent = session.name;
    const pause = row.querySelector('.session-pause');
    pause.innerHTML = icon(session.isPaused ? 'play' : 'pause');
    pause.title = session.isPaused ? 'Resume sound' : 'Pause sound';
    pause.setAttribute('aria-label', pause.title);
    const range = row.querySelector('input');
    range.max = session.durationSeconds;
    if (!range.dataset.editing) {
      range.value = session.positionSeconds;
      row.querySelector('.session-time').textContent = `${time(session.positionSeconds)} / ${time(session.durationSeconds)}`;
    }
    for (const control of row.querySelectorAll('input, button')) control.disabled = !state.connected;
  }
  highlightPlaying();
}
function highlightPlaying() {
  for (const card of $('sounds').children)
    card.classList.toggle('is-playing', state.playback.sessions.some(session => session.soundId === card.dataset.soundId));
}

async function streamEvents(controller) {
  let retry = 1000;
  while (!controller.signal.aborted) {
    try {
      const response = await api('/api/events', { signal: controller.signal });
      connection(true, 'Connected');
      $('pinBox').classList.remove('visible');
      retry = 1000;
      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let pending = '';
      try {
        while (!controller.signal.aborted) {
          const { done, value } = await reader.read();
          if (done) throw new Error('PC disconnected');
          pending += decoder.decode(value, { stream: true });
          let boundary;
          while ((boundary = pending.indexOf('\n\n')) >= 0) {
            const event = pending.slice(0, boundary);
            pending = pending.slice(boundary + 2);
            if (!event.startsWith('data: ')) continue;
            const update = JSON.parse(event.slice(6));
            state.playback = update.playback;
            renderPlayback();
            if (!state.themeOverride) applyTheme(update.isDarkMode ? 'dark' : 'light');
            if (state.revision !== update.libraryRevision) {
              state.revision = update.libraryRevision;
              await loadBanks();
              await loadSounds();
              await loadStatus();
            }
          }
        }
      } finally { await reader.cancel().catch(() => {}); }
    } catch (error) {
      if (controller.signal.aborted) return;
      if ($('pinBox').classList.contains('visible')) { report(error); return; }
      connection(false, 'Disconnected - reconnecting');
      await new Promise(resolve => {
        const timeout = setTimeout(resolve, retry);
        controller.signal.addEventListener('abort', () => { clearTimeout(timeout); resolve(); }, { once: true });
      });
      retry = Math.min(retry * 2, 5000);
    }
  }
}
async function connect() {
  state.stream?.abort();
  const controller = new AbortController();
  state.stream = controller;
  connection(false, 'Connecting...');
  state.revision = -1;
  try {
    await loadStatus();
    await loadBanks();
    connection(true, 'Connected');
    await loadSounds();
  } catch (error) {
    report(error);
    if ($('pinBox').classList.contains('visible')) return;
    connection(false, 'Disconnected - reconnecting');
  }
  void streamEvents(controller);
}

$('theme').addEventListener('click', () => {
  state.themeOverride = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
  remember('localsoundboard-remote-theme', state.themeOverride);
  applyTheme(state.themeOverride);
});
$('savePin').addEventListener('click', () => {
  state.pin = $('pin').value.trim();
  remember('localsoundboard-pin', state.pin);
  connect().catch(report);
});
$('pin').addEventListener('keydown', event => { if (event.key === 'Enter') $('savePin').click(); });
let searchTimeout;
$('search').addEventListener('input', () => {
  $('clearSearch').classList.toggle('visible', $('search').value.length > 0);
  // Invalidate any older response immediately, before the debounce completes.
  state.listRequest++;
  clearTimeout(searchTimeout);
  searchTimeout = setTimeout(() => loadSounds().catch(report), 120);
});
$('clearSearch').addEventListener('click', () => {
  clearTimeout(searchTimeout);
  $('search').value = '';
  $('search').focus();
  $('clearSearch').classList.remove('visible');
  loadSounds().catch(report);
});
$('bank').addEventListener('change', () => loadSounds().catch(report));
$('favorites').addEventListener('click', () => {
  state.favoritesOnly = !state.favoritesOnly;
  $('favorites').classList.toggle('primary', state.favoritesOnly);
  $('favorites').setAttribute('aria-pressed', String(state.favoritesOnly));
  loadSounds().catch(report);
});
$('pause').addEventListener('click', () => post(state.playback.isPaused ? '/api/resume' : '/api/pause').catch(report));
$('stop').addEventListener('click', () => post('/api/stop').catch(report));
$('rescan').addEventListener('click', async () => {
  $('rescan').disabled = true;
  setStatus('Scanning library...');
  try { await post('/api/rescan'); await loadSounds(); setStatus('Library refreshed'); }
  catch (error) { report(error); }
  finally { $('rescan').disabled = false; }
});
window.addEventListener('pagehide', () => state.stream?.abort());
window.addEventListener('pageshow', event => { if (event.persisted) connect().catch(report); });
new ResizeObserver(entries => {
  document.documentElement.style.setProperty('--transport-height', `${Math.ceil(entries[0].target.getBoundingClientRect().height) + 24}px`);
}).observe(document.querySelector('.transport'));
applyTheme(state.themeOverride || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));
connect().then(() => setStatus('Private LAN remote control')).catch(error => { connection(false, 'Connection unavailable'); report(error); });
