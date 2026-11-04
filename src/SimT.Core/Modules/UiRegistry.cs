using SimT.Contracts;
using SimT.Contracts.Ui;

namespace SimT.Core.Modules;

/// <summary>
/// K7 (desktop variant) — modules publish screens and command handlers; the WPF host renders
/// them generically. Replaces the web build's <c>RouteRegistrar</c>.
/// </summary>
public sealed class UiRegistry : IUiRegistry
{
    private readonly List<IScreenContribution> _screens = new();
    private readonly Dictionary<string, IScreenCommandHandler> _handlers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<string>> _modalPresenters = new(StringComparer.Ordinal);

    public IReadOnlyList<IScreenContribution> Screens => _screens;

    public void AddScreen(IScreenContribution screen)
    {
        if (_screens.Any(existing => existing.ScreenId == screen.ScreenId))
        {
            throw new SimException("ui.duplicate_screen", $"Screen '{screen.ScreenId}' is registered twice.");
        }

        _screens.Add(screen);
        _screens.Sort((left, right) =>
        {
            var order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.CompareOrdinal(left.ScreenId, right.ScreenId);
        });
    }

    public void AddCommandHandler(IScreenCommandHandler handler)
    {
        if (!_handlers.TryAdd(handler.ScreenId, handler))
        {
            throw new SimException("ui.duplicate_handler", $"Command handler for '{handler.ScreenId}' is registered twice.");
        }
    }

    public bool TryGetCommandHandler(string screenId, out IScreenCommandHandler? handler) =>
        _handlers.TryGetValue(screenId, out handler);

    public void RegisterModalPresenter(string commandPrefix, Action<string> presenter)
    {
        _modalPresenters[commandPrefix] = presenter;
    }

    public bool TryPresentModal(string commandId)
    {
        foreach (var (prefix, presenter) in _modalPresenters)
        {
            if (commandId.StartsWith(prefix, StringComparison.Ordinal))
            {
                presenter(commandId);
                return true;
            }
        }

        return false;
    }
}
