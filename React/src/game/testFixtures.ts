import { createInitialState, reduceGame } from './reducer';
import type { GameState } from './types';
import { puzzleData } from './puzzleData';

// Unity/docs/sample-solution.md. Build exclusively through public game actions.
export const sampleSolution = [
  { pieceId: 'A', x: 0, y: 0, turns: 0 },
  { pieceId: 'B', x: 2, y: 0, turns: 1 },
  { pieceId: 'C', x: 4, y: 0, turns: 1 },
  { pieceId: 'D', x: 2, y: 3, turns: 0 },
  { pieceId: 'E', x: 2, y: 1, turns: 1 },
  { pieceId: 'F', x: 4, y: 2, turns: 1 },
  { pieceId: 'G', x: 0, y: 4, turns: 0 },
  { pieceId: 'H', x: 2, y: 5, turns: 0 },
  { pieceId: 'I', x: 0, y: 1, turns: 2 },
] as const;

export function completedState(): GameState {
  let state = createInitialState();
  for (const target of sampleSolution) {
    state = reduceGame(state, { type: 'PICK_UP', pieceId: target.pieceId }, puzzleData);
    for (let i = 0; i < target.turns; i++) state = reduceGame(state, { type: 'SPIN', delta: 1 }, puzzleData);
    while (state.holding!.x !== target.x) state = reduceGame(state,
      { type: 'MOVE', dx: Math.sign(target.x - state.holding!.x), dy: 0 }, puzzleData);
    while (state.holding!.y !== target.y) state = reduceGame(state,
      { type: 'MOVE', dx: 0, dy: Math.sign(target.y - state.holding!.y) }, puzzleData);
    state = reduceGame(state, { type: 'SET' }, puzzleData);
  }
  return state;
}
