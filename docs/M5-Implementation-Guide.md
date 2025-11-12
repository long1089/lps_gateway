# M5 实现指南：保留与可观测性

## 概述

M5 里程碑专注于系统的可观测性和数据保留策略，实现了完整的仪表盘、审计日志系统和自动文件清理功能。

## 主要功能

### 1. 仪表盘（Dashboard）

实现了一个全面的系统监控仪表盘，显示系统运行状态和关键指标。

#### 主要组件

**DashboardService**
- 位置：`src/Services/DashboardService.cs`
- 功能：聚合系统各个模块的数据，提供统一的仪表盘视图
- 接口：`IDashboardService`

**DashboardViewModel**
- 位置：`src/Models/DashboardViewModel.cs`
- 功能：包含仪表盘所需的所有数据模型
- 子模型：
  - `SystemStatusModel`：系统状态（文件数、任务数、运行时间）
  - `FileDownloadRecordModel`：文件下载记录
  - `FileTransferRecordModel`：文件传输记录
  - `ErrorAlertModel`：错误告警
  - `CommunicationStatusModel`：通讯状态
  - `DiskUsageModel`：磁盘使用情况

#### 主要指标

1. **系统状态**
   - 总下载文件数
   - 今日下载文件数
   - 总传输任务数
   - 待处理/进行中/失败任务数
   - 系统运行时间

2. **通讯状态**
   - IEC-102 从站运行状态
   - IEC-102 主站运行状态
   - 活跃连接数
   - 今日发送/接收帧数
   - 最后活动时间

3. **磁盘使用**
   - 总空间、已使用空间、可用空间
   - 使用率百分比
   - 警告阈值（80%）
   - 严重阈值（90%）

4. **错误告警**
   - 磁盘空间告警
   - 文件下载失败告警
   - 文件传输失败告警

### 2. 审计日志系统（Audit Logs）

实现了完整的操作审计日志记录和查询功能。

#### 主要组件

**AuditLogRepository**
- 位置：`src/Data/AuditLogRepository.cs`
- 功能：审计日志的持久化和查询
- 接口：`IAuditLogRepository`

**AuditLogsController**
- 位置：`src/Controllers/AuditLogsController.cs`
- 功能：审计日志的管理界面（仅管理员可访问）
- 路由：`/AuditLogs`

#### 主要功能

1. **日志记录**
   - 用户ID、操作类型、资源标识
   - 详细信息（JSON格式）
   - IP地址、时间戳

2. **日志查询**
   - 按操作类型筛选
   - 按用户ID筛选
   - 按时间范围查询
   - 分页显示

3. **统计报表**
   - 最近30天操作统计
   - 按操作类型分组统计
   - 可视化显示比例

### 3. 保留策略工作器（Retention Worker）

实现了自动清理过期文件的后台服务。

#### 主要组件

**RetentionWorkerHostedService**
- 位置：`src/HostedServices/RetentionWorkerHostedService.cs`
- 功能：定期检查并清理过期文件
- 类型：`IHostedService` 后台服务

#### 工作原理

1. **定时检查**
   - 默认每60分钟检查一次
   - 可通过配置调整间隔

2. **清理逻辑**
   - 查找 `RetentionExpiresAt < 当前时间` 的文件
   - 跳过正在处理的文件（status = processing/in_progress）
   - 删除物理文件
   - 删除数据库记录

3. **日志记录**
   - 清理开始/结束日志
   - 成功/失败统计
   - 详细的错误日志

## 配置说明

### appsettings.json

```json
{
  "Retention": {
    "CheckIntervalMinutes": 60,      // 检查间隔（分钟）
    "DefaultRetentionDays": 30       // 默认保留天数
  }
}
```

### 服务注册

在 `Program.cs` 中注册服务：

```csharp
// M5 repositories
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// M5 services
builder.Services.AddScoped<IDashboardService, DashboardService>();

// M5 hosted services
builder.Services.AddHostedService<RetentionWorkerHostedService>();
```

## 使用说明

### 1. 访问仪表盘

访问首页即可看到仪表盘：
```
http://localhost:5000/
```

仪表盘显示：
- 系统状态卡片（4个）
- 通讯状态与磁盘使用
- 最新文件下载记录（10条）
- 最新文件传输记录（10条）
- 错误告警（如有）
- 管理快捷入口

