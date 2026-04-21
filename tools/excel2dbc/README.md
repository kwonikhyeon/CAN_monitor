# excel2dbc

Generates DBC files from the TCU Protocol Excel (`data/120HP_TCU_CAN_Protocol_Updated_v241107.xlsx`).

## Setup

```bash
cd tools/excel2dbc
python -m venv .venv
# Windows:
.venv\Scripts\activate
# macOS/Linux:
source .venv/bin/activate
pip install -r requirements.txt
```

## Usage

```bash
python excel2dbc.py \
  --input ../../data/120HP_TCU_CAN_Protocol_Updated_v241107.xlsx \
  --output-dir ../../dbc
```

Produces:
- `dbc/confirmed/120HP_NoPto.dbc`
- `dbc/confirmed/160HP_WithPto.base.dbc`

## Assumptions

- All 16-bit signal pairs (`High Byte` + `Low Byte`) are treated as Motorola `@0+`, unsigned unless named with `signed`. These are tagged with `CM_ SG_ ... "ASSUMED_ENDIANNESS";`.
- Q5 confirmation will drive re-partitioning to `dbc/experimental/assumed_endian.dbc` in a later phase.
