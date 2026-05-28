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
