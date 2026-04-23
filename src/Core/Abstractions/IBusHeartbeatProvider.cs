using CanMonitor.Core.Models;

namespace CanMonitor.Core.Abstractions;

/// <summary>
/// 주기적 송신자 추상화. Enabled 상태는 읽기 전용이며 변경은
/// <see cref="SetEnabled"/> 를 통해 이루어진다. 이를 통해
/// <see cref="EnabledChanges"/> 가 UI 와 BusLifecycleService 양쪽에 동일한
/// 단일 진실원(source of truth)을 방출한다. 스펙 §6/§12 참조.
/// </summary>
public interface IBusHeartbeatProvider
{
    string Name { get; }
    TimeSpan Period { get; }
    CanFrame BuildFrame();

    bool Enabled { get; }
    IObservable<bool> EnabledChanges { get; }
    void SetEnabled(bool enabled);
}
