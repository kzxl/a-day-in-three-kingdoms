namespace SimT.Contracts.Ui;

/// <summary>
/// UI contribution registry. Modules push screens and command handlers here; the WPF host
/// renders whatever is registered. This is the desktop replacement for the web build's
/// <c>RouteRegistrar</c>: the host knows no module, and a new DLC adds a tab with zero host
/// changes.
/// </summary>
public interface IUiRegistry
{
    void AddScreen(IScreenContribution screen);

    void AddCommandHandler(IScreenCommandHandler handler);

    IReadOnlyList<IScreenContribution> Screens { get; }

    bool TryGetCommandHandler(string screenId, out IScreenCommandHandler? handler);

    /// <summary>
    /// Registers a presenter for interactive modals/dialogs matching a command prefix (e.g. "npc:modal:", "duel:").
    /// </summary>
    void RegisterModalPresenter(string commandPrefix, Action<string> presenter);

    /// <summary>
    /// Attempts to route a command to a registered modal presenter. Returns true if consumed.
    /// </summary>
    bool TryPresentModal(string commandId);
}

/// <summary>A screen is a pure read model plus commands. No WPF types cross this boundary.</summary>
public interface IScreenContribution
{
    string ScreenId { get; }

    string Title { get; }

    int Order { get; }

    ScreenModel Render();
}

public interface IScreenCommandHandler
{
    string ScreenId { get; }

    /// <summary>Returns a status line for the user, or null when the command is silent.</summary>
    string? Execute(string commandId);
}

/// <summary>Visual weight of a command: destructive actions must not look like the happy path.</summary>
public enum CommandKind
{
    Primary,
    Secondary,
    Danger
}

public enum MetricTone
{
    Neutral,
    Positive,
    Negative,
    Warning
}

public enum AlertLevel
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// A command as data. <see cref="RequiresQuantity"/> makes the host ask for a number and send
/// <c>id:qty</c>; <see cref="Preview"/> lets the module explain the outcome before it happens
/// ("sẽ mất 144, còn 925") without the host knowing any rules.
/// </summary>
public sealed record ScreenCommand(
    string Id,
    string Label,
    CommandKind Kind = CommandKind.Primary,
    bool RequiresQuantity = false,
    Func<int, string>? Preview = null,
    string? Cost = null);

/// <summary>One headline number for the page header strip.</summary>
public sealed record ScreenMetric(string Label, string Value, string? Hint = null, MetricTone Tone = MetricTone.Neutral);

/// <summary>Something the player should look at, in the spirit of a management sim's alert feed.</summary>
public sealed record ScreenAlert(string Message, AlertLevel Level = AlertLevel.Info, string? ScreenId = null);

/// <summary>One labelled line in the inspector panel.</summary>
public sealed record ScreenDetail(string Label, string Value);

public sealed record ScreenModel(
    string ScreenId,
    string Title,
    string StatusLine,
    IReadOnlyList<ScreenSection> Sections,
    IReadOnlyList<ScreenCommand> Commands,
    IReadOnlyList<ScreenMetric>? Metrics = null,
    IReadOnlyList<ScreenAlert>? Alerts = null,
    string? Recommendation = null)
{
    public static ScreenModel Empty(string screenId, string title, string statusLine = "") =>
        new(screenId, title, statusLine, Array.Empty<ScreenSection>(), Array.Empty<ScreenCommand>());

    public IReadOnlyList<ScreenMetric> Headline => Metrics ?? Array.Empty<ScreenMetric>();

    public IReadOnlyList<ScreenAlert> Attention => Alerts ?? Array.Empty<ScreenAlert>();
}

public sealed record ScreenSection(
    string Heading,
    IReadOnlyList<string> Columns,
    IReadOnlyList<ScreenRow> Rows);

/// <summary>
/// A row owns its buttons and, optionally, the detail lines the inspector shows when it is
/// selected. Details are plain strings so the module keeps owning every number it presents.
/// </summary>
public sealed record ScreenRow(
    IReadOnlyList<string> Cells,
    IReadOnlyList<ScreenCommand> Commands,
    string? Title = null,
    IReadOnlyList<ScreenDetail>? Details = null,
    string? IconHint = null,
    string? RarityHint = null,
    bool IsMuted = false)
{
    public static ScreenRow Of(IReadOnlyList<string> cells) => new(cells, Array.Empty<ScreenCommand>());

    public static ScreenRow Of(IReadOnlyList<string> cells, params ScreenDetail[] details) =>
        new(cells, Array.Empty<ScreenCommand>(), null, details);

    public IReadOnlyList<ScreenDetail> Info => Details ?? Array.Empty<ScreenDetail>();
}
