# Architecture Design Specification

## Document Information

| Field | Value |
|-------|-------|
| **Project** | Unified Patient Access & Clinical Intelligence Platform |
| **Version** | 1.0 |
| **Status** | Draft |
| **Source** | spec.md, BRD.md |
| **Phase** | Phase 1 |

---

## 1. Architecture Overview

### 1.1 System Context

The platform is a multi-tier web application serving three user roles (Patient, Staff, Admin) through an Angular SPA frontend backed by a .NET Web API, PostgreSQL database, and an AI-powered clinical intelligence processing pipeline. The system integrates with external calendar services, notification channels (SMS/Email), and processes uploaded clinical documents to produce a unified patient view.

### 1.2 Architecture Style

The system adopts a **Modular Monolith** architecture with clear bounded contexts, enabling future decomposition into microservices without premature complexity. Internally, the backend uses **Clean Architecture** (Domain → Application → Infrastructure → Presentation) with **CQRS** for read-heavy clinical data operations.

### 1.3 High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                        CLIENT LAYER                                  │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │           Angular SPA (Netlify/Vercel)                       │   │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐   │   │
│  │  │ Booking  │ │  Intake  │ │  Clinical│ │    Admin     │   │   │
│  │  │  Module  │ │  Module  │ │  Module  │ │    Module    │   │   │
│  │  └──────────┘ └──────────┘ └──────────┘ └──────────────┘   │   │
│  └──────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                              │ HTTPS/REST
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                         API LAYER                                    │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │              .NET 8 Web API (IIS / Windows Service)          │   │
│  │  ┌───────────┐ ┌───────────┐ ┌────────────┐ ┌───────────┐  │   │
│  │  │Controllers│ │ MediatR   │ │  Domain    │ │Background │  │   │
│  │  │ + Filters │ │ Handlers  │ │  Services  │ │   Jobs    │  │   │
│  │  └───────────┘ └───────────┘ └────────────┘ └───────────┘  │   │
│  └──────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
┌──────────────────┐ ┌──────────────┐ ┌──────────────────┐
│   PostgreSQL 16  │ │ Upstash Redis│ │  AI Processing   │
│  (Primary Store) │ │   (Cache)    │ │    Pipeline      │
└──────────────────┘ └──────────────┘ └──────────────────┘
                                              │
                              ┌───────────────┼───────────────┐
                              ▼               ▼               ▼
                     ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
                     │  PDF Parser  │ │  NLP/NER     │ │ Medical Code │
                     │  (Extraction)│ │  (Entities)  │ │   Mapper     │
                     └──────────────┘ └──────────────┘ └──────────────┘
