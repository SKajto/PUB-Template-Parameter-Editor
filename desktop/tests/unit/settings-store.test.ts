import assert from 'node:assert/strict';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, before, describe, it } from 'node:test';
import { loadSettings, saveSettings } from '../../src/main/settings-store';

let directory: string;
let settingsPath: string;

before(async () => {
  directory = await mkdtemp(join(tmpdir(), 'pub-editor-settings-test-'));
  settingsPath = join(directory, 'settings.json');
});

after(async () => {
  await rm(directory, { recursive: true, force: true });
});

describe('settings store', () => {
  it('uses system theme and no folder when settings do not exist', async () => {
    const result = await loadSettings(settingsPath);
    assert.deepEqual(result, {
      settings: { folderPath: null, theme: 'system' },
      error: null,
    });
  });

  it('persists folder and theme preferences atomically', async () => {
    const expected = { folderPath: 'C:/templates', theme: 'dark' as const };
    await saveSettings(settingsPath, expected);

    const result = await loadSettings(settingsPath);
    assert.deepEqual(result, { settings: expected, error: null });
  });

  it('falls back to safe defaults for malformed preferences', async () => {
    await saveSettings(settingsPath, { folderPath: null, theme: 'light' });
    const { writeFile } = await import('node:fs/promises');
    await writeFile(settingsPath, '{broken json', 'utf8');

    const result = await loadSettings(settingsPath);
    assert.deepEqual(result, {
      settings: { folderPath: null, theme: 'system' },
      error:
        'Saved preferences could not be read; default settings are in use.',
    });
  });
});
