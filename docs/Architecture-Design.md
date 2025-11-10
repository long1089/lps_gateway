# 新能源网关机架构设计（.NET 6 + MVC + SqlSugar + OpenGauss）

目标
- 定时/手动通过 SFTP 下载“E 文件”，本地存储与保留。
- 提供 Web（浏览器）配置：上报时点、SFTP 参数、路径模板、历史记录、手动补报。
- 建立 TCP Server，按 IEC60870-5-102（扩展 ASDU）与主站交互：时间同步、文件上送、重传/校验。
- 支持 1/2 级用户数据调度，文件分段传输与异常处理。

一、核心模块拆分
- Web/API（MVC）
  - UI 配置、历史记录、手动补报、健康看板；REST API 给运维使用。
- Auth
  - 登录、角色、API Token、操作审计；敏感配置权限隔离。
- 配置管理（System Settings）
  - ReportType、Schedule（每日/每月/自定义）、SftpConfig（账号/证书/路径模板）、保留策略（默认 30 天）。
- 调度引擎（Scheduler）
  - 分钟级触发下载/补报；分布式锁确保单次执行；失败重试与告警。
- SFTP 管理器（SftpManager）
  - 支持密码/私钥认证；动态路径模板（{yyyy}/{MM}/{dd}/{HH}{mm}）；流式下载、并发限流、断点续传（可选）。
- 文件存储与管理（FileStorage）
  - 本地/NFS/对象存储；文件元数据 FileRecord（路径、大小、MD5、状态、过期时间）；清理任务。
- TCP Server（NetworkServer）
  - 异步 Socket，连接/会话管理、超时与限流。
- IEC60870-5-102 协议处理（ProtocolHandler）
  - 帧解析/封包、序列/确认、扩展 ASDU：时间同步、文件点播/取消、文件片段上送、对账与重传。
- 任务队列与工作器（Task Queue / Workers）
  - 下载任务、文件发送任务解耦；进程内 Channel 起步，生产用 MQ（RabbitMQ/Redis Streams）。
- 数据访问层（DAL, SqlSugar）
  - OpenGauss（PG 协议）CRUD、事务、批量插入；表分区与索引优化。
- 日志与可观测性（Observability）
  - 协议日志、操作日志、下载/传输指标；Prometheus 指标、Grafana 看板、ELK 日志。
- 安全与证书（Security）
  - 凭据加密（KMS/DPAPI）、最小权限、审计；TCP 可选 TLS/隧道。
- 运维（Ops）
  - 健康检查、磁盘水位告警、任务重放、备份策略。
- 测试与模拟（Simulator）
  - IEC102 客户端模拟器、SFTP 测试服务；自动化联调。

二、模块交互方式
- Web -> API：同步 REST；触发后台任务（异步排队）。
- Scheduler -> Queue：异步发布“下载/补报任务”。
- SftpManager Worker -> FileStorage：流式写文件，同步；元数据落库异步批量写。
- ProtocolHandler <-> TCP Client：帧级同步（会话内），业务任务入队异步执行（文件发送）。
- Retention Worker：定时异步清理。
- 分布式协调：Redis/PG advisory lock 防止重复执行。

三、关键数据模型（要点）
- ReportType(id, code, name, default_sftp_config_id, created_at, updated_at)
- Schedule(id, report_type_id, type(daily|monthly|cron), times(json), month_days(json), timezone, enabled)
- SftpConfig(id, host, port, username, auth_type, password_enc, key_path, key_passphrase_enc, base_path_template, concurrency_limit, timeout_sec)
- FileRecord(id, report_type_id, sftp_config_id, original_filename, storage_path, file_size, md5_hash, download_time, status, retention_expires_at)
- ProtocolCommandLog/TcpSessionLog(frame_hex, typ, cot, result, ts, details json)
- FileTransferTask(id, file_record_id, session_id, status, progress, created/started/completed_at)

四、调度与路径模板
- 时点精度：分钟；每日 times=["08:00","11:15"]；每月 month_days=[1,10,20] + times。
- 动态路径：/reports/{yyyy}/{MM}/{dd}/、/EFJ/{yyyyMMdd}/ 等；tokens: {yyyy},{MM},{dd},{HH},{mm}。
- 时间：后端统一 UTC 存储，前端按时区显示；DST 用 tz database。

五、IEC60870-5-102 扩展（概要）
- 传输：非平衡模式，TCP 端口 3000；可变帧 68 LL 68 C ALo AHi ASDU CHK 16。
- 文件片段 TYP：0x95–0xA8（映射 19 类 E 文件），每帧 64B 文件名 + ≤512B 内容；COT=0x08 非末段，0x07 末段。
- 控制/对账：0x90–0x94（结束确认、重传、超长、文件名错误、单帧过长）。
- 扩展指令：
  - 时间同步 TYP=0x8B（CP56Time2a，激活/确认/结束）。
  - 文件点播 TYP=0x8D（按 ReportTypeCode 和最新/时间范围）。
  - 取消点播 TYP=0x8E。
- 1 级数据：短期/超短期/天气/采集类；ACD=1 提示主站召唤 1 级（FC=10）。

六、性能瓶颈与对策
- SFTP 并发/带宽/磁盘：并发限流、流式 IO、分布式 worker、连接重用与限速。
- DB 吞吐：批量/异步写、表分区（按日期）、热点索引优化、PgBouncer。
- TCP/协议 CPU：SocketAsyncEventArgs、解析线程池、零拷贝发送、背压。
- 任务唯一性：分布式锁/MQ 唯一消费。
- 磁盘空间：保留策略+对象存储/NFS，水位告警。
- 凭据风险：KMS 加密、最小权限、审计。

七、部署与运维
- 进程化或微服务：Web/API、Scheduler、SFTP Worker、TCP Server 可独立部署。
- 容器化：Docker + K8s；ConfigMap/Secret 管理配置；滚动升级。
- 监控：Prometheus 指标（下载速率、队列长度、TCP 连接数、错误率、磁盘使用）；Grafana 告警。

八、测试策略
- 单测：协议帧解析状态机、调度、模板解析。
- 集成：SFTP docker 容器、IEC102 模拟器。
- 性能：并发连接/大文件下载/DB 吞吐压测。
- 容错：网络抖动、磁盘满、DB 不可用。

附录：主要 API（示例）
- GET /api/reporttypes
- POST /api/reporttypes/{id}/schedules
- POST /api/reporttypes/{id}/trigger
- GET /api/filerecords?type=&from=&to=
- POST /api/sftpconfigs
