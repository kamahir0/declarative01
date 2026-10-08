import { afterEach, describe, expect, it, vi } from 'vitest';
import { completedState } from '../game/testFixtures';
import { createInitialState } from '../game/reducer';
import { submitPuzzle } from './submitPuzzle';

afterEach(() => vi.useRealTimers());

describe('local asynchronous submission', () => {
  it('waits before accepting a complete snapshot', async () => {
    vi.useFakeTimers();
    const resolved = vi.fn();
    const promise = submitPuzzle(completedState(), new AbortController().signal).then(resolved);
    await vi.advanceTimersByTimeAsync(649);
    expect(resolved).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    await promise;
    expect(resolved).toHaveBeenCalledWith({ accepted: true });
  });
  it('rejects an incomplete submitted puzzle', async () => {
    vi.useFakeTimers();
    const promise = submitPuzzle(createInitialState(), new AbortController().signal);
    await vi.runAllTimersAsync();
    expect(await promise).toEqual({ accepted: false });
  });
  it('aborts an in-flight request and removes its timer', async () => {
    vi.useFakeTimers();
    const controller = new AbortController();
    const assertion = expect(submitPuzzle(completedState(), controller.signal))
      .rejects.toMatchObject({ name: 'AbortError' });
    controller.abort();
    await assertion;
    expect(vi.getTimerCount()).toBe(0);
  });
  it('does not schedule work with an already aborted signal', async () => {
    vi.useFakeTimers();
    const controller = new AbortController();
    controller.abort();
    await expect(submitPuzzle(completedState(), controller.signal)).rejects.toMatchObject({ name: 'AbortError' });
    expect(vi.getTimerCount()).toBe(0);
  });
});
