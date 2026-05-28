export interface DocumentUploadResultDto {
  documentId: string;
  fileName: string;
  mimeType: string;
  fileSizeBytes: number;
  uploadedAt: string; // ISO-8601
  processingStatus: DocumentProcessingStatus;
}

export interface DocumentSummaryDto {
  documentId: string;
  fileName: string;
  mimeType: string;
  fileSizeBytes: number;
  uploadedAt: string; // ISO-8601
  processingStatus: DocumentProcessingStatus;
}

export type DocumentProcessingStatus =
  | 'Uploaded'
  | 'Processing'
  | 'Processed'
  | 'Verified'
  | 'Failed';
