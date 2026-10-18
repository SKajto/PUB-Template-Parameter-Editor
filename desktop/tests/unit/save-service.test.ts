import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import {
  mkdtemp,
  mkdir,
  readFile,
  rename,
  rm,
  stat,
  writeFile,
} from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, it } from 'node:test';
import {
  listInterruptedSaveBatches,
  recoverInterruptedSaveBatch,
  saveBatch,
  type SaveBatchItem,
} from '../../src/main/save-service';

const temporaryRoots: string[] = [];

async function makeTarget(
  root: string,
  name: string,
  content: string,
): Promise<SaveBatchItem> {
  const path = join(root, name);
  const originalBytes = Buffer.from(content);
  await writeFile(path, originalBytes);
  return {
    fileId: name,
    fileName: name,
    path,
    baselineFingerprint: createHash('sha256')
      .update(originalBytes)
      .digest('hex'),
    outputBytes: Buffer.from(`${content}-updated`),
  };
}

async function makeRoot(): Promise<string> {
  const root = await mkdtemp(join(tmpdir(), 'dta-save-test-'));
  temporaryRoots.push(root);
  return root;
}

async function makeInterruptedJournal(
  root: string,
  targetPath: string,
  diskContent: string,
): Promise<{ recoveryRoot: string; batchId: string }> {
  const recoveryRoot = join(root, 'recovery');
  const batchId = randomUUID();
  const fileId = randomUUID();
  const batchDirectory = join(recoveryRoot, batchId);
  const original = Buffer.from('original');
  const output = Buffer.from('prepared output');
  await mkdir(batchDirectory, { recursive: true });
  await writeFile(join(batchDirectory, `${fileId}.original`), original);
  await writeFile(targetPath, Buffer.from(diskContent));
  const digest = (bytes: Buffer): string =>
    createHash('sha256').update(bytes).digest('hex');
  await writeFile(
    join(batchDirectory, 'journal.json'),
    JSON.stringify({
      version: 1,
      batchId,
      startedAt: '2026-10-16T10:00:00.000Z',
      completedAt: null,
      state: 'replacing',
      files: [
        {
          fileId,
          fileName: 'interrupted.dta',
          path: targetPath,
          backupPath: join(batchDirectory, `${fileId}.original`),
          temporaryPath: `${targetPath}.dta-editor-${batchId}.tmp`,
          baselineFingerprint: digest(original),
          outputFingerprint: digest(output),
          status: 'uncertain',
          message: 'Replacement may have been interrupted.',
        },
      ],
    }),
  );
  return { recoveryRoot, batchId };
}

afterEach(async () => {
  await Promise.all(
    temporaryRoots
      .splice(0)
      .map((root) => rm(root, { recursive: true, force: true })),
  );
});

