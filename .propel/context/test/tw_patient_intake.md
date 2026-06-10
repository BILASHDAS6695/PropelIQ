# Test Workflow: Patient Intake

## Metadata

| Field | Value |
|-------|-------|
| **Feature** | Patient Intake |
| **Test Type** | Feature-level |
| **Requirements** | FR-027 to FR-031 |
| **Use Cases** | UC-005, UC-006 |
| **Priority** | High |
| **Test Framework** | Playwright + TypeScript |

---

## Feature Overview

This test workflow validates the patient intake system including:
- AI conversational intake mode (FR-027)
- Manual form intake mode (FR-028)
- Seamless mode switching (FR-029)
- Patient self-editing capabilities (FR-030)
- Data persistence across modes (FR-031)

---

## Page Objects Required

```yaml
pages:
  - IntakeLandingPage:
      selectors:
        aiModeButton: '[data-testid="ai-mode-button"]'
        manualModeButton: '[data-testid="manual-mode-button"]'
        modeSwitcher: '[data-testid="mode-switcher"]'
        
  - AIConversationalIntakePage:
      selectors:
        chatInterface: '[data-testid="ai-chat-interface"]'
        messageInput: '[data-testid="message-input"]'
        sendButton: '[data-testid="send-message"]'
        aiResponse: '[data-testid="ai-response"]'
        dataSummary: '[data-testid="captured-data-summary"]'
        switchToManualButton: '[data-testid="switch-to-manual"]'
        confirmButton: '[data-testid="confirm-intake"]'
        editButton: '[data-testid="edit-data"]'
        
  - ManualFormIntakePage:
      selectors:
        medicalHistoryInput: '[data-testid="medical-history"]'
        currentMedicationsInput: '[data-testid="current-medications"]'
        allergiesInput: '[data-testid="allergies"]'
        currentSymptomsInput: '[data-testid="current-symptoms"]'
        chronicConditionsCheckboxes: '[data-testid^="condition-"]'
        submitButton: '[data-testid="submit-intake"]'
        switchToAIButton: '[data-testid="switch-to-ai"]'
        validationErrors: '[data-testid="validation-errors"]'
        
  - IntakeSummaryPage:
      selectors:
        summaryContainer: '[data-testid="intake-summary"]'
        medicalHistoryDisplay: '[data-testid="summary-medical-history"]'
        medicationsDisplay: '[data-testid="summary-medications"]'
        allergiesDisplay: '[data-testid="summary-allergies"]'
        symptomsDisplay: '[data-testid="summary-symptoms"]'
        editIntakeButton: '[data-testid="edit-intake"]'
        confirmFinalButton: '[data-testid="confirm-final"]'
```

---

## Test Cases

### Happy Path

#### TW-INTAKE-001: Complete AI Conversational Intake

**Requirement**: FR-027, FR-031  
**Use Case**: UC-005  
**Priority**: Critical

