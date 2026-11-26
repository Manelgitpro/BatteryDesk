using BatteryDesk.Core.Models;

namespace BatteryDesk.Core.Interfaces;

/// <summary>Abstração para leitores de bateria específicos de protocolo ou fabricante.</summary>
public interface IDeviceBatteryProvider
{
    string Name { get; }
    Task<IReadOnlyList<BatteryDevice>> ScanAsync(CancellationToken cancellationToken = default);
}
