import { http, HttpResponse } from 'msw'

// Base API URL
const API_BASE = '/api'

export const handlers = [
  // Health check
  http.get(`${API_BASE}/health`, () => {
    return HttpResponse.json({ status: 'healthy' })
  }),
]
