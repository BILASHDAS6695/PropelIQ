# Figma Design Implementation

## Document Information

| Field | Value |
|-------|-------|
| **Project** | Unified Patient Access & Clinical Intelligence Platform |
| **Figma File** | [HealthPlatform - Design System & Screens](https://www.figma.com/design/ZNVfDrqzdNswBEnfbjPW9a) |
| **File Key** | `ZNVfDrqzdNswBEnfbjPW9a` |
| **Status** | Design System COMPLETE; Screens SCRIPTED (pending execution) |
| **Source** | figma_spec.md, wireframes.md |
| **Scripts** | `.propel/context/figma/scripts/` (5 executable Figma Plugin API files) |

---

## 1. Completed in Figma

### 1.1 Page Structure

| Page | Content | Status |
|------|---------|--------|
| 1. Design System | Tokens, typography, spacing, components | COMPLETE |
| 2. Screens | All portal screens | PENDING |
| 3. Flows | Prototype connections | PENDING |

### 1.2 Color Tokens (Complete)

34 color swatches organized in sections:

- **Primary (Indigo)**: 50, 100, 200, 300, 400, 500, 600, 700, 800, 900
- **Neutral (Gray)**: 50, 100, 200, 300, 500, 700, 900
- **Semantic**: success, warning, error, info
- **Appointment Status**: Scheduled, Arrived, In Progress, Completed, Cancelled, No Show, Walk-in
- **NER Entity Highlights**: Diagnosis, Medication, Procedure, Lab Value, Symptom, Anatomy

### 1.3 Typography Scale (Complete)

| Level | Size | Weight | Line Height |
|-------|------|--------|-------------|
| Display Large | 36px | Bold | 44px |
| Display Medium | 30px | Bold | 38px |
| Heading 1 | 24px | Semi Bold | 32px |
| Heading 2 | 20px | Semi Bold | 28px |
| Heading 3 | 18px | Semi Bold | 26px |
| Body Large | 16px | Regular | 24px |
| Body | 14px | Regular | 20px |
| Body Small | 13px | Regular | 18px |
| Caption | 12px | Medium | 16px |
| Overline | 11px | Semi Bold | 16px |

### 1.4 Spacing Scale (Complete)

12 values: 4, 8, 12, 16, 20, 24, 32, 40, 48, 64, 80, 96px (base: 4px)

### 1.5 Core Components (Complete)

| Component | Variants |
|-----------|----------|
| Buttons | Primary, Secondary, Outlined, Danger, Success, Ghost |
| Input Fields | Default, Focus, Error, Disabled |
| Cards | Appointment Card, Patient Card, Alert Card |
| Status Badges | Scheduled, Arrived, In Progress, Completed, Cancelled, No Show |

---

## 2. Remaining Screens (To Build)

### 2.1 Auth Screens

#### SCR-AUTH-01: Login

```
Layout: Split (60/40)
Left: Indigo brand panel with logo + tagline
Right: Form panel
  - Title: "Sign in to your account"
  - Email input (with label)
  - Password input (with show/hide toggle)
  - "Remember me" checkbox + "Forgot password?" link
  - Primary button: "Sign In" (full width)
  - Divider: "or"
  - Link: "Create a new account"
  
States: Default, Loading, Error (invalid credentials), MFA prompt
Mobile: Full-width form, brand panel hidden
```

#### SCR-AUTH-02: Registration

```
Layout: Split (60/40) — same brand panel
Right: Multi-step form
  Step 1: Name (first, last), Email, Phone
  Step 2: Password, Confirm Password (strength meter)
  Step 3: Role selection (Patient pre-selected, Staff/Admin admin-only)
  
Stepper: Horizontal dots (1-2-3)
Validation: Inline, real-time
```

---

### 2.2 Patient Portal Screens

#### SCR-PAT-01: Dashboard

```
Layout: Shell (sidebar + header + content)
┌─────────────────────────────────────────────────┐
│ Header: Logo | Search | Notifications | Avatar  │
├────────┬────────────────────────────────────────┤
│ Nav    │ Content                                 │
│        │ ┌──────────────────────────────────┐   │
│ Dash   │ │ Welcome, Sarah     Good Morning  │   │
│ Book   │ └──────────────────────────────────┘   │
│ Appts  │ ┌─────────────┐ ┌─────────────────┐   │
│ Intake │ │ Next Appt   │ │ Pending Intake  │   │
│ Docs   │ │ Dr. Smith   │ │ Complete before │   │
│ Cal    │ │ 10:30 AM    │ │ your visit      │   │
│ Notify │ │ [View]      │ │ [Start Now]     │   │
│ Profile│ └─────────────┘ └─────────────────┘   │
│        │ ┌──────────────────────────────────┐   │
│        │ │ Upcoming Appointments            │   │
│        │ │ ┌────┐ ┌────┐ ┌────┐            │   │
│        │ │ │Card│ │Card│ │Card│             │   │
│        │ │ └────┘ └────┘ └────┘            │   │
│        │ └──────────────────────────────────┘   │
│        │ ┌──────────────────────────────────┐   │
│        │ │ Recent Notifications             │   │
│        │ │ • Appointment confirmed          │   │
│        │ │ • Document processed             │   │
│        │ └──────────────────────────────────┘   │
└────────┴────────────────────────────────────────┘

Components: AppShell, WelcomeBanner, StatCard, AppointmentCard, NotificationList
States: Empty (no appointments), Has pending intake, Has notifications
```

#### SCR-PAT-02: Book Appointment (3-step wizard)

```
Step 1 — Provider Selection:
┌─────────────────────────────────────────┐
│ Book Appointment           Step 1 of 3  │
├─────────────────────────────────────────┤
│ [Search providers...]                   │
│                                         │
│ ┌──────────────────────────────┐        │
│ │ 👤 Dr. Anand Patel          │        │
│ │    Cardiology | Rating: 4.8 │        │
│ │    Next available: Tomorrow │        │
│ │    [Select]                 │        │
│ └──────────────────────────────┘        │
│ ┌──────────────────────────────┐        │
│ │ 👤 Dr. Lisa Wong            │        │
│ │    General Practice         │        │
│ │    Next available: Today    │        │
│ │    [Select]                 │        │
│ └──────────────────────────────┘        │
└─────────────────────────────────────────┘

Step 2 — Date & Slot:
- Calendar date picker (current month, disabled past dates)
- Time slots grid (Morning / Afternoon / Evening sections)
- Selected slot highlighted in primary-500
- Conflict warning banner if overlapping

Step 3 — Confirmation:
- Summary card (Provider, Date, Time, Type)
- Reason for visit (textarea, optional)
- Insurance selection dropdown
- [Confirm Booking] primary button
- Success state: confetti animation, add to calendar CTA
```

#### SCR-PAT-03: My Appointments

```
Tabs: Upcoming | Past | Cancelled
Table/Card toggle (grid icon)

Card view:
┌────────────────────────────────┐
│ 🟦 Scheduled                   │
│ Dr. Patel — Cardiology         │
│ Jan 15, 2025 • 10:30 AM       │
│ [Cancel] [Reschedule] [Details]│
└────────────────────────────────┘

Actions per status:
- Scheduled: Cancel, Reschedule, View
- Completed: View Notes, Rebook
- Cancelled: Rebook
```

#### SCR-PAT-04: Intake — Chat Mode

```
Layout: Chat interface
┌─────────────────────────────────────────┐
│ Intake for: Dr. Patel — Jan 15         │
│ ○○○●○ Step 3 of 5                      │
├─────────────────────────────────────────┤
│                                         │
│  🤖 What brings you in today?          │
│                                         │
│      Chest pain that started    👤     │
│      two days ago                       │
│                                         │
│  🤖 Can you describe the pain?         │
│     Is it sharp, dull, or              │
│     pressure-like?                      │
│                                         │
│      It feels like pressure     👤     │
│                                         │
│  🤖 On a scale of 1-10, how           │
│     severe is it?                       │
│                                         │
├─────────────────────────────────────────┤
│ [Type your response...]        [Send]   │
│ [Switch to Form] [Skip Question]        │
└─────────────────────────────────────────┘

Features:
- Progress stepper (Chief Complaint → History → Medications → Allergies → Review)
- Bot avatar + user avatar alignment
- Typing indicator animation
- "Switch to Form" escape hatch
- Summary panel (collapsible right sidebar on desktop)
```

#### SCR-PAT-05: Intake — Form Mode

```
Traditional multi-section form:
- Chief Complaint (textarea)
- Current Medications (repeatable row: name, dosage, frequency)
- Allergies (tag input with severity chips)
- Medical History (checkbox conditions + custom entry)
- Review & Submit (read-only summary)

Progress: Same stepper as chat mode
```

#### SCR-PAT-06: Document Upload

```
┌─────────────────────────────────────────┐
│ My Documents                    [Upload] │
├─────────────────────────────────────────┤
│ ┌──────────────────────────────────┐    │
│ │ ┌────┐  Blood Work Results.pdf  │    │
│ │ │ PDF│  Uploaded: Jan 10        │    │
│ │ │icon│  Status: ✅ Processed     │    │
│ │ └────┘  [View] [Download]       │    │
│ └──────────────────────────────────┘    │
│ ┌──────────────────────────────────┐    │
│ │ ┌────┐  Referral Letter.pdf     │    │
│ │ │ PDF│  Uploaded: Jan 8         │    │
│ │ │icon│  Status: ⏳ Processing    │    │
│ │ └────┘  Progress: 65%           │    │
│ └──────────────────────────────────┘    │
│                                         │
│  Upload Zone:                           │
│  ┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐    │
│  │  📄 Drag & drop files here     │    │
│  │     or click to browse          │    │
│  │  Supported: PDF, JPG, PNG      │    │
│  │  Max: 10MB per file             │    │
│  └ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘    │
└─────────────────────────────────────────┘
```

#### SCR-PAT-07: Document Viewer (NER Highlights)

```
Layout: PDF viewer + NER sidebar
┌──────────────────────┬─────────────────┐
│ Document Viewer      │ Extracted Data  │
│                      │                 │
│ [PDF content with    │ Diagnoses:      │
│  highlighted         │ • Hypertension  │
│  entities in color]  │ • Type 2 DM    │
│                      │                 │
│ "Patient presents    │ Medications:    │
│  with [hypertension] │ • Metformin 500 │
│  controlled on       │ • Lisinopril 10 │
│  [metformin 500mg]"  │                 │
│                      │ Lab Values:     │
│                      │ • HbA1c: 6.8   │
│                      │ • BP: 130/85   │
│                      │                 │
│                      │ Confidence: 92% │
│                      │ [Verify] [Edit] │
└──────────────────────┴─────────────────┘

Color legend at top matching NER tokens
Hover entity → tooltip with confidence %
Click entity → scroll to source in document
```

#### SCR-PAT-08: Calendar

```
Views: Month | Week | Day (toggle buttons)
Month view: Standard grid with appointment dots
Week view: Time grid (7am–7pm) with appointment blocks
Day view: Detailed timeline with appointment cards

Appointment blocks colored by status
Click → appointment detail popover
```

---

### 2.3 Staff Portal Screens

#### SCR-STAFF-01: Staff Dashboard / Queue

```
Layout: Full-width with provider tabs
┌─────────────────────────────────────────────────┐
│ Today's Queue — Jan 15, 2025      [Walk-in +]   │
├─────────────────────────────────────────────────┤
│ [Dr. Patel] [Dr. Wong] [Dr. Rivera]             │
├─────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────┐    │
│ │ 🟣 9:00  John Doe      Arrived  [✓ Start]│    │
│ ├──────────────────────────────────────────┤    │
│ │ 🟦 9:30  Jane Smith    Scheduled [Mark]  │    │
│ ├──────────────────────────────────────────┤    │
│ │ 🟢 10:00 Bob Johnson   In Progress       │    │
│ ├──────────────────────────────────────────┤    │
│ │ 🟦 10:30 Sarah Chen    Scheduled [Mark]  │    │
│ ├──────────────────────────────────────────┤    │
│ │ 🟡 11:00 Mike Brown    Late       [NoShow]│   │
│ └──────────────────────────────────────────┘    │
│                                                  │
│ Stats: 12 Total | 3 Arrived | 2 In Progress     │
│        1 No-show | 6 Remaining                   │
└─────────────────────────────────────────────────┘

Real-time: SignalR updates, row animations on status change
Actions: Mark Arrived, Start, Complete, No-Show, Cancel
Alerts: Banner for conflicts, overlapping slots
```

#### SCR-STAFF-02: Multi-Provider Calendar

```
Layout: Week view with provider columns
Time axis on left (7am–7pm, 15-min increments)
Each provider gets a column
Color-coded appointment blocks
Drag-to-reschedule (with conflict detection)
Click empty slot → quick book modal
```

#### SCR-STAFF-03: Walk-in Registration

```
Quick form:
- Patient search (autocomplete existing patients)
- OR: New patient (Name, Phone, DOB, Reason)
- Provider selection (only those with availability)
- Auto-assigns next available slot
- Prints queue ticket (via browser print)
```

#### SCR-STAFF-04: Patient 360-Degree View

```
Layout: Tabbed interface
┌─────────────────────────────────────────────────┐
│ Patient: John Doe (MRN: 12345)    [Edit] [Flag] │
├─────────────────────────────────────────────────┤
│ [Overview] [Appointments] [Documents] [Intake]   │
├─────────────────────────────────────────────────┤
│ Demographics          │ Active Conditions        │
│ DOB: 1958-03-15      │ • Hypertension (ICD: I10)│
│ Phone: 555-0123      │ • T2DM (ICD: E11.9)     │
│ Insurance: Aetna     │ • Hyperlipidemia (E78.5) │
│                      │                           │
│ Current Medications  │ Recent Labs               │
│ • Metformin 500mg   │ • HbA1c: 6.8 (Jan 10)   │
│ • Lisinopril 10mg   │ • Lipid Panel (Jan 10)   │
│ • Atorvastatin 20mg │ • CBC (Dec 15)           │
│                      │                           │
│ Allergies            │ Upcoming Appointments     │
│ • Penicillin (severe)│ • Jan 15 — Dr. Patel    │
│ • Sulfa (moderate)   │ • Feb 20 — Dr. Wong     │
└─────────────────────────────────────────────────┘

Data sources: Intake records, Document NER, Manual entry
Conflicts highlighted with ⚠️ icon → link to conflict resolution
```

#### SCR-STAFF-05: Conflict Resolution

```
Layout: Side-by-side comparison
┌──────────────────────┬──────────────────────┐
│ Source A: Intake     │ Source B: Document   │
│ (Jan 15 chat)       │ (Blood Work.pdf)     │
├──────────────────────┼──────────────────────┤
│ Medication:          │ Medication:          │
│ Metformin 500mg BID │ Metformin 1000mg QD  │
│                      │                      │
│ ⚠️ CONFLICT:        │                      │
│ Dosage discrepancy  │                      │
├──────────────────────┴──────────────────────┤
│ Resolution:                                  │
│ ○ Keep Source A (Intake)                    │
│ ○ Keep Source B (Document)                  │
│ ○ Manual override: [________________]       │
│                                              │
│ [Resolve] [Skip] [Flag for Review]          │
└──────────────────────────────────────────────┘
```

#### SCR-STAFF-06: Medical Coding Queue

```
Table: Patient | Document | Suggested Codes | Confidence | Action
┌─────────────────────────────────────────────────────────────────┐
│ Medical Coding Verification              [Filter ▼] [Export]    │
├──────────┬───────────┬──────────────┬────────┬─────────────────┤
│ Patient  │ Document  │ Code         │ Conf.  │ Action          │
├──────────┼───────────┼──────────────┼────────┼─────────────────┤
│ John Doe │ Labs.pdf  │ E11.9 T2DM  │ 95%    │ [✓] [✗] [Edit] │
│          │           │ I10 HTN     │ 92%    │ [✓] [✗] [Edit] │
│          │           │ E78.5 HLD   │ 78%    │ [✓] [✗] [Edit] │
├──────────┼───────────┼──────────────┼────────┼─────────────────┤
│ Jane Doe │ Ref.pdf   │ M54.5 LBP   │ 88%    │ [✓] [✗] [Edit] │
└──────────┴───────────┴──────────────┴────────┴─────────────────┘

Low confidence (<80%) highlighted in warning-50 background
Click code → opens document viewer with highlighted source text
Bulk actions: Verify All >90%, Export verified
```

---

### 2.4 Admin Portal Screens

#### SCR-ADMIN-01: User Management

```
Table with search + filters:
- Search by name/email
- Filter: Role, Status (Active/Deactivated)
- Columns: Name, Email, Role, Status, Created, Last Login, Actions
- Actions: Edit, Deactivate/Activate, Reset Password
- [+ Create User] button → modal form
```

#### SCR-ADMIN-02: Audit Log Viewer

```
Filterable log table:
- Date range picker
- Filter: User, Action Type, Entity Type
- Columns: Timestamp, User, Action, Entity, IP Address, Details
- Expandable row → JSON diff of changes
- [Export CSV] button
- Hash chain integrity indicator (green check)
```

#### SCR-ADMIN-03: System Health Dashboard

```
Grid of metric cards:
┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│ API Uptime  │ │ Avg Response│ │ Active Users│
│ 99.9%       │ │ 145ms       │ │ 23          │
│ ✅ Healthy  │ │ ✅ <200ms   │ │             │
└─────────────┘ └─────────────┘ └─────────────┘
┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│ DB Pool     │ │ Redis Conn  │ │ Failed Jobs │
│ 12/50       │ │ Connected   │ │ 0           │
│ ✅ Normal   │ │ ✅ Active   │ │ ✅ Clear    │
└─────────────┘ └─────────────┘ └─────────────┘

Below: Recent error log (last 24h), Job queue status
```

---

### 2.5 Shared Components

#### Navigation Shell

```
Desktop (>1024px):
- Collapsible left sidebar (240px expanded, 64px collapsed)
- Top header bar (64px height)
  - Logo + App Name (left)
  - Global Search (center, expandable)
  - Notification bell (badge count) + User avatar dropdown (right)
- Sidebar items: Icon + Label, active state = primary-500 bg tint
- Footer: Collapse toggle + App version

Mobile (<768px):
- Bottom tab bar (5 main items)
- Hamburger menu for secondary nav
- No sidebar
```

#### Modal / Dialog

```
Sizes: Small (400px), Medium (560px), Large (720px), Full (90vw)
Structure:
  - Header: Title + Close (X) button
  - Body: Scrollable content
  - Footer: Action buttons (right-aligned)
  
Backdrop: neutral-900 at 50% opacity
Animation: Fade in + slide up (200ms ease-out)
Focus trap: Tab cycles within modal
ESC key: Closes modal
```

#### Toast / Notification

```
Position: Top-right, stacked
Types: Success (green), Error (red), Warning (amber), Info (blue)
Structure: Icon + Message + Close button
Auto-dismiss: 5s (info/success), persistent (error/warning)
Animation: Slide in from right (300ms)
Max visible: 3 (oldest auto-dismissed)
```

#### Data Table

```
Features:
- Sortable columns (click header)
- Column resize (drag border)
- Pagination (10/25/50/100 per page)
- Row selection (checkbox)
- Inline actions (icon buttons)
- Empty state illustration
- Loading skeleton rows
- Responsive: Horizontal scroll on mobile, or card layout toggle
```

---

## 3. Prototype Flows

### Flow 1: Patient Books Appointment

```
Login → Dashboard → [Book Appointment] →
Provider Selection → Date/Slot → Confirmation →
Success → Dashboard (updated)
```

### Flow 2: Patient Completes Intake (Chat)

```
Dashboard → [Start Intake] → Chat Mode →
(5 steps: Complaint → History → Medications → Allergies → Review) →
Submit → Success → Dashboard
```

### Flow 3: Staff Manages Queue

```
Staff Login → Staff Dashboard/Queue →
[Mark Arrived] → Row updates →
[Start Appointment] → Status: In Progress →
[Complete] → Status: Completed → Next patient
```

### Flow 4: Document Upload & Review

```
Patient Portal → Documents → [Upload] →
Drag file → Upload progress → Processing →
Status: Processed → [View] → Document Viewer (NER highlights)
```

### Flow 5: Staff Conflict Resolution

```
Patient 360 View → [⚠️ Conflict] →
Conflict Resolution screen → Compare sources →
Select resolution → [Resolve] → Updated 360 view
```

### Flow 6: Medical Coding Verification

```
Staff Dashboard → Medical Coding →
Select patient row → Code Review →
Verify/Reject each code → [Save] → Updated queue
```

---

## 4. Responsive Breakpoints

| Breakpoint | Width | Layout Changes |
|------------|-------|----------------|
| Desktop XL | ≥1440px | Max content width, sidebar expanded |
| Desktop | ≥1024px | Full layout, sidebar collapsible |
| Tablet | ≥768px | Sidebar hidden, hamburger nav, 2-col grid → 1-col |
| Mobile | <768px | Bottom nav, single column, cards full-width |

---

## 5. Implementation Guide (Figma MCP)

To continue building screens when rate limits reset, use:

```javascript
// File key for all operations
const fileKey = "ZNVfDrqzdNswBEnfbjPW9a";

// Switch to Screens page
const page = figma.root.children[1]; // "2. Screens"
await figma.setCurrentPageAsync(page);

// Grid layout for screen placement:
// Row 1 (y=0):    Auth screens (x: 0, 1600, 3200)
// Row 2 (y=1100): Patient screens (x: 0, 1600, 3200, 4800...)
// Row 3 (y=2200): Staff screens
// Row 4 (y=3300): Admin screens
```

### Component Naming Convention

```
Category/Variant/State
Examples:
  Button/Primary/Default
  Button/Primary/Hover
  Input/Text/Error
  Card/Appointment/Scheduled
  Badge/Status/Arrived
  Nav/Sidebar/Expanded
  Modal/Confirmation/Default
```

### Font Loading (Required)

```javascript
await figma.loadFontAsync({family: "Inter", style: "Bold"});
await figma.loadFontAsync({family: "Inter", style: "Semi Bold"});
await figma.loadFontAsync({family: "Inter", style: "Medium"});
await figma.loadFontAsync({family: "Inter", style: "Regular"});
```

---

## 6. Traceability

| Screen ID | Wireframe Ref | User Story | Epic |
|-----------|--------------|------------|------|
| SCR-AUTH-01 | WF-AUTH-01 | US-013 | EP-001 |
| SCR-AUTH-02 | WF-AUTH-02 | US-014 | EP-001 |
| SCR-PAT-01 | WF-PAT-01 | US-019 | EP-002 |
| SCR-PAT-02 | WF-PAT-02 | US-020, US-021 | EP-002 |
| SCR-PAT-03 | WF-PAT-03 | US-022, US-023 | EP-002 |
| SCR-PAT-04 | WF-PAT-04 | US-040, US-041 | EP-006 |
| SCR-PAT-05 | WF-PAT-05 | US-042 | EP-006 |
| SCR-PAT-06 | WF-PAT-06 | US-045, US-046 | EP-007 |
| SCR-PAT-07 | WF-PAT-07 | US-047, US-048 | EP-007 |
| SCR-PAT-08 | WF-PAT-08 | US-037, US-038 | EP-005 |
| SCR-STAFF-01 | WF-STAFF-01 | US-024 | EP-002 |
| SCR-STAFF-02 | WF-STAFF-02 | US-025 | EP-002 |
| SCR-STAFF-03 | WF-STAFF-03 | US-026 | EP-002 |
| SCR-STAFF-04 | WF-STAFF-04 | US-050–054 | EP-008 |
| SCR-STAFF-05 | WF-STAFF-05 | US-051 | EP-008 |
| SCR-STAFF-06 | WF-STAFF-06 | US-055–058 | EP-009 |
| SCR-ADMIN-01 | WF-ADMIN-01 | US-059, US-060 | EP-010 |
| SCR-ADMIN-02 | WF-ADMIN-02 | US-061, US-062 | EP-010 |
| SCR-ADMIN-03 | WF-ADMIN-03 | US-063 | EP-010 |
