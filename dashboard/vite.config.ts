import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
      '/workflow.WorkflowMonitor': {
        target: 'http://localhost:5080',
        changeOrigin: true,
        // gRPC-Web uses HTTP/1.1 streaming — disable response buffering
        configure: (proxy) => {
          proxy.on('proxyRes', (proxyRes) => {
            // Ensure chunked transfer is not buffered
            proxyRes.headers['cache-control'] = 'no-cache';
            proxyRes.headers['x-accel-buffering'] = 'no';
          });
        },
      },
    },
  },
  build: {
    outDir: 'dist',
  },
});
