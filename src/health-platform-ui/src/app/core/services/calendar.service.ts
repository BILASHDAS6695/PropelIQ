import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CalendarAppointmentDto } from '../models/calendar.models';

@Injectable({ providedIn: 'root' })
export class CalendarService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  getAppointments(from: Date, to: Date, providerId?: string): Observable<CalendarAppointmentDto[]> {
    let params = new HttpParams().set('from', from.toISOString()).set('to', to.toISOString());
    if (providerId) params = params.set('providerId', providerId);
    return this.http.get<CalendarAppointmentDto[]>(`${this.base}/appointments/calendar`, {
      params,
    });
  }
}
