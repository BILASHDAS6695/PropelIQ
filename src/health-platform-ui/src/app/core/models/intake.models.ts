export interface IntakeChatRequest {
  sessionId?: string | null;
  message: string;
  patientId?: string | null;
  appointmentId?: string | null;
}

export interface IntakeChatResponse {
  sessionId: string;
  reply: string;
  isComplete: boolean;
  collected: Record<string, string | null>;
  fallbackRequired: boolean;
}

// --- Structured Form ---

export interface IntakeFormData {
  chiefComplaint: string;
  symptoms: string[];
  duration: string;
  severity: number; // 1–10
  medications: string[];
  allergies: string[];
  medicalHistory: string;
}

export interface IntakeFormDraft {
  data: IntakeFormData;
  savedAt: number;
  appointmentId?: string;
}

// --- Backend Integration ---

export type IntakeMode = 'AiConversational' | 'ManualForm';
export type IntakeStatus = 'Draft' | 'Completed' | 'ReviewedByProvider' | 'Orphaned';

export interface IntakeSummaryDto {
  id: string;
  appointmentId: string;
  patientId: string;
  mode: IntakeMode;
  status: IntakeStatus;
  data: IntakeFormData | null;
  completedAt: string | null;
  reviewedAt: string | null;
  reviewedByProviderId: string | null;
}

export interface IntakeSubmitRequest {
  appointmentId: string;
  mode: IntakeMode;
  data: IntakeFormData;
}