```

### 1.4 Architectural Principles

| # | Principle | Rationale |
|---|-----------|-----------|
| AP-1 | Requirements-first, technology-neutral decisions | Technology choices serve requirements, not preferences |
| AP-2 | Security by design (HIPAA-first) | Healthcare data demands zero-trust posture from day one |
| AP-3 | Free/open-source infrastructure only | BRD constraint for Phase 1 cost management |
| AP-4 | Separation of concerns via bounded contexts | Booking, Clinical, and Admin are distinct domains |
| AP-5 | Fail-safe over fail-fast for patient data | Clinical data must never be silently lost |
| AP-6 | Auditability as a first-class concern | Every mutation is logged immutably |

---

## 2. Non-Functional Requirements (Expanded)

### 2.1 Performance

| ID | Requirement | Target | Measurement |
|----|-------------|--------|-------------|
| NFR-001 | API response time for standard CRUD operations | < 200ms (p95) | Application Performance Monitoring |
| NFR-002 | Appointment slot search response time | < 500ms (p95) | API endpoint metrics |
| NFR-003 | 360-Degree Patient View generation time | < 120 seconds | Background job duration |
| NFR-004 | Document extraction processing time | < 5 minutes per document | Job queue metrics |
| NFR-005 | Concurrent user support | 100 simultaneous users | Load testing |
| NFR-006 | Page load time (frontend) | < 2 seconds (FCP) | Lighthouse score |

### 2.2 Availability and Reliability

| ID | Requirement | Target | Measurement |
|----|-------------|--------|-------------|
| NFR-007 | System uptime | 99.9% (8.76h downtime/year) | Uptime monitoring |
| NFR-008 | Recovery Time Objective (RTO) | < 1 hour | Disaster recovery drills |
| NFR-009 | Recovery Point Objective (RPO) | < 15 minutes | Backup frequency validation |
| NFR-010 | Background job failure recovery | Auto-retry with 3 attempts | Job queue monitoring |

### 2.3 Security and Compliance

| ID | Requirement | Target | Measurement |
|----|-------------|--------|-------------|
| NFR-011 | Data encryption in transit | TLS 1.2+ mandatory | Certificate validation |
| NFR-012 | Data encryption at rest | AES-256 for all PHI | Storage audit |
| NFR-013 | Session timeout | 15 minutes of inactivity | Session management tests |
| NFR-014 | Password policy | Min 12 chars, complexity rules | Authentication tests |
| NFR-015 | Audit log retention | 7 years minimum (HIPAA) | Storage policy |
| NFR-016 | Failed login lockout | 5 attempts → 30-minute lock | Security testing |
| NFR-017 | HIPAA compliance | 100% of applicable controls | Compliance audit |

### 2.4 Scalability

| ID | Requirement | Target | Measurement |
|----|-------------|--------|-------------|
| NFR-018 | Horizontal scaling readiness | Stateless API design | Architecture review |
| NFR-019 | Database connection pooling | Max 100 connections | Connection monitoring |
| NFR-020 | Cache hit ratio | > 80% for read-heavy endpoints | Redis metrics |

### 2.5 Maintainability

| ID | Requirement | Target | Measurement |
|----|-------------|--------|-------------|
| NFR-021 | Code coverage (backend) | > 80% | CI pipeline reports |
| NFR-022 | Code coverage (frontend) | > 70% | CI pipeline reports |
| NFR-023 | API documentation | 100% endpoint coverage | OpenAPI spec validation |
| NFR-024 | Deployment frequency | On-demand (no batching) | Release cadence |

---

## 3. Technical Requirements

### 3.1 Frontend

| ID | Requirement | Decision | Justification |
|----|-------------|----------|---------------|
| TR-001 | Frontend framework | Angular 17+ with standalone components | BRD mandate; standalone components reduce module boilerplate |
| TR-002 | State management | NgRx Signals or RxJS-based services | Reactive state for real-time slot updates and intake forms |
| TR-003 | UI component library | Angular Material or PrimeNG (free tier) | HIPAA-friendly, accessible components OOTB |
| TR-004 | HTTP client | Angular HttpClient with interceptors | Built-in, supports JWT injection and error handling |
| TR-005 | Frontend hosting | Netlify or Vercel (static SPA) | Free tier, global CDN, automatic HTTPS |
| TR-006 | Form handling | Angular Reactive Forms | Complex intake forms with dynamic validation |
| TR-007 | Routing guards | Angular Router Guards + JWT validation | Role-based route protection |

### 3.2 Backend

| ID | Requirement | Decision | Justification |
|----|-------------|----------|---------------|
| TR-008 | Backend framework | .NET 8 (LTS) Web API | BRD mandate; LTS ensures long-term support |
| TR-009 | Architecture pattern | Clean Architecture (4-layer) | Testability, separation of concerns, dependency inversion |
| TR-010 | CQRS implementation | MediatR | Decouples read/write paths for clinical data; simplifies handlers |
| TR-011 | ORM | Entity Framework Core 8 | Code-first migrations, LINQ queries, PostgreSQL support |
| TR-012 | Authentication | ASP.NET Identity + JWT Bearer tokens | Industry-standard, HIPAA-compatible auth framework |
| TR-013 | Authorization | Policy-based authorization with claims | Fine-grained RBAC per BRD roles |
| TR-014 | Validation | FluentValidation | Declarative, testable input validation at API boundary |
| TR-015 | Background processing | Hangfire (free/OSS) with PostgreSQL storage | Persistent job queue; no external dependencies; dashboard |
| TR-016 | PDF generation | QuestPDF (MIT license) | Free, fluent API, .NET native |
| TR-017 | Email delivery | MailKit + SMTP (free tier provider) | Open-source, robust MIME support |
| TR-018 | SMS delivery | Free SMS API (e.g., Vonage trial / open gateway) | Phase 1 uses trial/free tier for demo |
| TR-019 | Logging | Serilog with structured logging | Correlation IDs, sink flexibility, HIPAA-safe redaction |
| TR-020 | API documentation | Swagger/OpenAPI via Swashbuckle | Auto-generated, interactive documentation |
| TR-021 | Health checks | ASP.NET Health Checks middleware | Readiness/liveness probes for monitoring |

### 3.3 Data Infrastructure

| ID | Requirement | Decision | Justification |
|----|-------------|----------|---------------|
| TR-022 | Primary database | PostgreSQL 16 | BRD mandate; JSONB for flexible clinical data; free/OSS |
| TR-023 | Caching layer | Upstash Redis (serverless) | BRD mandate; free tier; low-latency session/cache |
| TR-024 | Database hosting | Neon or Supabase (free tier) | Managed PostgreSQL with free tier for Phase 1 |
| TR-025 | Migrations | EF Core Migrations | Version-controlled schema evolution |
| TR-026 | Connection management | Npgsql connection pooling | Efficient resource utilization |

### 3.4 Integration

| ID | Requirement | Decision | Justification |
|----|-------------|----------|---------------|
| TR-027 | Google Calendar sync | Google Calendar API v3 (free tier) | OAuth2 consent flow; free up to quota |
| TR-028 | Outlook Calendar sync | Microsoft Graph API (free tier) | Unified MS endpoint; OAuth2 |
| TR-029 | API communication style | RESTful with JSON | Standard, tooling-rich, frontend-friendly |
| TR-030 | Real-time updates | SignalR (WebSocket fallback) | Slot availability changes; queue status updates |

### 3.5 Deployment

| ID | Requirement | Decision | Justification |
|----|-------------|----------|---------------|
| TR-031 | Backend hosting | IIS on Windows Server / Windows Service | BRD requirement for native Windows deployment |
| TR-032 | CI/CD pipeline | GitHub Actions (free for public repos) | Automated build, test, deploy |
| TR-033 | Container support | Docker (optional for dev) | Consistent dev environments; not required for prod |
| TR-034 | Environment config | appsettings.json + Environment Variables | .NET standard; secrets via user-secrets/env vars |

---

## 4. Data Requirements

### 4.1 Logical Data Model (Core Entities)

| ID | Entity | Description | Key Attributes |
|----|--------|-------------|----------------|
| DR-001 | User | Base identity for all roles | Id, Email, PasswordHash, Role, IsActive, CreatedAt |
| DR-002 | PatientProfile | Extended patient demographics | UserId, FirstName, LastName, DOB, Phone, InsuranceId |
| DR-003 | Provider | Healthcare provider reference | Id, Name, Specialty, ScheduleTemplateId |
| DR-004 | Appointment | Booking record | Id, PatientId, ProviderId, SlotTime, Status, PreferredSlotId |
| DR-005 | AppointmentSlot | Available time slots | Id, ProviderId, StartTime, EndTime, IsAvailable |
| DR-006 | PreferredSlotPreference | Waitlist entry | Id, AppointmentId, PreferredSlotId, RegisteredAt, Status |
| DR-007 | IntakeRecord | Patient intake data | Id, PatientId, AppointmentId, Mode, DataJson, CompletedAt |
| DR-008 | ClinicalDocument | Uploaded PDF metadata | Id, PatientId, FileName, StoragePath, UploadedAt, ProcessingStatus |
| DR-009 | ExtractedData | Parsed clinical data | Id, DocumentId, PatientId, DataCategory, DataJson, ConfidenceScore |
| DR-010 | PatientView360 | Aggregated patient view | Id, PatientId, ConsolidatedDataJson, LastUpdatedAt, ConflictCount |
| DR-011 | DataConflict | Identified data conflicts | Id, PatientViewId, Field, ValueA, ValueB, SourceDocA, SourceDocB, ResolutionStatus |
| DR-012 | MedicalCode | Assigned ICD-10/CPT codes | Id, PatientViewId, CodeType, Code, Description, Confidence, VerifiedBy |
| DR-013 | AuditLog | Immutable action log | Id, UserId, Action, EntityType, EntityId, Timestamp, Details |
| DR-014 | Notification | Sent notifications | Id, PatientId, Channel, Type, SentAt, DeliveryStatus |
| DR-015 | InsuranceRecord | Dummy validation records | Id, ProviderName, MemberId, Status |

### 4.2 Data Flow Overview

```
Patient Upload → ClinicalDocument (stored encrypted)
                       │
                       ▼ (Background Job)
              PDF Text Extraction
                       │
                       ▼
              NLP/NER Processing
                       │
                       ▼
              ExtractedData (per document)
                       │
                       ▼ (Aggregation Job)
              De-duplication + Conflict Detection
                       │
                       ▼
              PatientView360 + DataConflict entries
                       │
                       ▼ (Coding Job)
              ICD-10/CPT Mapping → MedicalCode entries
