-- ============================================================
-- HealthPlatform — Relational Demo Seed Data
-- Run once against a migrated database to populate realistic
-- cross-table data visible in the UI.
--
-- Password for ALL test accounts: Test@1234
-- Logins:
--   alice.johnson@test.com   (Patient)
--   bob.smith@test.com       (Patient)
--   carol.white@test.com     (Patient)
--   david.brown@test.com     (Patient)
--   emily.davis@test.com     (Patient)
--   frank.miller@test.com    (Patient)
--   staff.nurse@test.com     (Staff)
-- ============================================================

BEGIN;

-- ============================================================
-- 1. USERS  (6 patients + 1 staff)
--    All bcrypt hashes correspond to password: Test@1234
-- ============================================================
INSERT INTO users (
    id, email, password_hash, role, is_active,
    credential_expires_at, failed_login_attempts,
    password_history, notification_preferences,
    created_at, updated_at, is_deleted
) VALUES
-- Patients
(
    'aaaaaaaa-0001-0000-0000-000000000001',
    'alice.johnson@test.com',
    '$2b$12$Tr4vDZwoO8p9bKqwgeLPU.Pl2o2dGFTeysSTZjn5RxQmgMetnBTO.',
    'Patient', true,
    NOW() + INTERVAL '90 days', 0, '[]', '{}',
    NOW() - INTERVAL '30 days', NOW() - INTERVAL '30 days', false
),
(
    'aaaaaaaa-0001-0000-0000-000000000002',
    'bob.smith@test.com',
    '$2b$12$tYmy/.aHCAPUE8STgJSX4.kRQlJt3d8aqiUu8T3L9LPhE3UaXxRYa',
    'Patient', true,
    NOW() + INTERVAL '90 days', 0, '[]', '{}',
    NOW() - INTERVAL '25 days', NOW() - INTERVAL '25 days', false
),
(
    'aaaaaaaa-0001-0000-0000-000000000003',
    'carol.white@test.com',
    '$2b$12$mHadrrGVzNOghS5fCi/pXex98r.hZoGBOlQEYEmHtYccTpPsO6CA6',
    'Patient', true,
    NOW() + INTERVAL '90 days', 0, '[]', '{}',
    NOW() - INTERVAL '20 days', NOW() - INTERVAL '20 days', false
),
(
    'aaaaaaaa-0001-0000-0000-000000000004',
    'david.brown@test.com',
    '$2b$12$PJDy3IlbbFjENfPxBWbDFOzGctBpEsgrfo2OqgdIaQuNZmBiONOce',
    'Patient', true,
    NOW() + INTERVAL '90 days', 0, '[]', '{}',
    NOW() - INTERVAL '15 days', NOW() - INTERVAL '15 days', false
),
(
    'aaaaaaaa-0001-0000-0000-000000000005',
    'emily.davis@test.com',
    '$2b$12$sSYd1KPuufnkPYXol7STv.vhqD65sEBskHUmlwXJY2pCSjozaX8lS',
    'Patient', true,
    NOW() + INTERVAL '90 days', 0, '[]', '{}',
    NOW() - INTERVAL '10 days', NOW() - INTERVAL '10 days', false
),
(
    'aaaaaaaa-0001-0000-0000-000000000006',
    'frank.miller@test.com',
    '$2b$12$kNJLQ.IwlvOx.RFhELXgkOEwpCzkPJZ3RoLQXWjdCm6b/rxjHX7y6',
    'Patient', true,
    NOW() + INTERVAL '90 days', 0, '[]', '{}',
    NOW() - INTERVAL '5 days', NOW() - INTERVAL '5 days', false
),
-- Staff
(
    'aaaaaaaa-0002-0000-0000-000000000001',
    'staff.nurse@test.com',
    '$2b$12$Tr4vDZwoO8p9bKqwgeLPU.Pl2o2dGFTeysSTZjn5RxQmgMetnBTO.',
    'Staff', true,
    NOW() + INTERVAL '90 days', 0, '[]', '{}',
    NOW() - INTERVAL '60 days', NOW() - INTERVAL '60 days', false
)
ON CONFLICT (id) DO NOTHING;

