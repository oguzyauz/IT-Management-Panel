import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        // MUI tek başına bundle'ın büyük kısmı; ayrı chunk'ta tutmak tarayıcı önbelleğini korur.
        manualChunks: {
          mui: ['@mui/material', '@mui/icons-material'],
          vendor: ['react', 'react-dom', 'react-router-dom', '@tanstack/react-query', 'axios'],
        },
      },
    },
  },
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
