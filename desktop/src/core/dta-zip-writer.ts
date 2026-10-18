import * as CRC32 from 'crc-32';
import { deflateRawSync, inflateRawSync } from 'node:zlib';

const LOCAL_SIGNATURE = 0x04034b50;
const CENTRAL_SIGNATURE = 0x02014b50;
const EOCD_SIGNATURE = 0x06054b50;
const DESCRIPTOR_SIGNATURE = 0x08074b50;
const UTF8_DESCRIPTOR_FLAGS = 0x0808;
const MAX_ARCHIVE_BYTES = 512 * 1024 * 1024;
const MAX_MEMBER_BYTES = 128 * 1024 * 1024;
const MAX_MEMBER_COUNT = 10_000;
const TEMPLATE_NAME = Buffer.from('template.xml', 'ascii');

interface CentralEntry {
  raw: Buffer;
  name: Buffer;
  nameText: string;
  flags: number;
  method: number;
  crc: number;
  compressedSize: number;
  uncompressedSize: number;
  localOffset: number;
}

interface LocalRecord {
  entry: CentralEntry;
  localHeader: Buffer;
  compressedData: Buffer;
  end: number;
}

interface DirectoryModel {
  entries: CentralEntry[];
  records: LocalRecord[];
  centralOffset: number;
  centralSize: number;
  eocdOffset: number;
  eocd: Buffer;
}

function fail(message: string): never {
  throw new Error(`Unsupported native DTA ZIP structure: ${message}`);
}

function findEndRecord(archive: Buffer): { offset: number; eocd: Buffer } {
  const minimumOffset = Math.max(0, archive.length - 22 - 65_535);
  for (let offset = archive.length - 22; offset >= minimumOffset; offset -= 1) {
    if (archive.readUInt32LE(offset) !== EOCD_SIGNATURE) continue;
    const commentLength = archive.readUInt16LE(offset + 20);
    if (offset + 22 + commentLength !== archive.length) continue;
    return { offset, eocd: archive.subarray(offset) };
  }
  return fail('end-of-central-directory record was not found');
}

