'use strict';
/**
 * UYGULAMA KATMANI
 * ----------------
 * - Socket.io ile sunucuya bağlanır, olayları senaryo motoruna iletir
 * - Sahneyi pencereye/OBS'e sığdırır (1080x1920)
 * - Test panelini yönetir
 *
 * URL parametreleri:
 *   ?obs=1          OBS modu: panel ve ipuçları tamamen gizlenir
 *   ?panel=1        Test paneli açık başlar
 *   ?transparent=1  Senaryo arka planını gizler (şeffaf yayın katmanı)
 *   ?scenario=uzay  Sunucudaki aktif senaryoyu beklemeden bu senaryoyu aç
 */
(function () {
  const params = new URLSearchParams(location.search);
  const OBS = params.get('obs') === '1';
  const TRANSPARENT = params.get('transparent') === '1';
  const FORCE_SCENARIO = params.get('scenario');

  document.body.classList.toggle('obs', OBS);
  document.body.classList.toggle('transparent', TRANSPARENT);

  // ---------------------------------------------------------------- ölçekleme (OBS 1080x1920'de ölçek = 1)
  const stage = document.getElementById('stage');
  function fit() {
    const s = Math.min(window.innerWidth / 1080, window.innerHeight / 1920);
    stage.style.setProperty('--fit', s.toFixed(5));
  }
  window.addEventListener('resize', fit);
  fit();

  // ---------------------------------------------------------------- motor
  const audio = new AudioEngine();
  const engine = new SceneEngine({ audio, options: { transparent: TRANSPARENT, obs: OBS } });
  window.engine = engine; // konsoldan denemek için
  window.audio = audio;

  const socket = io();
  let currentScenarioId = null;
  let loading = false;

  async function loadScenario(id) {
    if (!id || loading) return;
    loading = true;
    try {
      const res = await fetch('/api/scenario/' + encodeURIComponent(id));
      if (!res.ok) throw new Error('Senaryo bulunamadı: ' + id);
      const sc = await res.json();
      currentScenarioId = id;
      await engine.loadScenario(sc);
      const sel = document.getElementById('scenario-select');
      if (sel) sel.value = id;
      buildCommandButtons(sc);
      setupGamePanel(sc);
      if (sc.game) {
        // Sunucudaki kalıcı oyun durumunu iste; önbellekte varsa hemen çiz
        if (gameStates[sc.id] && engine.gameView) engine.gameView.onState(gameStates[sc.id]);
        socket.emit('game:sync', { scenario: sc.id });
      }
      console.log('[sahne] senaryo yüklendi:', sc.name);
    } catch (err) {
      console.error(err);
    } finally {
      loading = false;
    }
  }

  socket.on('hello', (info) => {
    fillScenarioSelect(info.scenarios, info.active);
    setStatus(info.live);
    const target = FORCE_SCENARIO || info.active;
    if (target && target !== currentScenarioId) loadScenario(target);
  });
  socket.on('scenario:change', (id) => { if (!FORCE_SCENARIO) loadScenario(id); });
  socket.on('scene:reset', () => loadScenario(currentScenarioId));
  socket.on('event', (ev) => {
    audio.unlock(); // OBS'te kullanıcı etkileşimi olmadan da dene
    engine.handleEvent(ev);
  });
  socket.on('live:status', setStatus);

  // ---------------------------------------------------------------- oyun (sunucu taraflı) olayları
  const gameStates = {}; // senaryo id -> son anlık durum
  let currentScenario = null;
  socket.on('connect', () => { if (currentScenario && currentScenario.game) socket.emit('game:sync', { scenario: currentScenario.id }); });
  socket.on('game:state', (st) => {
    gameStates[st.scenario] = st;
    if (engine.gameView) engine.gameView.onState(st);
    if (currentScenario && currentScenario.id === st.scenario) renderGamePanel(st);
  });
  socket.on('game:fx', (fx) => { audio.unlock(); if (engine.gameView) engine.gameView.onFx(fx); });
  socket.on('game:admin-result', (res) => {
    const box = document.getElementById('game-result');
    if (!box) return;
    box.hidden = false; box.textContent = res.message; box.className = 'status ' + (res.ok ? 'connected' : 'error');
  });

  // ---------------------------------------------------------------- ses kilidi
  const hint = document.getElementById('audio-hint');
  async function tryUnlock() {
    const ok = await audio.unlock();
    if (ok && !audio.playing) audio.startMusic();
    return ok;
  }
  ['pointerdown', 'keydown', 'touchstart'].forEach((n) => window.addEventListener(n, tryUnlock, { passive: true }));
  hint.addEventListener('click', tryUnlock);
  setInterval(async () => {
    if (!audio.running) await tryUnlock();
    hint.hidden = OBS || audio.running;
  }, 1000);

  // ---------------------------------------------------------------- panel
  const panel = document.getElementById('panel');
  const toggleBtn = document.getElementById('panel-toggle');
  const setPanel = (open) => panel.classList.toggle('closed', !open);
  if (!OBS) {
    setPanel(params.get('panel') === '1');
    toggleBtn.addEventListener('click', () => panel.classList.toggle('closed'));
    document.getElementById('panel-close').addEventListener('click', () => setPanel(false));
    window.addEventListener('keydown', (e) => {
      if (['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement.tagName)) return;
      if (e.key.toLowerCase() === 'h') panel.classList.toggle('closed');
    });
  }

  const $ = (id) => document.getElementById(id);
  $('obs-url').value = `${location.origin}/?obs=1`;

  function setStatus(live) {
    const el = $('live-status');
    if (!el || !live) return;
    el.textContent = live.message;
    el.className = 'status ' + live.status;
  }

  function fillScenarioSelect(list, active) {
    const sel = $('scenario-select');
    sel.innerHTML = '';
    for (const s of list) {
      const o = document.createElement('option');
      o.value = s.id; o.textContent = s.name; o.title = s.description || '';
      sel.appendChild(o);
    }
    sel.value = currentScenarioId || active;
  }

  $('scenario-select').addEventListener('change', (e) => socket.emit('admin:setScenario', e.target.value));
  $('sc-reload').addEventListener('click', () => socket.emit('admin:reloadScenarios'));
  $('sc-reset').addEventListener('click', () => socket.emit('admin:reset'));
  $('tt-connect').addEventListener('click', () => socket.emit('admin:connect', $('tt-user').value));
  $('tt-disconnect').addEventListener('click', () => socket.emit('admin:disconnect'));
  $('tt-user').addEventListener('keydown', (e) => { if (e.key === 'Enter') socket.emit('admin:connect', $('tt-user').value); });
  try { $('tt-user').value = localStorage.getItem('tt-user') || ''; } catch (_e) { /* */ }
  $('tt-user').addEventListener('input', (e) => { try { localStorage.setItem('tt-user', e.target.value); } catch (_e) { /* */ } });

  $('audio-unlock').addEventListener('click', tryUnlock);
  $('mute').addEventListener('change', (e) => { audio.ensure(); audio.setMuted(e.target.checked); });
  $('volume').addEventListener('input', (e) => { audio.ensure(); audio.setMasterVolume(e.target.value / 100); });

  // ---------------------------------------------------------------- sahte olay üretimi
  const FIRST = ['Ahmet', 'Mehmet', 'Ayşe', 'Fatma', 'Zeynep', 'Emre', 'Elif', 'Can', 'Deniz', 'Cem', 'Selin', 'Burak', 'Merve', 'Kaan', 'Ece', 'Onur', 'Gizem', 'Hakan', 'Buse', 'Mert', 'Yağmur', 'Barış', 'Naz', 'Efe', 'İpek', 'Arda', 'Sude', 'Tolga', 'Derya', 'Murat'];
  const TAIL = ['_61', '_34', '.official', 'xx', '_tr', '35', '_halay', 'king', '_07', '1903', '_ist', '_ank'];
  let uid = 1000;

  function makeUser() {
    const typed = $('fake-name').value.trim();
    if (typed) return { id: 'test_' + typed.toLowerCase(), uniqueId: typed.toLowerCase(), nickname: typed };
    const name = FIRST[Math.floor(Math.random() * FIRST.length)];
    const id = ++uid;
    const uniqueId = (name + TAIL[Math.floor(Math.random() * TAIL.length)] + id).toLowerCase();
    return { id: 'test_' + id, uniqueId, nickname: name };
  }
  const send = (ev) => { audio.unlock(); socket.emit('test:event', ev); };

  const GIFTS = [
    { name: 'Rose', diamonds: 1, icon: '🌹' },
    { name: 'TikTok', diamonds: 1, icon: '🎵' },
    { name: 'Finger Heart', diamonds: 5, icon: '🫰' },
    { name: 'Doughnut', diamonds: 30, icon: '🍩' },
    { name: 'Hand Hearts', diamonds: 100, icon: '🫶' },
    { name: 'Galaxy', diamonds: 1000, icon: '🌌' },
    { name: 'Lion', diamonds: 29999, icon: '🦁' },
    { name: 'Diamond Ring', diamonds: 500, icon: '💍' },
  ];

  function sendGift(g, user) {
    const count = Math.max(1, parseInt($('gift-count').value, 10) || 1);
    send({ type: 'gift', user: user || makeUser(), gift: { id: g.name, name: g.name, diamondEach: g.diamonds, diamonds: g.diamonds * count, count, image: '' } });
  }

  const giftGrid = $('gift-grid');
  for (const g of GIFTS) {
    const b = document.createElement('button');
    b.innerHTML = `${g.icon} ${g.name}<small>${g.diamonds} 💎</small>`;
    b.addEventListener('click', () => sendGift(g));
    giftGrid.appendChild(b);
  }

  document.querySelectorAll('[data-like]').forEach((b) => b.addEventListener('click', () => {
    const n = Number(b.dataset.like);
    send({ type: 'like', user: makeUser(), count: n, total: n });
  }));
  document.querySelectorAll('[data-social]').forEach((b) => b.addEventListener('click', () => send({ type: b.dataset.social, user: makeUser() })));

  function sendComment(text) { if (text.trim()) send({ type: 'comment', user: makeUser(), text }); }
  $('comment-send').addEventListener('click', () => { sendComment($('comment-text').value); $('comment-text').value = ''; });
  $('comment-text').addEventListener('keydown', (e) => { if (e.key === 'Enter') { sendComment(e.target.value); e.target.value = ''; } });

  // Senaryodaki "command" tetikleyicilerinden hızlı komut butonları üret
  function buildCommandButtons(sc) {
    const grid = $('cmd-grid');
    grid.innerHTML = '';
    const seen = new Set();
    for (const tr of sc.triggers || []) {
      if (tr.on !== 'comment' || !tr.match || !tr.match.command) continue;
      const cmd = Array.isArray(tr.match.command) ? tr.match.command[0] : tr.match.command;
      if (seen.has(cmd)) continue;
      seen.add(cmd);
      const b = document.createElement('button');
      b.textContent = cmd;
      b.addEventListener('click', () => sendComment(cmd));
      grid.appendChild(b);
    }
  }

  // ---------------------------------------------------------------- OYUN PANELİ (Osmanlı vb.)
  let selectedUser = null; // listeden seçilen oyuncu (id)

  function setupGamePanel(sc) {
    currentScenario = sc;
    const sec = $('game-section');
    sec.hidden = !sc.game;
    if (!sc.game) return;
    const g = sc.game;
    $('game-title').textContent = sc.name;
    $('game-disclaimer').textContent = g.disclaimer || '';

    // test hediyeleri
    const gg = $('game-gifts'); gg.innerHTML = '';
    for (const t of g.testGifts || []) {
      const b = document.createElement('button');
      b.innerHTML = `${t.icon || '🎁'} ${t.name}<small>${t.diamonds.toLocaleString('tr-TR')} 💎</small>`;
      b.addEventListener('click', () => sendGift(t));
      gg.appendChild(b);
    }
    // komutlar
    const cg = $('game-cmds'); cg.innerHTML = '';
    const cmds = [];
    const first = (k) => [].concat(g.commands[k] || [])[0];
    cmds.push(first('join'), first('rank'), `${first('name')} Test Ad ${Math.floor(Math.random() * 90 + 10)}`);
    for (const j of Object.values(g.professions || {})) cmds.push(`${first('job')} ${j.names[0]}`);
    cmds.push(first('army'), first('treasury'));
    for (const c of cmds.filter(Boolean)) {
      const b = document.createElement('button');
      b.textContent = c;
      b.addEventListener('click', () => sendComment(c));
      cg.appendChild(b);
    }
    // rütbe seçici + rütbe tablosu
    const sel = $('game-rank'); sel.innerHTML = '';
    const tbl = $('game-ranks'); tbl.innerHTML = '';
    const grants = {};
    for (const gr of g.giftRankGrants || []) grants[gr.rank] = Math.min(grants[gr.rank] ?? Infinity, gr.minDiamonds);
    g.ranks.forEach((r, i) => {
      const o = document.createElement('option'); o.value = r.id; o.textContent = `${i + 1}. ${r.icon} ${r.name}`; sel.appendChild(o);
      const row = document.createElement('div'); row.className = 'rrow';
      const via = [r.auto !== false ? `${r.minPoints.toLocaleString('tr-TR')} puan` : null, grants[r.id] !== undefined ? `tek hediye ${grants[r.id].toLocaleString('tr-TR')}💎` : null].filter(Boolean).join(' · ');
      row.innerHTML = `<span class="rn">${i + 1}</span><span class="ri">${r.icon}</span><span class="rt">${r.name}</span><span class="rv">${via}</span>`;
      tbl.appendChild(row);
    });
    selectedUser = null;
    if (gameStates[sc.id]) renderGamePanel(gameStates[sc.id]);
  }

  function renderGamePanel(st) {
    if (!currentScenario || !currentScenario.game) return;
    const ranks = new Map(currentScenario.game.ranks.map((r) => [r.id, r]));
    $('game-total').textContent = `(${st.total} oyuncu)`;
    const th = st.throne;
    $('game-throne').textContent = th.holder
      ? `👑 Padişah: ${th.holder.name} (taç değeri ${th.crownValue.toLocaleString('tr-TR')} 💎) · koruma: ${th.cooldownLeft} sn`
      : '👑 Taht boş';
    const box = $('game-users');
    box.innerHTML = '';
    st.users.slice(0, 80).forEach((u, i) => {
      const r = ranks.get(u.rank);
      const row = document.createElement('div');
      row.className = 'urow' + (selectedUser === u.id ? ' sel' : '');
      row.innerHTML = `<span class="un">${i + 1}</span><img><span class="ut"><b></b><small></small></span><span class="up"></span>`;
      const img = row.querySelector('img'); img.src = u.avatar; img.onerror = () => { img.style.visibility = 'hidden'; };
      row.querySelector('b').textContent = u.name;
      row.querySelector('small').textContent = `${r.icon} ${r.name}${u.job ? ' · ' + u.job : ''}`;
      row.querySelector('.up').textContent = u.points.toLocaleString('tr-TR');
      row.addEventListener('click', () => {
        selectedUser = u.id;
        if (u.id.startsWith('test_')) $('fake-name').value = u.uniqueId; // test kullanıcısıysa onun adına olay gönder
        renderGamePanel(st);
        $('game-target').textContent = `Hedef: ${u.name} (${r.name})`;
      });
      box.appendChild(row);
    });
  }

  const adminTarget = () => selectedUser || (($('fake-name').value.trim()) ? 'test_' + $('fake-name').value.trim().toLowerCase() : null);
  function gameAdmin(action, payload) { socket.emit('admin:game', { action, payload }); }
  function needTarget() {
    const t = adminTarget();
    if (!t) { const b = $('game-result'); b.hidden = false; b.className = 'status error'; b.textContent = 'Önce listeden bir oyuncu seç veya Test Kullanıcısı kutusuna ad yaz (önce !katıl ile ekle).'; }
    return t;
  }
  $('game-setrank').addEventListener('click', () => { const t = needTarget(); if (t) gameAdmin('setRank', { user: t, rank: $('game-rank').value }); });
  $('game-crown').addEventListener('click', () => { const t = needTarget(); if (t) gameAdmin('crown', { user: t }); });
  $('game-points').addEventListener('click', () => { const t = needTarget(); if (t) gameAdmin('addPoints', { user: t, points: 100 }); });
  $('game-remove').addEventListener('click', () => { const t = needTarget(); if (t) { gameAdmin('removeUser', { user: t }); selectedUser = null; } });
  $('game-vacate').addEventListener('click', () => gameAdmin('vacateThrone'));
  $('game-wipe').addEventListener('click', () => {
    const ans = window.prompt('Tüm oyuncular, rütbeler ve hazine KALICI olarak silinecek.\nOnaylamak için SIFIRLA yaz:');
    if (ans === 'SIFIRLA') gameAdmin('wipe', { confirm: 'SIFIRLA' });
  });
  function joinMany(n) {
    let i = 0;
    const t = setInterval(() => {
      const id = ++uid;
      send({ type: 'comment', user: { id: 'test_' + id, uniqueId: 'kisi' + id, nickname: FIRST[id % FIRST.length] }, text: '!katıl' });
      if (++i >= n) clearInterval(t);
    }, 80);
  }
  $('game-join10').addEventListener('click', () => joinMany(10));
  $('game-join50').addEventListener('click', () => joinMany(50));

  // ---------------------------------------------------------------- toplu / otomatik test
  function burstUsers(n) {
    let i = 0;
    const t = setInterval(() => {
      sendGift(GIFTS[0], { id: 'test_' + (++uid), uniqueId: 'kisi' + uid, nickname: FIRST[uid % FIRST.length] });
      if (++i >= n) clearInterval(t);
    }, 90);
  }
  $('burst10').addEventListener('click', () => burstUsers(10));
  $('burst50').addEventListener('click', () => burstUsers(50));

  let autoTimer = null;
  $('auto-toggle').addEventListener('change', (e) => {
    clearInterval(autoTimer);
    if (!e.target.checked) return;
    autoTimer = setInterval(() => {
      const r = Math.random();
      if (r < 0.45) send({ type: 'like', user: makeUser(), count: 1 + Math.floor(Math.random() * 20), total: 0 });
      else if (r < 0.65) sendGift(GIFTS[Math.random() < 0.6 ? 0 : Math.floor(Math.random() * GIFTS.length)]);
      else if (r < 0.85) sendComment(['!zıpla', '!hızlan', 'merhaba', 'selam yayıncı', '!halay', '!fırlat', '🔥🔥🔥'][Math.floor(Math.random() * 7)]);
      else send({ type: Math.random() < 0.5 ? 'follow' : 'share', user: makeUser() });
    }, 700);
  });
})();
