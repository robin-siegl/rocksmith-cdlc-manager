const list = document.querySelector('#queue');
const status = document.querySelector('#status');

async function update(message = {type:'status'}) {
  try {
    const data = await chrome.runtime.sendMessage(message);
    if (data.error) throw new Error(data.error);
    const songs = data.queue.filter(song => !data.installed.includes(song.id));
    status.textContent = (songs.length ? songs.length + ' saved' : 'No songs saved') +
      (data.connected ? ' · Windows app connected' : ' · Stored in Chrome');
    list.replaceChildren(...songs.map(song => {
      const row = document.createElement('li');
      const link = document.createElement('a');
      link.href = 'https://ignition4.customsforge.com/cdlc/' + song.id;
      link.target = '_blank';
      link.rel = 'noopener';
      link.textContent = song.title;
      const remove = document.createElement('button');
      remove.type = 'button';
      remove.textContent = 'Remove';
      remove.setAttribute('aria-label', 'Remove ' + song.title);
      remove.onclick = async () => {
        remove.disabled = true;
        await update({type:'remove',song});
        remove.disabled = false;
      };
      row.append(link, remove);
      return row;
    }));
  } catch (error) { status.textContent = error.message; }
}

document.querySelector('#refresh').onclick = () => update();
void update();
