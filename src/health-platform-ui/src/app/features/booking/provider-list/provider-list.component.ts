import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { SkeletonModule } from 'primeng/skeleton';
import { BookingStore } from '../booking.store';
import { ProviderSummaryDto } from '../../../core/models/booking.models';

const SPECIALTIES = [
  { label: 'All Specialties', value: null },
  { label: 'General Practice', value: 'General Practice' },
  { label: 'Cardiology', value: 'Cardiology' },
  { label: 'Dermatology', value: 'Dermatology' },
  { label: 'Pediatrics', value: 'Pediatrics' },
  { label: 'Orthopedics', value: 'Orthopedics' },
];

@Component({
  selector: 'app-provider-list',
  standalone: true,
  imports: [CommonModule, FormsModule, CardModule, ButtonModule, InputTextModule, SelectModule, SkeletonModule],
  template: `
    <div class="provider-list">
      <h2 class="text-xl font-semibold mb-3">Select a Provider</h2>

      <div class="grid mb-4">
        <div class="col-12 md:col-6 lg:col-4 mb-2">
          <p-select
            [(ngModel)]="selectedSpecialty"
            [options]="specialties"
            optionLabel="label"
            optionValue="value"
            placeholder="Filter by specialty"
            styleClass="w-full"
            (onChange)="applyFilters()"
          />
        </div>
        <div class="col-12 md:col-6 lg:col-4 mb-2">
          <input
            pInputText
            [(ngModel)]="nameFilter"
            placeholder="Search by name"
            class="w-full"
            (input)="applyFilters()"
          />
        </div>
      </div>

      @if (store.isLoading()) {
        <div class="grid">
          @for (i of skeletonItems; track i) {
            <div class="col-12 md:col-6 lg:col-4 mb-3">
              <p-card>
                <p-skeleton height="1.5rem" styleClass="mb-2" />
                <p-skeleton height="1rem" width="60%" />
              </p-card>
            </div>
          }
        </div>
      }

      @if (store.error() && !store.isLoading()) {
        <div class="text-center py-5">
          <p class="text-color-secondary mb-3">{{ store.error() }}</p>
          <p-button label="Retry" icon="pi pi-refresh" (onClick)="loadProviders()" />
        </div>
      }

      @if (!store.isLoading() && !store.error()) {
        @if (filteredProviders().length === 0) {
          <div class="text-center py-5 text-color-secondary">
            No providers found matching your criteria.
          </div>
        } @else {
          <div class="grid">
            @for (provider of filteredProviders(); track provider.providerId) {
              <div class="col-12 md:col-6 lg:col-4 mb-3">
                <p-card styleClass="h-full">
                  <div class="flex align-items-center gap-3 mb-2">
                    <div
                      class="flex align-items-center justify-content-center border-circle bg-primary-100 text-primary-700 font-bold"
                      style="width:3rem;height:3rem;flex-shrink:0"
                    >
                      {{ initials(provider.name) }}
                    </div>
                    <div>
                      <div class="font-semibold text-lg">{{ provider.name }}</div>
                      <div class="text-color-secondary text-sm">
                        {{ provider.specialty ?? 'General Practice' }}
                      </div>
                    </div>
                  </div>
                  <p-button
                    label="Select"
                    styleClass="w-full mt-2"
                    size="small"
                    (onClick)="select(provider)"
                  />
                </p-card>
              </div>
            }
          </div>
        }
      }
    </div>
  `,
})
export class ProviderListComponent implements OnInit {
  readonly store = inject(BookingStore);

  selectedSpecialty: string | null = null;
  nameFilter = '';
  specialties = SPECIALTIES;
  skeletonItems = [1, 2, 3, 4, 5, 6];

  filteredProviders = signal<ProviderSummaryDto[]>([]);

  ngOnInit(): void {
    this.loadProviders();
  }

  loadProviders(): void {
    this.store
      .loadProviders(this.selectedSpecialty ?? undefined, this.nameFilter || undefined)
      .then(() => this.filteredProviders.set(this.store.providers()));
  }

  applyFilters(): void {
    const name = this.nameFilter.trim().toLowerCase();
    if (!this.selectedSpecialty && !name) {
      // No API filters active — use client-side filtering on cached list
      this.filteredProviders.set(this.store.providers());
    } else if (!this.selectedSpecialty && name) {
      // Client-side name filter only (backend doesn't support name param yet)
      this.filteredProviders.set(
        this.store.providers().filter((p) => p.name.toLowerCase().includes(name)),
      );
    } else {
      // Specialty filter → hits API, then apply name filter locally
      this.store
        .loadProviders(this.selectedSpecialty ?? undefined)
        .then(() =>
          this.filteredProviders.set(
            this.store
              .providers()
              .filter((p) => !name || p.name.toLowerCase().includes(name)),
          ),
        );
    }
  }

  select(provider: ProviderSummaryDto): void {
    this.store.selectProvider(provider);
  }

  initials(name: string): string {
    return name
      .split(' ')
      .slice(0, 2)
      .map((w) => w[0]?.toUpperCase() ?? '')
      .join('');
  }
}
