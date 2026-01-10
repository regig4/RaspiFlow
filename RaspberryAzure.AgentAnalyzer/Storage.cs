using System.Collections.Concurrent;

public class Storage
{
    private static readonly ConcurrentStack<List<Record>> Records = new();

    public static void AddRecords(List<Record> records)
    {
        Records.Push(records);
    }

    public static List<Record> GetLatestRecord()
    {
        Records.TryPeek(out var records);
        return records!;
    }
}