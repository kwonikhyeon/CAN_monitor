using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

internal sealed record TxJob(string Name, CanFrame Frame);
