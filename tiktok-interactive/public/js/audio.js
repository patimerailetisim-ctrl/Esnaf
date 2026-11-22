'use strict';
/**
 * SES MOTORU
 * ----------
 * - Senaryodaki MP3/WAV/OGG dosyalarını yükler. Dosya yoksa otomatik olarak yerleşik
 *   (WebAudio ile üretilen) sese düşer, böylece hiç dosya olmadan da sistem sesli çalışır.
 * - Müzik tempoyu (hız) ve ses seviyesini canlı olarak değiştirebilir.
 */
class AudioEngine {
  constructor() {
    this.ctx = null;
    this.master = null;
    this.musicGain = null;
    this.sfxGain = null;
    this.noiseBuf = null;

    this.sounds = {};      // ad -> { buffer?, synth? }
    this.musicCfg = null;  // { buffer?, synth?, baseBpm, volume }

    this.tempo = 1;
    this.musicVol = 1;
    this.masterVol = 0.9;
    this.muted = false;

    this.musicSource = null;  // dosya müziği için
    this.timer = null;        // synth müzik zamanlayıcısı
    this.step = 0;
    this.nextTime = 0;
    this.playing = false;
  }

  // ---------------------------------------------------------------- temel
  ensure() {
    if (this.ctx) return this.ctx;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return null;
    this.ctx = new AC();
    this.master = this.ctx.createGain();
    this.master.gain.value = this.muted ? 0 : this.masterVol;
    this.master.connect(this.ctx.destination);
    this.musicGain = this.ctx.createGain();
    this.musicGain.gain.value = 0;
    this.musicGain.connect(this.master);
    this.sfxGain = this.ctx.createGain();
    this.sfxGain.gain.value = 0.9;
    this.sfxGain.connect(this.master);

    // paylaşılan beyaz gürültü
    const len = this.ctx.sampleRate * 2;
    this.noiseBuf = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
    const d = this.noiseBuf.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
    return this.ctx;
  }

  get running() { return !!this.ctx && this.ctx.state === 'running'; }

  async unlock() {
    this.ensure();
    if (!this.ctx) return false;
    if (this.ctx.state !== 'running') {
      try { await this.ctx.resume(); } catch (_e) { /* tarayıcı izin vermedi */ }
    }
    return this.running;
  }

  setMasterVolume(v) {
    this.masterVol = Math.max(0, Math.min(1, v));
    if (this.master) this.master.gain.setTargetAtTime(this.muted ? 0 : this.masterVol, this.ctx.currentTime, 0.05);
  }

  setMuted(m) {
    this.muted = !!m;
    if (this.master) this.master.gain.setTargetAtTime(this.muted ? 0 : this.masterVol, this.ctx.currentTime, 0.05);
  }

  // ---------------------------------------------------------------- yükleme
  async _loadBuffer(src) {
    if (!src) return null;
    try {
      const res = await fetch(src);
      if (!res.ok || res.status === 204) return null;
      const arr = await res.arrayBuffer();
      if (!arr.byteLength) return null;
      return await this.ctx.decodeAudioData(arr);
    } catch (_e) {
      return null; // dosya yok / bozuk -> synth'e düş
    }
  }

  async load(assets = {}) {
    this.stopMusic();
    this.sounds = {};
    this.musicCfg = null;
    if (!this.ensure()) return;

    const jobs = [];
    for (const [name, def] of Object.entries(assets.sounds || {})) {
      const cfg = typeof def === 'string' ? { src: def } : def;
      jobs.push(this._loadBuffer(cfg.src).then((buffer) => {
        this.sounds[name] = { buffer, synth: cfg.synth || 'pop', volume: cfg.volume ?? 1 };
      }));
    }
    if (assets.music) {
      const m = typeof assets.music === 'string' ? { src: assets.music } : assets.music;
      jobs.push(this._loadBuffer(m.src).then((buffer) => {
        this.musicCfg = { buffer, synth: m.synth || 'halay', baseBpm: m.baseBpm || 100, volume: m.volume ?? 0.6 };
      }));
    }
    await Promise.all(jobs);
  }

