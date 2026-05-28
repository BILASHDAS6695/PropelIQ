CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE insurance_records (
        id uuid NOT NULL,
        provider_name character varying(200) NOT NULL,
        member_id character varying(100) NOT NULL,
        status character varying(10) NOT NULL,
        CONSTRAINT pk_insurance_records PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE providers (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        specialty character varying(100),
        schedule_template_id uuid,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        created_by text,
        updated_by text,
        CONSTRAINT pk_providers PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE users (
        id uuid NOT NULL,
        email character varying(256) NOT NULL,
        password_hash character varying(512) NOT NULL,
        role character varying(20) NOT NULL,
        is_active boolean NOT NULL,
        last_login_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        created_by text,
        updated_by text,
        CONSTRAINT pk_users PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE appointment_slots (
        id uuid NOT NULL,
        provider_id uuid NOT NULL,
        start_time timestamp with time zone NOT NULL,
        end_time timestamp with time zone NOT NULL,
        is_available boolean NOT NULL,
        CONSTRAINT pk_appointment_slots PRIMARY KEY (id),
        CONSTRAINT fk_appointment_slots_providers_provider_id FOREIGN KEY (provider_id) REFERENCES providers (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE audit_logs (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        action character varying(200) NOT NULL,
        entity_type character varying(200) NOT NULL,
        entity_id uuid NOT NULL,
        timestamp timestamp with time zone NOT NULL,
        details jsonb,
        previous_hash character varying(64),
        current_hash character varying(64) NOT NULL,
        CONSTRAINT pk_audit_logs PRIMARY KEY (id),
        CONSTRAINT fk_audit_logs_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE patient_profiles (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        first_name character varying(100) NOT NULL,
        last_name character varying(100) NOT NULL,
        dob date NOT NULL,
        phone character varying(20),
        insurance_provider_name character varying(200),
        insurance_member_id character varying(100),
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        created_by text,
        updated_by text,
        CONSTRAINT pk_patient_profiles PRIMARY KEY (id),
        CONSTRAINT fk_patient_profiles_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE appointments (
        id uuid NOT NULL,
        patient_id uuid NOT NULL,
        provider_id uuid NOT NULL,
        slot_id uuid NOT NULL,
        slot_time timestamp with time zone NOT NULL,
        status character varying(20) NOT NULL,
        preferred_slot_id uuid,
        is_walk_in boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        created_by text,
        updated_by text,
        CONSTRAINT pk_appointments PRIMARY KEY (id),
        CONSTRAINT fk_appointments_appointment_slots_slot_id FOREIGN KEY (slot_id) REFERENCES appointment_slots (id) ON DELETE RESTRICT,
        CONSTRAINT fk_appointments_patient_profiles_patient_id FOREIGN KEY (patient_id) REFERENCES patient_profiles (id) ON DELETE RESTRICT,
        CONSTRAINT fk_appointments_providers_provider_id FOREIGN KEY (provider_id) REFERENCES providers (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE clinical_documents (
        id uuid NOT NULL,
        patient_id uuid NOT NULL,
        file_name character varying(500) NOT NULL,
        storage_path character varying(1000) NOT NULL,
        file_size_bytes bigint NOT NULL,
        uploaded_at timestamp with time zone NOT NULL,
        processing_status character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        created_by text,
        updated_by text,
        CONSTRAINT pk_clinical_documents PRIMARY KEY (id),
        CONSTRAINT fk_clinical_documents_patient_profiles_patient_id FOREIGN KEY (patient_id) REFERENCES patient_profiles (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE patient_views360 (
        id uuid NOT NULL,
        patient_id uuid NOT NULL,
        consolidated_data_json jsonb,
        last_updated_at timestamp with time zone NOT NULL,
        conflict_count integer NOT NULL,
        CONSTRAINT pk_patient_views360 PRIMARY KEY (id),
        CONSTRAINT fk_patient_views360_patient_profiles_patient_id FOREIGN KEY (patient_id) REFERENCES patient_profiles (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE intake_records (
        id uuid NOT NULL,
        patient_id uuid NOT NULL,
        appointment_id uuid NOT NULL,
        mode character varying(30) NOT NULL,
        data_json jsonb,
        completed_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        created_by text,
        updated_by text,
        CONSTRAINT pk_intake_records PRIMARY KEY (id),
        CONSTRAINT fk_intake_records_appointments_appointment_id FOREIGN KEY (appointment_id) REFERENCES appointments (id) ON DELETE CASCADE,
        CONSTRAINT fk_intake_records_patient_profiles_patient_id FOREIGN KEY (patient_id) REFERENCES patient_profiles (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE notifications (
        id uuid NOT NULL,
        patient_id uuid NOT NULL,
        appointment_id uuid,
        channel character varying(10) NOT NULL,
        type character varying(20) NOT NULL,
        sent_at timestamp with time zone NOT NULL,
        delivery_status character varying(20) NOT NULL,
        CONSTRAINT pk_notifications PRIMARY KEY (id),
        CONSTRAINT fk_notifications_appointments_appointment_id FOREIGN KEY (appointment_id) REFERENCES appointments (id) ON DELETE SET NULL,
        CONSTRAINT fk_notifications_patient_profiles_patient_id FOREIGN KEY (patient_id) REFERENCES patient_profiles (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE preferred_slot_preferences (
        id uuid NOT NULL,
        appointment_id uuid NOT NULL,
        preferred_slot_id uuid NOT NULL,
        registered_at timestamp with time zone NOT NULL,
        status character varying(20) NOT NULL,
        CONSTRAINT pk_preferred_slot_preferences PRIMARY KEY (id),
        CONSTRAINT fk_preferred_slot_preferences_appointments_appointment_id FOREIGN KEY (appointment_id) REFERENCES appointments (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE extracted_data (
        id uuid NOT NULL,
        document_id uuid NOT NULL,
        patient_id uuid NOT NULL,
        data_category character varying(30) NOT NULL,
        data_json jsonb,
        confidence_score integer NOT NULL,
        page_number integer NOT NULL,
        CONSTRAINT pk_extracted_data PRIMARY KEY (id),
        CONSTRAINT fk_extracted_data_clinical_documents_document_id FOREIGN KEY (document_id) REFERENCES clinical_documents (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE data_conflicts (
        id uuid NOT NULL,
        patient_view_id uuid NOT NULL,
        field character varying(200) NOT NULL,
        value_a character varying(1000) NOT NULL,
        value_b character varying(1000) NOT NULL,
        source_doc_a uuid NOT NULL,
        source_doc_b uuid NOT NULL,
        severity character varying(20) NOT NULL,
        resolution_status character varying(20) NOT NULL,
        resolved_by uuid,
        resolved_at timestamp with time zone,
        CONSTRAINT pk_data_conflicts PRIMARY KEY (id),
        CONSTRAINT fk_data_conflicts_patient_views360_patient_view_id FOREIGN KEY (patient_view_id) REFERENCES patient_views360 (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE TABLE medical_codes (
        id uuid NOT NULL,
        patient_view_id uuid NOT NULL,
        code_type character varying(10) NOT NULL,
        code character varying(20) NOT NULL,
        description character varying(500) NOT NULL,
        confidence integer NOT NULL,
        verified_by uuid,
        verified_at timestamp with time zone,
        CONSTRAINT pk_medical_codes PRIMARY KEY (id),
        CONSTRAINT fk_medical_codes_patient_views360_patient_view_id FOREIGN KEY (patient_view_id) REFERENCES patient_views360 (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_appointment_slots_provider_id_start_time ON appointment_slots (provider_id, start_time);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_appointments_patient_id ON appointments (patient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_appointments_provider_id ON appointments (provider_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_appointments_slot_id ON appointments (slot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_audit_logs_entity_type_entity_id ON audit_logs (entity_type, entity_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_audit_logs_user_id ON audit_logs (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_clinical_documents_patient_id ON clinical_documents (patient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_data_conflicts_patient_view_id ON data_conflicts (patient_view_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_extracted_data_document_id ON extracted_data (document_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_extracted_data_patient_id ON extracted_data (patient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_intake_records_appointment_id ON intake_records (appointment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_intake_records_patient_id ON intake_records (patient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_medical_codes_patient_view_id ON medical_codes (patient_view_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_notifications_appointment_id ON notifications (appointment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE INDEX ix_notifications_patient_id ON notifications (patient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_patient_profiles_user_id ON patient_profiles (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_patient_views360_patient_id ON patient_views360 (patient_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_preferred_slot_preferences_appointment_id ON preferred_slot_preferences (appointment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_users_email ON users (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526100151_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260526100151_InitialCreate', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE users ADD deleted_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE users ADD deleted_by text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE users ADD is_deleted boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE providers ADD deleted_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE providers ADD deleted_by text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE providers ADD is_deleted boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE patient_profiles ADD deleted_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE patient_profiles ADD deleted_by text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE patient_profiles ADD is_deleted boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE intake_records ADD deleted_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE intake_records ADD deleted_by text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE intake_records ADD is_deleted boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE clinical_documents ADD deleted_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE clinical_documents ADD deleted_by text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE clinical_documents ADD is_deleted boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE appointments ADD deleted_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE appointments ADD deleted_by text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    ALTER TABLE appointments ADD is_deleted boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260526105355_AddSoftDelete') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260526105355_AddSoftDelete', '8.0.11');
    END IF;
END $EF$;
COMMIT;


-- ============================================================
-- US-030: Staff Swap Mediation — extend slot_swap_requests
-- ============================================================

ALTER TABLE slot_swap_requests
    ADD COLUMN IF NOT EXISTS override_reason               VARCHAR(500),
    ADD COLUMN IF NOT EXISTS mediated_by_user_id           UUID,
    ADD COLUMN IF NOT EXISTS overridden_at                 TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS three_way_new_target_slot_id  UUID;

-- Foreign key: staff user who performed the override
ALTER TABLE slot_swap_requests
    ADD CONSTRAINT fk_slot_swap_requests_mediated_by_user
    FOREIGN KEY (mediated_by_user_id)
    REFERENCES users (id)
    ON DELETE RESTRICT;

-- Note: xmin concurrency token is a built-in PostgreSQL system column —
-- no ALTER TABLE required. EF Core/Npgsql reads it automatically.

COMMENT ON COLUMN slot_swap_requests.override_reason IS
    'Mandatory reason text for staff force-approve, force-decline, or three-way reassignment.';
COMMENT ON COLUMN slot_swap_requests.mediated_by_user_id IS
    'User ID of the staff member who performed the override action.';
COMMENT ON COLUMN slot_swap_requests.overridden_at IS
    'UTC timestamp when the staff override was applied.';
COMMENT ON COLUMN slot_swap_requests.three_way_new_target_slot_id IS
    'For three-way reassignment: new slot ID assigned to the target patient.';

-- ============================================================
-- US-045: Document Upload — add mime_type and encryption_iv
-- ============================================================

DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'clinical_documents' AND column_name = 'mime_type'
    ) THEN
        ALTER TABLE clinical_documents
            ADD COLUMN mime_type    character varying(100) NOT NULL DEFAULT '',
            ADD COLUMN encryption_iv character varying(64)  NOT NULL DEFAULT '';
    END IF;
END $EF$;

-- ============================================================
-- US-046: OCR Pipeline — add extracted_text (JSONB) and ocr_confidence_score
-- ============================================================

DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'clinical_documents' AND column_name = 'extracted_text'
    ) THEN
        ALTER TABLE clinical_documents
            ADD COLUMN extracted_text       jsonb            NULL,
            ADD COLUMN ocr_confidence_score double precision NULL;
    END IF;
END $EF$;