function readCentralDirectory(archive: Buffer): DirectoryModel {
  if (archive.length > MAX_ARCHIVE_BYTES) {
    return fail('archive exceeds the 512 MiB safety limit');
  }
  const { offset: eocdOffset, eocd } = findEndRecord(archive);
  const diskNumber = eocd.readUInt16LE(4);
  const centralDisk = eocd.readUInt16LE(6);
  const diskEntries = eocd.readUInt16LE(8);
  const totalEntries = eocd.readUInt16LE(10);
  const centralSize = eocd.readUInt32LE(12);
  const centralOffset = eocd.readUInt32LE(16);
  if (
    diskNumber !== 0 ||
    centralDisk !== 0 ||
    diskEntries !== totalEntries ||
    totalEntries === 0xffff ||
    centralSize === 0xffffffff ||
    centralOffset === 0xffffffff
  ) {
    return fail('multi-disk and ZIP64 archives are not supported');
  }
  if (totalEntries === 0 || totalEntries > MAX_MEMBER_COUNT) {
    return fail('archive member count is unsupported');
  }
  if (centralOffset + centralSize !== eocdOffset) {
    return fail('central directory bounds are inconsistent');
  }

  const decoder = new TextDecoder('utf-8', { fatal: true });
  const entries: CentralEntry[] = [];
  const names = new Set<string>();
  let cursor = centralOffset;
  for (let index = 0; index < totalEntries; index += 1) {
    if (
      cursor + 46 > eocdOffset ||
      archive.readUInt32LE(cursor) !== CENTRAL_SIGNATURE
    ) {
      return fail(`central directory record ${index} is invalid`);
    }
    const flags = archive.readUInt16LE(cursor + 8);
    const method = archive.readUInt16LE(cursor + 10);
    const crc = archive.readUInt32LE(cursor + 16);
    const compressedSize = archive.readUInt32LE(cursor + 20);
    const uncompressedSize = archive.readUInt32LE(cursor + 24);
    const nameLength = archive.readUInt16LE(cursor + 28);
    const extraLength = archive.readUInt16LE(cursor + 30);
    const commentLength = archive.readUInt16LE(cursor + 32);
    const diskStart = archive.readUInt16LE(cursor + 34);
    const localOffset = archive.readUInt32LE(cursor + 42);
    const recordLength = 46 + nameLength + extraLength + commentLength;
    if (cursor + recordLength > eocdOffset) {
      return fail(`central directory record ${index} exceeds its bounds`);
    }
    if (
      flags !== UTF8_DESCRIPTOR_FLAGS ||
      method !== 8 ||
      diskStart !== 0 ||
      compressedSize === 0xffffffff ||
      uncompressedSize === 0xffffffff ||
      localOffset === 0xffffffff ||
      extraLength !== 0 ||
      commentLength !== 0
    ) {
      return fail(`member ${index} uses unsupported ZIP metadata`);
    }
    if (
      compressedSize > MAX_ARCHIVE_BYTES ||
      uncompressedSize > MAX_MEMBER_BYTES
    ) {
      return fail(`member ${index} exceeds a safety limit`);
    }

    const raw = Buffer.from(archive.subarray(cursor, cursor + recordLength));
    const name = Buffer.from(raw.subarray(46, 46 + nameLength));
    let nameText: string;
    try {
      nameText = decoder.decode(name);
    } catch {
      return fail(`member ${index} has an invalid UTF-8 name`);
    }
    if (names.has(nameText)) return fail(`duplicate member name ${nameText}`);
    names.add(nameText);
    entries.push({
      raw,
      name,
      nameText,
      flags,
      method,
      crc,
      compressedSize,
      uncompressedSize,
      localOffset,
    });
    cursor += recordLength;
  }
  if (cursor !== centralOffset + centralSize) {
    return fail('central directory size does not match its records');
  }

  const ordered = [...entries].sort(
    (left, right) => left.localOffset - right.localOffset,
  );
  if (ordered[0]?.localOffset !== 0) {
    return fail('prepended data or a self-extracting stub is not supported');
  }
  const records: LocalRecord[] = [];
  let expectedOffset = 0;
  for (const entry of ordered) {
    if (entry.localOffset !== expectedOffset) {
      return fail(
        `local record order or padding is unsupported near ${entry.nameText}`,
      );
    }
    const offset = entry.localOffset;
    if (
      offset + 30 > centralOffset ||
      archive.readUInt32LE(offset) !== LOCAL_SIGNATURE
    ) {
      return fail(`local header for ${entry.nameText} is invalid`);
    }
    const flags = archive.readUInt16LE(offset + 6);
    const method = archive.readUInt16LE(offset + 8);
    const nameLength = archive.readUInt16LE(offset + 26);
    const extraLength = archive.readUInt16LE(offset + 28);
    if (
      flags !== entry.flags ||
      method !== entry.method ||
      extraLength !== 0 ||
      nameLength !== entry.name.length
    ) {
      return fail(`local header metadata disagrees for ${entry.nameText}`);
    }
    const nameStart = offset + 30;
    const dataStart = nameStart + nameLength;
    if (!archive.subarray(nameStart, dataStart).equals(entry.name)) {
      return fail(`local and central names disagree for ${entry.nameText}`);
    }
    const descriptorStart = dataStart + entry.compressedSize;
    const recordEnd = descriptorStart + 16;
    if (
      recordEnd > centralOffset ||
      archive.readUInt32LE(descriptorStart) !== DESCRIPTOR_SIGNATURE ||
      archive.readUInt32LE(descriptorStart + 4) !== entry.crc ||
      archive.readUInt32LE(descriptorStart + 8) !== entry.compressedSize ||
      archive.readUInt32LE(descriptorStart + 12) !== entry.uncompressedSize
    ) {
      return fail(`data descriptor for ${entry.nameText} is invalid`);
    }

    const compressedData = archive.subarray(dataStart, descriptorStart);
    let uncompressed: Buffer;
    try {
      uncompressed = inflateRawSync(compressedData, {
        maxOutputLength: MAX_MEMBER_BYTES,
      });
    } catch (error) {
      return fail(
        `member ${entry.nameText} could not be decompressed: ${String(error)}`,
      );
    }
    if (
      uncompressed.length !== entry.uncompressedSize ||
      CRC32.buf(uncompressed) >>> 0 !== entry.crc
    ) {
      return fail(`member ${entry.nameText} failed size or CRC validation`);
    }

    records.push({
      entry,
      localHeader: Buffer.from(archive.subarray(offset, dataStart)),
      compressedData,
      end: recordEnd,
    });
    expectedOffset = recordEnd;
  }
  if (expectedOffset !== centralOffset) {
    return fail('local records do not end at the central directory');
  }

  return {
    entries,
    records,
    centralOffset,
    centralSize,
    eocdOffset,
    eocd,
  };
}

