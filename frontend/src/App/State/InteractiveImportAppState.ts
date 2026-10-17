import AppSectionState from 'App/State/AppSectionState';
import ImportMode from 'InteractiveImport/ImportMode';
import InteractiveImport from 'InteractiveImport/InteractiveImport';

interface FavoriteFolder {
  folder: string;
}

interface RecentFolder {
  folder: string;
  lastUsed: string;
}

interface InteractiveImportAppState extends AppSectionState<InteractiveImport> {
  originalItems: InteractiveImport[];
  importMode: ImportMode;
  favoriteFolders: FavoriteFolder[];
  recentFolders: RecentFolder[];
  // Set while selected rows are being read again a batch at a time.
  checkProgress: { done: number; total: number } | null;
}

export default InteractiveImportAppState;
