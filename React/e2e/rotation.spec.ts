import { expect, test } from '@playwright/test';
import type { Page } from '@playwright/test';

async function recordPlayback(page: Page, bursts: { at: number; keys: string[] }[], pieceId = 'A') {
  const result = await page.evaluate(async ({ bursts, pieceId }) => {
    const start = performance.now();
    const samples: { at: number; angle: number; id: number | null; logical: number }[] = [];
    const steps: { at: number; id: number | null; from: number; to: number }[] = [];
    const completed: { id: number; angle: number }[] = [];
    let previousId: number | null = null;
    const currentButton = () => document.querySelector<HTMLElement>(`[data-piece="${pieceId}"] .piece-hit-area`)!;
    const sample = () => {
      const button = currentButton();
      const id = button.dataset.rotationId === undefined ? null : Number(button.dataset.rotationId);
      const angle = Number(button.style.transform.match(/rotate\(([-\d.]+)deg\)/)?.[1] ?? 0);
      if (id !== previousId) {
        if (previousId !== null) completed.push({ id: previousId, angle });
        steps.push({ at: performance.now() - start, id,
          from: Number(button.dataset.rotationFrom), to: Number(button.dataset.rotationTo) });
        previousId = id;
      }
      if (samples.at(-1)?.angle !== angle || samples.at(-1)?.id !== id)
        samples.push({ at: performance.now() - start, angle, id,
          logical: Number(button.closest<HTMLElement>('.board-piece')!.dataset.turns) });
    };
    const observe = new MutationObserver(sample);
    observe.observe(document.querySelector(`[data-piece="${pieceId}"]`)!,
      { attributes: true, attributeFilter: ['style', 'data-rotation-id'], childList: true, subtree: true });
    const send = (keys: string[]) => keys.forEach((key) =>
      window.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true })));
    let remainingBursts = bursts.length;
    const deliver = (keys: string[]) => { send(keys); remainingBursts--; };
    const timers = bursts.map((burst) => burst.at === 0 ? (deliver(burst.keys), 0) : setTimeout(() => deliver(burst.keys), burst.at));
    while (performance.now() - start < 10_000) {
      await new Promise(requestAnimationFrame);
      if (remainingBursts === 0 && steps.length > 0 && steps.at(-1)!.id === null) break;
    }
    observe.disconnect();
    timers.forEach(clearTimeout);
    return { samples, steps, completed };
  }, { bursts, pieceId });
  await test.info().attach('rotation-playback', {
    body: JSON.stringify(result), contentType: 'application/json',
  });
  return result;
}

function expectQuarterTurnTiming(
  steps: { at: number; id: number | null; from: number; to: number }[],
  samples: { at: number; angle: number; id: number | null }[],
) {
  expect(steps.at(-1)?.id).toBeNull();
  for (const step of steps.filter((s) => s.id !== null)) {
    // Measure actual linear angular velocity. DOM mount-to-unmount time also
    // includes React commits and Motion startup, so it is not the tween duration.
    const moving = samples.filter((s) => {
      const progress = (s.angle - step.from * 90) / ((step.to - step.from) * 90);
      return s.id === step.id && progress > .1 && progress < .9;
    });
    expect(moving.length).toBeGreaterThanOrEqual(2);
    const first = moving[0];
    const last = moving.at(-1)!;
    const duration = 90 * (last.at - first.at) / Math.abs(last.angle - first.angle);
    expect(duration).toBeGreaterThanOrEqual(170);
    expect(duration).toBeLessThanOrEqual(235);
  }
}

test('right then left in one React batch displays both complete quarter turns', async ({ page }) => {
  await page.goto('/');
  await page.keyboard.press('1');
  await expect(page.locator('[data-piece="A"]')).toHaveClass(/is-held/);
  const samples = await page.evaluate(async () => {
    const result: number[] = [];
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'r', bubbles: true }));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'q', bubbles: true }));
    const start = performance.now();
    while (performance.now() - start < 700) {
      await new Promise(requestAnimationFrame);
      const button = document.querySelector<HTMLElement>('[data-piece="A"] .piece-hit-area')!;
      result.push(Number(button.style.transform.match(/rotate\(([-\d.]+)deg\)/)?.[1] ?? 0));
    }
    return result;
  });
  expect(Math.max(...samples)).toBeGreaterThan(85);
  expect(samples.at(-1)).toBeCloseTo(0, 2);
  await expect(page.locator('[data-piece="A"]')).toHaveAttribute('data-turns', '0');
});