describe('save batch service', () => {
  it('preflights, backs up, replaces and verifies one file', async () => {
    const root = await makeRoot();
    const sourceDirectory = join(root, 'templates');
    const recoveryDirectory = join(root, 'recovery');
    await mkdir(sourceDirectory);
    const target = await makeTarget(sourceDirectory, 'one.dta', 'original');

    const result = await saveBatch([target], recoveryDirectory);

    assert.equal(result.ok, true);
    assert.equal(result.outcomes[0].status, 'saved');
    assert.deepEqual(await readFile(target.path), target.outputBytes);
    assert.equal((await stat(result.journalPath)).isFile(), true);
    const recoveryFiles = await (
      await import('node:fs/promises')
    ).readdir(join(recoveryDirectory, result.batchId));
    assert.ok(recoveryFiles.includes(`${target.fileId}.original`));
  });

  it('skips chmod when the temporary file already has the original mode', async () => {
    const root = await makeRoot();
    const sourceDirectory = join(root, 'templates');
    await mkdir(sourceDirectory);
    const target = await makeTarget(
      sourceDirectory,
      'unchanged-mode.dta',
      'original',
    );
    let chmodCalls = 0;

    const result = await saveBatch([target], join(root, 'recovery'), {
      chmodFile: async () => {
        chmodCalls += 1;
        throw new Error('chmod should not be needed when modes match');
      },
    });

    assert.equal(result.ok, true);
    assert.equal(chmodCalls, 0);
    assert.deepEqual(await readFile(target.path), target.outputBytes);
  });

  it('blocks a concurrent disk change before creating replacements', async () => {
    const root = await makeRoot();
    const sourceDirectory = join(root, 'templates');
    await mkdir(sourceDirectory);
    const target = await makeTarget(sourceDirectory, 'changed.dta', 'original');
    const externalBytes = Buffer.from('external edit');
    await writeFile(target.path, externalBytes);

    const result = await saveBatch([target], join(root, 'recovery'));

    assert.equal(result.ok, false);
    assert.match(result.error ?? '', /changed on disk/u);
    assert.deepEqual(await readFile(target.path), externalBytes);
    assert.equal(result.outcomes[0].status, 'failed');
  });

  it('restores an earlier file when a later replacement fails', async () => {
    const root = await makeRoot();
    const sourceDirectory = join(root, 'templates');
    await mkdir(sourceDirectory);
    const first = await makeTarget(sourceDirectory, 'first.dta', 'first');
    const second = await makeTarget(sourceDirectory, 'second.dta', 'second');
    let replaceCalls = 0;

    const result = await saveBatch([first, second], join(root, 'recovery'), {
      replaceFile: async (temporaryPath, targetPath) => {
        replaceCalls += 1;
        if (replaceCalls === 2) throw new Error('injected replacement failure');
        await rename(temporaryPath, targetPath);
      },
    });

    assert.equal(result.ok, false);
    assert.match(result.error ?? '', /injected replacement failure/u);
    assert.deepEqual(await readFile(first.path), Buffer.from('first'));
    assert.deepEqual(await readFile(second.path), Buffer.from('second'));
    assert.deepEqual(
      result.outcomes.map((outcome) => outcome.status),
      ['restored', 'failed'],
    );
    assert.equal((await stat(result.journalPath)).isFile(), true);
  });

  it('discovers an interrupted batch and restores only the verified prepared output', async () => {
    const root = await makeRoot();
    const sourceDirectory = join(root, 'templates');
    await mkdir(sourceDirectory);
    const targetPath = join(sourceDirectory, 'interrupted.dta');
    const { recoveryRoot, batchId } = await makeInterruptedJournal(
      root,
      targetPath,
      'prepared output',
    );

    const pending = await listInterruptedSaveBatches(
      recoveryRoot,
      new Set([targetPath]),
    );
    assert.equal(pending.length, 1);
    assert.equal(pending[0].canRecover, true);
    const result = await recoverInterruptedSaveBatch(
      recoveryRoot,
      batchId,
      new Set([targetPath]),
    );

    assert.equal(result.ok, true);
    assert.equal(result.outcomes[0].status, 'restored');
    assert.deepEqual(await readFile(targetPath), Buffer.from('original'));
    assert.deepEqual(
      await listInterruptedSaveBatches(recoveryRoot, new Set([targetPath])),
      [],
    );
  });

  it('leaves a file with an external fingerprint untouched during recovery', async () => {
    const root = await makeRoot();
    const sourceDirectory = join(root, 'templates');
    await mkdir(sourceDirectory);
    const targetPath = join(sourceDirectory, 'interrupted.dta');
    const { recoveryRoot, batchId } = await makeInterruptedJournal(
      root,
      targetPath,
      'external edit',
    );

    const result = await recoverInterruptedSaveBatch(
      recoveryRoot,
      batchId,
      new Set([targetPath]),
    );

    assert.equal(result.ok, false);
    assert.equal(result.outcomes[0].status, 'uncertain');
    assert.deepEqual(await readFile(targetPath), Buffer.from('external edit'));
  });

  it('refuses recovery when journal paths are outside the registered workspace', async () => {
    const root = await makeRoot();
    const sourceDirectory = join(root, 'templates');
    await mkdir(sourceDirectory);
    const targetPath = join(sourceDirectory, 'interrupted.dta');
    const { recoveryRoot, batchId } = await makeInterruptedJournal(
      root,
      targetPath,
      'prepared output',
    );

    const pending = await listInterruptedSaveBatches(recoveryRoot, new Set());
    assert.equal(pending[0].canRecover, false);
    const result = await recoverInterruptedSaveBatch(
      recoveryRoot,
      batchId,
      new Set(),
    );

    assert.equal(result.ok, false);
    assert.deepEqual(
      await readFile(targetPath),
      Buffer.from('prepared output'),
    );
  });
});
