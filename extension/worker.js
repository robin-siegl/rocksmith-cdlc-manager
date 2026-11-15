const host = 'local.rocksmith_cdlc_manager';
let serial = Promise.resolve();
let lastRefresh = 0;

async function native(message) {
  const response = await chrome.runtime.sendNativeMessage(host, message);
  if (!response.ok) throw new Error(response.error);
  return response;
}

async function update(message) {
  const {queue = [], pending = {}, installed = [], connected = false} = await chrome.storage.local.get(['queue', 'pending', 'installed', 'connected']);
  const entries = new Map(queue.map(song => [song.id, song]));
  if (['save', 'remove'].includes(message.type)) {
    const id = String(message.song?.id);
    if (!/^\d{1,12}$/.test(id)) throw new Error('Expected an Ignition song.');
    const song = {id, title:String(message.song.title || 'CDLC ' + id).slice(0,250)};
    if (message.type === 'save') entries.set(id, song);
    else entries.delete(id);
    pending[id] = {type:message.type, song};
    await chrome.storage.local.set({queue:[...entries.values()], pending});
  } else if (message.type === 'cached' && Date.now() - lastRefresh < 15000) {
    return {queue, installed, connected};
  }

  try {
    for (const [id, change] of Object.entries(pending)) {
      await native(change);
      delete pending[id];
      await chrome.storage.local.set({pending});
    }
    const result = await native({type:'status'});
    const next = {queue:result.queue, installed:result.installed, connected:true};
    await chrome.storage.local.set(next);
    lastRefresh = Date.now();
    return next;
  } catch {
    const next = {queue:[...entries.values()], installed:[], connected:false};
    await chrome.storage.local.set(next);
    lastRefresh = Date.now();
    return next;
  }
}

chrome.runtime.onMessage.addListener((message, sender, reply) => {
  const origin = sender.url ? new URL(sender.url).origin : '';
  if (!['https://ignition4.customsforge.com', 'https://www.ignition4.customsforge.com'].includes(origin) &&
      sender.url !== chrome.runtime.getURL('popup.html')) return;
  if (!['save','remove','status','cached'].includes(message.type)) return;
  const task = serial.then(() => update(message));
  serial = task.catch(() => {});
  task.then(reply, error => reply({error:error.message}));
  return true;
});
