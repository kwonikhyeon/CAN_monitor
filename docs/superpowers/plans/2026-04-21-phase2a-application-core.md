# Phase 2a — Application Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 2 前半부(Q1/Q3 미차단 구간)만 구현해 `TC-001`, `TC-002`, `TC-010` 3건을 `VirtualCanBus` 위에서 헤드리스 자동화한다. Phase 2b에서 Q1(EEC1 payload) / Q3(Virtual Input bitmap) 확정 후 `TC-024` + 12건을 덧붙인다.

**Deliverable 명세:**
- ✅ `CanEventHub` / `CanReceivePipeline` / `RawFrameStore` / `TxScheduler` / `CanTransmitService` — production 사용 가능한 완성본 (integration 테스트에서 end-to-end 검증).
- ⚠️ `AlarmEngine` — **구현체와 unit 테스트만** 전달. `AlarmRuleFactory.CreatePhase2aRules()`는 Phase 2a에서 `Array.Empty<IAlarmRule>()` 반환하며, 실제 production 활성화(rule 작성 + hub 구독)는 Plan B에서 Q1/Q3 신호 확정 후 진행. TC-002/010 동작 판정은 `AlarmEngine`이 아닌 integration 테스트가 직접 `CanEventHub.DecodedSignals`를 구독해 수행.
- ✅ Test DSL record 10종 전부 + unblocked executor 6종 + `NotSupportedException` fallback.

**Architecture:** 신규 `CanMonitor.Application` 레이어가 Rx 파이프라인(`CanEventHub` → `CanReceivePipeline` → `AlarmEngine`)과 Tx 파이프라인(`CanTransmitService` → `TxScheduler`)을 조립한다. Test DSL은 `Core`에 YAML 직렬화 가능한 pure DTO로 추가되며, `TestRunner`는 `IStepExecutor<T>` DI 디스패치로 6개 미차단 step을 실행한다. UI 관심사(Buffer/ObserveOn Dispatcher)는 이 레이어에 포함하지 않는다 — WPF는 Phase 3에서 접붙인다.

**Tech Stack:** .NET 8, System.Reactive 6.x (기존), System.Threading.Channels (BCL), Microsoft.Extensions.DependencyInjection.Abstractions 8.x, xUnit + FluentAssertions + **Microsoft.Reactive.Testing** (신규), `VirtualCanBus` (Phase 1).

**Scope boundary (Phase 2a vs. 2b):**

| Component | Phase 2a | Phase 2b (Q1/Q3 이후) |
|---|---|---|
| CanEventHub | ✅ | |
| CanReceivePipeline | ✅ | |
| RawFrameStore | ✅ | |
| AlarmEngine (구현 + unit test) | ✅ (rule 리스트는 빈 배열) | rule 작성 + hub 구독 배선 |
| TxScheduler / CanTransmitService | ✅ | |
| ManualBusStatusPublisher (BusStatus stub) | ✅ | → `BusLifecycleService` 로 교체 |
| Core Test DSL (10개 step record 전부) | ✅ (record 정의만) | |
| `ITestRunner` / `TestRunner` | ✅ (NotSupportedException fallback) | |
| `WaitStep` / `ObserveSignalStep` / `ObserveBitStep` / `SendCanFrameStep` / `AssertFrameRateStep` / `ManualConfirmStep` executors | ✅ | |
| `SetVirtualInputStep` / `SetHeartbeatStep` / `EnterSimulationModeStep` / `ExitSimulationModeStep` executors | ❌ (Plan B) | ✅ |
| `Eec1HeartbeatProvider` / `VirtualInputHeartbeat` / `VirtualInputService` impl | ❌ (Plan B) | ✅ |
| `experimental` DBC + experimental TC 자동화 | ❌ (Plan B) | ✅ |
| TC-001 / TC-002 / TC-010 end-to-end | ✅ | |
| TC-024 (EEC1 Timeout) | ❌ (Plan B) | ✅ |

---

## File Structure

**새 프로젝트: `src/Application/CanMonitor.Application.csproj`** — Rx 파이프라인 조립, Test DSL executor, DI extension. `Core`, `Dbc`, `Infrastructure.Can` 참조.

- `src/Application/Can/CanEventHub.cs` — hot `Subject<T>` relay (process lifetime).
- `src/Application/Can/RawFrameStore.cs` — `ConcurrentQueue<CanFrame>` 링버퍼 + `DroppedCount`.
- `src/Application/Can/CanReceivePipeline.cs` — `Frames → ISignalDecoder → IAlarmEngine` 조립.
- `src/Application/Can/CanTransmitService.cs` — `ICanBus` + `ITxScheduler` facade.
- `src/Application/Can/TxScheduler.cs` — `Channel`+worker 기반 ITxScheduler 구현.
- `src/Application/Can/TxJob.cs` — internal record.
- `src/Application/Can/ManualBusStatusPublisher.cs` — Plan A용 BusStatus stub (Plan B에서 `BusLifecycleService`로 교체).
- `src/Application/Alarms/AlarmEngine.cs` — rule 기반 `IAlarmEngine` impl.
- `src/Application/Alarms/IAlarmRule.cs` — rule 계약.
- `src/Application/Alarms/AlarmRuleFactory.cs` — 하드코딩된 Phase 2a rule 세트 (YAGNI: 코드 생성 도구는 Phase 3+).
- `src/Application/Testing/TestRunner.cs` — `ITestRunner` 구현, `IStepExecutor<T>` 디스패치.
- `src/Application/Testing/TestRunnerContext.cs` — executor가 공유하는 런타임 컨텍스트.
- `src/Application/Testing/Executors/WaitStepExecutor.cs`
- `src/Application/Testing/Executors/ObserveSignalStepExecutor.cs`
- `src/Application/Testing/Executors/ObserveBitStepExecutor.cs`
- `src/Application/Testing/Executors/SendCanFrameStepExecutor.cs`
- `src/Application/Testing/Executors/AssertFrameRateStepExecutor.cs`
- `src/Application/Testing/Executors/ManualConfirmStepExecutor.cs`
- `src/Application/ServiceCollectionExtensions.cs` — `AddCanMonitorApplication`.

**Core 추가 (기존 프로젝트):**

- `src/Core/Testing/TestStep.cs` — abstract record + 10개 concrete record 한 파일 (spec §17과 동일한 discriminator).
- `src/Core/Testing/TestCase.cs`
- `src/Core/Testing/TestResult.cs`
- `src/Core/Testing/TestStepProgress.cs`
- `src/Core/Abstractions/IStepExecutor.cs` — non-generic base + `IStepExecutor<TStep>`.
- `src/Core/Abstractions/ITestRunner.cs`

**새 테스트 프로젝트: `tests/Application.Tests/CanMonitor.Application.Tests.csproj`** — 단위 테스트. Core/Application 참조.

- `tests/Application.Tests/Can/CanEventHubTests.cs`
- `tests/Application.Tests/Can/RawFrameStoreTests.cs`
- `tests/Application.Tests/Can/CanReceivePipelineTests.cs`
- `tests/Application.Tests/Can/TxSchedulerTests.cs`
- `tests/Application.Tests/Can/CanTransmitServiceTests.cs`
- `tests/Application.Tests/Can/ManualBusStatusPublisherTests.cs`
- `tests/Application.Tests/Alarms/AlarmEngineTests.cs`
- `tests/Application.Tests/Testing/TestRunnerTests.cs`
- `tests/Application.Tests/Testing/Executors/WaitStepExecutorTests.cs`
- `tests/Application.Tests/Testing/Executors/ObserveSignalStepExecutorTests.cs`
- `tests/Application.Tests/Testing/Executors/ObserveBitStepExecutorTests.cs`
- `tests/Application.Tests/Testing/Executors/SendCanFrameStepExecutorTests.cs`
- `tests/Application.Tests/Testing/Executors/AssertFrameRateStepExecutorTests.cs`
- `tests/Application.Tests/Testing/Executors/ManualConfirmStepExecutorTests.cs`

**Integration.Tests에 추가:**

- `tests/Integration.Tests/TestCases/Tc001FrameRateTests.cs`
- `tests/Integration.Tests/TestCases/Tc002OperatingModeTests.cs`
- `tests/Integration.Tests/TestCases/Tc010DrivingWorkingTests.cs`

---

### Design Decisions (플랜 전반에 적용되는 전제)

1. **CanEventHub 수명**: 프로세스 수명 동안 살아있는 singleton. `Attach(...)`는 upstream subscription만 swap — 기존 downstream 구독자는 영향 받지 않고, upstream이 완료돼도 `OnCompleted`를 전파하지 않는다 (하위 consumer는 bus 교체를 알 필요 없음). 이 속성은 Task 3의 테스트로 명시 검증한다.
2. **RawFrameStore 용량**: 기본 `10_000`. 초과 시 best-effort oldest-drop + `DroppedCount` 증가. 정확한 ordered drop은 보장하지 않으며 snapshot은 race 시 drop 직후 상태를 반영할 수 있다 (UI는 이 전제하에 렌더링).
3. **TxScheduler 경합 정책**: 단일 worker + `Channel.CreateBounded<TxJob>(new(1024){ FullMode=BoundedChannelFullMode.DropOldest })`. CAN 송신은 직렬화되어야 하므로 병렬 worker 금지.
4. **AlarmEngine 상태**: `ImmutableDictionary<string, AlarmState>`을 전체 교체. `CurrentAlarms`는 `.Values` snapshot을 반환하고, `AlarmChanges`는 상태가 변한 항목만 emit.
5. **Buffer/Dispatcher 분리**: `Buffer(33ms).GroupBy.Last` 등 UI 스로틀링은 절대 Application 레이어에 넣지 않는다 — WPF 전용. Application 테스트가 UI 스로틀링 시간 창에 의존하면 플래키해지므로 명시적으로 금지.
6. **Alarm rule 공급**: Phase 2a의 `AlarmRuleFactory.CreatePhase2aRules()`는 `Array.Empty<IAlarmRule>()`을 반환 — TC-002/010는 `AlarmEngine`을 거치지 않고 integration 테스트가 `CanEventHub.DecodedSignals`를 직접 구독해 판정한다. Plan B에서 Q1(EEC1) / Q3(Virtual Input) 확정 후 실제 rule 세트를 추가하고 hub 배선도 활성화. DBC 기반 자동 rule 생성은 Phase 3+에서 재검토 (YAGNI).
7. **BusStatus stub**: Plan A는 `ManualBusStatusPublisher`로 BusStatus를 수동 publish. Phase 2b의 `BusLifecycleService`가 이를 대체할 때 hub 구독자 측은 변경 없음(인터페이스 동일).
8. **Executor 미구현 스텝**: Plan A `TestRunner`는 디스패치 맵에 등록되지 않은 step type을 만나면 `NotSupportedException("Step type X is not registered in Phase 2a")`을 던진다. Q3/Q1 blocked step은 record만 Core에 존재, executor는 Plan B에서 추가.
9. **Integration 테스트 DBC 선택**: `dbc/confirmed/120HP_NoPto.dbc`를 기본 fixture로 사용 (Phase 1 스냅샷). TC 테스트는 해당 DBC의 메시지/시그널 이름을 직접 참조한다.

---

## Task 1: Application 프로젝트 스캐폴딩

**Files:**
- Create: `src/Application/CanMonitor.Application.csproj`
- Modify: `CanMonitor.sln` — `src/Application/` 프로젝트 등록

- [ ] **Step 1: `CanMonitor.Application.csproj` 생성**

