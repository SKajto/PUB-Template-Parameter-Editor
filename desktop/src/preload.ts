import { contextBridge, ipcRenderer, type IpcRendererEvent } from 'electron';
import type {
  ReadProgress,
  InterruptedSaveRecoveryResult,
  ReviewedFieldChange,
  ReviewedSaveResult,
  TemplateEditorApi,
} from './shared/types';

const templateEditor: TemplateEditorApi = {
  getInitialState: () => ipcRenderer.invoke('workspace:get-initial-state'),
  chooseFolder: () => ipcRenderer.invoke('workspace:choose-folder'),
  setTheme: (theme) => ipcRenderer.invoke('workspace:set-theme', theme),
  readTemplates: (fileIds) =>
    ipcRenderer.invoke('workspace:read-templates', fileIds),
  saveReviewed: (changes: ReviewedFieldChange[]): Promise<ReviewedSaveResult> =>
    ipcRenderer.invoke('workspace:save-reviewed', changes),
  recoverInterruptedSave: (
    batchId: string,
  ): Promise<InterruptedSaveRecoveryResult> =>
    ipcRenderer.invoke('workspace:recover-interrupted-save', batchId),
  reportPendingWork: (hasPendingWork) =>
    ipcRenderer.send('workspace:report-pending-work', hasPendingWork),
  confirmClose: () => ipcRenderer.send('workspace:confirm-close'),
  onCloseRequested: (listener) => {
    const handler = (): void => listener();
    ipcRenderer.on('workspace:close-requested', handler);
    return () =>
      ipcRenderer.removeListener('workspace:close-requested', handler);
  },
  onReadProgress: (listener) => {
    const handler = (
      _event: IpcRendererEvent,
      progress: ReadProgress,
    ): void => {
      listener(progress);
    };
    ipcRenderer.on('workspace:read-progress', handler);
    return () => ipcRenderer.removeListener('workspace:read-progress', handler);
  },
};

contextBridge.exposeInMainWorld('templateEditor', templateEditor);
