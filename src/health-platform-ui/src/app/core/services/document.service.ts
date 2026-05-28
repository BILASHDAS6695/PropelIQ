import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type {
  DocumentSummaryDto,
  DocumentUploadResultDto,
  DocumentOcrResultDto,
} from '../models/document.models';

@Injectable({ providedIn: 'root' })
export class DocumentService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  /**
   * GET /api/patients/{patientId}/documents
   * Returns all documents for the patient, ordered by upload date descending.
   * patientId is the patient's User.Id (JWT sub).
   */
  getDocuments(patientId: string): Observable<DocumentSummaryDto[]> {
    return this.http.get<DocumentSummaryDto[]>(`${this.base}/patients/${patientId}/documents`);
  }

  /**
   * POST /api/patients/{patientId}/documents
   * Uploads a file as a multipart/form-data request.
   * patientId is the patient's User.Id (JWT sub).
   */
  uploadDocument(patientId: string, file: File): Observable<DocumentUploadResultDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<DocumentUploadResultDto>(
      `${this.base}/patients/${patientId}/documents`,
      form,
    );
  }

  /**
   * GET /api/patients/{patientId}/documents/{documentId}
   * Returns the OCR extraction result for a specific document.
   * patientId is the patient's User.Id (JWT sub).
   */
  getDocumentOcrResult(patientId: string, documentId: string): Observable<DocumentOcrResultDto> {
    return this.http.get<DocumentOcrResultDto>(
      `${this.base}/patients/${patientId}/documents/${documentId}`,
    );
  }
}