  // ---------------------------------------------------------------- müzik
  startMusic() {
    if (!this.ctx || !this.musicCfg || this.playing) return;
    this.playing = true;
    this._applyMusicGain();

    if (this.musicCfg.buffer) {
      const src = this.ctx.createBufferSource();
      src.buffer = this.musicCfg.buffer;
      src.loop = true;
      src.playbackRate.value = this.tempo;
      src.connect(this.musicGain);
      src.start();
      this.musicSource = src;
    } else {
      this.step = 0;
      this.nextTime = this.ctx.currentTime + 0.08;
      this.timer = setInterval(() => this._pump(), 25);
    }
  }

  stopMusic() {
    this.playing = false;
    if (this.timer) { clearInterval(this.timer); this.timer = null; }
    if (this.musicSource) { try { this.musicSource.stop(); } catch (_e) { /* */ } this.musicSource = null; }
    if (this.musicGain && this.ctx) this.musicGain.gain.setTargetAtTime(0, this.ctx.currentTime, 0.05);
  }

  setTempo(t) {
    this.tempo = Math.max(0.5, Math.min(3, t));
    if (this.musicSource) this.musicSource.playbackRate.setTargetAtTime(this.tempo, this.ctx.currentTime, 0.4);
  }

  setMusicVolume(v) {
    this.musicVol = Math.max(0, Math.min(1.5, v));
    this._applyMusicGain();
  }

  _applyMusicGain() {
    if (!this.musicGain || !this.ctx) return;
    const base = this.musicCfg ? this.musicCfg.volume : 0.6;
    const target = this.playing ? base * this.musicVol : 0;
    this.musicGain.gain.setTargetAtTime(target, this.ctx.currentTime, 0.3);
  }

  // ---------------------------------------------------------------- synth müzik zamanlayıcı
  _pump() {
    if (!this.ctx || !this.playing) return;
    const stepDur = () => 60 / (this.musicCfg.baseBpm * this.tempo) / 2; // sekizlik nota
    while (this.nextTime < this.ctx.currentTime + 0.14) {
      if (this.musicCfg.synth === 'space') this._stepSpace(this.step, this.nextTime, stepDur());
      else if (this.musicCfg.synth === 'mehter') this._stepMehter(this.step, this.nextTime, stepDur());
      else this._stepHalay(this.step, this.nextTime, stepDur());
      this.nextTime += stepDur();
      this.step = (this.step + 1) % 32;
    }
  }

  // Darbuka + zurna (Hicaz dizisi)
  _stepHalay(step, t, dur) {
    const bar = step % 8;
    const dum = [0, 3, 4].includes(bar);
    const tek = [2, 6].includes(bar);
    const ka = [1, 5, 7].includes(bar);
    if (dum) this._dum(t, bar === 0 ? 1 : 0.8);
    if (tek) this._tek(t, 0.55);
    if (ka) this._tek(t, 0.2);
    if (bar === 0 || bar === 4) this._bass(t, 73.42, dur * 3.6, 0.5);

    const scale = [293.66, 311.13, 369.99, 392.0, 440.0, 466.16, 523.25, 587.33];
    const melody = [
      4, -1, 3, 2, 3, -1, 2, 1,
      2, -1, 1, 0, 1, -1, 0, -1,
      4, -1, 5, 4, 3, -1, 2, 3,
      2, 1, 0, 1, 2, -1, 0, -1,
    ];
    const n = melody[step];
    if (n >= 0) {
      this._zurna(t, scale[n], dur * 1.7, 0.2);
      if (this.tempo > 1.5) this._zurna(t, scale[n] * 2, dur * 1.2, 0.07); // kalabalıkta üst oktav katmanı
    }
    if (this.tempo > 1.3 && bar % 2 === 1) this._tek(t + dur / 2, 0.15); // ekstra ritim
  }

