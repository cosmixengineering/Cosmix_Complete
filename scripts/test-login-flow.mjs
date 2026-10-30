import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const userDir = join(tmpdir(), 'cdp-user-' + Date.now());
const edgePath = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';

console.log('Launching Edge browser...');
const browser = spawn(edgePath, [
  '--remote-debugging-port=9222',
  '--headless=new',
  '--ignore-certificate-errors',
  '--disable-gpu',
  '--no-first-run',
  '--no-default-browser-check',
  `--user-data-dir=${userDir}`
], { stdio: 'ignore' });

async function sleep(ms) {
  return new Promise(r => setTimeout(r, ms));
}

async function run() {
  let version = null;
  for (let i = 0; i < 20; i++) {
    try {
      const res = await fetch('http://127.0.0.1:9222/json/version');
      if (res.ok) {
        version = await res.json();
        break;
      }
    } catch {
      await sleep(500);
    }
  }

  if (!version) {
    throw new Error('CDP not reachable on 9222');
  }

  console.log('Browser connected:', version.Browser);
  const target = await (await fetch('http://127.0.0.1:9222/json/new?https://localhost:5173/sign-in', { method: 'PUT' })).json();
  const ws = new WebSocket(target.webSocketDebuggerUrl);

  await new Promise((res, rej) => {
    ws.onopen = res;
    ws.onerror = rej;
  });

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
  await send('DOM.enable');

  console.log('Waiting for sign-in page to mount...');
  await sleep(4000);

  const dump = await send('Runtime.evaluate', {
    expression: 'document.body.innerHTML'
  });
  console.log('BODY HTML LENGTH:', dump.result.value?.length);
  console.log('BODY HTML PREVIEW:', dump.result.value?.substring(0, 400));

  // Fill in email and password
  const fillRes = await send('Runtime.evaluate', {
    expression: `(() => {
      const emailInput = document.querySelector('input[name="email"], input[type="email"]');
      const passInput = document.querySelector('input[name="password"], input[type="password"]');
      if (!emailInput || !passInput) return 'inputs_not_found';
      
      const nativeInputValueSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
      nativeInputValueSetter.call(emailInput, 'admin@example.invalid');
      emailInput.dispatchEvent(new Event('input', { bubbles: true }));
      emailInput.dispatchEvent(new Event('change', { bubbles: true }));

      nativeInputValueSetter.call(passInput, 'NLYHRfEtjUognTNLlURukM/Epgu0495W');
      passInput.dispatchEvent(new Event('input', { bubbles: true }));
      passInput.dispatchEvent(new Event('change', { bubbles: true }));

      const submitBtn = document.querySelector('button[type="submit"]');
      if (submitBtn) {
        submitBtn.click();
        return 'submitted';
      }
      return 'button_not_found';
    })()`
  });

  console.log('Submit initial credentials result:', fillRes.result.value);

  // Wait for OTP challenge
  console.log('Waiting for OTP challenge...');
  let otpReady = false;
  for (let i = 0; i < 20; i++) {
    await sleep(500);
    const checkChallenge = await send('Runtime.evaluate', {
      expression: `(() => {
        const text = document.body.innerText;
        return text.includes('Verify your identity') || text.includes('verification code') || text.includes('Show test OTP');
      })()`
    });
    if (checkChallenge.result.value) {
      otpReady = true;
      break;
    }
  }

  console.log('OTP challenge ready:', otpReady);

  if (otpReady) {
    // Click 'Show test OTP' if button exists, or fetch OTP via API
    const revealResult = await send('Runtime.evaluate', {
      expression: `(() => {
        const buttons = Array.from(document.querySelectorAll('button'));
        const otpBtn = buttons.find(b => b.innerText.includes('Show test OTP'));
        if (otpBtn) {
          otpBtn.click();
          return 'clicked_otp_btn';
        }
        return 'btn_not_found';
      })()`
    });
    console.log('OTP button action:', revealResult.result.value);

    // Wait for code to be populated or fetch from local-testing
    await sleep(1500);

    const submitOtpRes = await send('Runtime.evaluate', {
      expression: `(() => {
        const codeInput = document.querySelector('input[name="code"]');
        const submitBtn = document.querySelector('button[type="submit"]');
        return {
          codeValue: codeInput ? codeInput.value : null,
          hasSubmit: Boolean(submitBtn)
        };
      })()`,
      returnByValue: true
    });
    console.log('Code input status:', submitOtpRes.result.value);

    // If code input is not filled yet, fetch directly from local-testing endpoint
    if (!submitOtpRes.result.value?.codeValue) {
      const fillOtpDirectly = await send('Runtime.evaluate', {
        expression: `(async () => {
          try {
            const csrfRes = await fetch('/api/v1/auth/web/csrf');
            const csrf = await csrfRes.json();
            // find challenge id from DOM or network
            const localCheck = await (await fetch('/api/v1/auth/web/local-testing')).json();
            return 'checked local-testing: ' + JSON.stringify(localCheck);
          } catch (e) {
            return 'error: ' + e.message;
          }
        })()`,
        awaitPromise: true
      });
      console.log('Local testing check:', fillOtpDirectly.result.value);
    }

    // Click submit for OTP
    const clickSubmit = await send('Runtime.evaluate', {
      expression: `(() => {
        const submitBtn = document.querySelector('button[type="submit"]');
        if (submitBtn) {
          submitBtn.click();
          return 'submitted_otp';
        }
        return 'no_submit';
      })()`
    });
    console.log('Submitted OTP:', clickSubmit.result.value);

    await sleep(3000);
  }

  const currentUrl = await send('Runtime.evaluate', { expression: 'window.location.href' });
  console.log('Current URL after sign in:', currentUrl.result.value);

  const shot = await send('Page.captureScreenshot');
  mkdirSync('docs/acceptance/evidence/browser', { recursive: true });
  writeFileSync('docs/acceptance/evidence/browser/00_post_login_verification.png', Buffer.from(shot.data, 'base64'));
  console.log('Saved screenshot: docs/acceptance/evidence/browser/00_post_login_verification.png');

  ws.close();
  browser.kill();
  console.log('Browser done.');
}

run().catch(err => {
  console.error('Execution error:', err);
  browser.kill();
  process.exit(1);
});
