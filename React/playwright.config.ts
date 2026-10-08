import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  // Rotation duration tests observe animation frames; avoid competing browser instances.
  workers: 1,
  use: { baseURL: 'http://127.0.0.1:5173', trace: 'retain-on-failure' },
  webServer: { command: 'npm run dev -- --host 127.0.0.1 --port 5173 --strictPort',
    url: 'http://127.0.0.1:5173', reuseExistingServer: !process.env.CI },
});
