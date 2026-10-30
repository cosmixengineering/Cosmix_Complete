import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const userDir = join(tmpdir(), 'cdp-user-' + Date.now());
const edgePath = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';

console.log('Launching browser process...');
const browser = spawn(edgePath, [
  '--remote-debugging-port=9222',
  '--headless=new',
  '--ignore-certificate-errors',
  '--disable-gpu',
  '--no-first-run',
  '--no-default-browser-check',
  `--user-data-dir=${userDir}`
], { stdio: 'ignore' });

browser.on('error', (err) => console.error('Browser error:', err));
browser.on('exit', (code) => console.log('Browser exited with code:', code));

async function run() {
  // Wait for 9222 to be ready
  let version = null;
  for (let i = 0; i < 20; i++) {
    try {
      const res = await fetch('http://127.0.0.1:9222/json/version');
      if (res.ok) {
        version = await res.json();
        break;
      }
    } catch {
      await new Promise(r => setTimeout(r, 500));
    }
  }

  if (!version) {
    throw new Error('Browser failed to start CDP listener on port 9222');
  }

  console.log('Browser ready:', version.Browser);
  
  // Create new tab navigating to https://localhost:5173/
  const target = await (await fetch('http://127.0.0.1:9222/json/new?https://localhost:5173/', { method: 'PUT' })).json();
  console.log('Created tab:', target.id, target.url);

  const ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((res, rej) => {
    ws.onopen = res;
    ws.onerror = rej;
  });
  console.log('WebSocket connected.');

  let id = 1;
  function send(method, params = {}) {
    return new Promise((res) => {
      const msgId = id++;
      const handler = (e) => {
        const data = JSON.parse(e.data);
        if (data.id === msgId) {
          ws.removeEventListener('message', handler);
          res(data.result);
        }
      };
      ws.addEventListener('message', handler);
      ws.send(JSON.stringify({ id: msgId, method, params }));
    });
  }

  await send('Page.enable');
  await send('Runtime.enable');
  await send('Network.enable');

  // Wait 3 seconds for React app to mount
  await new Promise(r => setTimeout(r, 3000));

  const titleRes = await send('Runtime.evaluate', { expression: 'document.title' });
  console.log('Page Title:', titleRes.result.value);

  const urlRes = await send('Runtime.evaluate', { expression: 'window.location.href' });
  console.log('Page URL:', urlRes.result.value);

  const bodyText = await send('Runtime.evaluate', { expression: 'document.body.innerText' });
  console.log('Page Body Preview:\n', (bodyText.result.value || '').substring(0, 300));

  const shot = await send('Page.captureScreenshot');
  mkdirSync('docs/acceptance/evidence/browser', { recursive: true });
  writeFileSync('docs/acceptance/evidence/browser/00_initial_login_screen.png', Buffer.from(shot.data, 'base64'));
  console.log('Saved screenshot: docs/acceptance/evidence/browser/00_initial_login_screen.png');

  ws.close();
  browser.kill();
  console.log('Browser closed cleanly.');
}

run().catch(err => {
  console.error('Test error:', err);
  browser.kill();
  process.exit(1);
});