-- ============================================================
-- 2. PATIENT PROFILES
-- ============================================================
INSERT INTO patient_profiles (
    id, user_id, first_name, last_name, dob, phone,
    insurance_provider_name, insurance_member_id, total_no_show_count,
    created_at, updated_at, is_deleted
) VALUES
(
    'b000b001-0001-0000-0000-000000000001',
    'aaaaaaaa-0001-0000-0000-000000000001',
    'Alice', 'Johnson', '1985-03-15', '+1-555-0101',
    'BlueCross BlueShield', 'BCB-2025-00101', 0,
    NOW() - INTERVAL '30 days', NOW() - INTERVAL '30 days', false
),
(
    'b000b001-0001-0000-0000-000000000002',
    'aaaaaaaa-0001-0000-0000-000000000002',
    'Bob', 'Smith', '1978-07-22', '+1-555-0102',
    'Aetna Health', 'AET-2025-00202', 0,
    NOW() - INTERVAL '25 days', NOW() - INTERVAL '25 days', false
),
(
    'b000b001-0001-0000-0000-000000000003',
    'aaaaaaaa-0001-0000-0000-000000000003',
    'Carol', 'White', '1992-11-08', '+1-555-0103',
    'UnitedHealthcare', 'UHC-2025-00303', 1,
    NOW() - INTERVAL '20 days', NOW() - INTERVAL '20 days', false
),
(
    'b000b001-0001-0000-0000-000000000004',
    'aaaaaaaa-0001-0000-0000-000000000004',
    'David', 'Brown', '1965-05-30', '+1-555-0104',
    'Cigna Healthcare', 'CGN-2025-00404', 0,
    NOW() - INTERVAL '15 days', NOW() - INTERVAL '15 days', false
),
(
    'b000b001-0001-0000-0000-000000000005',
    'aaaaaaaa-0001-0000-0000-000000000005',
    'Emily', 'Davis', '2000-01-19', '+1-555-0105',
    'Humana', 'HUM-2025-00505', 0,
    NOW() - INTERVAL '10 days', NOW() - INTERVAL '10 days', false
),
(
    'b000b001-0001-0000-0000-000000000006',
    'aaaaaaaa-0001-0000-0000-000000000006',
    'Frank', 'Miller', '1958-09-04', '+1-555-0106',
    'Medicare', 'MCR-2025-00606', 0,
    NOW() - INTERVAL '5 days', NOW() - INTERVAL '5 days', false
)
ON CONFLICT (id) DO NOTHING;

-- ============================================================
-- 3. MARK PAST SLOTS AS BOOKED
--    Provider 1 (Dr. Sarah Mitchell) — May 27 slots
-- ============================================================
UPDATE appointment_slots
SET    status = 'Booked'
WHERE  id IN (
    '16bbb4fb-6790-4c06-877a-a284fe41b805',   -- Alice  past slot
    '5bfa320a-bfb9-4791-9bdc-b5388bbab895',   -- Bob    past slot
    '38db07e8-e2d2-486d-bc36-51d169f9f80a'    -- Carol  past slot
)
AND status = 'Available';

-- Mark upcoming slots as Booked so they can't be double-booked
UPDATE appointment_slots
SET    status = 'Booked'
WHERE  id IN (
    '31977fad-c32e-4c94-8e86-d911737220bc',   -- Alice  upcoming  Provider 1
    'c19d8ac2-c13b-40a2-978e-0050eee8244f',   -- Bob    upcoming  Provider 2
    '46a2023f-033a-4f5d-ace9-178e93aa1750',   -- Carol  upcoming  Provider 3
    '6c79d0f8-65f2-4981-b04b-e19cc60d17e2',   -- David  upcoming  Provider 4
    'df25634d-1e4f-4ab9-b3fb-26490af93c22',   -- Emily  upcoming  Provider 5
    '2ff931cb-a075-4171-8cd1-b012bf4347b6'    -- Frank  upcoming  Provider 1
)
AND status = 'Available';

