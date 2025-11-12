# IEC-102 文件传输协议交互流程文档

## 概述

本文档详细描述LPS Gateway实现的IEC-102文件传输（新能源信息E文件）通讯流程，遵循 **"主站召唤 - 子站响应 - 分批传输 - 确认收尾"** 的核心协议规范。

## 一、前置条件：链路已初始化

在文件传输开始前，主站和子站之间必须完成链路初始化过程（参考协议5.1）：

1. **链路测试**：主站发送测试报文，验证通信正常
2. **复位链路**：主站发送复位指令（FC=0），子站回复确认
3. **就绪状态**：链路初始化完成，FCB状态清零，可开始数据交互

## 二、核心交互流程

### 阶段1：主站召唤数据，子站触发文件传输

#### 1.1 主站发起召唤

主站根据数据优先级发送召唤指令：

**召唤2级用户数据（常规数据）：**
```
帧格式：固定帧长帧
起始符：0x10
控制域：FC=11 (0x5B), PRM=1（主动端）, FCV=1（帧计数有效）, FCB=交替
地址域：站地址（2字节，小端序）
校验和：字节和取低8位
结束符：0x16
```

**召唤1级用户数据（优先数据）：**
```
帧格式：固定帧长帧
起始符：0x10
控制域：FC=10 (0x5A), PRM=1, FCV=1, FCB=交替
地址域：站地址（2字节）
校验和：字节和取低8位
结束符：0x16
```

#### 1.2 子站标识文件传输需求

子站根据待传输文件的数据类型，设置ACD标志：

**1级数据文件（优先数据）：**
- 包含：测风塔采集(0x9A)、短期预测(0x9B)、超短期预测(0x9C)、天气预报(0x9D)、气象站采集(0xA1)
- **ACD=1**：在响应2级召唤时设置，告知主站有1级数据待传

**2级数据文件（常规数据）：**
- 包含：风电场信息(0x95)、机组信息(0x96)、运行状态(0x97)等
- **ACD=0**：直接响应2级召唤，启动文件传输

### 阶段2：主站确认传输，子站分批发送文件

#### 2.1 主站召唤文件数据

根据子站ACD标志，主站发送对应召唤：

- **若ACD=1**：主站发送FC=10召唤1级数据，FCB状态切换
- **若ACD=0**：主站继续召唤2级数据，进入文件接收状态

#### 2.2 子站分帧发送文件

子站采用**可变帧长帧**传输文件：

**帧结构：**
```
起始符1：0x68
长度L：用户数据区字节数
起始符2：0x68
控制域：FC=8（用户数据响应）, PRM=0（从动端）, ACD, DFC
地址域：站地址（2字节）
用户数据区：
  ├─ TYP（类型标识，1字节）：0x95-0xA8
  ├─ VSQ（可变结构限定词，1字节）：长度+2
  ├─ COT（传送原因，1字节）：0x08（非最后帧）或0x07（最后帧）
  ├─ 公共地址（2字节）：站地址
  └─ 文件数据：
      ├─ 文件名字段（64字节，GBK编码）
      └─ 文件内容（≤512字节，GBK编码）
校验和：字节和取低8位
结束符：0x16
```

**关键字段说明：**

| 字段 | 说明 |
|------|------|
| **TYP** | 类型标识，根据报表类型映射（0x95-0xA8） |
| **COT** | 0x08=文件未传输结束，0x07=文件传输结束（最后一帧） |
| **ACD** | 1级文件时为1，2级文件时为0 |
| **DFC** | 0=缓冲区可用，1=缓冲区满（暂停接收） |
| **文件名** | 固定64字节，GBK编码，不足部分填充0x00 |
| **文件内容** | 每帧最多512字节，GBK编码 |

**分帧示例：**

假设文件"风电数据.txt"（1500字节）分3帧传输：

```
帧1：[文件名64字节] + [内容0-511]    → COT=0x08
帧2：[文件名64字节] + [内容512-1023] → COT=0x08
帧3：[文件名64字节] + [内容1024-1499] → COT=0x07（最后帧）
```

### 阶段3：主站校验文件，双方确认收尾

#### 3.1 主站校验文件

主站接收完所有帧后，进行以下校验：

1. **文件总长度校验**：累加所有帧的文件内容长度
2. **格式校验**：检查文件扩展名、风场标识等
3. **大小限制**：总长度≤20480字节（512×40）

#### 3.2 主站发送确认指令（可变帧长帧）

**校验通过（文件接收成功）：**
```
TYP=0x90（对账帧）
COT=0x0A（主站确认文件接收结束）
数据区：4字节文件长度（小端序）
```

**校验失败（错误类型）：**

