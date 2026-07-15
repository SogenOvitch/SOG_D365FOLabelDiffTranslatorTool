namespace LabelDiffTool.Core.Models;

/// <summary>
/// An in-memory representation of one label file for a single language.
/// Entry order is preserved (for faithful save-back) while an internal index
/// gives O(1) lookup by label id.
/// </summary>
public sealed class LabelFile
{
    // Label ids are case-sensitive in D365, so use an ordinal (case-sensitive) index.
    private readonly Dictionary<string, LabelEntry> _index = new(StringComparer.Ordinal);
    private readonly List<LabelEntry> _entries = new();

    public LabelFile(string fileId, string language, string? filePath = null)
    {
        FileId = fileId ?? throw new ArgumentNullException(nameof(fileId));
        Language = language;
        FilePath = filePath;
    }

    /// <summary>A logical identifier for this file, unique within a comparison (defaults to the file name).</summary>
    public string FileId { get; }

    /// <summary>Absolute path on disk, when the file was loaded from / will be saved to disk.</summary>
    public string? FilePath { get; set; }

    /// <summary>BCP-47-ish language tag, e.g. <c>en-US</c>. May be edited by the user or set by detection.</summary>
    public string Language { get; set; }

    /// <summary>Raw file-level header comment lines (verbatim, including the leading <c>;</c>).</summary>
    public IList<string> HeaderComments { get; } = new List<string>();

    /// <summary>All entries in original file order.</summary>
    public IReadOnlyList<LabelEntry> Entries => _entries;

    public int Count => _entries.Count;

    public bool Contains(string id) => _index.ContainsKey(id);

    public LabelEntry? Find(string id) => _index.TryGetValue(id, out var e) ? e : null;

    public bool TryGet(string id, out LabelEntry entry) => _index.TryGetValue(id, out entry!);

    /// <summary>Adds or replaces an entry, keeping ordering and the index in sync.</summary>
    public void AddOrReplace(LabelEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_index.TryGetValue(entry.Id, out var existing))
        {
            var i = _entries.IndexOf(existing);
            _entries[i] = entry;
        }
        else
        {
            _entries.Add(entry);
        }
        _index[entry.Id] = entry;
    }

    /// <summary>Position of the entry with the given id in file order, or -1 if absent.</summary>
    public int IndexOf(string id) => _index.TryGetValue(id, out var e) ? _entries.IndexOf(e) : -1;

    /// <summary>
    /// Inserts an entry at the given position, keeping the index in sync. If an entry with the
    /// same id already exists it is replaced in place (position unchanged). The index is clamped
    /// to the valid range.
    /// </summary>
    public void Insert(int index, LabelEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_index.TryGetValue(entry.Id, out var existing))
        {
            _entries[_entries.IndexOf(existing)] = entry;
        }
        else
        {
            _entries.Insert(Math.Clamp(index, 0, _entries.Count), entry);
        }
        _index[entry.Id] = entry;
    }

    public bool Remove(string id)
    {
        if (!_index.TryGetValue(id, out var e)) return false;
        _entries.Remove(e);
        return _index.Remove(id);
    }

    /// <summary>Removes all entries and header comments.</summary>
    public void Clear()
    {
        _entries.Clear();
        _index.Clear();
        HeaderComments.Clear();
    }

    /// <summary>
    /// Replaces this file's content (entries, header comments and language) with a copy of
    /// another file's, keeping this instance and its <see cref="FilePath"/>. Used to reload from disk.
    /// </summary>
    public void ReplaceWith(LabelFile other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Clear();
        foreach (var header in other.HeaderComments)
            HeaderComments.Add(header);
        foreach (var entry in other.Entries)
            AddOrReplace(entry.Clone());
        Language = other.Language;
    }
}
