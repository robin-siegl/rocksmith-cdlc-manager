let data = {queue: [], installed: []};
let scheduled = false;

function addControl(container, id, title) {
  if (!/^\d{1,12}$/.test(id) || !container) return;
  let control = container.querySelector('.rs-cdlc-control');
  if (!control) {
    control = document.createElement('button');
    control.type = 'button';
    control.className = 'rs-cdlc-control';
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
