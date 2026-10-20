import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync } from 'node:fs';
import { stat } from 'node:fs/promises';
import { resolve } from 'node:path';
import { describe, it } from 'node:test';
import {
  readTemplateSnapshot,
  type RegisteredTemplate,
} from '../../src/main/template-reader';

const samplePath = resolve(__dirname, '../../../examples/sample-template.dta');

describe('native DTA reader snapshots', () => {
  it(
    'reads the archive, declarations, editable spans, and exact fingerprint without writes',
    { skip: !existsSync(samplePath) },
    async () => {
      const before = await stat(samplePath);
      const file: RegisteredTemplate = {
        id: 'sample-file',
        name: 'sample-template.dta',
        size: before.size,
        modifiedAt: before.mtimeMs,
        error: null,
        path: samplePath,
      };
      const snapshot = await readTemplateSnapshot(file);

      assert.equal(snapshot.result.error, null);
      assert.ok(snapshot.result.declarations.length > 0);
      assert.ok(snapshot.archiveBytes);
      assert.ok(snapshot.templateXml);
      assert.equal(
        snapshot.fingerprint,
        createHash('sha256').update(snapshot.archiveBytes).digest('hex'),
      );
      assert.deepEqual(snapshot.result.declarations[0].editableFields, {
        access: true,
        defaultValue: true,
        description: true,
      });
      const after = await stat(samplePath);
      assert.equal(after.size, before.size);
      assert.equal(after.mtimeMs, before.mtimeMs);
    },
  );

  it(
    'rejects a listing fingerprint that no longer matches file metadata',
    { skip: !existsSync(samplePath) },
    async () => {
      const before = await stat(samplePath);
      const file: RegisteredTemplate = {
        id: 'stale-file',
        name: 'stale.dta',
        size: before.size + 1,
        modifiedAt: before.mtimeMs,
        error: null,
        path: samplePath,
      };
      const snapshot = await readTemplateSnapshot(file);
      assert.match(snapshot.result.error ?? '', /changed since it was listed/u);
      assert.equal(snapshot.archiveBytes, null);
      assert.equal(snapshot.templateXml, null);
      assert.equal(snapshot.fingerprint, null);
    },
  );
});
