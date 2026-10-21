const apiBase =
  import.meta.env.VITE_API_BASE_URL ||
  (import.meta.env.DEV ? 'https://localhost:7240/api/v1' : '/api/v1');
const resolved = new URL(apiBase, window.location.origin);
if (resolved.username || resolved.password || resolved.search || resolved.hash) {
  throw new Error('Invalid API base URL configuration.');
}
if (resolved.protocol !== 'https:' || window.location.protocol !== 'https:') {
  throw new Error('Web sign-in requires HTTPS for both the frontend and API.');
}
export const env = Object.freeze({
  apiBaseUrl: resolved.href.replace(/\/$/, ''),
});
