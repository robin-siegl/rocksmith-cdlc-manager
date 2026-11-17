const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const {spawnSync} = require('node:child_process');

const root = path.resolve(__dirname, '..');
const version = fs.readFileSync(path.join(root, 'VERSION'), 'utf8').trim();
const temporary = fs.mkdtempSync(path.join(os.tmpdir(), 'rocksmith-release-'));
const archives = {
  desktop: path.join(root, 'dist', 'Rocksmith-CDLC-Manager-' + version + '-windows-x64.zip'),
  chrome: path.join(root, 'dist', 'Rocksmith-CDLC-Manager-' + version + '-chrome.zip')
};
const psQuote = value => "'" + value.replaceAll("'", "''") + "'";

try {
  for (const [name, archive] of Object.entries(archives)) {
    const target = path.join(temporary, name);
    const extracted = spawnSync('powershell.exe', ['-NoProfile', '-Command',
      '$ErrorActionPreference = "Stop"; Add-Type -AssemblyName System.IO.Compression.FileSystem; ' +
      '[IO.Compression.ZipFile]::ExtractToDirectory(' + psQuote(archive) + ', ' + psQuote(target) + ')'],
      {encoding:'utf8', timeout:60000});
    assert.equal(extracted.status, 0, extracted.stderr || String(extracted.error));
  }
  const extension = path.join(temporary, 'chrome');
  const manifest = JSON.parse(fs.readFileSync(path.join(extension, 'manifest.json')));
  assert.equal(manifest.version, version);
  for (const icon of Object.values(manifest.icons)) assert.ok(fs.statSync(path.join(extension, icon)).size > 0);
  assert.ok(manifest.permissions.includes('sidePanel'));
  assert.ok(fs.existsSync(path.join(extension, manifest.side_panel.default_path)));

  const exe = path.join(temporary, 'desktop', 'Rocksmith CDLC Manager.exe');
  const body = Buffer.from('{"type":"status"}');
  const header = Buffer.alloc(4);
  header.writeUInt32LE(body.length);
  const bridge = spawnSync(exe, ['chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/', '--parent-window=0'], {
    input:Buffer.concat([header,body]), timeout:10000, windowsHide:true
  });
  assert.equal(bridge.status, 0, String(bridge.error));
  assert.equal(bridge.stdout.readUInt32LE(), bridge.stdout.length - 4);
  assert.equal(JSON.parse(bridge.stdout.subarray(4)).ok, true);

  const preview = path.join(temporary, 'desktop.png');
  const ui = spawnSync(exe, ['--preview', preview], {timeout:20000, windowsHide:true});
  assert.equal(ui.status, 0, ui.stderr.toString() || String(ui.error));
  assert.ok(fs.statSync(preview).size > 10000, 'WPF shell rendered an empty image');
  console.log('Both extracted packages, icon assets, native messaging and rendered desktop shell passed.');
} finally {
  fs.rmSync(temporary, {recursive:true, force:true});
}

