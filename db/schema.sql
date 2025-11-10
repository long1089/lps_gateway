-- LPS Gateway 数据库模式
-- 兼容 OpenGauss/PostgreSQL
-- M1: 项目骨架与基础设施

-- ============================================================
-- 用户与认证表
-- ============================================================

-- 用户表
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(20) NOT NULL DEFAULT 'Operator',
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_users_username ON users(username);
CREATE INDEX idx_users_role ON users(role);

COMMENT ON TABLE users IS '用户账户表';
COMMENT ON COLUMN users.id IS '用户ID';
COMMENT ON COLUMN users.username IS '用户名';
COMMENT ON COLUMN users.password_hash IS '密码哈希';
COMMENT ON COLUMN users.role IS '角色 (Admin/Operator)';
COMMENT ON COLUMN users.enabled IS '账户是否启用';

-- 操作审计表
CREATE TABLE IF NOT EXISTS audit_logs (
    id SERIAL PRIMARY KEY,
    user_id INTEGER,
    action VARCHAR(50) NOT NULL,
    resource VARCHAR(100) NOT NULL,
    details JSONB,
    ip_address VARCHAR(50),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL
);

CREATE INDEX idx_audit_user ON audit_logs(user_id);
CREATE INDEX idx_audit_action ON audit_logs(action);
CREATE INDEX idx_audit_created ON audit_logs(created_at);

COMMENT ON TABLE audit_logs IS '操作审计日志';

-- ============================================================
-- 配置管理表
-- ============================================================

