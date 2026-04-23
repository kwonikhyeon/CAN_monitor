# Phase 3b — 파이프라인 활성화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 3a의 placeholder Rx 파이프라인을 실데이터로 교체한다 — `ICanBus.Frames → SignalDecoder → AlarmEngine → CanEventHub` 체인 활성화 + Phase 3a 잔여 정리(SelectedDbc 자동 재로드, DecodeFailures, dead DI 제거).

**Architecture:** 인터페이스 확장 2건(`ISignalDecoder.UnknownFrames`, `IAlarmEngine.ReplaceRules`) → 실구현 + 신규 `BitAlarmRule` + DBC 기반 자동 룰 생성 → `SessionViewModel`이 connection 단위로 `CanReceivePipeline` 조립 + DBC 변경 시 `ReplaceRules` 오케스트레이션. 동시성은 단순 `lock(_sync)`. Pipeline은 connection-scope, AlarmEngine은 DI 싱글턴.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting, System.Reactive (`Subject`, `Buffer`, `Scan`, `Publish().RefCount()`), DbcParserLib, xUnit + FluentAssertions + Microsoft.Reactive.Testing.

**Reference Spec:** `docs/superpowers/specs/2026-04-23-phase3b-pipeline-design.md`

---

## File Structure

**신규 소스**
- `src/Application/Alarms/BitAlarmRule.cs`

**신규 테스트**
- `tests/Application.Tests/Alarms/BitAlarmRuleTests.cs`
- `tests/Application.Tests/Alarms/AlarmRuleFactoryTests.cs`
- `tests/Application.Tests/Alarms/AlarmEngineReplaceRulesTests.cs`
- `tests/Wpf.Tests/Shell/SessionViewModelDbcReloadTests.cs`
- `tests/Wpf.Tests/Shell/SessionViewModelPipelineTests.cs`
- `tests/Wpf.Tests/Shell/StatusBarDecodeFailuresTests.cs`
- `tests/Integration.Tests/Phase3b/Phase3bPipelineTests.cs`

**수정 (인터페이스 / 구현)**
- `src/Core/Abstractions/ISignalDecoder.cs` — `UnknownFrames` 옵저버블 추가
- `src/Core/Abstractions/IAlarmEngine.cs` — `ReplaceRules` 추가
- `src/Dbc/SignalDecoder.cs` — `Subject<CanFrame> _unknown` + `IDisposable`
- `src/Application/Alarms/AlarmEngine.cs` — `lock(_sync)` + `ReplaceRules`
- `src/Application/Alarms/AlarmRuleFactory.cs` — `CreatePhase2aRules` 삭제, `FromDbc` 추가
- `src/Application/ServiceCollectionExtensions.cs` — dead 등록 제거 + `AlarmEngine` 빈 룰
- `src/Wpf/Shell/SessionViewModel.cs` — `ReloadDbcAsync`, 파이프라인 조립
- `src/Wpf/Shell/StatusBarViewModel.cs` — `DecodeFailures` 활성화
- `src/Wpf/App.xaml.cs` — `ISignalDecoder` 등록

**수정 (회귀)**
- `tests/Integration.Tests/TestCases/Tc001FrameRateTests.cs` — `NoopDecoder.UnknownFrames`
- `tests/Wpf.Tests/Shell/StatusBarViewModelTests.cs` — `FakeAlarmEngine.ReplaceRules`, decoder 인자
- `tests/Wpf.Tests/Shell/SessionViewModelTests.cs` — `FakeDbcProvider.Current` 갱신, decoder 인자

---

## Task 1: `ISignalDecoder.UnknownFrames` 인터페이스 + `SignalDecoder` 구현

**목표:** Decoder 가 알 수 없는 Id 의 프레임을 별도 옵저버블로 노출. StatusBar가 이를 카운트해 DecodeFailures를 표시한다.

**Files:**
- Modify: `src/Core/Abstractions/ISignalDecoder.cs`
- Modify: `src/Dbc/SignalDecoder.cs`
- Modify: `tests/Dbc.Tests/SignalDecoderTests.cs` — unknown id 회귀 케이스 추가
- Modify: `tests/Integration.Tests/TestCases/Tc001FrameRateTests.cs` — `NoopDecoder` 회귀

- [ ] **Step 1: SignalDecoderTests 에 unknown id 회귀 테스트 추가 (실패)**

`tests/Dbc.Tests/SignalDecoderTests.cs` 파일 끝(마지막 `}` 직전)에 추가. 기존 `FakeDbcProvider` (primary constructor 형태) 헬퍼를 재사용:

```csharp
    [Fact]
    public void Unknown_id_publishes_to_UnknownFrames_and_returns_empty()
    {
        var provider = new FakeDbcProvider(DbcDatabase.Empty);
        var sut = new SignalDecoder(provider);

        var seen = new List<CanFrame>();
        using var sub = sut.UnknownFrames.Subscribe(seen.Add);

        var frame = new CanFrame(0xDEAD, true, new byte[] { 0 }, DateTimeOffset.UtcNow, CanDirection.Rx);
        var result = sut.Decode(frame);

        result.Should().BeEmpty();
        seen.Should().ContainSingle().Which.Id.Should().Be(0xDEADu);
    }
```

- [ ] **Step 2: 테스트 실행 — 컴파일 실패 확인**

Run: `dotnet build tests/Dbc.Tests/CanMonitor.Dbc.Tests.csproj`
Expected: FAIL — `'ISignalDecoder' does not contain a definition for 'UnknownFrames'`

- [ ] **Step 3: `ISignalDecoder` 인터페이스에 `UnknownFrames` 추가**

`src/Core/Abstractions/ISignalDecoder.cs` 전체를 다음으로 교체:

```csharp
using CanMonitor.Core.Models;

namespace CanMonitor.Core.Abstractions;

public interface ISignalDecoder
{
    IReadOnlyList<SignalValue> Decode(CanFrame frame);
    IObservable<CanFrame> UnknownFrames { get; }
}
```

- [ ] **Step 4: `SignalDecoder` 구현 갱신**

`src/Dbc/SignalDecoder.cs` 전체를 다음으로 교체:

```csharp
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Dbc;

public sealed class SignalDecoder : ISignalDecoder, IDisposable
{
    private readonly IDbcProvider _dbc;
    private readonly Subject<CanFrame> _unknown = new();

    public SignalDecoder(IDbcProvider dbc) => _dbc = dbc;

    public IObservable<CanFrame> UnknownFrames => _unknown.AsObservable();

    public IReadOnlyList<SignalValue> Decode(CanFrame frame)
    {
        var db = _dbc.Current;                                // 시작 시점의 스냅샷을 캡처
        if (!db.MessagesById.TryGetValue(frame.Id, out var msg))
        {
            _unknown.OnNext(frame);
            return Array.Empty<SignalValue>();
        }

        var payload = frame.Data.Span;
        var results = new SignalValue[msg.Signals.Length];

        for (int i = 0; i < msg.Signals.Length; i++)
        {
            var sig = msg.Signals[i];
            long raw = sig.LittleEndian
                ? ExtractIntel(payload, sig.StartBit, sig.Length, sig.IsSigned)
                : ExtractMotorola(payload, sig.StartBit, sig.Length, sig.IsSigned);

            double phys = raw * sig.Factor + sig.Offset;
            results[i] = new SignalValue(msg.Name, sig.Name, raw, phys, sig.Unit, frame.Timestamp);
        }
        return results;
    }

    public void Dispose()
    {
        _unknown.OnCompleted();
        _unknown.Dispose();
    }

    private static long ExtractIntel(ReadOnlySpan<byte> data, int startBit, int length, bool isSigned)
    {
        ulong raw = 0;
        for (int i = 0; i < length; i++)
        {
            int absBit    = startBit + i;
            int byteIndex = absBit >> 3;
            int bitInByte = absBit & 7;
            if (byteIndex >= data.Length) break;
            if (((data[byteIndex] >> bitInByte) & 1) != 0)
                raw |= 1UL << i;
        }
        return SignExtend(raw, length, isSigned);
    }

    private static long ExtractMotorola(ReadOnlySpan<byte> data, int startBit, int length, bool isSigned)
    {
        ulong raw   = 0;
        int byteIdx = startBit >> 3;
        int bitIdx  = startBit & 7;

        for (int i = 0; i < length; i++)
        {
            if (byteIdx >= data.Length) break;
            raw <<= 1;
            uint bit = (uint)((data[byteIdx] >> bitIdx) & 1);
            raw |= bit;

            if (bitIdx == 0)
            {
                bitIdx = 7;
                byteIdx++;
            }
            else
            {
                bitIdx--;
            }
        }
        return SignExtend(raw, length, isSigned);
    }

    private static long SignExtend(ulong raw, int length, bool isSigned)
    {
        if (!isSigned || length == 64) return unchecked((long)raw);
        ulong signBit = 1UL << (length - 1);
        if ((raw & signBit) == 0) return (long)raw;
        ulong mask = ~((1UL << length) - 1);
        return unchecked((long)(raw | mask));
    }
}
```

