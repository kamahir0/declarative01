export type PieceId = string;
export type Cell = Readonly<{ x: number; y: number }>;
export type PieceDefinition = Readonly<{
  id: PieceId;
  name: string;
  color: string;
  cells: readonly Cell[];
}>;
export type PuzzleDefinition = Readonly<{
  width: number;
  height: number;
  pieces: readonly PieceDefinition[];
}>;
export type PieceState = Readonly<{
  location: { type: 'stock' } | { type: 'board'; x: number; y: number };
  turns: number;
}>;
export type HoldingState = Readonly<{
  pieceId: PieceId;
  x: number;
  y: number;
  turns: number;
}>;
export type GamePhase = 'playing' | 'submitting' | 'celebrating' | 'finished';
export type GameState = Readonly<{
  pieces: Readonly<Record<PieceId, PieceState>>;
  holding: HoldingState | null;
  phase: GamePhase;
}>;
export type GameAction =
  | { type: 'PICK_UP'; pieceId: PieceId }
  | { type: 'MOVE'; dx: number; dy: number }
  | { type: 'SPIN'; delta: 1 | -1 }
  | { type: 'SET' }
  | { type: 'RETURN_TO_STOCK' }
  | { type: 'SUBMIT_STARTED' }
  | { type: 'SUBMIT_SUCCEEDED' }
  | { type: 'SUBMIT_FAILED' }
  | { type: 'CELEBRATION_FINISHED' };
