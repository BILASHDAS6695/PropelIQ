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
