import type { Variants } from 'motion/react';

export const CELEBRATION_DURATION = 0.5;
export const CELEBRATION_INTERVAL = 0.2;

export const celebrationVariants: Variants = {
  idle: { filter: 'brightness(1) drop-shadow(0px 0px 0px rgba(255, 255, 255, 0))' },
  celebrating: (index: number) => ({
    filter: [
      'brightness(1) drop-shadow(0px 0px 0px rgba(255, 255, 255, 0))',
      'brightness(1.6) drop-shadow(0px 0px 12px rgba(255, 255, 255, 0.9))',
      'brightness(1) drop-shadow(0px 0px 0px rgba(255, 255, 255, 0))',
    ],
    transition: { duration: CELEBRATION_DURATION, delay: index * CELEBRATION_INTERVAL,
      times: [0, 0.45, 1], ease: 'easeInOut' },
  }),
};
