using SimT.Contracts;

namespace SimT.Core.Kernel;

/// <summary>
/// Holds an exclusive lock next to the save for the lifetime of the kernel. SQLite would allow
/// two connections and then fail somewhere in the middle of a turn with a raw SQLITE_BUSY; this
/// makes the conflict visible at open time with an actionable message.
/// </summary>
public sealed class CampaignLock : IDisposable
{
    private FileStream? _stream;

    private CampaignLock(FileStream stream) => _stream = stream;

    public static CampaignLock Acquire(string databasePath)
    {
        var lockPath = LockPathFor(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(lockPath))!);

        try
        {
            var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new CampaignLock(stream);
        }
        catch (IOException error)
        {
            throw new SimException(
                "save.locked",
                $"Ván này đang mở ở một cửa sổ hoặc tiến trình khác ({lockPath}). Đóng cửa sổ kia rồi thử lại.",
                error);
        }
        catch (UnauthorizedAccessException error)
        {
            throw new SimException("save.locked", $"Không tạo được khoá cho ván: {lockPath}", error);
        }
    }

    public static string LockPathFor(string databasePath) =>
        Path.ChangeExtension(Path.GetFullPath(databasePath), ".lock");

    public void Dispose()
    {
        if (_stream is null)
        {
            return;
        }

        var stream = _stream;
        _stream = null;
        stream.Dispose();
    }
}