### 2. 查看审计日志

仅管理员可访问：
```
http://localhost:5000/AuditLogs
```

功能：
- 查看所有审计日志
- 按操作类型筛选
- 调整显示数量
- 查看详细信息（JSON格式）

查看统计报表：
```
http://localhost:5000/AuditLogs/Statistics
```

### 3. 配置保留策略

在数据库中设置文件的保留期限：

```sql
-- 设置文件在30天后过期
UPDATE file_records
SET retention_expires_at = NOW() + INTERVAL '30 days'
WHERE id = 123;
```

保留工作器会自动清理过期的文件。

## UI 特性

### 仪表盘特性

1. **响应式设计**
   - 适配桌面和移动设备
   - Bootstrap 5 卡片布局

2. **颜色编码**
   - 成功：绿色
   - 警告：黄色
   - 错误：红色
   - 信息：蓝色

3. **实时数据**
   - 刷新按钮立即更新数据
   - 支持浏览器刷新

4. **进度可视化**
   - 磁盘使用进度条
   - 传输任务进度显示
   - 状态徽章

### 审计日志特性

1. **筛选功能**
   - 操作类型下拉选择
   - 显示数量调整
   - 一键筛选

2. **详情查看**
   - 模态框显示JSON详情
   - 格式化显示

3. **统计可视化**
   - 进度条显示比例
   - 总计统计

## 数据库表

### audit_logs 表

已在 `db/schema.sql` 中定义：

```sql
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
```

索引：
- `idx_audit_user`：用户ID索引
- `idx_audit_action`：操作类型索引
- `idx_audit_created`：创建时间索引

## 测试

### 单元测试

位置：`tests/M5Tests.cs`

包含测试：
- 磁盘使用阈值测试
- 审计日志创建测试
- 视图模型属性测试
- 仪表盘服务测试

运行测试：
```bash
dotnet test
```

### 手动测试

1. **仪表盘测试**
   - 启动应用
   - 访问首页
   - 验证各个指标显示
   - 测试刷新功能

2. **审计日志测试**
   - 以管理员身份登录
   - 访问审计日志页面
   - 测试筛选功能
   - 查看统计报表

3. **保留策略测试**
   - 创建过期文件记录
   - 等待保留工作器运行
   - 验证文件被清理
   - 检查日志输出

## 性能考虑

### 仪表盘性能

1. **数据聚合**
   - 使用数据库索引优化查询
   - 限制返回记录数（默认10条）
   - 避免复杂的联表查询

2. **缓存策略**
   - 可添加内存缓存减少数据库查询
   - 建议缓存时间：1-5分钟

### 保留工作器性能

1. **批量处理**
   - 分批删除文件避免长时间锁定
   - 每批处理后短暂休眠

2. **错误恢复**
   - 单个文件失败不影响其他文件
   - 详细记录失败原因

## 扩展建议

### 1. Prometheus 指标

可以添加 Prometheus 指标导出：

```csharp
// 安装 prometheus-net
dotnet add package prometheus-net.AspNetCore

// 在 Program.cs 中添加
app.UseMetricServer();
app.UseHttpMetrics();
```

### 2. Grafana 仪表盘

使用 Prometheus 数据源创建 Grafana 仪表盘：
- 文件下载趋势图
- 传输任务状态饼图
- 磁盘使用率时间序列
- 错误率告警

### 3. 告警通知

实现告警通知功能：
- 邮件通知
- 钉钉/企业微信机器人
- SMS 短信通知

### 4. 审计日志增强

- 添加更多审计点
- 实现审计日志导出
- 添加审计日志搜索功能

## 故障排查

### 仪表盘无数据

1. 检查数据库连接
2. 验证相关表是否有数据
3. 查看应用日志

### 保留工作器不运行

1. 检查配置：`Retention:CheckIntervalMinutes`
2. 查看应用日志：是否有启动日志
3. 验证文件的 `retention_expires_at` 字段

### 审计日志无法访问

1. 确认用户角色为 Admin
2. 检查路由配置
3. 验证数据库表是否存在

## 总结

M5 里程碑成功实现了：

✅ 完整的系统仪表盘
✅ 实时状态监控
✅ 审计日志系统
✅ 自动文件清理
✅ 错误告警机制
✅ 磁盘使用监控

为系统的可观测性和运维管理提供了坚实的基础。