- [ ] **Step 5: `Tc001FrameRateTests` 의 `NoopDecoder` 회귀 fix**

`tests/Integration.Tests/TestCases/Tc001FrameRateTests.cs` 파일 상단 `using` 절에 다음을 추가:

```csharp
using System.Reactive.Linq;
```

그리고 `NoopDecoder` 내부에 한 줄 추가:

```csharp
    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
        public IObservable<CanFrame> UnknownFrames => Observable.Never<CanFrame>();
    }
```

- [ ] **Step 6: 빌드 + 테스트 통과 확인**

Run: `dotnet test tests/Dbc.Tests/CanMonitor.Dbc.Tests.csproj`
Expected: PASS — 새 테스트 포함 모두 green.

Run: `dotnet test tests/Integration.Tests/CanMonitor.Integration.Tests.csproj`
Expected: PASS — `NoopDecoder` 회귀 fix 가 통합 테스트도 통과시킴.

- [ ] **Step 7: 커밋**

```bash
git add src/Core/Abstractions/ISignalDecoder.cs src/Dbc/SignalDecoder.cs tests/Dbc.Tests/SignalDecoderTests.cs tests/Integration.Tests/TestCases/Tc001FrameRateTests.cs
git commit -m "feat(decoder): expose UnknownFrames observable on ISignalDecoder"
```

---

## Task 2: `BitAlarmRule` 신규 클래스

**목표:** Alarms_0x200 같은 메시지의 1-bit 플래그 시그널을 알람으로 변환하는 규칙. 0→1 시 Active=true, 1→0 시 Active=false 만 emit (no-op 시 null).

**Files:**
- Create: `src/Application/Alarms/BitAlarmRule.cs`
- Create: `tests/Application.Tests/Alarms/BitAlarmRuleTests.cs`

- [ ] **Step 1: 실패 테스트 파일 생성**

`tests/Application.Tests/Alarms/BitAlarmRuleTests.cs`:

```csharp
using CanMonitor.Application.Alarms;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Alarms;

public sealed class BitAlarmRuleTests
{
    private static SignalValue Bit(string msg, string sig, double raw)
        => new(msg, sig, raw, raw, null, DateTimeOffset.UtcNow);

    [Fact]
    public void Activates_on_zero_to_one_transition()
    {
        var rule = new BitAlarmRule("EEC1_Timeout", AlarmSeverity.Warning,
            "Alarms_0x200", "EEC1_Timeout", "EEC1_Timeout");

        var result = rule.Evaluate(Bit("Alarms_0x200", "EEC1_Timeout", 1), prior: null);

        result.Should().NotBeNull();
        result!.Code.Should().Be("EEC1_Timeout");
        result.Severity.Should().Be(AlarmSeverity.Warning);
        result.Message.Should().Be("EEC1_Timeout");
        result.Active.Should().BeTrue();
    }

    [Fact]
    public void Deactivates_on_one_to_zero_transition()
    {
        var rule = new BitAlarmRule("EEC1_Timeout", AlarmSeverity.Warning,
            "Alarms_0x200", "EEC1_Timeout", "EEC1_Timeout");
        var prior = new AlarmState("EEC1_Timeout", AlarmSeverity.Warning, "EEC1_Timeout", true, DateTimeOffset.UtcNow);

        var result = rule.Evaluate(Bit("Alarms_0x200", "EEC1_Timeout", 0), prior: prior);

        result.Should().NotBeNull();
        result!.Active.Should().BeFalse();
    }

    [Fact]
    public void No_op_when_state_unchanged()
    {
        var rule = new BitAlarmRule("EEC1_Timeout", AlarmSeverity.Warning,
            "Alarms_0x200", "EEC1_Timeout", "EEC1_Timeout");
        var prior = new AlarmState("EEC1_Timeout", AlarmSeverity.Warning, "EEC1_Timeout", true, DateTimeOffset.UtcNow);

        var result = rule.Evaluate(Bit("Alarms_0x200", "EEC1_Timeout", 1), prior: prior);

        result.Should().BeNull();
    }

    [Fact]
    public void Returns_null_for_wrong_message()
    {
        var rule = new BitAlarmRule("EEC1_Timeout", AlarmSeverity.Warning,
            "Alarms_0x200", "EEC1_Timeout", "EEC1_Timeout");

        var result = rule.Evaluate(Bit("OtherMsg", "EEC1_Timeout", 1), prior: null);

        result.Should().BeNull();
    }

    [Fact]
    public void Returns_null_for_wrong_signal()
    {
        var rule = new BitAlarmRule("EEC1_Timeout", AlarmSeverity.Warning,
            "Alarms_0x200", "EEC1_Timeout", "EEC1_Timeout");

        var result = rule.Evaluate(Bit("Alarms_0x200", "Other_Bit", 1), prior: null);

        result.Should().BeNull();
    }
}
```

- [ ] **Step 2: 테스트 실행 — 컴파일 실패**

Run: `dotnet build tests/Application.Tests/CanMonitor.Application.Tests.csproj`
Expected: FAIL — `The type or namespace name 'BitAlarmRule' could not be found`.

- [ ] **Step 3: `BitAlarmRule` 구현**

`src/Application/Alarms/BitAlarmRule.cs`:

```csharp
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Alarms;

public sealed class BitAlarmRule : IAlarmRule
{
    private readonly AlarmSeverity _severity;
    private readonly string _messageName;
    private readonly string _signalName;
    private readonly string _description;

    public BitAlarmRule(string code, AlarmSeverity severity,
        string messageName, string signalName, string description)
    {
        Code = code;
        _severity = severity;
        _messageName = messageName;
        _signalName = signalName;
        _description = description;
    }

    public string Code { get; }

    public AlarmState? Evaluate(SignalValue value, AlarmState? prior)
    {
        if (value.MessageName != _messageName || value.SignalName != _signalName)
            return null;

        var active = value.RawValue == 1;
        if (prior is not null && prior.Active == active)
            return null;

        return new AlarmState(Code, _severity, _description, active, value.Timestamp);
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~BitAlarmRuleTests"`
Expected: PASS — 5 개 테스트 모두 green.

- [ ] **Step 5: 커밋**

```bash
git add src/Application/Alarms/BitAlarmRule.cs tests/Application.Tests/Alarms/BitAlarmRuleTests.cs
git commit -m "feat(alarms): add BitAlarmRule for single-bit DBC flag signals"
```

---

## Task 3: `AlarmRuleFactory.FromDbc` (DBC → 룰 자동 생성)

**목표:** `120HP_NoPto.dbc` 의 `Alarms_0x200` 메시지에서 15 개 비트 시그널을 자동으로 `BitAlarmRule` 로 변환. dead `CreatePhase2aRules` 제거.

**Files:**
- Modify: `src/Application/Alarms/AlarmRuleFactory.cs`
- Create: `tests/Application.Tests/Alarms/AlarmRuleFactoryTests.cs`

- [ ] **Step 1: 실패 테스트 파일 생성**

`tests/Application.Tests/Alarms/AlarmRuleFactoryTests.cs`:

