import type { EditableField, ParameterDeclaration } from '../shared/types';

export interface DraftChange {
  fileId: string;
  declarationId: string;
  parameterName: string;
  field: EditableField;
  baseline: string;
  proposed: string;
}

export type DraftMap = Map<string, DraftChange>;

export interface ParameterEditorValues {
  access: string;
  defaultValue: string;
  description: string;
}

export interface ParameterEditorForm {
  fileId: string;
  declaration: ParameterDeclaration;
  values: ParameterEditorValues;
}

const EDITABLE_FIELDS: EditableField[] = [
  'access',
  'defaultValue',
  'description',
];

export function draftKey(
  fileId: string,
  declarationId: string,
  field: EditableField,
): string {
  return `${fileId}\u0000${declarationId}\u0000${field}`;
}

function fieldValue(
  declaration: ParameterDeclaration,
  field: EditableField,
): string | null {
  if (field === 'access') return declaration.access;
  if (field === 'defaultValue') return declaration.defaultValue;
  return declaration.description;
}

export function editorValues(
  fileId: string,
  declaration: ParameterDeclaration,
  drafts: DraftMap,
): ParameterEditorValues {
  const get = (field: EditableField): string =>
    drafts.get(draftKey(fileId, declaration.id, field))?.proposed ??
    fieldValue(declaration, field) ??
    '';
  return {
    access: get('access'),
    defaultValue: get('defaultValue'),
    description: get('description'),
  };
}

export function workingDeclaration(
  fileId: string,
  declaration: ParameterDeclaration,
  drafts: DraftMap,
): ParameterDeclaration {
  const effective = { ...declaration };
  for (const field of EDITABLE_FIELDS) {
    const draft = drafts.get(draftKey(fileId, declaration.id, field));
    if (!draft) continue;
    if (field === 'access') effective.access = draft.proposed;
    if (field === 'defaultValue') effective.defaultValue = draft.proposed;
    if (field === 'description') effective.description = draft.proposed;
  }
  return effective;
}

export function stageParameterEdits(
  currentDrafts: DraftMap,
  fileId: string,
  declaration: ParameterDeclaration,
  values: ParameterEditorValues,
): DraftMap {
  if (declaration.readOnlyReasons.length > 0) {
    throw new Error(
      'This declaration has an unsupported shape and is read-only.',
    );
  }

  const next = new Map(currentDrafts);
  const current = editorValues(fileId, declaration, currentDrafts);
  for (const field of EDITABLE_FIELDS) {
    const proposed = values[field];
    if (
      field === 'access' &&
      proposed !== 'internal' &&
      proposed !== 'external'
    ) {
      throw new Error('Access must be internal or external.');
    }
    if (proposed === current[field]) continue;
    if (declaration.editableFields?.[field] !== true) {
      throw new Error(`The ${field} field has no supported source span.`);
    }
    const baseline = fieldValue(declaration, field);
    if (baseline === null) {
      throw new Error(`The ${field} field is missing and cannot be edited.`);
    }
    const key = draftKey(fileId, declaration.id, field);
    if (proposed === baseline) {
      next.delete(key);
    } else {
      next.set(key, {
        fileId,
        declarationId: declaration.id,
        parameterName: declaration.name ?? 'Unnamed declaration',
        field,
        baseline,
        proposed,
      });
    }
  }
  return next;
}

export function stageParameterForms(
  currentDrafts: DraftMap,
  forms: readonly ParameterEditorForm[],
): DraftMap {
  let next = currentDrafts;
  for (const form of forms) {
    next = stageParameterEdits(
      next,
      form.fileId,
      form.declaration,
      form.values,
    );
  }
  return next;
}

export function hasDraftsForParameter(
  drafts: DraftMap,
  fileId: string,
  declarationId: string,
): boolean {
  return EDITABLE_FIELDS.some((field) =>
    drafts.has(draftKey(fileId, declarationId, field)),
  );
}

export function draftsForFile(drafts: DraftMap, fileId: string): number {
  let count = 0;
  for (const draft of drafts.values()) {
    if (draft.fileId === fileId) count += 1;
  }
  return count;
}
