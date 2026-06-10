import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The API base URL is read at runtime from VITE_API_BASE_URL (see src/api/client.ts),
// defaulting to the local API. The dev server also proxies /api to avoid CORS in dev.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
});