-- 报表类型配置表
CREATE TABLE IF NOT EXISTS report_types (
    id SERIAL PRIMARY KEY,
    code VARCHAR(20) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    default_sftp_config_id INTEGER,
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_report_types_code ON report_types(code);
CREATE INDEX idx_report_types_enabled ON report_types(enabled);

COMMENT ON TABLE report_types IS '报表类型配置';
COMMENT ON COLUMN report_types.code IS '报表类型编码';
COMMENT ON COLUMN report_types.name IS '报表类型名称';
COMMENT ON COLUMN report_types.default_sftp_config_id IS '默认SFTP配置ID';

-- SFTP 配置表
CREATE TABLE IF NOT EXISTS sftp_configs (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    host VARCHAR(255) NOT NULL,
    port INTEGER NOT NULL DEFAULT 22,
    username VARCHAR(100) NOT NULL,
    auth_type VARCHAR(20) NOT NULL DEFAULT 'password',
    password_encrypted TEXT,
    key_path VARCHAR(500),
    key_passphrase_encrypted TEXT,
    base_path_template VARCHAR(500) NOT NULL,
    concurrency_limit INTEGER NOT NULL DEFAULT 5,
    timeout_sec INTEGER NOT NULL DEFAULT 30,
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_sftp_configs_enabled ON sftp_configs(enabled);

COMMENT ON TABLE sftp_configs IS 'SFTP服务器配置';
COMMENT ON COLUMN sftp_configs.auth_type IS '认证类型 (password/key)';
COMMENT ON COLUMN sftp_configs.password_encrypted IS '加密的密码';
COMMENT ON COLUMN sftp_configs.base_path_template IS '路径模板，支持 {yyyy}/{MM}/{dd}/{HH}/{mm}';

-- 调度配置表
CREATE TABLE IF NOT EXISTS schedules (
    id SERIAL PRIMARY KEY,
    report_type_id INTEGER NOT NULL,
    schedule_type VARCHAR(20) NOT NULL,
    times JSONB,
    month_days JSONB,
    cron_expression VARCHAR(100),
    timezone VARCHAR(50) NOT NULL DEFAULT 'UTC',
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (report_type_id) REFERENCES report_types(id) ON DELETE CASCADE
);

CREATE INDEX idx_schedules_report_type ON schedules(report_type_id);
CREATE INDEX idx_schedules_enabled ON schedules(enabled);

COMMENT ON TABLE schedules IS '定时调度配置';
COMMENT ON COLUMN schedules.schedule_type IS '调度类型 (daily/monthly/cron)';
COMMENT ON COLUMN schedules.times IS '时间点列表 JSON，如 ["08:00","11:15"]';
COMMENT ON COLUMN schedules.month_days IS '月份中的日期 JSON，如 [1,10,20]';

-- 添加外键约束
ALTER TABLE report_types ADD CONSTRAINT fk_report_types_sftp 
    FOREIGN KEY (default_sftp_config_id) REFERENCES sftp_configs(id) ON DELETE SET NULL;

-- ============================================================
-- 文件存储与管理表
-- ============================================================

-- 文件记录表
CREATE TABLE IF NOT EXISTS file_records (
    id SERIAL PRIMARY KEY,
    report_type_id INTEGER NOT NULL,
    sftp_config_id INTEGER,
    original_filename VARCHAR(255) NOT NULL,
    storage_path VARCHAR(1000) NOT NULL,
    file_size BIGINT NOT NULL,
    md5_hash VARCHAR(32),
    download_time TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(20) NOT NULL DEFAULT 'downloaded',
    retention_expires_at TIMESTAMP,
    error_message TEXT,
    metadata JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (report_type_id) REFERENCES report_types(id) ON DELETE CASCADE,
    FOREIGN KEY (sftp_config_id) REFERENCES sftp_configs(id) ON DELETE SET NULL
);

CREATE INDEX idx_file_records_report_type ON file_records(report_type_id);
CREATE INDEX idx_file_records_download_time ON file_records(download_time);
CREATE INDEX idx_file_records_status ON file_records(status);
CREATE INDEX idx_file_records_expires ON file_records(retention_expires_at);
CREATE INDEX idx_file_records_filename ON file_records(original_filename);

COMMENT ON TABLE file_records IS '文件记录表，存储文件元数据';
COMMENT ON COLUMN file_records.status IS '文件状态 (downloaded/processing/sent/error/expired)';
COMMENT ON COLUMN file_records.retention_expires_at IS '保留策略过期时间';

-- ============================================================
-- IEC-102 协议相关表
-- ============================================================

-- TCP 会话日志表
CREATE TABLE IF NOT EXISTS tcp_session_logs (
    id SERIAL PRIMARY KEY,
    session_id VARCHAR(100) NOT NULL,
    client_address VARCHAR(50) NOT NULL,
    connected_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    disconnected_at TIMESTAMP,
    status VARCHAR(20) NOT NULL,
    error_message TEXT
);

CREATE INDEX idx_tcp_session_id ON tcp_session_logs(session_id);
CREATE INDEX idx_tcp_connected ON tcp_session_logs(connected_at);

COMMENT ON TABLE tcp_session_logs IS 'TCP会话日志';

-- 协议指令日志表
CREATE TABLE IF NOT EXISTS protocol_command_logs (
    id SERIAL PRIMARY KEY,
    session_id VARCHAR(100),
    direction VARCHAR(10) NOT NULL,
    frame_hex TEXT NOT NULL,
    type_id VARCHAR(10),
    cot VARCHAR(10),
    common_address VARCHAR(20),
    result VARCHAR(20),
    details JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_protocol_session ON protocol_command_logs(session_id);
CREATE INDEX idx_protocol_created ON protocol_command_logs(created_at);
CREATE INDEX idx_protocol_type ON protocol_command_logs(type_id);

COMMENT ON TABLE protocol_command_logs IS '协议指令日志';
COMMENT ON COLUMN protocol_command_logs.direction IS '方向 (send/receive)';

-- 文件传输任务表
CREATE TABLE IF NOT EXISTS file_transfer_tasks (
    id SERIAL PRIMARY KEY,
    file_record_id INTEGER NOT NULL,
    session_id VARCHAR(100),
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    progress INTEGER NOT NULL DEFAULT 0,
    total_segments INTEGER,
    sent_segments INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    error_message TEXT,
    FOREIGN KEY (file_record_id) REFERENCES file_records(id) ON DELETE CASCADE
);

CREATE INDEX idx_transfer_file ON file_transfer_tasks(file_record_id);
CREATE INDEX idx_transfer_session ON file_transfer_tasks(session_id);
CREATE INDEX idx_transfer_status ON file_transfer_tasks(status);
CREATE INDEX idx_transfer_created ON file_transfer_tasks(created_at);

COMMENT ON TABLE file_transfer_tasks IS '文件传输任务';
COMMENT ON COLUMN file_transfer_tasks.status IS '任务状态 (pending/in_progress/completed/failed/cancelled)';

-- ============================================================
-- 旧版 E 文件接收表（保持兼容）
-- ============================================================

-- 创建主表用于跟踪已接收的 E 文件
CREATE TABLE IF NOT EXISTS received_efiles (
    id SERIAL PRIMARY KEY,
    common_addr VARCHAR(100) NOT NULL,
    type_id VARCHAR(50) NOT NULL,
    file_name VARCHAR(100) NOT NULL,
    received_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    file_size INTEGER NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'SUCCESS',
    error_message VARCHAR(500),
    CONSTRAINT uk_efile UNIQUE (common_addr, type_id, file_name)
);

CREATE INDEX idx_received_at ON received_efiles(received_at);
CREATE INDEX idx_status ON received_efiles(status);

COMMENT ON TABLE received_efiles IS '跟踪所有已接收和处理的 E 文件（旧版兼容）';

-- ============================================================
-- 示例 E 文件内容表（动态创建）
-- ============================================================
-- 以下是示例，实际表将由解析器根据 E 文件内容动态创建

CREATE TABLE IF NOT EXISTS station_info (
    id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100),
    location VARCHAR(200),
    latitude DECIMAL(10, 6),
    longitude DECIMAL(10, 6),
    capacity DECIMAL(10, 2),
    status VARCHAR(20),
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS device_info (
    id VARCHAR(50) PRIMARY KEY,
    station_id VARCHAR(50),
    device_type VARCHAR(50),
    model VARCHAR(100),
    manufacturer VARCHAR(100),
    install_date DATE,
    status VARCHAR(20),
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS energy_data (
    id SERIAL PRIMARY KEY,
    station_id VARCHAR(50),
    timestamp TIMESTAMP,
    active_power DECIMAL(10, 2),
    reactive_power DECIMAL(10, 2),
    voltage DECIMAL(10, 2),
    current DECIMAL(10, 2),
    frequency DECIMAL(5, 2),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_energy_station ON energy_data(station_id);
CREATE INDEX idx_energy_timestamp ON energy_data(timestamp);

-- ============================================================
-- 初始数据（可选）
-- ============================================================

-- 插入默认管理员用户 (密码: admin123)
INSERT INTO users (username, password_hash, role, enabled) 
VALUES ('admin', '$2a$11$3eqvjhs.vVhKqmv8f.6ry.nKp6WdqLB5bSF1GXoF6H8BH.pX/9O7q', 'Admin', TRUE)
ON CONFLICT (username) DO NOTHING;

-- 插入默认 SFTP 配置示例
INSERT INTO sftp_configs (name, host, port, username, auth_type, base_path_template, enabled)
VALUES ('Default SFTP', 'sftp.example.com', 22, 'user', 'password', '/reports/{yyyy}/{MM}/{dd}/', TRUE)
ON CONFLICT DO NOTHING;

-- 插入示例报表类型
INSERT INTO report_types (code, name, description, enabled)
VALUES 
    ('DAILY_ENERGY', '日能量报表', '每日能量统计报表', TRUE),
    ('MONTHLY_SUMMARY', '月度汇总报表', '月度统计汇总报表', TRUE)
ON CONFLICT (code) DO NOTHING;
