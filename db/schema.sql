-- Database schema for IEC-102 E file reception system
-- Compatible with OpenGauss/PostgreSQL

-- Create main table for tracking received E files
CREATE TABLE IF NOT EXISTS RECEIVED_EFILES (
    Id SERIAL PRIMARY KEY,
    CommonAddr VARCHAR(100) NOT NULL,
    TypeId VARCHAR(50) NOT NULL,
    FileName VARCHAR(100) NOT NULL,
    ReceivedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FileSize INTEGER NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'SUCCESS',
    ErrorMessage VARCHAR(500),
    CONSTRAINT uk_efile UNIQUE (CommonAddr, TypeId, FileName)
);

CREATE INDEX idx_received_at ON RECEIVED_EFILES(ReceivedAt);
CREATE INDEX idx_status ON RECEIVED_EFILES(Status);

-- Example INFO table (for demonstration purposes)
-- These tables are created based on the E file content
-- The following is an example schema for *_INFO tables

CREATE TABLE IF NOT EXISTS STATION_INFO (
    ID VARCHAR(50) PRIMARY KEY,
    Name VARCHAR(100),
    Location VARCHAR(200),
    Latitude DECIMAL(10, 6),
    Longitude DECIMAL(10, 6),
    Capacity DECIMAL(10, 2),
    Status VARCHAR(20),
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS DEVICE_INFO (
    ID VARCHAR(50) PRIMARY KEY,
    StationId VARCHAR(50),
    DeviceType VARCHAR(50),
    Model VARCHAR(100),
    Manufacturer VARCHAR(100),
    InstallDate DATE,
    Status VARCHAR(20),
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Example data table (for non-INFO tables, bulk insert)
CREATE TABLE IF NOT EXISTS ENERGY_DATA (
    ID SERIAL PRIMARY KEY,
    StationId VARCHAR(50),
    Timestamp TIMESTAMP,
    ActivePower DECIMAL(10, 2),
    ReactivePower DECIMAL(10, 2),
    Voltage DECIMAL(10, 2),
    Current DECIMAL(10, 2),
    Frequency DECIMAL(5, 2),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_energy_station ON ENERGY_DATA(StationId);
CREATE INDEX idx_energy_timestamp ON ENERGY_DATA(Timestamp);

-- Comments
COMMENT ON TABLE RECEIVED_EFILES IS 'Tracks all received and processed E files';
COMMENT ON TABLE STATION_INFO IS 'Station information, supports upsert by ID';
COMMENT ON TABLE DEVICE_INFO IS 'Device information, supports upsert by ID';
COMMENT ON TABLE ENERGY_DATA IS 'Energy measurement data, bulk insert only';
