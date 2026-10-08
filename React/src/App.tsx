import { MotionConfig } from 'motion/react';
import { PuzzleGame } from './components/PuzzleGame';

export default function App() {
  return <MotionConfig reducedMotion="user"><PuzzleGame /></MotionConfig>;
}
