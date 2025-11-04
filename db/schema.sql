-- Database schema for LPS Gateway (OpenGauss/PostgreSQL)

-- Table to track received E-files
CREATE TABLE IF NOT EXISTS received_efiles (
    id SERIAL PRIMARY KEY,
    source_identifier VARCHAR(255) NOT NULL UNIQUE,
    received_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    processed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(50) DEFAULT 'processed'
);

CREATE INDEX idx_received_efiles_source ON received_efiles(source_identifier);
CREATE INDEX idx_received_efiles_status ON received_efiles(status);

-- Example info table (created dynamically by application)
-- CREATE TABLE IF NOT EXISTS {tablename}_info (
--     id SERIAL PRIMARY KEY,
--     key VARCHAR(255) NOT NULL,
--     value TEXT,
--     updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
-- );

-- Example data table (created dynamically by application)
-- CREATE TABLE IF NOT EXISTS {tablename}_data (
--     id SERIAL PRIMARY KEY,
--     -- columns are created dynamically based on E-file content
--     created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
-- );

COMMENT ON TABLE received_efiles IS 'Tracks processed E-files to prevent duplicate processing';
COMMENT ON COLUMN received_efiles.source_identifier IS 'Unique identifier for the E-file (address+type or filename)';
COMMENT ON COLUMN received_efiles.received_at IS 'When the E-file was first received';
COMMENT ON COLUMN received_efiles.processed_at IS 'When the E-file was successfully processed';
COMMENT ON COLUMN received_efiles.status IS 'Processing status: processed, error, etc.';
