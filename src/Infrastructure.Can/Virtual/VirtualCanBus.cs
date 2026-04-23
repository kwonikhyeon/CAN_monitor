using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Infrastructure.Can.Virtual;

/// <summary>
/// 프로세스 내부 loopback bus. 테스트(와 Phase 2/4 시뮬레이터)는
/// <see cref="Inject"/> 로 프레임을 주입하며, <see cref="CanBusBase.SendAsync"/>
/// 는 이를 Direction=Tx 로 구독자에게 자동 echo 한다.
/// </summary>
public sealed class VirtualCanBus : CanBusBase
{
    public override string Name => "Virtual";

    public void Inject(CanFrame frame) => PublishRx(frame);

    protected override Task OpenDriverAsync(CanBusOptions options, CancellationToken ct)
        => Task.CompletedTask;

    protected override Task StopDriverAsync() => Task.CompletedTask;

    protected override Task SendDriverAsync(CanFrame frame, CancellationToken ct)
        => Task.CompletedTask;
}
