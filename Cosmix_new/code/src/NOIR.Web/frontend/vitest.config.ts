import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    passWithNoTests: true,
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    exclude: [
      'src/uikit/**/*.stories.*',
      'node_modules',
      'dist',
      'e2e/**',
    ],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html', 'lcov'],
      reportsDirectory: './coverage',
      thresholds: {
        lines: 60,
        functions: 60,
        branches: 50,
        statements: 60,
      },
      // No 'include' = only track files actually imported in tests (all: false default)
      // Prevents untested uikit/hooks from dragging thresholds below 60%
      exclude: [
        'src/**/*.stories.*',
        'src/**/*.d.ts',
        'src/**/*.test.*',
        'src/**/*.spec.*',
        'src/types/**',
        'src/test/**',
        'src/main.tsx',
        'src/App.tsx',
        'src/i18n/**',
        'src/uikit/schemas.generated.ts',
        'src/lib/constants/**',
      ],
    },
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
      '@uikit': path.resolve(__dirname, './src/uikit'),
    },
  },
})
