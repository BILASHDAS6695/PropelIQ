# Figma Design Specification

## Document Information

| Field | Value |
|-------|-------|
| **Project** | Unified Patient Access & Clinical Intelligence Platform |
| **Version** | 1.0 |
| **Status** | Draft |
| **Source** | spec.md, design.md |
| **Phase** | Phase 1 |

---

## 1. UX Strategy

### 1.1 Design Principles

| # | Principle | Application |
|---|-----------|-------------|
| DP-1 | Clinical clarity | Dense medical information presented in scannable hierarchy; critical data (conflicts, alerts) visually dominant |
| DP-2 | Progressive disclosure | Complex workflows (intake, document review) broken into steps; advanced actions hidden until relevant |
| DP-3 | Error prevention over error recovery | Slot conflicts detected before booking; destructive actions require explicit confirmation |
| DP-4 | Accessibility-first | WCAG 2.1 AA minimum; keyboard navigation for all workflows; screen reader compatible |
| DP-5 | Role-appropriate density | Patient views are spacious and friendly; Staff/Admin views are information-dense and efficient |
| DP-6 | Trust through transparency | Processing states visible; AI confidence displayed; audit trail accessible |

### 1.2 Personas & Goals

| Persona | Role | Primary Goals | Pain Points |
|---------|------|---------------|-------------|
| **Sarah Chen** | Patient (30s, tech-savvy) | Book appointments quickly; complete intake before visit; upload documents from phone | Long hold times; paper forms at clinic; lost medical history |
| **James Okafor** | Patient (65, limited tech) | Find provider; book with minimal steps; prefer form over AI chat | Small text; too many options; confusing navigation |
| **Maria Gonzalez** | Front-desk Staff | Process walk-ins fast; manage queue; mark arrivals without friction | Switching between systems; losing queue position; manual data entry |
| **Dr. Anand Patel** | Provider | See today's queue at a glance; review patient 360-view before appointment; verify codes quickly | Scrolling through documents; missing patient history; low-confidence codes |
| **Rachel Kim** | Admin | Manage users; view audit logs; monitor system health | Complex user management; unclear audit filtering; no system visibility |

### 1.3 Emotional Design Targets

| Context | Emotion | How |
|---------|---------|-----|
| Booking flow | Confidence | Clear availability, instant confirmation, calendar integration |
| Intake (AI chat) | Comfort | Friendly tone, progress awareness, easy escape to form |
| Document processing | Trust | Visible progress, confidence scores, source attribution |
| Staff queue | Control | Real-time updates, one-click actions, clear status hierarchy |
| Conflict resolution | Precision | Side-by-side comparison, source documents linked, clear resolution action |

---

## 2. Information Architecture

### 2.1 Navigation Structure

```
Patient Portal
├── Dashboard (Home)
│   ├── Upcoming Appointments (card list)
│   ├── Pending Intake (CTA)
│   └── Recent Notifications
├── Book Appointment
│   ├── Provider Selection
│   ├── Date & Slot Picker
│   └── Confirmation
├── My Appointments
│   ├── Upcoming (with actions)
│   ├── Past
│   └── Cancelled
├── Intake
│   ├── Chat Mode
│   └── Form Mode
├── Documents
│   ├── Upload
│   ├── Document List
│   └── Document Viewer
├── Calendar
│   ├── Month View
│   ├── Week View
│   └── Day View
├── Notifications
│   └── History
└── Profile & Settings
    ├── Personal Info
    ├── Insurance
    ├── Notification Preferences
    └── Security (Password)

Staff Portal
├── Dashboard
│   ├── Today's Queue (per provider)
│   ├── Pending Arrivals
│   └── Alerts (conflicts, no-shows)
├── Schedule Management
│   ├── Multi-Provider Calendar
│   ├── Walk-in Registration
│   └── Search Appointments
├── Patient Records
│   ├── Patient Search
│   ├── Patient Detail
│   │   ├── 360-Degree View
│   │   ├── Documents
│   │   ├── Appointments
│   │   └── Intake Records
│   └── Conflict Resolution
├── Medical Coding
│   ├── Pending Verification
│   └── Code Review Interface
├── Notifications
│   └── Swap Requests / Alerts
└── Reports

Admin Portal (extends Staff)
├── User Management
│   ├── User List
│   ├── Create User
│   └── User Detail (edit/deactivate)
├── Audit Logs
│   ├── Log Viewer (filterable)
│   └── Export
├── System
│   ├── Health Dashboard
│   └── Configuration
└── Provider Management
    ├── Provider List
    └── Schedule Configuration
```

### 2.2 URL Routing Plan

| Route | Screen | Role |
|-------|--------|------|
| `/login` | Login | Public |
| `/register` | Registration | Public |
| `/dashboard` | Role-specific Dashboard | All |
| `/appointments/book` | Booking Flow | Patient |
| `/appointments` | My Appointments | Patient |
| `/appointments/:id` | Appointment Detail | Patient/Staff |
| `/intake/:appointmentId` | Intake (Chat/Form) | Patient |
| `/documents` | Document List | Patient |
| `/documents/:id` | Document Viewer | Patient/Staff |
| `/calendar` | Calendar View | All |
| `/notifications` | Notification Center | All |
| `/profile` | Profile & Settings | All |
| `/staff/queue` | Daily Queue | Staff |
| `/staff/schedule` | Multi-Provider Calendar | Staff |
| `/staff/walkin` | Walk-in Registration | Staff |
| `/staff/patients` | Patient Search | Staff |
| `/staff/patients/:id` | Patient 360 View | Staff |
| `/staff/patients/:id/conflicts` | Conflict Resolution | Staff |
| `/staff/coding` | Medical Coding Queue | Staff |
| `/staff/coding/:patientId` | Code Review | Staff |
| `/admin/users` | User Management | Admin |
| `/admin/users/:id` | User Detail | Admin |
| `/admin/audit` | Audit Log Viewer | Admin |
| `/admin/system` | System Health | Admin |
| `/admin/providers` | Provider Management | Admin |

---

## 3. Design System Tokens

### 3.1 Color Palette

#### Brand Colors

| Token | Hex | Usage |
|-------|-----|-------|
| `--color-primary-50` | `#EEF2FF` | Primary tint backgrounds |
| `--color-primary-100` | `#E0E7FF` | Hover states, selected row bg |
| `--color-primary-200` | `#C7D2FE` | Active states |
| `--color-primary-500` | `#6366F1` | Primary buttons, links, active nav |
| `--color-primary-600` | `#4F46E5` | Primary button hover |
| `--color-primary-700` | `#4338CA` | Primary button pressed |
| `--color-primary-900` | `#312E81` | Dark emphasis text on primary bg |

