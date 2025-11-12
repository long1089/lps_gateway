# LPS Gateway 运维手册

## 概述

本文档提供 LPS Gateway 系统的完整运维指南，包括部署、配置、监控、故障排查和维护。

## 系统要求

### 硬件要求

**最低配置**：
- CPU：4 核心
- 内存：8 GB RAM
- 磁盘：100 GB SSD
- 网络：1 Gbps

**推荐配置（生产环境）**：
- CPU：8 核心 或更高
- 内存：16 GB RAM 或更高
- 磁盘：500 GB SSD 或更高
- 网络：10 Gbps
- 备份存储：独立NFS或对象存储

### 软件要求

**操作系统**：
- Ubuntu 22.04 LTS（推荐）
- CentOS 8+ / Red Hat Enterprise Linux 8+
- Windows Server 2022（支持但不推荐生产环境）

**运行时环境**：
- .NET 8 Runtime（必需）
- ASP.NET Core 8 Runtime（必需）

**数据库**：
- PostgreSQL 15+ 或 OpenGauss 5.0+（必需）
- 推荐使用 OpenGauss 以获得更好的性能

**依赖服务**：
- SFTP 服务器（用于文件下载）
- 可选：Redis（用于分布式锁）
- 可选：Prometheus + Grafana（用于监控）

## 部署指南

### 1. 环境准备

#### 1.1 安装 .NET 8 Runtime

**Ubuntu/Debian**：
```bash
# 添加 Microsoft 包源
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# 安装 .NET 8 Runtime
sudo apt-get update
sudo apt-get install -y dotnet-runtime-8.0 aspnetcore-runtime-8.0
```

**CentOS/RHEL**：
```bash
# 添加 Microsoft 包源
sudo rpm -Uvh https://packages.microsoft.com/config/centos/8/packages-microsoft-prod.rpm

# 安装 .NET 8 Runtime
sudo dnf install -y dotnet-runtime-8.0 aspnetcore-runtime-8.0
```

#### 1.2 安装 PostgreSQL/OpenGauss

**PostgreSQL（Ubuntu）**：
```bash
# 安装 PostgreSQL 15
sudo apt-get install -y postgresql-15 postgresql-contrib-15

# 启动服务
sudo systemctl start postgresql
sudo systemctl enable postgresql
```

**OpenGauss（推荐）**：
```bash
# 下载并安装 OpenGauss
# 参考 OpenGauss 官方文档：https://opengauss.org/

# 创建数据库
gsql -U postgres -c "CREATE DATABASE lps_gateway"
```

### 2. 数据库初始化

#### 2.1 创建数据库和用户

```sql
-- 创建数据库
CREATE DATABASE lps_gateway;

-- 创建用户
CREATE USER lps_user WITH PASSWORD 'secure_password_here';

-- 授权
GRANT ALL PRIVILEGES ON DATABASE lps_gateway TO lps_user;
```

#### 2.2 执行数据库迁移

```bash
# 切换到项目目录
cd /opt/lps_gateway

# 应用数据库 schema
psql -U lps_user -d lps_gateway -f db/schema.sql
```

### 3. 应用部署

#### 3.1 编译和打包

```bash
# 克隆代码
git clone https://github.com/long1089/lps_gateway.git
cd lps_gateway

# 编译发布版本
dotnet publish src/LpsGateway.csproj -c Release -o /opt/lps_gateway/app

# 复制配置文件
cp src/appsettings.json /opt/lps_gateway/app/
```

#### 3.2 配置应用

编辑 `/opt/lps_gateway/app/appsettings.json`：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "LpsGateway": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=lps_gateway;Username=lps_user;Password=secure_password_here"
  },
  "Lib60870": {
    "Port": 2404,
    "TimeoutMs": 5000,
    "MaxRetries": 3
  },
  "Quartz": {
    "quartz.scheduler.instanceName": "LpsGatewayScheduler",
    "quartz.jobStore.type": "Quartz.Impl.AdoJobStore.JobStoreTX",
    "quartz.jobStore.dataSource": "default"
  },
  "Retention": {
    "CheckIntervalMinutes": 60,
    "DefaultRetentionDays": 30
  }
}
```

#### 3.3 创建 systemd 服务

创建 `/etc/systemd/system/lps-gateway.service`：

```ini
[Unit]
Description=LPS Gateway - IEC-102 File Transfer Gateway
After=network.target postgresql.service

