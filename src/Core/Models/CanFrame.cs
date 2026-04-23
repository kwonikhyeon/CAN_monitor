namespace CanMonitor.Core.Models;

/// <summary>
/// 불변 CAN 프레임. <c>Data</c> 는 반드시 독립적으로 소유된 버퍼를 가리켜야 한다 —
/// 드라이버의 재사용/풀링된 네이티브 버퍼를 감싸면 안 된다. 프레임 생성자
/// (ICanBus 구현체) 가 진입 시점에 payload 를 복사할 책임을 진다. 스펙 §5/§8 참조.
/// </summary>
public readonly record struct CanFrame(
    uint Id,
    bool IsExtended,
    ReadOnlyMemory<byte> Data,
    DateTimeOffset Timestamp,
    CanDirection Direction);