#### Semantic Colors

| Token | Hex | Usage |
|-------|-----|-------|
| `--color-success-50` | `#F0FDF4` | Success alert bg |
| `--color-success-500` | `#22C55E` | Success icons, completed status |
| `--color-success-700` | `#15803D` | Success text |
| `--color-warning-50` | `#FFFBEB` | Warning alert bg |
| `--color-warning-500` | `#F59E0B` | Warning icons, late arrival |
| `--color-warning-700` | `#B45309` | Warning text |
| `--color-error-50` | `#FEF2F2` | Error alert bg |
| `--color-error-500` | `#EF4444` | Error icons, cancelled status |
| `--color-error-700` | `#B91C1C` | Error text |
| `--color-info-50` | `#EFF6FF` | Info alert bg |
| `--color-info-500` | `#3B82F6` | Info icons, scheduled status |
| `--color-info-700` | `#1D4ED8` | Info text |

#### Neutral Colors

| Token | Hex | Usage |
|-------|-----|-------|
| `--color-neutral-0` | `#FFFFFF` | Page background, card bg |
| `--color-neutral-50` | `#F8FAFC` | Secondary bg, table alt rows |
| `--color-neutral-100` | `#F1F5F9` | Input bg (disabled), dividers |
| `--color-neutral-200` | `#E2E8F0` | Borders, separators |
| `--color-neutral-300` | `#CBD5E1` | Placeholder text, disabled |
| `--color-neutral-500` | `#64748B` | Secondary text, icons |
| `--color-neutral-700` | `#334155` | Body text |
| `--color-neutral-900` | `#0F172A` | Headings, emphasis |

#### Status Colors (Appointment-specific)

| Token | Hex | Status |
|-------|-----|--------|
| `--status-scheduled` | `#3B82F6` | Scheduled (blue) |
| `--status-arrived` | `#8B5CF6` | Arrived (purple) |
| `--status-in-progress` | `#22C55E` | In Progress (green) |
| `--status-completed` | `#6B7280` | Completed (gray) |
| `--status-cancelled` | `#EF4444` | Cancelled (red) |
| `--status-no-show` | `#F59E0B` | No Show (amber) |
| `--status-walk-in` | `#06B6D4` | Walk-in (cyan) |

#### NER Entity Highlight Colors

| Token | Hex | Entity Type |
|-------|-----|-------------|
| `--entity-diagnosis` | `#FCA5A5` | Diagnosis (red tint) |
| `--entity-medication` | `#93C5FD` | Medication (blue tint) |
| `--entity-procedure` | `#86EFAC` | Procedure (green tint) |
| `--entity-lab` | `#C4B5FD` | Lab Test/Value (purple tint) |
| `--entity-symptom` | `#FDBA74` | Symptom (orange tint) |
| `--entity-anatomy` | `#67E8F9` | Anatomy (cyan tint) |

### 3.2 Typography

| Token | Font | Weight | Size | Line Height | Usage |
|-------|------|--------|------|-------------|-------|
| `--type-display-lg` | Inter | 700 | 30px | 36px | Page titles (Patient portal) |
| `--type-display-sm` | Inter | 600 | 24px | 32px | Section headings |
| `--type-heading-lg` | Inter | 600 | 20px | 28px | Card titles, modal titles |
| `--type-heading-sm` | Inter | 600 | 16px | 24px | Subsections, table headers |
| `--type-body-lg` | Inter | 400 | 16px | 24px | Primary body text |
| `--type-body-md` | Inter | 400 | 14px | 20px | Secondary text, table cells |
| `--type-body-sm` | Inter | 400 | 12px | 16px | Captions, metadata, timestamps |
| `--type-label` | Inter | 500 | 14px | 20px | Form labels, chip text |
| `--type-button` | Inter | 500 | 14px | 20px | Button text |
| `--type-code` | JetBrains Mono | 400 | 13px | 18px | Appointment IDs, codes |

### 3.3 Spacing Scale

| Token | Value | Usage |
|-------|-------|-------|
| `--space-1` | 4px | Tight inline spacing (icon-to-text) |
| `--space-2` | 8px | Compact element padding |
| `--space-3` | 12px | Input internal padding |
| `--space-4` | 16px | Standard element gap |
| `--space-5` | 20px | Card internal padding |
| `--space-6` | 24px | Section spacing |
| `--space-8` | 32px | Major section dividers |
| `--space-10` | 40px | Page-level margins |
| `--space-12` | 48px | Hero section padding |
| `--space-16` | 64px | Page header height |

### 3.4 Border Radius

| Token | Value | Usage |
|-------|-------|-------|
| `--radius-sm` | 4px | Chips, badges, small buttons |
| `--radius-md` | 8px | Cards, inputs, standard buttons |
| `--radius-lg` | 12px | Modals, dialogs, panels |
| `--radius-xl` | 16px | Hero cards, featured sections |
| `--radius-full` | 9999px | Avatars, circular buttons |

### 3.5 Elevation (Shadow)

| Token | Value | Usage |
|-------|-------|-------|
| `--shadow-sm` | `0 1px 2px rgba(0,0,0,0.05)` | Cards at rest |
| `--shadow-md` | `0 4px 6px -1px rgba(0,0,0,0.1)` | Elevated cards, dropdowns |
| `--shadow-lg` | `0 10px 15px -3px rgba(0,0,0,0.1)` | Modals, popovers |
| `--shadow-xl` | `0 20px 25px -5px rgba(0,0,0,0.1)` | Full-screen overlays |

### 3.6 Breakpoints

| Token | Value | Behavior |
|-------|-------|----------|
| `--bp-mobile` | 320px–767px | Single column; bottom nav; simplified views |
| `--bp-tablet` | 768px–1023px | Two columns; side nav collapses; calendar adapts |
| `--bp-desktop` | 1024px–1439px | Full layout; side nav expanded; multi-panel views |
| `--bp-wide` | 1440px+ | Max-width container (1280px); centered content |

### 3.7 Animation Tokens

| Token | Value | Usage |
|-------|-------|-------|
| `--duration-fast` | 150ms | Hover states, tooltips |
| `--duration-normal` | 250ms | Page transitions, drawer open |
| `--duration-slow` | 400ms | Modal enter/exit, skeleton fade |
| `--easing-default` | `cubic-bezier(0.4, 0, 0.2, 1)` | Standard motion |
| `--easing-enter` | `cubic-bezier(0, 0, 0.2, 1)` | Elements entering view |
| `--easing-exit` | `cubic-bezier(0.4, 0, 1, 1)` | Elements leaving view |

