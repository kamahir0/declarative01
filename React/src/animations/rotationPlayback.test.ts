import { describe, expect, it } from 'vitest';
import { createInitialState, gameReducer } from '../game/reducer';
import { puzzleData } from '../game/puzzleData';
import type { GameAction } from '../game/types';
import { createRotationPlaybackState, reduceRotationPlayback, rotationPlaybackReducer } from './rotationPlayback';

const pickA = () => rotationPlaybackReducer(createRotationPlaybackState(), { type: 'PICK_UP', pieceId: 'A' });

describe('ordered rotation presentation', () => {
  it('keeps right → left even when the logical cumulative turn returns to zero', () => {
    const initial = pickA();
    const right = rotationPlaybackReducer(initial, { type: 'SPIN', delta: 1 });
    const left = rotationPlaybackReducer(right, { type: 'SPIN', delta: -1 });
    expect(left.game.holding?.turns).toBe(0);
    expect(left.rotations.map((s) => [s.from.turns, s.to.turns])).toEqual([[0, 1], [1, 0]]);
    expect(initial.rotations).toEqual([]);
    expect(initial.game.holding?.turns).toBe(0);
  });
  it('accepts more input without changing the active step or replaying its start', () => {
    const active = rotationPlaybackReducer(pickA(), { type: 'SPIN', delta: 1 });
    const appended = rotationPlaybackReducer(active, { type: 'SPIN', delta: 1 });
    expect(appended.rotations[0]).toBe(active.rotations[0]);
    expect(appended.rotations).toHaveLength(2);
    expect(appended.game.holding?.turns).toBe(2);
  });
  it('retains every signed step across several full turns, then drains to the logical result', () => {
    let state = pickA();
    const deltas = [-1, -1, -1, -1, -1, 1, -1, 1, 1, 1, 1, 1] as const;
    for (const delta of deltas) state = rotationPlaybackReducer(state, { type: 'SPIN', delta });
    expect(state.rotations.map((s) => s.to.turns - s.from.turns)).toEqual(deltas);
    expect(state.rotations.map((s) => s.id)).toEqual(deltas.map((_, i) => i));
    const game = state.game;
    const finalPose = state.rotations.at(-1)!.to;
    for (let i = 0; i < deltas.length; i++) state = rotationPlaybackReducer(state, { type: 'ROTATION_FINISHED', id: i });
    expect(state.rotations).toEqual([]);
    expect(state.game).toBe(game);
    expect(finalPose).toEqual(state.game.holding);
    expect(state.game.holding?.turns).toBe(deltas.reduce((sum, delta) => sum + delta, 0));
  });
  it('stores each clamped origin, since reverse rotation does not undo a boundary correction', () => {
    let game = gameReducer(createInitialState(), { type: 'PICK_UP', pieceId: 'H' });
    for (let i = 0; i < 5; i++) game = gameReducer(game, { type: 'MOVE', dx: 0, dy: 1 });
    let state = createRotationPlaybackState(game);
    state = rotationPlaybackReducer(state, { type: 'SPIN', delta: 1 });
    state = rotationPlaybackReducer(state, { type: 'SPIN', delta: -1 });
    expect(state.rotations.map((s) => [s.from.y, s.to.y])).toEqual([[5, 2], [2, 2]]);
    expect(state.game.holding).toMatchObject({ y: 2, turns: 0 });
  });
  it('only dequeues the current ID; stale, duplicate, and out-of-order completions do nothing', () => {
    const first = rotationPlaybackReducer(pickA(), { type: 'SPIN', delta: 1 });
    const second = rotationPlaybackReducer(first, { type: 'SPIN', delta: -1 });
    expect(rotationPlaybackReducer(second, { type: 'ROTATION_FINISHED', id: 1 })).toBe(second);
    expect(rotationPlaybackReducer(second, { type: 'ROTATION_FINISHED', id: 99 })).toBe(second);
    const done = rotationPlaybackReducer(second, { type: 'ROTATION_FINISHED', id: 0 });
    expect(done.rotations.map((s) => s.id)).toEqual([1]);
    expect(rotationPlaybackReducer(done, { type: 'ROTATION_FINISHED', id: 0 })).toBe(done);
  });
  it('does not reuse completed IDs when another turn starts later', () => {
    let state = rotationPlaybackReducer(pickA(), { type: 'SPIN', delta: 1 });
    state = rotationPlaybackReducer(state, { type: 'ROTATION_FINISHED', id: 0 });
    state = rotationPlaybackReducer(state, { type: 'SPIN', delta: -1 });
    expect(state.rotations[0].id).toBe(1);
    expect(rotationPlaybackReducer(state, { type: 'ROTATION_FINISHED', id: 0 })).toBe(state);
  });
  it('does not enqueue rejected operations or spins without Holding', () => {
    const initial = createRotationPlaybackState();
    expect(rotationPlaybackReducer(initial, { type: 'SPIN', delta: 1 })).toBe(initial);
    const definition = { ...puzzleData, width: 1, height: 4, pieces: [puzzleData.pieces[7]] };
    const held = reduceRotationPlayback(createRotationPlaybackState(createInitialState(definition)),
      { type: 'PICK_UP', pieceId: 'H' }, definition);
    expect(reduceRotationPlayback(held, { type: 'SPIN', delta: 1 }, definition)).toBe(held);
    expect(held.rotations).toEqual([]);
  });
  it('waits for playback before MOVE, SET, RETURN and submission, then allows interaction', () => {
    const active = rotationPlaybackReducer(pickA(), { type: 'SPIN', delta: 1 });
    const blocked: GameAction[] = [{ type: 'MOVE', dx: 1, dy: 0 }, { type: 'SET' },
      { type: 'RETURN_TO_STOCK' }, { type: 'SUBMIT_STARTED' }, { type: 'PICK_UP', pieceId: 'B' }];
    for (const action of blocked) expect(rotationPlaybackReducer(active, action)).toBe(active);
    const idle = rotationPlaybackReducer(active, { type: 'ROTATION_FINISHED', id: 0 });
    expect(rotationPlaybackReducer(idle, { type: 'MOVE', dx: 1, dy: 0 }).game.holding?.x).toBe(3);
    expect(rotationPlaybackReducer(idle, { type: 'SET' }).game.pieces.A.turns).toBe(1);
    expect(rotationPlaybackReducer(idle, { type: 'RETURN_TO_STOCK' }).game.holding).toBeNull();
  });
  it('holds onto the original committed puzzle until playback and Set both finish', () => {
    const held = pickA();
    const active = rotationPlaybackReducer(held, { type: 'SPIN', delta: -1 });
    expect(active.game.pieces).toBe(held.game.pieces);
    expect(active.game.pieces.A).toEqual({ location: { type: 'stock' }, turns: 0 });
    const idle = rotationPlaybackReducer(active, { type: 'ROTATION_FINISHED', id: 0 });
    const set = rotationPlaybackReducer(idle, { type: 'SET' });
    expect(set.game.pieces.A.turns).toBe(-1);
    expect(set.rotations).toEqual([]);
  });
  it('a fresh Restart session has no work, and old callbacks cannot advance it', () => {
    let old = pickA();
    for (let i = 0; i < 4; i++) old = rotationPlaybackReducer(old, { type: 'SPIN', delta: 1 });
    const fresh = createRotationPlaybackState();
    expect(fresh.rotations).toEqual([]);
    expect(fresh.game).toEqual(createInitialState());
    expect(rotationPlaybackReducer(fresh, { type: 'ROTATION_FINISHED', id: old.rotations[0].id })).toBe(fresh);
  });
});