```yaml
test: "Patient Completes Intake via AI Conversation"
preconditions:
  - authenticated_as: "Patient"
  - upcoming_appointment: true
  
steps:
  - step: "Navigate to intake"
    action: goto
    url: "/intake"
    
  - step: "Select AI conversational mode"
    action: click
    selector: '[data-testid="ai-mode-button"]'
    
  - step: "Wait for AI greeting"
    action: expect
    selector: '[data-testid="ai-response"]'
    assertion: toContainText
    text: "Hi! I'll help you complete your medical intake"
    
  - step: "AI asks about chronic conditions"
    action: expect
    selector: '[data-testid="ai-response"]'
    assertion: toContainText
    text: "Do you have any chronic conditions"
    
  - step: "Patient responds with conditions"
    actions:
      - fill:
          selector: '[data-testid="message-input"]'
          value: "I have type 2 diabetes and hypertension"
      - click: '[data-testid="send-message"]'
          
  - step: "Verify AI parses response"
    action: expect
    selector: '[data-testid="captured-data-summary"]'
    assertion: toContainText
    text: "Type 2 Diabetes"
    
  - step: "AI asks about current medications"
    action: expect
    selector: '[data-testid="ai-response"]'
    assertion: toContainText
    text: "What medications are you currently taking"
    
  - step: "Patient responds with medications"
    actions:
      - fill:
          selector: '[data-testid="message-input"]'
          value: "Metformin 500mg twice daily and Lisinopril 10mg once daily"
      - click: '[data-testid="send-message"]'
          
  - step: "Verify medications captured"
    actions:
      - expect:
          selector: '[data-testid="captured-data-summary"]'
          assertion: toContainText
          text: "Metformin 500mg"
      - expect:
          selector: '[data-testid="captured-data-summary"]'
          assertion: toContainText
          text: "Lisinopril 10mg"
          
  - step: "AI asks about allergies"
    action: waitForSelector
    selector: '[data-testid="ai-response"]:has-text("allergies")'
    
  - step: "Patient responds about allergies"
    actions:
      - fill:
          selector: '[data-testid="message-input"]'
          value: "I'm allergic to penicillin"
      - click: '[data-testid="send-message"]'
          
  - step: "AI asks about current symptoms"
    action: waitForSelector
    selector: '[data-testid="ai-response"]:has-text("symptoms")'
    
  - step: "Patient describes symptoms"
    actions:
      - fill:
          selector: '[data-testid="message-input"]'
          value: "I have occasional headaches and mild dizziness"
      - click: '[data-testid="send-message"]'
          
  - step: "AI presents summary for review"
    action: expect
    selector: '[data-testid="data-summary-review"]'
    assertion: toBeVisible
    
  - step: "Verify all captured data in summary"
    actions:
      - expect:
          selector: '[data-testid="summary-conditions"]'
          assertion: toContainText
          text: "Type 2 Diabetes"
      - expect:
          selector: '[data-testid="summary-medications"]'
          assertion: toContainText
          text: "Metformin"
      - expect:
          selector: '[data-testid="summary-allergies"]'
          assertion: toContainText
          text: "Penicillin"
      - expect:
          selector: '[data-testid="summary-symptoms"]'
          assertion: toContainText
          text: "headaches"
          
  - step: "Confirm intake data"
    action: click
    selector: '[data-testid="confirm-intake"]'
    
  - step: "Verify success message"
    action: expect
    selector: '[data-testid="success-message"]'
    assertion: toContainText
    text: "Intake completed successfully"

checkpoints:
  - name: "All Data Captured"
    verify: "Database has complete intake record"
  - name: "Structured Format"
    verify: "Conversational text parsed into structured fields"
```

---

#### TW-INTAKE-002: Complete Manual Form Intake

**Requirement**: FR-028, FR-031  
**Use Case**: UC-006  
**Priority**: Critical

```yaml
test: "Patient Completes Intake via Manual Form"
preconditions:
  - authenticated_as: "Patient"
  - upcoming_appointment: true
  
steps:
  - step: "Navigate to intake"
    action: goto
    url: "/intake"
    
  - step: "Select manual form mode"
    action: click
    selector: '[data-testid="manual-mode-button"]'
    
  - step: "Fill medical history"
    action: fill
    selector: '[data-testid="medical-history"]'
    value: "Type 2 Diabetes (diagnosed 2015), Hypertension (diagnosed 2018)"
    
  - step: "Fill current medications"
    action: fill
    selector: '[data-testid="current-medications"]'
    value: "Metformin 500mg twice daily, Lisinopril 10mg once daily"
    
  - step: "Fill allergies"
    action: fill
    selector: '[data-testid="allergies"]'
    value: "Penicillin"
    
  - step: "Fill current symptoms"
    action: fill
    selector: '[data-testid="current-symptoms"]'
    value: "Occasional headaches, mild dizziness"
    
  - step: "Select chronic conditions"
    actions:
      - check: '[data-testid="condition-diabetes"]'
      - check: '[data-testid="condition-hypertension"]'
          
  - step: "Submit form"
    action: click
    selector: '[data-testid="submit-intake"]'
    
  - step: "Verify success message"
    action: expect
    selector: '[data-testid="success-message"]'
    assertion: toContainText
    text: "Intake submitted successfully"

checkpoints:
  - name: "Form Data Persisted"
    verify: "All fields saved to database"
  - name: "Required Fields Validated"
    verify: "Submission only allowed when complete"
```

---

#### TW-INTAKE-003: Switch From AI to Manual Mode Mid-Process

**Requirement**: FR-029  
**Use Case**: UC-005 Alternative Flow  
**Priority**: High