```

### 4.3 Storage Strategy

| Data Type | Storage | Encryption | Retention |
|-----------|---------|------------|-----------|
| User credentials | PostgreSQL | bcrypt hash (passwords) | Account lifetime |
| Patient demographics | PostgreSQL | AES-256 column encryption | Account lifetime + 7 years |
| Clinical documents (PDF) | Local file system (encrypted) | AES-256 at rest | 7 years minimum |
| Extracted clinical data | PostgreSQL (JSONB) | AES-256 column encryption | 7 years minimum |
| Audit logs | PostgreSQL (append-only) | Integrity hash chain | 7 years minimum |
| Session data | Upstash Redis | TLS in transit | 15-minute TTL |
| Cache data | Upstash Redis | TLS in transit | Configurable TTL |

### 4.4 Data Integrity Rules

| ID | Rule | Implementation |
|----|------|----------------|
| DR-016 | Audit logs are append-only; no UPDATE or DELETE permitted | Database-level triggers + application-level enforcement |
| DR-017 | Patient data deletion requires soft-delete with audit trail | IsDeleted flag; physical deletion only via admin override |
| DR-018 | Document uploads are validated for format and size before storage | API-level validation (PDF only, max 50MB) |
| DR-019 | Clinical data conflicts must be explicitly resolved before final patient view | Status tracking on DataConflict entity |
| DR-020 | All timestamps stored in UTC | Application convention enforced in EF Core |

---

## 5. AI Requirements

### 5.1 Document Processing Pipeline

| ID | Requirement | Specification |
|----|-------------|---------------|
| AIR-001 | PDF text extraction | Extract text from both native PDFs and scanned documents (OCR) |
| AIR-002 | OCR capability | Tesseract OCR (open-source) for scanned document pages |
| AIR-003 | Text preprocessing | Normalize extracted text: remove artifacts, standardize formatting |
| AIR-004 | Processing throughput | Process a single document within 5 minutes |
| AIR-005 | Format support | PDF only for Phase 1 (extensible to DOCX, images in future) |

### 5.2 Clinical Entity Extraction

| ID | Requirement | Specification |
|----|-------------|---------------|
| AIR-006 | Named Entity Recognition (NER) | Extract: Medications, Diagnoses, Vitals, Procedures, Allergies |
| AIR-007 | NER model | spaCy with custom healthcare NER model or BioBERT-based model |
| AIR-008 | Entity confidence scoring | Each extracted entity includes a 0-100 confidence score |
| AIR-009 | Source attribution | Every extracted data point links back to source document and page |
| AIR-010 | Extraction accuracy | > 95% precision for medication and diagnosis extraction |

### 5.3 Data Aggregation Intelligence

| ID | Requirement | Specification |
|----|-------------|---------------|
| AIR-011 | De-duplication algorithm | Fuzzy matching on entity names + date proximity for same-entity detection |
| AIR-012 | Conflict detection | Identify contradicting values for the same clinical field across documents |
| AIR-013 | Conflict severity classification | Critical (medications, allergies), Warning (vitals variance), Info (minor) |
| AIR-014 | Temporal ordering | Latest document data takes precedence unless conflict is critical |

### 5.4 Medical Coding Engine

| ID | Requirement | Specification |
|----|-------------|---------------|
| AIR-015 | ICD-10 mapping | Map extracted diagnoses/conditions to ICD-10-CM codes |
| AIR-016 | CPT mapping | Map extracted procedures/services to CPT codes |
| AIR-017 | Code suggestion confidence | Each suggested code includes confidence score (0-100) |
| AIR-018 | Agreement rate target | > 98% AI-Human agreement rate for suggested codes |
| AIR-019 | Code database | Embedded ICD-10-CM and CPT lookup tables (open-source datasets) |
| AIR-020 | Multi-code support | Single clinical finding may map to multiple codes |

### 5.5 Conversational Intake AI

| ID | Requirement | Specification |
|----|-------------|---------------|
| AIR-021 | Conversational interface | Structured question flow with natural language understanding |
| AIR-022 | Intent recognition | Parse patient responses into structured intake fields |
| AIR-023 | Fallback handling | If AI cannot parse response, prompt for clarification or suggest manual entry |
| AIR-024 | Model hosting | Local/self-hosted model (Ollama with open-source LLM) or rule-based NLU |
| AIR-025 | Data privacy | No patient data sent to external AI services; all processing local |

### 5.6 AI Technology Stack

| Component | Technology | License | Justification |
|-----------|-----------|---------|---------------|
| OCR | Tesseract 5.x | Apache 2.0 | Industry-standard open-source OCR |
| PDF parsing | PdfPig (.NET) or PyMuPDF | Apache 2.0 / AGPL | Native text extraction from PDFs |
| NER | spaCy + scispaCy (Python) | MIT | Healthcare-specific NER models available |
| Medical coding | Custom rule engine + embedding search | N/A | ICD-10/CPT lookup with fuzzy matching |
| Conversational AI | Ollama + Mistral/Llama (local) OR rule-based | Open-source | HIPAA-safe local inference |
| AI service integration | Python FastAPI microservice | MIT | Lightweight; called by .NET backend |

---

## 6. Cross-Cutting Concerns

### 6.1 Security Architecture

| Concern | Implementation |
|---------|----------------|
| Authentication | JWT Bearer tokens (short-lived: 30min) + Refresh tokens (7-day, rotated) |
| Authorization | Claims-based policies: `Patient`, `Staff`, `Admin` with fine-grained permissions |
| Input validation | FluentValidation at API boundary; parameterized queries for all DB access |
| CORS | Strict origin whitelist (frontend domain only) |
| Rate limiting | ASP.NET Rate Limiting middleware (100 req/min per IP for anonymous; 300 for authenticated) |
| Secret management | Environment variables + .NET User Secrets (dev); no secrets in source control |
| Dependency scanning | Dependabot / OWASP dependency-check in CI pipeline |
| PHI redaction in logs | Serilog destructuring policies to mask SSN, DOB, full names in log output |

### 6.2 Logging and Monitoring

| Concern | Implementation |
|---------|----------------|
| Structured logging | Serilog with JSON output; correlation IDs per request |
| Log storage | File sink (rolling) + optional Seq/ELK (free/self-hosted) |
| Health monitoring | ASP.NET Health Checks (DB, Redis, external services) |
| Alerting | Health check failures trigger notification (email to admin) |
| Audit logging | Dedicated AuditLog table; EF Core interceptors for automatic capture |
| Performance metrics | Middleware for request duration tracking |

### 6.3 Error Handling Strategy

| Layer | Strategy |
|-------|----------|
| API Controllers | Global exception handler middleware; ProblemDetails RFC 7807 responses |
| Domain Services | Result pattern (no exceptions for expected failures) |
| Background Jobs | Retry with exponential backoff (3 attempts); dead-letter logging |
| External integrations | Circuit breaker pattern (Polly); graceful degradation |
| Frontend | Global error interceptor; user-friendly error messages; offline detection |

### 6.4 Configuration Management

| Environment | Strategy |
|-------------|----------|
| Development | appsettings.Development.json + User Secrets |
| Staging | appsettings.Staging.json + Environment Variables |
| Production | appsettings.Production.json + Environment Variables (no secrets in files) |
| Feature flags | Simple boolean config entries (extensible to feature flag service later) |

---

## 7. Architecture Decision Records

### ADR-001: Modular Monolith over Microservices

| Field | Value |
|-------|-------|
| **Status** | Accepted |
| **Context** | Phase 1 has a small team and free-hosting constraints; microservices add operational complexity |
| **Decision** | Use a modular monolith with bounded context boundaries (Booking, Clinical, Identity, Notification) |
| **Consequences** | Simpler deployment and debugging; clear module interfaces allow future extraction |

### ADR-002: CQRS for Clinical Data

| Field | Value |
|-------|-------|
| **Status** | Accepted |
| **Context** | Clinical data is write-infrequent (document upload) but read-heavy (patient view, coding); separate optimization needed |
| **Decision** | Apply CQRS via MediatR; read models optimized for 360-degree view; write models enforce business rules |
| **Consequences** | Slightly more code; significantly better read performance and maintainability |

### ADR-003: Python Sidecar for AI Processing

| Field | Value |
|-------|-------|
| **Status** | Accepted |
| **Context** | Best open-source NLP/NER libraries (spaCy, scispaCy, Tesseract bindings) are Python-native; .NET alternatives are immature for clinical NER |
| **Decision** | Deploy a Python FastAPI microservice alongside the .NET API; communicate via HTTP; .NET triggers jobs, Python executes AI logic |
| **Consequences** | Two runtime environments; Docker simplifies local dev; clear API contract between services |

### ADR-004: Local AI Inference (No External AI APIs)

| Field | Value |
|-------|-------|
| **Status** | Accepted |
| **Context** | HIPAA requires patient data not be sent to external services without BAA; free-tier LLM APIs do not provide BAAs |
| **Decision** | All AI processing runs locally: Tesseract for OCR, spaCy for NER, local LLM (Ollama) for conversational intake |
| **Consequences** | Higher compute requirements on deployment server; full data sovereignty; no API costs |

### ADR-005: JWT with Redis Session Validation

| Field | Value |
|-------|-------|
| **Status** | Accepted |
| **Context** | JWTs are stateless but cannot be revoked; HIPAA requires immediate session termination capability |
| **Decision** | Short-lived JWTs (30min) + session validation against Redis; deactivated users immediately blocked |
| **Consequences** | Slight overhead per request (Redis lookup); enables instant revocation and 15-min timeout enforcement |

### ADR-006: Append-Only Audit Log with Hash Chain

| Field | Value |
|-------|-------|
| **Status** | Accepted |
| **Context** | HIPAA mandates immutable audit trails; standard tables allow accidental UPDATE/DELETE |
| **Decision** | Audit table uses database triggers to prevent UPDATE/DELETE; each entry includes hash of previous entry for tamper detection |
| **Consequences** | Guaranteed immutability; tamper-evident chain; slightly more complex insert logic |

### ADR-007: QuestPDF for Document Generation

| Field | Value |
|-------|-------|
| **Status** | Accepted |
| **Context** | Need to generate appointment confirmation PDFs; must be free/OSS and .NET native |
| **Decision** | Use QuestPDF (MIT license) for all PDF generation |
| **Consequences** | Fluent C# API; no external dependencies; high-quality output |

### ADR-008: Hangfire for Background Job Processing

| Field | Value |
|-------|-------|
| **Status** | Accepted |
| **Context** | Need persistent, retriable background jobs for: document processing, reminder scheduling, slot swap monitoring, calendar sync |
| **Decision** | Use Hangfire with PostgreSQL storage (free for single-server) |
| **Consequences** | Built-in dashboard; persistent jobs survive restarts; PostgreSQL storage reuses existing infra |

---

## 8. Component Architecture

### 8.1 Backend Module Structure

```
src/
├── HealthPlatform.Api/                    # Presentation Layer
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── AppointmentsController.cs
│   │   ├── IntakeController.cs
│   │   ├── ClinicalController.cs
│   │   ├── AdminController.cs
│   │   └── NotificationsController.cs
│   ├── Middleware/
│   ├── Filters/
│   └── Program.cs
│
├── HealthPlatform.Application/            # Application Layer
│   ├── Booking/
│   │   ├── Commands/
│   │   └── Queries/
│   ├── Clinical/
│   │   ├── Commands/
│   │   └── Queries/
│   ├── Identity/
│   │   ├── Commands/
│   │   └── Queries/
│   ├── Notifications/
│   │   └── Commands/
│   └── Common/
│       ├── Interfaces/
│       ├── Behaviors/
│       └── Models/
│
├── HealthPlatform.Domain/                 # Domain Layer
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Enums/
│   ├── Events/
│   └── Exceptions/
│
├── HealthPlatform.Infrastructure/         # Infrastructure Layer
│   ├── Persistence/
│   │   ├── Configurations/
│   │   ├── Migrations/
│   │   └── ApplicationDbContext.cs
│   ├── Services/
│   │   ├── EmailService.cs
│   │   ├── SmsService.cs
│   │   ├── CalendarSyncService.cs
│   │   ├── PdfService.cs
│   │   └── AiProcessingClient.cs
│   ├── Identity/
│   └── Caching/
│
└── HealthPlatform.AI/                     # Python AI Microservice
    ├── app/
    │   ├── main.py                        # FastAPI entry
    │   ├── routers/
    │   │   ├── extraction.py
    │   │   ├── coding.py
    │   │   └── intake.py
    │   ├── services/
    │   │   ├── ocr_service.py
    │   │   ├── ner_service.py
    │   │   ├── dedup_service.py
    │   │   ├── coding_service.py
    │   │   └── intake_nlp_service.py
    │   └── models/
    └── requirements.txt
