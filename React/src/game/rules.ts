import type { Cell, GameState, PieceDefinition, PuzzleDefinition } from './types';

export const logicalTurns = (turns: number): number => ((turns % 4) + 4) % 4;
export const cellKey = ({ x, y }: Cell): string => `${x},${y}`;

export function rotateCells(cells: readonly Cell[], turns: number): Cell[] {
  const rotated = cells.map((cell) => {
    let { x, y } = cell;
    for (let i = 0; i < logicalTurns(turns); i++) [x, y] = [y, -x];
    return { x, y };
  });
  const minX = Math.min(...rotated.map((cell) => cell.x));
  const minY = Math.min(...rotated.map((cell) => cell.y));
  return rotated.map(({ x, y }) => ({ x: x - minX, y: y - minY }))
    .sort((a, b) => a.y - b.y || a.x - b.x);
}

export function shapeSize(cells: readonly Cell[]): { width: number; height: number } {
  return { width: Math.max(...cells.map((c) => c.x)) + 1,
    height: Math.max(...cells.map((c) => c.y)) + 1 };
}

export function clampPosition(
  definition: PuzzleDefinition, cells: readonly Cell[], position: Cell,
): Cell | null {
  const size = shapeSize(cells);
  if (size.width > definition.width || size.height > definition.height) return null;
  return { x: Math.max(0, Math.min(position.x, definition.width - size.width)),
    y: Math.max(0, Math.min(position.y, definition.height - size.height)) };
}

export function getOccupiedCells(piece: PieceDefinition, position: Cell, turns: number): Cell[] {
  return rotateCells(piece.cells, turns).map(({ x, y }) => ({ x: x + position.x, y: y + position.y }));
}

export function isInside(definition: PuzzleDefinition, { x, y }: Cell): boolean {
  return Number.isInteger(x) && Number.isInteger(y)
    && x >= 0 && y >= 0 && x < definition.width && y < definition.height;
}

// Counts committed placements; Holding never changes the committed board.
export function getBoardAnalysis(state: GameState, definition: PuzzleDefinition) {
  const occupancy = new Map<string, number>();
  let placedPieces = 0;
  let outsideCells = 0;
  for (const piece of definition.pieces) {
    const placed = state.pieces[piece.id];
    if (placed?.location.type !== 'board') continue;
    placedPieces++;
    for (const cell of getOccupiedCells(piece, placed.location, placed.turns)) {
      if (!isInside(definition, cell)) { outsideCells++; continue; }
      const key = cellKey(cell);
      occupancy.set(key, (occupancy.get(key) ?? 0) + 1);
    }
  }
  return { occupancy, placedPieces, outsideCells, coveredCells: occupancy.size,
    overlapCells: [...occupancy.values()].filter((count) => count > 1).length,
    totalCells: definition.width * definition.height, totalPieces: definition.pieces.length };
}

export function isCompleted(state: GameState, definition: PuzzleDefinition): boolean {
  const a = getBoardAnalysis(state, definition);
  return a.placedPieces === a.totalPieces && a.coveredCells === a.totalCells
    && a.overlapCells === 0 && a.outsideCells === 0;
}

export function canSubmit(state: GameState, definition: PuzzleDefinition): boolean {
  return state.phase === 'playing' && state.holding === null && isCompleted(state, definition);
}