-- ============================================================
-- 4. APPOINTMENTS
-- ============================================================
INSERT INTO appointments (
    id, patient_id, provider_id, slot_id, slot_time,
    status, is_walk_in, is_conflict_override, is_deleted,
    visit_reason, created_at, updated_at
) VALUES

-- ── Past: Completed ──────────────────────────────────────────
(
    'eeeeeeee-0001-0000-0000-000000000001',
    'b000b001-0001-0000-0000-000000000001',          -- Alice
    '11111111-0000-0000-0000-000000000001',          -- Dr. Sarah Mitchell
    '16bbb4fb-6790-4c06-877a-a284fe41b805',
    '2026-05-27 09:00:00+00',
    'Completed', false, false, false,
    'Annual cardiac check-up',
    NOW() - INTERVAL '5 days', NOW() - INTERVAL '2 days'
),
(
    'eeeeeeee-0001-0000-0000-000000000002',
    'b000b001-0001-0000-0000-000000000002',          -- Bob
    '11111111-0000-0000-0000-000000000001',          -- Dr. Sarah Mitchell
    '5bfa320a-bfb9-4791-9bdc-b5388bbab895',
    '2026-05-27 09:30:00+00',
    'Cancelled', false, false, false,
    'Follow-up consultation',
    NOW() - INTERVAL '6 days', NOW() - INTERVAL '3 days'
),
(
    'eeeeeeee-0001-0000-0000-000000000003',
    'b000b001-0001-0000-0000-000000000003',          -- Carol
    '11111111-0000-0000-0000-000000000001',          -- Dr. Sarah Mitchell
    '38db07e8-e2d2-486d-bc36-51d169f9f80a',
    '2026-05-27 10:00:00+00',
    'NoShow', false, false, false,
    'Routine check-up',
    NOW() - INTERVAL '7 days', NOW() - INTERVAL '2 days'
),

-- ── Upcoming: Scheduled ──────────────────────────────────────
(
    'eeeeeeee-0002-0000-0000-000000000001',
    'b000b001-0001-0000-0000-000000000001',          -- Alice
    '11111111-0000-0000-0000-000000000001',          -- Dr. Sarah Mitchell (Cardiology)
    '31977fad-c32e-4c94-8e86-d911737220bc',
    '2026-05-29 09:00:00+00',
    'Scheduled', false, false, false,
    'Echocardiogram review',
    NOW() - INTERVAL '3 days', NOW() - INTERVAL '3 days'
),
(
    'eeeeeeee-0002-0000-0000-000000000002',
    'b000b001-0001-0000-0000-000000000002',          -- Bob
    '11111111-0000-0000-0000-000000000002',          -- Dr. James Okafor (General Practice)
    'c19d8ac2-c13b-40a2-978e-0050eee8244f',
    '2026-05-29 09:00:00+00',
    'Scheduled', false, false, false,
    'Annual physical exam',
    NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days'
),
(
    'eeeeeeee-0002-0000-0000-000000000003',
    'b000b001-0001-0000-0000-000000000003',          -- Carol
    '11111111-0000-0000-0000-000000000003',          -- Dr. Priya Sharma (Neurology)
    '46a2023f-033a-4f5d-ace9-178e93aa1750',
    '2026-05-29 09:00:00+00',
    'Scheduled', false, false, false,
    'Migraine management consultation',
    NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days'
),
(
    'eeeeeeee-0002-0000-0000-000000000004',
    'b000b001-0001-0000-0000-000000000004',          -- David
    '11111111-0000-0000-0000-000000000004',          -- Dr. Marcus Chen (Orthopedics)
    '6c79d0f8-65f2-4981-b04b-e19cc60d17e2',
    '2026-05-29 09:00:00+00',
    'Scheduled', false, false, false,
    'Knee pain assessment',
    NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day'
),
(
    'eeeeeeee-0002-0000-0000-000000000005',
    'b000b001-0001-0000-0000-000000000005',          -- Emily
    '11111111-0000-0000-0000-000000000005',          -- Dr. Fatima Al-Rashid (Pediatrics)
    'df25634d-1e4f-4ab9-b3fb-26490af93c22',
    '2026-05-29 09:00:00+00',
    'Scheduled', false, false, false,
    'Vaccination follow-up',
    NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days'
),
(
    'eeeeeeee-0002-0000-0000-000000000006',
    'b000b001-0001-0000-0000-000000000006',          -- Frank
    '11111111-0000-0000-0000-000000000001',          -- Dr. Sarah Mitchell (Cardiology)
    '2ff931cb-a075-4171-8cd1-b012bf4347b6',
    '2026-05-29 09:30:00+00',
    'Booked', false, false, false,
    'Post-bypass recovery check',
    NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day'
)
ON CONFLICT (id) DO NOTHING;

