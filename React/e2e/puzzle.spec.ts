import { expect, test } from '@playwright/test';
import type { Page } from '@playwright/test';
import { sampleSolution } from '../src/game/testFixtures';
import { puzzleData } from '../src/game/puzzleData';
import { getOccupiedCells } from '../src/game/rules';

async function solve(page: Page) {
  for (const target of sampleSolution) {
    const index = puzzleData.pieces.findIndex((p) => p.id === target.pieceId);
    await page.keyboard.press(String(index + 1));
    for (let i = 0; i < target.turns; i++) await page.keyboard.press('r');
    await expect(page.getByRole('button', { name: /^Set / })).toBeEnabled();
    const piece = page.locator(`[data-piece="${target.pieceId}"]`);
    const x = Number(await piece.getAttribute('data-x'));
    const y = Number(await piece.getAttribute('data-y'));
    for (let i = 0; i < Math.abs(x - target.x); i++) await page.keyboard.press(x < target.x ? 'ArrowRight' : 'ArrowLeft');
    for (let i = 0; i < Math.abs(y - target.y); i++) await page.keyboard.press(y < target.y ? 'ArrowUp' : 'ArrowDown');
    await page.keyboard.press('Space');
  }
  await expect(page.getByRole('button', { name: /^Submit/ })).toBeEnabled();
}

test('mouse + keyboard, continuous rotation, overlaps, Return and restart', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/');
  await expect(page.getByRole('button', { name: /^Submit/ })).toBeDisabled();
  await page.getByRole('button', { name: 'ピース A を持ち上げる', exact: true }).click();
  await expect(page.locator('[data-piece="A"]')).toHaveAttribute('data-x', '2');
  for (let i = 0; i < 5; i++) await page.keyboard.press('q');
  await expect(page.locator('[data-piece="A"]')).toHaveAttribute('data-turns', '-5');
  await expect(page.getByRole('button', { name: /^Set / })).toBeEnabled();
  await page.keyboard.press('Space');
  await expect(page.locator('[data-piece="A"]')).not.toHaveClass(/is-held/);
  await page.locator('[data-piece="A"] rect').first().click();
  await expect(page.locator('[data-piece="A"]')).toHaveClass(/is-held/);
  await page.keyboard.press('Space');
  await page.keyboard.press('9');
  await page.keyboard.press('Space');
  await expect(page.locator('.overlap-layer g').first()).toBeVisible();
  await page.keyboard.press('1');
  await page.keyboard.press('Escape');
  await expect(page.locator('[data-piece="A"]')).toHaveCount(0);
  await page.getByRole('button', { name: '↻ やり直す' }).click();
  await expect(page.locator('.board-piece')).toHaveCount(0);
  await expect(page.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '0');
  expect(errors).toEqual([]);
});

test('all rotations visually occupy the exact logical cells, including negative turns', async ({ page }) => {
  await page.goto('/');
  for (let index = 0; index < puzzleData.pieces.length; index++) {
    const definition = puzzleData.pieces[index];
    await page.keyboard.press(String(index + 1));
    for (let turn = 0; turn >= -4; turn--) {
      const piece = page.locator(`[data-piece="${definition.id}"]`);
      await expect(piece).toHaveAttribute('data-turns', String(turn));
      await expect(piece.locator('[data-rotation-id]')).toHaveCount(0);
      const x = Number(await piece.getAttribute('data-x'));
      const y = Number(await piece.getAttribute('data-y'));
      const targets = getOccupiedCells(definition, { x, y }, turn).map((cell) => `${cell.x},${cell.y}`).sort();
      await expect.poll(async () => piece.evaluate((element) => {
        const board = element.closest('.puzzle-board')!.getBoundingClientRect();
        const size = board.width / 6;
        return [...element.querySelectorAll('rect')].map((cell) => {
          const box = cell.getBoundingClientRect();
          const cx = (box.x + box.width / 2 - board.x) / size - 0.5;
          const cy = 6 - (box.y + box.height / 2 - board.y) / size - 0.5;
          // Check actual centers; rounding alone would accept badly offset cells.
          if (Math.abs(cx - Math.round(cx)) > .01 || Math.abs(cy - Math.round(cy)) > .01) return 'animating';
          return `${Math.round(cx)},${Math.round(cy)}`;
        }).sort();
      })).toEqual(targets);
      if (turn > -4) await page.keyboard.press('q');
    }
    await page.keyboard.press('Escape');
  }
});

