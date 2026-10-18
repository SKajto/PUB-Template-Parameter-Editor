export type ThemePreference = 'system' | 'light' | 'dark';

export interface TemplateFile {
  id: string;
  name: string;
  size: number;
  modifiedAt: number;
  error: string | null;
}

export interface WorkspaceState {
  folderPath: string | null;
  files: TemplateFile[];
  theme: ThemePreference;
  error: string | null;
  recoveryBatches: InterruptedSaveBatchSummary[];
}

export type EditableField = 'access' | 'defaultValue' | 'description';

export type EditableFieldCapabilities = Record<EditableField, boolean>;

export interface ParameterDeclaration {
  id: string;
  name: string | null;
  description: string | null;
  access: string | null;
  defaultValue: string | null;
  xmlType: string | null;
  readOnlyReasons: string[];
  editableFields?: EditableFieldCapabilities;
}

export interface ReadTemplateResult {
  fileId: string;
  fileName: string;
  declarations: ParameterDeclaration[];
  error: string | null;
}

export interface ReadProgress {
  completed: number;
  total: number;
  fileName: string;
}

export interface ReviewedFieldChange {
  fileId: string;
  declarationId: string;
  field: EditableField;
  proposed: string;
  included: boolean;
}

export type SaveFileStatus =
  | 'not-written'
  | 'saved'
  | 'restored'
  | 'failed'
  | 'uncertain';

export interface SaveFileOutcome {
  fileId: string;
  fileName: string;
  status: SaveFileStatus;
  message: string;
}

export interface InterruptedSaveBatchSummary {
  batchId: string;
  startedAt: string;
  files: SaveFileOutcome[];
  canRecover: boolean;
  reason: string | null;
}

export interface InterruptedSaveRecoveryResult {
  ok: boolean;
  batchId: string;
  error: string | null;
  outcomes: SaveFileOutcome[];
  workspace: WorkspaceState;
}

export interface ReviewedSaveResult {
  ok: boolean;
  batchId: string;
  journalPath: string;
  error: string | null;
  outcomes: SaveFileOutcome[];
  updatedTemplates: ReadTemplateResult[];
}

export interface TemplateEditorApi {
  getInitialState(): Promise<WorkspaceState>;
  chooseFolder(): Promise<WorkspaceState>;
  setTheme(theme: ThemePreference): Promise<WorkspaceState>;
  readTemplates(fileIds: string[]): Promise<ReadTemplateResult[]>;
  saveReviewed(changes: ReviewedFieldChange[]): Promise<ReviewedSaveResult>;
  recoverInterruptedSave(
    batchId: string,
  ): Promise<InterruptedSaveRecoveryResult>;
  reportPendingWork(hasPendingWork: boolean): void;
  confirmClose(): void;
  onCloseRequested(listener: () => void): () => void;
  onReadProgress(listener: (progress: ReadProgress) => void): () => void;
}

declare global {
  interface Window {
    templateEditor: TemplateEditorApi;
  }
}
