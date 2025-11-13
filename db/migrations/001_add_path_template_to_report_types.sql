-- Migration: Add path_template to report_types and remove base_path_template from sftp_configs
-- Date: 2025-11-13

-- Add path_template column to report_types table
ALTER TABLE report_types ADD COLUMN IF NOT EXISTS path_template VARCHAR(500);

COMMENT ON COLUMN report_types.path_template IS '下载路径模板，支持 {yyyy}/{MM}/{dd}/{HH}/{mm}';

-- Remove base_path_template column from sftp_configs table
ALTER TABLE sftp_configs DROP COLUMN IF EXISTS base_path_template;
