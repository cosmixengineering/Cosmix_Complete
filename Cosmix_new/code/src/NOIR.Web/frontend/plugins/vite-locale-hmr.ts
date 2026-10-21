import type { Plugin } from 'vite'
import path from 'path'

/**
 * Vite plugin that watches locale translation files and triggers
 * a full page reload when they change. Since i18next loads translations
 * at runtime via HTTP, module HMR doesn't apply — a full reload is needed.
 *
 * Only active in dev mode (`apply: 'serve'`).
 */
export const localeHmrPlugin = (): Plugin => {
  let debounceTimer: ReturnType<typeof setTimeout> | null = null

  return {
    name: 'vite-locale-hmr',
    apply: 'serve',

    configureServer(devServer) {
      const localesDir = path.resolve(devServer.config.root, 'public/locales')

      // Watch the locales directory for changes
      devServer.watcher.add(localesDir)

      devServer.watcher.on('change', (filePath) => {
        if (!filePath.includes('locales') || !filePath.endsWith('.json')) return

        // Debounce 300ms to handle rapid saves
        if (debounceTimer) clearTimeout(debounceTimer)
        debounceTimer = setTimeout(() => {
          const relative = path.relative(devServer.config.root, filePath)
          devServer.config.logger.info(
            `\x1b[36m[locale-hmr]\x1b[0m ${relative} changed — reloading`,
            { timestamp: true }
          )
          devServer.ws.send({ type: 'full-reload', path: '*' })
        }, 300)
      })
    },
  }
}
