import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// The dashboard only talks to the gateway. Under the Aspire AppHost GATEWAY_URL and PORT are injected;
// standalone it uses the gateway's default address.
const gatewayUrl = process.env.GATEWAY_URL ?? 'http://localhost:5080';
const port = Number(process.env.PORT ?? 5173);

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port,
    strictPort: true,
    proxy: {
      '/api': {
        target: gatewayUrl,
        changeOrigin: true,
      },
      '/llmflows.RunMonitor': {
        target: gatewayUrl,
        changeOrigin: true,
        // gRPC-Web uses HTTP/1.1 streaming — disable response buffering
        configure: (proxy) => {
          proxy.on('proxyRes', (proxyRes) => {
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