---

## 4. Screen Inventory

### 4.1 Authentication Screens

#### SCR-AUTH-001: Login

| Property | Value |
|----------|-------|
| **Route** | `/login` |
| **Role** | Public |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- Centered card (max-width 400px) on neutral-50 background
- Logo + app name at top
- Email field (required, email format validation)
- Password field (required, show/hide toggle)
- "Remember me" checkbox
- Primary CTA: "Sign In" (full width)
- Secondary link: "Forgot password?"
- Tertiary link: "Don't have an account? Register"

**States:**

| State | Behavior |
|-------|----------|
| Default | Empty fields, CTA disabled until valid |
| Loading | CTA shows spinner, fields disabled |
| Error (invalid credentials) | Red alert banner: "Invalid email or password" |
| Error (locked) | Red alert: "Account locked. Try again in X minutes" |
| Error (network) | Red alert: "Unable to connect. Check your connection" |
| Success | Redirect to `/dashboard` |

**Validation:**

- Email: required, valid email format
- Password: required, min 1 character (no policy enforcement on login)

---

#### SCR-AUTH-002: Registration

| Property | Value |
|----------|-------|
| **Route** | `/register` |
| **Role** | Public |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- Centered card (max-width 480px)
- Logo + "Create your account"
- Fields: First Name, Last Name, Email, Phone, Password, Confirm Password
- Password strength indicator (bar with color gradient)
- Password requirements checklist (inline, updates in real-time)
- Primary CTA: "Create Account"
- Link: "Already have an account? Sign In"

**States:**

| State | Behavior |
|-------|----------|
| Default | Empty fields, live validation on blur |
| Field error | Red border + inline error message below field |
| Password mismatch | "Passwords do not match" below confirm field |
| Duplicate email | "An account with this email already exists" |
| Loading | CTA spinner, fields disabled |
| Success | Redirect to login with "Account created" success toast |

**Validation Rules:**

- First Name: required, 2–50 chars
- Last Name: required, 2–50 chars
- Email: required, valid format, unique
- Phone: required, digits only (10–15 chars)
- Password: min 12, 1 uppercase, 1 lowercase, 1 digit, 1 special
- Confirm Password: must match Password

---

### 4.2 Patient Screens

#### SCR-PAT-001: Patient Dashboard

| Property | Value |
|----------|-------|
| **Route** | `/dashboard` |
| **Role** | Patient |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- Greeting: "Good morning, {firstName}" with date
- Section 1: "Upcoming Appointments" (max 3 cards)
  - Each card: Provider name, date, time, status badge, action buttons
  - Empty state: "No upcoming appointments" + "Book Now" CTA
- Section 2: "Pending Intake" (conditional CTA)
  - Shown only if upcoming appointment has incomplete intake
  - Card: "Complete your pre-visit intake" + appointment info + "Start Intake" button
- Section 3: "Recent Activity" (notification feed, last 5)
  - Each item: icon, message, timestamp, read/unread dot
- Quick Actions Bar: "Book Appointment", "Upload Document", "View Calendar"

**States:**

| State | Behavior |
|-------|----------|
| Loading | 3 skeleton cards + notification skeleton |
| Empty (new user) | Welcome illustration + "Book your first appointment" CTA |
| With data | Populated sections |
| Notification badge | Bell icon in header with count |

---

#### SCR-PAT-002: Provider Selection (Booking Step 1)

| Property | Value |
|----------|-------|
| **Route** | `/appointments/book` |
| **Role** | Patient |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- Page title: "Book an Appointment"
- Stepper: Step 1 (Provider) → Step 2 (Date & Time) → Step 3 (Confirm)
- Filter bar: Specialty dropdown, Provider name search
- Provider cards grid (2 cols desktop, 1 col mobile):
  - Avatar placeholder (initials)
  - Provider name
  - Specialty badge
  - "Next available: {date}"
  - "Select" button

**States:**

| State | Behavior |
|-------|----------|
| Loading | Skeleton grid (6 cards) |
| Filtered (no results) | "No providers found for {specialty}" + clear filter |
| Provider selected | Card highlighted with checkmark; "Continue" button activates |

---

#### SCR-PAT-003: Date & Slot Picker (Booking Step 2)

| Property | Value |
|----------|-------|
| **Route** | `/appointments/book` (step 2) |
| **Role** | Patient |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- Selected provider card (compact, with "Change" link)
- Calendar (month view): days with available slots highlighted
- Time slot grid below calendar: available slots as clickable chips
  - Format: "9:00 AM", "9:30 AM", etc.
  - Available: outlined chip
  - Selected: filled primary chip
  - Unavailable: disabled/hidden
- Visit reason textarea (max 500 chars, optional)
- "Back" and "Continue" buttons

**States:**

| State | Behavior |
|-------|----------|
| Date selected, loading slots | Skeleton chips below calendar |
| No slots for date | "No slots available on {date}. Try another day." |
| Slot selected | Chip filled; Continue activates |
| All dates full (month) | Days not clickable; "No availability this month" |

---

#### SCR-PAT-004: Booking Confirmation (Booking Step 3)

| Property | Value |
|----------|-------|
| **Route** | `/appointments/book` (step 3) |
| **Role** | Patient |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- Summary card:
  - Provider: name + specialty
  - Date: formatted date
  - Time: start–end
  - Visit reason: (if provided)
- "Confirm Booking" primary CTA
- "Back" secondary button
- Terms text: "You'll receive a confirmation email"

**States:**

| State | Behavior |
|-------|----------|
| Confirming | CTA spinner; "Booking your appointment..." |
| Success | Green checkmark animation → confirmation card with: appointment ID, "Add to Calendar" (ICS), "View Appointment", "Book Another" |
| Conflict detected | Yellow warning: "You have another appointment at {time}" + options |
| Slot taken (race condition) | Red alert: "This slot is no longer available. Please select another." → back to step 2 |

---

#### SCR-PAT-005: My Appointments

| Property | Value |
|----------|-------|
| **Route** | `/appointments` |
| **Role** | Patient |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- Tab bar: "Upcoming" | "Past" | "Cancelled"
- Appointment list (card per appointment):
  - Provider name + avatar
  - Date + time
  - Status badge (colored)
  - Actions (contextual): Cancel, Reschedule, Swap Slot, Complete Intake
- Empty states per tab

**States:**

| State | Behavior |
|-------|----------|
| Loading | Skeleton list (4 items) |
| Upcoming empty | "No upcoming appointments" + "Book Now" |
| Past empty | "No past appointments yet" |
| Cancel dialog | Confirmation modal with reason dropdown |

