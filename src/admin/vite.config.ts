import { defineConfig } from 'vite';
import solidPlugin from 'vite-plugin-solid';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [solidPlugin(), tailwindcss()],
  server: {
    port: 3003,
    proxy: {
      '/api': 'http://localhost:5041',
    },
  },
  build: {
    target: 'esnext',
    outDir: 'dist',
  },
});
