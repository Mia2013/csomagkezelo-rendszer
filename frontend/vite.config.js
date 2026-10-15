import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true, open: true },
  test: {
    testTimeout: 15000,
    environment: "jsdom",
    setupFiles: "./src/test/setup.js",
  },
});