```

### 8.2 Frontend Module Structure

```
src/
├── app/
│   ├── core/                              # Singleton services, guards, interceptors
│   │   ├── auth/
│   │   ├── interceptors/
│   │   └── guards/
│   ├── shared/                            # Reusable components, pipes, directives
│   │   ├── components/
│   │   └── models/
│   ├── features/
│   │   ├── booking/                       # Appointment booking module
│   │   │   ├── components/
│   │   │   ├── services/
│   │   │   └── booking.routes.ts
│   │   ├── intake/                        # Patient intake module
│   │   │   ├── components/
│   │   │   ├── services/
│   │   │   └── intake.routes.ts
│   │   ├── clinical/                      # Clinical data view module
│   │   │   ├── components/
│   │   │   ├── services/
│   │   │   └── clinical.routes.ts
│   │   ├── admin/                         # Administration module
│   │   │   ├── components/
│   │   │   ├── services/
│   │   │   └── admin.routes.ts
│   │   └── dashboard/                     # Patient/Staff dashboards
│   │       ├── components/
│   │       └── dashboard.routes.ts
│   ├── app.component.ts
│   ├── app.config.ts
│   └── app.routes.ts
├── assets/
├── environments/
└── styles/
```

### 8.3 Bounded Contexts

| Context | Responsibilities | Key Entities |
|---------|-----------------|--------------|
| **Identity** | Authentication, authorization, user management, session control | User, Role, Session |
| **Booking** | Appointment CRUD, slot management, preferred swap, queue management | Appointment, AppointmentSlot, PreferredSlotPreference |
| **Clinical** | Document upload, extraction, aggregation, 360-view, coding | ClinicalDocument, ExtractedData, PatientView360, DataConflict, MedicalCode |
| **Intake** | Patient intake forms (AI + manual), intake data management | IntakeRecord |
| **Notification** | Email, SMS, reminders, calendar sync | Notification, ReminderSchedule |
| **Audit** | Immutable logging, compliance reporting | AuditLog |

---

## 9. Integration Architecture

### 9.1 External Integrations

| Integration | Protocol | Authentication | Rate Limits | Fallback |
|-------------|----------|---------------|-------------|----------|
| Google Calendar API | REST/HTTPS | OAuth 2.0 | 500 req/100s (free) | Queue and retry |
| Microsoft Graph API | REST/HTTPS | OAuth 2.0 | 10,000 req/10min (free) | Queue and retry |
| SMTP (Email) | SMTP/TLS | SASL | Provider-dependent | Queue with retry |
| SMS Gateway | REST/HTTPS | API Key | Provider-dependent | Log failure; notify staff |
| AI Processing Service | REST/HTTP (internal) | Internal API key | N/A (local) | Mark job as failed; allow manual retry |

### 9.2 Internal Communication

| From | To | Method | Purpose |
|------|----|--------|---------|
| Angular SPA | .NET API | HTTPS REST + SignalR | All user operations + real-time updates |
| .NET API | Python AI Service | HTTP REST (internal network) | Trigger document processing, coding, intake NLP |
| .NET API | PostgreSQL | TCP (Npgsql) | Data persistence |
| .NET API | Upstash Redis | TLS (StackExchange.Redis) | Caching, session validation |
| Hangfire Worker | .NET Services | In-process | Background job execution |

---

## 10. Deployment Architecture

### 10.1 Environment Topology

| Component | Development | Production |
|-----------|-------------|------------|
| Frontend (Angular) | localhost:4200 | Netlify/Vercel (CDN) |
| Backend (.NET API) | localhost:5000 | IIS / Windows Service |
| AI Service (Python) | localhost:8000 | Same server (systemd/Windows Service) |
| PostgreSQL | Neon free tier | Self-hosted or Neon |
| Redis | Upstash free tier | Upstash free tier |
| Background Jobs | In-process Hangfire | In-process Hangfire |

### 10.2 Deployment Pipeline

```
Code Push → GitHub Actions CI
              │
              ├── Build + Test (.NET)
              ├── Build + Test (Angular)
              ├── Build + Test (Python AI)
              ├── SAST Scan (security)
              │
              ▼
         Deploy to Staging
              │
              ├── Integration Tests
              ├── HIPAA Compliance Check
              │
              ▼
         Deploy to Production
              ├── Frontend → Netlify/Vercel
              ├── Backend → IIS (Web Deploy)
              └── AI Service → Windows Service