Create `src/Application/CanMonitor.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>CanMonitor.Application</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Core\CanMonitor.Core.csproj" />
    <ProjectReference Include="..\Dbc\CanMonitor.Dbc.csproj" />
    <ProjectReference Include="..\Infrastructure.Can\CanMonitor.Infrastructure.Can.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
    <PackageReference Include="System.Reactive" Version="6.0.1" />
    <PackageReference Include="System.Collections.Immutable" Version="8.0.0" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 솔루션에 프로젝트 등록**

Run from repo root:

```bash
dotnet sln CanMonitor.sln add src/Application/CanMonitor.Application.csproj --solution-folder src
```

Expected: `Project 'src\Application\CanMonitor.Application.csproj' added to the solution.`

- [ ] **Step 3: 빌드 검증**

```bash
dotnet build src/Application/CanMonitor.Application.csproj
```

Expected: Build succeeded. 0 Warning(s) 0 Error(s).

- [ ] **Step 4: Commit**

```bash
git add src/Application/CanMonitor.Application.csproj CanMonitor.sln
git commit -m "feat(application): scaffold CanMonitor.Application project"
```

---

## Task 2: Application.Tests 프로젝트 스캐폴딩 + Microsoft.Reactive.Testing

**Files:**
- Create: `tests/Application.Tests/CanMonitor.Application.Tests.csproj`
- Create: `tests/Application.Tests/_Smoke.cs` (첫 빌드 확인용, 이후 삭제)
- Modify: `CanMonitor.sln`

- [ ] **Step 1: `CanMonitor.Application.Tests.csproj` 생성**

Create `tests/Application.Tests/CanMonitor.Application.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <RootNamespace>CanMonitor.Application.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Core\CanMonitor.Core.csproj" />
    <ProjectReference Include="..\..\src\Application\CanMonitor.Application.csproj" />
    <ProjectReference Include="..\..\src\Infrastructure.Can\CanMonitor.Infrastructure.Can.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.Reactive.Testing" Version="6.0.1" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 스모크 테스트 파일 생성**

Create `tests/Application.Tests/_Smoke.cs`:

```csharp
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace CanMonitor.Application.Tests;

public sealed class _Smoke
{
    [Fact]
    public void TestScheduler_advances_virtual_time()
    {
        var scheduler = new TestScheduler();
        scheduler.AdvanceBy(100);
        scheduler.Clock.Should().Be(100);
    }
}
```

- [ ] **Step 3: 솔루션 등록**

```bash
dotnet sln CanMonitor.sln add tests/Application.Tests/CanMonitor.Application.Tests.csproj --solution-folder tests
```

