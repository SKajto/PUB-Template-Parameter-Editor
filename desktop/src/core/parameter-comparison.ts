import type { EditableField, ParameterDeclaration } from '../shared/types';
import {
  draftKey,
  editorValues,
  hasDraftsForParameter,
  stageParameterEdits,
  workingDeclaration,
  type DraftMap,
} from './workspace-drafts';

export interface LoadedParameterTemplate {
  fileId: string;
  fileName: string;
  declarations: ParameterDeclaration[];
}

export type ComparisonStatus =
  | 'active'
  | 'missing'
  | 'ambiguous'
  | 'equal'
  | 'different';

export interface ComparisonFieldDifference {
  field: EditableField;
  activeValue: string | null;
  targetValue: string | null;
  activeIsDraft: boolean;
  targetIsDraft: boolean;
}

export interface ParameterComparisonRow {
  fileId: string;
  fileName: string;
  status: ComparisonStatus;
  reason: string | null;
  hasWorkingDraft: boolean;
  differences: ComparisonFieldDifference[];
}

export type BulkCandidateStatus =
  | 'eligible'
  | 'missing'
  | 'ambiguous'
  | 'read-only';

export interface BulkValueCandidate {
  fileId: string;
  fileName: string;
  status: BulkCandidateStatus;
  reason: string | null;
  declaration: ParameterDeclaration | null;
  currentValue: string | null;
  currentValueIsDraft: boolean;
}

function fieldValue(
  declaration: ParameterDeclaration,
  field: EditableField,
): string | null {
  if (field === 'access') return declaration.access;
  if (field === 'defaultValue') return declaration.defaultValue;
  return declaration.description;
}

function hasFieldDraft(
  drafts: DraftMap,
  fileId: string,
  declarationId: string,
  field: EditableField,
): boolean {
  return drafts.has(draftKey(fileId, declarationId, field));
}

function hasDuplicateName(declaration: ParameterDeclaration): boolean {
  return declaration.readOnlyReasons.some((reason) =>
    reason.startsWith('Duplicate declaration name:'),
  );
}

export function compareParameterAcrossTemplates(
  activeFileId: string,
  activeDeclaration: ParameterDeclaration,
  templates: LoadedParameterTemplate[],
  drafts: DraftMap,
  selectedFileIds: ReadonlySet<string> | null,
): ParameterComparisonRow[] {
  const active = workingDeclaration(activeFileId, activeDeclaration, drafts);
  const activeName = activeDeclaration.name;
  const activeAmbiguous =
    activeName === null || hasDuplicateName(activeDeclaration);
  const activeHasDraft = hasDraftsForParameter(
    drafts,
    activeFileId,
    activeDeclaration.id,
  );
  const rows: ParameterComparisonRow[] = [];

  for (const template of templates) {
    const isActive = template.fileId === activeFileId;
    if (
      !isActive &&
      selectedFileIds !== null &&
      !selectedFileIds.has(template.fileId)
    ) {
      continue;
    }
    if (isActive) {
      rows.push({
        fileId: template.fileId,
        fileName: template.fileName,
        status: 'active',
        reason: null,
        hasWorkingDraft: activeHasDraft,
        differences: [],
      });
      continue;
    }
    if (activeAmbiguous) {
      rows.push({
        fileId: template.fileId,
        fileName: template.fileName,
        status: 'ambiguous',
        reason: 'The active declaration name is missing or duplicated.',
        hasWorkingDraft: activeHasDraft,
        differences: [],
      });
      continue;
    }

    const matches = template.declarations.filter(
      (declaration) => declaration.name === activeName,
    );
    if (matches.length === 0) {
      rows.push({
        fileId: template.fileId,
        fileName: template.fileName,
        status: 'missing',
        reason: 'No declaration with this exact name is loaded.',
        hasWorkingDraft: activeHasDraft,
        differences: [],
      });
      continue;
    }
    if (matches.length > 1 || hasDuplicateName(matches[0])) {
      rows.push({
        fileId: template.fileId,
        fileName: template.fileName,
        status: 'ambiguous',
        reason:
          'This template has more than one declaration with this exact name.',
        hasWorkingDraft:
          activeHasDraft ||
          matches.some((declaration) =>
            hasDraftsForParameter(drafts, template.fileId, declaration.id),
          ),
        differences: [],
      });
      continue;
    }

    const match = matches[0];
    const target = workingDeclaration(template.fileId, match, drafts);
    const differences = (['defaultValue', 'access', 'description'] as const)
      .filter(
        (field) => fieldValue(active, field) !== fieldValue(target, field),
      )
      .map((field) => ({
        field,
        activeValue: fieldValue(active, field),
        targetValue: fieldValue(target, field),
        activeIsDraft: hasFieldDraft(
          drafts,
          activeFileId,
          activeDeclaration.id,
          field,
        ),
        targetIsDraft: hasFieldDraft(drafts, template.fileId, match.id, field),
      }));
    rows.push({
      fileId: template.fileId,
      fileName: template.fileName,
      status: differences.length === 0 ? 'equal' : 'different',
      reason: null,
      hasWorkingDraft:
        activeHasDraft ||
        hasDraftsForParameter(drafts, template.fileId, match.id),
      differences,
    });
  }
  return rows;
}

