#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace SpecialPG;

/// <summary>
/// Context-scoped persistent undo journal for lightweight runtime snapshots.
/// Each context is stored independently under user://undo/.
/// </summary>
public static class UndoJournal
{
    private static readonly Dictionary<string, int> SuspendedContextRefCounts = new();

    public static IDisposable Suspend(string context) => new SuspendScope(context);

    public static void Push<TSnapshot>(string context, TSnapshot snapshot, int capacity = 128)
    {
        if (IsSuspended(context))
        {
            return;
        }

        var list = ReadContextList<TSnapshot>(context);
        list.Add(snapshot);
        if (list.Count > capacity)
        {
            list.RemoveRange(0, list.Count - capacity);
        }

        WriteContextList(context, list);
    }

    public static bool TryPop<TSnapshot>(string context, out TSnapshot snapshot)
    {
        var list = ReadContextList<TSnapshot>(context);
        if (list.Count == 0)
        {
            snapshot = default!;
            return false;
        }

        var last = list.Count - 1;
        snapshot = list[last];
        list.RemoveAt(last);
        WriteContextList(context, list);
        return true;
    }

    private static bool IsSuspended(string context) =>
        SuspendedContextRefCounts.TryGetValue(context, out var refs) && refs > 0;

    private static string ContextPath(string context)
    {
        var safe = context.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
        return ProjectSettings.GlobalizePath($"user://undo/{safe}.json");
    }

    private static List<TSnapshot> ReadContextList<TSnapshot>(string context)
    {
        try
        {
            var path = ContextPath(context);
            if (!File.Exists(path))
            {
                return new List<TSnapshot>();
            }

            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<TSnapshot>>(json);
            return list ?? new List<TSnapshot>();
        }
        catch
        {
            return new List<TSnapshot>();
        }
    }

    private static void WriteContextList<TSnapshot>(string context, List<TSnapshot> list)
    {
        try
        {
            var path = ContextPath(context);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(list);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Best-effort persistence: failures should not block caller behavior.
        }
    }

    private sealed class SuspendScope : IDisposable
    {
        private readonly string _context;
        private bool _disposed;

        public SuspendScope(string context)
        {
            _context = context;
            SuspendedContextRefCounts.TryGetValue(_context, out var refs);
            SuspendedContextRefCounts[_context] = refs + 1;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!SuspendedContextRefCounts.TryGetValue(_context, out var refs))
            {
                return;
            }

            refs--;
            if (refs <= 0)
            {
                SuspendedContextRefCounts.Remove(_context);
            }
            else
            {
                SuspendedContextRefCounts[_context] = refs;
            }
        }
    }
}