test('each direction runs for 0.2s and new input during playback never restarts the active turn', async ({ page }) => {
  await page.goto('/');
  await page.keyboard.press('1');
  await expect(page.locator('[data-piece="A"]')).toHaveClass(/is-held/);
  const { samples, steps } = await recordPlayback(page,
    [{ at: 0, keys: ['r'] }, { at: 80, keys: ['q', 'r'] }]);
  expect(steps.filter((s) => s.id !== null).map((s) => [s.from, s.to])).toEqual([[0, 1], [1, 0], [0, 1]]);
  expectQuarterTurnTiming(steps, samples);
  expect(samples.some((s) => s.id === 0 && s.at > 100 && s.logical === 1)).toBe(true);
  expect(Math.max(...samples.filter((s) => s.id === 0).map((s) => s.angle))).toBeGreaterThan(80);
  expect(Math.min(...samples.filter((s) => s.id === 1).map((s) => s.angle))).toBeLessThan(10);
  expect(samples.at(-1)?.angle).toBeCloseTo(90, 2);
  await expect(page.locator('[data-piece="A"]')).toHaveAttribute('data-turns', '1');
});

test('a negative mixed burst displays every step across a full rotation and converges', async ({ page }) => {
  await page.goto('/');
  await page.keyboard.press('1');
  await expect(page.locator('[data-piece="A"]')).toHaveClass(/is-held/);
  const keys = ['q', 'q', 'q', 'q', 'q', 'r', 'q', 'r', 'r', 'r', 'r', 'r'];
  const { samples, steps, completed } = await recordPlayback(page, [{ at: 0, keys }]);
  let turn = 0;
  const expected = keys.map((key) => { const from = turn; turn += key === 'r' ? 1 : -1; return [from, turn]; });
  expect(steps.filter((s) => s.id !== null).map((s) => [s.from, s.to])).toEqual(expected);
  expectQuarterTurnTiming(steps, samples);
  expect(completed).toEqual(expected.map(([, to], id) => ({ id, angle: to * 90 })));
  // Observe actual DOM angles for every step, rather than only checking the queue.
  for (let id = 0; id < keys.length; id++) {
    const angles = samples.filter((s) => s.id === id).map((s) => s.angle);
    const [from, to] = expected[id].map((t) => t * 90);
    expect(angles.length).toBeGreaterThan(4);
    expect(Math.abs(angles[0] - from)).toBeLessThan(20);
    expect((angles.at(-1)! - angles[0]) * Math.sign(to - from)).toBeGreaterThan(45);
  }
  expect(Math.min(...samples.map((s) => s.angle))).toBeLessThan(-440);
  expect(samples.at(-1)?.angle).toBeCloseTo(turn * 90, 2);
  await expect(page.locator('[data-piece="A"]')).toHaveAttribute('data-turns', String(turn));
});

test('spin remains enabled while MOVE, SET and RETURN wait for the full rotation queue', async ({ page }) => {
  await page.goto('/');
  await page.keyboard.press('1');
  await expect(page.locator('[data-piece="A"]')).toHaveClass(/is-held/);
  await page.evaluate(() => {
    for (const key of ['r', 'q', 'r', 'q', 'ArrowUp', ' ', 'Escape'])
      window.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));
  });
  await expect(page.getByRole('button', { name: '左に90度回転', exact: true })).toBeEnabled();
  await expect(page.getByRole('button', { name: '右に90度回転', exact: true })).toBeEnabled();
  await expect(page.getByRole('button', { name: /^Set / })).toBeDisabled();
  await expect(page.getByRole('button', { name: /持ちピースに戻す/ })).toBeDisabled();
  await expect(page.getByRole('button', { name: '上に移動', exact: true })).toBeDisabled();
  await expect(page.locator('[data-piece="A"]')).toHaveAttribute('data-y', '1');
  await expect(page.locator('[data-piece="A"]')).toHaveClass(/is-held/);
  await expect(page.getByRole('button', { name: /^Set / })).toBeEnabled();
  await page.keyboard.press('Space');
  await expect(page.locator('[data-piece="A"]')).not.toHaveClass(/is-held/);
});

