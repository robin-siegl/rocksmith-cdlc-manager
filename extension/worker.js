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
    await retryCompleted();
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
  const page = sender.url ? new URL(sender.url) : null;
  const origin = page?.origin;
  const popup = sender.url === chrome.runtime.getURL('popup.html');
  if (['start-download','download-intent','download-link','host-intent','host-download'].includes(message.type)) {
    downloadAction(message, sender, page, popup).then(reply, error => reply({error:error.message}));
    return true;
  }
  if (!['https://ignition4.customsforge.com', 'https://www.ignition4.customsforge.com'].includes(origin) &&
      sender.url !== chrome.runtime.getURL('popup.html')) return;
  if (!['save','remove','status','cached'].includes(message.type)) return;
  const task = serial.then(() => update(message));
  serial = task.catch(() => {});
  task.then(reply, error => reply({error:error.message}));
  return true;
});

const pcFile = /_p(?: \(\d+\))?\.psarc$/i;
const ignition = 'https://ignition4.customsforge.com';
const mediafire = new Set(['https://www.mediafire.com','https://mediafire.com']);

async function trackComplete(id) {
  const key = 'download:' + id;
  const stored = await chrome.storage.local.get(key);
  if (!stored[key]) return;
  const [item] = await chrome.downloads.search({id});
  if (!item || item.state === 'in_progress') return;
  if (item.state === 'complete' && pcFile.test(item.filename)) {
    await native({type:'downloaded',song:{id:stored[key]},file:item.filename});
  }
  await chrome.storage.local.remove(key);
}

chrome.downloads.onChanged.addListener(change => {
  if (change.state) void trackComplete(change.id).catch(() => {});
});

async function downloadFile(url, id) {
  const downloadId = await chrome.downloads.download({url:url.href,saveAs:false});
  await chrome.storage.local.set({['download:' + downloadId]:id});
  void trackComplete(downloadId).catch(() => {});
}

async function retryCompleted() {
  const all = await chrome.storage.local.get(null);
  for (const key of Object.keys(all)) {
    if (key.startsWith('download:')) {
      try { await trackComplete(Number(key.slice(9))); }
      catch { /* The Windows connection can be established later. */ }
    }
  }
}

async function downloadAction(message, sender, page, popup) {
  const id = String(message.id);
  if (message.type === 'start-download' && popup && /^\d{1,12}$/.test(id)) {
    const tab = await chrome.tabs.create({url:'about:blank'});
    await chrome.storage.session.set({['ignition:' + tab.id]:id});
    await chrome.tabs.update(tab.id,{url:ignition + '/cdlc/' + id});
    return {};
  }
  const tabId = sender.tab?.id;
  if (tabId === undefined || !page) throw new Error('Open the song in Ignition first.');
  const key = 'ignition:' + tabId;
  const mfKey = 'mediafire:' + tabId;
  const song = page.pathname.match(/^\/cdlc\/(\d+)\/?$/)?.[1];
  const isIgnition = [ignition,'https://www.ignition4.customsforge.com'].includes(page.origin);
  if (message.type === 'download-intent' && isIgnition && song) {
    const values = await chrome.storage.session.get(key);
    await chrome.storage.session.remove(key);
    return {start:values[key] === song};
  }
  if (message.type === 'host-intent' && mediafire.has(page.origin)) {
    return {start:Boolean((await chrome.storage.session.get(mfKey))[mfKey])};
  }

  const url = new URL(message.url);
  if (url.protocol !== 'https:' || url.username || url.password) throw new Error('The host returned an unsupported download link.');
  const leaf = decodeURIComponent(url.pathname).split('/').filter(Boolean).pop() || '';
  if (message.type === 'host-download' && mediafire.has(page.origin)) {
    const tracked = (await chrome.storage.session.get(mfKey))[mfKey];
    if (!tracked || !/^download\d*\.mediafire\.com$/.test(url.hostname) || !pcFile.test(leaf))
      throw new Error('Select the PC file on MediaFire.');
    await chrome.storage.session.remove(mfKey);
    await downloadFile(url, tracked);
    return {downloaded:true};
  }
  if (message.type !== 'download-link' || !isIgnition || !song || song !== id) throw new Error('Invalid song download request.');

  if (['dropbox.com','www.dropbox.com'].includes(url.hostname) && /^\/(?:s\/|scl\/fi\/)/.test(url.pathname) && pcFile.test(leaf)) {
    url.searchParams.delete('raw');
    url.searchParams.set('dl','1');
    await downloadFile(url,id);
    return {downloaded:true};
  }
  if (pcFile.test(leaf) && !mediafire.has(url.origin) && !['drive.google.com','mega.nz','mega.io'].includes(url.hostname)) {
    await downloadFile(url,id);
    return {downloaded:true};
  }
  const tab = await chrome.tabs.create({url:'about:blank'});
  if (mediafire.has(url.origin) && url.pathname.startsWith('/file/'))
    await chrome.storage.session.set({['mediafire:' + tab.id]:id});
  await chrome.tabs.update(tab.id,{url:url.href});
  return {downloaded:false};
}

chrome.tabs.onRemoved.addListener(id => {
  void chrome.storage.session.remove(['ignition:' + id,'mediafire:' + id]);
});


