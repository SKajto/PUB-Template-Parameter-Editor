import { randomUUID, createHash } from 'node:crypto';
import {
  access,
  chmod,
  copyFile,
  lstat,
  mkdir,
  open,
  readFile,
  readdir,
  rename,
  stat,
  unlink,
} from 'node:fs/promises';
import { constants } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import type {
  InterruptedSaveBatchSummary,
  InterruptedSaveRecoveryResult,
  SaveFileOutcome,
  SaveFileStatus,
} from '../shared/types';

export interface SaveBatchItem {
  fileId: string;
  fileName: string;
  path: string;
  baselineFingerprint: string;
  outputBytes: Buffer;
}

export interface SaveBatchResult {
  ok: boolean;
  batchId: string;
  journalPath: string;
  error: string | null;
  outcomes: SaveFileOutcome[];
}

interface PreparedFile {
  item: SaveBatchItem;
  backupPath: string;
  temporaryPath: string;
  originalMode: number;
  status: SaveFileStatus;
  message: string;
}

interface SaveJournal {
  version: 1;
  batchId: string;
  startedAt: string;
  completedAt: string | null;
  state: 'preparing' | 'replacing' | 'complete' | 'recovery-needed';
  files: Array<{
    fileId: string;
    fileName: string;
    path: string;
    backupPath: string;
    temporaryPath: string;
    baselineFingerprint: string;
    outputFingerprint: string;
    status: SaveFileStatus;
    message: string;
  }>;
}

export interface SaveBatchOptions {
  replaceFile?: typeof rename;
  chmodFile?: typeof chmod;
}

export type SaveRecoveryResult = Omit<
  InterruptedSaveRecoveryResult,
  'workspace'
>;

const UUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/iu;

function fingerprint(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}

async function writeJournal(path: string, journal: SaveJournal): Promise<void> {
  const temporaryPath = `${path}.${randomUUID()}.tmp`;
  try {
    await writeSyncedFile(
      temporaryPath,
      Buffer.from(JSON.stringify(journal, null, 2), 'utf8'),
      0o600,
    );
    await rename(temporaryPath, path);
  } catch (error) {
    await unlink(temporaryPath).catch(() => undefined);
    throw error;
  }
}

async function syncFile(path: string): Promise<void> {
  const file = await open(path, 'r+');
  try {
    await file.sync();
  } finally {
    await file.close();
  }
}

async function writeSyncedFile(
  path: string,
  bytes: Buffer,
  mode: number,
): Promise<void> {
  const file = await open(path, 'wx', mode);
  try {
    await file.writeFile(bytes);
    await file.sync();
  } finally {
    await file.close();
  }
}

async function chmodIfNeeded(
  path: string,
  mode: number,
  changeMode: typeof chmod = chmod,
): Promise<void> {
  const requestedMode = mode & 0o777;
  const currentMode = (await stat(path)).mode & 0o777;
  if (currentMode !== requestedMode) {
    await changeMode(path, requestedMode);
  }
}

async function diskFingerprint(path: string): Promise<string> {
  return fingerprint(await readFile(path));
}

function isSaveFileStatus(value: unknown): value is SaveFileStatus {
  return (
    value === 'not-written' ||
    value === 'saved' ||
    value === 'restored' ||
    value === 'failed' ||
    value === 'uncertain'
  );
}

function parseSaveJournal(value: unknown, batchId: string): SaveJournal | null {
  if (value === null || typeof value !== 'object') return null;
  const candidate = value as Record<string, unknown>;
  if (
    candidate.version !== 1 ||
    candidate.batchId !== batchId ||
    typeof candidate.startedAt !== 'string' ||
    (candidate.completedAt !== null &&
      typeof candidate.completedAt !== 'string') ||
    !['preparing', 'replacing', 'complete', 'recovery-needed'].includes(
      String(candidate.state),
    ) ||
    !Array.isArray(candidate.files)
  ) {
    return null;
  }
  const files: SaveJournal['files'] = [];
  for (const rawFile of candidate.files) {
    if (rawFile === null || typeof rawFile !== 'object') return null;
    const file = rawFile as Record<string, unknown>;
    if (
      typeof file.fileId !== 'string' ||
      typeof file.fileName !== 'string' ||
      typeof file.path !== 'string' ||
      typeof file.backupPath !== 'string' ||
      typeof file.temporaryPath !== 'string' ||
      typeof file.baselineFingerprint !== 'string' ||
      !/^[0-9a-f]{64}$/iu.test(file.baselineFingerprint) ||
      typeof file.outputFingerprint !== 'string' ||
      !/^[0-9a-f]{64}$/iu.test(file.outputFingerprint) ||
      !isSaveFileStatus(file.status) ||
      typeof file.message !== 'string'
    ) {
      return null;
    }
    files.push({
      fileId: file.fileId,
      fileName: file.fileName,
      path: file.path,
      backupPath: file.backupPath,
      temporaryPath: file.temporaryPath,
      baselineFingerprint: file.baselineFingerprint,
      outputFingerprint: file.outputFingerprint,
      status: file.status,
      message: file.message,
    });
  }
  return {
    version: 1,
    batchId,
    startedAt: candidate.startedAt,
    completedAt: candidate.completedAt as string | null,
    state: candidate.state as SaveJournal['state'],
    files,
  };
}

