import { motion, MotionConfig } from 'motion/react';
import { celebrationVariants } from '../animations/celebration';
import { ROTATION_DURATION } from '../animations/rotationPlayback';
import type { RotationStep } from '../animations/rotationPlayback';
import { rotateCells, shapeSize } from '../game/rules';
import type { GamePhase, PieceDefinition } from '../game/types';

export const CELL_SIZE = 64;

export function PieceShape({ piece, turns = 0 }: { piece: PieceDefinition; turns?: number }) {
  const cells = rotateCells(piece.cells, turns);
  const { width, height } = shapeSize(cells);
  return <svg viewBox={`0 0 ${width * CELL_SIZE} ${height * CELL_SIZE}`} aria-hidden="true">
    {cells.map(({ x, y }) => <rect key={`${x},${y}`}
      x={x * CELL_SIZE + 3} y={(height - y - 1) * CELL_SIZE + 3}
      width={CELL_SIZE - 6} height={CELL_SIZE - 6} rx="9" fill={piece.color} />)}
  </svg>;
}

type PieceProps = {
  piece: PieceDefinition;
  x: number;
  y: number;
  turns: number;
  selected: boolean;
  boardHeight: number;
  phase: GamePhase;
  index: number;
  disabled: boolean;
  onPickUp: () => void;
  onCelebrationFinished?: () => void;
  rotation?: RotationStep;
  onRotationFinished?: (id: number) => void;
};

export function Piece({ piece, x, y, turns, selected, boardHeight, phase, index,
  disabled, onPickUp, onCelebrationFinished, rotation, onRotationFinished }: PieceProps) {
  const base = shapeSize(rotateCells(piece.cells, 0));
  const target = rotation?.to ?? { x, y, turns };
  const rotationLabel = rotation ? `rotation-${rotation.id}` : 'rest';
  const rotated = shapeSize(rotateCells(piece.cells, target.turns));
  return <motion.div className={`board-piece ${selected ? 'is-held' : ''}`}
    data-piece={piece.id} data-turns={turns} data-x={x} data-y={y}
    initial={false} animate={{ x: (target.x + rotated.width / 2) * CELL_SIZE,
      y: (boardHeight - target.y - rotated.height / 2) * CELL_SIZE }}
    transition={{ duration: 0.2, ease: 'easeOut' }} style={{ zIndex: selected ? 30 : index + 1 }}>
    <MotionConfig reducedMotion="never">
      <motion.button className="piece-hit-area" type="button" disabled={disabled}
        data-rotation-id={rotation?.id} data-rotation-from={rotation?.from.turns} data-rotation-to={rotation?.to.turns}
        aria-label={`盤面のピース ${piece.id} を持ち上げる`} aria-pressed={selected}
        onClick={onPickUp} initial={{ rotate: (rotation?.from.turns ?? turns) * 90 }}
        animate={rotationLabel} variants={{ [rotationLabel]: { rotate: target.turns * 90 } }}
        transition={{ type: 'tween', duration: ROTATION_DURATION, ease: 'linear' }}
        onAnimationComplete={(label) => {
          if (rotation && label === rotationLabel) onRotationFinished?.(rotation.id);
        }}
        style={{ width: base.width * CELL_SIZE, height: base.height * CELL_SIZE,
          left: -base.width * CELL_SIZE / 2, top: -base.height * CELL_SIZE / 2 }}>
        <motion.div className="piece-glow" variants={celebrationVariants} custom={index}
          initial="idle" animate={phase === 'celebrating' ? 'celebrating' : 'idle'}
          onAnimationComplete={(variant) => {
            if (variant === 'celebrating') onCelebrationFinished?.();
          }}>
          <PieceShape piece={piece} />
        </motion.div>
      </motion.button>
    </MotionConfig>
  </motion.div>;
}
