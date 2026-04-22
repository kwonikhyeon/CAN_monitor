# Phase 2b Preparation Notes

Status: **Q1/Q3 확정, Phase 2b 구현 완료 (2026-04-22)**

Phase 2a 완료(commit cc71e6b) 이후, Q1(EEC1 payload)/Q3(virtual-input) 해제를 위한 선행 의사결정과 실험 산출물을 기록한다. 모두 문서 추정/자가 정의 기반이며, 제조사 공식 답변 수신 시 교체 필요.

---

## Q1: EEC1 (0x18F00417) Payload Hypothesis

### Excel 분석 결과 (`data/120HP_TCU_CAN_Protocol_Updated_v241107.xlsx`)

| No | Signal | Byte | Bit | Length (문서) | Operating Range |
|----|--------|------|-----|----|------|
| 1  | EEC1_Low  | 0 | 3 | "1 bit" | 11 codes 0000b..1010b |
| 2  | EEC1_High | 0 | 4 | "1 bit" | 11 codes 0000b..1010b |
| 29 | EL0601_EEC1_Timeout | 3 | 1 | "1 bit" | — (TCU Tx 알람) |

**핵심 관찰:**

- Length="1 bit"는 문서 오류(Operating Range의 11개 코드는 4 bit 필요).
- EEC1_Low / EEC1_High는 Byte 0를 2개 nibble로 분할(low nibble + high nibble).
- TCU=Rx, Display=Tx → PC → TCU 하트비트 역할(송신 주기 100ms).
- Payload는 PSV(Proportional Solenoid Valve) driver fault code (J1939 표준 EEC1의 RPM/Torque와 무관, 제조사 custom).
- 코드 테이블: 0=No Failure … 10=PSV Short batt/open/internal SC.

### Q5 (엔디안) 연관

Excel Bit 번호는 LSB 기준으로 읽힌다(3 = low nibble LSB, 4 = high nibble LSB). DBC Motorola/Intel 양쪽으로 표현 가능하며, 현장 장비가 어떤 바이트 순서를 기대하는지 캡처 전에는 확정 불가. → **두 변종 DBC를 준비하고 런타임 전환 지원**으로 결정.

### Byte 1~7 Reserved 재조사 (2026-04-22)

`0x18F00417` 그룹 실제 행 = 2개 신호 + 10개 Note 주석. Byte 1~7 신호 **Excel에 정의 없음**. 가능성 C(제조사 커스텀 신호) 기각.

Note 5번 핵심 단서:
> "Our TCU needs to get information from engine **through VCU** (Please look at Alarm No 29)."

- EEC1은 엔진ECU 직송이 아닌 **VCU 중계**, 용도는 **PSV driver fault 전용**으로 재정의 → 가능성 B(J1939 표준 RPM/Torque) 약화.
- **Byte 1~7 = Reserved 0x00 고정**으로 확정. 단, VCU가 실제로 다른 byte를 채울 가능성 대비 `Eec1HeartbeatProvider`는 **byte/bit 단위 payload override 가능**하게 설계.

### Q1 확정사항 요약 (2026-04-22)

| 항목 | 결정 | 근거 |
|------|------|------|
| 엔디안 | Motorola/Intel 두 변종 DBC 병행, 런타임 선택 | 추정 방식, 현장 캡처 전환 용이성 우선 |
| Byte 0 | Low(bits 3..0) + High(bits 7..4) nibble = PSV fault code | Excel No 1/2 + ODR 11 codes |
| Byte 1~7 | Reserved 0x00 고정, DSL override 가능 | Excel 신호 정의 없음 + VCU 중계형 |
| 송신 주기 | 100 ms | Excel Rep.rate |
| EEC1 Timeout | spec §6 값 그대로 사용 | 결정 유보 기각 |
| 초기 payload | 8 byte 전부 0x00 (No Failure) 부팅 직후 송출 | 기본 안전 상태 |

---

## 산출물: 실험용 DBC 2개 변종

`dbc/experimental/`:

