import { puzzleData } from '../game/puzzleData';
import { createInitialState, reduceGame } from '../game/reducer';
import type { GameAction, GameState, HoldingState, PuzzleDefinition } from '../game/types';

export const ROTATION_DURATION = 0.2;

export type RotationStep = Readonly<{
  id: number;
  from: HoldingState;
  to: HoldingState;
}>;

// Presentation state: the puzzle's canonical GameState stays unchanged.
export type RotationPlaybackState = Readonly<{
  game: GameState;
  rotations: readonly RotationStep[];
  nextRotationId: number;
}>;
export type RotationPlaybackAction = GameAction | { type: 'ROTATION_FINISHED'; id: number };

export function createRotationPlaybackState(game = createInitialState()): RotationPlaybackState {
  return { game, rotations: [], nextRotationId: 0 };
}

export function reduceRotationPlayback(
  state: RotationPlaybackState, action: RotationPlaybackAction, definition: PuzzleDefinition = puzzleData,
): RotationPlaybackState {
  if (action.type === 'ROTATION_FINISHED') {
    // Only the active step may advance playback; repeated/stale callbacks are harmless.
    return state.rotations[0]?.id === action.id
      ? { ...state, rotations: state.rotations.slice(1) } : state;
  }
  if (state.rotations.length > 0 && (action.type === 'MOVE' || action.type === 'SET'
    || action.type === 'RETURN_TO_STOCK' || action.type === 'SUBMIT_STARTED')) return state;

  const game = reduceGame(state.game, action, definition);
  if (game === state.game) return state;
  if (action.type === 'SPIN' && state.game.holding && game.holding) {
    const step: RotationStep = { id: state.nextRotationId, from: state.game.holding, to: game.holding };
    return { game, rotations: [...state.rotations, step], nextRotationId: state.nextRotationId + 1 };
  }
  return { ...state, game };
}

export function rotationPlaybackReducer(state: RotationPlaybackState, action: RotationPlaybackAction): RotationPlaybackState {
  return reduceRotationPlayback(state, action);
}
