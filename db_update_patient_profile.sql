-- 1. Create Dictionary Tables
CREATE TABLE medical_diseases (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    requires_note BOOLEAN NOT NULL DEFAULT FALSE,
    is_other BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE medical_allergy_types (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(50) NOT NULL,
    is_other BOOLEAN NOT NULL DEFAULT FALSE
);

-- 2. Create Transactional Tables
CREATE TABLE patient_diseases (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_profile_id UUID NOT NULL REFERENCES patient_profiles(patient_profile_id) ON DELETE CASCADE,
    disease_id UUID NOT NULL REFERENCES medical_diseases(id) ON DELETE RESTRICT,
    note VARCHAR(500),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE patient_allergies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_profile_id UUID NOT NULL REFERENCES patient_profiles(patient_profile_id) ON DELETE CASCADE,
    allergy_type_id UUID NOT NULL REFERENCES medical_allergy_types(id) ON DELETE RESTRICT,
    note VARCHAR(500),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 3. Drop old columns
ALTER TABLE patient_profiles
DROP COLUMN medical_history,
DROP COLUMN allergies;

-- 4. Seed Data for medical_allergy_types
INSERT INTO medical_allergy_types (id, name, is_other) VALUES
(gen_random_uuid(), 'Thuốc', false),
(gen_random_uuid(), 'Hóa chất/mỹ phẩm', false),
(gen_random_uuid(), 'Thực phẩm', false),
(gen_random_uuid(), 'Khác', true);

-- 5. Seed Data for medical_diseases
INSERT INTO medical_diseases (id, name, requires_note, is_other) VALUES
(gen_random_uuid(), 'Bệnh tim mạch', false, false),
(gen_random_uuid(), 'Tăng huyết áp', false, false),
(gen_random_uuid(), 'Đái tháo đường', false, false),
(gen_random_uuid(), 'Bệnh dạ dày', false, false),
(gen_random_uuid(), 'Bệnh phổi mạn tính', false, false),
(gen_random_uuid(), 'Hen suyễn', false, false),
(gen_random_uuid(), 'Bệnh bướu cổ', false, false),
(gen_random_uuid(), 'Viêm gan', false, false),
(gen_random_uuid(), 'Tim bẩm sinh', false, false),
(gen_random_uuid(), 'Tâm thần', false, false),
(gen_random_uuid(), 'Tự kỷ', false, false),
(gen_random_uuid(), 'Động kinh', false, false),
(gen_random_uuid(), 'Ung thư', true, false),
(gen_random_uuid(), 'Lao', true, false),
(gen_random_uuid(), 'Khác', true, true);