```yaml
test: "Patient Switches from AI to Manual Mode"
preconditions:
  - authenticated_as: "Patient"
  
steps:
  - step: "Start AI conversational mode"
    actions:
      - goto: "/intake"
      - click: '[data-testid="ai-mode-button"]'
          
  - step: "Answer first question (conditions)"
    actions:
      - fill:
          selector: '[data-testid="message-input"]'
          value: "I have diabetes"
      - click: '[data-testid="send-message"]'
          
  - step: "Answer second question (medications)"
    actions:
      - fill:
          selector: '[data-testid="message-input"]'
          value: "Metformin 500mg"
      - click: '[data-testid="send-message"]'
          
  - step: "Verify partial data captured"
    actions:
      - expect:
          selector: '[data-testid="captured-data-summary"]'
          assertion: toContainText
          text: "Diabetes"
      - expect:
          selector: '[data-testid="captured-data-summary"]'
          assertion: toContainText
          text: "Metformin"
          
  - step: "Switch to manual mode"
    action: click
    selector: '[data-testid="switch-to-manual"]'
    
  - step: "Verify mode switched"
    action: expect
    selector: '[data-testid="manual-form-intake"]'
    assertion: toBeVisible
    
  - step: "Verify previously captured data pre-populated"
    actions:
      - expect:
          selector: '[data-testid="medical-history"]'
          assertion: toHaveValue
          value: "Diabetes"
      - expect:
          selector: '[data-testid="current-medications"]'
          assertion: toHaveValue
          value: "Metformin 500mg"
          
  - step: "Complete remaining fields manually"
    actions:
      - fill:
          selector: '[data-testid="allergies"]'
          value: "None"
      - fill:
          selector: '[data-testid="current-symptoms"]'
          value: "Fatigue"
          
  - step: "Submit form"
    action: click
    selector: '[data-testid="submit-intake"]'
    
  - step: "Verify all data saved"
    actions:
      - expect:
          selector: '[data-testid="success-message"]'
          assertion: toBeVisible
      - apiRequest:
          method: GET
          url: "/api/intake/${patient.id}"
          expect:
            body:
              medical_history: "Diabetes"
              medications: "Metformin 500mg"
              allergies: "None"
              symptoms: "Fatigue"

checkpoints:
  - name: "Seamless Mode Switch"
    verify: "No data lost during switch"
  - name: "Data Merged"
    verify: "AI-captured and manual data combined"
```

---

#### TW-INTAKE-004: Patient Edits Submitted Intake

**Requirement**: FR-030  
**Priority**: High

```yaml
test: "Patient Self-Edits Intake After Submission"
preconditions:
  - authenticated_as: "Patient"
  - completed_intake:
      medications: "Metformin 500mg twice daily"
      
steps:
  - step: "Navigate to intake summary"
    action: goto
    url: "/intake/summary"
    
  - step: "View current intake data"
    action: expect
    selector: '[data-testid="summary-medications"]'
    assertion: toContainText
    text: "Metformin 500mg"
    
  - step: "Click edit button"
    action: click
    selector: '[data-testid="edit-intake"]'
    
  - step: "Modify medication dosage"
    actions:
      - fill:
          selector: '[data-testid="current-medications"]'
          value: "Metformin 1000mg twice daily"  # Changed from 500mg
      - fill:
          selector: '[data-testid="current-medications"]'
          value: "${existing_value}, Aspirin 81mg daily"  # Added new
          
  - step: "Save changes"
    action: click
    selector: '[data-testid="save-changes"]'
    
  - step: "Verify update confirmation"
    action: expect
    selector: '[data-testid="success-message"]'
    assertion: toContainText
    text: "Intake updated successfully"
    
  - step: "Verify updated data displayed"
    actions:
      - expect:
          selector: '[data-testid="summary-medications"]'
          assertion: toContainText
          text: "Metformin 1000mg"
      - expect:
          selector: '[data-testid="summary-medications"]'
          assertion: toContainText
          text: "Aspirin 81mg"
          
  - step: "Verify no staff intervention required"
    action: apiRequest
    method: GET
    url: "/api/audit-logs?patient=${patient.id}&action=UPDATE_INTAKE"
    expect:
      body:
        performer_role: "Patient"  # Not staff

checkpoints:
  - name: "Self-Service Edit"
    verify: "Patient edited own intake"
  - name: "Data Updated"
    verify: "Changes persisted to database"
```

