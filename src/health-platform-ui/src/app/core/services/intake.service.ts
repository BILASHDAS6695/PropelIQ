import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { IntakeChatRequest, IntakeChatResponse } from '../models/intake.models';

@Injectable({ providedIn: 'root' })
export class IntakeService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  chat(request: IntakeChatRequest): Observable<IntakeChatResponse> {
    return this.http.post<IntakeChatResponse>(`${this.base}/intake/chat`, request);
  }
}
