# Figma Scripts Execution Guide

## File Information

| Field | Value |
|-------|-------|
| **Figma File** | [HealthPlatform - Design System & Screens](https://www.figma.com/design/ZNVfDrqzdNswBEnfbjPW9a) |
| **File Key** | `ZNVfDrqzdNswBEnfbjPW9a` |
| **Scripts Path** | `.propel/context/figma/scripts/` |

---

## Execution Order

Run scripts sequentially via Figma MCP `use_figma` tool. Each script is self-contained.

| # | Script | Screens Created | MCP Calls |
|---|--------|----------------|-----------|
| 1 | `01-auth-screens.js` | Login, Registration | 1 |
| 2 | `02-patient-dashboard-booking.js` | Patient Dashboard, Provider Selection | 1 |
| 3 | `03-patient-intake-documents.js` | Intake Chat, Document Viewer (NER) | 1 |
| 4 | `04-staff-screens.js` | Staff Queue, Medical Coding Review | 1 |
| 5 | `05-admin-patient360.js` | Patient 360, User Management, Audit Logs | 1 |

**Total MCP calls needed:** 5 (fits within next month's Starter limit of 6)

---

## How to Execute

### Option A: Via Copilot (when rate limits reset)

```
Ask: "Execute the Figma script at .propel/context/figma/scripts/01-auth-screens.js 
      against file ZNVfDrqzdNswBEnfbjPW9a"
```

The agent will read the file content and pass it to `mcp_figma_use_figma`.

### Option B: Via Figma Plugin Console

1. Open the file in Figma
2. Go to Plugins → Development → Open Console
3. Paste script content (remove the top comment block)
4. Execute

### Option C: Upgrade to Professional Plan

With a Full/Dev seat on Professional plan (200 calls/day), all 5 scripts can run in one session plus iterations for refinement.

---

## Screen Layout Map

```
Page: "2. Screens"

Row 1 (y=0):      Auth
  (0,0)           AUTH / Login
  (1600,0)        AUTH / Register

Row 2 (y=1100):   Patient Portal
  (0,1100)        PAT / Dashboard
  (1600,1100)     PAT / Book — Provider Selection
  (3200,1100)     PAT / Intake — Chat
  (4800,1100)     PAT / Document Viewer (NER)

Row 3 (y=2200):   Staff Portal
  (0,2200)        STAFF / Queue Dashboard
  (1600,2200)     STAFF / Medical Coding Review
  (3200,2200)     STAFF / Patient 360 View

Row 4 (y=3300):   Admin Portal
  (0,3300)        ADMIN / User Management
  (1600,3300)     ADMIN / Audit Logs
```

---

## Already Built (Design System page)

- Color tokens: 34 swatches (Primary, Neutral, Semantic, Status, NER)
- Typography scale: 10 levels (Inter, 11–36px)
- Spacing scale: 12 values (4px base)
- Core components: 6 buttons, 4 input states, 3 cards, 6 status badges

---

## Prototype Flows (Page 3 — Future)

After all screens are built, connect with prototype interactions:

1. **Login → Dashboard**: Login button → Patient/Staff Dashboard
2. **Dashboard → Booking**: "Book Appointment" → Provider Selection
3. **Booking Wizard**: Provider → Date/Slot → Confirmation
4. **Dashboard → Intake**: "Start Intake" → Chat Mode
5. **Staff Queue**: Status transitions (Mark Arrived → Start → Complete)
6. **Document Upload**: Upload → Processing → Viewer with NER
