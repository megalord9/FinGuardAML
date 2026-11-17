using System.Collections.Concurrent;
using FinGuardAML.Models;

namespace FinGuardAML.Services;

// Только для практической работы №3: данные хранятся в памяти до остановки API.
public sealed class TransactionStore
{
    private readonly ConcurrentDictionary<Guid, RiskAssessment> _entries = new();

    public void Add(RiskAssessment entry) => _entries[entry.TransactionId] = entry;

    public RiskAssessment? Find(Guid id) =>
        _entries.TryGetValue(id, out var entry) ? entry : null;

    public IReadOnlyList<RiskAssessment> GetAll() =>
        _entries.Values.OrderByDescending(x => x.CalculatedAtUtc).ToList();
}
