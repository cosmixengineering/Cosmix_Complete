// Central Google Maps configuration & utilities for enterprise geolocation and tracking

export const GOOGLE_MAPS_API_KEY: string =
  (import.meta.env.VITE_GOOGLE_MAPS_API_KEY as string | undefined) ||
  'AIzaSyDVpMf1g9wbSUTF1I_jzRkMNrE-FbVMLF0';

// Official Google Maps Tile endpoints with 4 subdomains
export const GOOGLE_MAPS_TILES = {
  // lyrs=m: Standard Roadmap
  roadmap: `https://{s}.google.com/vt/lyrs=m&x={x}&y={y}&z={z}&key=${GOOGLE_MAPS_API_KEY}`,
  // lyrs=y: Hybrid (Satellite with street names and landmarks)
  hybrid: `https://{s}.google.com/vt/lyrs=y&x={x}&y={y}&z={z}&key=${GOOGLE_MAPS_API_KEY}`,
  // lyrs=s: Pure Satellite Imagery
  satellite: `https://{s}.google.com/vt/lyrs=s&x={x}&y={y}&z={z}&key=${GOOGLE_MAPS_API_KEY}`,
  // lyrs=p: Terrain & Topography
  terrain: `https://{s}.google.com/vt/lyrs=p&x={x}&y={y}&z={z}&key=${GOOGLE_MAPS_API_KEY}`,
};

export const GOOGLE_MAPS_TILE_OPTIONS = {
  subdomains: ['mt0', 'mt1', 'mt2', 'mt3'],
  maxZoom: 20,
  attribution: '&copy; Google Maps',
};

export function getGoogleTileUrl(
  type: 'roadmap' | 'satellite' | 'hybrid' | 'terrain',
  x: number,
  y: number,
  z: number
): string {
  const lyrs = type === 'satellite' ? 's' : type === 'hybrid' ? 'y' : type === 'terrain' ? 'p' : 'm';
  return `https://mt1.google.com/vt/lyrs=${lyrs}&x=${x}&y=${y}&z=${z}&key=${GOOGLE_MAPS_API_KEY}`;
}

let googleMapsScriptLoadingPromise: Promise<void> | null = null;

export function loadGoogleMapsScript(): Promise<void> {
  if (typeof window === 'undefined') return Promise.resolve();

  // Already loaded
  if ((window as unknown as { google?: { maps?: unknown } }).google?.maps) {
    return Promise.resolve();
  }

  if (googleMapsScriptLoadingPromise) {
    return googleMapsScriptLoadingPromise;
  }

  googleMapsScriptLoadingPromise = new Promise<void>((resolve, reject) => {
    // Check if script tag already in DOM
    const existing = document.getElementById('google-maps-js-sdk');
    if (existing) {
      existing.addEventListener('load', () => resolve());
      existing.addEventListener('error', (e) => reject(e));
      return;
    }

    const script = document.createElement('script');
    script.id = 'google-maps-js-sdk';
    script.src = `https://maps.googleapis.com/maps/api/js?key=${GOOGLE_MAPS_API_KEY}&libraries=places,geometry`;
    script.async = true;
    script.defer = true;
    script.onload = () => resolve();
    script.onerror = (err) => reject(err);
    document.head.appendChild(script);
  });

  return googleMapsScriptLoadingPromise;
}
