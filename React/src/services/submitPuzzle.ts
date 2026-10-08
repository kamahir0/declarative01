import { puzzleData } from '../game/puzzleData';
import { canSubmit } from '../game/rules';
import type { GameState } from '../game/types';

export type SubmissionResult = Readonly<{ accepted: boolean }>;
export type SubmitPuzzle = (snapshot: GameState, signal: AbortSignal) => Promise<SubmissionResult>;

// The immutable snapshot is captured before SUBMIT_STARTED. Replace this function with fetch later.
export const submitPuzzle: SubmitPuzzle = (snapshot, signal) => new Promise((resolve, reject) => {
  if (signal.aborted) { reject(new DOMException('Aborted', 'AbortError')); return; }
  const onAbort = () => { clearTimeout(timer); reject(new DOMException('Aborted', 'AbortError')); };
  const timer = setTimeout(() => {
    signal.removeEventListener('abort', onAbort);
    resolve({ accepted: canSubmit(snapshot, puzzleData) });
  }, 650);
  signal.addEventListener('abort', onAbort, { once: true });
});