export function findBulkValueCandidates(
  exactName: string,
  templates: LoadedParameterTemplate[],
  drafts: DraftMap,
): BulkValueCandidate[] {
  return templates.map((template) => {
    const matches = template.declarations.filter(
      (declaration) => declaration.name === exactName,
    );
    if (matches.length === 0) {
      return {
        fileId: template.fileId,
        fileName: template.fileName,
        status: 'missing',
        reason: 'No declaration with this exact name is loaded.',
        declaration: null,
        currentValue: null,
        currentValueIsDraft: false,
      };
    }
    if (matches.length > 1 || hasDuplicateName(matches[0])) {
      return {
        fileId: template.fileId,
        fileName: template.fileName,
        status: 'ambiguous',
        reason: 'Multiple declarations with this exact name are present.',
        declaration: null,
        currentValue: null,
        currentValueIsDraft: false,
      };
    }

    const declaration = matches[0];
    const current = workingDeclaration(template.fileId, declaration, drafts);
    const readOnlyReason =
      declaration.readOnlyReasons[0] ??
      (declaration.editableFields?.defaultValue !== true
        ? 'The default value has no supported source span.'
        : declaration.defaultValue === null
          ? 'The default value is missing.'
          : null);
    if (readOnlyReason !== null) {
      return {
        fileId: template.fileId,
        fileName: template.fileName,
        status: 'read-only',
        reason: readOnlyReason,
        declaration,
        currentValue: current.defaultValue,
        currentValueIsDraft: hasFieldDraft(
          drafts,
          template.fileId,
          declaration.id,
          'defaultValue',
        ),
      };
    }
    return {
      fileId: template.fileId,
      fileName: template.fileName,
      status: 'eligible',
      reason: null,
      declaration,
      currentValue: current.defaultValue,
      currentValueIsDraft: hasFieldDraft(
        drafts,
        template.fileId,
        declaration.id,
        'defaultValue',
      ),
    };
  });
}

/** Stage the proposed default only for explicitly selected eligible targets. */
export function stageBulkDefaultValue(
  currentDrafts: DraftMap,
  candidates: BulkValueCandidate[],
  selectedFileIds: ReadonlySet<string>,
  proposedValue: string,
): DraftMap {
  let next = new Map(currentDrafts);
  for (const fileId of selectedFileIds) {
    const candidate = candidates.find((item) => item.fileId === fileId);
    if (
      !candidate ||
      candidate.status !== 'eligible' ||
      !candidate.declaration
    ) {
      throw new Error(`Template ${fileId} is not an eligible bulk target.`);
    }
    const values = editorValues(fileId, candidate.declaration, next);
    next = stageParameterEdits(next, fileId, candidate.declaration, {
      ...values,
      defaultValue: proposedValue,
    });
  }
  return next;
}