- [ ] **Step 4: 테스트 실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj
```

Expected: Passed! - Failed: 0, Passed: 1.

- [ ] **Step 5: 스모크 테스트 제거 + 커밋**

Delete `tests/Application.Tests/_Smoke.cs` (이후 task의 실제 테스트로 대체).

```bash
git rm tests/Application.Tests/_Smoke.cs
git add tests/Application.Tests/CanMonitor.Application.Tests.csproj CanMonitor.sln
git commit -m "feat(application-tests): scaffold Application.Tests with Microsoft.Reactive.Testing"
```

---

## Task 3: CanEventHub — hot Subject relay

**Files:**
- Create: `src/Application/Can/CanEventHub.cs`
- Create: `tests/Application.Tests/Can/CanEventHubTests.cs`

### 계약

- `Frames`, `Signals`, `Alarms`, `Bus` 각각 hot `IObservable<T>` — singleton 수명 동안 `OnCompleted`를 내지 않는다.
- `Attach(ICanBus bus, IObservable<SignalValue> signals, IObservable<AlarmState> alarms, IObservable<BusStatusChange> busStatus)`은 **upstream subscription만 교체**. 이전 upstream이 `OnCompleted`를 발행해도 downstream에 전달되지 않도록 `Catch` + `TakeWhile` 없이 **중간 relay Subject**를 두는 방식으로 격리한다.
- `Attach`가 반환하는 `IDisposable`은 해당 upstream binding만 해제 (다음 Attach 호출은 Disposable 자동 교체).

- [ ] **Step 1: 테스트 파일 생성 (실패 상태)**

Create `tests/Application.Tests/Can/CanEventHubTests.cs`:

```csharp
using System.Reactive.Subjects;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class CanEventHubTests
{
    [Fact]
    public async Task Subscriber_created_before_Attach_receives_frames_after_Attach()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var hub = new CanEventHub();

        var received = new List<CanFrame>();
        using var _ = hub.Frames.Subscribe(received.Add);

        using var __ = hub.Attach(
            bus,
            signals: new Subject<SignalValue>(),
            alarms: new Subject<AlarmState>(),
            busStatus: new Subject<BusStatusChange>());

        bus.Inject(new CanFrame(0x100, false, new byte[] { 1 }, DateTimeOffset.UtcNow, CanDirection.Rx));

        received.Should().ContainSingle().Which.Id.Should().Be(0x100u);
    }

    [Fact]
    public void Upstream_OnCompleted_does_not_propagate_to_downstream()
    {
        var hub = new CanEventHub();
        var upstreamFrames = new Subject<CanFrame>();

        bool downstreamCompleted = false;
        using var _ = hub.Frames.Subscribe(_ => { }, () => downstreamCompleted = true);

        using var __ = hub.AttachRawStreams(
            upstreamFrames,
            signals: new Subject<SignalValue>(),
            alarms: new Subject<AlarmState>(),
            busStatus: new Subject<BusStatusChange>());

        upstreamFrames.OnCompleted();

        downstreamCompleted.Should().BeFalse("hub은 프로세스 수명 동안 완료되면 안 된다");
    }

    [Fact]
    public void Re_Attach_replaces_upstream_without_completing_downstream()
    {
        var hub = new CanEventHub();
        var firstUpstream = new Subject<CanFrame>();
        var secondUpstream = new Subject<CanFrame>();

        var received = new List<uint>();
        using var _ = hub.Frames.Subscribe(f => received.Add(f.Id));

        var firstBinding = hub.AttachRawStreams(
            firstUpstream,
            new Subject<SignalValue>(), new Subject<AlarmState>(), new Subject<BusStatusChange>());

        firstUpstream.OnNext(new CanFrame(0x1, false, Array.Empty<byte>(), DateTimeOffset.UtcNow, CanDirection.Rx));
        firstBinding.Dispose();

        using var __ = hub.AttachRawStreams(
            secondUpstream,
            new Subject<SignalValue>(), new Subject<AlarmState>(), new Subject<BusStatusChange>());

        secondUpstream.OnNext(new CanFrame(0x2, false, Array.Empty<byte>(), DateTimeOffset.UtcNow, CanDirection.Rx));
        firstUpstream.OnNext(new CanFrame(0x999, false, Array.Empty<byte>(), DateTimeOffset.UtcNow, CanDirection.Rx));

        received.Should().Equal(new uint[] { 0x1, 0x2 });
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~CanEventHubTests"
```

Expected: Build 실패 — `CanEventHub` 타입 없음.

- [ ] **Step 3: `CanEventHub` 구현**

Create `src/Application/Can/CanEventHub.cs`:

```csharp
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class CanEventHub : IDisposable
{
    private readonly Subject<CanFrame> _frames = new();
    private readonly Subject<SignalValue> _signals = new();
    private readonly Subject<AlarmState> _alarms = new();
    private readonly Subject<BusStatusChange> _bus = new();
    private readonly SerialDisposable _currentBinding = new();

    public IObservable<CanFrame> Frames => _frames.AsObservable();
    public IObservable<SignalValue> Signals => _signals.AsObservable();
    public IObservable<AlarmState> Alarms => _alarms.AsObservable();
    public IObservable<BusStatusChange> Bus => _bus.AsObservable();

    public IDisposable Attach(
        ICanBus bus,
        IObservable<SignalValue> signals,
        IObservable<AlarmState> alarms,
        IObservable<BusStatusChange> busStatus)
        => AttachRawStreams(bus.Frames, signals, alarms, busStatus);

    public IDisposable AttachRawStreams(
        IObservable<CanFrame> frames,
        IObservable<SignalValue> signals,
        IObservable<AlarmState> alarms,
        IObservable<BusStatusChange> busStatus)
    {
        var binding = new CompositeDisposable(
            frames.Subscribe(_frames.OnNext, _ => { }, () => { }),
            signals.Subscribe(_signals.OnNext, _ => { }, () => { }),
            alarms.Subscribe(_alarms.OnNext, _ => { }, () => { }),
            busStatus.Subscribe(_bus.OnNext, _ => { }, () => { }));

        _currentBinding.Disposable = binding;
        return binding;
    }

    public void Dispose()
    {
        _currentBinding.Dispose();
        _frames.Dispose();
        _signals.Dispose();
        _alarms.Dispose();
        _bus.Dispose();
    }
}
```

- [ ] **Step 4: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~CanEventHubTests"
```

Expected: Passed: 3.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Can/CanEventHub.cs tests/Application.Tests/Can/CanEventHubTests.cs
git commit -m "feat(application): add CanEventHub hot relay with upstream swap isolation"
```

---

## Task 4: RawFrameStore — 링버퍼

**Files:**
- Create: `src/Application/Can/RawFrameStore.cs`
- Create: `tests/Application.Tests/Can/RawFrameStoreTests.cs`

### 계약

- 기본 capacity = `10_000`.
- `Record(CanFrame)`는 비차단. capacity 초과 시 가장 오래된 항목 `TryDequeue`, `_dropped` 증가 (best-effort — 경합 시 추가 drop 가능).
- `Snapshot()`는 현재 큐의 `ToArray()` 스냅샷.
- `DroppedCount`는 `long` 누적 카운터 (`Interlocked.Read`).

- [ ] **Step 1: 실패 테스트 작성**

Create `tests/Application.Tests/Can/RawFrameStoreTests.cs`:

```csharp
using CanMonitor.Application.Can;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class RawFrameStoreTests
{
    private static CanFrame Fr(uint id) =>
        new(id, false, new byte[] { 0 }, DateTimeOffset.UtcNow, CanDirection.Rx);

    [Fact]
    public void Preserves_frames_under_capacity()
    {
        var store = new RawFrameStore(capacity: 4);
        store.Record(Fr(1));
        store.Record(Fr(2));
        store.Record(Fr(3));

        store.Snapshot().Select(f => f.Id).Should().Equal(new uint[] { 1, 2, 3 });
        store.DroppedCount.Should().Be(0);
    }

    [Fact]
    public void Drops_oldest_when_over_capacity()
    {
        var store = new RawFrameStore(capacity: 3);
        store.Record(Fr(1));
        store.Record(Fr(2));
        store.Record(Fr(3));
        store.Record(Fr(4));
        store.Record(Fr(5));

        store.Snapshot().Select(f => f.Id).Should().Equal(new uint[] { 3, 4, 5 });
        store.DroppedCount.Should().Be(2);
    }

    [Fact]
    public void Concurrent_writes_do_not_throw_and_respect_capacity()
    {
        var store = new RawFrameStore(capacity: 1_000);
        var tasks = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (int i = 0; i < 2_000; i++)
                store.Record(Fr((uint)(worker * 10_000 + i)));
        })).ToArray();
        Task.WaitAll(tasks);

        store.Snapshot().Count.Should().BeLessThanOrEqualTo(1_000);
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~RawFrameStoreTests"
```

Expected: Build 실패 — `RawFrameStore` 없음.

- [ ] **Step 3: 구현**

Create `src/Application/Can/RawFrameStore.cs`:

```csharp
using System.Collections.Concurrent;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class RawFrameStore
{
    public const int DefaultCapacity = 10_000;

    private readonly ConcurrentQueue<CanFrame> _queue = new();
    private readonly int _capacity;
    private long _dropped;

    public RawFrameStore(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public long DroppedCount => Interlocked.Read(ref _dropped);

    public void Record(CanFrame frame)
    {
        _queue.Enqueue(frame);
        while (_queue.Count > _capacity && _queue.TryDequeue(out _))
            Interlocked.Increment(ref _dropped);
    }

    public IReadOnlyCollection<CanFrame> Snapshot() => _queue.ToArray();
}
```

- [ ] **Step 4: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~RawFrameStoreTests"
```

Expected: Passed: 3.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Can/RawFrameStore.cs tests/Application.Tests/Can/RawFrameStoreTests.cs
git commit -m "feat(application): add RawFrameStore bounded ring buffer"
```

---

## Task 5: AlarmEngine — ImmutableDictionary-backed

**Files:**
- Create: `src/Application/Alarms/IAlarmRule.cs`
- Create: `src/Application/Alarms/AlarmEngine.cs`
- Create: `src/Application/Alarms/AlarmRuleFactory.cs`
- Create: `tests/Application.Tests/Alarms/AlarmEngineTests.cs`

### 계약

- `IAlarmRule.Evaluate(SignalValue value, AlarmState? current)` → `AlarmState?`; `null` = 변경 없음, 값 반환 = 현재 상태로 교체.
- `AlarmEngine`는 `ImmutableDictionary<string, AlarmState>`을 `Interlocked.Exchange`로 교체, 변경된 항목만 `AlarmChanges`에 emit.
- `AlarmRuleFactory.CreatePhase2aRules()`는 Phase 2a에서 `Array.Empty<IAlarmRule>()`을 반환 (Design Decision #6 / Deliverable 명세 참조). rule 정의와 hub 배선은 Plan B 책임.

- [ ] **Step 1: `IAlarmRule` 작성**

Create `src/Application/Alarms/IAlarmRule.cs`:

```csharp
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Alarms;

public interface IAlarmRule
{
    string Code { get; }
    AlarmState? Evaluate(SignalValue value, AlarmState? current);
}
```

- [ ] **Step 2: 실패 테스트 작성**

Create `tests/Application.Tests/Alarms/AlarmEngineTests.cs`:

```csharp
using CanMonitor.Application.Alarms;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Alarms;

public sealed class AlarmEngineTests
{
    private sealed class ThresholdRule : IAlarmRule
    {
        private readonly string _message;
        private readonly string _signal;
        private readonly double _threshold;
        public ThresholdRule(string code, string message, string signal, double threshold)
        { Code = code; _message = message; _signal = signal; _threshold = threshold; }

        public string Code { get; }
        public AlarmState? Evaluate(SignalValue value, AlarmState? current)
        {
            if (value.MessageName != _message || value.SignalName != _signal) return null;
            var shouldBeActive = value.PhysicalValue > _threshold;
            var currentActive = current?.Active ?? false;
            if (shouldBeActive == currentActive) return null;
            return new AlarmState(Code, AlarmSeverity.Warning,
                shouldBeActive ? "high" : "ok", shouldBeActive, value.Timestamp);
        }
    }

    private static SignalValue SV(string msg, string sig, double v) =>
        new(msg, sig, v, v, null, DateTimeOffset.UtcNow);

    [Fact]
    public void Emits_when_rule_first_activates()
    {
        var rule = new ThresholdRule("EL.TEST", "M", "S", 10);
        var engine = new AlarmEngine(new[] { rule });

        var emitted = new List<AlarmState>();
        using var _ = engine.AlarmChanges.Subscribe(emitted.Add);

        engine.Submit(SV("M", "S", 5));
        engine.Submit(SV("M", "S", 15));

        emitted.Should().ContainSingle().Which.Active.Should().BeTrue();
        engine.CurrentAlarms.Should().ContainSingle(a => a.Code == "EL.TEST" && a.Active);
    }

    [Fact]
    public void Emits_transition_back_to_inactive()
    {
        var rule = new ThresholdRule("EL.TEST", "M", "S", 10);
        var engine = new AlarmEngine(new[] { rule });
        var emitted = new List<AlarmState>();
        using var _ = engine.AlarmChanges.Subscribe(emitted.Add);

        engine.Submit(SV("M", "S", 15));
        engine.Submit(SV("M", "S", 3));

        emitted.Should().HaveCount(2);
        emitted[0].Active.Should().BeTrue();
        emitted[1].Active.Should().BeFalse();
        engine.CurrentAlarms.Single(a => a.Code == "EL.TEST").Active.Should().BeFalse();
    }

    [Fact]
    public void Does_not_emit_for_unchanged_state()
    {
        var rule = new ThresholdRule("EL.TEST", "M", "S", 10);
        var engine = new AlarmEngine(new[] { rule });
        var emitted = new List<AlarmState>();
        using var _ = engine.AlarmChanges.Subscribe(emitted.Add);

        engine.Submit(SV("M", "S", 15));
        engine.Submit(SV("M", "S", 20));
        engine.Submit(SV("M", "S", 25));

        emitted.Should().ContainSingle();
    }

    [Fact]
    public void Ignores_signals_not_matched_by_any_rule()
    {
        var rule = new ThresholdRule("EL.TEST", "M", "S", 10);
        var engine = new AlarmEngine(new[] { rule });
        var emitted = new List<AlarmState>();
        using var _ = engine.AlarmChanges.Subscribe(emitted.Add);

        engine.Submit(SV("OTHER", "X", 999));

        emitted.Should().BeEmpty();
        engine.CurrentAlarms.Should().BeEmpty();
    }
}
```

- [ ] **Step 3: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~AlarmEngineTests"
```

Expected: Build 실패.

- [ ] **Step 4: `AlarmEngine` 구현**

Create `src/Application/Alarms/AlarmEngine.cs`:

```csharp
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Alarms;

public sealed class AlarmEngine : IAlarmEngine
{
    private readonly IReadOnlyList<IAlarmRule> _rules;
    private readonly Subject<AlarmState> _changes = new();
    private ImmutableDictionary<string, AlarmState> _states = ImmutableDictionary<string, AlarmState>.Empty;

    public AlarmEngine(IEnumerable<IAlarmRule> rules)
    {
        _rules = rules.ToArray();
    }

    public IObservable<AlarmState> AlarmChanges => _changes.AsObservable();

    public IReadOnlyCollection<AlarmState> CurrentAlarms =>
        Volatile.Read(ref _states).Values.ToArray();

    public void Submit(SignalValue value)
    {
        var current = Volatile.Read(ref _states);
        var next = current;
        var diffs = new List<AlarmState>();

        foreach (var rule in _rules)
        {
            current.TryGetValue(rule.Code, out var prior);
            var updated = rule.Evaluate(value, prior);
            if (updated is null) continue;
            next = next.SetItem(rule.Code, updated);
            diffs.Add(updated);
        }

        if (!ReferenceEquals(current, next))
            Interlocked.Exchange(ref _states, next);

        foreach (var state in diffs)
            _changes.OnNext(state);
    }
}
```

- [ ] **Step 5: Phase 2a rule factory 작성**

Create `src/Application/Alarms/AlarmRuleFactory.cs`:

```csharp
namespace CanMonitor.Application.Alarms;

public static class AlarmRuleFactory
{
    public static IReadOnlyList<IAlarmRule> CreatePhase2aRules() => Array.Empty<IAlarmRule>();
}
```

(Phase 2a integration tests는 rule 없이도 TC-001/002/010을 판정할 수 있도록 설계되었다 — `CanEventHub.DecodedSignals` 직접 구독. 실제 rule 정의 및 hub 배선은 Plan B 범위.)

- [ ] **Step 6: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~AlarmEngineTests"
```

Expected: Passed: 4.

- [ ] **Step 7: Commit**

```bash
git add src/Application/Alarms/ tests/Application.Tests/Alarms/
git commit -m "feat(application): add AlarmEngine with ImmutableDictionary state + rule factory"
```

---

## Task 6: CanReceivePipeline — decoder → alarm

**Files:**
- Create: `src/Application/Can/CanReceivePipeline.cs`
- Create: `tests/Application.Tests/Can/CanReceivePipelineTests.cs`

### 계약

- Constructor: `(IObservable<CanFrame> source, ISignalDecoder decoder, IAlarmEngine alarmEngine, IScheduler? scheduler = null)`
- `DecodedSignals` property: hot `IObservable<SignalValue>` — source 구독을 Share/Publish한 뒤 decoder를 통과시킨 결과.
- decoder 호출 스레드는 `scheduler ?? TaskPoolScheduler.Default` (UI 스레드에서 디코딩 금지).
- 각 `SignalValue`마다 `alarmEngine.Submit`.

- [ ] **Step 1: 실패 테스트 작성**

Create `tests/Application.Tests/Can/CanReceivePipelineTests.cs`:

```csharp
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class CanReceivePipelineTests
{
    private sealed class FakeDecoder : ISignalDecoder
    {
        private readonly IReadOnlyList<SignalValue> _out;
        public FakeDecoder(IReadOnlyList<SignalValue> output) { _out = output; }
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => _out;
    }

    private sealed class RecordingAlarm : IAlarmEngine
    {
        public List<SignalValue> Submitted { get; } = new();
        public IObservable<AlarmState> AlarmChanges => Observable.Never<AlarmState>();
        public IReadOnlyCollection<AlarmState> CurrentAlarms => Array.Empty<AlarmState>();
        public void Submit(SignalValue value) => Submitted.Add(value);
    }

    private static CanFrame Fr() =>
        new(0x100, false, new byte[] { 1 }, DateTimeOffset.UtcNow, CanDirection.Rx);

    [Fact]
    public void Decoded_signals_are_forwarded_in_order()
    {
        var source = new Subject<CanFrame>();
        var values = new[]
        {
            new SignalValue("M", "A", 1, 1, null, DateTimeOffset.UtcNow),
            new SignalValue("M", "B", 2, 2, null, DateTimeOffset.UtcNow),
        };
        var decoder = new FakeDecoder(values);
        var alarm = new RecordingAlarm();
        var pipeline = new CanReceivePipeline(source, decoder, alarm, ImmediateScheduler.Instance);

        var received = new List<string>();
        using var _ = pipeline.DecodedSignals.Subscribe(v => received.Add(v.SignalName));

        source.OnNext(Fr());

        received.Should().Equal(new[] { "A", "B" });
    }

    [Fact]
    public void Each_decoded_value_is_submitted_to_alarm_engine()
    {
        var source = new Subject<CanFrame>();
        var values = new[]
        {
            new SignalValue("M", "A", 1, 1, null, DateTimeOffset.UtcNow),
            new SignalValue("M", "B", 2, 2, null, DateTimeOffset.UtcNow),
        };
        var decoder = new FakeDecoder(values);
        var alarm = new RecordingAlarm();
        var pipeline = new CanReceivePipeline(source, decoder, alarm, ImmediateScheduler.Instance);

        using var _ = pipeline.DecodedSignals.Subscribe(_ => { });
        source.OnNext(Fr());
        source.OnNext(Fr());

        alarm.Submitted.Should().HaveCount(4);
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~CanReceivePipelineTests"
```

Expected: Build 실패.

- [ ] **Step 3: 구현**

Create `src/Application/Can/CanReceivePipeline.cs`:

```csharp
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class CanReceivePipeline
{
    private readonly IObservable<SignalValue> _decoded;

    public CanReceivePipeline(
        IObservable<CanFrame> source,
        ISignalDecoder decoder,
        IAlarmEngine alarmEngine,
        IScheduler? scheduler = null)
    {
        var runOn = scheduler ?? TaskPoolScheduler.Default;
        _decoded = source
            .ObserveOn(runOn)
            .SelectMany(frame => decoder.Decode(frame))
            .Do(alarmEngine.Submit)
            .Publish()
            .RefCount();
    }

    public IObservable<SignalValue> DecodedSignals => _decoded;
}
```

- [ ] **Step 4: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~CanReceivePipelineTests"
```

Expected: Passed: 2.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Can/CanReceivePipeline.cs tests/Application.Tests/Can/CanReceivePipelineTests.cs
git commit -m "feat(application): add CanReceivePipeline (decoder → alarm) on TaskPool"
```

---

## Task 7: ManualBusStatusPublisher — BusStatus stub

**Files:**
- Create: `src/Application/Can/ManualBusStatusPublisher.cs`
- Create: `tests/Application.Tests/Can/ManualBusStatusPublisherTests.cs`

### 계약

- 명시적 publish API만 제공. Plan B에서 `BusLifecycleService`가 이 타입을 DI로 받아 push하거나 교체.
- `IObservable<BusStatusChange> Changes` + `BusStatusChange Current` (가장 최근 상태, 기본값 = Disconnected).

- [ ] **Step 1: 실패 테스트 작성**

Create `tests/Application.Tests/Can/ManualBusStatusPublisherTests.cs`:

```csharp
using CanMonitor.Application.Can;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class ManualBusStatusPublisherTests
{
    [Fact]
    public void Default_status_is_Disconnected()
    {
        var pub = new ManualBusStatusPublisher();
        pub.Current.Status.Should().Be(BusStatus.Disconnected);
    }

    [Fact]
    public void Publish_updates_Current_and_emits_on_Changes()
    {
        var pub = new ManualBusStatusPublisher();
        var received = new List<BusStatusChange>();
        using var _ = pub.Changes.Skip(1).Subscribe(received.Add);

        var change = new BusStatusChange(BusStatus.Connected, "ok", null, 0, DateTimeOffset.UtcNow);
        pub.Publish(change);

        pub.Current.Should().Be(change);
        received.Should().ContainSingle().Which.Should().Be(change);
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~ManualBusStatusPublisherTests"
```

Expected: Build 실패.

- [ ] **Step 3: 구현**

Create `src/Application/Can/ManualBusStatusPublisher.cs`:

```csharp
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class ManualBusStatusPublisher : IDisposable
{
    private readonly BehaviorSubject<BusStatusChange> _subject;

    public ManualBusStatusPublisher()
    {
        _subject = new BehaviorSubject<BusStatusChange>(
            new BusStatusChange(BusStatus.Disconnected, null, null, 0, DateTimeOffset.UtcNow));
    }

    public BusStatusChange Current => _subject.Value;
    public IObservable<BusStatusChange> Changes => _subject.AsObservable();

    public void Publish(BusStatusChange change) => _subject.OnNext(change);

    public void Dispose() => _subject.Dispose();
}
```

- [ ] **Step 4: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~ManualBusStatusPublisherTests"
```

Expected: Passed: 2.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Can/ManualBusStatusPublisher.cs tests/Application.Tests/Can/ManualBusStatusPublisherTests.cs
git commit -m "feat(application): add ManualBusStatusPublisher stub (Phase 2b replaces with BusLifecycleService)"
```

---

## Task 8: TxScheduler — Channel + 단일 worker

**Files:**
- Create: `src/Application/Can/TxJob.cs`
- Create: `src/Application/Can/TxScheduler.cs`
- Create: `tests/Application.Tests/Can/TxSchedulerTests.cs`

### 계약

- Constructor: `(ICanBus bus, IScheduler? scheduler = null)` — scheduler 주입은 테스트용 (TestScheduler).
- 내부 `Channel.CreateBounded<TxJob>(new BoundedChannelOptions(1024){ FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true })`.
- 단일 worker: `channel.Reader.ReadAllAsync` → `bus.SendAsync`. Dispose 시 writer complete + worker join.
- `Schedule(name, factory, period)`: `Observable.Interval(period, scheduler).Subscribe(_ => writer.TryWrite(new TxJob(name, factory())))`. Dispose 시 해당 subscription 해제.
- `SendBurst(frames, interval)`: 전 frames를 차례대로 writer.TryWrite. `interval` 지정 시 scheduler로 지연.

- [ ] **Step 1: `TxJob` 작성**

Create `src/Application/Can/TxJob.cs`:

```csharp
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

internal sealed record TxJob(string Name, CanFrame Frame);
```

- [ ] **Step 2: 실패 테스트 작성**

Create `tests/Application.Tests/Can/TxSchedulerTests.cs`:

```csharp
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class TxSchedulerTests
{
    private static CanFrame Fr(uint id) =>
        new(id, false, new byte[] { 0 }, DateTimeOffset.UtcNow, CanDirection.Tx);

    [Fact]
    public async Task Scheduled_factory_fires_at_configured_period()
    {
        var scheduler = new TestScheduler();
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var sent = new List<uint>();
        using var _ = bus.Frames.Subscribe(f => { if (f.Direction == CanDirection.Tx) sent.Add(f.Id); });

        await using var tx = new TxScheduler(bus, scheduler);
        uint counter = 0;
        using var sub = tx.Schedule("T", () => Fr(++counter), TimeSpan.FromMilliseconds(100));

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(350).Ticks);
        await tx.DrainForTestsAsync();

        sent.Should().Equal(new uint[] { 1, 2, 3 });
    }

    [Fact]
    public async Task Dispose_of_Schedule_handle_stops_further_sends()
    {
        var scheduler = new TestScheduler();
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var sent = new List<uint>();
        using var _ = bus.Frames.Subscribe(f => { if (f.Direction == CanDirection.Tx) sent.Add(f.Id); });

        await using var tx = new TxScheduler(bus, scheduler);
        uint counter = 0;
        var sub = tx.Schedule("T", () => Fr(++counter), TimeSpan.FromMilliseconds(100));

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);
        sub.Dispose();
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500).Ticks);
        await tx.DrainForTestsAsync();

        sent.Should().Equal(new uint[] { 1 });
    }

    [Fact]
    public async Task SendBurst_flushes_frames_in_order()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var sent = new List<uint>();
        using var _ = bus.Frames.Subscribe(f => { if (f.Direction == CanDirection.Tx) sent.Add(f.Id); });

        await using var tx = new TxScheduler(bus);
        using var __ = tx.SendBurst(new[] { Fr(10), Fr(11), Fr(12) });

        await tx.DrainForTestsAsync();

        sent.Should().Equal(new uint[] { 10, 11, 12 });
    }
}
```

- [ ] **Step 3: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~TxSchedulerTests"
```