-- ============================================================
-- 5. INTAKE RECORDS
-- ============================================================
INSERT INTO intake_records (
    id, patient_id, appointment_id, mode,
    data_json, completed_at, created_at, updated_at, is_deleted
) VALUES
-- Alice — completed AI intake for upcoming appointment
(
    'cafe0001-0001-0000-0000-000000000001',
    'b000b001-0001-0000-0000-000000000001',
    'eeeeeeee-0002-0000-0000-000000000001',
    'AiConversational',
    '{
        "chiefComplaint": "Chest tightness and shortness of breath on exertion",
        "duration": "3 weeks",
        "severity": 6,
        "currentMedications": ["Lisinopril 10mg", "Atorvastatin 40mg"],
        "allergies": ["Penicillin"],
        "smokingStatus": "Never",
        "alcoholUse": "Social",
        "familyHistory": "Father had MI at age 58"
    }',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '3 days', NOW() - INTERVAL '1 day', false
),
-- Bob — draft manual form intake for upcoming appointment
(
    'cafe0001-0001-0000-0000-000000000002',
    'b000b001-0001-0000-0000-000000000002',
    'eeeeeeee-0002-0000-0000-000000000002',
    'ManualForm',
    '{
        "chiefComplaint": "Routine annual physical",
        "currentMedications": ["Metformin 500mg"],
        "allergies": [],
        "smokingStatus": "Former",
        "alcoholUse": "None"
    }',
    NULL,
    NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day', false
),
-- Alice — completed intake for past completed appointment
(
    'cafe0001-0001-0000-0000-000000000003',
    'b000b001-0001-0000-0000-000000000001',
    'eeeeeeee-0001-0000-0000-000000000001',
    'AiConversational',
    '{
        "chiefComplaint": "Annual cardiac screening",
        "duration": "N/A",
        "severity": 2,
        "currentMedications": ["Lisinopril 10mg"],
        "allergies": ["Penicillin"],
        "smokingStatus": "Never"
    }',
    NOW() - INTERVAL '3 days',
    NOW() - INTERVAL '6 days', NOW() - INTERVAL '3 days', false
)
ON CONFLICT (id) DO NOTHING;

