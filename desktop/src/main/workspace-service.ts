import { createHash, randomUUID } from 'node:crypto';
import { constants } from 'node:fs';
import { access, readdir, realpath, stat } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { dialog, ipcMain, nativeTheme } from 'electron';
import type {
  ReadProgress,
  ReadTemplateResult,
  InterruptedSaveRecoveryResult,
  ReviewedFieldChange,
  ReviewedSaveResult,
  EditableField,
  TemplateFile,
  ThemePreference,
  WorkspaceState,
} from '../shared/types';
import { loadSettings, saveSettings } from './settings-store';
import { parseXmlSource, patchXmlSource } from '../core/xml-source-patcher';
import { replaceNativeTemplateXml } from '../core/dta-zip-writer';
import {
  listInterruptedSaveBatches,
  recoverInterruptedSaveBatch,
  saveBatch,
} from './save-service';
import {
  readTemplateSnapshot,
  type RegisteredTemplate,
  type TemplateReadSnapshot,
} from './template-reader';

const MAX_READ_BATCH = 500;
const SETTINGS_FILENAME = 'settings.json';

function isThemePreference(value: unknown): value is ThemePreference {
  return value === 'system' || value === 'light' || value === 'dark';
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Unknown filesystem error.';
}

function isEditableField(value: unknown): value is EditableField {
  return (
    value === 'access' || value === 'defaultValue' || value === 'description'
  );
}

function archiveFingerprint(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}