Expected: Build 실패 — `TxScheduler` 없음.

- [ ] **Step 4: `TxScheduler` 구현**

Create `src/Application/Can/TxScheduler.cs`:

```csharp
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Channels;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class TxScheduler : ITxScheduler, IAsyncDisposable
{
    private readonly ICanBus _bus;
    private readonly IScheduler _scheduler;
    private readonly Channel<TxJob> _channel;
    private readonly Task _workerTask;
    private readonly CancellationTokenSource _cts = new();
    private long _enqueuedCount;
    private long _processedCount;

    public TxScheduler(ICanBus bus, IScheduler? scheduler = null)
    {
        _bus = bus;
        _scheduler = scheduler ?? DefaultScheduler.Instance;
        _channel = Channel.CreateBounded<TxJob>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
    }

    public IDisposable Schedule(string name, Func<CanFrame> factory, TimeSpan period)
    {
        return Observable.Interval(period, _scheduler)
            .Subscribe(_ => Enqueue(new TxJob(name, factory())));
    }

    public IDisposable SendBurst(IReadOnlyList<CanFrame> frames, TimeSpan? interval = null)
    {
        if (!interval.HasValue || interval.Value == TimeSpan.Zero)
        {
            foreach (var frame in frames)
                Enqueue(new TxJob("Burst", frame));
            return System.Reactive.Disposables.Disposable.Empty;
        }

        return Observable.Generate(
                0, i => i < frames.Count, i => i + 1, i => frames[i], _ => interval.Value, _scheduler)
            .Subscribe(frame => Enqueue(new TxJob("Burst", frame)));
    }

    private void Enqueue(TxJob job)
    {
        if (_channel.Writer.TryWrite(job))
            Interlocked.Increment(ref _enqueuedCount);
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(ct))
            {
                try { await _bus.SendAsync(job.Frame, ct); }
                catch (OperationCanceledException) { break; }
                catch { /* Phase 2a: swallow; Plan B에서 ILogger 주입 */ }
                finally { Interlocked.Increment(ref _processedCount); }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// 테스트 전용 hook — 지금까지 enqueue된 모든 작업이 worker에서 처리되었음을 결정적으로 보장.
    /// 타이밍 sleep 없음. Phase 2a 테스트 시나리오는 채널 bound(1024) 이하로 enqueue하므로
    /// DropOldest로 인한 카운터 divergence는 발생하지 않는다.
    /// </summary>
    public async Task DrainForTestsAsync()
    {
        var target = Interlocked.Read(ref _enqueuedCount);
        while (Interlocked.Read(ref _processedCount) < target)
            await Task.Yield();
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        try { await _workerTask; } catch { }
        _cts.Dispose();
    }
}
```

- [ ] **Step 5: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~TxSchedulerTests"
```

Expected: Passed: 3.

- [ ] **Step 6: Commit**

```bash
git add src/Application/Can/TxJob.cs src/Application/Can/TxScheduler.cs tests/Application.Tests/Can/TxSchedulerTests.cs
git commit -m "feat(application): add TxScheduler (bounded channel + single worker)"
```

---

## Task 9: CanTransmitService — facade

**Files:**
- Create: `src/Application/Can/CanTransmitService.cs`
- Create: `tests/Application.Tests/Can/CanTransmitServiceTests.cs`

### 계약

- Constructor: `(ICanBus bus, ITxScheduler scheduler)`
- `Task SendAsync(CanFrame, CancellationToken)` → `bus.SendAsync` 즉시 (주기 없음, 단발).
- `IDisposable SchedulePeriodic(string name, Func<CanFrame>, TimeSpan period)` → scheduler 위임.
- `IDisposable Burst(IReadOnlyList<CanFrame>, TimeSpan? interval = null)` → scheduler 위임.

- [ ] **Step 1: 실패 테스트 작성**

Create `tests/Application.Tests/Can/CanTransmitServiceTests.cs`:

```csharp
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class CanTransmitServiceTests
{
    private static CanFrame Fr(uint id) =>
        new(id, false, new byte[] { 0 }, DateTimeOffset.UtcNow, CanDirection.Tx);

    [Fact]
    public async Task SendAsync_forwards_to_ICanBus_immediately()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        await using var sched = new TxScheduler(bus);
        var svc = new CanTransmitService(bus, sched);

        var sent = new List<uint>();
        using var _ = bus.Frames.Subscribe(f => { if (f.Direction == CanDirection.Tx) sent.Add(f.Id); });

        await svc.SendAsync(Fr(1));
        await svc.SendAsync(Fr(2));

        sent.Should().Equal(new uint[] { 1, 2 });
    }

    [Fact]
    public async Task Burst_delegates_to_scheduler()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        await using var sched = new TxScheduler(bus);
        var svc = new CanTransmitService(bus, sched);

        var sent = new List<uint>();
        using var _ = bus.Frames.Subscribe(f => { if (f.Direction == CanDirection.Tx) sent.Add(f.Id); });

        using var __ = svc.Burst(new[] { Fr(10), Fr(11), Fr(12) });
        await sched.DrainForTestsAsync();

        sent.Should().Equal(new uint[] { 10, 11, 12 });
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~CanTransmitServiceTests"
```

Expected: Build 실패.

- [ ] **Step 3: 구현**

Create `src/Application/Can/CanTransmitService.cs`:

```csharp
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class CanTransmitService
{
    private readonly ICanBus _bus;
    private readonly ITxScheduler _scheduler;

    public CanTransmitService(ICanBus bus, ITxScheduler scheduler)
    {
        _bus = bus;
        _scheduler = scheduler;
    }

    public Task SendAsync(CanFrame frame, CancellationToken ct = default)
        => _bus.SendAsync(frame, ct);

    public IDisposable SchedulePeriodic(string name, Func<CanFrame> factory, TimeSpan period)
        => _scheduler.Schedule(name, factory, period);

    public IDisposable Burst(IReadOnlyList<CanFrame> frames, TimeSpan? interval = null)
        => _scheduler.SendBurst(frames, interval);
}
```

- [ ] **Step 4: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~CanTransmitServiceTests"
```

Expected: Passed: 2.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Can/CanTransmitService.cs tests/Application.Tests/Can/CanTransmitServiceTests.cs
git commit -m "feat(application): add CanTransmitService facade"
```

---

## Task 10: Core Test DSL — TestStep + 파생 10종 + TestCase/TestResult/TestStepProgress

**Files:**
- Create: `src/Core/Testing/TestStep.cs`
- Create: `src/Core/Testing/TestCase.cs`
- Create: `src/Core/Testing/TestResult.cs`
- Create: `src/Core/Testing/TestStepProgress.cs`
- Create: `tests/Core.Tests/Testing/TestStepIdentityTests.cs`

### 계약

- `TestStep`은 abstract record, `Type` 문자열은 YAML discriminator와 1:1.
- 구체 step 10종은 spec §17과 동일 시그니처.
- `TestCase`/`TestResult`/`TestStepProgress`는 spec §16/§17 기반 DTO.

- [ ] **Step 1: 실패 테스트 작성**

Create `tests/Core.Tests/Testing/TestStepIdentityTests.cs`:

```csharp
using CanMonitor.Core.Testing;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Core.Tests.Testing;