```

---

## 11. Risk Analysis

| Risk | Impact | Likelihood | Mitigation |
|------|--------|-----------|------------|
| Free-tier service limits exceeded | Service degradation | Medium | Monitor usage; design for graceful degradation; multiple free providers |
| AI extraction accuracy below target | Clinical safety | Medium | Mandatory human verification; confidence thresholds; gradual model improvement |
| HIPAA violation due to data leak | Legal/financial | Low | Encryption at all layers; access logging; regular security audits |
| Single-server deployment failure | Total outage | Medium | Automated backups; documented recovery procedure; health monitoring |
| Python AI service unavailability | Clinical features offline | Low | Health checks; auto-restart; manual processing fallback |
| Calendar API breaking changes | Sync failures | Low | Version-pin APIs; integration tests; circuit breaker pattern |
| Hangfire job queue backup | Delayed processing | Low | Job prioritization; monitoring; alerting on queue depth |

---

## 12. Traceability Matrix (Design → Spec → BRD)

| Design Requirement | Spec Requirement | BRD Section |
|-------------------|-----------------|-------------|
| TR-001, TR-005 | NFR-011 | §5 (Frontend: Angular) |
| TR-008, TR-031 | NFR-007, NFR-012 | §5 (Backend: .NET), §7 (Infrastructure) |
| TR-022, TR-024 | NFR-008 | §5 (Data: PostgreSQL) |
| TR-023 | NFR-009 | §7 (Upstash Redis) |
| AIR-001–AIR-005 | FR-032–FR-035 | §3, §6 (Clinical Data Aggregation) |
| AIR-006–AIR-010 | FR-034, FR-036 | §3 (360-Degree Patient View) |
| AIR-015–AIR-020 | FR-041–FR-044 | §6 (Medical Coding) |
| AIR-021–AIR-025 | FR-027–FR-031 | §4 (Flexible Patient Intake) |
| NFR-011–NFR-017 | FR-045–FR-048 | §7 (Security & Compliance) |
| ADR-004 | FR-047 | §7 (HIPAA Compliance) |
| ADR-006 | FR-045, FR-046 | §7 (Immutable Audit Logging) |

---

## 13. Technology Stack Summary

| Layer | Technology | Version | License |
|-------|-----------|---------|---------|
| Frontend Framework | Angular | 17+ | MIT |
| Frontend UI | Angular Material | 17+ | MIT |
| Backend Framework | .NET | 8 (LTS) | MIT |
| CQRS | MediatR | 12+ | Apache 2.0 |
| ORM | Entity Framework Core | 8 | MIT |
| Authentication | ASP.NET Identity + JWT | 8 | MIT |
| Validation | FluentValidation | 11+ | Apache 2.0 |
| Background Jobs | Hangfire | 1.8+ | LGPL |
| PDF Generation | QuestPDF | 2024+ | MIT |
| Email | MailKit | 4+ | MIT |
| Logging | Serilog | 3+ | Apache 2.0 |
| Primary Database | PostgreSQL | 16 | PostgreSQL License |
| Cache/Session | Upstash Redis | Serverless | N/A (SaaS) |
| OCR | Tesseract | 5.x | Apache 2.0 |
| NLP/NER | spaCy + scispaCy | 3.7+ | MIT |
| AI Service Framework | FastAPI (Python) | 0.100+ | MIT |
| Local LLM | Ollama + Mistral/Llama | Latest | Apache 2.0 / Meta |
| Real-time | SignalR | 8 | MIT |
| API Docs | Swashbuckle (OpenAPI) | 6+ | MIT |
| CI/CD | GitHub Actions | N/A | Free (public repos) |
| Frontend Hosting | Netlify or Vercel | N/A | Free tier |