[Service]
Type=notify
User=lps
Group=lps
WorkingDirectory=/opt/lps_gateway/app
ExecStart=/usr/bin/dotnet /opt/lps_gateway/app/LpsGateway.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=lps-gateway
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_ENVIRONMENT=Production

# 资源限制
LimitNOFILE=65536
MemoryLimit=2G

[Install]
WantedBy=multi-user.target
```

#### 3.4 创建应用用户

```bash
# 创建系统用户
sudo useradd -r -s /bin/false lps

# 设置目录权限
sudo chown -R lps:lps /opt/lps_gateway
sudo chmod -R 755 /opt/lps_gateway
```

#### 3.5 启动服务

```bash
# 重新加载 systemd
sudo systemctl daemon-reload

# 启动服务
sudo systemctl start lps-gateway

# 设置开机自启
sudo systemctl enable lps-gateway

# 检查状态
sudo systemctl status lps-gateway
```

### 4. 配置反向代理（可选但推荐）

#### 4.1 安装 Nginx

```bash
sudo apt-get install -y nginx
```

#### 4.2 配置 Nginx

创建 `/etc/nginx/sites-available/lps-gateway`：

```nginx
upstream lps_gateway {
    server 127.0.0.1:5000;
}

server {
    listen 80;
    server_name lps-gateway.example.com;
    
    # 重定向到 HTTPS（可选）
    # return 301 https://$server_name$request_uri;

    location / {
        proxy_pass http://lps_gateway;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # 超时设置
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
    
    # 静态文件
    location ~* \.(jpg|jpeg|png|gif|ico|css|js)$ {
        expires 1h;
        add_header Cache-Control "public, immutable";
    }
}
```

启用站点：

```bash
sudo ln -s /etc/nginx/sites-available/lps-gateway /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

## 配置管理

### 核心配置项

#### ConnectionStrings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=lps_gateway;Username=lps_user;Password=xxx"
  }
}
```

**参数说明**：
- `Host`: 数据库服务器地址
- `Port`: 数据库端口（PostgreSQL默认5432，OpenGauss默认5432）
- `Database`: 数据库名称
- `Username`: 数据库用户名
- `Password`: 数据库密码（建议使用环境变量或密钥管理服务）

#### Lib60870（IEC-102 协议配置）

```json
{
  "Lib60870": {
    "Port": 2404,
    "TimeoutMs": 5000,
    "MaxRetries": 3,
    "InitialFcb": false
  }
}
```

**参数说明**：
- `Port`: TCP 监听端口（默认2404，IEC-102标准端口）
- `TimeoutMs`: 超时时间（毫秒，默认5000）
- `MaxRetries`: 最大重传次数（默认3）
- `InitialFcb`: FCB 初始值（默认false）

#### Quartz（调度器配置）

```json
{
  "Quartz": {
    "quartz.scheduler.instanceName": "LpsGatewayScheduler",
    "quartz.threadPool.threadCount": "10",
    "quartz.jobStore.misfireThreshold": "60000"
  }
}
```

#### Retention（数据保留策略）

```json
{
  "Retention": {
    "CheckIntervalMinutes": 60,
    "DefaultRetentionDays": 30
  }
}
```

**参数说明**：
- `CheckIntervalMinutes`: 检查间隔（分钟）
- `DefaultRetentionDays`: 默认保留天数

### 环境变量

推荐使用环境变量存储敏感信息：

```bash
# 在 /etc/systemd/system/lps-gateway.service 中添加
Environment="ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=lps_gateway;Username=lps_user;Password=xxx"
Environment="Lib60870__Port=2404"
```

## 监控和告警

### 1. 系统监控

#### 1.1 健康检查

应用提供健康检查端点：

```bash
# 检查应用健康状态
curl http://localhost:5000/health

# 期望响应
{
  "status": "Healthy",
  "components": {
    "database": "Healthy",
    "iec102_slave": "Healthy"
  }
}
```

#### 1.2 日志监控

查看应用日志：

```bash
# 查看 systemd 日志
sudo journalctl -u lps-gateway -f

