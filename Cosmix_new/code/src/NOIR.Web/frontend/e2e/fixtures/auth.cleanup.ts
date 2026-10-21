import { test as teardown } from '@playwright/test';
import fs from 'fs';
import path from 'path';

teardown('cleanup auth state', async () => {
  const authDir = path.join(__dirname, '..', '.auth');
  if (fs.existsSync(authDir)) {
    for (const file of fs.readdirSync(authDir)) {
      if (file.endsWith('.json')) {
        fs.unlinkSync(path.join(authDir, file));
      }
    }
  }
});