  // Mehter marşı: kös (bas davul) + davul + zil + zurna (Hicaz dizisi)
  _stepMehter(step, t, dur) {
    const bar = step % 8;
    if (bar === 0 || bar === 4) this._dum(t, bar === 0 ? 1.1 : 0.9);        // kös
    if (bar === 2 || bar === 6) this._tek(t, 0.7);                           // davul
    if (bar % 2 === 1) this._cymbal(t, 0.16);                                // zil
    if (bar === 0) this._bass(t, 73.42, dur * 7, 0.35);
    if (bar === 4) this._bass(t, 110.0, dur * 3.5, 0.28);

    const scale = [293.66, 311.13, 369.99, 392.0, 440.0, 466.16, 523.25, 587.33];
    const melody = [
      4, -1, 4, 5, 4, -1, 2, -1,
      3, -1, 3, 4, 3, -1, 1, -1,
      2, -1, 2, 3, 2, -1, 1, 0,
      1, -1, 2, 1, 0, -1, 0, -1,
    ];
    const n = melody[step];
    if (n >= 0) {
      this._zurna(t, scale[n], dur * 1.5, 0.19);
      if (this.tempo > 1.15) this._zurna(t, scale[n] * 2, dur * 1.1, 0.06);
    }
  }

  _cymbal(t, vel = 0.2, dest = this.musicGain) {
    const s = this.ctx.createBufferSource();
    s.buffer = this.noiseBuf;
    const hp = this.ctx.createBiquadFilter();
    hp.type = 'highpass'; hp.frequency.value = 6500;
    const g = this.ctx.createGain();
    this._env(g, t, 0.002, vel, 0.14);
    s.connect(hp).connect(g).connect(dest);
    s.start(t, Math.random()); s.stop(t + 0.16);
  }

  // Uzay ambiyansı
  _stepSpace(step, t, dur) {
    const bar = step % 8;
    const arp = [220, 261.63, 329.63, 392, 329.63, 261.63, 196, 246.94];
    this._pluck(t, arp[bar] * (step >= 16 ? 1.5 : 1), dur * 1.8, 0.16);
    if (bar === 0) {
      this._pad(t, 110, dur * 8, 0.12);
      this._pad(t, 164.81, dur * 8, 0.09);
    }
    if (bar === 0 || bar === 4) this._dum(t, 0.45);
  }

  // ---------------------------------------------------------------- enstrüman parçaları
  _env(g, t, a, peak, dur) {
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(peak, t + a);
    g.gain.exponentialRampToValueAtTime(0.0001, t + Math.max(dur, a + 0.02));
  }

  _dum(t, vel = 1, dest = this.musicGain) {
    const o = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    o.type = 'sine';
    o.frequency.setValueAtTime(160, t);
    o.frequency.exponentialRampToValueAtTime(52, t + 0.14);
    this._env(g, t, 0.004, 0.95 * vel, 0.3);
    o.connect(g).connect(dest);
    o.start(t); o.stop(t + 0.32);
  }

  _tek(t, vel = 0.5, dest = this.musicGain) {
    const s = this.ctx.createBufferSource();
    s.buffer = this.noiseBuf;
    const bp = this.ctx.createBiquadFilter();
    bp.type = 'bandpass'; bp.frequency.value = 3200; bp.Q.value = 0.9;
    const g = this.ctx.createGain();
    this._env(g, t, 0.002, vel, 0.085);
    s.connect(bp).connect(g).connect(dest);
    s.start(t, Math.random()); s.stop(t + 0.1);
  }

  _bass(t, f, dur, vel) {
    const o = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    o.type = 'triangle'; o.frequency.value = f;
    this._env(g, t, 0.01, vel, dur);
    o.connect(g).connect(this.musicGain);
    o.start(t); o.stop(t + dur + 0.05);
  }

