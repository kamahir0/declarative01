import { describe, expect, it } from 'vitest';
import { puzzleData } from './puzzleData';
import { createInitialState, gameReducer, reduceGame } from './reducer';
import { getOccupiedCells, isInside } from './rules';
import { completedState } from './testFixtures';
import type { GameAction } from './types';

const pickA = () => gameReducer(createInitialState(), { type: 'PICK_UP', pieceId: 'A' });

describe('interaction transitions', () => {
  it('picks stock near the center and permits only one held piece', () => {
    const state = pickA();
    expect(state.holding).toEqual({ pieceId: 'A', x: 2, y: 1, turns: 0 });
    expect(state.pieces.A.location.type).toBe('stock');
    expect(gameReducer(state, { type: 'PICK_UP', pieceId: 'B' })).toBe(state);
  });
  it('ignores unknown pieces and operations without Holding', () => {
    const state = createInitialState();
    expect(gameReducer(state, { type: 'PICK_UP', pieceId: 'unknown' })).toBe(state);
    for (const action of [{ type: 'MOVE', dx: 1, dy: 0 }, { type: 'SPIN', delta: 1 },
      { type: 'SET' }, { type: 'RETURN_TO_STOCK' }] satisfies GameAction[]) {
      expect(gameReducer(state, action)).toBe(state);
    }
  });
  it('only accepts one orthogonal cell per move', () => {
    const state = pickA();
    for (const [dx, dy] of [[0, 0], [1, 1], [2, 0], [0.5, 0.5], [NaN, 1]]) {
      expect(gameReducer(state, { type: 'MOVE', dx, dy })).toBe(state);
    }
  });
  it('keeps cumulative left and right rotations including more than a full turn', () => {
    let state = pickA();
    for (let i = 0; i < 6; i++) state = gameReducer(state, { type: 'SPIN', delta: -1 });
    expect(state.holding?.turns).toBe(-6);
    for (let i = 0; i < 12; i++) state = gameReducer(state, { type: 'SPIN', delta: 1 });
    expect(state.holding?.turns).toBe(6);
    state = gameReducer(state, { type: 'SET' });
    expect(state.pieces.A.turns).toBe(6);
    state = gameReducer(state, { type: 'PICK_UP', pieceId: 'A' });
    expect(state.holding?.turns).toBe(6);
  });
  it('fits rotations at board edges and ignores moves beyond the edge', () => {
    let state = gameReducer(createInitialState(), { type: 'PICK_UP', pieceId: 'H' });
    for (let i = 0; i < 6; i++) state = gameReducer(state, { type: 'MOVE', dx: 0, dy: 1 });
    expect(state.holding?.y).toBe(5);
    expect(gameReducer(state, { type: 'MOVE', dx: 0, dy: 1 })).toBe(state);
    state = gameReducer(state, { type: 'SPIN', delta: 1 });
    expect(state.holding?.y).toBe(2);
    expect(getOccupiedCells(puzzleData.pieces[7], state.holding!, state.holding!.turns)
      .every((cell) => isInside(puzzleData, cell))).toBe(true);
  });
  it('leaves committed position and rotation untouched until Set', () => {
    const before = completedState();
    let state = gameReducer(before, { type: 'PICK_UP', pieceId: 'A' });
    expect(state.holding).toEqual({ pieceId: 'A', x: 0, y: 0, turns: 0 });
    state = gameReducer(state, { type: 'SPIN', delta: -1 });
    state = gameReducer(state, { type: 'MOVE', dx: 1, dy: 0 });
    expect(state.pieces).toBe(before.pieces);
    const confirmed = gameReducer(state, { type: 'SET' });
    expect(confirmed.pieces.A).toEqual({ location: { type: 'board', x: 1, y: 0 }, turns: -1 });
    expect(confirmed.holding).toBeNull();
    expect(before.pieces.A).toEqual({ location: { type: 'board', x: 0, y: 0 }, turns: 0 });
  });
  it('permits overlapping placements', () => {
    let state = gameReducer(pickA(), { type: 'SET' });
    state = gameReducer(state, { type: 'PICK_UP', pieceId: 'I' });
    state = gameReducer(state, { type: 'SET' });
    expect(state.pieces.A.location).toEqual(state.pieces.I.location);
  });
  it('returns a board piece to stock and preserves its cumulative orientation', () => {
    let state = gameReducer(completedState(), { type: 'PICK_UP', pieceId: 'A' });
    state = gameReducer(state, { type: 'SPIN', delta: -1 });
    state = gameReducer(state, { type: 'RETURN_TO_STOCK' });
    expect(state.pieces.A).toEqual({ location: { type: 'stock' }, turns: -1 });
    expect(state.holding).toBeNull();
    state = gameReducer(state, { type: 'PICK_UP', pieceId: 'A' });
    expect(state.holding?.turns).toBe(-1);
  });
  it('selects a fitting stock orientation and rejects spins that cannot fit', () => {
    const definition = { ...puzzleData, width: 1, height: 4, pieces: [puzzleData.pieces[7]] };
    const state = reduceGame(createInitialState(definition), { type: 'PICK_UP', pieceId: 'H' }, definition);
    expect(state.holding).toEqual({ pieceId: 'H', x: 0, y: 0, turns: 1 });
    expect(reduceGame(state, { type: 'SPIN', delta: 1 }, definition)).toBe(state);
  });
  it('remains inside the board after a deterministic burst of moves and spins', () => {
    for (const piece of puzzleData.pieces) {
      let state = gameReducer(createInitialState(), { type: 'PICK_UP', pieceId: piece.id });
      const actions: GameAction[] = [{ type: 'MOVE', dx: 1, dy: 0 }, { type: 'SPIN', delta: -1 },
        { type: 'MOVE', dx: 0, dy: 1 }, { type: 'SPIN', delta: 1 }, { type: 'MOVE', dx: -1, dy: 0 },
        { type: 'MOVE', dx: 0, dy: -1 }, { type: 'SPIN', delta: -1 }];
      for (let i = 0; i < 140; i++) {
        state = gameReducer(state, actions[i % actions.length]);
        expect(getOccupiedCells(piece, state.holding!, state.holding!.turns)
          .every((cell) => isInside(puzzleData, cell))).toBe(true);
        expect(state.pieces[piece.id].location.type).toBe('stock');
      }
    }
  });
});