# 查看最近100行
sudo journalctl -u lps-gateway -n 100

# 查看错误日志
sudo journalctl -u lps-gateway -p err
```

#### 1.3 性能监控

使用 `dotnet-counters` 监控运行时性能：

```bash
# 安装工具
dotnet tool install --global dotnet-counters

# 监控进程
dotnet-counters monitor -n LpsGateway

# 监控特定指标
dotnet-counters monitor -n LpsGateway System.Runtime[cpu-usage,working-set]
```

### 2. Prometheus 集成（推荐）

#### 2.1 安装 Prometheus

```bash
# 下载 Prometheus
wget https://github.com/prometheus/prometheus/releases/download/v2.45.0/prometheus-2.45.0.linux-amd64.tar.gz
tar xvfz prometheus-*.tar.gz
cd prometheus-*

# 配置
cat > prometheus.yml <<EOF
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'lps-gateway'
    static_configs:
      - targets: ['localhost:5000']
    metrics_path: '/metrics'
EOF

# 启动
./prometheus --config.file=prometheus.yml
```

#### 2.2 关键指标

监控以下指标：

- `lps_gateway_tcp_connections_total`: TCP连接总数
- `lps_gateway_file_transfers_total`: 文件传输总数
- `lps_gateway_file_transfer_duration_seconds`: 文件传输时长
- `lps_gateway_sftp_downloads_total`: SFTP下载总数
- `lps_gateway_errors_total`: 错误总数
- `dotnet_total_memory_bytes`: 内存使用量
- `process_cpu_seconds_total`: CPU使用量

### 3. 告警规则

配置 Prometheus 告警规则：

```yaml
# alerts.yml
groups:
  - name: lps_gateway_alerts
    interval: 30s
    rules:
      - alert: HighErrorRate
        expr: rate(lps_gateway_errors_total[5m]) > 0.01
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "高错误率告警"
          description: "错误率超过阈值: {{ $value }}"
      
      - alert: HighMemoryUsage
        expr: process_resident_memory_bytes > 2e9
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "内存使用过高"
          description: "内存使用: {{ $value | humanize }}"
      
      - alert: ServiceDown
        expr: up{job="lps-gateway"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "服务不可用"
          description: "LPS Gateway 服务已停止"
      
      - alert: DiskSpaceLow
        expr: node_filesystem_avail_bytes{mountpoint="/opt/lps_gateway"} < 10e9
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "磁盘空间不足"
          description: "剩余空间: {{ $value | humanize }}"
```

## 故障排查

### 常见问题

#### 1. 服务无法启动

**症状**：systemctl start 失败

**排查步骤**：
```bash
# 检查服务状态
sudo systemctl status lps-gateway

# 查看详细日志
sudo journalctl -u lps-gateway -n 50

# 检查配置文件
dotnet /opt/lps_gateway/app/LpsGateway.dll --dry-run
```

**可能原因**：
- 端口已被占用
- 数据库连接失败
- 配置文件格式错误
- 权限问题

#### 2. 数据库连接失败

**症状**：日志显示数据库连接错误

**排查步骤**：
```bash
# 测试数据库连接
psql -h localhost -U lps_user -d lps_gateway

# 检查数据库服务状态
sudo systemctl status postgresql

# 检查防火墙
sudo ufw status
sudo firewall-cmd --list-all
```

**解决方案**：
- 验证连接字符串正确
- 确保数据库服务运行
- 检查防火墙规则
- 验证用户权限

#### 3. TCP 端口冲突

**症状**：IEC-102 服务无法监听端口2404

**排查步骤**：
```bash
# 检查端口占用
sudo netstat -tlnp | grep 2404
sudo ss -tlnp | grep 2404

# 查找占用进程
sudo lsof -i :2404
```

**解决方案**：
- 修改配置使用其他端口
- 停止冲突的进程
- 配置防火墙规则

#### 4. 文件传输失败

**症状**：主站无法接收文件

**排查步骤**：
```bash
# 检查文件记录
psql -U lps_user -d lps_gateway -c "SELECT * FROM FILE_RECORDS ORDER BY created_at DESC LIMIT 10;"

# 检查传输任务
psql -U lps_user -d lps_gateway -c "SELECT * FROM FILE_TRANSFER_TASKS WHERE status != 'completed' ORDER BY created_at DESC;"

# 查看传输日志
sudo journalctl -u lps-gateway | grep "FileTransfer"
```

**可能原因**：
- 网络连接问题
- 主站未正确请求数据
- 文件格式错误
- 传输超时

#### 5. SFTP 下载失败

**症状**：无法从 SFTP 服务器下载文件

**排查步骤**：
```bash
# 手动测试 SFTP 连接
sftp username@sftp-server

# 检查 SFTP 配置
psql -U lps_user -d lps_gateway -c "SELECT * FROM SFTP_CONFIGS;"

# 查看下载日志
sudo journalctl -u lps-gateway | grep "SFTP"
```

**可能原因**：
- 认证失败（密码或密钥错误）
- 网络不通
- 路径不存在
- 权限不足

### 日志分析

#### 重要日志关键字

- `ERROR`: 错误日志，需要立即关注
- `WARNING`: 警告日志，可能影响功能
- `FileTransfer`: 文件传输相关
- `SFTP`: SFTP 下载相关
- `Iec102`: IEC-102 协议相关
- `Database`: 数据库相关

#### 日志级别调整

临时调整日志级别（不重启服务）：

```bash
# 编辑配置文件
sudo nano /opt/lps_gateway/app/appsettings.json

# 修改日志级别为 Debug
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "LpsGateway": "Debug"
    }
  }
}

