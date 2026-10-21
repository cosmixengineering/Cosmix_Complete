import { fileURLToPath, URL } from 'node:url';
import { readFileSync } from 'node:fs';
import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode, command }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const pfxPath = process.env.COSMIX_DEV_HTTPS_PFX;
  const passphrase = process.env.COSMIX_DEV_HTTPS_PASSWORD;
  if (command === 'serve' && (!pfxPath || !passphrase)) {
    throw new Error(
      'Use npm run dev with a trusted localhost development certificate. Web authentication requires HTTPS.',
    );
  }
  return {
    plugins: [react()],
    resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
    server: {
      host: 'localhost',
      port: 5173,
      https: pfxPath && passphrase ? { pfx: readFileSync(pfxPath), passphrase } : undefined,
      headers: {
        'Cache-Control': 'no-store',
        'Referrer-Policy': 'no-referrer',
        'X-Content-Type-Options': 'nosniff',
      },
      proxy: {
        '/api': {
          target: env.API_PROXY_TARGET || 'https://localhost:7240',
          changeOrigin: true,
          secure: false,
        },
      },
    },
    build: { sourcemap: false },
  };
});