| 错误类型 | TYP | COT | 说明 |
|---------|-----|-----|------|
| 文件过长 | 0x92 | 0x0F | 文件＞20480字节 |
| 格式错误 | 0x93 | 0x11 | 文件名格式不符合规范 |
| 单帧过长 | 0x94 | 0x13 | 单帧内容＞512字节 |

#### 3.3 子站回复确认（可变帧长帧）

**接收主站"校验通过"指令：**
```
TYP=0x90
COT=0x0B（确认文件传送成功）
ACD=0（1级文件传输完毕，清除标志）
```

**接收主站"校验失败"指令：**

| 主站COT | 子站响应COT | 说明 |
|---------|------------|------|
| 0x0F（文件过长） | 0x10 | 确认接收错误通知 |
| 0x11（格式错误） | 0x12 | 确认接收错误通知 |
| 0x13（单帧过长） | 0x14 | 确认接收错误通知 |
| 长度不匹配 | 0x0C | 准备重新传输 |

### 阶段4：异常处理（可选分支）

#### 4.1 主站超时未收帧

- 主站保留原FCB状态
- 重传召唤报文（最多3次）
- 超时后认为传输失败

#### 4.2 子站缓冲区满

- 子站设置**DFC=1**
- 主站暂停发送召唤指令
- 待子站DFC=0后恢复传输

#### 4.3 文件重传

**主站发送重传通知：**
```
TYP=0x91
COT=0x0D（通知重传）
```

**子站回复确认：**
```
TYP=0x91
COT=0x0E（通知重传确认）
```

随后子站重新分帧传输文件。

## 三、关键交互规则总结

### 3.1 帧类型

- **文件传输**：均使用可变帧长帧（0x68起始符）
- **主站召唤**：使用固定帧长帧（0x10起始符）
- **最大帧长**：用户数据区≤255字节

### 3.2 核心标识

| 标识 | 说明 |
|------|------|
| **ACD=1** | 子站触发1级文件传输（在响应2级召唤时设置） |
| **COT=0x08** | 文件未传输结束（非最后帧） |
| **COT=0x07** | 文件传输结束（最后帧） |
| **DFC=1** | 子站缓冲区满，主站暂停召唤 |
| **FCB** | 帧计数位，主站每次有效数据帧切换 |

### 3.3 长度限制

| 限制项 | 最大值 | 超限处理 |
|-------|-------|---------|
| 单帧文件内容 | 512字节 | 主站拒绝（COT=0x13） |
| 文件总大小 | 20480字节（512×40） | 主站拒绝（COT=0x0F） |
| 文件名长度 | 64字节（GBK编码） | 超长截断或填充 |

### 3.4 校验机制

1. **帧校验和**：每帧字节累加取低8位
2. **文件长度校验**：主站累加所有帧的内容长度
3. **格式校验**：主站检查文件扩展名、标识符等

## 四、代码实现对照

### 4.1 COT常量定义

```csharp
public static class CauseOfTransmission
{
    public const byte FileTransferComplete = 0x07;     // 文件传输结束
    public const byte FileTransferInProgress = 0x08;   // 文件未传输结束
    public const byte ReconciliationFromMaster = 0x0A; // 主站确认接收
    public const byte ReconciliationFromSlave = 0x0B;  // 子站确认成功
    public const byte ReconciliationReconfirm = 0x0C;  // 准备重传
    public const byte RetransmitNotification = 0x0D;   // 通知重传
    public const byte RetransmitNotificationAck = 0x0E;// 重传确认
    public const byte FileTooLongError = 0x0F;         // 文件过长错误
    public const byte FileTooLongAck = 0x10;           // 文件过长确认
    public const byte InvalidFileNameFormat = 0x11;    // 文件名格式错误
    public const byte InvalidFileNameFormatAck = 0x12; // 格式错误确认
    public const byte FrameTooLongError = 0x13;        // 单帧过长错误
    public const byte FrameTooLongAck = 0x14;          // 单帧过长确认
}
```

### 4.2 文件分段发送

```csharp
// 创建文件分段（64字节文件名 + ≤512字节内容）
private List<byte[]> CreateFileSegments(string filename, byte[] fileContent)
{
    const int FileNameFieldSize = 64;
    const int MaxSegmentSize = 512;
    
    // GBK编码文件名
    var gbk = Encoding.GetEncoding("GBK");
    byte[] filenameBytes = new byte[FileNameFieldSize];
    byte[] encodedName = gbk.GetBytes(filename);
    Array.Copy(encodedName, filenameBytes, Math.Min(encodedName.Length, FileNameFieldSize));
    
    var segments = new List<byte[]>();
    int offset = 0;
    
    while (offset < fileContent.Length)
    {
        int segmentDataSize = Math.Min(MaxSegmentSize, fileContent.Length - offset);
        byte[] segment = new byte[FileNameFieldSize + segmentDataSize];
        
        // 复制文件名字段
        Array.Copy(filenameBytes, 0, segment, 0, FileNameFieldSize);
        
        // 复制数据字段
        Array.Copy(fileContent, offset, segment, FileNameFieldSize, segmentDataSize);
        
        segments.Add(segment);
        offset += segmentDataSize;
    }
    
    return segments;
}
```