-- ============================================================
-- 6. NOTIFICATIONS
-- ============================================================
INSERT INTO notifications (
    id, patient_id, appointment_id, user_id,
    channel, type, title, message,
    sent_at, delivery_status, is_read, expires_at
) VALUES
-- Confirmation sent when Alice booked her upcoming appointment
(
    'ffffffff-0001-0000-0000-000000000001',
    'b000b001-0001-0000-0000-000000000001',
    'eeeeeeee-0002-0000-0000-000000000001',
    'aaaaaaaa-0001-0000-0000-000000000001',
    'Email', 'Confirmation',
    'Appointment Confirmed',
    'Your appointment with Dr. Sarah Mitchell on May 29 at 2:30 PM has been confirmed.',
    NOW() - INTERVAL '3 days', 'Delivered', true,
    NOW() + INTERVAL '7 days'
),
-- 24h reminder for Alice''s upcoming appointment
(
    'ffffffff-0001-0000-0000-000000000002',
    'b000b001-0001-0000-0000-000000000001',
    'eeeeeeee-0002-0000-0000-000000000001',
    'aaaaaaaa-0001-0000-0000-000000000001',
    'Email', 'Reminder',
    'Appointment Reminder – Tomorrow',
    'Reminder: You have an appointment with Dr. Sarah Mitchell tomorrow at 2:30 PM.',
    NOW() - INTERVAL '1 hour', 'Delivered', false,
    NOW() + INTERVAL '2 days'
),
-- Confirmation for Bob''s upcoming appointment
(
    'ffffffff-0001-0000-0000-000000000003',
    'b000b001-0001-0000-0000-000000000002',
    'eeeeeeee-0002-0000-0000-000000000002',
    'aaaaaaaa-0001-0000-0000-000000000002',
    'Email', 'Confirmation',
    'Appointment Confirmed',
    'Your appointment with Dr. James Okafor on May 29 at 2:30 PM has been confirmed.',
    NOW() - INTERVAL '2 days', 'Delivered', true,
    NOW() + INTERVAL '7 days'
),
-- Confirmation for Carol''s upcoming appointment
(
    'ffffffff-0001-0000-0000-000000000004',
    'b000b001-0001-0000-0000-000000000003',
    'eeeeeeee-0002-0000-0000-000000000003',
    'aaaaaaaa-0001-0000-0000-000000000003',
    'Email', 'Confirmation',
    'Appointment Confirmed',
    'Your appointment with Dr. Priya Sharma on May 29 at 2:30 PM has been confirmed.',
    NOW() - INTERVAL '4 days', 'Delivered', true,
    NOW() + INTERVAL '7 days'
),
-- Cancellation notification for Bob''s past cancelled appointment
(
    'ffffffff-0001-0000-0000-000000000005',
    'b000b001-0001-0000-0000-000000000002',
    'eeeeeeee-0001-0000-0000-000000000002',
    'aaaaaaaa-0001-0000-0000-000000000002',
    'Email', 'Confirmation',
    'Appointment Cancelled',
    'Your appointment with Dr. Sarah Mitchell on May 27 has been cancelled.',
    NOW() - INTERVAL '3 days', 'Delivered', true,
    NOW() + INTERVAL '7 days'
),
-- Confirmation for David''s upcoming appointment
(
    'ffffffff-0001-0000-0000-000000000006',
    'b000b001-0001-0000-0000-000000000004',
    'eeeeeeee-0002-0000-0000-000000000004',
    'aaaaaaaa-0001-0000-0000-000000000004',
    'Email', 'Confirmation',
    'Appointment Confirmed',
    'Your appointment with Dr. Marcus Chen on May 29 at 2:30 PM has been confirmed.',
    NOW() - INTERVAL '1 day', 'Delivered', true,
    NOW() + INTERVAL '7 days'
),
-- Confirmation for Emily''s upcoming appointment
(
    'ffffffff-0001-0000-0000-000000000007',
    'b000b001-0001-0000-0000-000000000005',
    'eeeeeeee-0002-0000-0000-000000000005',
    'aaaaaaaa-0001-0000-0000-000000000005',
    'Email', 'Confirmation',
    'Appointment Confirmed',
    'Your appointment with Dr. Fatima Al-Rashid on May 29 at 2:30 PM has been confirmed.',
    NOW() - INTERVAL '2 days', 'Delivered', true,
    NOW() + INTERVAL '7 days'
),
-- Confirmation for Frank''s upcoming appointment
(
    'ffffffff-0001-0000-0000-000000000008',
    'b000b001-0001-0000-0000-000000000006',
    'eeeeeeee-0002-0000-0000-000000000006',
    'aaaaaaaa-0001-0000-0000-000000000006',
    'Email', 'Confirmation',
    'Appointment Confirmed',
    'Your appointment with Dr. Sarah Mitchell on May 29 at 3:00 PM has been confirmed.',
    NOW() - INTERVAL '1 day', 'Delivered', true,
    NOW() + INTERVAL '7 days'
),
-- In-app reminder for Alice
(
    'ffffffff-0001-0000-0000-000000000009',
    'b000b001-0001-0000-0000-000000000001',
    'eeeeeeee-0002-0000-0000-000000000001',
    'aaaaaaaa-0001-0000-0000-000000000001',
    'InApp', 'Reminder',
    'Your appointment is today!',
    'You have an appointment with Dr. Sarah Mitchell in 2 hours. Please complete your intake form.',
    NOW() - INTERVAL '30 minutes', 'Delivered', false,
    NOW() + INTERVAL '1 day'
)
ON CONFLICT (id) DO NOTHING;

