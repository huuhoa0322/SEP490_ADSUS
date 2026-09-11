-- ============================================================
-- Migration: 20260911_clinic_services.sql
-- Description: Add Clinic Services, Case Clinic Services, and Invoice Item Type
-- ============================================================

-- 1. Enum cho loại invoice item (MEDICINE, SERVICE)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'invoice_item_type') THEN
        CREATE TYPE invoice_item_type AS ENUM ('MEDICINE', 'SERVICE');
    END IF;
END $$;

-- 2. Bảng danh mục dịch vụ phòng khám (Admin quản lý)
CREATE TABLE IF NOT EXISTS clinic_services (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    code            VARCHAR(50)     NOT NULL UNIQUE,
    name            VARCHAR(200)    NOT NULL,
    description     TEXT,
    price           NUMERIC(18,2)   NOT NULL,
    is_active       BOOLEAN         NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ     NOT NULL DEFAULT now()
);

COMMENT ON TABLE clinic_services IS 'Danh mục dịch vụ phòng khám do Admin cấu hình giá. Admin có thể CRUD tự do.';

-- Seed 2 dịch vụ mặc định (auto-trigger dùng code để lookup)
INSERT INTO clinic_services (code, name, price) VALUES
    ('GENERAL_EXAM',    'Khám thường',   100000),
    ('ULTRASOUND_EXAM', 'Khám siêu âm',  200000)
ON CONFLICT (code) DO NOTHING;

-- 3. Bảng trung gian: dịch vụ đã gắn vào từng ca khám
CREATE TABLE IF NOT EXISTS case_clinic_services (
    id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    case_id             UUID            NOT NULL REFERENCES cases(case_id) ON DELETE CASCADE,
    clinic_service_id   UUID            NOT NULL REFERENCES clinic_services(id),
    price_at_time       NUMERIC(18,2)   NOT NULL,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT now(),
    UNIQUE (case_id, clinic_service_id)
);

COMMENT ON TABLE case_clinic_services IS 'Dịch vụ đã áp dụng cho ca khám. price_at_time = snapshot giá tại thời điểm gắn.';

-- 4. Bổ sung cột item_type vào bảng hóa đơn chi tiết (invoice_item)
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'invoice_item') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'invoice_item' AND column_name = 'item_type') THEN
            ALTER TABLE invoice_item ADD COLUMN item_type invoice_item_type NOT NULL DEFAULT 'MEDICINE';
        END IF;
    ELSIF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'invoice_items') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'invoice_items' AND column_name = 'item_type') THEN
            ALTER TABLE invoice_items ADD COLUMN item_type invoice_item_type NOT NULL DEFAULT 'MEDICINE';
        END IF;
    END IF;
END $$;