```csharp
using System.Collections.Immutable;
using CanMonitor.Application.Alarms;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Alarms;

public sealed class AlarmRuleFactoryTests
{
    private static DbcSignal Bit(string name, int startBit) => new(
        Name: name,
        StartBit: startBit,
        Length: 1,
        LittleEndian: false,
        IsSigned: false,
        Factor: 1.0,
        Offset: 0.0,
        Minimum: 0.0,
        Maximum: 1.0,
        Unit: null,
        ValueTable: null);

    [Fact]
    public void FromDbc_creates_rule_per_alarm_bit_signal()
    {
        var alarms = new DbcMessage(
            Id: 0x200,
            IsExtended: false,
            Name: "Alarms_0x200",
            Dlc: 8,
            Signals: ImmutableArray.Create(
                Bit("EEC1_Timeout", 25),
                Bit("Pressure_1_Fault", 16),
                Bit("Pedal_Failure", 2)),
            CycleTime: null);

        var db = new DbcDatabase(new[] { alarms });

        var rules = AlarmRuleFactory.FromDbc(db);

        rules.Select(r => r.Code).Should().BeEquivalentTo(
            new[] { "EEC1_Timeout", "Pressure_1_Fault", "Pedal_Failure" });
    }

    [Fact]
    public void FromDbc_skips_non_bit_signals()
    {
        var alarms = new DbcMessage(
            Id: 0x200,
            IsExtended: false,
            Name: "Alarms_0x200",
            Dlc: 8,
            Signals: ImmutableArray.Create(
                Bit("EEC1_Timeout", 25),
                new DbcSignal("MultiBitField", 0, 4, false, false, 1, 0, 0, 15, null, null)),
            CycleTime: null);

        var db = new DbcDatabase(new[] { alarms });

        var rules = AlarmRuleFactory.FromDbc(db);

        rules.Select(r => r.Code).Should().BeEquivalentTo(new[] { "EEC1_Timeout" });
    }

    [Fact]
    public void FromDbc_returns_empty_when_no_alarm_message()
    {
        var other = new DbcMessage(
            Id: 0x100,
            IsExtended: false,
            Name: "OtherMsg",
            Dlc: 8,
            Signals: ImmutableArray.Create(Bit("Foo", 0)),
            CycleTime: null);
        var db = new DbcDatabase(new[] { other });

        var rules = AlarmRuleFactory.FromDbc(db);

        rules.Should().BeEmpty();
    }

    [Fact]
    public void FromDbc_returns_empty_for_empty_database()
    {
        AlarmRuleFactory.FromDbc(DbcDatabase.Empty).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: 테스트 실행 — 컴파일 실패**

Run: `dotnet build tests/Application.Tests/CanMonitor.Application.Tests.csproj`
Expected: FAIL — `'AlarmRuleFactory' does not contain a definition for 'FromDbc'`.

- [ ] **Step 3: `AlarmRuleFactory.FromDbc` 구현 + dead 메서드 제거**

`src/Application/Alarms/AlarmRuleFactory.cs` 전체를 다음으로 교체:

```csharp
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Alarms;

public static class AlarmRuleFactory
{
    private const string AlarmMessageName = "Alarms_0x200";