---

#### SCR-PAT-006: Intake — Chat Mode

| Property | Value |
|----------|-------|
| **Route** | `/intake/:appointmentId` |
| **Role** | Patient |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- Header: Appointment info (provider, date) + mode toggle (Chat / Form)
- Chat area (scrollable):
  - AI messages: left-aligned bubble (neutral bg)
  - Patient messages: right-aligned bubble (primary bg, white text)
  - Typing indicator: animated dots in AI bubble
- Quick-reply chips (contextual suggestions)
- Input area: text field + send button
- Progress indicator: "Step 3 of 6: Medications"
- "Save & Continue Later" link in header

**States:**

| State | Behavior |
|-------|----------|
| Initial | AI greeting: "Let's get your pre-visit info ready..." |
| Waiting for response | Input enabled, no typing indicator |
| AI processing | Typing indicator shown, input temporarily disabled |
| Quick replies available | Chip row above input |
| Network error | "Message not sent" with retry icon |
| Complete | Summary view with "Submit" and "Edit" options |

---

#### SCR-PAT-007: Intake — Form Mode

| Property | Value |
|----------|-------|
| **Route** | `/intake/:appointmentId` (form) |
| **Role** | Patient |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- Header: same as chat mode with toggle
- Multi-step wizard (stepper):
  - Step 1: Chief Complaint (textarea)
  - Step 2: Symptoms (checkboxes + free text, severity slider 1–10)
  - Step 3: Medications (autocomplete list, add custom)
  - Step 4: Allergies (autocomplete, "None known" checkbox)
  - Step 5: Medical History (checkbox list of common conditions)
  - Step 6: Review & Submit
- Progress bar at top
- "Previous" / "Next" navigation at bottom
- "Save Draft" button

**States:**

| State | Behavior |
|-------|----------|
| Step incomplete | "Next" disabled; required fields highlighted |
| Draft saved | Toast: "Draft saved" |
| Review step | Collapsible sections with "Edit" links |
| Submitting | Spinner on "Submit" button |
| Submitted | Success page + "Return to appointment" link |

---

#### SCR-PAT-008: Document Upload

| Property | Value |
|----------|-------|
| **Route** | `/documents` (upload mode) |
| **Role** | Patient |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- Drop zone: dashed border area, icon + "Drag files here or click to browse"
- Accepted formats note: "PDF, PNG, JPG, TIFF — Max 10 MB"
- Upload queue: list of files being uploaded
  - Filename, size, progress bar, cancel button
- Uploaded documents list below

**States:**

| State | Behavior |
|-------|----------|
| Empty | Drop zone prominent |
| Drag hover | Drop zone highlighted (primary border) |
| Uploading | Progress bars per file |
| Upload success | Green check + "Processing..." status |
| Upload error | Red X + error message + "Retry" |
| Invalid file | Inline error: "Unsupported format" or "File too large" |

---

#### SCR-PAT-009: Document List

| Property | Value |
|----------|-------|
| **Route** | `/documents` |
| **Role** | Patient |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- "Upload Document" button (top right)
- Document table/list:
  - Filename (clickable → viewer)
  - Upload date
  - Status badge (Uploaded / Processing / Processed / Failed)
  - File size
  - Actions: Download, Delete (with confirmation)
- Sort: by date (default newest first)
- Empty state: "No documents uploaded yet" + "Upload" CTA

---

#### SCR-PAT-010: Slot Swap Browser

| Property | Value |
|----------|-------|
| **Route** | `/appointments/:id/swap` |
| **Role** | Patient |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- Current appointment card (your slot info)
- "Available Swap Slots" list:
  - Time only (no patient names)
  - Date
  - "Request Swap" button per slot
- Swap confirmation dialog: "Offer your {time} for {selected_time}?"
- Pending swap requests section (if any active)

**States:**

| State | Behavior |
|-------|----------|
| Loading | Skeleton list |
| No swaps available | "No swap options available at this time" |
| Request sent | Toast: "Swap request sent" + status: Pending |
| Request expired | "This swap request has expired" |

---

### 4.3 Staff Screens

#### SCR-STF-001: Staff Dashboard / Queue

| Property | Value |
|----------|-------|
| **Route** | `/staff/queue` |
| **Role** | Staff |
| **Breakpoints** | Tablet, Desktop |

**Layout:**

- Provider selector: dropdown or tab strip (if few providers)
- Queue summary bar: "{X} waiting | {Y} in progress | {Z} remaining"
- Queue list (real-time via SignalR):
  - Patient name (clickable → patient detail)
  - Appointment time
  - Status badge (color-coded)
  - Wait time (since arrival)
  - Visit reason (truncated)
  - Actions: Mark Arrived → In Progress → Complete
- Late arrival flag (orange indicator if >15 min past slot)
- Walk-in section at bottom (separated)

**States:**

| State | Behavior |
|-------|----------|
| Loading | Skeleton queue (5 rows) |
| Empty queue | "No patients in queue for today" |
| Real-time update | Smooth insert/reorder animation when new patient arrives |
| SignalR disconnect | Yellow banner: "Connection lost. Reconnecting..." |
| New arrival | Subtle highlight animation on new entry |

---

#### SCR-STF-002: Walk-in Registration

| Property | Value |
|----------|-------|
| **Route** | `/staff/walkin` |
| **Role** | Staff |
| **Breakpoints** | Tablet, Desktop |

**Layout:**

- Patient search field (search by name or phone)
- Search results (if patient exists) with "Select" button
- "Create New Patient" section (if not found):
  - First Name, Last Name, Phone, DOB (minimum fields)
- Provider assignment dropdown
- Visit reason (required)
- "Register Walk-in" CTA

**States:**

| State | Behavior |
|-------|----------|
| Patient found | Autofill patient info; provider dropdown shown |
| Patient not found | "No patient found. Create a new record?" |
| Registering | CTA spinner |
| Success | Toast: "Walk-in registered. Position: #{n}" |

---

#### SCR-STF-003: Multi-Provider Calendar

| Property | Value |
|----------|-------|
| **Route** | `/staff/schedule` |
| **Role** | Staff |
| **Breakpoints** | Desktop (primarily) |

**Layout:**

- Date picker (single day)
- Provider filter checkboxes (show/hide columns)
- Day grid: columns per provider, rows per 15-min interval (8 AM–6 PM)
- Appointment blocks span their duration within the grid
- Available slots: clickable empty cells → quick-book dialog
- Blocked/unavailable: hatched pattern overlay
- Print button for daily schedule

**States:**

