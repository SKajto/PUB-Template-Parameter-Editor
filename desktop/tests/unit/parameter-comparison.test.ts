import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { ParameterDeclaration } from '../../src/shared/types';
import {
  compareParameterAcrossTemplates,
  findBulkValueCandidates,
  stageBulkDefaultValue,
  type LoadedParameterTemplate,
} from '../../src/core/parameter-comparison';
import { draftKey, stageParameterEdits } from '../../src/core/workspace-drafts';

function declaration(
  id: string,
  name: string | null,
  values: Partial<ParameterDeclaration> = {},
): ParameterDeclaration {
  return {
    id,
    name,
    description: 'Description',
    access: 'external',
    defaultValue: 'Default',
    xmlType: 'user',
    readOnlyReasons: [],
    editableFields: { access: true, defaultValue: true, description: true },
    ...values,
  };
}

describe('parameter comparison and bulk targeting', () => {
  it('compares exact names and distinguishes equality, differences, and missing declarations', () => {
    const active = declaration('declaration-0', 'param01Attribute', {
      defaultValue: '',
    });
    const templates: LoadedParameterTemplate[] = [
      { fileId: 'active', fileName: 'active.dta', declarations: [active] },
      {
        fileId: 'equal',
        fileName: 'equal.dta',
        declarations: [
          declaration('declaration-0', 'param01Attribute', {
            defaultValue: '',
          }),
        ],
      },
      {
        fileId: 'different',
        fileName: 'different.dta',
        declarations: [
          declaration('declaration-0', 'param01Attribute', {
            defaultValue: 'x',
          }),
        ],
      },
      {
        fileId: 'case',
        fileName: 'case.dta',
        declarations: [declaration('declaration-0', 'Param01Attribute')],
      },
    ];

    const rows = compareParameterAcrossTemplates(
      'active',
      active,
      templates,
      new Map(),
      null,
    );
    assert.deepEqual(
      rows.map((row) => row.status),
      ['active', 'equal', 'different', 'missing'],
    );
    assert.deepEqual(
      rows[2].differences.map((difference) => difference.field),
      ['defaultValue'],
    );
    assert.equal(rows[2].differences[0].activeValue, '');
    assert.equal(rows[2].differences[0].targetValue, 'x');
  });

  it('reports ambiguous duplicate names and respects explicit comparison selection', () => {
    const active = declaration('declaration-0', 'same');
    const templates: LoadedParameterTemplate[] = [
      { fileId: 'active', fileName: 'active.dta', declarations: [active] },
      {
        fileId: 'duplicate',
        fileName: 'duplicate.dta',
        declarations: [
          declaration('declaration-0', 'same'),
          declaration('declaration-1', 'same'),
        ],
      },
      {
        fileId: 'other',
        fileName: 'other.dta',
        declarations: [declaration('declaration-0', 'same')],
      },
    ];
    const rows = compareParameterAcrossTemplates(
      'active',
      active,
      templates,
      new Map(),
      new Set(['duplicate']),
    );
    assert.deepEqual(
      rows.map((row) => row.fileId),
      ['active', 'duplicate'],
    );
    assert.equal(rows[1].status, 'ambiguous');
  });

  it('marks draft-derived comparison values and compares working state', () => {
    const active = declaration('declaration-0', 'shared', {
      defaultValue: 'old',
    });
    const target = declaration('declaration-0', 'shared', {
      defaultValue: 'old',
    });
    const templates: LoadedParameterTemplate[] = [
      { fileId: 'active', fileName: 'active.dta', declarations: [active] },
      { fileId: 'target', fileName: 'target.dta', declarations: [target] },
    ];
    const drafts = stageParameterEdits(new Map(), 'active', active, {
      access: 'external',
      defaultValue: 'draft value',
      description: 'Description',
    });
    const rows = compareParameterAcrossTemplates(
      'active',
      active,
      templates,
      drafts,
      null,
    );

    assert.equal(rows[1].status, 'different');
    assert.equal(rows[1].differences[0].field, 'defaultValue');
    assert.equal(rows[1].differences[0].activeValue, 'draft value');
    assert.equal(rows[1].differences[0].activeIsDraft, true);
    assert.equal(rows[1].hasWorkingDraft, true);
  });

  it('lists bulk eligibility and stages values only for checked exact-name targets', () => {
    const active = declaration('declaration-0', 'shared', {
      access: 'internal',
      defaultValue: 'active old',
      description: 'active description',
    });
    const target = declaration('declaration-0', 'shared', {
      access: 'external',
      defaultValue: 'target old',
      description: 'target description',
    });
    const readonly = declaration('declaration-0', 'shared', {
      editableFields: { access: true, defaultValue: false, description: true },
    });
    const templates: LoadedParameterTemplate[] = [
      { fileId: 'active', fileName: 'active.dta', declarations: [active] },
      { fileId: 'target', fileName: 'target.dta', declarations: [target] },
      { fileId: 'missing', fileName: 'missing.dta', declarations: [] },
      {
        fileId: 'ambiguous',
        fileName: 'ambiguous.dta',
        declarations: [
          declaration('declaration-0', 'shared'),
          declaration('declaration-1', 'shared'),
        ],
      },
      {
        fileId: 'readonly',
        fileName: 'readonly.dta',
        declarations: [readonly],
      },
    ];
    const candidates = findBulkValueCandidates('shared', templates, new Map());
    assert.deepEqual(
      candidates.map((candidate) => candidate.status),
      ['eligible', 'eligible', 'missing', 'ambiguous', 'read-only'],
    );

    const selected = stageBulkDefaultValue(
      new Map(),
      candidates,
      new Set(['target']),
      'bulk value',
    );
    assert.equal(
      selected.get(draftKey('target', target.id, 'defaultValue'))?.proposed,
      'bulk value',
    );
    assert.equal(
      selected.has(draftKey('active', active.id, 'defaultValue')),
      false,
    );
    assert.equal(selected.has(draftKey('target', target.id, 'access')), false);
    assert.equal(
      selected.has(draftKey('target', target.id, 'description')),
      false,
    );
    assert.throws(
      () =>
        stageBulkDefaultValue(
          selected,
          candidates,
          new Set(['readonly']),
          'blocked',
        ),
      /not an eligible bulk target/u,
    );
  });
});
