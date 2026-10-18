import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { ParameterDeclaration } from '../../src/shared/types';
import {
  draftKey,
  draftsForFile,
  editorValues,
  hasDraftsForParameter,
  stageParameterEdits,
  stageParameterForms,
  workingDeclaration,
} from '../../src/core/workspace-drafts';

const declaration: ParameterDeclaration = {
  id: 'declaration-0',
  name: 'title',
  description: 'Original description',
  access: 'external',
  defaultValue: 'Original value',
  xmlType: 'user',
  readOnlyReasons: [],
  editableFields: {
    access: true,
    defaultValue: true,
    description: true,
  },
};

describe('workspace draft staging', () => {
  it('stages only edited fields without mutating the baseline declaration', () => {
    const drafts = stageParameterEdits(new Map(), 'file-a', declaration, {
      access: 'internal',
      defaultValue: 'Changed value',
      description: 'Original description',
    });

    assert.equal(drafts.size, 2);
    assert.equal(draftsForFile(drafts, 'file-a'), 2);
    assert.equal(declaration.access, 'external');
    assert.equal(declaration.defaultValue, 'Original value');
    assert.equal(declaration.description, 'Original description');
    assert.equal(
      drafts.get(draftKey('file-a', declaration.id, 'defaultValue'))?.proposed,
      'Changed value',
    );
    assert.equal(
      workingDeclaration('file-a', declaration, drafts).access,
      'internal',
    );
  });

  it('removes a draft when the proposal returns to its baseline', () => {
    const changed = stageParameterEdits(new Map(), 'file-a', declaration, {
      access: 'external',
      defaultValue: 'New value',
      description: 'New description',
    });
    const restored = stageParameterEdits(changed, 'file-a', declaration, {
      access: 'external',
      defaultValue: 'Original value',
      description: 'Original description',
    });

    assert.equal(restored.size, 0);
    assert.deepEqual(editorValues('file-a', declaration, restored), {
      access: 'external',
      defaultValue: 'Original value',
      description: 'Original description',
    });
    assert.equal(
      hasDraftsForParameter(changed, 'file-a', declaration.id),
      true,
    );
    assert.equal(
      hasDraftsForParameter(restored, 'file-a', declaration.id),
      false,
    );
  });

  it('keeps unsupported fields read-only while allowing supported fields', () => {
    const partial: ParameterDeclaration = {
      ...declaration,
      editableFields: { access: true, defaultValue: true, description: false },
    };

    const staged = stageParameterEdits(new Map(), 'file-a', partial, {
      access: 'external',
      defaultValue: 'New value',
      description: 'Original description',
    });
    assert.equal(staged.size, 1);
    assert.throws(
      () =>
        stageParameterEdits(staged, 'file-a', partial, {
          access: 'external',
          defaultValue: 'New value',
          description: 'Changed description',
        }),
      /no supported source span/u,
    );
  });

  it('keeps each template draft separate and rejects invalid access values', () => {
    const fileA = stageParameterEdits(new Map(), 'file-a', declaration, {
      access: 'internal',
      defaultValue: 'A',
      description: 'Original description',
    });
    const fileB = stageParameterEdits(fileA, 'file-b', declaration, {
      access: 'external',
      defaultValue: 'B',
      description: 'Original description',
    });

    assert.equal(draftsForFile(fileB, 'file-a'), 2);
    assert.equal(draftsForFile(fileB, 'file-b'), 1);
    assert.equal(
      workingDeclaration('file-a', declaration, fileB).defaultValue,
      'A',
    );
    assert.equal(
      workingDeclaration('file-b', declaration, fileB).defaultValue,
      'B',
    );
    assert.throws(
      () =>
        stageParameterEdits(new Map(), 'file-c', declaration, {
          access: 'shared',
          defaultValue: 'Original value',
          description: 'Original description',
        }),
      /internal or external/u,
    );
  });

  it('stages multiple unapplied editor forms while preserving existing drafts', () => {
    const existing = stageParameterEdits(
      new Map(),
      'file-existing',
      declaration,
      {
        access: 'external',
        defaultValue: 'Existing staged value',
        description: 'Original description',
      },
    );
    const secondDeclaration = {
      ...declaration,
      id: 'declaration-1',
      name: 'summary',
    };
    const staged = stageParameterForms(existing, [
      {
        fileId: 'file-a',
        declaration,
        values: {
          access: 'internal',
          defaultValue: 'Applied form A',
          description: 'Original description',
        },
      },
      {
        fileId: 'file-b',
        declaration: secondDeclaration,
        values: {
          access: 'external',
          defaultValue: 'Applied form B',
          description: 'Original description',
        },
      },
    ]);

    assert.equal(existing.size, 1);
    assert.equal(draftsForFile(staged, 'file-existing'), 1);
    assert.equal(draftsForFile(staged, 'file-a'), 2);
    assert.equal(draftsForFile(staged, 'file-b'), 1);
    assert.equal(
      workingDeclaration('file-a', declaration, staged).defaultValue,
      'Applied form A',
    );
    assert.equal(
      workingDeclaration('file-b', secondDeclaration, staged).defaultValue,
      'Applied form B',
    );
  });
});