public sealed class TestStepIdentityTests
{
    [Fact]
    public void Discriminators_match_spec()
    {
        new WaitStep(TimeSpan.FromMilliseconds(1)).Type.Should().Be("Wait");
        new ObserveSignalStep("M", "S", 1, TimeSpan.FromMilliseconds(1), 0.1).Type.Should().Be("ObserveSignal");
        new ObserveBitStep("M", "S", true, TimeSpan.FromMilliseconds(1)).Type.Should().Be("ObserveBit");
        new SendCanFrameStep(0x100, false, new byte[] { 1 }).Type.Should().Be("SendCanFrame");
        new AssertFrameRateStep(0x100, 100, 5).Type.Should().Be("AssertFrameRate");
        new ManualConfirmStep("instruct").Type.Should().Be("ManualConfirm");
        new SetVirtualInputStep().Type.Should().Be("SetVirtualInput");
        new SetHeartbeatStep("EEC1", true).Type.Should().Be("SetHeartbeat");
        new EnterSimulationModeStep().Type.Should().Be("EnterSimulationMode");
        new ExitSimulationModeStep().Type.Should().Be("ExitSimulationMode");
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
dotnet test tests/Core.Tests/CanMonitor.Core.Tests.csproj --filter "FullyQualifiedName~TestStepIdentityTests"
```

Expected: Build 실패.

- [ ] **Step 3: `TestStep.cs` 작성**

Create `src/Core/Testing/TestStep.cs`:

```csharp
namespace CanMonitor.Core.Testing;

public abstract record TestStep(string Type);

public sealed record WaitStep(TimeSpan Duration) : TestStep("Wait");

public sealed record ObserveSignalStep(
    string Message,
    string Signal,
    double Expected,
    TimeSpan Within,
    double Tolerance) : TestStep("ObserveSignal");

public sealed record ObserveBitStep(
    string Message,
    string Signal,
    bool Expected,
    TimeSpan Within) : TestStep("ObserveBit");

public sealed record SendCanFrameStep(
    uint Id,
    bool IsExtended,
    byte[] Data) : TestStep("SendCanFrame");

public sealed record AssertFrameRateStep(
    uint CanId,
    double HzExpected,
    double TolerancePct) : TestStep("AssertFrameRate");

public sealed record ManualConfirmStep(string Instruction) : TestStep("ManualConfirm");

public sealed record SetVirtualInputStep(
    string? GearLever = null,
    string? RangeShift = null,
    bool?   TemperatureSwitch = null,
    double? ClutchPedalPercent = null,
    double? WheelSpeedKph = null,
    double? SpeedSensor1Rpm = null,
    double? SpeedSensor2Rpm = null,
    bool?   PtoSwitch = null,
    bool?   FourWdSwitch = null,
    bool?   InchingSwitch = null,
    bool?   ParkingSwitch = null) : TestStep("SetVirtualInput");

public sealed record SetHeartbeatStep(string Name, bool Enabled) : TestStep("SetHeartbeat");

public sealed record EnterSimulationModeStep() : TestStep("EnterSimulationMode");

public sealed record ExitSimulationModeStep() : TestStep("ExitSimulationMode");
```

- [ ] **Step 4: `TestCase.cs` 작성**

Create `src/Core/Testing/TestCase.cs`:

```csharp
namespace CanMonitor.Core.Testing;

public sealed record TestCase(
    string Id,
    string Category,
    string Name,
    IReadOnlyList<TestStep> Prerequisites,
    IReadOnlyList<TestStep> Steps,
    string? FailCode = null);
```

- [ ] **Step 5: `TestResult.cs` 작성**

Create `src/Core/Testing/TestResult.cs`:

```csharp
namespace CanMonitor.Core.Testing;

public enum TestOutcome
{
    Passed = 0,
    Failed = 1,
    ManualRequired = 2,
    NotSupported = 3
}

public sealed record TestResult(
    string TestCaseId,
    TestOutcome Outcome,
    string? Reason,
    IReadOnlyList<TestStepProgress> StepLog,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
```

- [ ] **Step 6: `TestStepProgress.cs` 작성**

Create `src/Core/Testing/TestStepProgress.cs`:

```csharp
namespace CanMonitor.Core.Testing;

public enum StepOutcome
{
    Pending = 0,
    Running = 1,
    Passed = 2,
    Failed = 3,
    ManualRequired = 4
}

public sealed record TestStepProgress(
    int StepIndex,
    TestStep Step,
    StepOutcome Outcome,
    string? Message,
    DateTimeOffset At);
```

- [ ] **Step 7: 테스트 재실행**

```bash
dotnet test tests/Core.Tests/CanMonitor.Core.Tests.csproj --filter "FullyQualifiedName~TestStepIdentityTests"
```

Expected: Passed: 1.

- [ ] **Step 8: Commit**

```bash
git add src/Core/Testing/ tests/Core.Tests/Testing/
git commit -m "feat(core): add Test DSL records (TestStep + TestCase + TestResult + TestStepProgress)"
```

---

## Task 11: Core — IStepExecutor + ITestRunner

**Files:**
- Create: `src/Core/Abstractions/IStepExecutor.cs`
- Create: `src/Core/Abstractions/ITestRunner.cs`

- [ ] **Step 1: `IStepExecutor.cs` 작성**

Create `src/Core/Abstractions/IStepExecutor.cs`:

```csharp
using CanMonitor.Core.Testing;

namespace CanMonitor.Core.Abstractions;

public interface IStepExecutor
{
    Type StepType { get; }
    Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct);
}

public interface IStepExecutor<TStep> : IStepExecutor where TStep : TestStep
{
    Task<StepOutcome> ExecuteAsync(TStep step, ITestRunnerContext context, CancellationToken ct);
}

public interface ITestRunnerContext
{
    ICanBus Bus { get; }
    IObservable<Models.SignalValue> Signals { get; }
    IObservable<Models.AlarmState> Alarms { get; }
    ISignalDecoder Decoder { get; }
}
```

- [ ] **Step 2: `ITestRunner.cs` 작성**

Create `src/Core/Abstractions/ITestRunner.cs`:

```csharp
using CanMonitor.Core.Testing;

namespace CanMonitor.Core.Abstractions;

public interface ITestRunner
{
    Task<TestResult> RunAsync(
        TestCase testCase,
        IProgress<TestStepProgress>? progress = null,
        CancellationToken ct = default);
}
```

- [ ] **Step 3: 빌드 검증**

```bash
dotnet build src/Core/CanMonitor.Core.csproj
```

Expected: 0 Error.

- [ ] **Step 4: Commit**

```bash
git add src/Core/Abstractions/IStepExecutor.cs src/Core/Abstractions/ITestRunner.cs
git commit -m "feat(core): add ITestRunner + IStepExecutor<T> + ITestRunnerContext"
```

---

## Task 12: TestRunnerContext + TestRunner

**Files:**
- Create: `src/Application/Testing/TestRunnerContext.cs`
- Create: `src/Application/Testing/TestRunner.cs`
- Create: `tests/Application.Tests/Testing/TestRunnerTests.cs`

### 계약

- `TestRunnerContext`는 record로 `ICanBus`/`IObservable<SignalValue>`/`IObservable<AlarmState>`/`ISignalDecoder`를 모음 (`ITestRunnerContext` 구현).
- `TestRunner` constructor: `(IEnumerable<IStepExecutor> executors, ITestRunnerContext context)` — DI가 모든 executor 수집해 주입.
- `RunAsync`:
  1. `prerequisites` → `steps` 순서대로 실행.
  2. step 실행 시 `progress.Report(Running)` → executor 호출 → outcome 기록.
  3. `StepOutcome.Failed` → 루프 중단 + `TestResult.Failed`.
  4. `StepOutcome.ManualRequired` → 루프 중단 + `TestResult.ManualRequired` (반자동 TC 지원).
  5. 디스패치 맵에 없는 step type → `NotSupportedException("Step type {Name} is not registered in this runner build.")`.

- [ ] **Step 1: 실패 테스트 작성**

Create `tests/Application.Tests/Testing/TestRunnerTests.cs`:

```csharp
using CanMonitor.Application.Testing;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Xunit;

namespace CanMonitor.Application.Tests.Testing;

public sealed class TestRunnerTests
{
    private sealed class AlwaysPass<TStep> : IStepExecutor<TStep> where TStep : TestStep
    {
        public Type StepType => typeof(TStep);
        public int CallCount { get; private set; }
        public Task<StepOutcome> ExecuteAsync(TStep step, ITestRunnerContext ctx, CancellationToken ct)
        { CallCount++; return Task.FromResult(StepOutcome.Passed); }
        public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext ctx, CancellationToken ct)
            => ExecuteAsync((TStep)step, ctx, ct);
    }

    private sealed class AlwaysFail<TStep> : IStepExecutor<TStep> where TStep : TestStep
    {
        public Type StepType => typeof(TStep);
        public Task<StepOutcome> ExecuteAsync(TStep step, ITestRunnerContext ctx, CancellationToken ct)
            => Task.FromResult(StepOutcome.Failed);
        public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext ctx, CancellationToken ct)
            => ExecuteAsync((TStep)step, ctx, ct);
    }