    public static IReadOnlyList<IAlarmRule> FromDbc(DbcDatabase db)
    {
        var msg = db.Messages.FirstOrDefault(m => m.Name == AlarmMessageName);
        if (msg is null) return Array.Empty<IAlarmRule>();

        var rules = new List<IAlarmRule>(msg.Signals.Length);
        foreach (var sig in msg.Signals)
        {
            if (sig.Length != 1) continue;
            rules.Add(new BitAlarmRule(
                code: sig.Name,
                severity: AlarmSeverity.Warning,
                messageName: msg.Name,
                signalName: sig.Name,
                description: sig.Name));
        }
        return rules;
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~AlarmRuleFactoryTests"`
Expected: PASS — 4 개 테스트 모두 green.

- [ ] **Step 5: 커밋**

```bash
git add src/Application/Alarms/AlarmRuleFactory.cs tests/Application.Tests/Alarms/AlarmRuleFactoryTests.cs
git commit -m "feat(alarms): generate BitAlarmRule set from DBC Alarms_0x200 signals"
```

---

## Task 4: `IAlarmEngine.ReplaceRules` + `AlarmEngine` 동시성 강화

**목표:** 런타임에 룰을 바꿀 수 있는 진입점. 기존 active 알람을 dismiss + 상태 리셋. `lock(_sync)` 로 `Submit`/`ReplaceRules` 직렬화.

**Files:**
- Modify: `src/Core/Abstractions/IAlarmEngine.cs`
- Modify: `src/Application/Alarms/AlarmEngine.cs`
- Create: `tests/Application.Tests/Alarms/AlarmEngineReplaceRulesTests.cs`
- Modify: `tests/Wpf.Tests/Shell/StatusBarViewModelTests.cs` — `FakeAlarmEngine.ReplaceRules` 회귀

- [ ] **Step 1: 실패 테스트 파일 생성**

`tests/Application.Tests/Alarms/AlarmEngineReplaceRulesTests.cs`:

```csharp
using CanMonitor.Application.Alarms;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Alarms;

public sealed class AlarmEngineReplaceRulesTests
{
    private static SignalValue Bit(string sig, double raw)
        => new("Alarms_0x200", sig, raw, raw, null, DateTimeOffset.UtcNow);

    private static IAlarmRule Rule(string sig)
        => new BitAlarmRule(sig, AlarmSeverity.Warning, "Alarms_0x200", sig, sig);

    [Fact]
    public void ReplaceRules_dismisses_active_alarms_and_resets_state()
    {
        using var sut = new AlarmEngine(new[] { Rule("EEC1_Timeout"), Rule("Pedal_Low") });
        var seen = new List<AlarmState>();
        using var sub = sut.AlarmChanges.Subscribe(seen.Add);

        sut.Submit(Bit("EEC1_Timeout", 1));
        sut.Submit(Bit("Pedal_Low", 1));
        sut.CurrentAlarms.Where(a => a.Active).Should().HaveCount(2);

        sut.ReplaceRules(Array.Empty<IAlarmRule>());

        sut.CurrentAlarms.Should().BeEmpty();
        seen.Where(a => !a.Active).Select(a => a.Code)
            .Should().BeEquivalentTo(new[] { "EEC1_Timeout", "Pedal_Low" });
    }

    [Fact]
    public void ReplaceRules_lets_new_rules_emit_active_on_next_Submit()
    {
        using var sut = new AlarmEngine(Array.Empty<IAlarmRule>());
        sut.ReplaceRules(new[] { Rule("EEC1_Timeout") });

        var seen = new List<AlarmState>();
        using var sub = sut.AlarmChanges.Subscribe(seen.Add);

        sut.Submit(Bit("EEC1_Timeout", 1));

        seen.Should().ContainSingle().Which.Active.Should().BeTrue();
    }

    [Fact]
    public void ReplaceRules_with_no_prior_active_emits_nothing()
    {
        using var sut = new AlarmEngine(new[] { Rule("EEC1_Timeout") });
        var seen = new List<AlarmState>();
        using var sub = sut.AlarmChanges.Subscribe(seen.Add);

        sut.ReplaceRules(new[] { Rule("Pedal_Low") });

        seen.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: 테스트 실행 — 컴파일 실패**

Run: `dotnet build tests/Application.Tests/CanMonitor.Application.Tests.csproj`
Expected: FAIL — `'AlarmEngine' does not contain a definition for 'ReplaceRules'`.

- [ ] **Step 3: `IAlarmEngine` 인터페이스 확장**

`src/Core/Abstractions/IAlarmEngine.cs` 전체를 다음으로 교체:

```csharp
using CanMonitor.Core.Models;

namespace CanMonitor.Core.Abstractions;

public interface IAlarmEngine
{
    IObservable<AlarmState> AlarmChanges { get; }
    IReadOnlyCollection<AlarmState> CurrentAlarms { get; }
    void Submit(SignalValue value);
    void ReplaceRules(IReadOnlyList<IAlarmRule> rules);
}
```

> Note: `IAlarmRule` 은 `CanMonitor.Application.Alarms` 네임스페이스에 있어 Core 가 Application 을 참조할 수 없다. 따라서 인터페이스 시그니처는 `IReadOnlyList<object>` 가 아니라 — `IAlarmRule` 자체를 Core 로 옮기지 않는 한 의존성 역전이 깨진다. 다음 Step 에서 `IAlarmRule` 을 `Core.Abstractions` 로 이동한다.

- [ ] **Step 4: `IAlarmRule` 을 `Core.Abstractions` 로 이동**

`src/Application/Alarms/IAlarmRule.cs` 파일을 삭제하고, `src/Core/Abstractions/IAlarmRule.cs` 를 새로 생성:

```csharp
using CanMonitor.Core.Models;

namespace CanMonitor.Core.Abstractions;

public interface IAlarmRule
{
    string Code { get; }
    AlarmState? Evaluate(SignalValue value, AlarmState? current);
}
```

`BitAlarmRule.cs` 와 `AlarmRuleFactory.cs` 는 `using CanMonitor.Core.Abstractions;` 로 갱신:

`src/Application/Alarms/BitAlarmRule.cs` 첫 줄에 추가:
```csharp
using CanMonitor.Core.Abstractions;
```

`src/Application/Alarms/AlarmRuleFactory.cs` 첫 줄에 추가:
```csharp
using CanMonitor.Core.Abstractions;
```

`src/Application/Alarms/AlarmEngine.cs` 의 `using` 절은 이미 `Core.Abstractions` 포함 — 변경 없음.

이제 `IAlarmEngine.ReplaceRules(IReadOnlyList<IAlarmRule>)` 가 컴파일된다.

- [ ] **Step 5: `AlarmEngine` 구현 갱신**

`src/Application/Alarms/AlarmEngine.cs` 전체를 다음으로 교체:

```csharp
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Alarms;

public sealed class AlarmEngine : IAlarmEngine, IDisposable
{
    private readonly Subject<AlarmState> _changes = new();
    private readonly object _sync = new();
    private volatile IReadOnlyList<IAlarmRule> _rules;
    private ImmutableDictionary<string, AlarmState> _states = ImmutableDictionary<string, AlarmState>.Empty;

    public AlarmEngine(IEnumerable<IAlarmRule> rules)
    {
        _rules = rules.ToArray();
    }

    public IObservable<AlarmState> AlarmChanges => _changes.AsObservable();

    public IReadOnlyCollection<AlarmState> CurrentAlarms
    {
        get
        {
            lock (_sync) return _states.Values.ToArray();
        }
    }

    public void Submit(SignalValue value)
    {
        var diffs = new List<AlarmState>();
        lock (_sync)
        {
            var current = _states;
            var next = current;
            foreach (var rule in _rules)
            {
                current.TryGetValue(rule.Code, out var prior);
                var updated = rule.Evaluate(value, prior);
                if (updated is null) continue;
                next = next.SetItem(rule.Code, updated);
                diffs.Add(updated);
            }
            _states = next;
        }
        foreach (var state in diffs)
            _changes.OnNext(state);
    }

    public void ReplaceRules(IReadOnlyList<IAlarmRule> rules)
    {
        var dismissed = new List<AlarmState>();
        lock (_sync)
        {
            foreach (var prior in _states.Values)
            {
                if (!prior.Active) continue;
                dismissed.Add(prior with { Active = false, Since = DateTimeOffset.UtcNow });
            }
            _states = ImmutableDictionary<string, AlarmState>.Empty;
            _rules = rules;
        }
        foreach (var state in dismissed)
            _changes.OnNext(state);
    }

    public void Dispose()
    {
        _changes.OnCompleted();
        _changes.Dispose();
    }
}
```

- [ ] **Step 6: `StatusBarViewModelTests` 의 `FakeAlarmEngine` 회귀 fix**

`tests/Wpf.Tests/Shell/StatusBarViewModelTests.cs` 의 `FakeAlarmEngine` 클래스에 한 줄 추가:

```csharp
    private sealed class FakeAlarmEngine : IAlarmEngine
    {
        public readonly Subject<AlarmState> Changes = new();
        public IObservable<AlarmState> AlarmChanges => Changes;
        public IReadOnlyCollection<AlarmState> CurrentAlarms { get; set; } = Array.Empty<AlarmState>();
        public void Submit(SignalValue value) { }
        public void ReplaceRules(IReadOnlyList<IAlarmRule> rules) { }
    }
```

추가로 파일 상단 `using` 절에 다음을 추가 (없는 경우):
```csharp
using CanMonitor.Core.Abstractions;
```

(이미 있으면 스킵.)

- [ ] **Step 7: `SessionViewModelTests` 의 `FakeAlarmEngine` 회귀 fix**

`tests/Wpf.Tests/Shell/SessionViewModelTests.cs` 의 `FakeAlarmEngine` 클래스에 같은 줄 추가:

```csharp
    private sealed class FakeAlarmEngine : IAlarmEngine
    {
        public IObservable<AlarmState> AlarmChanges => Observable.Never<AlarmState>();
        public IReadOnlyCollection<AlarmState> CurrentAlarms => Array.Empty<AlarmState>();
        public void Submit(SignalValue value) { }
        public void ReplaceRules(IReadOnlyList<IAlarmRule> rules) { }
    }
```

추가로 `using CanMonitor.Core.Abstractions;` 가 이미 있는지 확인 — 이 파일은 이미 포함하고 있다.

- [ ] **Step 8: 빌드 + 테스트 통과 확인**

Run: `dotnet build CanMonitor.sln`
Expected: 0 errors.

Run: `dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~AlarmEngineReplaceRulesTests"`
Expected: PASS — 3 개 테스트 모두 green.

Run: `dotnet test tests/Wpf.Tests/CanMonitor.Wpf.Tests.csproj`
Expected: PASS — 회귀 fix 통과.

- [ ] **Step 9: 커밋**

```bash
git rm src/Application/Alarms/IAlarmRule.cs
git add src/Core/Abstractions/IAlarmEngine.cs src/Core/Abstractions/IAlarmRule.cs src/Application/Alarms/AlarmEngine.cs src/Application/Alarms/BitAlarmRule.cs src/Application/Alarms/AlarmRuleFactory.cs tests/Application.Tests/Alarms/AlarmEngineReplaceRulesTests.cs tests/Wpf.Tests/Shell/StatusBarViewModelTests.cs tests/Wpf.Tests/Shell/SessionViewModelTests.cs
git commit -m "feat(alarms): add ReplaceRules with lock-based concurrency on AlarmEngine"
```

---

## Task 5: DI 정리 (`ServiceCollectionExtensions` + `App.xaml.cs`)

**목표:** Dead `ITxScheduler` / `BusLifecycleService` 등록 제거. `AlarmEngine` 빈 룰 배열로 시작 (이후 `SessionViewModel` 이 `ReplaceRules`). `ISignalDecoder` 등록 추가 (App 측).

**Files:**
- Modify: `src/Application/ServiceCollectionExtensions.cs`
- Modify: `src/Wpf/App.xaml.cs`

- [ ] **Step 1: `ServiceCollectionExtensions` 갱신**

`src/Application/ServiceCollectionExtensions.cs` 전체를 다음으로 교체:

```csharp
using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
using CanMonitor.Application.Services;
using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanMonitor.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCanMonitorApplication(this IServiceCollection services)
    {
        services.AddSingleton<CanEventHub>();
        services.AddSingleton<RawFrameStore>();
        services.AddSingleton<ManualBusStatusPublisher>();

        services.AddSingleton<IAlarmEngine>(_ => new AlarmEngine(Array.Empty<IAlarmRule>()));
        services.AddSingleton<CanTransmitService>();

        services.AddSingleton<IVirtualInputService, VirtualInputService>();

        services.AddSingleton<Eec1HeartbeatProvider>();
        services.AddSingleton<VirtualInputHeartbeat>();
        services.AddSingleton<IBusHeartbeatProvider>(sp => sp.GetRequiredService<Eec1HeartbeatProvider>());
        services.AddSingleton<IBusHeartbeatProvider>(sp => sp.GetRequiredService<VirtualInputHeartbeat>());

        services.AddSingleton<IStepExecutor, WaitStepExecutor>();
        services.AddSingleton<IStepExecutor, SendCanFrameStepExecutor>();
        services.AddSingleton<IStepExecutor, ObserveSignalStepExecutor>();
        services.AddSingleton<IStepExecutor, ObserveBitStepExecutor>();
        services.AddSingleton<IStepExecutor, AssertFrameRateStepExecutor>();
        services.AddSingleton<IStepExecutor, ManualConfirmStepExecutor>();
        services.AddSingleton<IStepExecutor, SetHeartbeatStepExecutor>();
        services.AddSingleton<IStepExecutor, SetVirtualInputStepExecutor>();
        services.AddSingleton<IStepExecutor, EnterSimulationModeStepExecutor>();
        services.AddSingleton<IStepExecutor, ExitSimulationModeStepExecutor>();

        services.AddTransient<ITestRunner, TestRunner>();
        return services;
    }
}
```

> 변경점: (a) `services.AddSingleton<ITxScheduler, TxScheduler>()` 제거, (b) `services.AddSingleton<BusLifecycleService>()` 제거, (c) `AlarmRuleFactory.CreatePhase2aRules()` → `Array.Empty<IAlarmRule>()`.

- [ ] **Step 2: `App.xaml.cs` 갱신 — `ISignalDecoder` 등록**

`src/Wpf/App.xaml.cs` 의 `ConfigureServices` 안 `services` 체인에 한 줄 추가 (`AddSingleton<IDbcProvider, DbcParserLibProvider>()` 다음):

```csharp
            .AddSingleton<IDbcProvider, DbcParserLibProvider>()
            .AddSingleton<ISignalDecoder, SignalDecoder>()
            .AddSingleton<Wpf.Infrastructure.ICanBusFactory, CanBusFactory>()
```

(파일 상단 `using CanMonitor.Dbc;` 가 이미 있는지 확인 — Phase 3a 에서 추가되었음, 그대로 유지.)

- [ ] **Step 3: 빌드 통과 확인**

Run: `dotnet build CanMonitor.sln`
Expected: 0 errors.

Run: `dotnet test`
Expected: 모든 기존 테스트 PASS — `ITxScheduler`/`BusLifecycleService` 의 DI 호출자가 없으므로 회귀 없음.

- [ ] **Step 4: 커밋**

```bash
git add src/Application/ServiceCollectionExtensions.cs src/Wpf/App.xaml.cs
git commit -m "chore(di): remove dead TxScheduler/BusLifecycleService registrations, register SignalDecoder"
```

---

## Task 6: `SessionViewModel` — `ReloadDbcAsync` + 파이프라인 조립

**목표:** `ConnectAsync` 가 `CanReceivePipeline` 을 새로 만들어 `_hub.Attach` 의 `signals` 인자에 연결. `SelectedDbc` setter 가 `LoadAsync` 후 `ReplaceRules`. `InitializeAsync` 도 같은 헬퍼 사용.

**Files:**
- Modify: `src/Wpf/Shell/SessionViewModel.cs`
- Create: `tests/Wpf.Tests/Shell/SessionViewModelDbcReloadTests.cs`
- Create: `tests/Wpf.Tests/Shell/SessionViewModelPipelineTests.cs`
- Modify: `tests/Wpf.Tests/Shell/SessionViewModelTests.cs` — `FakeDbcProvider.Current` 갱신 + 생성자 시그니처 fix

- [ ] **Step 1: 신규 테스트 — `SessionViewModelDbcReloadTests`**

`tests/Wpf.Tests/Shell/SessionViewModelDbcReloadTests.cs`:

```csharp
using System.Collections.Immutable;
using System.IO;
using System.Reactive.Linq;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CanMonitor.Wpf.Shell;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Wpf.Tests.Shell;

public class SessionViewModelDbcReloadTests
{
    private sealed class FakeDbcProvider : IDbcProvider
    {
        public DbcDatabase Current { get; private set; } = DbcDatabase.Empty;
        public event EventHandler<DbcDatabase>? DatabaseReplaced;
        public List<string> LoadedPaths { get; } = new();

        public Task LoadAsync(string path, CancellationToken ct = default)
        {
            LoadedPaths.Add(path);
            var msg = new DbcMessage(0x200, false, "Alarms_0x200", 8,
                ImmutableArray.Create(new DbcSignal("EEC1_Timeout", 25, 1, false, false, 1, 0, 0, 1, null, null)),
                null);
            Current = new DbcDatabase(new[] { msg });
            DatabaseReplaced?.Invoke(this, Current);
            return Task.CompletedTask;
        }

        public Task SaveAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class CapturingAlarmEngine : IAlarmEngine
    {
        public IObservable<AlarmState> AlarmChanges => Observable.Never<AlarmState>();
        public IReadOnlyCollection<AlarmState> CurrentAlarms => Array.Empty<AlarmState>();
        public List<int> RuleCountAtReplace { get; } = new();
        public void Submit(SignalValue value) { }
        public void ReplaceRules(IReadOnlyList<IAlarmRule> rules) => RuleCountAtReplace.Add(rules.Count);
    }

    private sealed class FakeDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
        public IObservable<CanFrame> UnknownFrames => Observable.Never<CanFrame>();
    }

    private static string CreateTempDbc(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "VERSION \"\"\n");
        return path;
    }

    [Fact]
    public async Task InitializeAsync_loads_dbc_and_replaces_rules()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-rel-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var dbc = new FakeDbcProvider();
        var alarms = new CapturingAlarmEngine();
        var decoder = new FakeDecoder();
        var vm = new SessionViewModel(new CanBusFactory(), dbc, new CanEventHub(),
            new ManualBusStatusPublisher(), alarms, decoder,
            Array.Empty<IBusHeartbeatProvider>(), tmp);

        await vm.InitializeAsync();

        dbc.LoadedPaths.Should().ContainSingle();
        alarms.RuleCountAtReplace.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public async Task SelectedDbc_change_triggers_reload_and_replace()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-rel-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");
        CreateTempDbc(confirmed, "160HP_WithPto.dbc");

        var dbc = new FakeDbcProvider();
        var alarms = new CapturingAlarmEngine();
        var decoder = new FakeDecoder();
        var vm = new SessionViewModel(new CanBusFactory(), dbc, new CanEventHub(),
            new ManualBusStatusPublisher(), alarms, decoder,
            Array.Empty<IBusHeartbeatProvider>(), tmp);

        await vm.InitializeAsync();
        var initialLoads = dbc.LoadedPaths.Count;

        var other = vm.DbcFiles.First(f => f.DisplayName == "160HP_WithPto.dbc");
        vm.SelectedDbc = other;

        await Task.Delay(50);  // fire-and-forget 완료 대기

        dbc.LoadedPaths.Count.Should().BeGreaterThan(initialLoads);
        alarms.RuleCountAtReplace.Should().HaveCountGreaterOrEqualTo(2);
    }
}
```

- [ ] **Step 2: 신규 테스트 — `SessionViewModelPipelineTests`**

`tests/Wpf.Tests/Shell/SessionViewModelPipelineTests.cs`:

```csharp
using System.Collections.Immutable;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CanMonitor.Wpf.Shell;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Wpf.Tests.Shell;

public class SessionViewModelPipelineTests
{
    private sealed class FakeDbcProvider : IDbcProvider
    {
        public DbcDatabase Current { get; private set; } = DbcDatabase.Empty;
        public event EventHandler<DbcDatabase>? DatabaseReplaced;
        public Task LoadAsync(string path, CancellationToken ct = default)
        {
            var msg = new DbcMessage(0x100, false, "Status_0x100", 8,
                ImmutableArray.Create(new DbcSignal("Speed", 0, 16, true, false, 1, 0, 0, 65535, "rpm", null)),
                null);
            Current = new DbcDatabase(new[] { msg });
            DatabaseReplaced?.Invoke(this, Current);
            return Task.CompletedTask;
        }
        public Task SaveAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubBus : ICanBus
    {
        public string Name => "Stub";
        public bool IsOpen { get; private set; }
        public Subject<CanFrame> RxSubject { get; } = new();
        public IObservable<CanFrame> Frames => RxSubject;
        public Task OpenAsync(CanBusOptions options, CancellationToken ct = default) { IsOpen = true; return Task.CompletedTask; }
        public Task CloseAsync() { IsOpen = false; return Task.CompletedTask; }
        public Task SendAsync(CanFrame frame, CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() { RxSubject.Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class StubFactory : ICanBusFactory
    {
        public StubBus Bus { get; } = new();
        public IReadOnlyList<AdapterOption> Known { get; } =
            new[] { new AdapterOption(AdapterKind.Virtual, "Stub") };
        public ICanBus Create(AdapterKind kind) => Bus;
    }

    private static string CreateTempDbc(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "VERSION \"\"\n");
        return path;
    }

    [Fact]
    public async Task ConnectAsync_assembles_pipeline_and_decoded_signals_flow_through_hub()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-pipe-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var factory = new StubFactory();
        var dbc = new FakeDbcProvider();
        var hub = new CanEventHub();
        var publisher = new ManualBusStatusPublisher();
        var alarms = new AlarmEngine(Array.Empty<IAlarmRule>());
        var decoder = new CanMonitor.Dbc.SignalDecoder(dbc);
        var vm = new SessionViewModel(factory, dbc, hub, publisher, alarms, decoder,
            Array.Empty<IBusHeartbeatProvider>(), tmp);

        await vm.InitializeAsync();
        await vm.ConnectCommand.ExecuteAsync(null);

        var seen = new List<SignalValue>();
        using var sub = hub.Signals.Subscribe(seen.Add);

        factory.Bus.RxSubject.OnNext(new CanFrame(0x100, false,
            new byte[] { 0x09, 0xC4, 0, 0, 0, 0, 0, 0 }, DateTimeOffset.UtcNow, CanDirection.Rx));

        await Task.Delay(100);  // pool scheduler hop 대기

        seen.Should().NotBeEmpty();
        seen.Should().Contain(s => s.SignalName == "Speed");
    }

    [Fact]
    public async Task DisconnectAsync_clears_pipeline_reference()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cm-pipe-" + Guid.NewGuid().ToString("N"));
        var confirmed = Path.Combine(tmp, "confirmed");
        CreateTempDbc(confirmed, "120HP_NoPto.dbc");

        var factory = new StubFactory();
        var dbc = new FakeDbcProvider();
        var alarms = new AlarmEngine(Array.Empty<IAlarmRule>());
        var decoder = new CanMonitor.Dbc.SignalDecoder(dbc);
        var vm = new SessionViewModel(factory, dbc, new CanEventHub(),
            new ManualBusStatusPublisher(), alarms, decoder,
            Array.Empty<IBusHeartbeatProvider>(), tmp);

        await vm.InitializeAsync();
        await vm.ConnectCommand.ExecuteAsync(null);
        await vm.DisconnectAsync();

        // 두 번째 Connect 가 새 파이프라인을 만들어 throw 없이 동작
        await vm.ConnectCommand.ExecuteAsync(null);
        vm.State.Should().Be(ConnectionState.Connected);
    }
}
```

- [ ] **Step 3: `SessionViewModelTests` 의 `FakeDbcProvider` 갱신 + 생성자 시그니처 fix**

`tests/Wpf.Tests/Shell/SessionViewModelTests.cs` 변경:

3-1) `FakeDbcProvider.LoadAsync` 의 `Current` 할당부를 `120HP_NoPto.dbc` 의 시그니처에 맞춰 의미 있게 갱신:

```csharp
        public Task LoadAsync(string path, CancellationToken ct = default)
        {
            LoadedPaths.Add(path);
            if (ShouldFail) throw new InvalidOperationException("fake failure");
            var msg = new DbcMessage(0x200, false, "Alarms_0x200", 8,
                System.Collections.Immutable.ImmutableArray.Create(
                    new DbcSignal("EEC1_Timeout", 25, 1, false, false, 1, 0, 0, 1, null, null)),
                null);
            Current = new DbcDatabase(new[] { msg });
            DatabaseReplaced?.Invoke(this, Current);
            return Task.CompletedTask;
        }
```

3-2) `FakeAlarmEngine` 에는 Step 4-7 에서 이미 `ReplaceRules` 가 추가됨 — 변경 없음.

