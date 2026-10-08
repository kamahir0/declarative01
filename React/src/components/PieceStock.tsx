import type { Dispatch } from 'react';
import type { GameAction, GameState, PuzzleDefinition } from '../game/types';
import { PieceShape } from './Piece';

export function PieceStock({ state, definition, dispatch }: {
  state: GameState; definition: PuzzleDefinition; dispatch: Dispatch<GameAction>;
}) {
  const stockCount = definition.pieces.filter((p) => state.pieces[p.id].location.type === 'stock').length;
  return <section className="stock-panel" aria-labelledby="stock-title">
    <div className="section-label"><h2 id="stock-title">PIECES</h2><span>{stockCount} / {definition.pieces.length} 未配置</span></div>
    <p className="section-description">クリックで持ち上げる</p>
    <div className="piece-stock">
      {definition.pieces.map((piece, index) => {
        const selected = state.holding?.pieceId === piece.id;
        const placed = state.pieces[piece.id];
        const isBoard = placed.location.type === 'board';
        return <button key={piece.id} type="button"
          className={`stock-piece ${selected ? 'is-selected' : ''} ${isBoard ? 'is-placed' : ''}`}
          aria-label={`ピース ${piece.id} を持ち上げる${isBoard ? '（配置済み）' : ''}`}
          aria-pressed={selected} disabled={state.phase !== 'playing' || state.holding !== null}
          onClick={() => dispatch({ type: 'PICK_UP', pieceId: piece.id })}>
          <span className="piece-letter">{piece.id}</span><span className="piece-shortcut">{index + 1}</span>
          <span className="stock-shape"><PieceShape piece={piece} turns={placed.turns} /></span>
          <span className="piece-state">{selected ? '持ち上げ中' : isBoard ? '✓ 配置済み' : `${piece.name} / 4 cells`}</span>
        </button>;
      })}
    </div>
    <p className="stock-note">重なったピースも、この一覧から選べます。</p>
  </section>;
}