- `eec1_emulation.motorola.dbc`
  - `BO_ 2565866519 EEC1: 8 Vector__XXX` (0x18F00417 | 0x80000000)
  - `SG_ EEC1_High : 7|4@0+ (1,0) [0|10]` — Motorola start=7 (MSB)
  - `SG_ EEC1_Low : 3|4@0+ (1,0) [0|10]` — Motorola start=3 (MSB)
  - `VAL_` 11개 fault code entries (0..10)
  - `CM_ SG_` 주석: Motorola start=MSB vs Excel Bit=LSB 혼동 방지

- `eec1_emulation.intel.dbc`
  - 동일 BO_ / VAL_
  - `SG_ EEC1_High : 4|4@1+` / `SG_ EEC1_Low : 0|4@1+` — Intel start=LSB(Excel Bit과 일치)

`GenMsgCycleTime`은 생략. 100ms는 `CM_ BO_`에 주석 + `Eec1HeartbeatProvider` 코드에서 강제.

검증: `tests/Dbc.Tests/Eec1ExperimentalDbcTests.cs` — 두 변종 모두 `DbcParserLib`가 `(StartBit, Length, LittleEndian)` 튜플을 기대값대로 반환하는지 확인.

---

## Q3: Virtual Input (0x18FF5080) Self-Defined Spec

### 배경 및 경로 선택

Excel 재조사 결과 **PC→TCU 방향 메시지는 0x18F00417(EEC1) 단 1개뿐**이고, Simulation Mode 가상 입력용 CAN 메시지는 **문서에 전혀 없음**. 제조사 공식 답변을 기다리는 대신 **경로 C: 자가 정의 설계** 채택.

- 실 TCU 수신 불가 가정 → **Simulator + VirtualCanBus 전용**
- 제조사 답변 수신 시 1 commit으로 ID/비트맵 교체

### CAN ID = 0x18FF5080 (J1939 Proprietary B)

29비트 분해:
| Priority | EDP | DP | PF | PS | SA |
|----------|-----|----|----|----|----|
| 6 (0b110) | 0 | 0 | 0xFF (PDU2 broadcast) | 0x50 | 0x80 (PC) |

PGN = 0xFF50 (Proprietary B 자유 할당 영역). 기존 메시지(0x0C000E00/01/02, 0x185, 0x200, 0x18F00417) 전부 충돌 없음.

### 비트맵 (DLC=8, 50 ms 주기)

spec §17 `SetVirtualInputStep` 11개 필드 전부 수용.

| Byte | Bits | Signal | Enum/Range |
|------|------|--------|------------|
| 0 | 1..0 | GearLever | 0=Neutral, 1=Forward, 2=Reverse |
| 0 | 3..2 | RangeShift | 0=Neutral, 1=First, 2=Second, 3=Third |
| 0 | 4 | TempSwitch | 0/1 |
| 0 | 7..5 | reserved | — |
| 1 | 0 | PtoSwitch | 0/1 |
| 1 | 1 | FourWdSwitch | 0/1 |
| 1 | 2 | InchingSwitch | 0/1 |
| 1 | 3 | ParkingSwitch | 0/1 |
| 1 | 7..4 | reserved | — |
| 2 | 7..0 | PedalPercent | 0..100 (%) |
| 3 | 7..0 | PedalVoltage | 0..255 (raw ADC) |
| 4~5 | 15..0 | SpeedSensor1 | 0..65535 RPM |
| 6~7 | 15..0 | SpeedSensor2 | 0..65535 RPM |

### 산출물

`dbc/experimental/`:
- `virtual_input.motorola.dbc` — Motorola @0+ (start_bit=MSB)
- `virtual_input.intel.dbc` — Intel @1+ (start_bit=LSB)

검증: `tests/Dbc.Tests/VirtualInputExperimentalDbcTests.cs` — 두 변종 모두 11개 신호의 `(StartBit, Length, LittleEndian)` 튜플 기대값 일치 확인.

---

## Phase 2b Task List (초안)

Q1 payload가 현장 캡처로 확인되면:

1. **Eec1HeartbeatProvider 구현** (100ms 주기 송신)
   - Virtual bus에서 주기적 CAN frame 발생
   - 런타임 DBC 선택: `dbc/experimental/eec1_emulation.<motorola|intel>.dbc`
   - IOptions 기반 `Eec1HeartbeatOptions { string DbcVariant; byte[] InitialPayload; }`
   - **Payload override API**: `SetFaultCode(low, high)` + `SetReservedByte(index, value)` → Byte 1~7도 필드 테스트에서 교체 가능

