import { app, BrowserWindow, ipcMain } from 'electron';
import {
  getSettingsPath,
  registerWorkspaceHandlers,
} from './main/workspace-service';

declare const MAIN_WINDOW_WEBPACK_ENTRY: string;
declare const MAIN_WINDOW_PRELOAD_WEBPACK_ENTRY: string;

const dirtyWindows = new Set<number>();
const approvedCloseWindows = new Set<number>();

const createWindow = (): BrowserWindow => {
  // Create the browser window.
  const mainWindow = new BrowserWindow({
    height: 860,
    minHeight: 640,
    minWidth: 980,
    title: 'PUB Template Parameter Editor',
    width: 1320,
    webPreferences: {
      preload: MAIN_WINDOW_PRELOAD_WEBPACK_ENTRY,
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });

  const contentsId = mainWindow.webContents.id;
  mainWindow.on('close', (event) => {
    if (
      !dirtyWindows.has(contentsId) ||
      approvedCloseWindows.delete(contentsId)
    )
      return;
    event.preventDefault();
    if (!mainWindow.webContents.isDestroyed()) {
      mainWindow.webContents.send('workspace:close-requested');
    }
  });
  mainWindow.on('closed', () => {
    dirtyWindows.delete(contentsId);
    approvedCloseWindows.delete(contentsId);
  });

  mainWindow.loadURL(MAIN_WINDOW_WEBPACK_ENTRY);
  return mainWindow;
};

// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
// Some APIs can only be used after this event occurs.
app.whenReady().then(() => {
  ipcMain.on('workspace:report-pending-work', (event, value: unknown) => {
    const window = BrowserWindow.fromWebContents(event.sender);
    if (!window || typeof value !== 'boolean') return;
    if (value) dirtyWindows.add(event.sender.id);
    else dirtyWindows.delete(event.sender.id);
  });
  ipcMain.on('workspace:confirm-close', (event) => {
    const window = BrowserWindow.fromWebContents(event.sender);
    if (!window) return;
    approvedCloseWindows.add(event.sender.id);
    window.close();
  });

  registerWorkspaceHandlers(getSettingsPath(app.getPath('userData')));
  createWindow();

  // On OS X it's common to re-create a window in the app when the
  // dock icon is clicked and there are no other windows open.
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

// Keep macOS applications available after the last window is closed.
app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});