test('wall corrections follow each queued target and logical and visual cells finally match', async ({ page }) => {
  await page.goto('/');
  await page.keyboard.press('8');
  for (let i = 0; i < 5; i++) await page.keyboard.press('ArrowUp');
  await expect(page.locator('[data-piece="H"]')).toHaveAttribute('data-y', '5');
  const { steps, samples } = await recordPlayback(page, [{ at: 0, keys: ['r', 'q'] }], 'H');
  expect(steps.filter((s) => s.id !== null).map((s) => [s.from, s.to])).toEqual([[0, 1], [1, 0]]);
  expect(samples.at(-1)?.angle).toBeCloseTo(0, 2);
  await expect(page.locator('[data-piece="H"]')).toHaveAttribute('data-y', '2');
  const occupied = await page.locator('[data-piece="H"]').evaluate((element) => {
    const board = element.closest('.puzzle-board')!.getBoundingClientRect();
    const cellSize = board.width / 6;
    return [...element.querySelectorAll('rect')].map((cell) => {
      const rect = cell.getBoundingClientRect();
      return [Math.round((rect.x + rect.width / 2 - board.x) / cellSize - .5),
        Math.round(6 - (rect.y + rect.height / 2 - board.y) / cellSize - .5)];
    });
  });
  expect(occupied).toEqual([[1, 2], [2, 2], [3, 2], [4, 2]]);
});

test('Restart interrupts an active turn, discards pending steps and isolates the new session', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/');
  await page.keyboard.press('1');
  await expect(page.locator('[data-piece="A"]')).toHaveClass(/is-held/);
  await page.evaluate(() => {
    for (let i = 0; i < 6; i++) window.dispatchEvent(new KeyboardEvent('keydown', { key: 'r', bubbles: true }));
  });
  await expect.poll(() => page.locator('[data-piece="A"] .piece-hit-area').evaluate((el) =>
    Number((el as HTMLElement).style.transform.match(/rotate\(([-\d.]+)deg\)/)?.[1] ?? 0))).toBeGreaterThan(5);
  await page.evaluate(() => {
    (window as unknown as { oldRotation: HTMLElement }).oldRotation = document.querySelector('[data-rotation-id]')!;
  });
  await page.getByRole('button', { name: '↻ やり直す' }).click();
  await expect(page.locator('.board-piece')).toHaveCount(0);
  const stopped = await page.evaluate(() => (window as unknown as { oldRotation: HTMLElement }).oldRotation.style.transform);
  await page.keyboard.press('1');
  await page.keyboard.press('q');
  await expect(page.getByRole('button', { name: /^Set / })).toBeEnabled();
  await page.waitForTimeout(1400);
  expect(await page.evaluate(() => (window as unknown as { oldRotation: HTMLElement }).oldRotation.style.transform)).toBe(stopped);
  await expect(page.locator('[data-piece="A"]')).toHaveAttribute('data-turns', '-1');
  await expect(page.locator('[data-piece="A"] [data-rotation-id]')).toHaveCount(0);
  expect(errors).toEqual([]);
});

test('Set and re-pick preserve the completed cumulative angle without replaying old steps', async ({ page }) => {
  await page.goto('/');
  await page.keyboard.press('1');
  for (let i = 0; i < 6; i++) await page.keyboard.press('q');
  await expect(page.getByRole('button', { name: /^Set / })).toBeEnabled();
  await page.keyboard.press('Space');
  await page.keyboard.press('1');
  await expect(page.locator('[data-piece="A"]')).toHaveAttribute('data-turns', '-6');
  await expect(page.locator('[data-piece="A"] [data-rotation-id]')).toHaveCount(0);
  expect(await page.locator('[data-piece="A"] .piece-hit-area').evaluate((el) =>
    Number((el as HTMLElement).style.transform.match(/rotate\(([-\d.]+)deg\)/)?.[1]))).toBe(-540);
});