2. **Alarms §6 – EEC1 Timeout 규칙 활성화**
   - 기 등록된 `AlarmRuleFactory.CreatePhase2aRules()`에 timeout rule 추가
   - TC-024 통합테스트: "300ms 정지 → EL0601 alarm"

3. **VirtualInputHeartbeat + VirtualInputService 구현** (50ms 주기 송신)
   - `IVirtualInputService` 상태 저장 → `VirtualInputHeartbeat`가 0x18FF5080 송출
   - 런타임 DBC 선택: `dbc/experimental/virtual_input.<motorola|intel>.dbc`
   - `IOptions<VirtualInputOptions> { string DbcVariant; }`
   - TestRunner `SetVirtualInputStep`, `EnterSimulationModeStep`, `ExitSimulationModeStep` executor
   - TC-003~009, 011~012, 017, 020, 025 자동화 (Simulator 환경 한정, 실기 검증은 현장 이관)

4. **Q5 확정 시 DBC 변종 정리**
   - 현장 캡처가 한 쪽 엔디안을 최종 확정하면, `dbc/confirmed/`로 승격하고 반대쪽 변종은 제거
   - 단, 제조사 2판(타 OEM) 대응 시 이중 변종 유지 가능성 있으니, 결정은 현장 배치 정책 확인 후

---

## 위험 / 미확정 항목

- **현장 CAN 캡처 없음**: Motorola/Intel 둘 다 "추정". 실측 시 2개 변종 중 하나는 폐기.
- **Q3 Virtual Input은 자가 정의**: 실 TCU가 0x18FF5080을 수신/해석한다는 근거 없음. Simulator/VirtualCanBus 전용. 현장 시나리오 테스트는 제조사 공식 스펙 수신 전까지 차단.
- **EL0601_EEC1_Timeout (Byte=3, Bit=1)**: 이 신호는 TCU→Display 방향(TCU=Tx)이므로 Phase 2a Alarms 엔진이 소비. EEC1 하트비트(Phase 2b) 송신과는 역방향. 혼동 금지.
- **Q2 Operating Mode 전환 방법 미해결**: TCU를 Simulation Mode(0001b)로 진입시키는 PC 측 명령 메시지 미상. Q3 VirtualInput 송출만으로 TCU가 자동 전환하는지 미검증. 현장 확인 필요.
- **GenMsgCycleTime 속성 의도적 생략**: `BA_DEF_` 선언 강제가 번거롭고, 주기는 provider 코드에서 결정되므로 주석으로 충분.

---

## Implementation Notes (2026-04-22)

구현 파일 (단일 commit 범위 아님 — 한 task = 한 commit):
- `src/Application/Can/Eec1HeartbeatProvider.cs`
- `src/Application/Can/VirtualInputHeartbeat.cs`
- `src/Application/Can/BusLifecycleService.cs`
- `src/Application/Services/VirtualInputService.cs`
- `src/Application/Testing/Executors/SetHeartbeatStepExecutor.cs`
- `src/Application/Testing/Executors/SetVirtualInputStepExecutor.cs`
- `src/Application/Testing/Executors/EnterSimulationModeStepExecutor.cs`
- `src/Application/Testing/Executors/ExitSimulationModeStepExecutor.cs`

의도적으로 단순화한 항목:
- `VirtualInputHeartbeat`는 Motorola 인코딩만 내장. Intel이 필요해지면 `Options.ByteOrder`로 분기 추가.
- `VirtualInputState.WheelSpeedKph`는 8-byte bitmap에 포함되지 않음 — UI/내부 상태 용도만, 필요 시 DBC/encoder 양쪽 확장.
- EEC1 Timeout 알람은 AlarmEngine rule로 구현하지 않음. TCU가 직접 `EL0601` 비트를 송출하므로 `ObserveBitStep`로 관찰 충분.

향후 Simulator 모듈에서 확장:
- TC-004~009, 011~012, 017, 020, 025 자동화
- EL0601 실제 TCU 감지 로직 (10초 타임아웃) 시뮬레이션
