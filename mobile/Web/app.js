'use strict';
const $ = id => document.getElementById(id);
let state = null, busy = false, hasSaved = false, nextId = 1, pending = new Map(), botTimer, noticeTimer;
const plain = text => (text || '').replace(/<[^>]*>/g, '').replace(/\$|#/g, '');
function notice(text) { $('notice').textContent = text; $('notice').hidden = false; clearTimeout(noticeTimer); noticeTimer = setTimeout(() => $('notice').hidden = true, 12000); }
$('notice').onclick = () => $('notice').hidden = true;
function button(text, action, className = '') { const b = document.createElement('button'); b.textContent = text; b.className = className; b.onclick = action; return b; }
function sheet(title) { $('sheet-title').textContent = title; $('sheet-body').replaceChildren(); if (!$('sheet').open) $('sheet').showModal(); return $('sheet-body'); }
$('close-sheet').onclick = () => $('sheet').close();
$('sheet').addEventListener('click', e => { if (e.target === $('sheet')) { const r = $('sheet').getBoundingClientRect(); if (e.clientY < r.top || e.clientX < r.left || e.clientX > r.right) $('sheet').close(); } });
function request(command, data = {}) {
  const id = nextId++;
  return new Promise((resolve, reject) => {
    pending.set(id, {resolve, reject});
    const payload = JSON.stringify({id, command, ...data});
    if (window.webkit?.messageHandlers?.solo) window.webkit.messageHandlers.solo.postMessage(payload);
    else fetch('/api', {method:'POST', headers:{'Content-Type':'application/json'}, body:payload})
      .then(r => { if (!r.ok) throw new Error(`Preview error ${r.status}`); return r.json(); }).then(window.receive)
      .catch(e => { pending.delete(id); reject(e); });
  });
}
window.receive = reply => { const p = pending.get(reply.id); if (!p) return; pending.delete(reply.id); p.resolve(reply); };
async function run(command, data = {}) {
  if (busy) return;
  busy = true; clearTimeout(botTimer); setControls();
  try {
    const reply = await request(command, data);
    if (reply.error) throw new Error(reply.error);
    if (reply.warning) notice(reply.warning);
    if (reply.state) { state = reply.state; render(); }
    hasSaved = reply.canResume;
    $('resume').hidden = !hasSaved;
    return reply;
  } catch(e) { notice(e.message); }
  finally { busy = false; setControls(); scheduleBot(); }
}
function setControls() {
  $('start').disabled = busy; $('resume').disabled = busy;
  $('end-turn').disabled = busy || !state?.decision?.options.some(o => o.type === 'END_TURN');
  $('all-moves').disabled = busy || !state?.decision;
  $('choices').disabled = busy;
  for (const b of $('sheet-body').querySelectorAll('button')) b.disabled = busy;
}
function scheduleBot() {
  if (!busy && state && !state.complete && !state.decision) botTimer = setTimeout(() => run('bot'), 350);
}
function answer(selections) {
  if (!state?.decision || busy) return;
  const data = {revision: state.revision, decision: state.decision.id, selections};
  $('sheet').close(); run('answer', data);
}
function addText(parent, text, className) { const p = document.createElement('p'); p.textContent = text; p.className = className || ''; parent.append(p); return p; }
function entityDetail(entity) {
  const body = sheet(entity.name);
  addText(body, plain(entity.text) || 'No card text.', 'detail');
  addText(body, `${entity.cost} mana${entity.attack !== null ? ` · ${entity.attack} attack · ${entity.health} health` : ''}${entity.armor ? ` · ${entity.armor} armor` : ''}`, 'modal-note');
  if (state?.decision?.mulligan) { body.append(button('Choose starting cards', showChoices)); return; }
  const actions = state?.decision?.options.filter(o => o.source === entity.id) || [];
  if (!actions.length) addText(body, 'No legal move from this card right now.', 'modal-note');
  else for (const action of actions) body.append(button(action.label, () => answer([action.id])));
}
function showMoves() {
  if (state?.decision?.mulligan) return showChoices();
  const body = sheet('Your legal moves');
  addText(body, 'Each move includes its target, board position and any card choice. Tap to confirm.', 'modal-note');
  for (const action of state?.decision?.options || []) body.append(button(action.label, () => answer([action.id])));
}
function showChoices() {
  const d = state?.decision; if (!d) return;
  if (!d.mulligan) return showMoves();
  const body = sheet('Choose your starting hand');
  addText(body, 'Select the cards to KEEP. Unselected cards will be replaced.', 'modal-note');
  const kept = new Set(d.options.map(o => o.id));
  for (const o of d.options) {
    const card = state.player.hand.find(c => c.id === o.source);
    const b = button(`Keep · ${o.label}${card ? ` · ${card.cost} mana` : ''}`, () => {
      kept.has(o.id) ? kept.delete(o.id) : kept.add(o.id); b.setAttribute('aria-pressed', String(kept.has(o.id)));
    }, 'keep');
    b.setAttribute('aria-pressed', 'true'); body.append(b);
  }
  body.append(button('Confirm starting hand', () => answer([...kept]), 'gold'));
}
function cardNode(entity, hand) {
  const b = button('', () => entityDetail(entity), hand ? 'card' : 'unit');
  if (state.decision?.options.some(o => o.source === entity.id)) b.classList.add('legal');
  const name = document.createElement('span'); name.textContent = entity.name; name.className = 'name';
  if (hand) {
    const top = document.createElement('div'); top.className = 'card-top';
    const cost = document.createElement('span'); cost.className = 'cost'; cost.textContent = entity.cost;
    top.append(cost, name); b.append(top);
    const text = document.createElement('span'); text.className = 'text'; text.textContent = plain(entity.text); b.append(text);
  } else b.append(name);
  if (entity.attack !== null) {
    const stats = document.createElement('div'); stats.className = 'stats';
    const atk = document.createElement('span'); atk.textContent = `${entity.attack} ⚔`;
    const hp = document.createElement('span'); hp.className = 'health'; hp.textContent = `${entity.health} ♥`;
    stats.append(atk, hp); b.append(stats);
  }
  b.setAttribute('aria-label', `${entity.name}, ${entity.cost} mana${entity.health !== null ? `, ${entity.attack} attack, ${entity.health} health` : ''}. Read card and moves`);
  return b;
}
function hero(id, seat) {
  const b = $(id); b.replaceChildren();
  const name = document.createElement('span'); name.textContent = seat.hero.name;
  const hp = document.createElement('strong'); hp.textContent = `${seat.hero.health} ♥${seat.hero.armor ? ` +${seat.hero.armor} ⛨` : ''}`;
  b.append(name, hp); b.onclick = () => entityDetail(seat.hero);
}
function render() {
  $('home').hidden = true; $('game').hidden = false;
  const s = state;
  $('turn').textContent = `TURN ${s.turn}`;
  hero('opponent-hero', s.opponent); hero('your-hero', s.player);
  $('opponent-meta').textContent = `${s.opponent.handCount} cards · ${s.opponent.deckCount} in deck`;
  $('your-meta').textContent = `${s.player.deckCount} in deck`;
  $('power').textContent = `${s.player.power.name} · ${s.player.power.cost} ◇`;
  $('power').onclick = () => entityDetail(s.player.power);
  for (const [id, cards] of [['enemy-board', s.opponent.board], ['your-board', s.player.board]]) {
    $(id).replaceChildren(...cards.map(c => cardNode(c, false)));
    if (!cards.length) addText($(id), id === 'enemy-board' ? 'Across the table' : 'Your side of the table', 'empty');
  }
  $('hand').replaceChildren(...s.player.hand.map(c => cardNode(c, true)));
  $('mana').textContent = `◇ ${s.player.mana} / ${s.player.maxMana} mana`;
  $('status').textContent = s.complete ? `MATCH COMPLETE · ${s.result}` : s.decision?.mulligan ? 'CHOOSE YOUR STARTING HAND' : s.decision ? 'YOUR MOVE' : 'OPPONENT IS THINKING';
  $('choices').hidden = !s.decision?.mulligan;
  $('end-turn').hidden = Boolean(s.decision?.mulligan) || s.complete;
  if (s.complete) {
    const body = sheet(s.result === 'WON' ? 'A well-earned victory.' : s.result === 'TIED' ? 'An even match.' : 'Another round awaits.');
    addText(body, `Match complete in ${s.turn} turns.`, 'detail');
    body.append(button('Back to the inn', backHome, 'gold'));
  }
}
function backHome() { clearTimeout(botTimer); state = null; $('sheet').close(); $('game').hidden = true; $('home').hidden = false; }
$('all-moves').onclick = showMoves; $('choices').onclick = showChoices;
$('end-turn').onclick = () => { const action = state?.decision?.options.find(o => o.type === 'END_TURN'); if (action) answer([action.id]); };
$('start').onclick = () => {
  const start = () => { $('sheet').close(); run('start', {player:$('player-deck').value, opponent:$('opponent-deck').value}); };
  if (!hasSaved) return start();
  const body = sheet('Start a new match?');
  addText(body, 'This replaces the saved match on this device.', 'detail');
  body.append(button('Start new match', start, 'gold'));
  body.append(button('Keep saved game', () => $('sheet').close()));
};
$('resume').onclick = () => run('resume');
$('menu').onclick = () => {
  const body = sheet('At your own pace');
  addText(body, 'The current position is saved after each move. You can close the app and continue later.', 'detail');
  body.append(button('Keep playing', () => $('sheet').close()));
  body.append(button('Return to the inn', backHome));
  body.append(button('Concede match', () => {
    const confirmBody = sheet('Concede this match?');
    confirmBody.append(button('Concede', () => { $('sheet').close(); run('concede'); }, 'danger'));
    confirmBody.append(button('Keep playing', () => $('sheet').close()));
  }, 'danger'));
};
$('about').onclick = () => {
  const body = sheet('SoloStone for iOS');
  addText(body, 'An early standalone port of SoloStone’s open-source rules engine with a new touch interface. This build supports offline practice matches and local saves. It does not yet include the Windows mod’s progression, shop, packs or original Hearthstone presentation.', 'detail');
  addText(body, 'SoloStone and the SabberStone-based engine are AGPL-3.0-or-later. Card names and text belong to Blizzard Entertainment. Unofficial; not affiliated with Blizzard. This app bundles no Hearthstone art, sound or client binaries.', 'modal-note');
  const link = document.createElement('a'); link.textContent = 'Source: github.com/KeyStarr/SoloStone'; link.href = 'https://github.com/KeyStarr/SoloStone'; body.append(link);
  body.append(button('Read licenses & notices', async () => {
    try {
      const reply = await request('legal');
      if (reply.error) throw new Error(reply.error);
      const list = sheet('Licenses & notices');
      for (const [name, text] of Object.entries(reply.legal)) list.append(button(name, () => {
        const detail = sheet(name); const pre = document.createElement('pre'); pre.className = 'license-text'; pre.textContent = text; detail.append(pre);
      }));
    } catch(e) { notice(e.message); }
  }));
};
(async () => {
  const reply = await run('init');
  if (!reply) { $('start').textContent = 'Reload to retry'; $('start').disabled = true; return; }
  for (const id of ['player-deck','opponent-deck']) for (const deck of reply.decks) {
    const option = document.createElement('option'); option.value = deck.id; option.textContent = `${deck.name} · ${deck.class.toLowerCase()}`; $(id).append(option);
  }
  $('opponent-deck').selectedIndex = Math.min(1, reply.decks.length - 1);
  $('start').textContent = 'Take a seat →';
})();
