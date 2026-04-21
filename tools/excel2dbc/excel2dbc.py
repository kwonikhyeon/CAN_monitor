"""
excel2dbc — Generate DBC files from the TCU Protocol Excel.

Current scope (Phase 1):
  - Sheet `120HP_No_PTO`     -> dbc/confirmed/120HP_NoPto.dbc
  - Sheet `160HP_With_PTO`   -> dbc/confirmed/160HP_WithPto.base.dbc
  - EEC1 / virtual-input / calibration messages are skipped; they belong
    in dbc/experimental/ once Q1/Q3 are confirmed.
"""
from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable

import cantools
from cantools.database import Database, Message, Signal
from cantools.database.conversion import LinearConversion
from openpyxl import load_workbook
from openpyxl.worksheet.worksheet import Worksheet


# ---------- Data model ----------

@dataclass
class SignalDef:
    name: str
    start_bit: int
    length: int
    factor: float
    offset: float
    minimum: float
    maximum: float
    unit: str | None
    is_signed: bool
    assumed_endian: bool


@dataclass
class MessageDef:
    frame_id: int
    is_extended: bool
    name: str
    dlc: int
    cycle_time_ms: int | None
    signals: list[SignalDef] = field(default_factory=list)


# ---------- Hard-coded layout for TCU Protocol Excel ----------
#
# Source of truth: data/120HP_TCU_CAN_Protocol_Updated_v241107.xlsx,
# sheet "All_Messages_Detail" columns (Byte, Bit, Signal name, Length).
#
# start_bit encoding uses the Motorola @0+ ("big_endian") sawtooth layout:
#   - 1-bit signal:    start = byte * 8 + bit
#   - multi-bit signal: start = byte * 8 + MSB_bit
#
# The Excel "Bit" column uses MSB-first notation in most cells (e.g. "3,0"
# means MSB=3, LSB=0 for a 4-bit field). Alarm_Status is the one exception
# — its cell reads "0,1" but the data-range key "10b: Error"=2 requires
# bit 1 to carry the MSB, so we treat "0,1" as (LSB,MSB) and derive MSB=1.

_CONFIRMED_120HP: list[MessageDef] = [
    MessageDef(
        frame_id=0x0C000E00, is_extended=True,
        name="Status_0x0C000E00", dlc=8, cycle_time_ms=100,
        signals=[
            SignalDef("Gear_Lever_N_Status",        7, 1, 1, 0, 0, 1,  None, False, False),
            SignalDef("Gear_Lever_F_Status",        3, 1, 1, 0, 0, 1,  None, False, False),
            SignalDef("Range_1st_Status",          15, 1, 1, 0, 0, 1,  None, False, False),
            SignalDef("Gear_Lever_R_Status",       11, 1, 1, 0, 0, 1,  None, False, False),
            SignalDef("Range_3rd_Status",          23, 1, 1, 0, 0, 1,  None, False, False),
            SignalDef("Range_2nd_Status",          19, 1, 1, 0, 0, 1,  None, False, False),
            SignalDef("Temperature_Switch_Status", 47, 1, 1, 0, 0, 1,  None, False, False),
            SignalDef("Operating_Mode",            43, 4, 1, 0, 0, 15, None, False, False),
            SignalDef("Alarm_Status",              49, 2, 1, 0, 0, 3,  None, False, False),
        ],
    ),
    MessageDef(
        frame_id=0x200, is_extended=False,
        name="Alarms_0x200", dlc=8, cycle_time_ms=1000,
        signals=[
            SignalDef("Pedal_Low",          0, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("Pedal_High",         1, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("Pedal_Failure",      2, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("Power_Low",          3, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("Power_High",         4, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("FOR_Open",           8, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("FOR_Low",            9, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("FOR_High",          10, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("REV_Open",          11, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("REV_Low",           12, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("REV_High",          13, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("Pressure_1_Fault",  16, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("Pressure_2_Fault",  17, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("Overspeed_Direction_Change", 32, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("EEC1_Timeout",      25, 1, 1, 0, 0, 1, None, False, False),
        ],
    ),
]

_CONFIRMED_160HP: list[MessageDef] = [
    *_CONFIRMED_120HP,
    MessageDef(
        frame_id=0x202, is_extended=False,
        name="ExtendedStatus_0x202", dlc=8, cycle_time_ms=100,
        signals=[
            SignalDef("PTO_Switch",    0, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("FourWD_Switch", 1, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("Inching_Switch",2, 1, 1, 0, 0, 1, None, False, False),
            SignalDef("Parking_Switch",3, 1, 1, 0, 0, 1, None, False, False),
        ],
    ),
]


def to_cantools_signal(s: SignalDef) -> Signal:
    comment = "ASSUMED_ENDIANNESS" if s.assumed_endian else None
    conversion = None
    if s.factor != 1.0 or s.offset != 0.0:
        conversion = LinearConversion(scale=s.factor, offset=s.offset, is_float=False)
    return Signal(
        name=s.name,
        start=s.start_bit,
        length=s.length,
        byte_order="big_endian",
        is_signed=s.is_signed,
        conversion=conversion,
        minimum=s.minimum,
        maximum=s.maximum,
        unit=s.unit,
        comment=comment,
    )


def to_cantools_message(m: MessageDef) -> Message:
    return Message(
        frame_id=m.frame_id,
        is_extended_frame=m.is_extended,
        name=m.name,
        length=m.dlc,
        signals=[to_cantools_signal(s) for s in m.signals],
        cycle_time=m.cycle_time_ms,
        senders=["TCU"],
    )


def build_database(messages: Iterable[MessageDef]) -> Database:
    db = Database()
    for m in messages:
        db.messages.append(to_cantools_message(m))
    db.refresh()
    return db


def main() -> int:
    p = argparse.ArgumentParser(description="Generate confirmed DBCs from Protocol Excel.")
    p.add_argument("--input", required=True, type=Path,
                   help="Path to 120HP_TCU_CAN_Protocol_Updated_v241107.xlsx")
    p.add_argument("--output-dir", required=True, type=Path,
                   help="Output base dir (confirmed/experimental are created beneath).")
    args = p.parse_args()

    if not args.input.exists():
        print(f"error: input not found: {args.input}", file=sys.stderr)
        return 2

    _ = load_workbook(args.input, data_only=True, read_only=True)

    confirmed_dir = args.output_dir / "confirmed"
    confirmed_dir.mkdir(parents=True, exist_ok=True)

    db120 = build_database(_CONFIRMED_120HP)
    cantools.database.dump_file(db120, confirmed_dir / "120HP_NoPto.dbc")
    print(f"wrote {confirmed_dir / '120HP_NoPto.dbc'}")

    db160 = build_database(_CONFIRMED_160HP)
    cantools.database.dump_file(db160, confirmed_dir / "160HP_WithPto.base.dbc")
    print(f"wrote {confirmed_dir / '160HP_WithPto.base.dbc'}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
