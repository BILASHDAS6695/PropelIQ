import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  IntakeChatRequest,
  IntakeChatResponse,
  IntakeSubmitRequest,
  IntakeSummaryDto,
} from '../models/intake.models';

@Injectable({ providedIn: 'root' })
export class IntakeService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/intake`;

  chat(request: IntakeChatRequest): Observable<IntakeChatResponse> {
    return this.http.post<IntakeChatResponse>(`${this.base}/chat`, request);
  }

  saveDraft(req: IntakeSubmitRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/draft`, req);
  }

  submitIntake(req: IntakeSubmitRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/submit`, req);
  }

  getIntakeSummary(appointmentId: string): Observable<IntakeSummaryDto> {
    return this.http.get<IntakeSummaryDto>(`${this.base}/${appointmentId}`);
  }

  markReviewed(appointmentId: string): Observable<void> {
    return this.http.put<void>(`${this.base}/${appointmentId}/reviewed`, {});
  }
}