    private static async Task<ITestRunnerContext> MakeContextAsync()
    {
        var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        return new TestRunnerContext(
            bus,
            new Subject<SignalValue>(),
            new Subject<AlarmState>(),
            new NoopDecoder());
    }

    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
    }

    private static TestCase Case(params TestStep[] steps) =>
        new("TC-X", "Unit", "x", Array.Empty<TestStep>(), steps);

    [Fact]
    public async Task All_steps_pass_returns_Passed()
    {
        var ctx = await MakeContextAsync();
        var runner = new TestRunner(new IStepExecutor[] { new AlwaysPass<WaitStep>() }, ctx);

        var result = await runner.RunAsync(Case(new WaitStep(TimeSpan.Zero), new WaitStep(TimeSpan.Zero)));

        result.Outcome.Should().Be(TestOutcome.Passed);
        result.StepLog.Should().HaveCount(2).And.OnlyContain(p => p.Outcome == StepOutcome.Passed);
    }

    [Fact]
    public async Task First_failing_step_stops_run_and_returns_Failed()
    {
        var ctx = await MakeContextAsync();
        var runner = new TestRunner(new IStepExecutor[] { new AlwaysFail<WaitStep>() }, ctx);

        var result = await runner.RunAsync(Case(new WaitStep(TimeSpan.Zero), new WaitStep(TimeSpan.Zero)));

        result.Outcome.Should().Be(TestOutcome.Failed);
        result.StepLog.Should().HaveCount(1).And.OnlyContain(p => p.Outcome == StepOutcome.Failed);
    }

    [Fact]
    public async Task Unknown_step_type_throws_NotSupportedException()
    {
        var ctx = await MakeContextAsync();
        var runner = new TestRunner(Array.Empty<IStepExecutor>(), ctx);

        var act = () => runner.RunAsync(Case(new WaitStep(TimeSpan.Zero)));

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*WaitStep*");
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~TestRunnerTests"
```

Expected: Build 실패.

- [ ] **Step 3: `TestRunnerContext` 작성**

Create `src/Application/Testing/TestRunnerContext.cs`:

```csharp
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Testing;

public sealed record TestRunnerContext(
    ICanBus Bus,
    IObservable<SignalValue> Signals,
    IObservable<AlarmState> Alarms,
    ISignalDecoder Decoder) : ITestRunnerContext;
```

- [ ] **Step 4: `TestRunner` 작성**

Create `src/Application/Testing/TestRunner.cs`:

```csharp
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing;

public sealed class TestRunner : ITestRunner
{
    private readonly IReadOnlyDictionary<Type, IStepExecutor> _executors;
    private readonly ITestRunnerContext _context;

    public TestRunner(IEnumerable<IStepExecutor> executors, ITestRunnerContext context)
    {
        _executors = executors.ToDictionary(e => e.StepType);
        _context = context;
    }

    public async Task<TestResult> RunAsync(
        TestCase testCase,
        IProgress<TestStepProgress>? progress = null,
        CancellationToken ct = default)
    {
        var log = new List<TestStepProgress>();
        var startedAt = DateTimeOffset.UtcNow;
        var overall = TestOutcome.Passed;
        string? reason = null;

        var steps = testCase.Prerequisites.Concat(testCase.Steps).ToArray();
        for (int i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            progress?.Report(new TestStepProgress(i, step, StepOutcome.Running, null, DateTimeOffset.UtcNow));

            if (!_executors.TryGetValue(step.GetType(), out var executor))
                throw new NotSupportedException(
                    $"Step type {step.GetType().Name} is not registered in this runner build.");

            StepOutcome outcome;
            string? message = null;
            try
            {
                outcome = await executor.ExecuteAsync(step, _context, ct);
            }
            catch (Exception ex)
            {
                outcome = StepOutcome.Failed;
                message = ex.Message;
            }

            var entry = new TestStepProgress(i, step, outcome, message, DateTimeOffset.UtcNow);
            log.Add(entry);
            progress?.Report(entry);

            if (outcome == StepOutcome.Failed) { overall = TestOutcome.Failed; reason = message ?? "step failed"; break; }
            if (outcome == StepOutcome.ManualRequired) { overall = TestOutcome.ManualRequired; reason = "manual confirmation required"; break; }
        }

        return new TestResult(testCase.Id, overall, reason, log, startedAt, DateTimeOffset.UtcNow);
    }
}
```

- [ ] **Step 5: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~TestRunnerTests"
```

Expected: Passed: 3.

- [ ] **Step 6: Commit**

```bash
git add src/Application/Testing/TestRunner.cs src/Application/Testing/TestRunnerContext.cs tests/Application.Tests/Testing/TestRunnerTests.cs
git commit -m "feat(application): add TestRunner dispatch with NotSupportedException fallback"
```

---

## Task 13: WaitStepExecutor

**Files:**
- Create: `src/Application/Testing/Executors/WaitStepExecutor.cs`
- Create: `tests/Application.Tests/Testing/Executors/WaitStepExecutorTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

Create `tests/Application.Tests/Testing/Executors/WaitStepExecutorTests.cs`:

```csharp
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Testing;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Testing.Executors;

public sealed class WaitStepExecutorTests
{
    [Fact]
    public async Task Waits_for_requested_duration()
    {
        var exec = new WaitStepExecutor();
        var start = DateTimeOffset.UtcNow;
        var outcome = await exec.ExecuteAsync(
            new WaitStep(TimeSpan.FromMilliseconds(100)), context: null!, CancellationToken.None);

        (DateTimeOffset.UtcNow - start).Should().BeGreaterThan(TimeSpan.FromMilliseconds(80));
        outcome.Should().Be(StepOutcome.Passed);
    }

    [Fact]
    public async Task Respects_cancellation()
    {
        var exec = new WaitStepExecutor();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(20);

        var act = () => exec.ExecuteAsync(new WaitStep(TimeSpan.FromSeconds(5)), null!, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~WaitStepExecutorTests"
```

Expected: Build 실패.

- [ ] **Step 3: 구현**

Create `src/Application/Testing/Executors/WaitStepExecutor.cs`:

```csharp
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class WaitStepExecutor : IStepExecutor<WaitStep>
{
    public Type StepType => typeof(WaitStep);

    public async Task<StepOutcome> ExecuteAsync(WaitStep step, ITestRunnerContext context, CancellationToken ct)
    {
        await Task.Delay(step.Duration, ct);
        return StepOutcome.Passed;
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((WaitStep)step, context, ct);
}
```

- [ ] **Step 4: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~WaitStepExecutorTests"
```

Expected: Passed: 2.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Testing/Executors/WaitStepExecutor.cs tests/Application.Tests/Testing/Executors/WaitStepExecutorTests.cs
git commit -m "feat(application): add WaitStepExecutor"
```

---

## Task 14: SendCanFrameStepExecutor

**Files:**
- Create: `src/Application/Testing/Executors/SendCanFrameStepExecutor.cs`
- Create: `tests/Application.Tests/Testing/Executors/SendCanFrameStepExecutorTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

Create `tests/Application.Tests/Testing/Executors/SendCanFrameStepExecutorTests.cs`:

```csharp
using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using System.Reactive.Subjects;
using Xunit;

namespace CanMonitor.Application.Tests.Testing.Executors;

public sealed class SendCanFrameStepExecutorTests
{
    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
    }

    [Fact]
    public async Task Sends_frame_through_context_bus()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var ctx = new TestRunnerContext(bus, new Subject<SignalValue>(), new Subject<AlarmState>(), new NoopDecoder());
        var exec = new SendCanFrameStepExecutor();

        var sent = new List<uint>();
        using var _ = bus.Frames.Subscribe(f => { if (f.Direction == CanDirection.Tx) sent.Add(f.Id); });

        var outcome = await exec.ExecuteAsync(
            new SendCanFrameStep(0x123, false, new byte[] { 1, 2, 3 }),
            ctx, CancellationToken.None);

        outcome.Should().Be(StepOutcome.Passed);
        sent.Should().ContainSingle().Which.Should().Be(0x123u);
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~SendCanFrameStepExecutorTests"
```

Expected: Build 실패.

- [ ] **Step 3: 구현**

Create `src/Application/Testing/Executors/SendCanFrameStepExecutor.cs`:

```csharp
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class SendCanFrameStepExecutor : IStepExecutor<SendCanFrameStep>
{
    public Type StepType => typeof(SendCanFrameStep);

    public async Task<StepOutcome> ExecuteAsync(SendCanFrameStep step, ITestRunnerContext context, CancellationToken ct)
    {
        var frame = new CanFrame(step.Id, step.IsExtended, step.Data, DateTimeOffset.UtcNow, CanDirection.Tx);
        await context.Bus.SendAsync(frame, ct);
        return StepOutcome.Passed;
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((SendCanFrameStep)step, context, ct);
}
```

- [ ] **Step 4: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~SendCanFrameStepExecutorTests"
```

Expected: Passed: 1.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Testing/Executors/SendCanFrameStepExecutor.cs tests/Application.Tests/Testing/Executors/SendCanFrameStepExecutorTests.cs
git commit -m "feat(application): add SendCanFrameStepExecutor"
```

---

## Task 15: ObserveSignalStepExecutor

**Files:**
- Create: `src/Application/Testing/Executors/ObserveSignalStepExecutor.cs`
- Create: `tests/Application.Tests/Testing/Executors/ObserveSignalStepExecutorTests.cs`

### 계약

- `SignalValue` 스트림에서 `MessageName == step.Message && SignalName == step.Signal` 조건의 값을 `step.Within` 내에 최대 1개 수신.
- 수신한 값 `PhysicalValue`와 `step.Expected` 절대차 ≤ `step.Tolerance` → `Passed`. 초과 → `Failed`.
- `step.Within` 경과 시 수신 없음 → `Failed`.

- [ ] **Step 1: 실패 테스트 작성**

Create `tests/Application.Tests/Testing/Executors/ObserveSignalStepExecutorTests.cs`:

```csharp
using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using System.Reactive.Subjects;
using Xunit;

namespace CanMonitor.Application.Tests.Testing.Executors;

public sealed class ObserveSignalStepExecutorTests
{
    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
    }

    private static async Task<(ITestRunnerContext ctx, Subject<SignalValue> signals)> MkAsync()
    {
        var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var signals = new Subject<SignalValue>();
        return (new TestRunnerContext(bus, signals, new Subject<AlarmState>(), new NoopDecoder()), signals);
    }

    [Fact]
    public async Task Passed_when_matching_value_within_tolerance_before_timeout()
    {
        var (ctx, signals) = await MkAsync();
        var exec = new ObserveSignalStepExecutor();
        var step = new ObserveSignalStep("M", "S", 10, TimeSpan.FromMilliseconds(300), 0.5);

        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            signals.OnNext(new SignalValue("M", "S", 10.2, 10.2, null, DateTimeOffset.UtcNow));
        });

        var outcome = await exec.ExecuteAsync(step, ctx, CancellationToken.None);
        outcome.Should().Be(StepOutcome.Passed);
    }

    [Fact]
    public async Task Failed_when_value_is_outside_tolerance()
    {
        var (ctx, signals) = await MkAsync();
        var exec = new ObserveSignalStepExecutor();
        var step = new ObserveSignalStep("M", "S", 10, TimeSpan.FromMilliseconds(200), 0.1);

        _ = Task.Run(async () =>
        {
            await Task.Delay(30);
            signals.OnNext(new SignalValue("M", "S", 11.0, 11.0, null, DateTimeOffset.UtcNow));
        });

        var outcome = await exec.ExecuteAsync(step, ctx, CancellationToken.None);
        outcome.Should().Be(StepOutcome.Failed);
    }

    [Fact]
    public async Task Failed_when_nothing_arrives_within_window()
    {
        var (ctx, _) = await MkAsync();
        var exec = new ObserveSignalStepExecutor();
        var step = new ObserveSignalStep("M", "S", 10, TimeSpan.FromMilliseconds(80), 0.1);

        var outcome = await exec.ExecuteAsync(step, ctx, CancellationToken.None);
        outcome.Should().Be(StepOutcome.Failed);
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~ObserveSignalStepExecutorTests"
```

Expected: Build 실패.

- [ ] **Step 3: 구현**

Create `src/Application/Testing/Executors/ObserveSignalStepExecutor.cs`:

```csharp
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class ObserveSignalStepExecutor : IStepExecutor<ObserveSignalStep>
{
    public Type StepType => typeof(ObserveSignalStep);

    public async Task<StepOutcome> ExecuteAsync(ObserveSignalStep step, ITestRunnerContext context, CancellationToken ct)
    {
        try
        {
            var match = await context.Signals
                .Where(v => v.MessageName == step.Message && v.SignalName == step.Signal)
                .Timeout(step.Within)
                .FirstAsync()
                .ToTask(ct);

            return Math.Abs(match.PhysicalValue - step.Expected) <= step.Tolerance
                ? StepOutcome.Passed
                : StepOutcome.Failed;
        }
        catch (TimeoutException) { return StepOutcome.Failed; }
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((ObserveSignalStep)step, context, ct);
}
```

- [ ] **Step 4: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~ObserveSignalStepExecutorTests"
```

Expected: Passed: 3.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Testing/Executors/ObserveSignalStepExecutor.cs tests/Application.Tests/Testing/Executors/ObserveSignalStepExecutorTests.cs
git commit -m "feat(application): add ObserveSignalStepExecutor"
```

---

## Task 16: ObserveBitStepExecutor

**Files:**
- Create: `src/Application/Testing/Executors/ObserveBitStepExecutor.cs`
- Create: `tests/Application.Tests/Testing/Executors/ObserveBitStepExecutorTests.cs`

### 계약

- 신호 스트림에서 `MessageName/SignalName` 매칭값 수신. `RawValue != 0` → `true`, `0` → `false`.
- `step.Expected`와 일치 → `Passed`, 불일치 → `Failed`, `step.Within` 경과 시 미수신 → `Failed`.

- [ ] **Step 1: 실패 테스트 작성**

Create `tests/Application.Tests/Testing/Executors/ObserveBitStepExecutorTests.cs`:

```csharp
using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using System.Reactive.Subjects;
using Xunit;

namespace CanMonitor.Application.Tests.Testing.Executors;

public sealed class ObserveBitStepExecutorTests
{
    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
    }

    private static async Task<(ITestRunnerContext ctx, Subject<SignalValue> signals)> MkAsync()
    {
        var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var signals = new Subject<SignalValue>();
        return (new TestRunnerContext(bus, signals, new Subject<AlarmState>(), new NoopDecoder()), signals);
    }

    [Fact]
    public async Task Passes_when_bit_becomes_expected()
    {
        var (ctx, signals) = await MkAsync();
        var exec = new ObserveBitStepExecutor();
        var step = new ObserveBitStep("M", "S", true, TimeSpan.FromMilliseconds(200));

        _ = Task.Run(async () =>
        {
            await Task.Delay(20);
            signals.OnNext(new SignalValue("M", "S", 1, 1, null, DateTimeOffset.UtcNow));
        });

        (await exec.ExecuteAsync(step, ctx, CancellationToken.None)).Should().Be(StepOutcome.Passed);
    }

    [Fact]
    public async Task Fails_when_bit_never_becomes_expected()
    {
        var (ctx, signals) = await MkAsync();
        var exec = new ObserveBitStepExecutor();
        var step = new ObserveBitStep("M", "S", true, TimeSpan.FromMilliseconds(80));

        _ = Task.Run(async () =>
        {
            await Task.Delay(10);
            signals.OnNext(new SignalValue("M", "S", 0, 0, null, DateTimeOffset.UtcNow));
        });

        (await exec.ExecuteAsync(step, ctx, CancellationToken.None)).Should().Be(StepOutcome.Failed);
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~ObserveBitStepExecutorTests"
```

Expected: Build 실패.

- [ ] **Step 3: 구현**

Create `src/Application/Testing/Executors/ObserveBitStepExecutor.cs`:

```csharp
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class ObserveBitStepExecutor : IStepExecutor<ObserveBitStep>
{
    public Type StepType => typeof(ObserveBitStep);

    public async Task<StepOutcome> ExecuteAsync(ObserveBitStep step, ITestRunnerContext context, CancellationToken ct)
    {
        try
        {
            var match = await context.Signals
                .Where(v => v.MessageName == step.Message && v.SignalName == step.Signal
                         && ((v.RawValue != 0) == step.Expected))
                .Timeout(step.Within)
                .FirstAsync()
                .ToTask(ct);

            return StepOutcome.Passed;
        }
        catch (TimeoutException) { return StepOutcome.Failed; }
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((ObserveBitStep)step, context, ct);
}
```

- [ ] **Step 4: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~ObserveBitStepExecutorTests"
```

Expected: Passed: 2.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Testing/Executors/ObserveBitStepExecutor.cs tests/Application.Tests/Testing/Executors/ObserveBitStepExecutorTests.cs
git commit -m "feat(application): add ObserveBitStepExecutor"
```

---

## Task 17: AssertFrameRateStepExecutor

**Files:**
- Create: `src/Application/Testing/Executors/AssertFrameRateStepExecutor.cs`
- Create: `tests/Application.Tests/Testing/Executors/AssertFrameRateStepExecutorTests.cs`

### 계약

- 1초 윈도우 동안 `context.Bus.Frames`에서 `Id == step.CanId`인 프레임 수집.
- 수집된 Hz와 `step.HzExpected`의 차이가 `HzExpected * (step.TolerancePct / 100)` 이내 → `Passed`.

- [ ] **Step 1: 실패 테스트 작성**

Create `tests/Application.Tests/Testing/Executors/AssertFrameRateStepExecutorTests.cs`:

```csharp
using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using System.Reactive.Subjects;
using Xunit;

namespace CanMonitor.Application.Tests.Testing.Executors;

public sealed class AssertFrameRateStepExecutorTests
{
    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
    }

    [Fact]
    public async Task Passed_when_rate_within_tolerance()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var ctx = new TestRunnerContext(bus, new Subject<SignalValue>(), new Subject<AlarmState>(), new NoopDecoder());
        var exec = new AssertFrameRateStepExecutor();

        // Deterministic: start the executor (subscribe completes synchronously before first await),
        // then inject 100 frames synchronously so all land in the first Buffer(1s) window.
        var execTask = exec.ExecuteAsync(
            new AssertFrameRateStep(0x200, 100, TolerancePct: 25),
            ctx, CancellationToken.None);

        for (int i = 0; i < 100; i++)
            bus.Inject(new CanFrame(0x200, false, new byte[] { 1 }, DateTimeOffset.UtcNow, CanDirection.Rx));

        var outcome = await execTask;

        outcome.Should().Be(StepOutcome.Passed);
    }

    [Fact]
    public async Task Failed_when_rate_below_tolerance()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var ctx = new TestRunnerContext(bus, new Subject<SignalValue>(), new Subject<AlarmState>(), new NoopDecoder());
        var exec = new AssertFrameRateStepExecutor();

        var outcome = await exec.ExecuteAsync(
            new AssertFrameRateStep(0x999, 100, TolerancePct: 10),
            ctx, CancellationToken.None);

        outcome.Should().Be(StepOutcome.Failed);
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~AssertFrameRateStepExecutorTests"
```

Expected: Build 실패.

- [ ] **Step 3: 구현**

Create `src/Application/Testing/Executors/AssertFrameRateStepExecutor.cs`:

```csharp
using System.Reactive.Linq;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class AssertFrameRateStepExecutor : IStepExecutor<AssertFrameRateStep>
{
    public Type StepType => typeof(AssertFrameRateStep);

    public async Task<StepOutcome> ExecuteAsync(AssertFrameRateStep step, ITestRunnerContext context, CancellationToken ct)
    {
        var window = TimeSpan.FromSeconds(1);
        var count = await context.Bus.Frames
            .Where(f => f.Id == step.CanId)
            .Buffer(window)
            .Take(1)
            .Select(b => b.Count)
            .FirstAsync()
            .ToTask(ct);

        var measured = count / window.TotalSeconds;
        var tolerance = step.HzExpected * (step.TolerancePct / 100.0);
        return Math.Abs(measured - step.HzExpected) <= tolerance
            ? StepOutcome.Passed
            : StepOutcome.Failed;
    }

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((AssertFrameRateStep)step, context, ct);
}
```

- [ ] **Step 4: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~AssertFrameRateStepExecutorTests"
```

Expected: Passed: 2.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Testing/Executors/AssertFrameRateStepExecutor.cs tests/Application.Tests/Testing/Executors/AssertFrameRateStepExecutorTests.cs
git commit -m "feat(application): add AssertFrameRateStepExecutor (1s window)"
```

---

## Task 18: ManualConfirmStepExecutor

**Files:**
- Create: `src/Application/Testing/Executors/ManualConfirmStepExecutor.cs`
- Create: `tests/Application.Tests/Testing/Executors/ManualConfirmStepExecutorTests.cs`

### 계약

- 반자동/수동 TC에서 사람이 확인해야 할 단계. Plan A는 GUI가 없으므로 즉시 `StepOutcome.ManualRequired` 반환 → TestRunner가 TestResult를 `ManualRequired`로 종료. Phase 3 GUI가 이 outcome에 prompt를 띄운다.

- [ ] **Step 1: 실패 테스트 작성**

Create `tests/Application.Tests/Testing/Executors/ManualConfirmStepExecutorTests.cs`:

```csharp
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Testing;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Testing.Executors;

public sealed class ManualConfirmStepExecutorTests
{
    [Fact]
    public async Task Returns_ManualRequired()
    {
        var exec = new ManualConfirmStepExecutor();
        var outcome = await exec.ExecuteAsync(
            new ManualConfirmStep("전원 공급기 전압을 12V로 맞추세요"),
            context: null!, CancellationToken.None);

        outcome.Should().Be(StepOutcome.ManualRequired);
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~ManualConfirmStepExecutorTests"
```

Expected: Build 실패.

- [ ] **Step 3: 구현**

Create `src/Application/Testing/Executors/ManualConfirmStepExecutor.cs`:

```csharp
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Testing;

namespace CanMonitor.Application.Testing.Executors;

public sealed class ManualConfirmStepExecutor : IStepExecutor<ManualConfirmStep>
{
    public Type StepType => typeof(ManualConfirmStep);

    public Task<StepOutcome> ExecuteAsync(ManualConfirmStep step, ITestRunnerContext context, CancellationToken ct)
        => Task.FromResult(StepOutcome.ManualRequired);

    public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext context, CancellationToken ct)
        => ExecuteAsync((ManualConfirmStep)step, context, ct);
}
```

- [ ] **Step 4: 테스트 재실행**

```bash
dotnet test tests/Application.Tests/CanMonitor.Application.Tests.csproj --filter "FullyQualifiedName~ManualConfirmStepExecutorTests"
```

Expected: Passed: 1.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Testing/Executors/ManualConfirmStepExecutor.cs tests/Application.Tests/Testing/Executors/ManualConfirmStepExecutorTests.cs
git commit -m "feat(application): add ManualConfirmStepExecutor (returns ManualRequired)"
```

---

## Task 19: ServiceCollectionExtensions — DI 부트스트랩

**Files:**
- Create: `src/Application/ServiceCollectionExtensions.cs`

### 계약

- 확장메서드 `IServiceCollection AddCanMonitorApplication(this IServiceCollection services)`:
  - `CanEventHub`, `RawFrameStore`, `ManualBusStatusPublisher` — singleton
  - `IAlarmEngine` → `AlarmEngine` singleton (`AlarmRuleFactory.CreatePhase2aRules()`로 rule 주입)
  - `ITxScheduler` → `TxScheduler` singleton (ICanBus는 호출자가 별도 등록)
  - `CanTransmitService` singleton
  - `ITestRunner` → `TestRunner` transient
  - `IStepExecutor` 6종: `AddSingleton<IStepExecutor, ...>` 패턴

- [ ] **Step 1: 구현**

Create `src/Application/ServiceCollectionExtensions.cs`:

```csharp
using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
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

        services.AddSingleton<IAlarmEngine>(_ => new AlarmEngine(AlarmRuleFactory.CreatePhase2aRules()));
        services.AddSingleton<ITxScheduler, TxScheduler>();
        services.AddSingleton<CanTransmitService>();

        services.AddSingleton<IStepExecutor, WaitStepExecutor>();
        services.AddSingleton<IStepExecutor, SendCanFrameStepExecutor>();
        services.AddSingleton<IStepExecutor, ObserveSignalStepExecutor>();
        services.AddSingleton<IStepExecutor, ObserveBitStepExecutor>();
        services.AddSingleton<IStepExecutor, AssertFrameRateStepExecutor>();
        services.AddSingleton<IStepExecutor, ManualConfirmStepExecutor>();

        services.AddTransient<ITestRunner, TestRunner>();
        return services;
    }
}
```

- [ ] **Step 2: 빌드 검증**

```bash
dotnet build src/Application/CanMonitor.Application.csproj
```

Expected: 0 Error.

- [ ] **Step 3: Commit**

```bash
git add src/Application/ServiceCollectionExtensions.cs
git commit -m "feat(application): add AddCanMonitorApplication DI extension"
```

---

## Task 20: Integration Test — TC-001 Frame Rate

**Files:**
- Create: `tests/Integration.Tests/TestCases/Tc001FrameRateTests.cs`
- Modify: `tests/Integration.Tests/CanMonitor.Integration.Tests.csproj` — Application 프로젝트 참조 추가

### 계약

- `VirtualCanBus` 위에서 100Hz 주기로 특정 ID의 frame을 inject한 뒤 `AssertFrameRateStep`로 `TestRunner` 실행 → `TestOutcome.Passed`.

- [ ] **Step 1: Integration.Tests에 Application 참조 추가**

Modify `tests/Integration.Tests/CanMonitor.Integration.Tests.csproj`:

```xml
<ItemGroup>
  <!-- 기존 참조 유지 -->
  <ProjectReference Include="..\..\src\Application\CanMonitor.Application.csproj" />
</ItemGroup>
```

- [ ] **Step 2: 테스트 작성**

Create `tests/Integration.Tests/TestCases/Tc001FrameRateTests.cs`:

```csharp
using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using System.Reactive.Subjects;
using Xunit;

namespace CanMonitor.Integration.Tests.TestCases;

public sealed class Tc001FrameRateTests
{
    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
    }

    [Fact]
    public async Task TC_001_100Hz_frame_passes_rate_assertion()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var ctx = new TestRunnerContext(bus, new Subject<SignalValue>(), new Subject<AlarmState>(), new NoopDecoder());
        var runner = new TestRunner(new IStepExecutor[] { new AssertFrameRateStepExecutor() }, ctx);

        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                bus.Inject(new CanFrame(0x0CF00400, true, new byte[] { 1 }, DateTimeOffset.UtcNow, CanDirection.Rx));
                await Task.Delay(10);
            }
        });

        var testCase = new TestCase(
            "TC-001", "Frame", "주기/누락",
            Prerequisites: Array.Empty<TestStep>(),
            Steps: new TestStep[] { new AssertFrameRateStep(0x0CF00400, 100, TolerancePct: 20) });

        var result = await runner.RunAsync(testCase);
        cts.Cancel();

        result.Outcome.Should().Be(TestOutcome.Passed);
        result.StepLog.Should().ContainSingle().Which.Outcome.Should().Be(StepOutcome.Passed);
    }
}
```

- [ ] **Step 3: 테스트 실행**

```bash
dotnet test tests/Integration.Tests/CanMonitor.Integration.Tests.csproj --filter "FullyQualifiedName~Tc001FrameRateTests"
```

Expected: Passed: 1.

- [ ] **Step 4: Commit**

```bash
git add tests/Integration.Tests/TestCases/Tc001FrameRateTests.cs tests/Integration.Tests/CanMonitor.Integration.Tests.csproj
git commit -m "test(integration): add TC-001 frame rate assertion (100Hz via VirtualCanBus)"
```

---

## Task 21: Integration Test — TC-002 Operating Mode (Driving=true)

**Files:**
- Create: `tests/Integration.Tests/TestCases/Tc002OperatingModeTests.cs`

### 계약

- VirtualCanBus에 raw frame을 inject → `CanReceivePipeline`이 decoder를 통해 SignalValue 생성 → `TestRunner`가 `ObserveBitStep`으로 bit true 관찰 → `Passed`.
- Phase 2a에서는 실제 DBC 파싱 대신, 테스트용 `StubDecoder`가 특정 frame ID를 보면 `Operating_Mode.Driving_Status = 1`인 SignalValue를 반환하도록 한다. TC-002의 목적은 Runner→Pipeline→Signal observation 통합 경로 검증이지 디코더 검증이 아님 (디코더 검증은 `Dbc.Tests`에서 이미 수행).

- [ ] **Step 1: 테스트 작성**

Create `tests/Integration.Tests/TestCases/Tc002OperatingModeTests.cs`:

```csharp
using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Integration.Tests.TestCases;