function normalizedPath(path: string): string {
  const absolute = resolve(path);
  return process.platform === 'win32'
    ? absolute.toLocaleLowerCase('en-US')
    : absolute;
}

function hasTrustedBatchPaths(
  journal: SaveJournal,
  batchDirectory: string,
  allowedPaths: ReadonlySet<string>,
): boolean {
  const normalizedAllowedPaths = new Set([...allowedPaths].map(normalizedPath));
  return (
    journal.files.length > 0 &&
    journal.files.every(
      (file) =>
        UUID_PATTERN.test(file.fileId) &&
        normalizedAllowedPaths.has(normalizedPath(file.path)) &&
        normalizedPath(file.backupPath) ===
          normalizedPath(join(batchDirectory, `${file.fileId}.original`)) &&
        normalizedPath(file.temporaryPath) ===
          normalizedPath(`${file.path}.dta-editor-${journal.batchId}.tmp`),
    )
  );
}

/** Finds unresolved journals. Recovery is enabled only for registered paths. */
export async function listInterruptedSaveBatches(
  recoveryRoot: string,
  allowedPaths: ReadonlySet<string>,
): Promise<InterruptedSaveBatchSummary[]> {
  let entries;
  try {
    entries = await readdir(recoveryRoot, { withFileTypes: true });
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') return [];
    return [
      {
        batchId: 'recovery-unavailable',
        startedAt: new Date().toISOString(),
        files: [],
        canRecover: false,
        reason:
          'Interrupted-save records could not be inspected. Do not save until this is resolved.',
      },
    ];
  }

  const summaries: InterruptedSaveBatchSummary[] = [];
  for (const entry of entries) {
    if (!UUID_PATTERN.test(entry.name)) continue;
    const batchDirectory = join(recoveryRoot, entry.name);
    try {
      const directoryInfo = await lstat(batchDirectory);
      if (!directoryInfo.isDirectory() || directoryInfo.isSymbolicLink())
        continue;
      const journalPath = join(batchDirectory, 'journal.json');
      const journalInfo = await lstat(journalPath);
      if (!journalInfo.isFile() || journalInfo.isSymbolicLink()) {
        summaries.push({
          batchId: entry.name,
          startedAt: new Date().toISOString(),
          files: [],
          canRecover: false,
          reason:
            'The interrupted-save journal is not a regular file. Inspect the retained backup folder manually.',
        });
        continue;
      }
      const raw = JSON.parse(await readFile(journalPath, 'utf8')) as unknown;
      const journal = parseSaveJournal(raw, entry.name);
      if (journal === null) {
        summaries.push({
          batchId: entry.name,
          startedAt: new Date().toISOString(),
          files: [],
          canRecover: false,
          reason:
            'The interrupted-save journal is invalid or unsupported. Inspect the retained backup folder manually.',
        });
        continue;
      }
      if (journal.state === 'complete' || journal.state === 'preparing')
        continue;
      const trustedPaths = hasTrustedBatchPaths(
        journal,
        batchDirectory,
        allowedPaths,
      );
      summaries.push({
        batchId: journal.batchId,
        startedAt: journal.startedAt,
        files: journal.files.map(({ fileId, fileName, status, message }) => ({
          fileId,
          fileName,
          status,
          message,
        })),
        canRecover: trustedPaths,
        reason: trustedPaths
          ? null
          : 'Select the folder containing the original templates before recovery can run safely.',
      });
    } catch {
      summaries.push({
        batchId: entry.name,
        startedAt: new Date().toISOString(),
        files: [],
        canRecover: false,
        reason:
          'The interrupted-save journal could not be verified. Inspect the retained backup folder manually.',
      });
    }
  }
  return summaries.sort((left, right) =>
    right.startedAt.localeCompare(left.startedAt),
  );
}

