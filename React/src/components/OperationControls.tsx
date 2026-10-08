import type { Dispatch } from 'react';
import type { GameAction, GameState } from '../game/types';

export function OperationControls({ state, dispatch }: { state: GameState; dispatch: Dispatch<GameAction> }) {
  const disabled = state.phase !== 'playing' || state.holding === null;
  return <section className="operation-panel" aria-labelledby="operation-title">
    <div className="section-label"><h2 id="operation-title">MAKE YOUR MOVE</h2>
      <span>{state.holding ? `PIECE ${state.holding.pieceId}` : '選択してください'}</span></div>
    <div className="operation-layout">
      <div className="move-buttons" aria-label="移動">
        <button type="button" className="move-up" aria-label="上に移動" disabled={disabled}
          onClick={() => dispatch({ type: 'MOVE', dx: 0, dy: 1 })}>↑</button>
        <button type="button" className="move-left" aria-label="左に移動" disabled={disabled}
          onClick={() => dispatch({ type: 'MOVE', dx: -1, dy: 0 })}>←</button>
        <span className="move-center" aria-hidden="true">·</span>
        <button type="button" className="move-right" aria-label="右に移動" disabled={disabled}
          onClick={() => dispatch({ type: 'MOVE', dx: 1, dy: 0 })}>→</button>
        <button type="button" className="move-down" aria-label="下に移動" disabled={disabled}
          onClick={() => dispatch({ type: 'MOVE', dx: 0, dy: -1 })}>↓</button>
      </div>
      <div className="piece-actions">
        <div className="spin-buttons">
          <button type="button" disabled={disabled} aria-label="左に90度回転"
            onClick={() => dispatch({ type: 'SPIN', delta: -1 })}><span>↶</span> 左回転 <kbd>Q</kbd></button>
          <button type="button" disabled={disabled} aria-label="右に90度回転"
            onClick={() => dispatch({ type: 'SPIN', delta: 1 })}><span>↷</span> 右回転 <kbd>R</kbd></button>
        </div>
        <button className="set-button" type="button" disabled={disabled}
          onClick={() => dispatch({ type: 'SET' })}>Set <span>配置を確定</span><kbd>Space</kbd></button>
        <button className="return-button" type="button" disabled={disabled}
          onClick={() => dispatch({ type: 'RETURN_TO_STOCK' })}>↩ 持ちピースに戻す <kbd>Esc</kbd></button>
      </div>
    </div>
  </section>;
}