# 重新加载配置（需要支持）或重启服务
sudo systemctl restart lps-gateway
```

## 备份和恢复

### 1. 数据库备份

#### 每日自动备份

创建备份脚本 `/opt/lps_gateway/scripts/backup.sh`：

```bash
#!/bin/bash

BACKUP_DIR="/opt/lps_gateway/backups"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/lps_gateway_$DATE.sql.gz"

# 创建备份目录
mkdir -p $BACKUP_DIR

# 备份数据库
pg_dump -U lps_user -h localhost lps_gateway | gzip > $BACKUP_FILE

# 删除7天前的备份
find $BACKUP_DIR -name "lps_gateway_*.sql.gz" -mtime +7 -delete

echo "Backup completed: $BACKUP_FILE"
```

设置定时任务：

```bash
# 编辑 crontab
sudo crontab -e

# 添加每天凌晨2点执行备份
0 2 * * * /opt/lps_gateway/scripts/backup.sh
```

#### 手动备份

```bash
# 完整备份
pg_dump -U lps_user -h localhost lps_gateway > lps_gateway_backup.sql

# 压缩备份
pg_dump -U lps_user -h localhost lps_gateway | gzip > lps_gateway_backup.sql.gz
```

### 2. 数据恢复

```bash
# 从 SQL 文件恢复
psql -U lps_user -h localhost lps_gateway < lps_gateway_backup.sql

# 从压缩文件恢复
gunzip < lps_gateway_backup.sql.gz | psql -U lps_user -h localhost lps_gateway
```

### 3. 文件备份

备份下载的文件：

```bash
# 备份文件目录
rsync -avz /opt/lps_gateway/files/ /backup/lps_gateway/files/

# 使用 tar 打包
tar -czf lps_gateway_files_$(date +%Y%m%d).tar.gz /opt/lps_gateway/files/
```

## 性能优化

### 1. 数据库优化

#### 索引优化

```sql
-- 添加常用查询索引
CREATE INDEX IF NOT EXISTS idx_file_records_status ON FILE_RECORDS(status);
CREATE INDEX IF NOT EXISTS idx_file_records_created_at ON FILE_RECORDS(created_at);
CREATE INDEX IF NOT EXISTS idx_file_transfer_tasks_status ON FILE_TRANSFER_TASKS(status);
CREATE INDEX IF NOT EXISTS idx_audit_logs_created_at ON AUDIT_LOGS(created_at);
```

#### 连接池配置

在连接字符串中添加：

```
Host=localhost;Port=5432;Database=lps_gateway;Username=lps_user;Password=xxx;Minimum Pool Size=5;Maximum Pool Size=20;Connection Idle Lifetime=300
```

#### 定期维护

```bash
# 创建维护脚本
cat > /opt/lps_gateway/scripts/db_maintenance.sh <<'EOF'
#!/bin/bash
psql -U lps_user -d lps_gateway <<SQL
-- 更新统计信息
ANALYZE;

