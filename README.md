# CAN Monitor

PC-side CAN bus monitor, emulator, and jig test runner for tractor TCU (변속제어기).

Status: **Phase 2a (Application) 완료, Q1/Q3 해제 준비** — Phase 2a 파이프라인·Test DSL·TC-001/002/010/024 자동화 완료. Q1(EEC1 payload)/Q3(VirtualInput) 실험용 DBC는 `dbc/experimental/`에 Motorola/Intel 양쪽 변종으로 작성됨 (문서 추정·자가 정의 기반, 현장 캡처 시 교체). Phase 2b (EEC1 heartbeat + VirtualInput 구현) 착수 가능.

- Spec: [`docs/superpowers/specs/2026-04-20-can-monitor-design.md`](docs/superpowers/specs/2026-04-20-can-monitor-design.md)
- Phase 1 plan: [`docs/superpowers/plans/2026-04-21-phase1-foundation.md`](docs/superpowers/plans/2026-04-21-phase1-foundation.md)
- Phase 2a plan: [`docs/superpowers/plans/2026-04-21-phase2a-application-core.md`](docs/superpowers/plans/2026-04-21-phase2a-application-core.md)

## Build

Requirements: .NET 8 SDK (see `global.json`), Python 3.11+ for the excel2dbc tool.

```bash
dotnet restore
dotnet build
dotnet test
```

## Regenerate DBCs from Protocol Excel

```bash
cd tools/excel2dbc
python -m venv .venv
source .venv/Scripts/activate      # Windows bash; POSIX: source .venv/bin/activate
pip install -r requirements.txt
python excel2dbc.py \
  --input ../../data/120HP_TCU_CAN_Protocol_Updated_v241107.xlsx \
  --output-dir ../../dbc
```

## Layout

- `src/Core/` — domain records + contracts, no deps
- `src/Dbc/` — DbcParserLib wrapper, SignalDecoder
- `src/Infrastructure.Can/` — bus abstraction + VirtualCanBus
- `src/Application/` — Rx 파이프라인 조립 + Test DSL runner (Phase 2a)
- `dbc/confirmed/` — validated DBCs, CI snapshot baseline
- `dbc/experimental/` — reserved for Q1/Q3/Q5 confirmed signals

## Status of Open Questions (blocks later phases)

- Q1 EEC1 payload, Q2 Operating Mode switching, Q3 virtual-input message spec, Q4 calibration mode, Q5 endianness numbers — see spec §22.
