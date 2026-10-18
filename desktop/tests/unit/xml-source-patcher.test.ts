import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, it } from 'node:test';
import { TextDecoder } from 'node:util';
import * as yauzl from 'yauzl';
import {
  parseXmlSource,
  patchXmlSource,
} from '../../src/core/xml-source-patcher';

const document = (description = 'Old &amp; safe'): string =>
  `<?xml version="1.0"?>\n<template><dataset><variables><variable><feature tag="definition"><property name='name' value='reportTitle'/><property name="description"><expression type="constant">${description}</expression></property><property name="type" value="user"/><property name='access' value='external'/><property name="default" value="before &quot;quoted&quot;"/><property name="central variable" value=""/></feature></variable></variables></dataset></template>`;

const nativeSamples = [
  'requirements_specification_sample.dta',
  'requirements_specification_sample_landscape.dta',
  'solution_configuration_specification.dta',
  'configuration_specification_sample_landscape.dta',
].map((name) => resolve(__dirname, '../../../examples', name));

async function readNativeTemplateXml(filePath: string): Promise<string> {
  const archive = await yauzl.openPromise(filePath, {
    lazyEntries: true,
    strictFileNames: true,
    validateEntrySizes: true,
  });
  try {
    for await (const entry of archive.eachEntry()) {
      if (entry.fileName !== 'template.xml') continue;
      const stream = await archive.openReadStreamPromise(entry);
      const chunks: Buffer[] = [];
      for await (const chunk of stream) {
        chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
      }
      return new TextDecoder('utf-8', { fatal: true }).decode(
        Buffer.concat(chunks),
      );
    }
  } finally {
    if (archive.isOpen) archive.close();
  }
  throw new Error(`Missing template.xml member in ${filePath}`);
}

describe('source-span XML patcher', () => {
  it('maps only supported declaration fields and leaves a no-op byte-identical', () => {
    const xml = document();
    const model = parseXmlSource(xml);

    assert.equal(model.declarations.length, 1);
    assert.deepEqual(model.editableFields[0], {
      access: true,
      defaultValue: true,
      description: true,
    });
    assert.equal(
      patchXmlSource(xml, [
        {
          declarationId: 'declaration-0',
          field: 'defaultValue',
          value: 'before "quoted"',
        },
      ]),
      xml,
    );
    assert.equal(patchXmlSource(xml, []), xml);
  });

  it('patches selected fields with XML escaping while retaining unrelated source', () => {
    const xml = document();
    const output = patchXmlSource(xml, [
      {
        declarationId: 'declaration-0',
        field: 'defaultValue',
        value: `  <x & "double" 'single'\tline\n雪\r  `,
      },
      {
        declarationId: 'declaration-0',
        field: 'description',
        value: 'New <description> & text\nalert(1)',
      },
      { declarationId: 'declaration-0', field: 'access', value: 'internal' },
    ]);
    const after = parseXmlSource(output);

    assert.equal(
      after.declarations[0].defaultValue,
      `  <x & "double" 'single'\tline\n雪\r  `,
    );
    assert.equal(
      after.declarations[0].description,
      'New <description> & text\nalert(1)',
    );
    assert.equal(after.declarations[0].access, 'internal');
    assert.equal(after.declarations[0].name, 'reportTitle');
    assert.equal(after.declarations[0].xmlType, 'user');
    assert.match(output, /<property name='name' value='reportTitle'\/>/u);
    assert.match(output, /<property name="type" value="user"\/>/u);
    assert.match(output, /&lt;x &amp; &quot;double&quot; 'single'/u);
  });

  it('keeps a complex description read-only without blocking a supported value span', () => {
    const xml = document('Keep <emphasis>formatting</emphasis>');
    const model = parseXmlSource(xml);

    assert.equal(model.editableFields[0].description, false);
    assert.equal(model.editableFields[0].defaultValue, true);
    assert.throws(
      () =>
        patchXmlSource(xml, [
          {
            declarationId: 'declaration-0',
            field: 'description',
            value: 'replace',
          },
        ]),
      /no supported source span/u,
    );
    const output = patchXmlSource(xml, [
      {
        declarationId: 'declaration-0',
        field: 'defaultValue',
        value: 'changed',
      },
    ]);
    assert.match(output, /Keep <emphasis>formatting<\/emphasis>/u);
    assert.equal(
      parseXmlSource(output).declarations[0].defaultValue,
      'changed',
    );
  });

  it('rejects invalid XML characters, unsupported access values, and unknown identities', () => {
    const xml = document();
    assert.throws(
      () =>
        patchXmlSource(xml, [
          {
            declarationId: 'declaration-0',
            field: 'defaultValue',
            value: 'bad\u0001',
          },
        ]),
      /XML-invalid character/u,
    );
    assert.throws(
      () =>
        patchXmlSource(xml, [
          { declarationId: 'declaration-0', field: 'access', value: 'shared' },
        ]),
      /internal or external/u,
    );
    assert.throws(
      () =>
        patchXmlSource(xml, [
          {
            declarationId: 'declaration-9',
            field: 'defaultValue',
            value: 'new',
          },
        ]),
      /Unknown declaration/u,
    );
  });

  it('does not rewrite comment or CDATA markup inside a description', () => {
    for (const description of [
      'Before <!-- note --> after',
      'Before <![CDATA[raw]]> after',
    ]) {
      const xml = document(description);
      const model = parseXmlSource(xml);
      assert.equal(model.editableFields[0].description, false);
      assert.throws(
        () =>
          patchXmlSource(xml, [
            {
              declarationId: 'declaration-0',
              field: 'description',
              value: 'replace',
            },
          ]),
        /no supported source span/u,
      );
    }
  });

  it(
    'maps and patches the supplied native DTA sample declarations',
    { skip: nativeSamples.some((sample) => !existsSync(sample)) },
    async () => {
      const expectedCounts = [234, 234, 234, 237];
      for (const [index, filePath] of nativeSamples.entries()) {
        const xml = await readNativeTemplateXml(filePath);
        const model = parseXmlSource(xml);
        assert.equal(model.declarations.length, expectedCounts[index]);
        assert.ok(
          model.editableFields.every(
            (fields) => fields.access && fields.defaultValue,
          ),
        );

        const first = model.declarations[0];
        assert.ok(first.defaultValue !== null);
        assert.equal(
          patchXmlSource(xml, [
            {
              declarationId: first.id,
              field: 'defaultValue',
              value: first.defaultValue,
            },
          ]),
          xml,
        );
        const proposed = `native sample ${index} & <value>`;
        const patched = patchXmlSource(xml, [
          { declarationId: first.id, field: 'defaultValue', value: proposed },
        ]);
        const after = parseXmlSource(patched);
        assert.equal(after.declarations[0].defaultValue, proposed);
        assert.equal(after.declarations[0].name, first.name);
        assert.equal(after.declarations[0].access, first.access);
        assert.equal(after.declarations[0].description, first.description);
        assert.equal(
          after.declarations[1].defaultValue,
          model.declarations[1].defaultValue,
        );
      }
    },
  );
});
