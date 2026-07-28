using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DVerse.Harness;

/// <summary>
/// The append only record of every gate verdict.
/// <para>
/// One ledger serves both stages. A generation time refusal and the CI re-check
/// of the same rule land in the same file, distinguished by
/// <see cref="GateVerdict.Stage"/>, because two ledgers would eventually
/// disagree and a reader would have no way to tell which one lied.
/// </para>
/// </summary>
public interface IRefusalLedger
{
    /// <summary>
    /// Validates and appends one verdict. Throws rather than writing anything
    /// if the verdict is malformed.
    /// </summary>
    void Record(GateVerdict verdict);

    /// <summary>Every verdict recorded so far, in write order.</summary>
    IReadOnlyList<GateVerdict> Read();
}

/// <summary>
/// JSON Lines ledger on disk. One verdict per line, appended, never rewritten.
/// <para>
/// JSONL rather than a JSON array so that appending is a single write with no
/// read-modify-write cycle. A crashed run leaves a truncated final line rather
/// than a corrupt document, and git diffs stay line oriented and reviewable.
/// </para>
/// </summary>
public sealed class JsonlRefusalLedger : IRefusalLedger
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    private readonly string _path;
    private readonly object _writeLock = new();

    /// <param name="path">
    /// Ledger file path. Parent directories are created on first write, not in
    /// the constructor, so simply constructing a ledger never touches the disk.
    /// </param>
    public JsonlRefusalLedger(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public void Record(GateVerdict verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        // Validate before touching disk. An invalid verdict must not be able to
        // half-write and leave the ledger with a line nothing can parse.
        verdict.Validate();

        var line = JsonSerializer.Serialize(verdict, SerializerOptions);

        if (line.Contains('\n') || line.Contains('\r'))
        {
            // Defensive: a newline inside a serialized record would split one
            // verdict across two ledger lines and silently corrupt every read
            // that followed it.
            throw new InvalidVerdictException(
                $"{verdict.GateId}: serialized verdict contains a line break.");
        }

        lock (_writeLock)
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    public IReadOnlyList<GateVerdict> Read()
    {
        if (!File.Exists(_path))
            return [];

        var verdicts = new List<GateVerdict>();

        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var verdict = JsonSerializer.Deserialize<GateVerdict>(line, SerializerOptions)
                ?? throw new LedgerCorruptException($"Ledger line deserialized to null: {line}");

            verdicts.Add(verdict);
        }

        return verdicts;
    }
}

/// <summary>The ledger on disk could not be read as written.</summary>
public sealed class LedgerCorruptException(string message) : Exception(message);
