import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent,
  type PointerEvent,
} from 'react';
import packageInfo from '../package.json';
import runtimeLibraries from './runtime-libraries.json';
import type {
  EditableField,
  ParameterDeclaration,
  ReadProgress,
  ReadTemplateResult,
  ReviewedFieldChange,
  ThemePreference,
  TemplateFile,
  WorkspaceState,
} from './shared/types';
import {
  draftsForFile,
  editorValues,
  hasDraftsForParameter,
  stageParameterEdits,
  stageParameterForms,
  workingDeclaration,
  type DraftMap,
  type ParameterEditorValues,
} from './core/workspace-drafts';
import { matchesParameterSearch } from './core/parameter-search';
import {
  compareParameterAcrossTemplates,
  findBulkValueCandidates,
  stageBulkDefaultValue,
  type LoadedParameterTemplate,
} from './core/parameter-comparison';

type AccessFilter = 'all' | 'internal' | 'external';
type PendingNavigation = 'close' | 'folder' | 'read';

const INITIAL_STATE: WorkspaceState = {
  folderPath: null,
  files: [],
  theme: 'system',
  error: null,
  recoveryBatches: [],
};

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function displayValue(value: string | null): string {
  if (value === null) return 'Missing';
  if (value === '') return 'Empty string';
  return value;
}

function fieldLabel(field: EditableField): string {
  if (field === 'access') return 'Access';
  if (field === 'defaultValue') return 'Default value';
  return 'Description';
}

function getFileStatus(
  file: TemplateFile,
  result: ReadTemplateResult | undefined,
  draftCount: number,
): string {
  if (file.error) return 'Unavailable';
  if (!result) return 'Not read';
  if (result.error) return 'Read error';
  return `${result.declarations.length} parameters${draftCount ? ` · ${draftCount} staged` : ''}`;
}

function hasChangedEditorValues(
  values: ParameterEditorValues,
  declaration: ParameterDeclaration,
): boolean {
  return (
    values.access !== (declaration.access ?? '') ||
    values.defaultValue !== (declaration.defaultValue ?? '') ||
    values.description !== (declaration.description ?? '')
  );
}

function editorFormKey(fileId: string, declarationId: string): string {
  return `${fileId}\\u0000${declarationId}`;
}

function effectiveTheme(preference: ThemePreference): 'light' | 'dark' {
  if (preference !== 'system') return preference;
  return window.matchMedia('(prefers-color-scheme: dark)').matches
    ? 'dark'
    : 'light';
}