-- ============================================================
-- 7. CLINICAL DOCUMENTS  (Alice has 2 uploaded docs)
-- ============================================================
INSERT INTO clinical_documents (
    id, patient_id, file_name, storage_path, file_size_bytes,
    uploaded_at, processing_status,
    created_at, updated_at, is_deleted
) VALUES
(
    'dddddddd-0001-0000-0000-000000000001',
    'b000b001-0001-0000-0000-000000000001',
    'ecg_report_2026_05.pdf',
    'patients/alice-johnson/ecg_report_2026_05.pdf',
    184320,
    NOW() - INTERVAL '10 days',
    'Processed',
    NOW() - INTERVAL '10 days', NOW() - INTERVAL '8 days', false
),
(
    'dddddddd-0001-0000-0000-000000000002',
    'b000b001-0001-0000-0000-000000000001',
    'blood_panel_2026_04.pdf',
    'patients/alice-johnson/blood_panel_2026_04.pdf',
    92160,
    NOW() - INTERVAL '25 days',
    'Processed',
    NOW() - INTERVAL '25 days', NOW() - INTERVAL '23 days', false
),
(
    'dddddddd-0001-0000-0000-000000000003',
    'b000b001-0001-0000-0000-000000000004',
    'xray_knee_left_2026_05.pdf',
    'patients/david-brown/xray_knee_left_2026_05.pdf',
    512000,
    NOW() - INTERVAL '3 days',
    'Processing',
    NOW() - INTERVAL '3 days', NOW() - INTERVAL '3 days', false
)
ON CONFLICT (id) DO NOTHING;

-- ============================================================
-- 8. PATIENT VIEW 360  (Alice — consolidated record)
-- ============================================================
INSERT INTO patient_views360 (
    id, patient_id, consolidated_data_json,
    last_updated_at, conflict_count
) VALUES
(
    'da1eda1e-0001-0000-0000-000000000001',
    'b000b001-0001-0000-0000-000000000001',
    '{
        "demographics": {
            "name": "Alice Johnson",
            "dob": "1985-03-15",
            "phone": "+1-555-0101"
        },
        "conditions": [
            { "name": "Hypertension", "icd10": "I10", "status": "Active" },
            { "name": "Hyperlipidemia", "icd10": "E78.5", "status": "Active" }
        ],
        "medications": [
            { "name": "Lisinopril",   "dose": "10mg", "frequency": "Daily" },
            { "name": "Atorvastatin", "dose": "40mg", "frequency": "Daily" }
        ],
        "allergies": [
            { "substance": "Penicillin", "reaction": "Rash", "severity": "Moderate" }
        ],
        "vitals": {
            "bp": "132/84",
            "hr": 72,
            "bmi": 24.1,
            "recordedAt": "2026-05-27"
        }
    }',
    NOW() - INTERVAL '2 days',
    0
)
ON CONFLICT (id) DO NOTHING;

