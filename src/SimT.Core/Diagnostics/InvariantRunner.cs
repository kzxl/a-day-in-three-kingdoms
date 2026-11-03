using SimT.Contracts.Diagnostics;

namespace SimT.Core.Diagnostics;

/// <summary>Runs every registered invariant and reports all failures instead of stopping at the first.</summary>
public sealed class InvariantRunner
{
    private readonly List<IInvariant> _invariants = new();
    private readonly IInvariantContext _context;

    public InvariantRunner(IInvariantContext context) => _context = context;

    public IReadOnlyList<IInvariant> Invariants => _invariants;

    public void Register(IInvariant invariant)
    {
        if (_invariants.Any(existing => string.Equals(existing.Id, invariant.Id, StringComparison.Ordinal)))
        {
            throw new Contracts.SimException("invariant.duplicate", $"Invariant '{invariant.Id}' is already registered.");
        }

        _invariants.Add(invariant);
    }

    public IReadOnlyList<InvariantFailure> Run()
    {
        var failures = new List<InvariantFailure>();
        foreach (var invariant in _invariants)
        {
            string? message;
            try
            {
                message = invariant.Check(_context);
            }
            catch (Exception error)
            {
                failures.Add(new InvariantFailure(invariant.Id, $"threw {error.GetType().Name}: {error.Message}"));
                continue;
            }

            if (message is not null)
            {
                failures.Add(new InvariantFailure(invariant.Id, message));
            }
        }

        return failures;
    }
}