export function App() {
  const [workspace, setWorkspace] = useState(INITIAL_STATE);
  const [detailPanelWidth, setDetailPanelWidth] = useState(360);
  const [isInitializing, setIsInitializing] = useState(true);
  const [folderQuery, setFolderQuery] = useState('');
  const [parameterQuery, setParameterQuery] = useState('');
  const [accessFilter, setAccessFilter] = useState<AccessFilter>('all');
  const [selectedFileIds, setSelectedFileIds] = useState<Set<string>>(
    () => new Set(),
  );
  const [activeFileId, setActiveFileId] = useState<string | null>(null);
  const [selectedParameterId, setSelectedParameterId] = useState<string | null>(
    null,
  );
  const [readResults, setReadResults] = useState<
    Map<string, ReadTemplateResult>
  >(() => new Map());
  const [drafts, setDrafts] = useState<DraftMap>(() => new Map());
  const [editorForms, setEditorForms] = useState<
    Map<string, ParameterEditorValues>
  >(() => new Map());
  const [isReviewOpen, setIsReviewOpen] = useState(false);
  const [pendingNavigation, setPendingNavigation] =
    useState<PendingNavigation | null>(null);
  const [reviewIncluded, setReviewIncluded] = useState<Set<string>>(
    () => new Set(),
  );
  const [reviewProposals, setReviewProposals] = useState<Map<string, string>>(
    () => new Map(),
  );
  const [isSaving, setIsSaving] = useState(false);
  const [recoveringBatchId, setRecoveringBatchId] = useState<string | null>(
    null,
  );
  const [toolMode, setToolMode] = useState<'compare' | 'bulk' | null>(null);
  const [comparisonSelection, setComparisonSelection] = useState<Set<string>>(
    () => new Set(),
  );
  const [bulkSelection, setBulkSelection] = useState<Set<string>>(
    () => new Set(),
  );
  const [bulkValue, setBulkValue] = useState('');
  const [isReading, setIsReading] = useState(false);
  const [progress, setProgress] = useState<ReadProgress | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionNotice, setActionNotice] = useState<string | null>(null);
  const [isAboutOpen, setIsAboutOpen] = useState(false);
  const aboutTriggerRef = useRef<HTMLButtonElement>(null);
  const aboutCloseRef = useRef<HTMLButtonElement>(null);

  const resizeDetailsPanel = (event: PointerEvent<HTMLDivElement>): void => {
    event.preventDefault();
    const workspaceElement = event.currentTarget.parentElement;
    if (!workspaceElement) return;
    const startX = event.clientX;
    const startWidth = detailPanelWidth;
    const maxWidth = Math.max(
      340,
      Math.min(520, workspaceElement.clientWidth - 258 - 420 - 8),
    );
    const handleMove = (moveEvent: globalThis.PointerEvent): void => {
      const nextWidth = Math.max(
        340,
        Math.min(maxWidth, startWidth - (moveEvent.clientX - startX)),
      );
      setDetailPanelWidth(nextWidth);
    };
    const stopResizing = (): void => {
      window.removeEventListener('pointermove', handleMove);
      window.removeEventListener('pointerup', stopResizing);
      window.removeEventListener('pointercancel', stopResizing);
    };
    window.addEventListener('pointermove', handleMove);
    window.addEventListener('pointerup', stopResizing, { once: true });
    window.addEventListener('pointercancel', stopResizing, { once: true });
  };

  const resizeDetailsPanelByKeyboard = (
    event: KeyboardEvent<HTMLDivElement>,
  ): void => {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
    event.preventDefault();
    const workspaceWidth = event.currentTarget.parentElement?.clientWidth ?? 0;
    const maxWidth = Math.max(
      340,
      Math.min(520, workspaceWidth - 258 - 420 - 8),
    );
    const direction = event.key === 'ArrowLeft' ? 1 : -1;
    setDetailPanelWidth((current) =>
      Math.max(340, Math.min(maxWidth, current + direction * 16)),
    );
  };

  useEffect(() => {
    document.documentElement.dataset.theme = effectiveTheme(workspace.theme);
  }, [workspace.theme]);

  useEffect(() => {
    if (!isAboutOpen) return;
    aboutCloseRef.current?.focus();
    const handleKeyDown = (event: globalThis.KeyboardEvent): void => {
      if (event.key === 'Escape') setIsAboutOpen(false);
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      aboutTriggerRef.current?.focus();
    };
  }, [isAboutOpen]);

  useEffect(
    () =>
      window.templateEditor.onCloseRequested(() => {
        setPendingNavigation('close');
        setActionError(null);
        setActionNotice(null);
      }),
    [],
  );

  useEffect(() => {
    if (actionNotice === null) return;
    const timer = window.setTimeout(() => setActionNotice(null), 8_000);
    return () => window.clearTimeout(timer);
  }, [actionNotice]);

  useEffect(() => {
    let mounted = true;
    const unsubscribe = window.templateEditor.onReadProgress((nextProgress) => {
      if (mounted) setProgress(nextProgress);
    });
    window.templateEditor
      .getInitialState()
      .then((state) => {
        if (mounted) setWorkspace(state);
      })
      .catch((error: unknown) => {
        if (mounted) {
          setActionError(
            error instanceof Error
              ? error.message
              : 'Could not initialize the workspace.',
          );
        }
      })
      .finally(() => {
        if (mounted) setIsInitializing(false);
      });
    return () => {
      mounted = false;
      unsubscribe();
    };
  }, []);

  const visibleFiles = useMemo(() => {
    const query = folderQuery.trim().toLocaleLowerCase();
    return workspace.files.filter((file) =>
      file.name.toLocaleLowerCase().includes(query),
    );
  }, [folderQuery, workspace.files]);

  const activeFile =
    workspace.files.find((file) => file.id === activeFileId) ?? null;
  const activeResult = activeFileId ? readResults.get(activeFileId) : undefined;
  const declarations = activeResult?.declarations ?? [];
  const workingDeclarations = activeFileId
    ? declarations.map((declaration) =>
        workingDeclaration(activeFileId, declaration, drafts),
      )
    : [];

  const visibleDeclarations = useMemo(() => {
    const query = parameterQuery.trim();
    return workingDeclarations.filter((declaration) => {
      const matchesQuery = matchesParameterSearch(declaration, query);
      const matchesAccess =
        accessFilter === 'all' || declaration.access === accessFilter;
      return matchesQuery && matchesAccess;
    });
  }, [accessFilter, parameterQuery, workingDeclarations]);

  const selectedParameterBaseline =
    declarations.find(
      (declaration) => declaration.id === selectedParameterId,
    ) ?? null;
  const selectedParameter =
    activeFileId && selectedParameterBaseline
      ? workingDeclaration(activeFileId, selectedParameterBaseline, drafts)
      : null;
  const selectedEditorKey =
    activeFileId && selectedParameterBaseline
      ? editorFormKey(activeFileId, selectedParameterBaseline.id)
      : null;
  const selectedEditorValues =
    activeFileId && selectedParameterBaseline
      ? (editorForms.get(selectedEditorKey!) ??
        editorValues(activeFileId, selectedParameterBaseline, drafts))
      : null;
  const canApplyEdits =
    selectedParameter !== null &&
    selectedEditorValues !== null &&
    selectedParameter.readOnlyReasons.length === 0 &&
    hasChangedEditorValues(selectedEditorValues, selectedParameter);
  const reviewItems = [...drafts.entries()].map(([key, draft]) => ({
    key,
    draft,
    included: reviewIncluded.has(key),
  }));
  const includedReviewCount = reviewItems.filter(
    (item) => item.included,
  ).length;
  const loadedTemplates: LoadedParameterTemplate[] = workspace.files.flatMap(
    (file) => {
      const result = readResults.get(file.id);
      if (!result || result.error) return [];
      return [
        {
          fileId: file.id,
          fileName: file.name,
          declarations: result.declarations,
        },
      ];
    },
  );
  const comparisonRows =
    activeFileId !== null && selectedParameterBaseline !== null
      ? compareParameterAcrossTemplates(
          activeFileId,
          selectedParameterBaseline,
          loadedTemplates,
          drafts,
          comparisonSelection.size === 0 ? null : comparisonSelection,
        )
      : [];
  const bulkCandidates =
    selectedParameterBaseline?.name !== null &&
    selectedParameterBaseline?.name !== undefined
      ? findBulkValueCandidates(
          selectedParameterBaseline.name,
          loadedTemplates,
          drafts,
        )
      : [];
  const eligibleBulkCount = bulkCandidates.filter(
    (candidate) => candidate.status === 'eligible',
  ).length;
  const reviewGroups = new Map<
    string,
    { fileName: string; items: typeof reviewItems }
  >();
  for (const item of reviewItems) {
    const fileName =
      workspace.files.find((file) => file.id === item.draft.fileId)?.name ??
      'Unknown template';
    const group = reviewGroups.get(item.draft.fileId) ?? {
      fileName,
      items: [],
    };
    group.items.push(item);
    reviewGroups.set(item.draft.fileId, group);
  }
  const explicitSelection = selectedFileIds.size > 0;
  const readTargetIds = explicitSelection
    ? [...selectedFileIds]
    : visibleFiles.map((file) => file.id);
  const folderLabel = workspace.folderPath
    ? (workspace.folderPath.split(/[\\/]/).filter(Boolean).at(-1) ??
      workspace.folderPath)
    : null;

  const clearFolderState = (): void => {
    setSelectedFileIds(new Set());
    setActiveFileId(null);
    setSelectedParameterId(null);
    setReadResults(new Map());
    setDrafts(new Map());
    setEditorForms(new Map());
    setReviewIncluded(new Set());
    setReviewProposals(new Map());
    setIsReviewOpen(false);
    setPendingNavigation(null);
    setToolMode(null);
    setFolderQuery('');
    setParameterQuery('');
    setActionError(null);
    setActionNotice(null);
  };

  const hasPendingWork = drafts.size > 0 || editorForms.size > 0;

  useEffect(() => {
    window.templateEditor.reportPendingWork(hasPendingWork);
  }, [hasPendingWork]);

  const performFolderChange = async (): Promise<void> => {
    setActionError(null);
    try {
      const state = await window.templateEditor.chooseFolder();
      const sameFileIds =
        state.files.length === workspace.files.length &&
        state.files.every(
          (file, index) => file.id === workspace.files[index]?.id,
        );
      if (state.folderPath !== workspace.folderPath || !sameFileIds) {
        clearFolderState();
      }
      setWorkspace(state);
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : 'Could not choose that folder.',
      );
    }
  };

  const chooseFolder = async (): Promise<void> => {
    if (hasPendingWork) {
      setPendingNavigation('folder');
      setActionError(null);
      return;
    }
    await performFolderChange();
  };

  const changeTheme = async (theme: ThemePreference): Promise<void> => {
    setActionError(null);
    try {
      setWorkspace(await window.templateEditor.setTheme(theme));
    } catch (error) {
      setActionError(
        error instanceof Error ? error.message : 'Could not save the theme.',
      );
    }
  };

  const updateEditorValue = (field: EditableField, value: string): void => {
    if (selectedEditorKey === null || selectedEditorValues === null) return;
    setEditorForms((current) =>
      new Map(current).set(selectedEditorKey, {
        ...selectedEditorValues,
        [field]: value,
      }),
    );
    window.templateEditor.reportPendingWork(true);
    setActionError(null);
  };

  const applySelectedEdits = (): void => {
    if (
      activeFileId === null ||
      selectedParameterBaseline === null ||
      selectedEditorValues === null ||
      selectedEditorKey === null
    ) {
      return;
    }
    try {
      const nextDrafts = stageParameterEdits(
        drafts,
        activeFileId,
        selectedParameterBaseline,
        selectedEditorValues,
      );
      const nextForms = new Map(editorForms);
      nextForms.delete(selectedEditorKey);
      setDrafts(nextDrafts);
      setEditorForms(nextForms);
      window.templateEditor.reportPendingWork(
        nextDrafts.size > 0 || nextForms.size > 0,
      );
      setActionError(null);
    } catch (error) {
      setActionError(
        error instanceof Error ? error.message : 'Could not stage these edits.',
      );
    }
  };

  const resetSelectedForm = (): void => {
    if (selectedEditorKey === null) return;
    const nextForms = new Map(editorForms);
    nextForms.delete(selectedEditorKey);
    setEditorForms(nextForms);
    window.templateEditor.reportPendingWork(
      drafts.size > 0 || nextForms.size > 0,
    );
  };

  const beginReview = (reviewDrafts: DraftMap): void => {
    if (reviewDrafts.size === 0) {
      setActionError('There are no staged changes to review.');
      return;
    }
    const currentKeys = new Set(reviewDrafts.keys());
    setReviewIncluded((current) => {
      const next = new Set([...current].filter((key) => currentKeys.has(key)));
      for (const [key, draft] of reviewDrafts) {
        if (reviewProposals.get(key) !== draft.proposed) next.add(key);
      }
      return next;
    });
    setReviewProposals(
      new Map([...reviewDrafts].map(([key, draft]) => [key, draft.proposed])),
    );
    setPendingNavigation(null);
    setToolMode(null);
    setIsReviewOpen(true);
    setActionError(null);
    setActionNotice(null);
  };

  const openReview = (): void => {
    if (editorForms.size > 0) {
      setActionError('Apply or reset unapplied editor values before review.');
      return;
    }
    beginReview(drafts);
  };

  const stageAllEditorForms = (): DraftMap => {
    const forms: Array<{
      fileId: string;
      declaration: ParameterDeclaration;
      values: ParameterEditorValues;
    }> = [];
    const consumed = new Set<string>();
    for (const [fileId, result] of readResults) {
      if (result.error !== null) continue;
      for (const declaration of result.declarations) {
        const key = editorFormKey(fileId, declaration.id);
        const values = editorForms.get(key);
        if (!values) continue;
        forms.push({ fileId, declaration, values });
        consumed.add(key);
      }
    }
    if (consumed.size !== editorForms.size) {
      throw new Error(
        'Some unapplied editor forms are no longer loaded. Read or reset them before continuing.',
      );
    }
    return stageParameterForms(drafts, forms);
  };

  const openComparison = (): void => {
    if (selectedParameterBaseline === null || activeFileId === null) return;
    if (editorForms.size > 0) {
      setActionError(
        'Apply or reset unapplied editor values before comparing.',
      );
      return;
    }
    setIsReviewOpen(false);
    setToolMode('compare');
    setActionError(null);
  };

  const toggleComparisonTarget = (fileId: string): void => {
    if (fileId === activeFileId) return;
    setComparisonSelection((current) => {
      const next =
        current.size === 0
          ? new Set(loadedTemplates.map((template) => template.fileId))
          : new Set(current);
      if (next.has(fileId)) next.delete(fileId);
      else next.add(fileId);
      return next;
    });
  };

  const compareAllLoaded = (): void => setComparisonSelection(new Set());

  const openBulkEditor = (): void => {
    if (selectedParameterBaseline === null || activeFileId === null) return;
    if (editorForms.size > 0) {
      setActionError(
        'Apply or reset unapplied editor values before bulk staging.',
      );
      return;
    }
    setBulkSelection(new Set());
    setBulkValue(
      editorValues(activeFileId, selectedParameterBaseline, drafts)
        .defaultValue,
    );
    setIsReviewOpen(false);
    setToolMode('bulk');
    setActionError(null);
  };

  const toggleBulkTarget = (fileId: string): void => {
    setBulkSelection((current) => {
      const next = new Set(current);
      if (next.has(fileId)) next.delete(fileId);
      else next.add(fileId);
      return next;
    });
  };

  const selectAllBulkTargets = (): void => {
    setBulkSelection(
      new Set(
        bulkCandidates
          .filter((candidate) => candidate.status === 'eligible')
          .map((candidate) => candidate.fileId),
      ),
    );
  };

  const applyBulkTargets = (): void => {
    if (bulkSelection.size === 0) return;
    try {
      const changedCount = [...bulkSelection].filter((fileId) =>
        bulkCandidates.some(
          (candidate) =>
            candidate.fileId === fileId && candidate.currentValue !== bulkValue,
        ),
      ).length;
      const next = stageBulkDefaultValue(
        drafts,
        bulkCandidates,
        bulkSelection,
        bulkValue,
      );
      setDrafts(next);
      window.templateEditor.reportPendingWork(
        next.size > 0 || editorForms.size > 0,
      );
      setActionNotice(
        changedCount === 0
          ? 'No new drafts were needed; the selected templates already have this working value.'
          : `Staged value changes for ${changedCount} template${changedCount === 1 ? '' : 's'}. Only default values changed; no files were written.`,
      );
      setBulkSelection(new Set());
      setToolMode(null);
      setActionError(null);
    } catch (error) {
      setActionError(
        error instanceof Error ? error.message : 'Could not stage bulk values.',
      );
    }
  };

  const toggleReviewItem = (key: string): void => {
    setReviewIncluded((current) => {
      const next = new Set(current);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const saveReviewedChanges = async (): Promise<void> => {
    if (includedReviewCount === 0 || isSaving) return;
    setIsSaving(true);
    setActionError(null);
    setActionNotice(null);
    const changes: ReviewedFieldChange[] = reviewItems.map(
      ({ draft, included }) => ({
        fileId: draft.fileId,
        declarationId: draft.declarationId,
        field: draft.field,
        proposed: draft.proposed,
        included,
      }),
    );
    try {
      const result = await window.templateEditor.saveReviewed(changes);
      if (!result.ok) {
        const statuses = result.outcomes
          .map((outcome) => `${outcome.fileName}: ${outcome.status}`)
          .join('; ');
        setActionError(
          [result.error ?? 'Save failed.', statuses, result.journalPath]
            .filter(Boolean)
            .join(' '),
        );
        return;
      }
      setReadResults((current) => {
        const next = new Map(current);
        for (const updated of result.updatedTemplates) {
          next.set(updated.fileId, updated);
        }
        return next;
      });
      setDrafts(new Map());
      setEditorForms(new Map());
      window.templateEditor.reportPendingWork(false);
      setReviewIncluded(new Set());
      setReviewProposals(new Map());
      setIsReviewOpen(false);
      setActionNotice(
        `Saved ${result.outcomes.length} template${result.outcomes.length === 1 ? '' : 's'}. Excluded field changes were reverted to baseline.`,
      );
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : 'Could not save the reviewed changes.',
      );
    } finally {
      setIsSaving(false);
    }
  };

  const discardPendingWork = (): void => {
    if (!hasPendingWork) return;
    const confirmed = window.confirm(
      `Discard ${drafts.size} staged field change${drafts.size === 1 ? '' : 's'} and ${editorForms.size} unapplied editor form${editorForms.size === 1 ? '' : 's'}? This cannot be undone.`,
    );
    if (!confirmed) return;
    setDrafts(new Map());
    setEditorForms(new Map());
    window.templateEditor.reportPendingWork(false);
    setReviewIncluded(new Set());
    setReviewProposals(new Map());
    setIsReviewOpen(false);
    setToolMode(null);
    setActionError(null);
    setActionNotice(null);
  };

  const recoverInterruptedSave = async (batchId: string): Promise<void> => {
    if (hasPendingWork || recoveringBatchId !== null) {
      setActionError(
        'Reset or discard pending edits before restoring an interrupted save.',
      );
      return;
    }
    setRecoveringBatchId(batchId);
    setActionError(null);
    try {
      const result =
        await window.templateEditor.recoverInterruptedSave(batchId);
      setWorkspace(result.workspace);
      if (!result.ok) {
        const statuses = result.outcomes
          .map(
            (outcome) =>
              `${outcome.fileName}: ${outcome.status} (${outcome.message})`,
          )
          .join('; ');
        setActionError([result.error, statuses].filter(Boolean).join(' '));
        return;
      }
      clearFolderState();
      setWorkspace(result.workspace);
      setActionNotice(
        'Interrupted save recovery verified the original files. Read the templates again before editing.',
      );
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : 'Could not recover the interrupted save.',
      );
    } finally {
      setRecoveringBatchId(null);
    }
  };

  const toggleReadSelection = (fileId: string): void => {
    setSelectedFileIds((current) => {
      const next = new Set(current);
      if (next.has(fileId)) next.delete(fileId);
      else next.add(fileId);
      return next;
    });
  };

  const selectVisible = (): void => {
    setSelectedFileIds(
      (current) =>
        new Set([...current, ...visibleFiles.map((file) => file.id)]),
    );
  };

  const clearVisibleSelection = (): void => {
    setSelectedFileIds((current) => {
      const next = new Set(current);
      for (const file of visibleFiles) next.delete(file.id);
      return next;
    });
  };

  const performReadParameters = async (): Promise<void> => {
    if (readTargetIds.length === 0 || isReading) return;
    setIsReading(true);
    setProgress({ completed: 0, total: readTargetIds.length, fileName: '' });
    setActionError(null);
    try {
      const results = await window.templateEditor.readTemplates(readTargetIds);
      setReadResults((current) => {
        const next = new Map(current);
        for (const result of results) next.set(result.fileId, result);
        return next;
      });
      setActiveFileId((current) => current ?? results[0]?.fileId ?? null);
      setSelectedParameterId(null);
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : 'Could not read the selected templates.',
      );
    } finally {
      setIsReading(false);
      setProgress(null);
    }
  };

  const readParameters = async (): Promise<void> => {
    if (hasPendingWork) {
      setPendingNavigation('read');
      setActionError(null);
      return;
    }
    await performReadParameters();
  };

  const continuePendingNavigation = (action: PendingNavigation): void => {
    if (action === 'close') {
      window.templateEditor.confirmClose();
    } else if (action === 'folder') {
      void performFolderChange();
    } else {
      void performReadParameters();
    }
  };

  useEffect(() => {
    if (pendingNavigation === null || hasPendingWork) return;
    const action = pendingNavigation;
    setPendingNavigation(null);
    continuePendingNavigation(action);
  }, [hasPendingWork, pendingNavigation]);

  const applyEditorFormsForNavigation = (): void => {
    if (pendingNavigation === null || editorForms.size === 0) return;
    try {
      const nextDrafts = stageAllEditorForms();
      const action = pendingNavigation;
      setDrafts(nextDrafts);
      setEditorForms(new Map());
      window.templateEditor.reportPendingWork(nextDrafts.size > 0);
      setActionError(null);
      if (nextDrafts.size === 0) {
        setPendingNavigation(null);
        continuePendingNavigation(action);
      }
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : 'Could not apply all pending editor forms.',
      );
    }
  };

  const reviewPendingNavigation = (): void => {
    if (pendingNavigation === null || editorForms.size > 0) return;
    if (isReviewOpen) {
      setPendingNavigation(null);
      setActionError(null);
      return;
    }
    beginReview(drafts);
  };

  const discardAndContinueNavigation = (): void => {
    if (pendingNavigation === null) return;
    const action = pendingNavigation;
    setDrafts(new Map());
    setEditorForms(new Map());
    window.templateEditor.reportPendingWork(false);
    setReviewIncluded(new Set());
    setReviewProposals(new Map());
    setIsReviewOpen(false);
    setToolMode(null);
    setPendingNavigation(null);
    setActionError(null);
    setActionNotice(null);
    continuePendingNavigation(action);
  };

  const cancelPendingNavigation = (): void => {
    setPendingNavigation(null);
    setActionError(null);
  };

  const selectActiveFile = (fileId: string): void => {
    setActiveFileId(fileId);
    setSelectedParameterId(null);
    setParameterQuery('');
    setAccessFilter('all');
  };

  const scopeDescription = explicitSelection
    ? `Reading ${readTargetIds.length} explicitly selected template${readTargetIds.length === 1 ? '' : 's'}.`
    : `No read selection is active; reading all ${visibleFiles.length} currently visible template${visibleFiles.length === 1 ? '' : 's'}.`;

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand-mark" aria-hidden="true">
          DTA
        </div>
        <div>
          <h1 className="brand-title">PUB Template Parameter Editor</h1>
          <p className="brand-subtitle">Local template workspace</p>
        </div>
        <div className="topbar-spacer" />
        <div className="theme-controls" role="group" aria-label="Color theme">
          {(['light', 'dark', 'system'] as const).map((theme) => (
            <button
              aria-pressed={workspace.theme === theme}
              className={
                workspace.theme === theme
                  ? 'theme-button selected'
                  : 'theme-button'
              }
              key={theme}
              onClick={() => void changeTheme(theme)}
              type="button"
            >
              {theme[0].toUpperCase() + theme.slice(1)}
            </button>
          ))}
        </div>
        {drafts.size > 0 && (
          <button
            className="primary-button review-button"
            disabled={editorForms.size > 0}
            onClick={openReview}
            type="button"
          >
            Review and save ({drafts.size})
          </button>
        )}
        {hasPendingWork && (
          <button
            className="secondary-button"
            onClick={discardPendingWork}
            type="button"
          >
            Discard all ({drafts.size + editorForms.size})
          </button>
        )}
        <button
          className="primary-button folder-button"
          onClick={() => void chooseFolder()}
          type="button"
        >
          Choose folder
        </button>
      </header>

      <div
        className="workspace"
        style={
          { '--detail-panel-width': `${detailPanelWidth}px` } as CSSProperties
        }
      >
        <aside className="sidebar" aria-label="Template explorer">
          <div className="sidebar-heading">
            <div>
              <p className="eyebrow">TEMPLATES</p>
              <p
                className="folder-name"
                title={workspace.folderPath ?? undefined}
              >
                {folderLabel ?? 'No folder selected'}
              </p>
            </div>
            <span className="file-count">{workspace.files.length}</span>
          </div>
          <label className="search-label" htmlFor="folder-filter">
            Filter files
          </label>
          <input
            id="folder-filter"
            className="text-input"
            onChange={(event) => setFolderQuery(event.target.value)}
            placeholder="Filename…"
            value={folderQuery}
          />
          <div className="selection-tools">
            <button
              className="text-button"
              onClick={selectVisible}
              type="button"
            >
              Select visible
            </button>
            <button
              className="text-button"
              onClick={clearVisibleSelection}
              type="button"
            >
              Clear visible
            </button>
          </div>
          <p className="selection-count">
            {selectedFileIds.size} selected for reading
          </p>
          <div className="file-list" role="list" aria-label="DTA templates">
            {visibleFiles.map((file) => {
              const result = readResults.get(file.id);
              const isActive = file.id === activeFileId;
              return (
                <div
                  className={isActive ? 'file-row active' : 'file-row'}
                  key={file.id}
                  role="listitem"
                >
                  <input
                    aria-label={`Select ${file.name} for reading`}
                    checked={selectedFileIds.has(file.id)}
                    disabled={Boolean(file.error)}
                    onChange={() => toggleReadSelection(file.id)}
                    type="checkbox"
                  />
                  <button
                    aria-current={isActive ? 'true' : undefined}
                    className="file-focus"
                    onClick={() => selectActiveFile(file.id)}
                    type="button"
                  >
                    <span className="file-name" title={file.name}>
                      {file.name}
                    </span>
                    <span className="file-meta">
                      {formatSize(file.size)} ·{' '}
                      {getFileStatus(
                        file,
                        result,
                        draftsForFile(drafts, file.id),
                      )}
                    </span>
                  </button>
                </div>
              );
            })}
            {visibleFiles.length === 0 && (
              <div className="list-empty">
                {workspace.folderPath
                  ? 'No matching .dta files.'
                  : 'Choose a folder to list .dta files.'}
              </div>
            )}
          </div>
          <div className="read-scope">
            <p>{scopeDescription}</p>
            <button
              className="primary-button read-button"
              disabled={
                isInitializing ||
                isReading ||
                readTargetIds.length === 0 ||
                workspace.recoveryBatches.length > 0
              }
              onClick={() => void readParameters()}
              type="button"
            >
              {isReading ? 'Reading…' : 'Read parameters'}
            </button>
          </div>
        </aside>

        <main className="main-panel">
          <div className="main-toolbar">
            <div className="active-heading">
              <p className="eyebrow">ACTIVE TEMPLATE</p>
              <h2 title={activeFile?.name ?? undefined}>
                {activeFile?.name ?? 'Parameter browser'}
              </h2>
            </div>
            <div className="parameter-filters">
              <label className="visually-hidden" htmlFor="parameter-filter">
                Search parameters
              </label>
              <input
                id="parameter-filter"
                className="text-input parameter-search"
                disabled={!activeResult || Boolean(activeResult.error)}
                onChange={(event) => setParameterQuery(event.target.value)}
                placeholder="Search name or description…"
                value={parameterQuery}
              />
              <label className="visually-hidden" htmlFor="access-filter">
                Filter by access
              </label>
              <select
                id="access-filter"
                className="text-input access-select"
                disabled={!activeResult || Boolean(activeResult.error)}
                onChange={(event) =>
                  setAccessFilter(event.target.value as AccessFilter)
                }
                value={accessFilter}
              >
                <option value="all">All access</option>
                <option value="internal">Internal</option>
                <option value="external">External</option>
              </select>
            </div>
          </div>
          <div className="parameter-actions">
            <button
              className="secondary-button"
              disabled={selectedParameterBaseline === null}
              onClick={openComparison}
              type="button"
            >
              Compare
            </button>
            <button
              className="secondary-button"
              disabled={selectedParameterBaseline === null}
              onClick={openBulkEditor}
              type="button"
            >
              Apply value to templates
            </button>
          </div>

          {workspace.error && (
            <div className="notice error-notice">{workspace.error}</div>
          )}
          {workspace.recoveryBatches.map((batch) => (
            <div
              className="notice warning-notice recovery-notice"
              key={batch.batchId}
              role="alert"
            >
              <div>
                <strong>Interrupted save needs attention</strong>
                {batch.files.length > 0 ? (
                  <p>
                    {batch.files
                      .map((file) => `${file.fileName} · ${file.status}`)
                      .join('; ')}
                  </p>
                ) : (
                  <p>The recovery journal could not be validated.</p>
                )}
                {batch.reason && <p>{batch.reason}</p>}
                <p>
                  Recovery restores only files that still match the exact
                  prepared output. Any other disk content is left untouched.
                </p>
              </div>
              <button
                className="secondary-button"
                disabled={
                  !batch.canRecover ||
                  hasPendingWork ||
                  recoveringBatchId !== null
                }
                onClick={() => void recoverInterruptedSave(batch.batchId)}
                type="button"
              >
                {recoveringBatchId === batch.batchId
                  ? 'Recovering…'
                  : 'Restore original backups'}
              </button>
            </div>
          ))}
          {actionError && (
            <div className="notice error-notice">{actionError}</div>
          )}
          {actionNotice && (
            <div className="notice success-notice" role="status">
              <span>{actionNotice}</span>
              <button
                aria-label="Dismiss save confirmation"
                className="notice-dismiss"
                onClick={() => setActionNotice(null)}
                type="button"
              >
                ×
              </button>
            </div>
          )}
          {isInitializing && (
            <div className="empty-state">Restoring workspace preferences…</div>
          )}
          {!isInitializing && !workspace.folderPath && (
            <div className="empty-state">
              <span className="empty-icon" aria-hidden="true">
                ⌂
              </span>
              <h3>Choose a template folder</h3>
              <p>
                Template files are listed first. Declarations are read only
                after you press Read parameters.
              </p>
              <button
                className="primary-button"
                onClick={() => void chooseFolder()}
                type="button"
              >
                Choose folder
              </button>
            </div>
          )}
          {activeResult?.error && (
            <div className="notice error-notice">
              <strong>Could not read {activeResult.fileName}</strong>
              <p>{activeResult.error}</p>
              <p>Other templates in the batch remain available.</p>
            </div>
          )}
          {!isInitializing && workspace.folderPath && !activeResult && (
            <div className="empty-state compact-empty">
              <h3>
                {activeFile ? 'Parameters not read yet' : 'No active template'}
              </h3>
              <p>
                {activeFile
                  ? 'Select templates for reading or use the default visible-file scope, then press Read parameters.'
                  : 'Choose a template in the explorer. Focusing it does not add it to the read selection.'}
              </p>
            </div>
          )}
          {activeResult && !activeResult.error && (
            <>
              <div className="result-summary">
                <span>
                  {visibleDeclarations.length} matched of {declarations.length}{' '}
                  parameters
                </span>
                <span>
                  {draftsForFile(drafts, activeFileId ?? '')} staged field edits
                  · Only Review Save writes files
                </span>
              </div>
              {declarations.length === 0 ? (
                <div className="empty-state compact-empty">
                  <h3>No declarations found</h3>
                </div>
              ) : visibleDeclarations.length === 0 ? (
                <div className="empty-state compact-empty">
                  <h3>No matching parameters</h3>
                </div>
              ) : (
                <div className="parameter-table-wrap">
                  <table className="parameter-table">
                    <thead>
                      <tr>
                        <th>Name</th>
                        <th>Access</th>
                        <th>Default value</th>
                        <th>Description</th>
                      </tr>
                    </thead>
                    <tbody>
                      {visibleDeclarations.map((declaration) => (
                        <ParameterRow
                          declaration={declaration}
                          isModified={
                            activeFileId !== null &&
                            (hasDraftsForParameter(
                              drafts,
                              activeFileId,
                              declaration.id,
                            ) ||
                              editorForms.has(
                                editorFormKey(activeFileId, declaration.id),
                              ))
                          }
                          isSelected={selectedParameterId === declaration.id}
                          key={declaration.id}
                          onSelect={() =>
                            setSelectedParameterId(declaration.id)
                          }
                        />
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </>
          )}
        </main>

        <div
          aria-label="Resize parameter and details panels"
          aria-orientation="vertical"
          aria-valuemax={520}
          aria-valuemin={340}
          aria-valuenow={detailPanelWidth}
          className="panel-resizer"
          onKeyDown={resizeDetailsPanelByKeyboard}
          onPointerDown={resizeDetailsPanel}
          role="separator"
          tabIndex={0}
        />
        <aside className="detail-panel" aria-label="Parameter details">
          <p className="eyebrow">DETAILS</p>
          {selectedParameter && selectedEditorValues ? (
            <ParameterDetails
              canApply={canApplyEdits}
              declaration={selectedParameter}
              hasUnappliedForm={
                selectedEditorKey !== null && editorForms.has(selectedEditorKey)
              }
              onApply={applySelectedEdits}
              onReset={resetSelectedForm}
              onValueChange={updateEditorValue}
              values={selectedEditorValues}
            />
          ) : (
            <div className="detail-empty">
              <h3>No parameter selected</h3>
              <p>Select a row to inspect its read-only declaration fields.</p>
            </div>
          )}
          <div className="detail-card safety-card">
            <h3>Review-gated saving</h3>
            <p>
              Apply edits stages drafts in memory. Only Review Save writes; each
              file is fingerprint-checked, backed up, replaced, and verified.
              Multi-file saving is not one global atomic operation.
            </p>
          </div>
        </aside>
      </div>

      {pendingNavigation !== null && (
        <div
          aria-labelledby="pending-navigation-title"
          aria-modal="true"
          className="tool-overlay navigation-overlay"
          role="dialog"
        >
          <section className="tool-panel dirty-work-panel">
            <header className="tool-heading">
              <div>
                <p className="eyebrow">UNSAVED WORK</p>
                <h2 id="pending-navigation-title">
                  {pendingNavigation === 'close'
                    ? 'Close with unsaved work?'
                    : pendingNavigation === 'folder'
                      ? 'Change folders with unsaved work?'
                      : 'Re-read with unsaved work?'}
                </h2>
                <p>
                  {drafts.size} staged field changes and {editorForms.size}{' '}
                  unapplied editor forms. Review and save, explicitly discard,
                  or cancel and keep editing.
                </p>
                {(pendingNavigation === 'folder' ||
                  pendingNavigation === 'read') && (
                  <p>
                    Saving for review cancels this navigation. After returning
                    to the editor, choose the action again.
                  </p>
                )}
              </div>
            </header>
            {actionError && (
              <div className="notice error-notice" role="alert">
                {actionError}
              </div>
            )}
            <div className="dirty-work-actions">
              {editorForms.size > 0 && (
                <button
                  className="secondary-button"
                  onClick={applyEditorFormsForNavigation}
                  type="button"
                >
                  Apply editor forms
                </button>
              )}
              {(drafts.size > 0 || isReviewOpen) && (
                <button
                  className="primary-button"
                  disabled={editorForms.size > 0}
                  onClick={reviewPendingNavigation}
                  type="button"
                >
                  {isReviewOpen ? 'Continue review' : 'Save for review'}
                </button>
              )}
              <button
                className="destructive-button"
                onClick={discardAndContinueNavigation}
                type="button"
              >
                {pendingNavigation === 'close'
                  ? 'Discard and close'
                  : pendingNavigation === 'folder'
                    ? 'Discard and change folder'
                    : 'Discard and re-read'}
              </button>
              <button
                className="secondary-button"
                onClick={cancelPendingNavigation}
                type="button"
              >
                Keep editing
              </button>
            </div>
          </section>
        </div>
      )}

      {toolMode !== null && (
        <div
          aria-labelledby="workspace-tool-title"
          aria-modal="true"
          className="tool-overlay"
          role="dialog"
        >
          {toolMode === 'compare' ? (
            <section className="tool-panel compare-panel">
              <header className="tool-heading">
                <div>
                  <p className="eyebrow">COMPARE</p>
                  <h2 id="workspace-tool-title">
                    Compare {selectedParameter?.name ?? 'parameter'}
                  </h2>
                  <p>
                    Matching uses the exact case-sensitive parameter name and
                    working values, including staged drafts.
                  </p>
                </div>
                <button
                  className="secondary-button"
                  onClick={() => setToolMode(null)}
                  type="button"
                >
                  Close
                </button>
              </header>
              {actionError && (
                <div className="notice error-notice" role="alert">
                  {actionError}
                </div>
              )}
              <div className="compare-layout">
                <aside className="compare-targets">
                  <h3>Loaded templates</h3>
                  <p>
                    No comparison selection means all loaded templates. This is
                    separate from the selection used by Read parameters.
                  </p>
                  <button
                    className="text-button"
                    onClick={compareAllLoaded}
                    type="button"
                  >
                    Compare all loaded
                  </button>
                  <div className="compare-target-list">
                    {loadedTemplates.map((template) => (
                      <label className="compare-target" key={template.fileId}>
                        <input
                          aria-label={`Compare ${template.fileName}`}
                          checked={
                            template.fileId === activeFileId ||
                            comparisonSelection.size === 0 ||
                            comparisonSelection.has(template.fileId)
                          }
                          disabled={template.fileId === activeFileId}
                          onChange={() =>
                            toggleComparisonTarget(template.fileId)
                          }
                          type="checkbox"
                        />
                        <span>{template.fileName}</span>
                        {template.fileId === activeFileId && (
                          <span className="scope-badge">Active</span>
                        )}
                      </label>
                    ))}
                  </div>
                  {loadedTemplates.length < workspace.files.length && (
                    <p className="field-note">
                      {workspace.files.length - loadedTemplates.length} folder
                      template(s) are not loaded. Close this view and use Read
                      parameters to include them.
                    </p>
                  )}
                </aside>
                <div className="comparison-results">
                  {comparisonRows.map((row) => (
                    <article className="comparison-row" key={row.fileId}>
                      <header>
                        <strong>{row.fileName}</strong>
                        <span className={`comparison-status ${row.status}`}>
                          {row.status === 'active'
                            ? 'Active template'
                            : row.status === 'different'
                              ? 'Differences'
                              : row.status === 'equal'
                                ? 'Equal'
                                : row.status === 'missing'
                                  ? 'Missing declaration'
                                  : 'Ambiguous'}
                        </span>
                        {row.hasWorkingDraft && (
                          <small className="draft-chip">Working draft</small>
                        )}
                      </header>
                      {row.reason && <p className="field-note">{row.reason}</p>}
                      {row.status === 'different' && (
                        <div className="comparison-differences">
                          {row.differences.map((difference) => (
                            <div
                              className="comparison-difference"
                              key={difference.field}
                            >
                              <strong>{fieldLabel(difference.field)}</strong>
                              <div>
                                <span>
                                  Active
                                  {difference.activeIsDraft && (
                                    <small className="draft-chip">Draft</small>
                                  )}
                                </span>
                                <pre>
                                  {displayValue(difference.activeValue)}
                                </pre>
                              </div>
                              <div>
                                <span>
                                  This template
                                  {difference.targetIsDraft && (
                                    <small className="draft-chip">Draft</small>
                                  )}
                                </span>
                                <pre>
                                  {displayValue(difference.targetValue)}
                                </pre>
                              </div>
                            </div>
                          ))}
                        </div>
                      )}
                    </article>
                  ))}
                  {comparisonRows.length === 0 && (
                    <div className="empty-state compact-empty">
                      <h3>No loaded templates to compare</h3>
                    </div>
                  )}
                </div>
              </div>
            </section>
          ) : (
            <section className="tool-panel bulk-panel">
              <header className="tool-heading">
                <div>
                  <p className="eyebrow">BULK VALUE STAGING</p>
                  <h2 id="workspace-tool-title">
                    Apply value to {selectedParameter?.name ?? 'parameter'}
                  </h2>
                  <p>
                    Only the default value is staged. Access, descriptions, and
                    other parameters remain unchanged. No targets start checked.
                  </p>
                </div>
                <button
                  className="secondary-button"
                  onClick={() => setToolMode(null)}
                  type="button"
                >
                  Close
                </button>
              </header>
              {actionError && (
                <div className="notice error-notice" role="alert">
                  {actionError}
                </div>
              )}
              <label className="bulk-value-label" htmlFor="bulk-value">
                Proposed default value
              </label>
              <textarea
                id="bulk-value"
                className="text-input editor-textarea bulk-value-input"
                onChange={(event) => setBulkValue(event.target.value)}
                rows={3}
                spellCheck={false}
                value={bulkValue}
              />
              <div className="bulk-controls">
                <span>
                  {bulkSelection.size} selected · {eligibleBulkCount} eligible
                </span>
                <button
                  className="text-button"
                  disabled={eligibleBulkCount === 0}
                  onClick={selectAllBulkTargets}
                  type="button"
                >
                  Select all eligible
                </button>
                <button
                  className="text-button"
                  disabled={bulkSelection.size === 0}
                  onClick={() => setBulkSelection(new Set())}
                  type="button"
                >
                  Clear targets
                </button>
              </div>
              <div className="bulk-candidate-list">
                {bulkCandidates.map((candidate) => (
                  <label
                    className={`bulk-candidate ${candidate.status}`}
                    key={candidate.fileId}
                  >
                    <input
                      aria-label={`Apply bulk value to ${candidate.fileName}`}
                      checked={bulkSelection.has(candidate.fileId)}
                      disabled={candidate.status !== 'eligible'}
                      onChange={() => toggleBulkTarget(candidate.fileId)}
                      type="checkbox"
                    />
                    <span className="bulk-candidate-main">
                      <strong>
                        {candidate.fileName}
                        {candidate.fileId === activeFileId && (
                          <small className="scope-badge">
                            Current template
                          </small>
                        )}
                      </strong>
                      {candidate.status === 'eligible' ? (
                        <span className="bulk-value-preview">
                          Current: {displayValue(candidate.currentValue)}
                          {candidate.currentValueIsDraft && ' · staged draft'}
                          <br />
                          Proposed: {displayValue(bulkValue)}
                        </span>
                      ) : (
                        <span className="field-note">
                          {candidate.status}: {candidate.reason}
                        </span>
                      )}
                    </span>
                  </label>
                ))}
                {bulkCandidates.length === 0 && (
                  <div className="empty-state compact-empty">
                    <h3>No templates are loaded</h3>
                    <p>Read templates before staging a bulk value.</p>
                  </div>
                )}
              </div>
              <div className="bulk-actions">
                <span>
                  Selected targets receive only the proposed default value.
                </span>
                <div>
                  <button
                    className="secondary-button"
                    onClick={() => setToolMode(null)}
                    type="button"
                  >
                    Cancel
                  </button>
                  <button
                    className="primary-button"
                    disabled={bulkSelection.size === 0}
                    onClick={applyBulkTargets}
                    type="button"
                  >
                    Apply changes ({bulkSelection.size})
                  </button>
                </div>
              </div>
            </section>
          )}
        </div>
      )}

      {isReviewOpen && (
        <div
          aria-labelledby="review-title"
          aria-modal="true"
          className="review-overlay"
          role="dialog"
        >
          <main className="review-panel">
            <div className="review-heading">
              <div>
                <p className="eyebrow">SAVE REVIEW</p>
                <h2 id="review-title">Review pending changes</h2>
                <p>
                  Only checked fields will be written. Unchecked fields revert
                  to baseline after a successful save. Back keeps every draft.
                </p>
              </div>
              <div className="review-counts">
                <strong>{includedReviewCount}</strong> included
                <span>{drafts.size - includedReviewCount} will revert</span>
              </div>
            </div>
            {actionError && (
              <div className="notice error-notice" role="alert">
                {actionError}
              </div>
            )}
            <div className="review-list">
              {Array.from(reviewGroups.entries()).map(([fileId, group]) => (
                <section className="review-file-group" key={fileId}>
                  <h3>{group.fileName}</h3>
                  {group.items.map(({ key, draft, included }) => (
                    <article className="review-change" key={key}>
                      <label className="review-toggle">
                        <input
                          checked={included}
                          disabled={isSaving}
                          onChange={() => toggleReviewItem(key)}
                          type="checkbox"
                        />
                        <span>Deliver this field change</span>
                      </label>
                      <div className="review-change-title">
                        <strong>{draft.parameterName}</strong>
                        <span>{fieldLabel(draft.field)}</span>
                      </div>
                      <div className="review-values">
                        <div>
                          <span className="review-value-label">Current</span>
                          <pre>{displayValue(draft.baseline)}</pre>
                        </div>
                        <div>
                          <span className="review-value-label">Proposed</span>
                          <pre>{displayValue(draft.proposed)}</pre>
                        </div>
                      </div>
                    </article>
                  ))}
                </section>
              ))}
            </div>
            <div className="review-actions">
              <span>
                {includedReviewCount} changes to deliver ·{' '}
                {drafts.size - includedReviewCount} excluded
              </span>
              <div>
                <button
                  className="secondary-button"
                  disabled={isSaving}
                  onClick={() => setIsReviewOpen(false)}
                  type="button"
                >
                  Back
                </button>
                <button
                  className="primary-button"
                  disabled={isSaving || includedReviewCount === 0}
                  onClick={() => void saveReviewedChanges()}
                  type="button"
                >
                  {isSaving ? 'Saving…' : `Save ${includedReviewCount} changes`}
                </button>
              </div>
            </div>
          </main>
        </div>
      )}

      <footer className="footer">
        <span>
          {workspace.folderPath
            ? `${workspace.files.length} templates in folder`
            : 'No folder selected'}
        </span>
        <span>
          {isReading && progress
            ? `Reading ${progress.completed}/${progress.total}: ${progress.fileName}`
            : 'Offline · Review Save writes selected changes'}
        </span>
        <span>
          <button
            aria-haspopup="dialog"
            aria-label={`About ${packageInfo.productName}, version ${packageInfo.version}`}
            className="footer-about-button"
            onClick={() => setIsAboutOpen(true)}
            ref={aboutTriggerRef}
            type="button"
          >
            Version {packageInfo.version}
          </button>
        </span>
      </footer>
      {isAboutOpen && (
        <div
          className="about-overlay"
          onClick={(event) => {
            if (event.target === event.currentTarget) setIsAboutOpen(false);
          }}
        >
          <section
            aria-labelledby="about-title"
            aria-modal="true"
            className="about-dialog"
            role="dialog"
          >
            <header className="about-heading">
              <div>
                <p className="eyebrow">ABOUT THIS APPLICATION</p>
                <h2 id="about-title">{packageInfo.productName}</h2>
              </div>
              <button
                aria-label="Close About"
                className="about-icon-button"
                onClick={() => setIsAboutOpen(false)}
                ref={aboutCloseRef}
                type="button"
              >
                ×
              </button>
            </header>
            <div className="about-content">
              <section className="about-summary">
                <p>{packageInfo.description}</p>
                <dl>
                  <div>
                    <dt>Version</dt>
                    <dd>{packageInfo.version}</dd>
                  </div>
                  <div>
                    <dt>Author / maintainer</dt>
                    <dd>{packageInfo.author}</dd>
                  </div>
                  <div>
                    <dt>Project</dt>
                    <dd>{packageInfo.homepage}</dd>
                  </div>
                  <div>
                    <dt>Operation</dt>
                    <dd>Offline; templates stay on this computer</dd>
                  </div>
                </dl>
              </section>
              <section className="about-section">
                <h3>Application license</h3>
                <p>
                  GNU General Public License v3.0 only{' '}
                  <code>(GPL-3.0-only)</code>
                </p>
                <p className="about-note">
                  Provided without warranty. Redistribution is permitted under
                  the license terms. The complete license text is included as
                  <code> LICENSE</code> in the application package and source
                  distribution.
                </p>
              </section>
              <section className="about-section">
                <h3>Bundled components and licenses</h3>
                <div className="about-library-list" role="table">
                  <div className="about-library-header" role="row">
                    <span role="columnheader">Component</span>
                    <span role="columnheader">Version</span>
                    <span role="columnheader">License</span>
                  </div>
                  {runtimeLibraries.map((library) => (
                    <div
                      className="about-library-row"
                      key={library.name}
                      role="row"
                    >
                      <span role="cell">{library.name}</span>
                      <span role="cell">{library.version}</span>
                      <span role="cell">{library.license}</span>
                    </div>
                  ))}
                </div>
                <p className="about-note">
                  Full third-party license texts and attributions are in{' '}
                  <code>resources/THIRD_PARTY_NOTICES.txt</code>. Electron’s
                  Chromium notices are in <code>LICENSES.chromium.html</code>.
                </p>
              </section>
            </div>
            <footer className="about-actions">
              <span>Thank you for using {packageInfo.productName}.</span>
              <button
                className="primary-button"
                onClick={() => setIsAboutOpen(false)}
                type="button"
              >
                Close
              </button>
            </footer>
          </section>
        </div>
      )}
    </div>
  );
}

function ParameterRow({
  declaration,
  isModified,
  isSelected,
  onSelect,
}: {
  declaration: ParameterDeclaration;
  isModified: boolean;
  isSelected: boolean;
  onSelect: () => void;
}) {
  const rowClass = [
    'parameter-row',
    isSelected ? 'selected' : '',
    isModified ? 'modified' : '',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <tr className={rowClass} onClick={onSelect}>
      <td>
        <button className="parameter-name" onClick={onSelect} type="button">
          {declaration.name ?? 'Unnamed declaration'}
        </button>
        {isModified && (
          <span className="modified-badge" title="Unsaved parameter changes">
            Unsaved
          </span>
        )}
        {declaration.readOnlyReasons.length > 0 && (
          <span className="readonly-badge">Read-only shape</span>
        )}
      </td>
      <td>
        <span className={`access-badge ${declaration.access ?? 'unknown'}`}>
          {declaration.access ?? 'Unknown'}
        </span>
      </td>
      <td>
        <span
          className="value-preview"
          title={displayValue(declaration.defaultValue)}
        >
          {displayValue(declaration.defaultValue)}
        </span>
      </td>
      <td>
        <span
          className="description-preview"
          title={displayValue(declaration.description)}
        >
          {displayValue(declaration.description)}
        </span>
      </td>
    </tr>
  );
}

function ParameterDetails({
  declaration,
  values,
  canApply,
  hasUnappliedForm,
  onApply,
  onReset,
  onValueChange,
}: {
  declaration: ParameterDeclaration;
  values: ParameterEditorValues;
  canApply: boolean;
  hasUnappliedForm: boolean;
  onApply: () => void;
  onReset: () => void;
  onValueChange: (field: EditableField, value: string) => void;
}) {
  const editable = (field: EditableField): boolean =>
    declaration.readOnlyReasons.length === 0 &&
    declaration.editableFields?.[field] === true;

  return (
    <div className="parameter-details">
      <div className="detail-card">
        <h3>Parameter name</h3>
        <p className="detail-value">{declaration.name ?? 'Missing name'}</p>
        <span className="field-note">Read-only</span>
      </div>
      <div className="detail-card">
        <h3>Default value</h3>
        <textarea
          aria-label="Default value"
          className="text-input editor-textarea preserved-text"
          disabled={!editable('defaultValue')}
          onChange={(event) =>
            onValueChange('defaultValue', event.target.value)
          }
          rows={5}
          spellCheck={false}
          value={values.defaultValue}
        />
        {!editable('defaultValue') && (
          <span className="field-note">Read-only: unsupported field shape</span>
        )}
      </div>
      <div className="detail-card">
        <h3>Access</h3>
        <select
          aria-label="Access classification"
          className="text-input editor-select"
          disabled={!editable('access')}
          onChange={(event) => onValueChange('access', event.target.value)}
          value={values.access}
        >
          <option value="internal">Internal</option>
          <option value="external">External</option>
        </select>
        {!editable('access') && (
          <span className="field-note">Read-only: unsupported field shape</span>
        )}
      </div>
      <div className="detail-card">
        <h3>Description</h3>
        <textarea
          aria-label="Description"
          className="text-input editor-textarea preserved-text"
          disabled={!editable('description')}
          onChange={(event) => onValueChange('description', event.target.value)}
          rows={4}
          spellCheck={false}
          value={values.description}
        />
        {!editable('description') && (
          <span className="field-note">Read-only: unsupported field shape</span>
        )}
      </div>
      <div className="detail-card">
        <h3>XML type property</h3>
        <p className="detail-value">{declaration.xmlType ?? 'Missing'}</p>
        <span className="field-note">
          Separate from Internal / External access
        </span>
      </div>
      {declaration.readOnlyReasons.length > 0 && (
        <div className="notice warning-notice">
          <strong>Unsupported declaration shape</strong>
          <ul>
            {declaration.readOnlyReasons.map((reason) => (
              <li key={reason}>{reason}</li>
            ))}
          </ul>
        </div>
      )}
      <div className="editor-actions">
        <button
          className="primary-button"
          disabled={!canApply}
          onClick={onApply}
          type="button"
        >
          Apply edits
        </button>
        {hasUnappliedForm && (
          <button className="text-button" onClick={onReset} type="button">
            Reset unapplied form
          </button>
        )}
        <p className="field-note">
          Apply stages changes in memory only. It does not write the DTA file.
        </p>
      </div>
    </div>
  );
}
