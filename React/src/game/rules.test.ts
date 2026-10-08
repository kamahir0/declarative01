import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { puzzleData } from './puzzleData';
import { createInitialState, gameReducer } from './reducer';
import { canSubmit, clampPosition, getBoardAnalysis, getOccupiedCells, isCompleted, logicalTurns, rotateCells } from './rules';
import { getDisplayedPiece, getDisplayOccupancy } from './selectors';
import { completedState } from './testFixtures';

describe('Unity master parity', () => {
  it('uses the exact board, piece IDs, cells, and colors authored in Unity', () => {
    const master = readFileSync(new URL('../../../Unity/Assets/Puzzle/MasterData/SamplePuzzle.asset', import.meta.url), 'utf8');
    expect(master).toContain(`width: ${puzzleData.width}`);
    expect(master).toContain(`height: ${puzzleData.height}`);
    for (const piece of puzzleData.pieces) {
      const asset = readFileSync(new URL(`../../../Unity/Assets/Puzzle/MasterData/Pieces/Piece_${piece.id}.asset`, import.meta.url), 'utf8');
      expect(asset).toContain(`pieceId: ${piece.id}`);
      expect(asset).toContain(`displayName: ${piece.name}`);
      const cells = [...asset.matchAll(/- \{x: (\d+), y: (\d+)\}/g)].map((m) => ({ x: Number(m[1]), y: Number(m[2]) }));
      expect(piece.cells).toEqual(cells);
      const tint = asset.match(/tint: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)/)!;
      const hex = '#' + tint.slice(1).map((v) => Math.round(Number(v) * 255).toString(16).padStart(2, '0')).join('').toUpperCase();
      expect(piece.color).toBe(hex);
    }
  });
});

describe('rotation and placement', () => {
  it('normalizes negative and cumulative turns without mutating the shape', () => {
    const piece = puzzleData.pieces[0];
    expect(logicalTurns(-5)).toBe(3);
    expect(rotateCells(piece.cells, -1)).toEqual(rotateCells(piece.cells, 3));
    expect(rotateCells(piece.cells, 5)).toEqual(rotateCells(piece.cells, 1));
    expect(rotateCells(piece.cells, 4)).toEqual(rotateCells(piece.cells, 0));
    expect(piece.cells).toEqual([{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 0, y: 1 }, { x: 0, y: 2 }]);
  });
  it('rotates clockwise with the Unity lower-left coordinate convention', () => {
    expect(rotateCells(puzzleData.pieces[0].cells, 1)).toEqual([
      { x: 0, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 },
    ]);
  });
  it.each(puzzleData.pieces)('$id preserves area and four turns restore the shape', (piece) => {
    for (let turns = -8; turns <= 8; turns++) {
      const shape = rotateCells(piece.cells, turns);
      expect(shape).toHaveLength(4);
      expect(new Set(shape.map((cell) => `${cell.x},${cell.y}`)).size).toBe(4);
      expect(Math.min(...shape.map((c) => c.x))).toBe(0);
      expect(Math.min(...shape.map((c) => c.y))).toBe(0);
      expect(rotateCells(shape, 4 - logicalTurns(turns))).toEqual(rotateCells(piece.cells, 0));
    }
  });
  it('clamps every boundary and rejects a shape that cannot fit', () => {
    const shape = rotateCells(puzzleData.pieces[0].cells, 0);
    expect(clampPosition(puzzleData, shape, { x: -5, y: -3 })).toEqual({ x: 0, y: 0 });
    expect(clampPosition(puzzleData, shape, { x: 10, y: 10 })).toEqual({ x: 4, y: 3 });
    expect(clampPosition({ ...puzzleData, width: 1 }, shape, { x: 0, y: 0 })).toBeNull();
  });
  it('derives occupied cells after rotation and translation', () => {
    expect(getOccupiedCells(puzzleData.pieces[0], { x: 2, y: 1 }, 1)).toEqual([
      { x: 2, y: 1 }, { x: 2, y: 2 }, { x: 3, y: 2 }, { x: 4, y: 2 },
    ]);
  });
});

describe('completion and display', () => {
  it('accepts the Unity solution built with actions', () => {
    const state = completedState();
    expect(isCompleted(state, puzzleData)).toBe(true);
    expect(canSubmit(state, puzzleData)).toBe(true);
    expect(getBoardAnalysis(state, puzzleData)).toMatchObject({ coveredCells: 36, overlapCells: 0, outsideCells: 0, placedPieces: 9 });
  });
  it('rejects an incomplete board, overlaps, missing pieces, and out-of-board placements', () => {
    expect(isCompleted(createInitialState(), puzzleData)).toBe(false);
    const state = completedState();
    const replaceA = (location: { type: 'stock' } | { type: 'board'; x: number; y: number }) => ({ ...state,
      pieces: { ...state.pieces, A: { ...state.pieces.A, location } } });
    expect(isCompleted(replaceA({ type: 'stock' }), puzzleData)).toBe(false);
    const overlap = replaceA({ type: 'board', x: 2, y: 0 });
    expect(getBoardAnalysis(overlap, puzzleData).overlapCells).toBeGreaterThan(0);
    expect(isCompleted(overlap, puzzleData)).toBe(false);
    const outside = replaceA({ type: 'board', x: -1, y: 0 });
    expect(getBoardAnalysis(outside, puzzleData).outsideCells).toBeGreaterThan(0);
    expect(isCompleted(outside, puzzleData)).toBe(false);
  });
  it('Holding leaves completion intact but blocks submission and only renders the current position', () => {
    const held = gameReducer(completedState(), { type: 'PICK_UP', pieceId: 'A' });
    const moved = gameReducer(held, { type: 'MOVE', dx: 1, dy: 0 });
    expect(isCompleted(moved, puzzleData)).toBe(true);
    expect(canSubmit(moved, puzzleData)).toBe(false);
    expect(getDisplayedPiece(moved, puzzleData.pieces[0])).toMatchObject({ x: 1, y: 0, selected: true });
    const display = getDisplayOccupancy(moved, puzzleData);
    expect(display.has('0,0')).toBe(false);
    expect([...display.values()].reduce((a, b) => a + b, 0)).toBe(36);
  });
  it.each(['submitting', 'celebrating', 'finished'] as const)('blocks submission during %s', (phase) => {
    expect(canSubmit({ ...completedState(), phase }, puzzleData)).toBe(false);
  });
});