3-3) `CreateVm` 헬퍼에 `ISignalDecoder` 인자 추가:

```csharp
    private sealed class FakeDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
        public IObservable<CanFrame> UnknownFrames => Observable.Never<CanFrame>();
    }

    private static SessionViewModel CreateVm(string rootDir, FakeDbcProvider dbc, CanBusFactory factory,
        CanEventHub hub, ManualBusStatusPublisher publisher)
        => new SessionViewModel(factory, dbc, hub, publisher,
            new FakeAlarmEngine(), new FakeDecoder(),
            Array.Empty<IBusHeartbeatProvider>(), rootDir);
```

(파일 상단에 이미 `using System.Reactive.Linq;` 이 있는지 확인 — 있으면 그대로, 없다면 추가.)

- [ ] **Step 4: 테스트 실행 — 컴파일 실패 확인**

Run: `dotnet build tests/Wpf.Tests/CanMonitor.Wpf.Tests.csproj`
Expected: FAIL — `SessionViewModel` 생성자가 6 인자 (decoder 빠짐).

- [ ] **Step 5: `SessionViewModel` 구현 갱신**

`src/Wpf/Shell/SessionViewModel.cs` 전체를 다음으로 교체:

```csharp
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanMonitor.Wpf.Shell;

public sealed partial class SessionViewModel : ObservableObject, ISessionState, IDisposable
{
    private readonly Wpf.Infrastructure.ICanBusFactory _factory;
    private readonly IDbcProvider _dbcProvider;
    private readonly CanEventHub _hub;
    private readonly ManualBusStatusPublisher _publisher;
    private readonly IAlarmEngine _alarmEngine;
    private readonly ISignalDecoder _decoder;
    private readonly IEnumerable<IBusHeartbeatProvider> _heartbeatProviders;
    private readonly string _dbcRootDir;
    private readonly BehaviorSubject<ConnectionState> _stateSubject = new(ConnectionState.Disconnected);
    private readonly BehaviorSubject<DbcFileOption?> _dbcSubject = new(null);

    private ICanBus? _currentBus;
    private CanReceivePipeline? _currentPipeline;
    private TxScheduler? _currentTxScheduler;
    private BusLifecycleService? _currentLifecycle;
    private IDisposable? _hubBinding;

    [ObservableProperty] private ConnectionState _state = ConnectionState.Disconnected;
    [ObservableProperty] private string? _errorMessage;

    public SessionViewModel(
        Wpf.Infrastructure.ICanBusFactory factory,
        IDbcProvider dbcProvider,
        CanEventHub hub,
        ManualBusStatusPublisher publisher,
        IAlarmEngine alarmEngine,
        ISignalDecoder decoder,
        IEnumerable<IBusHeartbeatProvider> heartbeatProviders,
        string dbcRootDir = "dbc")
    {
        _factory = factory;
        _dbcProvider = dbcProvider;
        _hub = hub;
        _publisher = publisher;
        _alarmEngine = alarmEngine;
        _decoder = decoder;
        _heartbeatProviders = heartbeatProviders;
        _dbcRootDir = dbcRootDir;

        Adapters = _factory.Known;
        SelectedAdapter = Adapters.First();
    }

    public IReadOnlyList<AdapterOption> Adapters { get; }
    public AdapterOption SelectedAdapter { get; set; }
    public IReadOnlyList<DbcFileOption> DbcFiles { get; private set; } = Array.Empty<DbcFileOption>();

    private DbcFileOption? _selectedDbc;
    public DbcFileOption? SelectedDbc
    {
        get => _selectedDbc;
        set
        {
            if (SetProperty(ref _selectedDbc, value))
            {
                _dbcSubject.OnNext(value);
                if (value is not null) _ = ReloadDbcAsync(value.Path);
            }
        }
    }

    public IObservable<ConnectionState> StateChanges => _stateSubject;
    public IObservable<DbcFileOption?> DbcChanges => _dbcSubject;

    public async Task InitializeAsync()
    {
        DbcFiles = ScanDbcFolder(_dbcRootDir);
        SelectedDbc = DbcFiles.FirstOrDefault(f => f.DisplayName.Equals("120HP_NoPto.dbc", StringComparison.OrdinalIgnoreCase))
                      ?? DbcFiles.FirstOrDefault();
        if (SelectedDbc is null) return;
        await ReloadDbcAsync(SelectedDbc.Path);
    }

    private async Task ReloadDbcAsync(string path)
    {
        try
        {
            await _dbcProvider.LoadAsync(path);
            _alarmEngine.ReplaceRules(AlarmRuleFactory.FromDbc(_dbcProvider.Current));
            if (State == ConnectionState.Error)
                SetState(ConnectionState.Disconnected);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            SetState(ConnectionState.Error);
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting) return;
        try
        {
            SetState(ConnectionState.Connecting);
            _currentBus = _factory.Create(SelectedAdapter.Kind);
            await _currentBus.OpenAsync(new CanBusOptions());
            _currentPipeline = new CanReceivePipeline(_currentBus.Frames, _decoder, _alarmEngine);
            _currentTxScheduler = new TxScheduler(_currentBus);
            _currentLifecycle = new BusLifecycleService(_heartbeatProviders, _currentTxScheduler);
            _hubBinding = _hub.Attach(_currentBus,
                _currentPipeline.DecodedSignals,
                _alarmEngine.AlarmChanges,
                _publisher.Changes);
            _currentLifecycle.Start();
            _publisher.Publish(new BusStatusChange(BusStatus.Connected, null, null, 0, DateTimeOffset.UtcNow));
            SetState(ConnectionState.Connected);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            SetState(ConnectionState.Error);
            ErrorMessage = ex.Message;
            await DisconnectAsync();
        }
    }

    [RelayCommand]
    private Task DisconnectCommandImpl() => DisconnectAsync();

    public async Task DisconnectAsync()
    {
        if (_currentBus is null && State == ConnectionState.Disconnected) return;
        try
        {
            if (_currentLifecycle is not null) await _currentLifecycle.DisposeAsync();
            if (_currentTxScheduler is not null) await _currentTxScheduler.DisposeAsync();
            _hubBinding?.Dispose();
            if (_currentBus is not null) await _currentBus.CloseAsync();
            _publisher.Publish(new BusStatusChange(BusStatus.Disconnected, null, null, 0, DateTimeOffset.UtcNow));
        }
        finally
        {
            _currentLifecycle = null;
            _currentTxScheduler = null;
            _hubBinding = null;
            _currentPipeline = null;
            _currentBus = null;
            SetState(ConnectionState.Disconnected);
        }
    }

    private void SetState(ConnectionState s)
    {
        State = s;
        _stateSubject.OnNext(s);
    }

    private static IReadOnlyList<DbcFileOption> ScanDbcFolder(string root)
    {
        var result = new List<DbcFileOption>();
        Add(Path.Combine(root, "confirmed"), DbcSource.Confirmed);
        Add(Path.Combine(root, "experimental"), DbcSource.Experimental);
        return result;

        void Add(string dir, DbcSource src)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var path in Directory.EnumerateFiles(dir, "*.dbc"))
                result.Add(new DbcFileOption(path, Path.GetFileName(path), src));
        }
    }

    public void Dispose()
    {
        _hubBinding?.Dispose();
        _stateSubject.Dispose();
        _dbcSubject.Dispose();
    }
}
```

