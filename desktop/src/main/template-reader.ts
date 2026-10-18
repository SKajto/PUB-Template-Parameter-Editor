import * as CRC32 from 'crc-32';
import { createHash } from 'node:crypto';
import { readFile, stat } from 'node:fs/promises';
import { TextDecoder } from 'node:util';
import * as yauzl from 'yauzl';
import { replaceNativeTemplateXml } from '../core/dta-zip-writer';
import type {
  ParameterDeclaration,
  ReadTemplateResult,
  TemplateFile,
} from '../shared/types';
import { parseXmlSource } from '../core/xml-source-patcher';

const MAX_ARCHIVE_BYTES = 512 * 1024 * 1024;
const MAX_MEMBER_BYTES = 128 * 1024 * 1024;
const MAX_ARCHIVE_MEMBERS = 10_000;

export interface RegisteredTemplate extends TemplateFile {
  path: string;
}

export interface TemplateReadSnapshot {
  result: ReadTemplateResult;
  archiveBytes: Buffer | null;
  templateXml: Buffer | null;
  fingerprint: string | null;
}

function asMessage(error: unknown): string {
  return error instanceof Error
    ? error.message
    : 'Unknown template read error.';
}

async function readTemplateXml(
  zipfile: yauzl.ZipFile,
  entry: yauzl.Entry,
): Promise<Buffer> {
  if (entry.generalPurposeBitFlag & 0x1) {
    throw new Error('Encrypted ZIP members are not supported.');
  }
  if (entry.compressionMethod !== 0 && entry.compressionMethod !== 8) {
    throw new Error('The template.xml compression method is not supported.');
  }
  if (entry.uncompressedSize > MAX_MEMBER_BYTES) {
    throw new Error('template.xml exceeds the 128 MiB safety limit.');
  }

  const stream = await zipfile.openReadStreamPromise(entry);
  const chunks: Buffer[] = [];
  let bytesRead = 0;
  for await (const chunk of stream) {
    const buffer = Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk);
    bytesRead += buffer.length;
    if (bytesRead > MAX_MEMBER_BYTES) {
      stream.destroy(
        new Error('template.xml exceeded the 128 MiB safety limit.'),
      );
      throw new Error('template.xml exceeded the 128 MiB safety limit.');
    }
    chunks.push(buffer);
  }

  if (bytesRead !== entry.uncompressedSize) {
    throw new Error(
      'template.xml size does not match its ZIP directory entry.',
    );
  }
  const content = Buffer.concat(chunks, bytesRead);
  if (CRC32.buf(content) >>> 0 !== entry.crc32 >>> 0) {
    throw new Error('template.xml failed its ZIP CRC check.');
  }
  return content;
}

export async function readTemplateSnapshot(
  file: RegisteredTemplate,
): Promise<TemplateReadSnapshot> {
  try {
    if (file.error !== null) throw new Error(file.error);
    const before = await stat(file.path);
    if (before.size !== file.size || before.mtimeMs !== file.modifiedAt) {
      throw new Error(
        'File changed since it was listed. Refresh the folder before reading.',
      );
    }
    if (before.size > MAX_ARCHIVE_BYTES) {
      throw new Error('The DTA archive exceeds the 512 MiB safety limit.');
    }

    const archiveBytes = await readFile(file.path);
    if (archiveBytes.length !== before.size) {
      throw new Error('File changed while it was being read. Read it again.');
    }
    const zipfile = await yauzl.fromBufferPromise(archiveBytes, {
      lazyEntries: true,
      strictFileNames: true,
      validateEntrySizes: true,
    });
    let memberCount = 0;
    const memberNames = new Set<string>();
    let templateXml: Buffer | null = null;

    try {
      for await (const entry of zipfile.eachEntry()) {
        memberCount += 1;
        if (memberCount > MAX_ARCHIVE_MEMBERS) {
          throw new Error(
            'The DTA archive exceeds the 10,000-member safety limit.',
          );
        }
        if (memberNames.has(entry.fileName)) {
          throw new Error(`Duplicate ZIP member name: ${entry.fileName}`);
        }
        memberNames.add(entry.fileName);

        if (entry.fileName === 'template.xml') {
          if (templateXml !== null) {
            throw new Error(
              'The DTA archive contains more than one template.xml member.',
            );
          }
          templateXml = await readTemplateXml(zipfile, entry);
        }
      }
    } finally {
      if (zipfile.isOpen) zipfile.close();
    }

    if (templateXml === null) {
      throw new Error(
        'The DTA archive does not contain a root template.xml member.',
      );
    }

    let xml: string;
    try {
      xml = new TextDecoder('utf-8', { fatal: true }).decode(templateXml);
    } catch (error) {
      throw new Error(`template.xml is not valid UTF-8: ${asMessage(error)}`, {
        cause: error,
      });
    }
    const sourceModel = parseXmlSource(xml);
    let containerWriteError: string | null = null;
    try {
      replaceNativeTemplateXml(archiveBytes, templateXml);
    } catch (error) {
      containerWriteError = asMessage(error);
    }
    const declarations: ParameterDeclaration[] = sourceModel.declarations.map(
      (declaration, index) => ({
        ...declaration,
        editableFields: containerWriteError
          ? { access: false, defaultValue: false, description: false }
          : sourceModel.editableFields[index],
        readOnlyReasons: containerWriteError
          ? [
              ...declaration.readOnlyReasons,
              `DTA container is read-only: ${containerWriteError}`,
            ]
          : declaration.readOnlyReasons,
      }),
    );

    const after = await stat(file.path);
    if (after.size !== before.size || after.mtimeMs !== before.mtimeMs) {
      throw new Error('File changed while it was being read. Read it again.');
    }
    return {
      result: {
        fileId: file.id,
        fileName: file.name,
        declarations,
        error: null,
      },
      archiveBytes,
      templateXml,
      fingerprint: createHash('sha256').update(archiveBytes).digest('hex'),
    };
  } catch (error) {
    return {
      result: {
        fileId: file.id,
        fileName: file.name,
        declarations: [],
        error: asMessage(error),
      },
      archiveBytes: null,
      templateXml: null,
      fingerprint: null,
    };
  }
}