/**
 * Replaces only template.xml in the observed ZIP variant and preserves all
 * unrelated local records and central-directory metadata. Unsupported variants
 * fail closed; this function never writes to the filesystem.
 */
export function replaceNativeTemplateXml(
  archive: Buffer,
  templateXml: Buffer,
): Buffer {
  const model = readCentralDirectory(archive);
  const targets = model.entries.filter((entry) =>
    entry.name.equals(TEMPLATE_NAME),
  );
  if (targets.length !== 1)
    return fail('expected exactly one root template.xml member');
  if (templateXml.length > MAX_MEMBER_BYTES) {
    return fail('replacement template.xml exceeds the 128 MiB safety limit');
  }
  const target = targets[0];
  const currentRecord = model.records.find((record) => record.entry === target);
  if (!currentRecord) return fail('template.xml local record is missing');
  const currentXml = inflateRawSync(currentRecord.compressedData, {
    maxOutputLength: MAX_MEMBER_BYTES,
  });
  if (CRC32.buf(currentXml) >>> 0 !== target.crc) {
    return fail('template.xml failed CRC validation');
  }
  if (currentXml.equals(templateXml)) return archive;

  const compressedXml = deflateRawSync(templateXml);
  const newCrc = CRC32.buf(templateXml) >>> 0;
  if (compressedXml.length > 0xffffffff || templateXml.length > 0xffffffff) {
    return fail('replacement member exceeds ZIP32 limits');
  }
  const newDescriptor = Buffer.alloc(16);
  newDescriptor.writeUInt32LE(DESCRIPTOR_SIGNATURE, 0);
  newDescriptor.writeUInt32LE(newCrc, 4);
  newDescriptor.writeUInt32LE(compressedXml.length, 8);
  newDescriptor.writeUInt32LE(templateXml.length, 12);
  const replacementRecord = Buffer.concat([
    currentRecord.localHeader,
    compressedXml,
    newDescriptor,
  ]);
  const lengthDelta =
    replacementRecord.length - (currentRecord.end - target.localOffset);

  const newLocalRecords: Buffer[] = [];
  const updatedOffsets = new Map<CentralEntry, number>();
  let newOffset = 0;
  for (const record of model.records) {
    updatedOffsets.set(record.entry, newOffset);
    const bytes =
      record.entry === target
        ? replacementRecord
        : Buffer.concat([
            record.localHeader,
            record.compressedData,
            archive.subarray(record.end - 16, record.end),
          ]);
    newLocalRecords.push(bytes);
    newOffset += bytes.length;
  }

  const centralRecords: Buffer[] = [];
  for (const entry of model.entries) {
    const record = Buffer.from(entry.raw);
    const localOffset = updatedOffsets.get(entry);
    if (localOffset === undefined || localOffset > 0xffffffff) {
      return fail(
        `new local offset for ${entry.nameText} exceeds ZIP32 limits`,
      );
    }
    record.writeUInt32LE(localOffset, 42);
    if (entry === target) {
      record.writeUInt32LE(newCrc, 16);
      record.writeUInt32LE(compressedXml.length, 20);
      record.writeUInt32LE(templateXml.length, 24);
    }
    centralRecords.push(record);
  }
  const centralDirectory = Buffer.concat(centralRecords);
  const eocd = Buffer.from(model.eocd);
  eocd.writeUInt32LE(centralDirectory.length, 12);
  eocd.writeUInt32LE(newOffset, 16);
  const output = Buffer.concat([...newLocalRecords, centralDirectory, eocd]);
  if (output.length > MAX_ARCHIVE_BYTES) {
    return fail('rewritten archive exceeds the 512 MiB safety limit');
  }
  if (output.length !== archive.length + lengthDelta) {
    return fail('rewritten archive length did not match the replacement delta');
  }
  readCentralDirectory(output);
  return output;
}