- [ ] **Step 6: 빌드 + 테스트 통과 확인**

Run: `dotnet build CanMonitor.sln`
Expected: 0 errors.

Run: `dotnet test tests/Wpf.Tests/CanMonitor.Wpf.Tests.csproj`
Expected: PASS — 신규 6 테스트 + 기존 회귀 통과.

- [ ] **Step 7: 커밋**

```bash
git add src/Wpf/Shell/SessionViewModel.cs tests/Wpf.Tests/Shell/SessionViewModelTests.cs tests/Wpf.Tests/Shell/SessionViewModelDbcReloadTests.cs tests/Wpf.Tests/Shell/SessionViewModelPipelineTests.cs
git commit -m "feat(shell): wire CanReceivePipeline + DBC-driven AlarmRule reload in SessionViewModel"
```

---

## Task 7: `StatusBarViewModel` — `DecodeFailures` 활성화

**목표:** Decoder 의 `UnknownFrames` 를 1 초 윈도우로 buffer + 누적 카운트해 `DecodeFailures` 표시.

**Files:**
- Modify: `src/Wpf/Shell/StatusBarViewModel.cs`
- Create: `tests/Wpf.Tests/Shell/StatusBarDecodeFailuresTests.cs`
- Modify: `tests/Wpf.Tests/Shell/StatusBarViewModelTests.cs` — 생성자 시그니처 fix + `FakeDecoder`

