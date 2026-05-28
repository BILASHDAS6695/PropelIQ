import { Routes } from '@angular/router';

export const CLINICAL_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./documents/documents.component').then((m) => m.DocumentsComponent),
  },
  {
    path: 'documents/:documentId',
    loadComponent: () =>
      import('./documents/document-detail.component').then((m) => m.DocumentDetailComponent),
  },
];