| State | Behavior |
|-------|----------|
| Loading | Skeleton grid |
| Provider unavailable | Full-column hatched overlay + "Not Available" label |
| Quick-book dialog | Mini modal: patient search + confirm |
| Drag reassign | Ghost block follows cursor; valid drop zones highlight |

---

#### SCR-STF-004: Patient 360-Degree View

| Property | Value |
|----------|-------|
| **Route** | `/staff/patients/:id` |
| **Role** | Staff |
| **Breakpoints** | Desktop |

**Layout:**

- Patient header: Name, DOB, phone, email, insurance status badge
- Tab navigation:
  - **Overview**: consolidated clinical summary
  - **Documents**: uploaded docs with NER status
  - **Appointments**: full history
  - **Intake Records**: submitted intakes
  - **Conflicts**: unresolved data conflicts
  - **Codes**: assigned ICD-10/CPT codes
- Overview tab:
  - Cards: Medications, Diagnoses, Allergies, Vitals, Procedures
  - Each card: list of items with source doc link + confidence badge
  - Conflict indicator (red dot) on cards with unresolved conflicts
- Conflict count badge on Conflicts tab

**States:**

| State | Behavior |
|-------|----------|
| Loading | Skeleton cards per section |
| No documents processed | "No clinical data available. Documents pending processing." |
| Conflicts present | Red badge on tab; conflict cards in overview |
| All resolved | Green check on Conflicts tab |

---

#### SCR-STF-005: Document Viewer with NER Highlights

| Property | Value |
|----------|-------|
| **Route** | `/documents/:id` (staff view) |
| **Role** | Staff |
| **Breakpoints** | Desktop |

**Layout:**

- Split view (resizable):
  - Left: Original document (PDF viewer / image display)
  - Right: Extracted text with entity highlights
- Entity legend (toggleable per type): color swatches + checkboxes
- Entity summary panel (collapsible sidebar):
  - Grouped by type (Diagnosis, Medication, etc.)
  - Each entity: text, confidence badge, code link (if mapped)
- Click entity in text → tooltip: type, confidence %, source page
- Entity navigation: "Previous Entity" / "Next Entity" (keyboard: ← →)

**States:**

| State | Behavior |
|-------|----------|
| Processing | "Document is being analyzed..." with progress |
| Processed | Full highlights visible |
| Processing failed | "Unable to process document" + retry button |
| No entities found | Text shown without highlights + "No entities detected" |
| Low confidence entity | Dashed underline vs solid for high confidence |

---

#### SCR-STF-006: Conflict Resolution Interface

| Property | Value |
|----------|-------|
| **Route** | `/staff/patients/:id/conflicts` |
| **Role** | Staff |
| **Breakpoints** | Desktop |

**Layout:**

- Conflict queue: list of unresolved conflicts
- Each conflict card:
  - Field name (e.g., "Current Medications")
  - Value A: source doc A name + extracted value
  - Value B: source doc B name + extracted value
  - Severity badge: Critical (red), Warning (amber), Info (blue)
  - Actions: "Accept A", "Accept B", "Enter Custom Value"
- Resolution confirmation dialog with optional notes
- Resolution audit log below (who resolved what, when)

**States:**

| State | Behavior |
|-------|----------|
| Conflicts pending | Queue populated; critical items pinned to top |
| Resolving | Inline loading on selected conflict |
| All resolved | "All conflicts resolved" success illustration |

---

#### SCR-STF-007: Medical Coding Review

| Property | Value |
|----------|-------|
| **Route** | `/staff/coding/:patientId` |
| **Role** | Staff |
| **Breakpoints** | Desktop |

**Layout:**

- Patient header (compact)
- Two-column layout:
  - Left: Extracted clinical data (grouped by category)
  - Right: Suggested codes
- Per code entry:
  - Code (ICD-10 or CPT): `E11.9`
  - Description: "Type 2 Diabetes Mellitus"
  - Confidence: progress bar (0–100%) + value
  - Source entity highlighted in left column
  - Actions: "Verify" (green check), "Reject" (red X), "Edit Code"
- Summary bar: "12 codes suggested | 8 verified | 2 rejected | 2 pending"
- "Submit All Verified" button when all reviewed

**States:**

| State | Behavior |
|-------|----------|
| Pending review | All codes in "unreviewed" state |
| Partially reviewed | Progress reflected in summary bar |
| Low confidence (<70%) | Amber border; "Requires Review" label |
| All verified | "Submit" button activated |
| Code rejected | Strikethrough + red bg; moves to rejected section |

---

### 4.4 Admin Screens

#### SCR-ADM-001: User Management

| Property | Value |
|----------|-------|
| **Route** | `/admin/users` |
| **Role** | Admin |
| **Breakpoints** | Desktop |

**Layout:**

- "Create User" button (top right)
- Filter bar: Role dropdown, Status (Active/Inactive), Search by name/email
- User table:
  - Name | Email | Role | Status | Last Login | Actions
- Actions: Edit, Deactivate/Activate, Unlock (if locked), Reset Password
- Pagination (25 per page)

**States:**

| State | Behavior |
|-------|----------|
| Loading | Skeleton table |
| Filtered (no results) | "No users match your filters" |
| Deactivate confirm | Modal: "Deactivate {name}? This will immediately revoke access." |
| Unlock confirm | "Unlock {name}? Failed login counter will reset." |

---

#### SCR-ADM-002: Audit Log Viewer

| Property | Value |
|----------|-------|
| **Route** | `/admin/audit` |
| **Role** | Admin |
| **Breakpoints** | Desktop |

**Layout:**

- Filter bar:
  - Date range picker (from–to)
  - Action type dropdown (Login, DataAccess, DataModify, UserAdmin, etc.)
  - User search (by name/email)
  - Entity type filter
- Log table:
  - Timestamp | User | Action | Entity | Details (expandable) | IP
- Row expansion: full JSON details view
- Export button: "Download CSV" (filtered results)
- Pagination (50 per page)
- No edit/delete actions available (read-only by design)

**States:**

| State | Behavior |
|-------|----------|
| Loading | Skeleton rows |
| Filtered (no results) | "No audit entries match your criteria" |
| Large result set | "Showing 1–50 of 12,345 entries" + pagination |
| Export in progress | Spinner on export button |

---

#### SCR-ADM-003: Provider Schedule Configuration

| Property | Value |
|----------|-------|
| **Route** | `/admin/providers/:id/schedule` |
| **Role** | Admin |
| **Breakpoints** | Desktop |

**Layout:**

