import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { describe, it } from 'node:test';
import * as CRC32 from 'crc-32';
import * as yauzl from 'yauzl';
import {
  patchXmlSource,
  parseXmlSource,
} from '../../src/core/xml-source-patcher';
import { replaceNativeTemplateXml } from '../../src/core/dta-zip-writer';
import { saveBatch } from '../../src/main/save-service';

const sampleNames = [
  'requirements_specification_sample.dta',
  'requirements_specification_sample_landscape.dta',
  'solution_configuration_specification.dta',
  'configuration_specification_sample_landscape.dta',
];
const samplePaths = sampleNames.map((name) =>
  resolve(__dirname, '../../../examples', name),
);

async function readArchiveMembers(
  archiveBytes: Buffer,
): Promise<Map<string, Buffer>> {
  const archive = await yauzl.fromBufferPromise(archiveBytes, {
    lazyEntries: true,
    strictFileNames: true,
    validateEntrySizes: true,
  });
  const members = new Map<string, Buffer>();
  try {
    for await (const entry of archive.eachEntry()) {
      const stream = await archive.openReadStreamPromise(entry);
      const chunks: Buffer[] = [];
      for await (const chunk of stream) {
        chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
      }
      const payload = Buffer.concat(chunks);
      assert.equal(payload.length, entry.uncompressedSize);
      assert.equal(CRC32.buf(payload) >>> 0, entry.crc32 >>> 0);
      assert.equal(members.has(entry.fileName), false);
      members.set(entry.fileName, payload);
    }
  } finally {
    if (archive.isOpen) archive.close();
  }
  return members;
}

describe('native DTA ZIP member writer', () => {
  it(
    'returns every supplied sample archive byte-identically for no-op replacements',
    { skip: samplePaths.some((path) => !existsSync(path)) },
    async () => {
      for (const path of samplePaths) {
        const archiveBytes = readFileSync(path);
        const members = await readArchiveMembers(archiveBytes);
        const templateXml = members.get('template.xml');
        assert.ok(templateXml);
        assert.strictEqual(
          replaceNativeTemplateXml(archiveBytes, templateXml),
          archiveBytes,
        );
        assert.deepEqual(readFileSync(path), archiveBytes);
      }
    },
  );

  it(
    'replaces only template.xml while retaining every unrelated member payload',
    { skip: samplePaths.some((path) => !existsSync(path)) },
    async () => {
      const samplePath = samplePaths[0];
      const originalArchive = readFileSync(samplePath);
      const originalMembers = await readArchiveMembers(originalArchive);
      const originalXmlBytes = originalMembers.get('template.xml');
      assert.ok(originalXmlBytes);
      const originalXml = new TextDecoder('utf-8', { fatal: true }).decode(
        originalXmlBytes,
      );
      const first = parseXmlSource(originalXml).declarations[0];
      assert.ok(first.defaultValue !== null);
      const proposed = 'writer test & <safe value>';
      const changedXml = patchXmlSource(originalXml, [
        {
          declarationId: first.id,
          field: 'defaultValue',
          value: proposed,
        },
      ]);
      const changedXmlBytes = Buffer.from(changedXml, 'utf8');
      const changedArchive = replaceNativeTemplateXml(
        originalArchive,
        changedXmlBytes,
      );
      const changedMembers = await readArchiveMembers(changedArchive);

      assert.notDeepEqual(changedArchive, originalArchive);
      assert.deepEqual(changedMembers.get('template.xml'), changedXmlBytes);
      assert.equal(changedMembers.size, originalMembers.size);
      for (const [name, payload] of originalMembers) {
        if (name === 'template.xml') continue;
        assert.deepEqual(changedMembers.get(name), payload, name);
      }
      assert.equal(
        parseXmlSource(
          new TextDecoder().decode(changedMembers.get('template.xml')),
        ).declarations[0].defaultValue,
        proposed,
      );
      assert.deepEqual(readFileSync(samplePath), originalArchive);
    },
  );

  it('fails closed for malformed, truncated, and unsupported containers', () => {
    assert.throws(
      () =>
        replaceNativeTemplateXml(
          Buffer.from('not a ZIP'),
          Buffer.from('<template/>'),
        ),
      /end-of-central-directory/u,
    );
    if (samplePaths.every((path) => existsSync(path))) {
      const sample = readFileSync(samplePaths[0]);
      assert.throws(
        () =>
          replaceNativeTemplateXml(
            sample.subarray(0, 100),
            Buffer.from('<template/>'),
          ),
        /end-of-central-directory/u,
      );
      const unsupported = Buffer.from(sample);
      const eocdOffset = unsupported.lastIndexOf(
        Buffer.from([0x50, 0x4b, 0x05, 0x06]),
      );
      const centralOffset = unsupported.readUInt32LE(eocdOffset + 16);
      unsupported.writeUInt16LE(0, centralOffset + 8);
      assert.throws(
        () => replaceNativeTemplateXml(unsupported, Buffer.from('<template/>')),
        /unsupported ZIP metadata/u,
      );
    }
  });

  it(
    'writes a real edited DTA only to a temporary copy and verifies its backup',
    { skip: !existsSync(samplePaths[0]) },
    async () => {
      const root = await mkdtemp(join(tmpdir(), 'native-dta-save-'));
      try {
        const originalArchive = readFileSync(samplePaths[0]);
        const originalMembers = await readArchiveMembers(originalArchive);
        const originalXml = new TextDecoder('utf-8', { fatal: true }).decode(
          originalMembers.get('template.xml')!,
        );
        const first = parseXmlSource(originalXml).declarations[0];
        const editedXml = Buffer.from(
          patchXmlSource(originalXml, [
            {
              declarationId: first.id,
              field: 'defaultValue',
              value: 'saved to temp copy',
            },
          ]),
          'utf8',
        );
        const outputArchive = replaceNativeTemplateXml(
          originalArchive,
          editedXml,
        );
        const templateDirectory = join(root, 'templates');
        const recoveryDirectory = join(root, 'recovery');
        await mkdir(templateDirectory);
        const copyPath = join(templateDirectory, 'edited-copy.dta');
        await writeFile(copyPath, originalArchive);
        const baselineFingerprint = createHash('sha256')
          .update(originalArchive)
          .digest('hex');

        const result = await saveBatch(
          [
            {
              fileId: 'native-sample-copy',
              fileName: 'edited-copy.dta',
              path: copyPath,
              baselineFingerprint,
              outputBytes: outputArchive,
            },
          ],
          recoveryDirectory,
        );

        assert.equal(result.ok, true);
        assert.equal(result.outcomes[0].status, 'saved');
        const savedMembers = await readArchiveMembers(await readFile(copyPath));
        assert.deepEqual(savedMembers.get('template.xml'), editedXml);
        assert.equal(savedMembers.size, originalMembers.size);
        for (const [name, payload] of originalMembers) {
          if (name === 'template.xml') continue;
          assert.deepEqual(savedMembers.get(name), payload, name);
        }
        assert.deepEqual(readFileSync(samplePaths[0]), originalArchive);
        const journal = await readFile(result.journalPath, 'utf8');
        assert.match(journal, /"state": "complete"/u);
        assert.match(journal, /native-sample-copy\.original/u);
      } finally {
        await rm(root, { recursive: true, force: true });
      }
    },
  );
});