describe('submission lifecycle', () => {
  it('guards submission and accepts success only during submitting', () => {
    const state = createInitialState();
    expect(gameReducer(state, { type: 'SUBMIT_STARTED' })).toBe(state);
    expect(gameReducer(state, { type: 'SUBMIT_SUCCEEDED' })).toBe(state);
    expect(gameReducer(state, { type: 'SUBMIT_FAILED' })).toBe(state);
    expect(gameReducer(state, { type: 'CELEBRATION_FINISHED' })).toBe(state);
  });
  it('transitions playing → submitting → celebrating → finished', () => {
    let state = gameReducer(completedState(), { type: 'SUBMIT_STARTED' });
    expect(state.phase).toBe('submitting');
    expect(gameReducer(state, { type: 'SUBMIT_STARTED' })).toBe(state);
    expect(gameReducer(state, { type: 'CELEBRATION_FINISHED' })).toBe(state);
    state = gameReducer(state, { type: 'SUBMIT_SUCCEEDED' });
    expect(state.phase).toBe('celebrating');
    state = gameReducer(state, { type: 'CELEBRATION_FINISHED' });
    expect(state.phase).toBe('finished');
    expect(gameReducer(state, { type: 'CELEBRATION_FINISHED' })).toBe(state);
  });
  it('failure restores playing and keeps the solution for retry', () => {
    const initial = completedState();
    const submitting = gameReducer(initial, { type: 'SUBMIT_STARTED' });
    const failed = gameReducer(submitting, { type: 'SUBMIT_FAILED' });
    expect(failed).toEqual(initial);
    expect(gameReducer(failed, { type: 'SUBMIT_STARTED' }).phase).toBe('submitting');
  });
  it.each(['submitting', 'celebrating', 'finished'] as const)('rejects all player operations while %s', (phase) => {
    const state = { ...completedState(), phase };
    for (const action of [{ type: 'PICK_UP', pieceId: 'A' }, { type: 'MOVE', dx: 1, dy: 0 },
      { type: 'SPIN', delta: -1 }, { type: 'SET' }, { type: 'RETURN_TO_STOCK' }] satisfies GameAction[]) {
      expect(gameReducer(state, action)).toBe(state);
    }
  });
});
