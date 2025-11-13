-- Migration: Increase report_types code column length
-- Date: 2025-11-13

-- Increase code column length from VARCHAR(20) to VARCHAR(100)
-- to accommodate longer report type codes
ALTER TABLE report_types ALTER COLUMN code TYPE VARCHAR(100);

COMMENT ON COLUMN report_types.code IS '报表类型编码（最大100字符）';
