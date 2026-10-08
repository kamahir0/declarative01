import { useCallback, useEffect, useReducer, useRef, useState } from 'react';
import { puzzleData } from '../game/puzzleData';
import { createInitialState, gameReducer } from '../game/reducer';
import { canSubmit, getBoardAnalysis, isCompleted } from '../game/rules';
import type { GameAction } from '../game/types';
import { submitPuzzle } from '../services/submitPuzzle';
import type { SubmitPuzzle } from '../services/submitPuzzle';
import { OperationControls } from './OperationControls';
import { PieceStock } from './PieceStock';
import { PuzzleBoard } from './PuzzleBoard';

// Remount creates a fresh session and aborts an in-flight request in the old one.
export function PuzzleGame({ submit = submitPuzzle }: { submit?: SubmitPuzzle }) {
  const [session, setSession] = useState(0);
  return <PuzzleSession key={session} submit={submit} onRestart={() => setSession((value) => value + 1)} />;
}

function PuzzleSession({ submit, onRestart }: { submit: SubmitPuzzle; onRestart: () => void }) {
  const [state, dispatch] = useReducer(gameReducer, undefined, () => createInitialState());
  const [error, setError] = useState<string | null>(null);
  const request = useRef<AbortController | null>(null);
  const navigatingWithTab = useRef(false);
  const analysis = getBoardAnalysis(state, puzzleData);
  const completed = isCompleted(state, puzzleData);
  const ready = canSubmit(state, puzzleData);

  useEffect(() => () => { request.current?.abort(); }, []);

  const handleSubmit = useCallback(async () => {
    // The ref closes the window before React renders SUBMIT_STARTED (double-click / Enter repeat).
    if (!canSubmit(state, puzzleData) || request.current) return;
    const controller = new AbortController();
    request.current = controller;
    setError(null);
    dispatch({ type: 'SUBMIT_STARTED' });
    try {
      const response = await submit(state, controller.signal);
      if (controller.signal.aborted) return;
      if (response.accepted) dispatch({ type: 'SUBMIT_SUCCEEDED' });
      else {
        setError('提出を受け付けられませんでした。もう一度お試しください。');
        dispatch({ type: 'SUBMIT_FAILED' });
      }
    } catch {
      if (!controller.signal.aborted) {
        setError('提出に失敗しました。配置はそのまま、もう一度提出できます。');
        dispatch({ type: 'SUBMIT_FAILED' });
      }
    } finally {
      if (request.current === controller) request.current = null;
    }
  }, [state, submit]);

  useEffect(() => {
    const onPointerDown = () => { navigatingWithTab.current = false; };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.altKey || event.ctrlKey || event.metaKey || event.isComposing || event.defaultPrevented) return;
      const target = event.target;
      if (target instanceof HTMLElement && target.closest('input, textarea, select, [contenteditable="true"]')) return;
      if (event.key === 'Tab') { navigatingWithTab.current = true; return; }
      // Preserve native activation when a keyboard user focuses a button.
      if (navigatingWithTab.current && target instanceof HTMLElement && target.closest('button, a')
        && (event.key === 'Enter' || event.key === ' ')) return;
      if (state.phase !== 'playing') return;
      const key = event.key.toLowerCase();
      let action: GameAction | undefined;
      const moves: Record<string, [number, number]> = {
        arrowup: [0, 1], w: [0, 1], arrowdown: [0, -1], s: [0, -1],
        arrowleft: [-1, 0], a: [-1, 0], arrowright: [1, 0], d: [1, 0],
      };
      if (moves[key]) {
        const [dx, dy] = moves[key];
        action = { type: 'MOVE', dx, dy };
      } else if (key === 'q') action = { type: 'SPIN', delta: -1 };
      else if (key === 'r' || key === 'e') action = { type: 'SPIN', delta: 1 };
      else if (key === 'escape' || key === 'backspace') action = { type: 'RETURN_TO_STOCK' };
      else if (key === ' ') action = { type: 'SET' };
      else if (key === 'enter') {
        event.preventDefault();
        if (state.holding) dispatch({ type: 'SET' });
        else if (!event.repeat) void handleSubmit();
        return;
      } else if (/^[1-9]$/.test(key)) {
        const piece = puzzleData.pieces[Number(key) - 1];
        if (piece) action = { type: 'PICK_UP', pieceId: piece.id };
      }
      if (action) { event.preventDefault(); dispatch(action); }
    };
    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('pointerdown', onPointerDown);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('pointerdown', onPointerDown);
    };
  }, [state.phase, state.holding, handleSubmit]);

  const status = state.phase === 'submitting' ? '過不足を確認しています…'
    : state.phase === 'celebrating' ? 'すべての形が、ひとつに。'
      : state.phase === 'finished' ? 'Finished — 完成しました。'
        : state.holding ? `ピース ${state.holding.pieceId} を移動・回転して、Setで確定。`
          : completed ? 'すべてぴったり。Submitで完成を確かめよう。'
            : '9つの形で、36マスを埋めてみよう。';

  return <div className="app-shell">
    <header className="site-header"><a href="./" className="brand" aria-label="POLY GRID ホーム">
      <span className="brand-mark" aria-hidden="true"><i /><i /><i /><i /></span>SHAPE STUDY</a>
      <span className="edition">DECLARATIVE EXPERIMENT <span>02 / REACT</span></span></header>
    <main>
      <div className="intro"><div><p className="eyebrow">A LITTLE PUZZLE, A DIFFERENT PERSPECTIVE.</p>
        <h1>POLY <span>/</span> GRID<span className="title-dot">.</span></h1>
        <p className="intro-copy">9つの形を、ひとつの正方形に。</p></div>
        <button className="restart-button" type="button" onClick={onRestart}>↻ やり直す</button></div>
      <div className="game-layout">
        <section className="canvas-panel" aria-label="盤面と提出">
          <PuzzleBoard state={state} definition={puzzleData} dispatch={dispatch} />
          <div className="progress-row"><div><strong>{analysis.coveredCells.toString().padStart(2, '0')}</strong>
            <span> / {analysis.totalCells} マス</span></div><span className="progress-detail">
              {analysis.placedPieces} / {analysis.totalPieces} 配置済み
              {analysis.overlapCells > 0 && <span className="overlap-count"> · 重なり {analysis.overlapCells}</span>}</span></div>
          <div className="progress-track" role="progressbar" aria-label="確定した盤面の充填率"
            aria-valuenow={analysis.coveredCells} aria-valuemin={0} aria-valuemax={analysis.totalCells}>
            <span style={{ width: `${analysis.coveredCells / analysis.totalCells * 100}%` }} /></div>
          <button className="submit-button" type="button" disabled={!ready} onClick={() => void handleSubmit()}>
            <span>{state.phase === 'submitting' ? 'Submitting…' : state.phase === 'celebrating'
              ? 'Celebrating…' : state.phase === 'finished' ? 'Finished ✓' : 'Submit'}</span>
            <span>{state.phase === 'finished' ? '完成しました' : '完成を確かめる'} <span aria-hidden="true">↗</span></span>
          </button>
          <p className="game-status" aria-live="polite">{status}</p>
          {error && <p className="submission-error" role="alert">{error}</p>}
        </section>
        <aside className="tools-panel"><PieceStock state={state} definition={puzzleData} dispatch={dispatch} />
          <OperationControls state={state} dispatch={dispatch} /></aside>
      </div>
      <section className="how-to" aria-label="遊び方"><span className="how-to-title">HOW TO PLAY</span>
        <p><b>01</b> 形を選ぶ <span>→</span> <b>02</b> 移動・回転 <span>→</span> <b>03</b> Setで置く <span>→</span> <b>04</b> Submit</p>
        <p className="keyboard-help"><kbd>1–9</kbd> 選択 <kbd>↑↓←→ / WASD</kbd> 移動 <kbd>Q / R</kbd> 回転 <kbd>Space</kbd> Set <kbd>Enter</kbd> Set / Submit <kbd>Esc</kbd> 戻す</p>
      </section>
    </main>
    <footer><span>ひとつずつ、形を見つけよう。</span><span>STATE → VIEW · REACT + MOTION</span></footer>
  </div>;
}