export function registerWorkspaceHandlers(settingsPath: string): void {
  const registeredFiles = new Map<string, RegisteredTemplate>();
  const templateSnapshots = new Map<string, TemplateReadSnapshot>();
  const recoveryRoot = join(dirname(settingsPath), 'save-recovery');
  let currentWorkspaceState: WorkspaceState | null = null;

  const currentSettings = async () => loadSettings(settingsPath);
  const interruptedBatches = async () =>
    listInterruptedSaveBatches(
      recoveryRoot,
      new Set([...registeredFiles.values()].map((file) => file.path)),
    );
  const remember = (state: WorkspaceState): WorkspaceState => {
    currentWorkspaceState = state;
    return state;
  };

  async function buildWorkspaceState(): Promise<WorkspaceState> {
    const { settings, error: settingsError } = await currentSettings();
    nativeTheme.themeSource = settings.theme;
    registeredFiles.clear();
    templateSnapshots.clear();

    if (settings.folderPath === null) {
      return remember({
        folderPath: null,
        files: [],
        theme: settings.theme,
        error: settingsError,
        recoveryBatches: await interruptedBatches(),
      });
    }

    let folderPath: string;
    try {
      folderPath = await realpath(settings.folderPath);
      const folderStat = await stat(folderPath);
      if (!folderStat.isDirectory())
        throw new Error('The remembered path is not a folder.');
    } catch {
      return remember({
        folderPath: settings.folderPath,
        files: [],
        theme: settings.theme,
        error:
          'The remembered folder is unavailable. Choose another folder to continue.',
        recoveryBatches: await interruptedBatches(),
      });
    }

    let entries;
    try {
      entries = await readdir(folderPath, { withFileTypes: true });
    } catch (error) {
      return remember({
        folderPath,
        files: [],
        theme: settings.theme,
        error: `The selected folder could not be listed: ${errorMessage(error)}`,
        recoveryBatches: await interruptedBatches(),
      });
    }

    const candidateNames = entries
      .filter(
        (entry) => entry.isFile() && entry.name.toLowerCase().endsWith('.dta'),
      )
      .map((entry) => entry.name)
      .sort((left, right) =>
        left.localeCompare(right, undefined, {
          numeric: true,
          sensitivity: 'base',
        }),
      );

    const files: TemplateFile[] = [];
    for (const name of candidateNames) {
      const id = randomUUID();
      const filePath = join(folderPath, name);
      try {
        const info = await stat(filePath);
        await access(filePath, constants.R_OK);
        const registered: RegisteredTemplate = {
          id,
          name,
          size: info.size,
          modifiedAt: info.mtimeMs,
          error: null,
          path: filePath,
        };
        registeredFiles.set(id, registered);
        files.push({
          id,
          name,
          size: info.size,
          modifiedAt: info.mtimeMs,
          error: null,
        });
      } catch (error) {
        const message = `File is not readable: ${errorMessage(error)}`;
        const registered: RegisteredTemplate = {
          id,
          name,
          size: 0,
          modifiedAt: 0,
          error: message,
          path: filePath,
        };
        registeredFiles.set(id, registered);
        files.push({ id, name, size: 0, modifiedAt: 0, error: message });
      }
    }

    return remember({
      folderPath,
      files,
      theme: settings.theme,
      error: settingsError,
      recoveryBatches: await interruptedBatches(),
    });
  }

  ipcMain.handle('workspace:get-initial-state', buildWorkspaceState);

  ipcMain.handle('workspace:choose-folder', async () => {
    const result = await dialog.showOpenDialog({
      properties: ['openDirectory'],
      title: 'Choose a folder containing PUB templates',
    });
    if (result.canceled || result.filePaths.length === 0) {
      return currentWorkspaceState ?? buildWorkspaceState();
    }

    const { settings } = await currentSettings();
    await saveSettings(settingsPath, {
      ...settings,
      folderPath: result.filePaths[0],
    });
    return buildWorkspaceState();
  });

  ipcMain.handle('workspace:set-theme', async (_event, theme: unknown) => {
    if (!isThemePreference(theme))
      throw new Error('Unsupported theme preference.');
    const { settings } = await currentSettings();
    await saveSettings(settingsPath, { ...settings, theme });
    nativeTheme.themeSource = theme;
    if (currentWorkspaceState !== null) {
      return remember({ ...currentWorkspaceState, theme });
    }
    return buildWorkspaceState();
  });

  ipcMain.handle('workspace:read-templates', async (event, ids: unknown) => {
    if ((await interruptedBatches()).length > 0) {
      throw new Error(
        'An interrupted save must be recovered before reading templates.',
      );
    }
    if (!Array.isArray(ids) || ids.some((id) => typeof id !== 'string')) {
      throw new Error('Template selection is invalid.');
    }
    const uniqueIds = [...new Set(ids as string[])];
    if (uniqueIds.length > MAX_READ_BATCH) {
      throw new Error(`Read at most ${MAX_READ_BATCH} templates at a time.`);
    }

    const results: ReadTemplateResult[] = [];
    for (const [index, id] of uniqueIds.entries()) {
      const file = registeredFiles.get(id);
      const snapshot = file ? await readTemplateSnapshot(file) : null;
      const result = snapshot?.result ?? {
        fileId: id,
        fileName: 'Unknown template',
        declarations: [],
        error:
          'The template is no longer in the current folder listing. Refresh the folder.',
      };
      if (snapshot?.result.error === null) templateSnapshots.set(id, snapshot);
      else templateSnapshots.delete(id);
      results.push(result);
      const progress: ReadProgress = {
        completed: index + 1,
        total: uniqueIds.length,
        fileName: result.fileName,
      };
      if (!event.sender.isDestroyed()) {
        event.sender.send('workspace:read-progress', progress);
      }
    }
    return results;
  });

  ipcMain.handle(
    'workspace:save-reviewed',
    async (_event, rawChanges: unknown): Promise<ReviewedSaveResult> => {
      const failure = (error: string): ReviewedSaveResult => ({
        ok: false,
        batchId: '',
        journalPath: '',
        error,
        outcomes: [],
        updatedTemplates: [],
      });

      if ((await interruptedBatches()).length > 0) {
        return failure(
          'An interrupted save must be recovered before another save.',
        );
      }

      if (!Array.isArray(rawChanges) || rawChanges.length > 20_000) {
        return failure('The review selection is invalid or too large.');
      }
      const changes: ReviewedFieldChange[] = [];
      const seen = new Set<string>();
      for (const rawChange of rawChanges) {
        if (rawChange === null || typeof rawChange !== 'object') {
          return failure('A reviewed field change is invalid.');
        }
        const change = rawChange as Record<string, unknown>;
        if (
          typeof change.fileId !== 'string' ||
          typeof change.declarationId !== 'string' ||
          !isEditableField(change.field) ||
          typeof change.proposed !== 'string' ||
          typeof change.included !== 'boolean'
        ) {
          return failure('A reviewed field change has invalid fields.');
        }
        const key = `${change.fileId}\u0000${change.declarationId}\u0000${change.field}`;
        if (seen.has(key))
          return failure('The review contains duplicate fields.');
        seen.add(key);
        changes.push({
          fileId: change.fileId,
          declarationId: change.declarationId,
          field: change.field,
          proposed: change.proposed,
          included: change.included,
        });
      }

      try {
        const grouped = new Map<string, ReviewedFieldChange[]>();
        for (const change of changes) {
          const file = registeredFiles.get(change.fileId);
          const snapshot = templateSnapshots.get(change.fileId);
          if (
            !file ||
            !snapshot ||
            snapshot.result.error !== null ||
            snapshot.archiveBytes === null ||
            snapshot.templateXml === null ||
            snapshot.fingerprint === null
          ) {
            throw new Error(
              'A reviewed template is no longer loaded. Read it again.',
            );
          }
          if (!change.included) continue;
          const group = grouped.get(change.fileId) ?? [];
          group.push(change);
          grouped.set(change.fileId, group);
        }

        if (grouped.size === 0) {
          throw new Error('Select at least one field change to save.');
        }

        const saveItems = [];
        const outputByFile = new Map<
          string,
          { archiveBytes: Buffer; templateXml: Buffer }
        >();
        for (const [fileId, fileChanges] of grouped) {
          const file = registeredFiles.get(fileId)!;
          const snapshot = templateSnapshots.get(fileId)!;
          const xml = new TextDecoder('utf-8', { fatal: true }).decode(
            snapshot.templateXml!,
          );
          const patchedXml = patchXmlSource(
            xml,
            fileChanges.map((change) => ({
              declarationId: change.declarationId,
              field: change.field,
              value: change.proposed,
            })),
          );
          const templateXml = Buffer.from(patchedXml, 'utf8');
          const archiveBytes = replaceNativeTemplateXml(
            snapshot.archiveBytes!,
            templateXml,
          );
          if (archiveBytes.equals(snapshot.archiveBytes!)) continue;
          outputByFile.set(fileId, { archiveBytes, templateXml });
          saveItems.push({
            fileId,
            fileName: file.name,
            path: file.path,
            baselineFingerprint: snapshot.fingerprint!,
            outputBytes: archiveBytes,
          });
        }
        if (saveItems.length === 0) {
          throw new Error(
            'All included fields already match the loaded baseline.',
          );
        }

        const result = await saveBatch(
          saveItems,
          join(dirname(settingsPath), 'save-recovery'),
        );
        if (!result.ok) {
          return {
            ...result,
            updatedTemplates: [],
          };
        }

        const updatedTemplates: ReadTemplateResult[] = [];
        const updatedFiles = new Map<string, TemplateFile>();
        for (const item of saveItems) {
          const output = outputByFile.get(item.fileId)!;
          const sourceModel = parseXmlSource(
            new TextDecoder('utf-8', { fatal: true }).decode(
              output.templateXml,
            ),
          );
          const result: ReadTemplateResult = {
            fileId: item.fileId,
            fileName: item.fileName,
            declarations: sourceModel.declarations.map(
              (declaration, index) => ({
                ...declaration,
                editableFields: sourceModel.editableFields[index],
              }),
            ),
            error: null,
          };
          updatedTemplates.push(result);
          templateSnapshots.set(item.fileId, {
            result,
            archiveBytes: output.archiveBytes,
            templateXml: output.templateXml,
            fingerprint: archiveFingerprint(output.archiveBytes),
          });
          const registered = registeredFiles.get(item.fileId)!;
          const info = await stat(registered.path);
          const refreshed: RegisteredTemplate = {
            ...registered,
            size: info.size,
            modifiedAt: info.mtimeMs,
          };
          registeredFiles.set(item.fileId, refreshed);
          updatedFiles.set(item.fileId, {
            id: refreshed.id,
            name: refreshed.name,
            size: refreshed.size,
            modifiedAt: refreshed.modifiedAt,
            error: refreshed.error,
          });
        }
        if (currentWorkspaceState !== null) {
          remember({
            ...currentWorkspaceState,
            files: currentWorkspaceState.files.map(
              (file) => updatedFiles.get(file.id) ?? file,
            ),
          });
        }
        return { ...result, updatedTemplates };
      } catch (error) {
        return failure(errorMessage(error));
      }
    },
  );

  ipcMain.handle(
    'workspace:recover-interrupted-save',
    async (
      _event,
      rawBatchId: unknown,
    ): Promise<InterruptedSaveRecoveryResult> => {
      const state = currentWorkspaceState ?? (await buildWorkspaceState());
      const batchId = typeof rawBatchId === 'string' ? rawBatchId : '';
      const failure = (error: string): InterruptedSaveRecoveryResult => ({
        ok: false,
        batchId,
        error,
        outcomes: [],
        workspace: state,
      });
      const summary = (await interruptedBatches()).find(
        (batch) => batch.batchId === batchId,
      );
      if (!summary)
        return failure('That interrupted save is no longer listed.');
      if (!summary.canRecover) {
        return failure(
          summary.reason ?? 'This interrupted save cannot be recovered safely.',
        );
      }
      if (templateSnapshots.size > 0) {
        return failure(
          'Restart the application before recovering; template reads are active in this session.',
        );
      }
      const recovered = await recoverInterruptedSaveBatch(
        recoveryRoot,
        batchId,
        new Set([...registeredFiles.values()].map((file) => file.path)),
      );
      const workspace = await buildWorkspaceState();
      return { ...recovered, workspace };
    },
  );
}

export function getSettingsPath(userDataPath: string): string {
  return join(userDataPath, SETTINGS_FILENAME);
}
