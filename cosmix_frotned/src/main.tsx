import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '@/app/App';
import '@/styles/tokens.css';
import '@/styles/global.css';
import '@/styles/components.css';
import '@/styles/auth.css';
import '@/styles/hr.css';

const root = document.getElementById('root');
if (!root) throw new Error('Application root is missing.');
createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