test('Unity solution submits once, locks input, glows in order, and finishes', async ({ page }) => {
  await page.goto('/');
  await solve(page);
  await expect(page.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '36');
  await page.keyboard.press('1');
  await expect(page.getByRole('button', { name: /^Submit/ })).toBeDisabled();
  await page.keyboard.press('Space');
  await page.evaluate(() => {
    const observed: string[] = [];
    (window as unknown as { glowOrder: string[] }).glowOrder = observed;
    const sample = () => {
      for (const element of document.querySelectorAll<HTMLElement>('.board-piece')) {
        const id = element.dataset.piece!;
        const filter = getComputedStyle(element.querySelector('.piece-glow')!).filter;
        const brightness = Number(filter.match(/brightness\(([\d.]+)\)/)?.[1] ?? 1);
        if (brightness > 1.02 && !observed.includes(id)) observed.push(id);
      }
      if (!document.querySelector('.finished-card')) requestAnimationFrame(sample);
    };
    requestAnimationFrame(sample);
  });
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: /^Submitting/ })).toBeDisabled();
  await page.keyboard.press('2');
  await expect(page.locator('.is-held')).toHaveCount(0);
  await expect(page.getByRole('button', { name: /^Celebrating/ })).toBeDisabled();
  await expect.poll(() => page.locator('[data-piece="A"] .piece-glow').evaluate((el) =>
    getComputedStyle(el).filter)).not.toBe('brightness(1) drop-shadow(rgba(255, 255, 255, 0) 0px 0px 0px)');
  await expect(page.getByRole('button', { name: /^Finished/ })).toBeDisabled();
  expect(await page.evaluate(() => (window as unknown as { glowOrder: string[] }).glowOrder))
    .toEqual(puzzleData.pieces.map((piece) => piece.id));
  await expect(page.getByText('ぴったり、完成。')).toBeVisible();
  await page.screenshot({ path: test.info().outputPath('finished.png'), fullPage: true });
});

test('rejected or failed async requests keep the solution and allow retry; double submit is guarded', async ({ page }) => {
  await page.route('**/src/services/submitPuzzle.ts', (route) => route.fulfill({
    contentType: 'application/javascript',
    body: `let count = 0;
      export const submitPuzzle = async (_, signal) => {
        count++; window.testSubmissionCount = count;
        await new Promise(resolve => setTimeout(resolve, 200));
        if (signal.aborted) throw new DOMException('Aborted', 'AbortError');
        if (count === 1) throw new Error('Network failure');
        return { accepted: count > 2 };
      };`,
  }));
  await page.goto('/');
  await solve(page);
  await page.keyboard.press('Enter');
  await page.keyboard.press('Enter');
  await expect(page.getByRole('alert')).toContainText('提出に失敗しました');
  await expect(page.getByRole('button', { name: /^Submit/ })).toBeEnabled();
  expect(await page.evaluate(() => (window as unknown as { testSubmissionCount: number }).testSubmissionCount)).toBe(1);
  await page.keyboard.press('Enter');
  await expect(page.getByRole('alert')).toContainText('提出を受け付けられませんでした');
  await expect(page.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '36');
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: /^Finished/ })).toBeDisabled();
});

test('restart cancels a pending submission and stale results do not affect the new session', async ({ page }) => {
  await page.route('**/src/services/submitPuzzle.ts', (route) => route.fulfill({
    contentType: 'application/javascript',
    body: `export const submitPuzzle = async (_, signal) => {
      signal.addEventListener('abort', () => { window.testAborted = true; }, { once: true });
      await new Promise(resolve => setTimeout(resolve, 400));
      return { accepted: true };
    };`,
  }));
  await page.goto('/');
  await solve(page);
  await page.keyboard.press('Enter');
  await page.getByRole('button', { name: '↻ やり直す' }).click();
  expect(await page.evaluate(() => (window as unknown as { testAborted: boolean }).testAborted)).toBe(true);
  await expect(page.getByRole('button', { name: /^Submit/ })).toBeDisabled();
  await expect(page.locator('.board-piece')).toHaveCount(0);
  await page.waitForTimeout(450);
  await expect(page.getByRole('button', { name: /^Submit/ })).toBeDisabled();
  await expect(page.locator('.finished-card')).toHaveCount(0);
});

test('Tab focus supports native button activation', async ({ page }) => {
  await page.goto('/');
  await page.keyboard.press('Tab');
  await page.keyboard.press('Tab');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('button', { name: 'ピース A を持ち上げる', exact: true })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.locator('[data-piece="A"]')).toHaveClass(/is-held/);
  await page.keyboard.press('Tab');
  await expect(page.getByRole('button', { name: '上に移動', exact: true })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.locator('[data-piece="A"]')).toHaveAttribute('data-y', '2');
});

test('mobile touch controls and reduced motion finish without horizontal overflow', async ({ browser }) => {
  const context = await browser.newContext({ viewport: { width: 375, height: 812 }, hasTouch: true, isMobile: true, reducedMotion: 'reduce' });
  const page = await context.newPage();
  await page.goto('/');
  await expect(page.locator('h1')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(375);
  await page.screenshot({ path: test.info().outputPath('mobile.png'), fullPage: true });
  await page.getByRole('button', { name: 'ピース A を持ち上げる', exact: true }).tap();
  await page.getByRole('button', { name: '左に90度回転', exact: true }).tap();
  await page.getByRole('button', { name: '上に移動', exact: true }).tap();
  await page.getByRole('button', { name: /^Set / }).tap();
  await expect(page.locator('[data-piece="A"]')).not.toHaveClass(/is-held/);
  await page.getByRole('button', { name: '↻ やり直す' }).tap();
  await page.locator('body').click({ position: { x: 2, y: 2 } });
  await solve(page);
  await page.getByRole('button', { name: /^Submit/ }).tap();
  await expect(page.getByRole('button', { name: /^Finished/ })).toBeDisabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(375);
  await context.close();
});

test('desktop initial screen', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto('/');
  await expect(page.locator('.stock-piece')).toHaveCount(9);
  await page.screenshot({ path: test.info().outputPath('initial.png'), fullPage: true });
});