- [ ] **Step 1: 신규 테스트 — `StatusBarDecodeFailuresTests`**

`tests/Wpf.Tests/Shell/StatusBarDecodeFailuresTests.cs`:

```csharp
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CanMonitor.Wpf.Shell;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace CanMonitor.Wpf.Tests.Shell;

public class StatusBarDecodeFailuresTests
{
    private sealed class FakeSessionState : ISessionState
    {
        public IObservable<ConnectionState> StateChanges => Observable.Return(ConnectionState.Disconnected);
        public IObservable<DbcFileOption?> DbcChanges => Observable.Return<DbcFileOption?>(null);
    }

    private sealed class FakeAlarmEngine : IAlarmEngine
    {
        public IObservable<AlarmState> AlarmChanges => Observable.Never<AlarmState>();
        public IReadOnlyCollection<AlarmState> CurrentAlarms => Array.Empty<AlarmState>();
        public void Submit(SignalValue value) { }
        public void ReplaceRules(IReadOnlyList<IAlarmRule> rules) { }
    }

    private sealed class FakeDecoder : ISignalDecoder
    {
        public Subject<CanFrame> Unknown { get; } = new();
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
        public IObservable<CanFrame> UnknownFrames => Unknown;
    }

    [Fact]
    public void DecodeFailures_accumulates_unknown_frames_per_window()
    {
        var sched = new TestScheduler();
        var hub = new CanEventHub();
        var store = new RawFrameStore();
        var alarms = new FakeAlarmEngine();
        var session = new FakeSessionState();
        var decoder = new FakeDecoder();

        var vm = new StatusBarViewModel(hub, store, alarms, session, decoder, sched);

        var f = new CanFrame(0xDEAD, true, new byte[] { 0 }, DateTimeOffset.UtcNow, CanDirection.Rx);
        decoder.Unknown.OnNext(f);
        decoder.Unknown.OnNext(f);

        sched.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        vm.DecodeFailures.Should().Be(2);

        decoder.Unknown.OnNext(f);
        sched.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        vm.DecodeFailures.Should().Be(3);
    }
}
```

- [ ] **Step 2: 테스트 실행 — 컴파일 실패 확인**

Run: `dotnet build tests/Wpf.Tests/CanMonitor.Wpf.Tests.csproj`
Expected: FAIL — `StatusBarViewModel` 생성자가 5 인자 (decoder 빠짐).

- [ ] **Step 3: `StatusBarViewModel` 구현 갱신**

`src/Wpf/Shell/StatusBarViewModel.cs` 전체를 다음으로 교체:

```csharp
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanMonitor.Wpf.Shell;

public sealed partial class StatusBarViewModel : ObservableObject, IDisposable
{
    private readonly CompositeDisposable _subscriptions = new();

    [ObservableProperty] private int _rxPerSecond;
    [ObservableProperty] private int _txPerSecond;
    [ObservableProperty] private long _droppedFrames;
    [ObservableProperty] private long _decodeFailures;
    [ObservableProperty] private int _activeAlarms;
    [ObservableProperty] private string _dbcFileLabel = string.Empty;
    [ObservableProperty] private ConnectionState _session;

    public StatusBarViewModel(
        CanEventHub hub,
        RawFrameStore store,
        IAlarmEngine alarmEngine,
        ISessionState sessionState,
        ISignalDecoder decoder,
        IScheduler uiScheduler)
    {
        var window = TimeSpan.FromSeconds(1);

        _subscriptions.Add(hub.Frames
            .Where(f => f.Direction == CanDirection.Rx)
            .Buffer(window, uiScheduler)
            .Subscribe(batch => RxPerSecond = batch.Count));

        _subscriptions.Add(hub.Frames
            .Where(f => f.Direction == CanDirection.Tx)
            .Buffer(window, uiScheduler)
            .Subscribe(batch => TxPerSecond = batch.Count));

        _subscriptions.Add(Observable
            .Interval(window, uiScheduler)
            .Select(_ => store.DroppedCount)
            .Subscribe(v => DroppedFrames = v));

        _subscriptions.Add(decoder.UnknownFrames
            .Buffer(window, uiScheduler)
            .Scan(0L, (acc, batch) => acc + batch.Count)
            .Subscribe(v => DecodeFailures = v));

        var initialActive = alarmEngine.CurrentAlarms.Count(a => a.Active);
        ActiveAlarms = initialActive;
        _subscriptions.Add(alarmEngine.AlarmChanges
            .Scan(initialActive, (count, alarm) => alarm.Active ? count + 1 : Math.Max(count - 1, 0))
            .ObserveOn(uiScheduler)
            .Subscribe(v => ActiveAlarms = v));

        _subscriptions.Add(sessionState.StateChanges
            .Subscribe(s => Session = s));

        _subscriptions.Add(sessionState.DbcChanges
            .Select(d => d?.DisplayName ?? string.Empty)
            .Subscribe(label => DbcFileLabel = label));
    }

    public void Dispose() => _subscriptions.Dispose();
}
```

- [ ] **Step 4: `StatusBarViewModelTests` 회귀 fix**

`tests/Wpf.Tests/Shell/StatusBarViewModelTests.cs` 변경:

4-1) 파일 안에 `FakeDecoder` 헬퍼 추가 (`FakeAlarmEngine` 다음):

```csharp
    private sealed class FakeDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
        public IObservable<CanFrame> UnknownFrames => Observable.Never<CanFrame>();
    }
```

4-2) 모든 `new StatusBarViewModel(hub, store, alarms, session, sched)` 호출 5 군데를 다음으로 변경:

```csharp
        var vm = new StatusBarViewModel(hub, store, alarms, session, new FakeDecoder(), sched);
```

(테스트별 위치 — `RxPerSecond_counts_Rx_frames_over_one_second`, `DroppedFrames_reflects_store_counter_after_sample`, `ActiveAlarms_counts_Active_state_transitions`, `Session_updates_on_state_change`, `DbcFileLabel_is_empty_when_no_selection` 다섯 곳 모두.)

- [ ] **Step 5: `App.xaml.cs` 의 `StatusBarViewModel` 등록 확인**

`src/Wpf/App.xaml.cs` 의 `services.AddSingleton<StatusBarViewModel>();` 는 DI 가 자동으로 `ISignalDecoder` 를 주입 — Step 5 (Task 5) 에서 등록했으므로 변경 없음.

- [ ] **Step 6: 빌드 + 테스트 통과 확인**

Run: `dotnet build CanMonitor.sln`
Expected: 0 errors.

Run: `dotnet test tests/Wpf.Tests/CanMonitor.Wpf.Tests.csproj`
Expected: PASS — 신규 1 테스트 + 회귀 5 테스트 통과.

- [ ] **Step 7: 커밋**

```bash
git add src/Wpf/Shell/StatusBarViewModel.cs tests/Wpf.Tests/Shell/StatusBarDecodeFailuresTests.cs tests/Wpf.Tests/Shell/StatusBarViewModelTests.cs
git commit -m "feat(shell): activate DecodeFailures counter from SignalDecoder.UnknownFrames"
```

