import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.PRESENTATION_BASE_URL ?? "http://localhost:5173";

export default defineConfig({
  testDir: ".",
  fullyParallel: false,
  forbidOnly: true,
  retries: 0,
  workers: 1,
  reporter: "list",
  use: {
    baseURL,
    trace: "off",
    video: "off",
    screenshot: "off",
    actionTimeout: 30_000,
    navigationTimeout: 15_000,
  },
  projects: [
    {
      name: "presentation",
      use: {
        ...devices["Desktop Chrome"],
        viewport: { width: 1280, height: 720 },
        headless: false,
        launchOptions: { slowMo: 400 },
        ignoreHTTPSErrors: true,
      },
    },
  ],
  webServer: process.env.SKIP_SERVER
    ? undefined
    : {
        command: "npm run dev",
        cwd: "../frontend",
        url: baseURL,
        reuseExistingServer: true,
        timeout: 60_000,
      },
});
