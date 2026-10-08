import { puzzleData } from './puzzleData';
import { canSubmit, clampPosition, getOccupiedCells, isInside, rotateCells, shapeSize } from './rules';
import type { GameAction, GameState, PuzzleDefinition } from './types';

export function createInitialState(definition: PuzzleDefinition = puzzleData): GameState {
  return { pieces: Object.fromEntries(definition.pieces.map((piece) =>
    [piece.id, { location: { type: 'stock' }, turns: 0 }])), holding: null, phase: 'playing' };
}

// Custom definitions can bind the same pure reducer without adding definition to GameState.
export function reduceGame(state: GameState, action: GameAction, definition: PuzzleDefinition): GameState {
  switch (action.type) {
    case 'SUBMIT_STARTED':
      return canSubmit(state, definition) ? { ...state, phase: 'submitting' } : state;
    case 'SUBMIT_SUCCEEDED':
      return state.phase === 'submitting' ? { ...state, phase: 'celebrating' } : state;
    case 'SUBMIT_FAILED':
      return state.phase === 'submitting' ? { ...state, phase: 'playing' } : state;
    case 'CELEBRATION_FINISHED':
      return state.phase === 'celebrating' ? { ...state, phase: 'finished' } : state;
  }
  if (state.phase !== 'playing') return state;
  if (action.type === 'PICK_UP') {
    if (state.holding) return state;
    const piece = definition.pieces.find((p) => p.id === action.pieceId);
    const placed = state.pieces[action.pieceId];
    if (!piece || !placed) return state;
    let turns = placed.turns;
    let shape = rotateCells(piece.cells, turns);
    if (placed.location.type === 'stock') {
      // Permit a shape that only fits the board in another orientation.
      const offset = [0, 1, 2, 3].find((delta) =>
        clampPosition(definition, rotateCells(piece.cells, turns + delta), { x: 0, y: 0 }) !== null);
      if (offset === undefined) return state;
      turns += offset;
      shape = rotateCells(piece.cells, turns);
    }
    const size = shapeSize(shape);
    const origin = placed.location.type === 'board' ? placed.location : {
      x: Math.floor((definition.width - size.width) / 2),
      y: Math.floor((definition.height - size.height) / 2),
    };
    const position = clampPosition(definition, shape, origin);
    return position ? { ...state, holding: { pieceId: piece.id, ...position, turns } } : state;
  }
  const held = state.holding;
  if (!held) return state;
  const piece = definition.pieces.find((p) => p.id === held.pieceId);
  if (!piece) return state;
  switch (action.type) {
    case 'MOVE': {
      if (!Number.isInteger(action.dx) || !Number.isInteger(action.dy)
        || Math.abs(action.dx) + Math.abs(action.dy) !== 1) return state;
      const position = clampPosition(definition, rotateCells(piece.cells, held.turns),
        { x: held.x + action.dx, y: held.y + action.dy });
      return !position || (position.x === held.x && position.y === held.y)
        ? state : { ...state, holding: { ...held, ...position } };
    }
    case 'SPIN': {
      if (action.delta !== 1 && action.delta !== -1) return state;
      const turns = held.turns + action.delta;
      const position = clampPosition(definition, rotateCells(piece.cells, turns), held);
      return position ? { ...state, holding: { ...held, ...position, turns } } : state;
    }
    case 'SET':
      if (!getOccupiedCells(piece, held, held.turns).every((cell) => isInside(definition, cell))) return state;
      return { ...state, holding: null, pieces: { ...state.pieces, [held.pieceId]: {
        location: { type: 'board', x: held.x, y: held.y }, turns: held.turns,
      } } };
    case 'RETURN_TO_STOCK':
      return { ...state, holding: null, pieces: { ...state.pieces, [held.pieceId]: {
        location: { type: 'stock' }, turns: held.turns,
      } } };
    default:
      return state;
  }
}

export function gameReducer(state: GameState, action: GameAction): GameState {
  return reduceGame(state, action, puzzleData);
}