public sealed class Tc002OperatingModeTests
{
    private sealed class StubDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame)
        {
            if (frame.Id == 0x0C000E00)
            {
                return new[]
                {
                    new SignalValue("Operating_Mode", "Driving_Status", 1, 1, null, frame.Timestamp),
                    new SignalValue("Operating_Mode", "Working_Status", 0, 0, null, frame.Timestamp),
                };
            }
            return Array.Empty<SignalValue>();
        }
    }

    [Fact]
    public async Task TC_002_driving_bit_true_observed_via_pipeline()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var alarm = new AlarmEngine(Array.Empty<IAlarmRule>());
        var pipeline = new CanReceivePipeline(bus.Frames, new StubDecoder(), alarm);

        using var pipelineSub = pipeline.DecodedSignals.Subscribe(_ => { });
        var ctx = new TestRunnerContext(bus, pipeline.DecodedSignals, alarm.AlarmChanges, new StubDecoder());
        var runner = new TestRunner(new IStepExecutor[] { new ObserveBitStepExecutor() }, ctx);

        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                bus.Inject(new CanFrame(0x0C000E00, true, new byte[] { 1 }, DateTimeOffset.UtcNow, CanDirection.Rx));
                await Task.Delay(50);
            }
        });

        var testCase = new TestCase(
            "TC-002", "OperatingMode", "정상모드 Driving",
            Prerequisites: Array.Empty<TestStep>(),
            Steps: new TestStep[]
            {
                new ObserveBitStep("Operating_Mode", "Driving_Status", true, TimeSpan.FromMilliseconds(500)),
                new ObserveBitStep("Operating_Mode", "Working_Status", false, TimeSpan.FromMilliseconds(500)),
            });

        var result = await runner.RunAsync(testCase);
        cts.Cancel();

        result.Outcome.Should().Be(TestOutcome.Passed);
        result.StepLog.Should().HaveCount(2).And.OnlyContain(p => p.Outcome == StepOutcome.Passed);
    }
}
```

- [ ] **Step 2: 테스트 실행**

```bash
dotnet test tests/Integration.Tests/CanMonitor.Integration.Tests.csproj --filter "FullyQualifiedName~Tc002OperatingModeTests"
```

Expected: Passed: 1.

- [ ] **Step 3: Commit**

```bash
git add tests/Integration.Tests/TestCases/Tc002OperatingModeTests.cs
git commit -m "test(integration): add TC-002 operating mode (Driving) via pipeline"
```

---

## Task 22: Integration Test — TC-010 Driving/Working 전환

**Files:**
- Create: `tests/Integration.Tests/TestCases/Tc010DrivingWorkingTests.cs`

### 계약

- TC-010: Driving → Working 전환 시 bit 상태가 뒤바뀌는 것을 순차적으로 관찰.
- stub decoder가 시간에 따라 다른 신호를 돌려주도록 mutable source 사용.

- [ ] **Step 1: 테스트 작성**

Create `tests/Integration.Tests/TestCases/Tc010DrivingWorkingTests.cs`:

```csharp
using CanMonitor.Application.Alarms;
using CanMonitor.Application.Can;
using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Integration.Tests.TestCases;