- Provider info header (name, specialty)
- Weekly schedule grid: rows per day (Mon–Sun), columns for start/end time
- Each day: toggle active/inactive + time inputs (start, end, slot duration)
- Exceptions section: "Add Date Override" (specific date → unavailable or custom hours)
- Preview: generated slots for next 7 days (read-only list)
- "Save Schedule" button

**States:**

| State | Behavior |
|-------|----------|
| Editing | Unsaved changes indicator; "Save" activates |
| Saved | Toast: "Schedule saved. Slots regenerated." |
| Conflict (booked slots) | Warning: "3 existing bookings conflict with this change" |

---

### 4.5 Shared/Common Screens

#### SCR-COM-001: Calendar View

| Property | Value |
|----------|-------|
| **Route** | `/calendar` |
| **Role** | All |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- View toggle: Month | Week | Day
- Navigation: ← Previous | Today (button) | Next →
- Month view: grid with appointment dots (colored by status)
- Week view: time grid (columns per day) with appointment blocks
- Day view: detailed list with full appointment info
- Click appointment → popover with details + actions

**States:**

| State | Behavior |
|-------|----------|
| Loading | Skeleton grid |
| No appointments (month) | Empty grid (navigable) |
| Day with >3 appointments | "+{n} more" link → expands |
| Mobile month view | Compact dots; tap day → day list below |

---

#### SCR-COM-002: Notification Center

| Property | Value |
|----------|-------|
| **Route** | `/notifications` |
| **Role** | All |
| **Breakpoints** | Mobile, Tablet, Desktop |

**Layout:**

- Notification list (full page):
  - Icon (by type: appointment, swap, reminder, system)
  - Title + message text
  - Timestamp (relative: "2 hours ago")
  - Read/unread indicator (dot)
  - Action link (if applicable)
- "Mark All as Read" button (top right)
- Filter: All | Unread
- Empty state: "You're all caught up!"

**States:**

| State | Behavior |
|-------|----------|
| Loading | Skeleton items |
| New notification | Top of list; optional toast popup |
| Click notification | Marks as read; navigates to related screen |
| Empty | Illustration + "No notifications" |

---

## 5. Interaction Flows

### 5.1 Booking Flow (Happy Path)

```
[Dashboard] → "Book Appointment"
    ↓
[Provider Selection] → Select provider → "Continue"
    ↓
[Date & Slot Picker] → Pick date → Pick slot → "Continue"
    ↓
[Confirmation] → "Confirm Booking"
    ↓
[Success] → Actions: "Add to Calendar" | "View Appointment" | "Book Another"
```

### 5.2 Intake Flow (Chat → Form Switch)

```
[Appointment Detail] → "Complete Intake"
    ↓
[Intake Chat Mode] → Answer questions → (optional: tap "Form" toggle)
    ↓
[Intake Form Mode] → Pre-filled with chat data → Complete remaining → Submit
    ↓
[Intake Summary] → Review → "Submit"
    ↓
[Confirmation] → "Return to Appointment"
```

### 5.3 Document Processing Flow (User Perspective)

```
[Documents] → "Upload" → Drop/select file
    ↓
[Upload Progress] → Progress bar → Upload complete
    ↓
[Document List] → Status: "Processing..." (auto-refresh)
    ↓
[Document List] → Status: "Processed" → Click to view
    ↓
[Document Viewer] → Highlights visible → Entity summary panel
```

### 5.4 Walk-in Registration Flow

```
[Staff Dashboard] → "Register Walk-in"
    ↓
[Walk-in Form] → Search patient (found/not found)
    ↓ (if found)
[Patient Selected] → Assign provider → "Register"
    ↓ (if not found)
[Create Patient] → Name + Phone + DOB → Assign provider → "Register"
    ↓
[Queue Updated] → Toast: "Position #{n}" → Back to queue
```

### 5.5 Conflict Resolution Flow

```
[Patient 360 View] → Red badge on Conflicts tab → Click tab
    ↓
[Conflict Queue] → Select conflict (Critical first)
    ↓
[Conflict Detail] → Compare Value A vs Value B (with source docs)
    ↓
[Resolution Action] → Accept A | Accept B | Custom → Confirm
    ↓
[Resolution Logged] → Next conflict or "All resolved" state
```

### 5.6 Slot Swap Flow (Complete Cycle)

```
[Patient A: Appointment Detail] → "Swap Slot"
    ↓
[Swap Browser] → View available times → "Request Swap" on desired slot
    ↓
[Patient B: Notification] → "New swap request" → Click
    ↓
[Swap Request Detail] → "Your slot [Time B] for [Time A]" → Accept/Decline
    ↓ (Accept)
[Both Patients: Confirmation email] → Calendar updated → Appointment details updated
```

### 5.7 Medical Coding Flow

```
[Staff: Coding Queue] → Select patient with pending codes
    ↓
[Code Review] → Clinical data (left) ↔ Suggested codes (right)
    ↓
[Per Code] → Review confidence → Verify / Reject / Edit
    ↓
[All Reviewed] → "Submit All Verified"
    ↓
[Confirmed] → Codes assigned → Audit logged → Next patient
```

---

## 6. Component Library Specification

### 6.1 Buttons

| Variant | Usage | Style |
|---------|-------|-------|
| Primary | Main actions (Submit, Confirm, Book) | Filled primary-500; white text; radius-md |
| Secondary | Alternative actions (Back, Cancel dialog) | Outlined primary-500 border; primary text |
| Tertiary | Low-emphasis (Learn more, Change) | No border; primary text; underline on hover |
| Danger | Destructive (Delete, Deactivate) | Filled error-500; white text |
| Ghost | Inline actions (Edit, Retry) | No bg; neutral-700 text; hover: neutral-50 bg |

**Button Sizes:**

| Size | Height | Padding | Font |
|------|--------|---------|------|
| sm | 32px | 12px 16px | type-body-sm |
| md | 40px | 12px 20px | type-button |
| lg | 48px | 16px 24px | type-button |

**Button States:** Default → Hover → Active → Focused → Disabled → Loading

### 6.2 Form Inputs

| Component | Description |
|-----------|-------------|
| Text Input | Label (above), input field, helper/error text below |
| Textarea | Same as text input, resizable vertically |
| Select/Dropdown | Native select or custom dropdown with search |
| Date Picker | Calendar popup, input with date format mask |
| Time Picker | 12-hour format with AM/PM toggle |
| Autocomplete | Text input + suggestions dropdown (debounced search) |
| Checkbox | Square check, label right |
| Radio Group | Circle select, vertical list |
| Toggle Switch | On/Off binary; label left |
| Slider | Range input with value label (used for severity 1–10) |

**Input States:** Default → Focused → Filled → Error → Disabled → Read-only

### 6.3 Data Display

