import { cellKey, getOccupiedCells } from './rules';
import type { GameState, PieceDefinition, PuzzleDefinition } from './types';

export function getDisplayedPiece(state: GameState, piece: PieceDefinition) {
  if (state.holding?.pieceId === piece.id) return { ...state.holding, selected: true };
  const committed = state.pieces[piece.id];
  return committed.location.type === 'board'
    ? { ...committed.location, turns: committed.turns, selected: false } : null;
}

// Render overlap markers from displayed placements, excluding a held piece's old location.
export function getDisplayOccupancy(state: GameState, definition: PuzzleDefinition): Map<string, number> {
  const occupancy = new Map<string, number>();
  for (const piece of definition.pieces) {
    const display = getDisplayedPiece(state, piece);
    if (!display) continue;
    for (const cell of getOccupiedCells(piece, display, display.turns)) {
      const key = cellKey(cell);
      occupancy.set(key, (occupancy.get(key) ?? 0) + 1);
    }
  }
  return occupancy;
}
