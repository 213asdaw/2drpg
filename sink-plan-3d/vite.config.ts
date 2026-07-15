import { defineConfig } from "vite";

export default defineConfig({
  // Electron / file:// 패키지에서 상대 경로로 로드
  base: "./",
  server: {
    host: "127.0.0.1",
    port: 5173,
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    chunkSizeWarningLimit: 800,
  },
});
