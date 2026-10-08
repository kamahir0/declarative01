import type { Cell, PuzzleDefinition } from './types';

const cells = (...pairs: [number, number][]): readonly Cell[] =>
  pairs.map(([x, y]) => ({ x, y }));

// Unity/Assets/Puzzle/MasterDataと同じ形状・色。座標は左下原点。
export const puzzleData: PuzzleDefinition = {
  width: 6,
  height: 6,
  pieces: [
    { id: 'A', name: 'L', color: '#EBA275', cells: cells([0, 0], [1, 0], [0, 1], [0, 2]) },
    { id: 'B', name: 'J', color: '#EBC467', cells: cells([0, 0], [1, 0], [1, 1], [1, 2]) },
    { id: 'C', name: 'T', color: '#85BB9A', cells: cells([0, 1], [1, 1], [2, 1], [1, 0]) },
    { id: 'D', name: 'T', color: '#7FAFCD', cells: cells([0, 1], [1, 1], [2, 1], [1, 0]) },
    { id: 'E', name: 'S', color: '#B19BC6', cells: cells([0, 0], [1, 0], [1, 1], [2, 1]) },
    { id: 'F', name: 'Z', color: '#DF989C', cells: cells([1, 0], [2, 0], [0, 1], [1, 1]) },
    { id: 'G', name: 'O', color: '#91BDAF', cells: cells([0, 0], [1, 0], [0, 1], [1, 1]) },
    { id: 'H', name: 'I', color: '#A9B77C', cells: cells([0, 0], [1, 0], [2, 0], [3, 0]) },
    { id: 'I', name: 'L', color: '#CCA27E', cells: cells([0, 0], [1, 0], [0, 1], [0, 2]) },
  ],
};