/** Restores only when disk still matches the exact save output; unknown edits stay untouched. */
export async function recoverInterruptedSaveBatch(
  recoveryRoot: string,
  batchId: string,
  allowedPaths: ReadonlySet<string>,
): Promise<SaveRecoveryResult> {
  const failure = (error: string): SaveRecoveryResult => ({
    ok: false,
    batchId,
    error,
    outcomes: [],
  });
  if (!UUID_PATTERN.test(batchId)) return failure('Invalid recovery batch ID.');
  const batchDirectory = join(recoveryRoot, batchId);
  const journalPath = join(batchDirectory, 'journal.json');
  let journal: SaveJournal | null;
  try {
    const directoryInfo = await lstat(batchDirectory);
    const journalInfo = await lstat(journalPath);
    if (
      !directoryInfo.isDirectory() ||
      directoryInfo.isSymbolicLink() ||
      !journalInfo.isFile() ||
      journalInfo.isSymbolicLink()
    ) {
      return failure(
        'Recovery journal paths are not regular application files.',
      );
    }
    journal = parseSaveJournal(
      JSON.parse(await readFile(journalPath, 'utf8')) as unknown,
      batchId,
    );
  } catch (error) {
    return failure(
      `Could not read the interrupted-save journal: ${error instanceof Error ? error.message : String(error)}`,
    );
  }
  if (
    journal === null ||
    (journal.state !== 'replacing' && journal.state !== 'recovery-needed')
  ) {
    return failure('This save journal is not eligible for recovery.');
  }
  if (!hasTrustedBatchPaths(journal, batchDirectory, allowedPaths)) {
    return failure(
      'The original template folder is not selected or the journal paths failed validation.',
    );
  }

  const outcomes: SaveFileOutcome[] = [];
  for (const file of journal.files) {
    let status: SaveFileStatus = 'uncertain';
    let message = 'Recovery could not verify the current file state.';
    try {
      const currentFingerprint = await diskFingerprint(file.path);
      if (currentFingerprint === file.baselineFingerprint) {
        status =
          file.status === 'not-written' || file.status === 'failed'
            ? 'not-written'
            : 'restored';
        message = 'Original baseline verified; no restore write was needed.';
      } else if (currentFingerprint !== file.outputFingerprint) {
        status = 'uncertain';
        message =
          'Disk content matches neither the original nor prepared output; it was left untouched.';
      } else {
        const backupInfo = await lstat(file.backupPath);
        if (!backupInfo.isFile() || backupInfo.isSymbolicLink()) {
          throw new Error('The original backup is not a regular file.');
        }
        if (
          (await diskFingerprint(file.backupPath)) !== file.baselineFingerprint
        ) {
          throw new Error('The original backup fingerprint does not match.');
        }
        const restorePath = `${file.path}.dta-editor-${batchId}-${randomUUID()}.restore`;
        try {
          await copyFile(file.backupPath, restorePath, constants.COPYFILE_EXCL);
          await chmodIfNeeded(restorePath, backupInfo.mode);
          if ((await diskFingerprint(file.path)) !== file.outputFingerprint) {
            throw new Error(
              'The template changed during recovery; it was not replaced.',
            );
          }
          await rename(restorePath, file.path);
        } catch (error) {
          await unlink(restorePath).catch(() => undefined);
          throw error;
        }
        if ((await diskFingerprint(file.path)) !== file.baselineFingerprint) {
          throw new Error('The restored original could not be verified.');
        }
        status = 'restored';
        message = 'Restored and verified from the original backup.';
      }
    } catch (error) {
      status = 'uncertain';
      message = `Recovery could not safely restore this file: ${error instanceof Error ? error.message : String(error)}`;
    }
    file.status = status;
    file.message = message;
    outcomes.push({
      fileId: file.fileId,
      fileName: file.fileName,
      status,
      message,
    });
  }

  const unresolved = outcomes.some((outcome) => outcome.status === 'uncertain');
  journal.state = unresolved ? 'recovery-needed' : 'complete';
  journal.completedAt = unresolved ? null : new Date().toISOString();
  try {
    await writeJournal(journalPath, journal);
  } catch (error) {
    return {
      ok: false,
      batchId,
      error: `Files were checked, but recovery status could not be journaled: ${error instanceof Error ? error.message : String(error)}`,
      outcomes,
    };
  }
  return {
    ok: !unresolved,
    batchId,
    error: unresolved
      ? 'Some files remain uncertain and were left untouched; inspect the per-file results.'
      : null,
    outcomes,
  };
}