| Component | Description |
|-----------|-------------|
| Status Badge | Pill-shaped; colored per status map |
| Avatar | Circle; initials or image; sizes: 24/32/40/56px |
| Card | White bg; shadow-sm; radius-md; padding space-5 |
| Table | Header row (neutral-50 bg); alternating row bg (optional); sortable columns |
| List Item | Icon + primary text + secondary text + action |
| Tooltip | Dark bg; white text; max 240px; arrow pointer |
| Progress Bar | Thin bar with fill percentage; colored by context |
| Skeleton | Animated pulse; matches shape of loading content |
| Empty State | Illustration + heading + description + CTA |

### 6.4 Navigation

| Component | Description |
|-----------|-------------|
| Top App Bar | Logo left; nav center (desktop); avatar/menu right; notification bell |
| Side Navigation | Desktop: 240px expanded / 64px collapsed; icons + labels |
| Bottom Nav | Mobile only: 5 icons max; active state fill |
| Breadcrumbs | Page hierarchy (Home > Section > Page); clickable ancestors |
| Tabs | Horizontal tab bar; active underline; badge support |
| Stepper | Horizontal (desktop) or vertical (mobile); numbered steps; states: complete/active/upcoming |

### 6.5 Feedback

| Component | Description |
|-----------|-------------|
| Toast | Top-right; auto-dismiss 5s; types: success/error/warning/info |
| Alert Banner | Full-width; dismissable; above content; colored by type |
| Modal Dialog | Centered; backdrop; title + content + footer actions |
| Confirmation Dialog | Modal subset: warning icon + message + Confirm/Cancel |
| Loading Spinner | Circular; primary color; sizes: 16/24/32px |
| Progress Indicator | Linear or circular; determinate or indeterminate |

### 6.6 Chat Components

| Component | Description |
|-----------|-------------|
| Chat Bubble (AI) | Left-aligned; neutral-100 bg; radius-lg (flat bottom-left corner) |
| Chat Bubble (User) | Right-aligned; primary-500 bg; white text; radius-lg (flat bottom-right) |
| Typing Indicator | 3 animated dots in AI bubble shape |
| Quick Reply Chip | Outlined; clickable; disappears after selection |
| Message Input | Full-width; auto-grow textarea; send button right |

### 6.7 Calendar Components

| Component | Description |
|-----------|-------------|
| Month Grid | 7 columns (days); 5–6 rows; day number + event dots |
| Week Grid | 7 columns; time rows (15 or 30 min); event blocks |
| Day Timeline | Single column; time labels left; event blocks right |
| Event Block | Colored by status; truncated title; height = duration |
| Slot Chip | Compact pill for time selection; outlined/filled states |

---

## 7. Responsive Design Strategy

### 7.1 Layout Grids

| Breakpoint | Columns | Gutter | Margin | Max Content Width |
|------------|---------|--------|--------|-------------------|
| Mobile (320–767px) | 4 | 16px | 16px | 100% |
| Tablet (768–1023px) | 8 | 24px | 32px | 100% |
| Desktop (1024–1439px) | 12 | 24px | 40px | 1200px |
| Wide (1440px+) | 12 | 32px | auto | 1280px |

### 7.2 Responsive Adaptations

| Pattern | Mobile | Tablet | Desktop |
|---------|--------|--------|---------|
| Navigation | Bottom nav (5 items) | Side nav (collapsed) | Side nav (expanded) |
| Provider cards | 1 column | 2 columns | 3 columns |
| Calendar | Day list view | Week view | Month/Multi-provider |
| Document viewer | Stacked (text below doc) | Stacked | Side-by-side |
| Queue dashboard | Not supported | Simplified list | Full dashboard |
| Data tables | Card list (stacked fields) | Responsive table | Full table |
| Modals | Full-screen sheet (bottom) | Centered modal | Centered modal |
| Forms | Single column | Single column | Two-column for long forms |

---

## 8. Accessibility Requirements

### 8.1 WCAG 2.1 AA Compliance

| Criterion | Requirement | Implementation |
|-----------|-------------|----------------|
| 1.1.1 | Text alternatives for non-text content | All icons have aria-label; images have alt text |
| 1.3.1 | Info and relationships | Semantic HTML (headings, landmarks, lists); ARIA roles for custom components |
| 1.4.3 | Contrast minimum (4.5:1 text, 3:1 large text) | All token combinations validated |
| 1.4.11 | Non-text contrast (3:1 for UI components) | Focus rings, borders, icons meet ratio |
| 2.1.1 | Keyboard accessible | All interactive elements focusable; logical tab order |
| 2.1.2 | No keyboard trap | Modals trap focus but Escape exits |
| 2.4.3 | Focus order | Matches visual order; skip navigation link |
| 2.4.7 | Focus visible | 2px primary-500 outline; 2px offset |
| 3.3.1 | Error identification | Errors announced via aria-live; linked to fields |
| 3.3.2 | Labels or instructions | All inputs have visible labels; placeholders supplement (not replace) |
| 4.1.2 | Name, role, value | Custom components expose correct ARIA properties |

### 8.2 Screen Reader Considerations

| Feature | Implementation |
|---------|----------------|
| Form errors | `aria-invalid="true"` + `aria-describedby` linking to error message |
| Live updates (queue) | `aria-live="polite"` region for status changes |
| Toasts | `role="alert"` for errors; `role="status"` for success |
| Modal focus | Focus trapped; announced with `aria-modal="true"` |
| Loading states | `aria-busy="true"` on loading containers |
| Status badges | Visually hidden text supplements color (e.g., "Status: Scheduled") |
| Entity highlights | `role="mark"` with `aria-label` for entity type |
| Chat messages | `role="log"` on chat container; `aria-label` on each message |

### 8.3 Keyboard Shortcuts (Staff)

| Key | Action | Context |
|-----|--------|---------|
| `N` | New walk-in | Queue dashboard |
| `A` | Mark as arrived | Selected appointment |
| `→` / `←` | Next/Previous entity | Document viewer |
| `Enter` | Verify code | Code review (selected row) |
| `Escape` | Close modal/panel | Any modal |
| `/` | Focus search | Any screen with search |

---

## 9. Error States & Edge Cases

### 9.1 Global Error States

| Error | Display | Recovery |
|-------|---------|----------|
| Network offline | Persistent top banner: "You're offline" | Auto-dismiss on reconnect |
| API 500 | Error page or inline alert | "Try Again" button; contact support link |
| Session expired | Modal overlay: "Session expired" | "Sign In Again" button → login |
| Unauthorized (403) | Inline alert: "You don't have permission" | No recovery; suggest contact admin |
| Rate limited (429) | Toast: "Too many requests. Wait a moment." | Auto-retry after delay |

