# 实现路线与里程碑

M0（1周）：需求与设计冻结
- 明确扩展 ASDU（时间同步、点播/取消），对齐文件名长度（64B）。
- 数据模型设计与索引方案；API 草图与权限模型。

M1（2周）：项目骨架与基础设施
- .NET 6 MVC 项目/分层；SqlSugar + OpenGauss 连接与迁移；Auth（JWT/Role）。
- 配置管理 UI：ReportType、SftpConfig、Schedule 基本 CRUD。

M2（2周）：调度与 SFTP
- Quartz.NET 集成（或轻量 Cron）；分布式锁（PG advisory/Redis）。
- SftpManager：密码/私钥认证、动态路径模板、流式下载；FileRecord 元数据持久化。
- 手动补报 API/按钮。

M3（2周）：TCP Server 与协议栈基础
- 异步 TCP Server；控制帧/固定帧与可变帧收发；FCB/FCV/ACD/DFC 处理。
- 扩展：时间同步（TYP=0x8B），协议日志。

M4（2周）：文件传输通道
- 文件片段上送（TYP=0x95–0xA8，64B 文件名 + ≤512B 片段）；对账帧 0x90。
- 重传/长度/文件名错误控制（0x91–0x94）；FileTransferTask 工作器与背压。

M5（1周）：保留与可观测性
- Retention Worker；Prometheus 指标、Grafana 看板；操作审计；磁盘水位告警。

M6（1周）：联调与性能/容灾测试
- 与主站联调；并发/带宽/DB 压测；异常恢复（SFTP 断线、会话重连、任务重放）。
- 文档与运维手册，发布版本。

交付物
- docs/Architecture-Design.md、docs/IEC102-Extended-ASDU-Spec.md
- 协议与 SFTP 代码骨架、SqlSugar 实体与迁移脚本
- 仪表盘与告警规则