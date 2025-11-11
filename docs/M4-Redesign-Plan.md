# M4 Communication Framework Redesign Plan

## Problem Statement

The current M4 implementation has fundamental flaws in understanding the IEC-102 protocol flow:

1. **Incorrect queue management**: Uses global queues instead of per-session queues
2. **Wrong data classification**: Doesn't properly distinguish Class 1 vs Class 2 data
3. **Missing ACD flag management**: Doesn't properly set ACD flags for Class 1 data availability
4. **No multi-client support**: Cannot handle multiple master stations connecting simultaneously

## Correct IEC-102 Protocol Flow

### Master-Slave Interaction Sequence

1. **Master calls Class 2 data** (FC=11)
   - If slave has Class 2 data → send it with FC=8
   - If no Class 2 data → respond with "no data available" (FC=9)
   - If slave has Class 1 data waiting → set ACD=1 in response

2. **Master switches to Class 1** (when ACD=1 detected)
   - Master sends FC=10 to request Class 1 data
   - Slave sends Class 1 data frames with FC=8
   - Intermediate frames: ACD=1, COT=9
   - Last frame: ACD=0, COT=7

3. **File Transfer Error Handling**
   - Master sends error notification: TI=144, COT=10, FC=3
   - Slave acknowledges error: TI=144, COT=11, FC=0, ACD=1
   - Master re-requests Class 1 data: FC=10
   - Slave confirms transfer end: TI=144, COT=11, FC=0, ACD=0

## Data Classification

### Class 1 Data (Priority, needs ACD flag)
- 0x9A: EFJ_FIVE_WIND_TOWER (测风塔采集数据)
- 0x9B: EFJ_DQ_RESULT_UP (短期预测)
- 0x9C: EFJ_CDQ_RESULT_UP (超短期预测)
- 0x9D: EFJ_NWP_UP (天气预报)
- 0xA1: EGF_FIVE_GF_QXZ (气象站采集数据)

### Class 2 Data (Regular)
All other types (0x95-0x9F, 0xA0, 0xA2-0xA8 except above)

## Required Changes

### 1. Iec102Slave.cs
- [x] Move data queues from global to per-session (ClientSession class)
- [x] Add per-session queue management methods
- [x] Update HandleClass1DataRequestAsync to use session queues and set ACD flags
- [x] Update HandleClass2DataRequestAsync to use session queues and set ACD flags
- [x] Update SendUserDataAsync to accept ACD parameter
- [x] Update SendNoDataAsync to accept ACD parameter
- [x] Add methods: QueueClass1DataToSession, QueueClass2DataToSession, QueueClass1DataToAll, QueueClass2DataToAll
- [ ] Remove old QueueClass1Data and QueueClass2Data methods

### 2. FileTransferManager (New Design)
- [ ] Create session-aware file transfer manager
- [ ] Implement proper data classification (isClass1Data method)
- [ ] Handle file segmentation with correct COT codes (COT=9 for intermediate, COT=7 for last)
- [ ] Implement file transfer state machine
- [ ] Handle TI=144 error control protocol

### 3. File Transfer State Machine
States:
- Idle
- Transferring
- WaitingForReconciliation
- Error
- Completed

Transitions:
- Idle → Transferring: When file is queued
- Transferring → WaitingForReconciliation: After last segment sent
- WaitingForReconciliation → Completed: Master confirms (TI=144, COT=11)
- Any → Error: On validation failure or master error signal
- Error → Transferring: On retransmission request

### 4. Testing
- [ ] Update existing tests to use new queue methods
- [ ] Add tests for per-session queue behavior
- [ ] Add tests for ACD flag management
- [ ] Add tests for Class 1/Class 2 classification
- [ ] Add tests for multi-client scenarios

## Reference Implementation

Based on lib60870.NET CS101 approach:
- Session-based queue management
- State machine for connection management
- Proper ACD/FCB handling
- Event-driven architecture for file transfer callbacks

## Implementation Status

- [x] Analyzed problem and created redesign plan
- [x] Modified ClientSession to have per-session queues
- [x] Updated Iec102Slave queue management methods
- [x] Updated test to use new API
- [ ] Delete old FileTransferWorker implementation
- [ ] Create new FileTransferManager with proper design
- [ ] Implement file transfer state machine
- [ ] Add comprehensive tests
- [ ] Update documentation

## Notes

- The user mentioned TI=155/156 which might be different from our current TYP=0x95-0xA8 mapping
- Need to clarify if there's a different TypeId mapping for file content vs file metadata
- COT codes: COT=7 (last frame), COT=9 (intermediate frame), COT=10 (error from master), COT=11 (ack from slave)