### 9.2 Feature-Specific Edge Cases

| Feature | Edge Case | Design Response |
|---------|-----------|-----------------|
| Booking | Last slot taken during selection | Alert: "Slot no longer available" → back to slot picker |
| Booking | Conflict with existing appointment | Warning modal with conflict details + override option (staff only) |
| Intake | AI response timeout | "Taking longer than usual..." → after 10s, offer form fallback |
| Document | Processing stuck >5 min | Status: "Processing (delayed)" → after 15 min, "Failed" with retry |
| Swap | Both patients respond simultaneously | First response wins; second gets "Request already resolved" |
| Queue | Patient arrives after auto-no-show | Staff can override: NoShow → Arrived |
| Calendar | No availability for 90 days | "No available slots in the next 90 days" + waitlist suggestion |

---

## 10. Screen Traceability Matrix

| Screen ID | Epic | User Stories | Functional Requirements |
|-----------|------|--------------|------------------------|
| SCR-AUTH-001 | EP-001 | US-014, US-018 | FR-005 |
| SCR-AUTH-002 | EP-001 | US-013, US-018 | FR-001 |
| SCR-PAT-001 | EP-002 | US-027 | FR-007, FR-022 |
| SCR-PAT-002 | EP-002 | US-027 | FR-006, FR-007 |
| SCR-PAT-003 | EP-002 | US-020, US-027 | FR-006, FR-008 |
| SCR-PAT-004 | EP-002 | US-020, US-025, US-027 | FR-007, FR-009, FR-015 |
| SCR-PAT-005 | EP-002 | US-022, US-027 | FR-014 |
| SCR-PAT-006 | EP-006 | US-040, US-044 | FR-027, FR-029 |
| SCR-PAT-007 | EP-006 | US-041, US-044 | FR-028, FR-029, FR-030 |
| SCR-PAT-008 | EP-007 | US-045 | FR-032 |
| SCR-PAT-009 | EP-007 | US-049 | FR-032, FR-036 |
| SCR-PAT-010 | EP-003 | US-028, US-031 | FR-019 |
| SCR-STF-001 | EP-002 | US-023, US-024 | FR-010, FR-011 |
| SCR-STF-002 | EP-002 | US-021 | FR-009, FR-010 |
| SCR-STF-003 | EP-005 | US-039 | FR-013 |
| SCR-STF-004 | EP-008 | US-050–US-054 | FR-036, FR-037, FR-038 |
| SCR-STF-005 | EP-007 | US-047, US-048 | FR-034, FR-035 |
| SCR-STF-006 | EP-008 | US-050–US-054 | FR-038, FR-039 |
| SCR-STF-007 | EP-009 | US-055–US-058 | FR-041, FR-042, FR-043 |
| SCR-ADM-001 | EP-010 | US-059–US-063 | FR-049, FR-050 |
| SCR-ADM-002 | EP-010 | US-059–US-063 | FR-045, FR-046, FR-051 |
| SCR-ADM-003 | EP-002 | US-019 | FR-007 |
| SCR-COM-001 | EP-005 | US-037, US-038 | FR-022, FR-023 |
| SCR-COM-002 | EP-004 | US-034 | FR-022 |

---

## 11. Figma File Structure

### 11.1 Page Organization

```
📄 Cover Page
📄 Design Tokens
    ├── Colors (all palettes)
    ├── Typography (scale + specimens)
    ├── Spacing & Grid
    ├── Elevation & Radius
    └── Icons (24px grid)
📄 Components
    ├── Atoms (Button, Input, Badge, Avatar, Icon)
    ├── Molecules (Card, Form Field, List Item, Toast)
    ├── Organisms (Nav Bar, Side Nav, Data Table, Calendar Grid)
    └── Templates (Page Shells per role)
📄 Auth Screens
    ├── Login (Desktop, Mobile)
    └── Registration (Desktop, Mobile)
📄 Patient Portal
    ├── Dashboard (Desktop, Mobile)
    ├── Booking Flow (3 steps × 2 breakpoints)
    ├── My Appointments
    ├── Intake Chat + Form
    ├── Documents (Upload, List, Viewer)
    ├── Slot Swap
    └── Calendar
📄 Staff Portal
    ├── Queue Dashboard
    ├── Walk-in Registration
    ├── Multi-Provider Calendar
    ├── Patient 360 View
    ├── Document Viewer (NER)
    ├── Conflict Resolution
    └── Medical Coding
📄 Admin Portal
    ├── User Management
    ├── Audit Logs
    ├── Provider Schedule Config
    └── System Health
📄 Shared Components
    ├── Notification Center
    ├── Profile & Settings
    └── Error States
📄 Interaction Flows (Prototype)
    ├── Booking Flow
    ├── Intake Flow
    ├── Document Upload → View
    ├── Walk-in → Queue
    └── Conflict Resolution
```

### 11.2 Naming Conventions

| Category | Pattern | Example |
|----------|---------|---------|
| Pages | `{Number}. {Category}` | `3. Patient Portal` |
| Frames | `{ScreenID} / {Breakpoint} / {State}` | `SCR-PAT-002 / Desktop / Default` |
| Components | `.{Atom/Mol/Org}/{Name}/{Variant}` | `.Atom/Button/Primary-MD` |
| Tokens | `{Category}/{Group}/{Name}` | `Color/Primary/500` |
| Auto Layout | Named layers with semantic labels | `Header`, `Content`, `Actions` |
| Boolean props | `Show {Element}` | `Show Badge`, `Show Icon` |
| Variant props | `{Property}={Value}` | `Size=MD`, `State=Hover` |

---

## 12. Handoff Checklist

| # | Item | Status |
|---|------|--------|
| 1 | Design tokens documented and linked to CSS variables | ☐ |
| 2 | All screens have Desktop + Mobile variants | ☐ |
| 3 | Interactive prototype covers all critical flows | ☐ |
| 4 | Component states (default, hover, active, focus, disabled, error, loading) specified | ☐ |
| 5 | Spacing and padding annotated on complex layouts | ☐ |
| 6 | Color contrast checked for all text/bg combinations | ☐ |
| 7 | Touch targets ≥ 44px on mobile | ☐ |
| 8 | Focus order annotations for complex screens | ☐ |
| 9 | Loading/empty/error states designed for every data-fetching screen | ☐ |
| 10 | Animation durations and easing curves specified | ☐ |
| 11 | Icon set selected and inventory complete | ☐ |
| 12 | Responsive behavior documented for each screen | ☐ |
