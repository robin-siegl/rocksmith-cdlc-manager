let data = {queue: [], installed: []};
let scheduled = false;

function addControl(container, id, title) {
  if (!/^\d{1,12}$/.test(id) || !container) return;
  let control = container.querySelector('.rs-cdlc-save');
  if (!control) {
    control = document.createElement('button');
    control.type = 'button';
    control.className = 'rs-cdlc-control rs-cdlc-save';
    container.append(control);
  }
  const installed = data.installed.includes(id);
  const saved = data.queue.some(song => song.id === id);
  const text = installed ? '✓ Installed' : saved ? '✓ Saved' : '+ Save for later';
  if (control.textContent !== text) control.textContent = text;
  control.dataset.state = installed ? 'installed' : saved ? 'saved' : 'new';
  control.title = installed ? 'This listing is linked to a file in your Rocksmith DLC folder' : saved ? 'Remove from Download Later' : 'Add to Download Later';
  control.setAttribute('aria-disabled', String(installed));
  control.onclick = async event => {
    event.preventDefault();
    event.stopPropagation();
    if (installed || control.disabled) return;
    control.disabled = true;
    try {
      const result = await chrome.runtime.sendMessage({type:saved ? 'remove' : 'save', song:{id,title}});
      if (result.error) throw new Error(result.error);
      data = result;
      render();
    } catch (error) {
      control.title = error.message;
      control.textContent = 'Try again';
    } finally { control.disabled = false; }
  };
}

function render() {
  for (const row of document.querySelectorAll('tr[data-cdlc-id]')) {
    const id = row.dataset.cdlcId;
    const link = row.querySelector('a[href$="/cdlc/' + CSS.escape(id) + '"]');
    if (link) addControl(link.closest('td'), id, link.title || link.textContent.trim());
  }
  const detail = location.pathname.match(/^\/cdlc\/(\d+)\/?$/);
  if (detail) {
    const heading = document.querySelector('main h1, h1');
    if (heading) addControl(heading.parentElement, detail[1], heading.textContent.trim());
  }
}

function schedule() {
  if (scheduled) return;
  scheduled = true;
  requestAnimationFrame(() => {
    scheduled = false;
    render();
  });
}

async function refresh() {
  try {
    const next = await chrome.runtime.sendMessage({type:'cached'});
    if (next?.queue) { data = next; schedule(); }
  } catch { /* Page can outlive an extension reload. */ }
}
new MutationObserver(mutations => {
  if (mutations.some(mutation => !mutation.target.closest?.('.rs-cdlc-control'))) schedule();
}).observe(document.body, {childList:true, subtree:true});
chrome.storage.onChanged.addListener(changes => {
  if (changes.queue) data.queue = changes.queue.newValue || [];
  if (changes.installed) data.installed = changes.installed.newValue || [];
  schedule();
});
document.addEventListener('visibilitychange', () => { if (!document.hidden) void refresh(); });
setInterval(() => { if (!document.hidden) void refresh(); }, 30000);
void refresh();

async function requestWindowsDownload(button) {
  const id = location.pathname.match(/^\/cdlc\/(\d+)\/?$/)?.[1];
  if (!id) return;
  button.disabled = true;
  button.textContent = 'Getting Windows link…';
  try {
    const link = Array.from(document.querySelectorAll('a[href*="collectedcdlcs/toggle/"]')).find(anchor => {
      const url = new URL(anchor.href);
      return url.origin === location.origin && url.pathname.endsWith('/toggle/' + id) && url.searchParams.get('platform') === 'pc';
    });
    if (!link) throw new Error('Sign in to Ignition and use its Windows download button if available.');
    const token = document.querySelector('meta[name="csrf-token"]')?.content;
    if (!token) throw new Error('Reload Ignition, then try again.');
    const response = await fetch(link.href, {method:'PATCH', credentials:'same-origin', headers:{
      Accept:'application/json','X-Requested-With':'XMLHttpRequest','X-CSRF-TOKEN':token
    }});
    if (!response.ok) throw new Error('Ignition could not refresh the link. Try its Windows download button.');
    const {downloadLink} = await response.json();
    if (typeof downloadLink !== 'string') throw new Error('Ignition did not return a file link. Try its Windows download button.');
    const result = await chrome.runtime.sendMessage({type:'download-link',url:downloadLink,id});
    if (result.error) throw new Error(result.error);
    button.textContent = result.downloaded ? 'Download started' : 'File host opened';
  } catch (error) {
    button.textContent = 'Try Ignition’s Windows button';
    button.title = error.message;
  } finally {
    button.disabled = false;
  }
}

if (/^\/cdlc\/\d+\/?$/.test(location.pathname)) {
  const title = document.querySelector('main h1, h1');
  if (title) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'rs-cdlc-control';
    button.textContent = 'Download Windows';
    button.onclick = () => requestWindowsDownload(button);
    title.parentElement.append(button);
    chrome.runtime.sendMessage({type:'download-intent'}).then(result => {
      if (result?.start) void requestWindowsDownload(button);
    }).catch(() => {});
  }
}


