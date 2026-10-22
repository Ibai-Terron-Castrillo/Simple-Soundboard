const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright');

(async () => {
  const config = JSON.parse(await fs.readFile(path.join(__dirname, '../artifacts/smoke/current.json'), 'utf8'));
  const browser = await chromium.launch({ headless: true, ...(process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE ? { executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE } : {}) });
  const output = path.join(__dirname, '../artifacts/ui-tests');
  await fs.mkdir(output, { recursive: true });
  const errors = [];
  try {
    const context = await browser.newContext({ viewport: { width: 1280, height: 800 } });
    const page = await context.newPage();
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(config.baseUrl);
    await page.getByText('Connected', { exact: true }).waitFor();
    await page.locator('.sound').nth(2).waitFor();
    await page.getByRole('textbox', { name: 'Search sounds' }).fill('Ambient');
    await page.waitForFunction(() => document.querySelectorAll('.sound').length === 1);
    await page.getByRole('button', { name: 'Clear search', exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.sound').length === 3);
    assert.equal(await page.getByRole('textbox', { name: 'Search sounds' }).inputValue(), '');
    await page.getByRole('button', { name: 'Favorites', exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.sound').length === 1);
    await page.getByRole('button', { name: 'Favorites', exact: true }).click();
    await page.getByRole('combobox', { name: 'Sound bank' }).selectOption('live');
    await page.waitForFunction(() => document.querySelectorAll('.sound').length === 2);
    assert.equal(await page.locator('.sound-name').first().textContent(), 'Ambient');
    await page.getByRole('combobox', { name: 'Sound bank' }).selectOption('');
    await page.waitForFunction(() => document.querySelectorAll('.sound').length === 3);
    await page.locator('.sound').filter({ hasText: 'Corrupt' }).getByRole('button', { name: 'Play', exact: true }).click();
    await page.getByText('Could not start playback.', { exact: false }).waitFor();
    assert.equal(await page.locator('.session').count(), 0);

    const sounds = await (await context.request.get(config.baseUrl + '/api/sounds')).json();
    const sound = sounds.find(sound => sound.name === 'Ambient');
    const response = await context.request.post(config.baseUrl + '/api/play/' + sound.id);
    const hardwarePlayback = response.ok();
    if (hardwarePlayback) {
      await page.locator('.session').waitFor();
      await page.getByRole('button', { name: 'Pause all', exact: true }).click();
      await page.getByRole('button', { name: 'Resume all', exact: true }).waitFor();
      const status = await (await context.request.get(config.baseUrl + '/api/status')).json();
      const session = status.playback.sessions[0];
      assert.equal(session.isPaused, true);
      await context.request.post(`${config.baseUrl}/api/sessions/${session.id}/seek`, { data: { seconds: 15 } });
      await page.waitForFunction(() => document.querySelector('.session-time').textContent.startsWith('0:15'));
      await page.getByRole('button', { name: 'Resume all', exact: true }).click();
      await page.getByRole('button', { name: 'Pause all', exact: true }).waitFor();
      const secondPage = await context.newPage();
      await secondPage.goto(config.baseUrl);
      await secondPage.locator('.session').waitFor();
      await context.request.post(config.baseUrl + '/api/pause');
      await secondPage.getByRole('button', { name: 'Resume all', exact: true }).waitFor();
      await page.getByRole('button', { name: 'Resume all', exact: true }).waitFor();
      await secondPage.close();
    }
    for (const viewport of [{ width: 1280, height: 800 }, { width: 1024, height: 768 }, { width: 390, height: 844 }, { width: 320, height: 640 }]) {
      await page.setViewportSize(viewport);
      for (const theme of ['dark', 'light']) {
        const current = await page.locator('html').getAttribute('data-theme');
        if (current !== theme) await page.getByRole('button', { name: 'Toggle color theme' }).click();
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'Horizontal overflow');
        await page.screenshot({ path: path.join(output, `web-${theme}-${viewport.width}.png`), fullPage: true, animations: 'disabled' });
      }
    }
    if (hardwarePlayback) {
      await page.getByRole('button', { name: 'Stop all', exact: true }).click();
      await page.waitForFunction(() => document.querySelectorAll('.session').length === 0);
    }
    assert.deepEqual(errors, []);
    console.log(JSON.stringify({ passed: true, hardwarePlayback, viewports: 4, themes: 2, pageErrors: errors }, null, 2));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