---

### Edge Cases

#### TW-INTAKE-005: Switch From Manual to AI Mode

**Requirement**: FR-029  
**Use Case**: UC-006 Alternative Flow  
**Priority**: Medium

```yaml
test: "Patient Switches from Manual to AI Mode"
preconditions:
  - authenticated_as: "Patient"
  
steps:
  - step: "Start manual form mode"
    actions:
      - goto: "/intake"
      - click: '[data-testid="manual-mode-button"]'
          
  - step: "Partially fill form"
    actions:
      - fill:
          selector: '[data-testid="medical-history"]'
          value: "Hypertension"
      - fill:
          selector: '[data-testid="allergies"]'
          value: "Latex"
          
  - step: "Switch to AI mode"
    action: click
    selector: '[data-testid="switch-to-ai"]'
    
  - step: "Verify AI interface displayed"
    action: expect
    selector: '[data-testid="ai-chat-interface"]'
    assertion: toBeVisible
    
  - step: "Verify AI acknowledges existing data"
    action: expect
    selector: '[data-testid="ai-response"]'
    assertion: toContainText
    text: "I see you've already provided some information"
    
  - step: "AI asks about remaining fields"
    action: expect
    selector: '[data-testid="ai-response"]'
    assertion: toContainText
    text: "current medications"
    
  - step: "Complete via AI"
    actions:
      - fill:
          selector: '[data-testid="message-input"]'
          value: "Amlodipine 5mg daily"
      - click: '[data-testid="send-message"]'
          
  - step: "Verify combined data in final summary"
    actions:
      - expect:
          selector: '[data-testid="summary-medical-history"]'
          assertion: toContainText
          text: "Hypertension"
      - expect:
          selector: '[data-testid="summary-allergies"]'
          assertion: toContainText
          text: "Latex"
      - expect:
          selector: '[data-testid="summary-medications"]'
          assertion: toContainText
          text: "Amlodipine"

checkpoints:
  - name: "Bidirectional Switch"
    verify: "Can switch from manual to AI"
  - name: "Data Preserved"
    verify: "All data retained across mode switch"
```

---

#### TW-INTAKE-006: Multiple Edit Cycles

**Requirement**: FR-030  
**Priority**: Medium

```yaml
test: "Patient Edits Intake Multiple Times"
preconditions:
  - authenticated_as: "Patient"
  - completed_intake: true
  
steps:
  - step: "First edit - add medication"
    actions:
      - goto: "/intake/summary"
      - click: '[data-testid="edit-intake"]'
      - fill:
          selector: '[data-testid="current-medications"]'
          value: "${existing}, New Medication X"
      - click: '[data-testid="save-changes"]'
          
  - step: "Verify first edit saved"
    action: expect
    selector: '[data-testid="summary-medications"]'
    assertion: toContainText
    text: "New Medication X"
    
  - step: "Second edit - update allergy"
    actions:
      - click: '[data-testid="edit-intake"]'
      - fill:
          selector: '[data-testid="allergies"]'
          value: "Penicillin, Sulfa drugs"
      - click: '[data-testid="save-changes"]'
          
  - step: "Verify second edit saved"
    action: expect
    selector: '[data-testid="summary-allergies"]'
    assertion: toContainText
    text: "Sulfa drugs"
    
  - step: "Third edit - correct symptom"
    actions:
      - click: '[data-testid="edit-intake"]'
      - fill:
          selector: '[data-testid="current-symptoms"]'
          value: "Corrected symptom description"
      - click: '[data-testid="save-changes"]'
          
  - step: "Verify all edits persisted"
    action: apiRequest
    method: GET
    url: "/api/intake/${patient.id}"
    expect:
      body:
        medications: "contains('New Medication X')"
        allergies: "Penicillin, Sulfa drugs"
        symptoms: "Corrected symptom description"

checkpoints:
  - name: "Multiple Edits Allowed"
    verify: "No limit on edit count"
  - name: "Version History"
    verify: "Edit history tracked in audit log"
```

---

### Error Scenarios

#### TW-INTAKE-007: Submit Manual Form With Missing Required Fields

**Requirement**: FR-028  
**Use Case**: UC-006 Alternative Flow  
**Priority**: High