  _zurna(t, f, dur, vel) {
    const o = this.ctx.createOscillator();
    o.type = 'sawtooth'; o.frequency.value = f;
    const lfo = this.ctx.createOscillator();
    const lg = this.ctx.createGain();
    lfo.frequency.value = 5.6; lg.gain.value = 14;
    lfo.connect(lg).connect(o.detune);
    const f1 = this.ctx.createBiquadFilter();
    f1.type = 'bandpass'; f1.frequency.value = 1300; f1.Q.value = 1.4;
    const f2 = this.ctx.createBiquadFilter();
    f2.type = 'lowpass'; f2.frequency.value = 3200;
    const g = this.ctx.createGain();
    this._env(g, t, 0.015, vel, dur);
    o.connect(f1).connect(f2).connect(g).connect(this.musicGain);
    o.start(t); lfo.start(t);
    o.stop(t + dur + 0.05); lfo.stop(t + dur + 0.05);
  }

  _pluck(t, f, dur, vel) {
    const o = this.ctx.createOscillator();
    o.type = 'triangle'; o.frequency.value = f;
    const lp = this.ctx.createBiquadFilter();
    lp.type = 'lowpass'; lp.frequency.setValueAtTime(3000, t); lp.frequency.exponentialRampToValueAtTime(500, t + dur);
    const g = this.ctx.createGain();
    this._env(g, t, 0.005, vel, dur);
    o.connect(lp).connect(g).connect(this.musicGain);
    o.start(t); o.stop(t + dur + 0.05);
  }

  _pad(t, f, dur, vel) {
    for (const det of [-6, 6]) {
      const o = this.ctx.createOscillator();
      o.type = 'sine'; o.frequency.value = f; o.detune.value = det;
      const g = this.ctx.createGain();
      g.gain.setValueAtTime(0.0001, t);
      g.gain.linearRampToValueAtTime(vel, t + dur * 0.4);
      g.gain.linearRampToValueAtTime(0.0001, t + dur);
      o.connect(g).connect(this.musicGain);
      o.start(t); o.stop(t + dur + 0.05);
    }
  }

  // ---------------------------------------------------------------- ses efektleri
  play(name, volume = 1) {
    if (!this.ctx || !this.running) return;
    const def = this.sounds[name];
    if (!def) return;
    const vol = volume * (def.volume ?? 1);
    if (def.buffer) {
      const s = this.ctx.createBufferSource();
      s.buffer = def.buffer;
      const g = this.ctx.createGain();
      g.gain.value = vol;
      s.connect(g).connect(this.sfxGain);
      s.start();
    } else {
      this._synthSfx(def.synth, vol);
    }
  }

  _tone({ type = 'sine', f0, f1 = f0, dur = 0.2, gain = 0.4, delay = 0, attack = 0.005 }) {
    const t = this.ctx.currentTime + delay;
    const o = this.ctx.createOscillator();
    o.type = type;
    o.frequency.setValueAtTime(f0, t);
    if (f1 !== f0) o.frequency.exponentialRampToValueAtTime(f1, t + dur);
    const g = this.ctx.createGain();
    this._env(g, t, attack, gain, dur);
    o.connect(g).connect(this.sfxGain);
    o.start(t); o.stop(t + dur + 0.05);
  }

  _noise({ type = 'bandpass', f0 = 1000, f1 = f0, q = 1, dur = 0.3, gain = 0.4, delay = 0, attack = 0.01 }) {
    const t = this.ctx.currentTime + delay;
    const s = this.ctx.createBufferSource();
    s.buffer = this.noiseBuf;
    const f = this.ctx.createBiquadFilter();
    f.type = type; f.Q.value = q;
    f.frequency.setValueAtTime(f0, t);
    if (f1 !== f0) f.frequency.exponentialRampToValueAtTime(f1, t + dur);
    const g = this.ctx.createGain();
    this._env(g, t, attack, gain, dur);
    s.connect(f).connect(g).connect(this.sfxGain);
    s.start(t, Math.random()); s.stop(t + dur + 0.05);
  }

