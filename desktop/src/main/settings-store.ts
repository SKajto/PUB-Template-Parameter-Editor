import { randomUUID } from 'node:crypto';
import { mkdir, readFile, rename, rm, writeFile } from 'node:fs/promises';
import { dirname } from 'node:path';
import type { ThemePreference } from '../shared/types';

export interface AppSettings {
  folderPath: string | null;
  theme: ThemePreference;
}

const DEFAULT_SETTINGS: AppSettings = {
  folderPath: null,
  theme: 'system',
};

function isThemePreference(value: unknown): value is ThemePreference {
  return value === 'system' || value === 'light' || value === 'dark';
}

export async function loadSettings(
  filePath: string,
): Promise<{ settings: AppSettings; error: string | null }> {
  try {
    const text = await readFile(filePath, 'utf8');
    const parsed: unknown = JSON.parse(text);
    if (typeof parsed !== 'object' || parsed === null) {
      throw new Error('Settings must contain a JSON object.');
    }
    const record = parsed as Record<string, unknown>;
    const folderPath =
      typeof record.folderPath === 'string' ? record.folderPath : null;
    const theme = isThemePreference(record.theme) ? record.theme : 'system';
    return { settings: { folderPath, theme }, error: null };
  } catch (error) {
    if (
      typeof error === 'object' &&
      error !== null &&
      'code' in error &&
      error.code === 'ENOENT'
    ) {
      return { settings: { ...DEFAULT_SETTINGS }, error: null };
    }
    return {
      settings: { ...DEFAULT_SETTINGS },
      error:
        'Saved preferences could not be read; default settings are in use.',
    };
  }
}

export async function saveSettings(
  filePath: string,
  settings: AppSettings,
): Promise<void> {
  const temporaryPath = `${filePath}.${randomUUID()}.tmp`;
  await mkdir(dirname(filePath), { recursive: true });
  try {
    const serialized = JSON.stringify(settings, null, 2);
    await writeFile(temporaryPath, serialized, {
      encoding: 'utf8',
      flag: 'wx',
    });
    await rename(temporaryPath, filePath);
  } catch (error) {
    await rm(temporaryPath, { force: true }).catch(() => undefined);
    throw error;
  }
}
