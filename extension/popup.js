const list = document.querySelector('#queue');
const status = document.querySelector('#status');
const icons = {
  Download:'M 8 1.5 V 10 M 4.5 6.5 L 8 10 L 11.5 6.5 M 2 11 V 14 H 14 V 11',
  Remove:'M 2 4 H 14 M 4 4 L 5 14 H 11 L 12 4 M 6 4 V 2 H 10 V 4 M 7 6.5 V 11.5 M 9 6.5 V 11.5'
};

function actionButton(action, song) {
  const button = document.createElement('button');
  button.type = 'button';
  button.title = action;
  button.setAttribute('aria-label', action + ' ' + song.title);
  const ns = 'http://www.w3.org/2000/svg';
  const icon = document.createElementNS(ns, 'svg');
  icon.setAttribute('viewBox', '0 0 16 16');
  icon.setAttribute('aria-hidden', 'true');
  const path = document.createElementNS(ns, 'path');
  path.setAttribute('d', icons[action]);
  icon.append(path);
  button.append(icon);
  return button;
}

async function update(message = {type:'status'}) {
  try {
    const data = await chrome.runtime.sendMessage(message);
    if (data.error) throw new Error(data.error);
    render(data);
  } catch (error) { status.textContent = error.message; }
}

function render(data) {
    const songs = data.queue;
    status.textContent = (songs.length ? songs.length + ' saved' : 'No songs saved') +
      (data.connected ? ' · Windows app connected' : ' · Stored in Chrome');
    list.replaceChildren(...songs.map(song => {
      const row = document.createElement('li');
      const link = document.createElement('a');
      link.href = 'https://ignition4.customsforge.com/cdlc/' + song.id;
      link.target = '_blank';
      link.rel = 'noopener';
      link.textContent = song.title;
      link.title = song.title;
      const download = actionButton('Download', song);
      download.onclick = async () => {
        download.disabled = true;
        try {
          const result = await chrome.runtime.sendMessage({type:'start-download',id:song.id});
          if (result.error) throw new Error(result.error);
        } catch (error) { status.textContent = error.message; }
        finally { download.disabled = false; }
      };
      const remove = actionButton('Remove', song);
      remove.onclick = async () => {
        remove.disabled = true;
        await update({type:'remove',song});
        remove.disabled = false;
      };
      row.append(link, download, remove);
      return row;
    }));
}

chrome.storage.onChanged.addListener((changes, area) => {
  if (area !== 'local' || (!changes.queue && !changes.connected)) return;
  chrome.storage.local.get(['queue','connected']).then(data => {
    if (data.queue) render(data);
  }).catch(error => { status.textContent = error.message; });
});
document.querySelector('#refresh').onclick = () => update();
void update();