function toOutcomes(files: PreparedFile[]): SaveFileOutcome[] {
  return files.map(({ item, status, message }) => ({
    fileId: item.fileId,
    fileName: item.fileName,
    status,
    message,
  }));
}

/**
 * Saves prepared DTA buffers individually. Temp files are beside originals for
 * same-volume rename; original backups and a journal are retained in app data.
 * The batch is not globally atomic. On a replacement failure, already-written
 * files are restored and verified where possible.
 */
export async function saveBatch(
  items: SaveBatchItem[],
  recoveryRoot: string,
  options: SaveBatchOptions = {},
): Promise<SaveBatchResult> {
  const batchId = randomUUID();
  const batchDirectory = join(recoveryRoot, batchId);
  const journalPath = join(batchDirectory, 'journal.json');
  const files: PreparedFile[] = [];
  const replaceFile = options.replaceFile ?? rename;
  const chmodFile = options.chmodFile ?? chmod;
  const seenIds = new Set<string>();
  const seenPaths = new Set<string>();
  let journal: SaveJournal = {
    version: 1,
    batchId,
    startedAt: new Date().toISOString(),
    completedAt: null,
    state: 'preparing',
    files: [],
  };

  if (items.length === 0) {
    return {
      ok: false,
      batchId,
      journalPath,
      error: 'No files were selected for delivery.',
      outcomes: [],
    };
  }

  try {
    for (const item of items) {
      if (seenIds.has(item.fileId) || seenPaths.has(item.path)) {
        throw new Error('The save batch contains duplicate template targets.');
      }
      seenIds.add(item.fileId);
      seenPaths.add(item.path);
      const backupPath = join(batchDirectory, `${item.fileId}.original`);
      const temporaryPath = `${item.path}.dta-editor-${batchId}.tmp`;
      const prepared: PreparedFile = {
        item,
        backupPath,
        temporaryPath,
        originalMode: 0o666,
        status: 'not-written',
        message: 'Preflight has not completed.',
      };
      files.push(prepared);
      const info = await stat(item.path);
      if (!info.isFile())
        throw new Error(`${item.fileName} is not a regular file.`);
      prepared.originalMode = info.mode & 0o777;
      await access(item.path, constants.R_OK | constants.W_OK);
      const original = await readFile(item.path);
      if (fingerprint(original) !== item.baselineFingerprint) {
        prepared.message = 'Disk content differs from the loaded baseline.';
        throw new Error(`${item.fileName} changed on disk since it was read.`);
      }
      prepared.message = 'Preflight passed; original not yet replaced.';
    }

    await mkdir(batchDirectory, { recursive: true });
    for (const file of files) {
      await copyFile(file.item.path, file.backupPath, constants.COPYFILE_EXCL);
      await syncFile(file.backupPath);
      if (
        (await diskFingerprint(file.backupPath)) !==
        file.item.baselineFingerprint
      ) {
        throw new Error(
          `Backup verification failed for ${file.item.fileName}.`,
        );
      }
      await writeSyncedFile(
        file.temporaryPath,
        file.item.outputBytes,
        file.originalMode,
      );
      await chmodIfNeeded(file.temporaryPath, file.originalMode, chmodFile);
    }

    journal.files = files.map((file) => ({
      fileId: file.item.fileId,
      fileName: file.item.fileName,
      path: file.item.path,
      backupPath: file.backupPath,
      temporaryPath: file.temporaryPath,
      baselineFingerprint: file.item.baselineFingerprint,
      outputFingerprint: fingerprint(file.item.outputBytes),
      status: file.status,
      message: file.message,
    }));
    await writeJournal(journalPath, journal);

    for (const file of files) {
      if (
        (await diskFingerprint(file.item.path)) !==
        file.item.baselineFingerprint
      ) {
        throw new Error(
          `${file.item.fileName} changed immediately before replacement.`,
        );
      }
    }

    journal.state = 'replacing';
    await writeJournal(journalPath, journal);
    for (const file of files) {
      file.status = 'uncertain';
      file.message = 'Replacement started; disk state is not yet verified.';
      const pendingJournalEntry = journal.files.find(
        (entry) => entry.fileId === file.item.fileId,
      );
      if (pendingJournalEntry) {
        pendingJournalEntry.status = file.status;
        pendingJournalEntry.message = file.message;
      }
      await writeJournal(journalPath, journal);
      await replaceFile(file.temporaryPath, file.item.path);
      const actual = await diskFingerprint(file.item.path);
      const expected = fingerprint(file.item.outputBytes);
      if (actual !== expected) {
        file.status = 'uncertain';
        file.message = 'Replacement returned, but output verification failed.';
        throw new Error(
          `Saved output verification failed for ${file.item.fileName}.`,
        );
      }
      file.status = 'saved';
      file.message = 'Saved and verified.';
      const journalEntry = journal.files.find(
        (entry) => entry.fileId === file.item.fileId,
      );
      if (journalEntry) {
        journalEntry.status = file.status;
        journalEntry.message = file.message;
      }
      await writeJournal(journalPath, journal);
    }

    journal.state = 'complete';
    journal.completedAt = new Date().toISOString();
    await writeJournal(journalPath, journal);
    return {
      ok: true,
      batchId,
      journalPath,
      error: null,
      outcomes: toOutcomes(files),
    };
  } catch (error) {
    const message =
      error instanceof Error ? error.message : 'Unknown save failure.';
    let recoveryNeeded = false;
    for (const file of [...files].reverse()) {
      if (file.status !== 'saved' && file.status !== 'uncertain') continue;
      try {
        const actual = await diskFingerprint(file.item.path);
        const outputFingerprint = fingerprint(file.item.outputBytes);
        if (actual === file.item.baselineFingerprint) {
          file.status = 'failed';
          file.message = 'Original remained unchanged.';
          continue;
        }
        if (actual !== outputFingerprint) {
          file.status = 'uncertain';
          file.message =
            'Disk content matches neither the original nor prepared output.';
          recoveryNeeded = true;
          continue;
        }
        const restorePath = `${file.item.path}.dta-editor-${batchId}.restore`;
        await copyFile(file.backupPath, restorePath, constants.COPYFILE_EXCL);
        await chmodIfNeeded(restorePath, file.originalMode, chmodFile);
        await replaceFile(restorePath, file.item.path);
        if (
          (await diskFingerprint(file.item.path)) !==
          file.item.baselineFingerprint
        ) {
          file.status = 'uncertain';
          file.message = 'Backup restoration ran but could not be verified.';
          recoveryNeeded = true;
        } else {
          file.status = 'restored';
          file.message = 'Restored from the verified original backup.';
        }
      } catch (recoveryError) {
        file.status = 'uncertain';
        file.message = `Recovery failed: ${recoveryError instanceof Error ? recoveryError.message : String(recoveryError)}`;
        recoveryNeeded = true;
      }
    }

    for (const file of files) {
      if (file.status === 'not-written') {
        file.status = 'failed';
        if (file.message === 'Preflight has not completed.') {
          file.message = 'No replacement was attempted.';
        }
      }
    }
    journal.state = recoveryNeeded ? 'recovery-needed' : 'complete';
    journal.completedAt = new Date().toISOString();
    journal.files = files.map((file) => ({
      fileId: file.item.fileId,
      fileName: file.item.fileName,
      path: file.item.path,
      backupPath: file.backupPath,
      temporaryPath: file.temporaryPath,
      baselineFingerprint: file.item.baselineFingerprint,
      outputFingerprint: fingerprint(file.item.outputBytes),
      status: file.status,
      message: file.message,
    }));
    try {
      await mkdir(dirname(journalPath), { recursive: true });
      await writeJournal(journalPath, journal);
    } catch {
      recoveryNeeded = true;
    }
    return {
      ok: false,
      batchId,
      journalPath,
      error: recoveryNeeded
        ? `${message} Recovery is incomplete; keep the workspace and backup journal.`
        : message,
      outcomes: toOutcomes(files),
    };
  }
}
