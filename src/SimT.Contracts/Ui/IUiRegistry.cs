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

public sealed record ScreenModel(
    string ScreenId,
    string Title,
    string StatusLine,
    IReadOnlyList<ScreenSection> Sections,
    IReadOnlyList<ScreenCommand> Commands)
{
    public static ScreenModel Empty(string screenId, string title, string statusLine = "") =>
        new(screenId, title, statusLine, Array.Empty<ScreenSection>(), Array.Empty<ScreenCommand>());
}

public sealed record ScreenSection(
    string Heading,
    IReadOnlyList<string> Columns,
    IReadOnlyList<ScreenRow> Rows);

/// <summary>A row owns its own buttons, so a data screen needs no host-side special cases.</summary>
public sealed record ScreenRow(IReadOnlyList<string> Cells, IReadOnlyList<ScreenCommand> Commands)
{
    public static ScreenRow Of(IReadOnlyList<string> cells) => new(cells, Array.Empty<ScreenCommand>());
}

public sealed record ScreenCommand(string Id, string Label);
