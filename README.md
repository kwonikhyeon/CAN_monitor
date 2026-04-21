# CAN Monitor

PC-side CAN bus monitor, emulator, and jig test runner for tractor TCU (변속제어기).

Status: **Phase 1 (Foundation)** — Core / Dbc / Infrastructure.Can libraries + excel2dbc.

- Spec: [`docs/superpowers/specs/2026-04-20-can-monitor-design.md`](docs/superpowers/specs/2026-04-20-can-monitor-design.md)
- Phase 1 plan: [`docs/superpowers/plans/2026-04-21-phase1-foundation.md`](docs/superpowers/plans/2026-04-21-phase1-foundation.md)

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
- `dbc/confirmed/` — validated DBCs, CI snapshot baseline
- `dbc/experimental/` — reserved for Q1/Q3/Q5 confirmed signals

## Status of Open Questions (blocks later phases)

- Q1 EEC1 payload, Q2 Operating Mode switching, Q3 virtual-input message spec, Q4 calibration mode, Q5 endianness numbers — see spec §22.
