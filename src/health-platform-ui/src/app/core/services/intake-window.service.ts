import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface IntakeWindowDto {
  isOpen: boolean;
  reason: string | null;
}

@Injectable({ providedIn: 'root' })
export class IntakeWindowService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  check(appointmentId: string): Observable<IntakeWindowDto> {
    return this.http.get<IntakeWindowDto>(
      `${this.base}/appointments/${appointmentId}/intake-window`,
    );
  }
}