public sealed class Tc010DrivingWorkingTests
{
    private sealed class ModeStubDecoder : ISignalDecoder
    {
        private volatile int _drivingFlag = 1;
        public void SetWorking() { _drivingFlag = 0; }

        public IReadOnlyList<SignalValue> Decode(CanFrame frame)
        {
            if (frame.Id != 0x0C000E00) return Array.Empty<SignalValue>();
            var driving = _drivingFlag;
            return new[]
            {
                new SignalValue("Operating_Mode", "Driving_Status", driving, driving, null, frame.Timestamp),
                new SignalValue("Operating_Mode", "Working_Status", 1 - driving, 1 - driving, null, frame.Timestamp),
            };
        }
    }

    [Fact]
    public async Task TC_010_driving_then_working_transition()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var decoder = new ModeStubDecoder();
        var alarm = new AlarmEngine(Array.Empty<IAlarmRule>());
        var pipeline = new CanReceivePipeline(bus.Frames, decoder, alarm);
        using var pipelineSub = pipeline.DecodedSignals.Subscribe(_ => { });

        var ctx = new TestRunnerContext(bus, pipeline.DecodedSignals, alarm.AlarmChanges, decoder);
        var runner = new TestRunner(
            new IStepExecutor[] { new ObserveBitStepExecutor(), new WaitStepExecutor() }, ctx);

        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                bus.Inject(new CanFrame(0x0C000E00, true, new byte[] { 1 }, DateTimeOffset.UtcNow, CanDirection.Rx));
                await Task.Delay(50);
            }
        });

        // 1단계: Driving=true 확인
        var phase1 = new TestCase("TC-010a", "OperatingMode", "Driving", Array.Empty<TestStep>(),
            new TestStep[] { new ObserveBitStep("Operating_Mode", "Driving_Status", true, TimeSpan.FromMilliseconds(500)) });
        (await runner.RunAsync(phase1)).Outcome.Should().Be(TestOutcome.Passed);

        // 상태 전환
        decoder.SetWorking();

        // 2단계: Working=true 확인
        var phase2 = new TestCase("TC-010b", "OperatingMode", "Working", Array.Empty<TestStep>(),
            new TestStep[]
            {
                new WaitStep(TimeSpan.FromMilliseconds(100)),
                new ObserveBitStep("Operating_Mode", "Working_Status", true, TimeSpan.FromMilliseconds(500)),
                new ObserveBitStep("Operating_Mode", "Driving_Status", false, TimeSpan.FromMilliseconds(500)),
            });
        var result2 = await runner.RunAsync(phase2);
        cts.Cancel();

        result2.Outcome.Should().Be(TestOutcome.Passed);
        result2.StepLog.Should().HaveCount(3).And.OnlyContain(p => p.Outcome == StepOutcome.Passed);
    }
}
```

- [ ] **Step 2: 테스트 실행**

```bash
dotnet test tests/Integration.Tests/CanMonitor.Integration.Tests.csproj --filter "FullyQualifiedName~Tc010DrivingWorkingTests"
```

Expected: Passed: 1.

- [ ] **Step 3: Commit**

```bash
git add tests/Integration.Tests/TestCases/Tc010DrivingWorkingTests.cs
git commit -m "test(integration): add TC-010 Driving→Working transition"
```

---

## Task 23: README 업데이트 + 전체 빌드/테스트 검증

**Files:**
- Modify: `README.md`

- [ ] **Step 1: README Status 갱신**

Modify `README.md` `Status:` 라인을 다음으로 교체:

```markdown
Status: **Phase 2a (Application)** — CanEventHub / Receive·Transmit Pipelines / AlarmEngine / TxScheduler + Test DSL + TC-001/002/010 자동화 완료. Phase 2b (Q1/Q3 해제 후 EEC1 heartbeat + VirtualInput + TC-024) 대기.
```

Modify `## Layout` 섹션에 라인 추가:

```markdown
- `src/Application/` — Rx 파이프라인 조립 + Test DSL runner (Phase 2a)
```

- [ ] **Step 2: 전체 빌드**

```bash
dotnet build
```

Expected: Build succeeded. 0 Warning(s) 0 Error(s).

- [ ] **Step 3: 전체 테스트**

```bash
dotnet test
```

Expected: Failed: 0 (Total passed = 35 Phase 1 + 신규 ~40).

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: bump status to Phase 2a (Application) complete"
```

---

## Deferred to Plan B (Phase 2b, Q1/Q3 해제 후)

다음 항목은 Q1 (EEC1 payload 확정) / Q3 (Virtual Input bitmap 확정) 이후 별도 플랜에서 구현:

1. `Eec1HeartbeatProvider` — Q1 payload에 맞는 100ms 주기 송신 및 `EL0601_EEC1_Timeout` rule.
2. `VirtualInputHeartbeat` — Q3 bitmap 기준 50ms 송신.
3. `VirtualInputService` — spec §6의 builder 패턴 vs. 현재 record replacement 중 유지할 형태 결정. Plan A가 채택한 `Update(VirtualInputState next)` 단순형은 그대로 둘 수도 있다 — Phase 2b 시작 시 builder 필요성 재평가.
4. `SetVirtualInputStep` / `SetHeartbeatStep` / `EnterSimulationModeStep` / `ExitSimulationModeStep` executors.
5. `BusLifecycleService` — `ManualBusStatusPublisher`를 대체, `ICanBus.OpenAsync` 재시도/fault 상태 관리.
6. `dbc/experimental/` — EEC1 J1939 잠정판 + Virtual Input placeholder DBC.
7. `TC-024 EEC1 Timeout` 자동화 (SetHeartbeat(EEC1,false) → Wait → ObserveBit(`EL0601.Active`, true)).
8. Q3 차단이었던 TC-003~009, 011~012, 017, 020, 025 자동화 (총 12건).

---

## Open Questions / Decisions

- **DQ-2a-1 (확정)**: BusStatus 공급자는 Plan A에서 `ManualBusStatusPublisher` stub, Plan B에서 `BusLifecycleService`로 교체. 구독자 (hub/UI)는 `IObservable<BusStatusChange>` 계약만 의존하므로 교체 시 downstream 영향 없음.
- **DQ-2a-2 (확정)**: Alarm rule은 Phase 2a에서 `AlarmRuleFactory.CreatePhase2aRules()`로 코드 생성. DBC 파서 기반 자동 생성은 Phase 3+에서 재평가 (YAGNI).
- **DQ-2a-3 (확정)**: Test DSL record는 10종 모두 Phase 2a에 정의하지만 Q3/Q1 blocked 4종은 executor 미구현. Runner는 `NotSupportedException`을 던져 Phase 2b 전 조기 fail-fast.
- **DQ-2a-4 (확정)**: `IVirtualInputService`는 현재 record-replacement 형태를 유지. spec §6의 builder 패턴은 Phase 2b 시작 시 재평가 (Phase 1 코드 변경 최소화).
- **DQ-2a-5 (열림)**: `TxScheduler.DrainForTestsAsync`는 테스트 전용 helper. Phase 2b에서 `Flush()` 공식 API로 승격할지, 테스트 헬퍼로 남길지 결정. Phase 2a는 테스트 통과 목적으로 유지.
- **DQ-2a-6 (확정)**: **Composition wiring 책임**은 Phase 3 `App.xaml.cs`(또는 동등한 host bootstrap)에 둔다. 즉 `pipeline.DecodedSignals → hub.AttachDecodedSignals(...)`, `alarm.AlarmChanges → hub.AttachAlarms(...)`, `busLifecycle.StatusChanges → hub.AttachBusStatus(...)` 배선은 `AddCanMonitorApplication()` 내부에서 하지 **않는다** — DI 컨테이너는 singleton 등록만 수행하며, Subject/Subscription 와이어링은 host가 `IServiceProvider` 해상 후 수동 수행. 이유: (a) 테스트가 partial wiring을 선택할 수 있게 하고, (b) Phase 2b에서 `BusLifecycleService`가 `ManualBusStatusPublisher`를 교체할 때 DI 레이어 변경 없이 host 레이어에서만 처리하기 위함. Phase 2a integration 테스트(Task 17~22)는 각 테스트가 필요한 최소 구성만 수동 wire 한다.