  _synthSfx(kind, v = 1) {
    switch (kind) {
      case 'pop':
        this._tone({ f0: 480, f1: 980, dur: 0.14, gain: 0.5 * v });
        break;
      case 'coin':
        this._tone({ type: 'square', f0: 988, dur: 0.08, gain: 0.22 * v });
        this._tone({ type: 'square', f0: 1319, dur: 0.28, gain: 0.22 * v, delay: 0.08 });
        break;
      case 'ding':
        this._tone({ f0: 1318, dur: 0.7, gain: 0.35 * v });
        this._tone({ f0: 1976, dur: 0.5, gain: 0.18 * v });
        break;
      case 'whoosh':
        this._noise({ f0: 300, f1: 3200, q: 1.2, dur: 0.5, gain: 0.5 * v, attack: 0.15 });
        break;
      case 'boom':
        this._tone({ f0: 130, f1: 28, dur: 1.0, gain: 1.0 * v });
        this._noise({ type: 'lowpass', f0: 1200, f1: 120, dur: 0.8, gain: 0.7 * v });
        this._tone({ type: 'sawtooth', f0: 261.63, dur: 0.6, gain: 0.15 * v, delay: 0.05 });
        break;
      case 'cheer':
        this._noise({ f0: 1400, q: 0.7, dur: 1.1, gain: 0.35 * v, attack: 0.4 });
        [523.25, 659.25, 783.99, 1046.5].forEach((f, i) => this._tone({ type: 'triangle', f0: f, dur: 0.35, gain: 0.25 * v, delay: i * 0.08 }));
        break;
      case 'rocket':
        this._noise({ type: 'lowpass', f0: 200, f1: 2200, dur: 1.4, gain: 0.7 * v, attack: 0.3 });
        this._tone({ type: 'sawtooth', f0: 70, f1: 220, dur: 1.3, gain: 0.25 * v });
        break;
      case 'gong':
        [[110, 2.8, 0.5], [166, 2.4, 0.3], [233, 2.0, 0.22], [311, 1.6, 0.15]].forEach(([f, d, g]) => this._tone({ f0: f, dur: d, gain: g * v, attack: 0.01 }));
        this._noise({ type: 'bandpass', f0: 800, q: 0.5, dur: 0.5, gain: 0.25 * v });
        break;
      case 'fanfare': { // taç giyme: davul çalımı + zurna/boru fanfarı + zil
        const seq = [[293.66, 0, 0.22], [293.66, 0.26, 0.2], [440, 0.52, 0.22], [587.33, 0.8, 0.5], [739.99, 1.4, 0.22], [880, 1.66, 1.0]];
        seq.forEach(([f, d, l]) => {
          this._tone({ type: 'sawtooth', f0: f, dur: l + 0.15, gain: 0.2 * v, delay: d });
          this._tone({ type: 'square', f0: f / 2, dur: l + 0.15, gain: 0.1 * v, delay: d });
        });
        for (let i = 0; i < 20; i++) this._noise({ type: 'highpass', f0: 3200, dur: 0.07, gain: 0.12 * v, delay: i * 0.07, attack: 0.002 });
        this._noise({ type: 'highpass', f0: 5200, dur: 1.6, gain: 0.45 * v, delay: 1.66, attack: 0.005 });
        this._tone({ f0: 90, f1: 38, dur: 0.9, gain: 0.9 * v, delay: 1.66 });
        break;
      }
      case 'fanfareShort':
        [[392, 0, 0.18], [523.25, 0.2, 0.18], [659.25, 0.4, 0.5]].forEach(([f, d, l]) => {
          this._tone({ type: 'sawtooth', f0: f, dur: l + 0.1, gain: 0.2 * v, delay: d });
          this._tone({ type: 'square', f0: f / 2, dur: l + 0.1, gain: 0.08 * v, delay: d });
        });
        this._noise({ type: 'highpass', f0: 5000, dur: 0.9, gain: 0.3 * v, delay: 0.4 });
        break;
      default:
        this._tone({ f0: 600, f1: 900, dur: 0.12, gain: 0.4 * v });
    }
  }
}

window.AudioEngine = AudioEngine;