-- 清理死元组
VACUUM;

-- 重建索引（可选，定期执行）
-- REINDEX DATABASE lps_gateway;
SQL
EOF

# 设置每周执行
sudo crontab -e
# 添加：0 3 * * 0 /opt/lps_gateway/scripts/db_maintenance.sh
```

### 2. 应用优化

#### 内存限制

在 systemd 服务中设置：

```ini
[Service]
MemoryLimit=2G
MemoryHigh=1.5G
```

#### 线程池调优

```json
{
  "Quartz": {
    "quartz.threadPool.threadCount": "20"
  }
}
```

### 3. 网络优化

#### TCP 参数调优

```bash
# 编辑 /etc/sysctl.conf
sudo nano /etc/sysctl.conf

# 添加以下参数
net.core.rmem_max = 134217728
net.core.wmem_max = 134217728
net.ipv4.tcp_rmem = 4096 87380 67108864
net.ipv4.tcp_wmem = 4096 65536 67108864
net.ipv4.tcp_max_syn_backlog = 8192
net.core.somaxconn = 1024

# 应用配置
sudo sysctl -p
```

## 升级指南

### 1. 准备工作

```bash
# 备份数据库
pg_dump -U lps_user lps_gateway > backup_before_upgrade.sql

# 备份配置
cp /opt/lps_gateway/app/appsettings.json /opt/lps_gateway/appsettings.json.backup

# 备份应用
tar -czf lps_gateway_app_backup.tar.gz /opt/lps_gateway/app/
```

### 2. 升级步骤

```bash
# 停止服务
sudo systemctl stop lps-gateway

# 拉取最新代码
cd ~/lps_gateway
git pull origin main

# 编译新版本
dotnet publish src/LpsGateway.csproj -c Release -o /tmp/lps_gateway_new

# 备份旧版本
mv /opt/lps_gateway/app /opt/lps_gateway/app.old

# 部署新版本
mv /tmp/lps_gateway_new /opt/lps_gateway/app

# 恢复配置
cp /opt/lps_gateway/appsettings.json.backup /opt/lps_gateway/app/appsettings.json

# 应用数据库迁移（如有）
psql -U lps_user -d lps_gateway -f db/migrations/vX.X.X.sql

# 启动服务
sudo systemctl start lps-gateway

# 检查状态
sudo systemctl status lps-gateway
sudo journalctl -u lps-gateway -f
```

### 3. 回滚步骤

如果升级失败：

```bash
# 停止服务
sudo systemctl stop lps-gateway

# 恢复旧版本
rm -rf /opt/lps_gateway/app
mv /opt/lps_gateway/app.old /opt/lps_gateway/app

# 恢复数据库（如果执行了迁移）
psql -U lps_user -d lps_gateway < backup_before_upgrade.sql

# 启动服务
sudo systemctl start lps-gateway
```

## 安全建议

### 1. 系统安全

```bash
# 配置防火墙
sudo ufw enable
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 2404/tcp
sudo ufw allow from 192.168.1.0/24 to any port 5432

# 定期更新系统
sudo apt-get update && sudo apt-get upgrade
```

### 2. 应用安全

- 使用强密码
- 定期轮换数据库密码
- 使用 HTTPS（配置 SSL 证书）
- 限制管理员访问
- 启用审计日志
- 定期审查日志

### 3. 数据库安全

```bash
# 限制数据库访问
sudo nano /etc/postgresql/15/main/pg_hba.conf

# 只允许本地和特定IP访问
local   all             all                                     peer
host    lps_gateway     lps_user        127.0.0.1/32           md5
host    lps_gateway     lps_user        192.168.1.0/24         md5

# 重启数据库
sudo systemctl restart postgresql
```

## 联系支持

如遇到问题，请：

1. 查看本文档的故障排查部分
2. 检查 GitHub Issues: https://github.com/long1089/lps_gateway/issues
3. 查看项目文档: https://github.com/long1089/lps_gateway/tree/main/docs
4. 联系开发团队

---

**文档版本**：1.0
**最后更新**：2024-11
**维护者**：LPS Gateway Team