---

## Task 8: 통합 테스트 `Phase3bPipelineTests`

**목표:** VirtualBus + 실제 `120HP_NoPto.dbc` 로 end-to-end 시나리오. EEC1_Timeout 비트 0→1 → AlarmEngine emit; 1→0 → emit; ReplaceRules → dismiss.

**Files:**
- Create: `tests/Integration.Tests/Phase3b/Phase3bPipelineTests.cs`

- [ ] **Step 1: 통합 테스트 파일 생성**

`tests/Integration.Tests/Phase3b/Phase3bPipelineTests.cs`:

```csharp
using System.Collections.Immutable;
using System.Reactive.Linq;
using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Dbc;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Integration.Tests.Phase3b;

public sealed class Phase3bPipelineTests
{
    private sealed class InMemoryDbcProvider : IDbcProvider
    {
        public InMemoryDbcProvider(DbcDatabase initial) { Current = initial; }
        public DbcDatabase Current { get; private set; }
        public event EventHandler<DbcDatabase>? DatabaseReplaced;
        public Task LoadAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(string path, CancellationToken ct = default) => Task.CompletedTask;

        public void Replace(DbcDatabase next)
        {
            Current = next;
            DatabaseReplaced?.Invoke(this, next);
        }
    }

    // 25 번 비트 = byte index 25/8 = 3, bit-in-byte = 25 & 7 = 1.
    // Motorola(big-endian) DBC startBit=25 → SignalDecoder.ExtractMotorola 동일 위치.
    // payload 의 byte 3 의 bit 1 을 1 로 설정.
    private static byte[] MakeAlarmsPayload(bool eec1Timeout)
    {
        var data = new byte[8];
        if (eec1Timeout) data[3] = 1 << 1;
        return data;
    }

    private static DbcDatabase MakeAlarmsDb()
    {
        var msg = new DbcMessage(
            Id: 0x200,
            IsExtended: false,
            Name: "Alarms_0x200",
            Dlc: 8,
            Signals: ImmutableArray.Create(
                new DbcSignal("EEC1_Timeout", 25, 1, false, false, 1, 0, 0, 1, null, null)),
            CycleTime: null);
        return new DbcDatabase(new[] { msg });
    }

    [Fact]
    public async Task Pipeline_emits_alarm_active_then_inactive_for_eec1_timeout_bit()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var dbc = new InMemoryDbcProvider(MakeAlarmsDb());
        using var decoder = new SignalDecoder(dbc);
        using var alarms = new AlarmEngine(AlarmRuleFactory.FromDbc(dbc.Current));
        var pipeline = new CanReceivePipeline(bus.Frames, decoder, alarms);

        var seen = new List<AlarmState>();
        using var alarmSub = alarms.AlarmChanges.Subscribe(seen.Add);
        using var pipeSub = pipeline.DecodedSignals.Subscribe();

        bus.Inject(new CanFrame(0x200, false, MakeAlarmsPayload(eec1Timeout: true), DateTimeOffset.UtcNow, CanDirection.Rx));
        await Task.Delay(100);

        bus.Inject(new CanFrame(0x200, false, MakeAlarmsPayload(eec1Timeout: false), DateTimeOffset.UtcNow, CanDirection.Rx));
        await Task.Delay(100);

        seen.Should().HaveCount(2);
        seen[0].Code.Should().Be("EEC1_Timeout");
        seen[0].Active.Should().BeTrue();
        seen[1].Code.Should().Be("EEC1_Timeout");
        seen[1].Active.Should().BeFalse();
    }

    [Fact]
    public async Task ReplaceRules_dismisses_active_alarm_when_dbc_changes()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var dbc = new InMemoryDbcProvider(MakeAlarmsDb());
        using var decoder = new SignalDecoder(dbc);
        using var alarms = new AlarmEngine(AlarmRuleFactory.FromDbc(dbc.Current));
        var pipeline = new CanReceivePipeline(bus.Frames, decoder, alarms);

        var seen = new List<AlarmState>();
        using var alarmSub = alarms.AlarmChanges.Subscribe(seen.Add);
        using var pipeSub = pipeline.DecodedSignals.Subscribe();

        bus.Inject(new CanFrame(0x200, false, MakeAlarmsPayload(eec1Timeout: true), DateTimeOffset.UtcNow, CanDirection.Rx));
        await Task.Delay(100);
        seen.Should().HaveCount(1);

        // DBC 교체 — 새 DB 는 빈 메시지셋 → FromDbc 빈 룰 → ReplaceRules 가 dismiss
        dbc.Replace(DbcDatabase.Empty);
        alarms.ReplaceRules(AlarmRuleFactory.FromDbc(dbc.Current));

        seen.Should().HaveCount(2);
        seen[1].Code.Should().Be("EEC1_Timeout");
        seen[1].Active.Should().BeFalse();
    }
}
```

- [ ] **Step 2: 테스트 통과 확인**

Run: `dotnet test tests/Integration.Tests/CanMonitor.Integration.Tests.csproj --filter "FullyQualifiedName~Phase3bPipelineTests"`
Expected: PASS — 2 개 통합 시나리오 green.

- [ ] **Step 3: 커밋**

```bash
git add tests/Integration.Tests/Phase3b/Phase3bPipelineTests.cs
git commit -m "test(integration): end-to-end Phase 3b pipeline + DBC reload alarm dismiss"
```

---

## Task 9: 전체 회귀 + 수동 스모크

**목표:** 모든 테스트 green 확인 + WPF Shell 을 띄워 Connect 시 LED 녹색 + 상태바가 placeholder 없이 동작하는지 점검.

- [ ] **Step 1: 전체 빌드 + 테스트**

Run: `dotnet build CanMonitor.sln`
Expected: 0 warnings, 0 errors.

Run: `dotnet test`
Expected: 모든 테스트 PASS — 신규 ~14 + 기존 131 = 약 145 건.

- [ ] **Step 2: WPF 스모크 실행**

Run: `dotnet run --project src/Wpf/CanMonitor.Wpf.csproj`
Expected:
- Shell 창이 뜨고 좌측 Rail 에 7 개 탭 표시.
- Connect 버튼 클릭 → 상단 LED 녹색.
- 상태바: Rx/s, Tx/s, Drop, DecodeFailures, Active Alarms, DBC Label 모두 표시.
- DBC 콤보박스에서 `120HP_NoPto.dbc` 선택 (기본).
- Disconnect 버튼 클릭 → LED 다시 회색.

체크 후 창을 닫고 종료가 깨끗한지 확인 (콘솔에 예외 없음).

- [ ] **Step 3: 전체 변경사항 점검 commit history**

Run: `git log --oneline bda293d..HEAD`
Expected: Task 1~8 의 8 개 커밋이 순서대로 보임.

- [ ] **Step 4: 최종 정리 — 커밋 불필요**

스모크가 통과하면 Phase 3b 완료. 다음 단계(Phase 3c: 위젯 구현 — Trend Chart, Signal Values, Alarm Panel) 는 별도 brainstorm.

---

## 위험 / 실행 시 확인할 것

- **Motorola big-endian 비트 위치**: `Phase3bPipelineTests.MakeAlarmsPayload` 의 byte index 와 bit position 이 `SignalDecoder.ExtractMotorola` 와 정확히 맞아야 함. 테스트 실패 시 `SignalDecoderTests` 의 Motorola 케이스를 참조해 비교.
- **fire-and-forget race**: `SelectedDbc` 를 빠르게 두 번 바꾸면 두 `ReloadDbcAsync` 가 동시 진행. Phase 3b 범위 외 — 통합 테스트는 한 번만 바꿔서 결정적.
- **`ConnectionState.Error → Disconnected` 회복**: `ReloadDbcAsync` 가 Error 상태에서 호출되면 성공 시 Disconnected 로 강제. 이미 Connected 인 상태에서는 그대로 둔다 (`if (State == ConnectionState.Error) ...` 조건).
- **`SessionViewModelTests.InitializeAsync_sets_error_on_load_failure`**: `ReloadDbcAsync` 일원화 후에도 동일 의미를 유지해야 함 — `LoadAsync` 가 throw 하므로 `ReloadDbcAsync` 의 catch 가 잡아 Error 로 천이. 통과해야 함.
- **`IAlarmRule` 이동**: Task 4 Step 4 에서 namespace 가 바뀌므로 이미 사용 중인 모든 파일의 `using` 갱신 필요. `Application.Alarms` 와 `Core.Abstractions` 양쪽에서 호환되도록 — `Application.Alarms` 안의 파일들은 이미 같은 namespace 내부라 `using` 추가만 필요.
