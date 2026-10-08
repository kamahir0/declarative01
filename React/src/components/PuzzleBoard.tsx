import type { Dispatch } from 'react';
import { motion } from 'motion/react';
import type { RotationStep } from '../animations/rotationPlayback';
import { cellKey } from '../game/rules';
import { getDisplayedPiece, getDisplayOccupancy } from '../game/selectors';
import type { GameAction, GameState, PuzzleDefinition } from '../game/types';
import { CELL_SIZE, Piece } from './Piece';

export function PuzzleBoard({ state, definition, dispatch, rotation, onRotationFinished }: {
  state: GameState; definition: PuzzleDefinition; dispatch: Dispatch<GameAction>;
  rotation?: RotationStep; onRotationFinished?: (id: number) => void;
}) {
  const occupancy = getDisplayOccupancy(state, definition);
  const cells = Array.from({ length: definition.width * definition.height }, (_, index) => ({
    x: index % definition.width, y: definition.height - Math.floor(index / definition.width) - 1,
  }));
  return <div className="board-frame">
    <div className="board-label"><span>THE CANVAS</span><span>{definition.width} × {definition.height}</span></div>
    <div className="board-sizer" style={{ aspectRatio: `${definition.width} / ${definition.height}` }}>
      <div className="puzzle-board" aria-label={`${definition.width}×${definition.height}のパズル盤面`}
        style={{ width: definition.width * CELL_SIZE, height: definition.height * CELL_SIZE }}>
        <svg className="board-grid" width="100%" height="100%" aria-hidden="true">
          {cells.map(({ x, y }) => <rect key={`${x},${y}`} x={x * CELL_SIZE + 2}
            y={(definition.height - y - 1) * CELL_SIZE + 2}
            width={CELL_SIZE - 4} height={CELL_SIZE - 4} rx="10" />)}
        </svg>
        {definition.pieces.map((piece, index) => {
          const displayed = getDisplayedPiece(state, piece);
          if (!displayed) return null;
          return <Piece key={piece.id} piece={piece} {...displayed} boardHeight={definition.height}
            phase={state.phase} index={index} disabled={state.phase !== 'playing' || state.holding !== null}
            onPickUp={() => dispatch({ type: 'PICK_UP', pieceId: piece.id })}
            rotation={rotation?.to.pieceId === piece.id ? rotation : undefined}
            onRotationFinished={onRotationFinished}
            onCelebrationFinished={index === definition.pieces.length - 1
              ? () => dispatch({ type: 'CELEBRATION_FINISHED' }) : undefined} />;
        })}
        <svg className="overlap-layer" width="100%" height="100%" aria-hidden="true">
          {cells.filter((cell) => (occupancy.get(cellKey(cell)) ?? 0) > 1).map(({ x, y }) =>
            <g key={`${x},${y}`} transform={`translate(${x * CELL_SIZE},${(definition.height - y - 1) * CELL_SIZE})`}>
              <rect x="4" y="4" width={CELL_SIZE - 8} height={CELL_SIZE - 8} rx="8" />
              <path d="M26 26L38 38M38 26L26 38" />
            </g>)}
        </svg>
      </div>
    </div>
    {state.phase === 'finished' && <motion.div className="finished-card"
      initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} role="status">
      <span className="finished-icon">✓</span><div><strong>ぴったり、完成。</strong><p>9つの形が、ひとつになりました。</p></div>
    </motion.div>}
    <div className="board-caption"><span className="small-dot" />
      {state.holding ? `ピース ${state.holding.pieceId} を操作中` : 'ピースを選んで、ここに置いてみよう。'}
    </div>
  </div>;
}