```yaml
test: "Form Validation Prevents Incomplete Submission"
preconditions:
  - authenticated_as: "Patient"
  
steps:
  - step: "Navigate to manual form"
    actions:
      - goto: "/intake"
      - click: '[data-testid="manual-mode-button"]'
          
  - step: "Fill only partial data (missing required fields)"
    actions:
      - fill:
          selector: '[data-testid="medical-history"]'
          value: "Diabetes"
      # Intentionally skip medications and allergies
      
  - step: "Attempt to submit incomplete form"
    action: click
    selector: '[data-testid="submit-intake"]'
    
  - step: "Verify validation errors displayed"
    actions:
      - expect:
          selector: '[data-testid="validation-errors"]'
          assertion: toBeVisible
      - expect:
          selector: '[data-testid="field-error-medications"]'
          assertion: toContainText
          text: "Current medications is required"
      - expect:
          selector: '[data-testid="field-error-allergies"]'
          assertion: toContainText
          text: "Allergies information is required"
          
  - step: "Verify form not submitted"
    action: expectURL
    url: "/intake"  # Still on intake page

checkpoints:
  - name: "Validation Enforced"
    verify: "Incomplete data rejected"
  - name: "Clear Error Messages"
    verify: "User knows what's missing"
```

---

#### TW-INTAKE-008: AI Parsing Ambiguous Response

**Requirement**: FR-027  
**Priority**: Medium

```yaml
test: "AI Handles Ambiguous Patient Response"
preconditions:
  - authenticated_as: "Patient"
  
steps:
  - step: "Start AI intake"
    actions:
      - goto: "/intake"
      - click: '[data-testid="ai-mode-button"]'
          
  - step: "AI asks about medications"
    action: waitForSelector
    selector: '[data-testid="ai-response"]:has-text("medications")'
    
  - step: "Provide ambiguous response"
    actions:
      - fill:
          selector: '[data-testid="message-input"]'
          value: "I take the blue pill and the white one"  # No drug names
      - click: '[data-testid="send-message"]'
          
  - step: "Verify AI requests clarification"
    action: expect
    selector: '[data-testid="ai-response"]'
    assertion: toContainText
    text: "Could you provide the medication names"
    
  - step: "Provide clearer response"
    actions:
      - fill:
          selector: '[data-testid="message-input"]'
          value: "Metformin and Lisinopril"
      - click: '[data-testid="send-message"]'
          
  - step: "Verify medications now captured"
    action: expect
    selector: '[data-testid="captured-data-summary"]'
    assertion: toContainText
    text: "Metformin"

checkpoints:
  - name: "Clarification Loop"
    verify: "AI asks follow-up questions for ambiguous data"
  - name: "Data Accuracy"
    verify: "Only clear responses captured"
```

---

## Test Data

### Sample Intake Data

```yaml
sample_intake:
  - profile: "Diabetic Patient"
    medical_history: "Type 2 Diabetes (2015), Hypertension (2018)"
    medications:
      - "Metformin 500mg twice daily"
      - "Lisinopril 10mg once daily"
    allergies: ["Penicillin"]
    symptoms: "Occasional headaches, mild dizziness"
    
  - profile: "Healthy Patient"
    medical_history: "No chronic conditions"
    medications: []
    allergies: ["None"]
    symptoms: "Routine checkup"
```

---

## Traceability Matrix

| Test Case | Requirements | Use Cases | Priority |
|-----------|--------------|-----------|----------|
| TW-INTAKE-001 | FR-027, FR-031 | UC-005 | Critical |
| TW-INTAKE-002 | FR-028, FR-031 | UC-006 | Critical |
| TW-INTAKE-003 | FR-029 | UC-005 | High |
| TW-INTAKE-004 | FR-030 | - | High |
| TW-INTAKE-005 | FR-029 | UC-006 | Medium |
| TW-INTAKE-006 | FR-030 | - | Medium |
| TW-INTAKE-007 | FR-028 | UC-006 | High |
| TW-INTAKE-008 | FR-027 | - | Medium |

---

## Execution Notes

- Mock AI service responses for consistent testing
- Implement retry logic for AI API timeouts
- Test AI parsing with various conversational patterns
- Validate data persistence across mode switches
- Test with screen readers for accessibility compliance

---

**Generated**: 2026-06-10  
**Source**: MasterTestPlan.md  
**Framework**: Playwright + TypeScript
