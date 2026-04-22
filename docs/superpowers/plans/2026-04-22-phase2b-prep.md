# Phase 2b Preparation Notes

Status: **DRAFT — Q1/Q3 해제 후 정식 plan으로 확장**

Phase 2a 완료(commit cc71e6b) 이후, Q1(EEC1 payload)/Q3(virtual-input) 해제를 위한 선행 의사결정과 실험 산출물을 기록한다. 정식 Phase 2b plan은 현장 CAN 캡처 1회 확보 후 작성한다.

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

## Phase 2b Task List (초안)

Q1 payload가 현장 캡처로 확인되면:

1. **Eec1HeartbeatProvider 구현** (100ms 주기 송신)
   - Virtual bus에서 주기적 CAN frame 발생
   - 런타임 DBC 선택: `dbc/experimental/eec1_emulation.<motorola|intel>.dbc`
   - IOptions 기반 `Eec1HeartbeatOptions { string DbcVariant; byte[] InitialPayload; }`

2. **Alarms §6 – EEC1 Timeout 규칙 활성화**
   - 기 등록된 `AlarmRuleFactory.CreatePhase2aRules()`에 timeout rule 추가
   - TC-024 통합테스트: "300ms 정지 → EL0601 alarm"

3. **Q3: Virtual Input 메시지 사양 확정**
   - Simulation Mode 자동 전환 조건: 현장 캡처 필요
   - VirtualInput 송출 주체/주기/payload 확정
   - TC-010 계열 확장 (Simulation↔Normal 전환)

4. **Q5 확정 시 DBC 변종 정리**
   - 현장 캡처가 한 쪽 엔디안을 최종 확정하면, `dbc/confirmed/`로 승격하고 반대쪽 변종은 제거
   - 단, 제조사 2판(타 OEM) 대응 시 이중 변종 유지 가능성 있으니, 결정은 현장 배치 정책 확인 후

---

## 위험 / 미확정 항목

- **현장 CAN 캡처 없음**: Motorola/Intel 둘 다 "추정". 실측 시 2개 변종 중 하나는 폐기.
- **EL0601_EEC1_Timeout (Byte=3, Bit=1)**: 이 신호는 TCU→Display 방향(TCU=Tx)이므로 Phase 2a Alarms 엔진이 소비. EEC1 하트비트(Phase 2b) 송신과는 역방향. 혼동 금지.
- **GenMsgCycleTime 속성 의도적 생략**: `BA_DEF_` 선언 강제가 번거롭고, 주기는 provider 코드에서 결정되므로 주석으로 충분.
