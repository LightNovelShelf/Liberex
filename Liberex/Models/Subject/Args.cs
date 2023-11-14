using Liberex.Models.Context;

namespace Liberex.Models.Subject;

public class FileChangeArgs
{
    public string LibraryId { get; }
    public string LibraryPath { get; }
    public WatcherChangeTypes ChangeType { get; }
    public string FullPath { get; set; }

    public FileChangeArgs(string id, string libraryPath, WatcherChangeTypes changeType, string fullPath)
    {
        LibraryId = id;
        LibraryPath = libraryPath;
        ChangeType = changeType;
        FullPath = fullPath;
    }

    public FileChangeArgs(string id, string libraryPath, FileSystemEventArgs args)
    {
        LibraryId = id;
        LibraryPath = libraryPath;
        ChangeType = args.ChangeType;
        FullPath = args.FullPath;
    }

    public FileChangeArgs(Library library, WatcherChangeTypes changeType, string fullPath)
    {
        LibraryId = library.Id;
        LibraryPath = library.FullPath;
        ChangeType = changeType;
        FullPath = fullPath;
    }
}

public enum ChangeSource
{
    Library,
    Book,
    Series,
}

public enum ChangeType
{
    Add,
    ReAdd,
    Delete,
    Update,
}

public class LibraryChangeArgs
{
    public ChangeSource ChangeSource { get; }
    public ChangeType ChangeType { get; }
    public string Id { get; }
    public string Path { get; }

    public LibraryChangeArgs(ChangeSource changeSource, ChangeType changeType, string id, string path)
    {
        ChangeSource = changeSource;
        ChangeType = changeType;
        Id = id;
        Path = path;
    }
}

public class SeriesUpdateArgs
{
    public string SeriesId { get; }
    public DateTime UpdateTime { get; }

    public SeriesUpdateArgs(Series series, DateTime updateTime)
    {
        SeriesId = series.Id;
        UpdateTime = updateTime;
    }

    public SeriesUpdateArgs(string seriesId, DateTime updateTime)
    {
        SeriesId = seriesId;
        UpdateTime = updateTime;
    }
}