### 4.3 发送文件分段

```csharp
for (int i = 0; i < segments.Count; i++)
{
    bool isLastSegment = (i == segments.Count - 1);
    byte cot = isLastSegment 
        ? CauseOfTransmission.FileTransferComplete    // 0x07
        : CauseOfTransmission.FileTransferInProgress; // 0x08
    
    // 排队到对应数据级别队列
    if (fileTask.IsClass1)
    {
        session.QueueClass1Data(new QueuedFrame 
        { 
            TypeId = typeId, 
            Cot = cot, 
            Data = segments[i] 
        });
    }
    else
    {
        session.QueueClass2Data(new QueuedFrame 
        { 
            TypeId = typeId, 
            Cot = cot, 
            Data = segments[i] 
        });
    }
}
```

### 4.4 处理主站确认

```csharp
// 处理主站对账确认（COT=0x0A）
private async Task HandleFileReconciliationAsync(ClientSession session, byte[] userData)
{
    // userData包含4字节文件长度
    if (userData.Length >= 10) // TypeId(1) + VSQ(1) + COT(1) + ADDR(2) + Length(4)
    {
        int fileLength = BitConverter.ToInt32(userData, 5);
        _logger.LogInformation("主站确认文件接收: 长度={Length}字节", fileLength);
        
        // 子站回复确认（COT=0x0B）
        byte[] ackData = new byte[4];
        BitConverter.GetBytes(fileLength).CopyTo(ackData, 0);
        
        session.QueueClass2Data(new QueuedFrame
        {
            TypeId = 0x90,
            Cot = CauseOfTransmission.ReconciliationFromSlave, // 0x0B
            Data = ackData
        });
    }
}
```

## 五、时序图

```
主站                                      子站（从站）
 │                                         │
 │─────── FC=11 召唤2级数据 ──────────────>│
 │                                         │ 检查待传输文件
 │                                         │ ├─ 1级文件：设置ACD=1
 │                                         │ └─ 2级文件：准备传输
 │<──────── FC=8 响应 (ACD=1/0) ──────────│
 │                                         │
 │─────── FC=10 召唤1级数据 ──────────────>│ （若ACD=1）
 │                                         │
 │<────── 帧1: TYP, COT=0x08, 文件段1 ─────│
 │                                         │
 │─────── FC=10/11 继续召唤 ──────────────>│
 │                                         │
 │<────── 帧2: TYP, COT=0x08, 文件段2 ─────│
 │                                         │
 │─────── FC=10/11 继续召唤 ──────────────>│
 │                                         │
 │<────── 帧N: TYP, COT=0x07, 文件段N ─────│ （最后一帧）
 │                                         │
 │ 校验文件长度、格式                       │
 │                                         │
 │─────── TYP=0x90, COT=0x0A, 长度 ───────>│ 校验通过
 │                                         │
 │<────── TYP=0x90, COT=0x0B ─────────────│ 子站确认成功
 │                                         │
 │ 文件传输完成                             │
```

## 六、测试验证要点

### 6.1 正常流程测试

- [ ] 2级数据文件传输（单帧）
- [ ] 2级数据文件传输（多帧）
- [ ] 1级数据文件传输（ACD标志正确）
- [ ] 中文文件名正确编码（GBK）
- [ ] 中文文件内容正确编码（GBK）

### 6.2 边界条件测试

- [ ] 文件大小=512字节（单帧边界）
- [ ] 文件大小=513字节（两帧边界）
- [ ] 文件大小=20480字节（最大限制）
- [ ] 文件名=64字节（最大长度）

### 6.3 异常场景测试

- [ ] 文件过大（＞20480字节）：主站返回COT=0x0F
- [ ] 单帧过大（＞512字节）：主站返回COT=0x13
- [ ] 文件名格式错误：主站返回COT=0x11
- [ ] 主站超时重传
- [ ] 子站缓冲区满（DFC=1）

## 七、参考文档

- IEC 60870-5-102 协议标准
- `docs/IEC102-Extended-Doc.md` - 扩展协议文档
- `docs/IEC102-Extended-ASDU-Spec.md` - ASDU规范
- `src/Lib60870/CauseOfTransmission.cs` - COT常量定义
- `src/Lib60870/DataClassification.cs` - 数据分类定义

## 八、版本历史

| 版本 | 日期 | 说明 |
|-----|------|------|
| 1.0 | 2025-11-12 | 初始版本，完整的文件传输协议流程文档 |

---

**文档维护者**: LPS Gateway开发团队  
**最后更新**: 2025-11-12
