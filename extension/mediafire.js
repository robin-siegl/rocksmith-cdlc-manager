(async () => {
  try {
    const intent = await chrome.runtime.sendMessage({type:'host-intent'});
    if (!intent?.start) return;
    const link = document.querySelector('a#downloadButton[href]');
    if (link) await chrome.runtime.sendMessage({type:'host-download',url:link.href});
  } catch { /* The MediaFire page remains available for manual download. */ }
})();