-- ============================================================
-- 9. EXTRACTED DATA  (linked to Alice's ECG document)
-- ============================================================
INSERT INTO extracted_data (
    id, document_id, patient_id, data_category,
    data_json, confidence_score, page_number
) VALUES
(
    'dec0dec0-0001-0000-0000-000000000001',
    'dddddddd-0001-0000-0000-000000000001',
    'b000b001-0001-0000-0000-000000000001',
    'Vitals',
    '{
        "heartRate": 72,
        "rhythm": "Normal Sinus Rhythm",
        "qtInterval": "420ms",
        "findings": "No acute ST changes"
    }',
    94, 1
),
(
    'dec0dec0-0001-0000-0000-000000000002',
    'dddddddd-0001-0000-0000-000000000002',
    'b000b001-0001-0000-0000-000000000001',
    'LabResults',
    '{
        "totalCholesterol": 198,
        "ldl": 112,
        "hdl": 58,
        "triglycerides": 140,
        "glucose": 95,
        "hba1c": 5.4
    }',
    97, 1
)
ON CONFLICT (id) DO NOTHING;

-- ============================================================
-- 10. MEDICAL CODES  (linked to Alice's patient view)
-- ============================================================
INSERT INTO medical_codes (
    id, patient_view_id, code_type, code,
    description, confidence
) VALUES
(
    'cccccccc-0001-0000-0000-000000000001',
    'da1eda1e-0001-0000-0000-000000000001',
    'ICD10', 'I10',
    'Essential (primary) hypertension', 95
),
(
    'cccccccc-0001-0000-0000-000000000002',
    'da1eda1e-0001-0000-0000-000000000001',
    'ICD10', 'E78.5',
    'Hyperlipidemia, unspecified', 92
),
(
    'cccccccc-0001-0000-0000-000000000003',
    'da1eda1e-0001-0000-0000-000000000001',
    'CPT', '93000',
    'Electrocardiogram, routine ECG with at least 12 leads', 98
)
ON CONFLICT (id) DO NOTHING;

-- ============================================================
-- 11. AUDIT LOGS  (representative activity trail)
-- ============================================================
INSERT INTO audit_logs (
    id, user_id, action, entity_type, entity_id,
    timestamp, current_hash
) VALUES
(
    'aaaaaaaa-aaaa-0001-0000-000000000001',
    'aaaaaaaa-0001-0000-0000-000000000001',
    'UserRegistered', 'User',
    'aaaaaaaa-0001-0000-0000-000000000001',
    NOW() - INTERVAL '30 days',
    encode(sha256('UserRegistered:aaaaaaaa-0001-0000-0000-000000000001'), 'hex')
),
(
    'aaaaaaaa-aaaa-0002-0000-000000000001',
    'aaaaaaaa-0001-0000-0000-000000000001',
    'AppointmentBooked', 'Appointment',
    'eeeeeeee-0002-0000-0000-000000000001',
    NOW() - INTERVAL '3 days',
    encode(sha256('AppointmentBooked:eeeeeeee-0002-0000-0000-000000000001'), 'hex')
),
(
    'aaaaaaaa-aaaa-0003-0000-000000000001',
    'aaaaaaaa-0001-0000-0000-000000000001',
    'AppointmentCompleted', 'Appointment',
    'eeeeeeee-0001-0000-0000-000000000001',
    NOW() - INTERVAL '2 days',
    encode(sha256('AppointmentCompleted:eeeeeeee-0001-0000-0000-000000000001'), 'hex')
),
(
    'aaaaaaaa-aaaa-0004-0000-000000000001',
    'aaaaaaaa-0001-0000-0000-000000000002',
    'AppointmentCancelled', 'Appointment',
    'eeeeeeee-0001-0000-0000-000000000002',
    NOW() - INTERVAL '3 days',
    encode(sha256('AppointmentCancelled:eeeeeeee-0001-0000-0000-000000000002'), 'hex')
),
(
    'aaaaaaaa-aaaa-0005-0000-000000000001',
    'aaaaaaaa-0001-0000-0000-000000000001',
    'DocumentUploaded', 'ClinicalDocument',
    'dddddddd-0001-0000-0000-000000000001',
    NOW() - INTERVAL '10 days',
    encode(sha256('DocumentUploaded:dddddddd-0001-0000-0000-000000000001'), 'hex')
)
ON CONFLICT (id) DO NOTHING;

COMMIT;
