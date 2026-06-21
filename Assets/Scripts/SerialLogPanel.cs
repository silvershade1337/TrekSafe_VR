// ─────────────────────────────────────────────────────────────────────────────
// SerialLogPanel.cs
//
// Requires: Edit → Project Settings → Player → Other Settings
//           → Api Compatibility Level → .NET Framework
//
// If System.IO.Ports still can't be found after that change, also create
// Assets/link.xml with:
//   <linker><assembly fullname="System" preserve="all"/></linker>
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One parsed reading from a device at a point in time, e.g.:
/// ID=T1,LAT=0.000000,LON=0.000000,TEMP=28.4,PRESS=90972,ALT=899.7,SOS=1,FALL=0
/// Used both as the "latest" snapshot and as a single entry in a device's History list.
/// </summary>
public class DeviceData
{
    public string Id;
    public float Lat;
    public float Lon;
    public float Temp;
    public float Press;
    public float Alt;
    public bool Sos;
    public bool Fall;
    public DateTime Timestamp;

    public override string ToString()
    {
        return $"{Id}: TEMP={Temp:F1} PRESS={Press:F0} ALT={Alt:F1} " +
               $"LAT={Lat:F6} LON={Lon:F6} SOS={Sos} FALL={Fall} " +
               $"(updated {Timestamp:HH:mm:ss})";
    }
}

/// <summary>
/// Full record for one device: its latest reading plus the complete history
/// of every reading received this session — handy for graphing (temp/press/alt
/// over time) and for drawing a GPS trail on a map (Lat/Lon sequence).
/// </summary>
public class DeviceRecord
{
    public string Id;
    public DeviceData Latest;
    public readonly List<DeviceData> History = new List<DeviceData>();
}

public class SerialLogPanel : MonoBehaviour
{
    [Header("Serial Settings")]
    [Tooltip("e.g. COM3 on Windows  |  /dev/ttyUSB0 on Linux/Mac")]
    public string portName = "COM3";
    public int baudRate = 115200;
    public int readTimeoutMs = 100;

    [Header("UI References")]
    [Tooltip("TextMeshProUGUI inside ScrollView > Viewport > Content")]
    public TextMeshProUGUI logText;

    [Tooltip("The ScrollRect of your ScrollView")]
    public ScrollRect scrollRect;

    [Header("Log Settings")]
    [Tooltip("Maximum lines kept in the raw text panel before old ones are trimmed")]
    public int maxLines = 200;

    [Header("CSV Logging")]
    [Tooltip("If true, every parsed reading is appended to a CSV file on disk")]
    public bool enableCsvLogging = true;

    [Tooltip("Folder the CSV is written into. Relative paths resolve under Application.persistentDataPath.")]
    public string csvFolder = "DeviceLogs";

    // ── Parsed device storage ───────────────────────────────────────────────
    // Keyed by device ID (e.g. "T1", "T2"...). Each record holds the latest
    // reading AND the full history of readings for that device this session.
    private readonly Dictionary<string, DeviceRecord> _devices = new Dictionary<string, DeviceRecord>();
    public IReadOnlyDictionary<string, DeviceRecord> Devices => _devices;

    // Matches: ID=T1,LAT=0.000000,LON=0.000000,TEMP=28.4,PRESS=90972,ALT=899.7,SOS=1,FALL=0
    private static readonly Regex DataLineRegex = new Regex(
        @"ID=(?<id>\w+)" +
        @",LAT=(?<lat>-?\d+(\.\d+)?)" +
        @",LON=(?<lon>-?\d+(\.\d+)?)" +
        @",TEMP=(?<temp>-?\d+(\.\d+)?)" +
        @",PRESS=(?<press>-?\d+(\.\d+)?)" +
        @",ALT=(?<alt>-?\d+(\.\d+)?)" +
        @",SOS=(?<sos>\d)" +
        @",FALL=(?<fall>\d)",
        RegexOptions.Compiled);

    // ── internals ──────────────────────────────────────────────────────────
    private SerialPort              _port;
    private Thread                  _readThread;
    private CancellationTokenSource _cts;

    private readonly ConcurrentQueue<string> _lineQueue  = new ConcurrentQueue<string>();
    private readonly Queue<string>           _lineBuffer = new Queue<string>();

    // One open StreamWriter per device ID, so each device gets its own CSV file.
    private readonly Dictionary<string, StreamWriter> _csvWriters = new Dictionary<string, StreamWriter>();
    private string _csvDirPath;

    // ── Unity lifecycle ────────────────────────────────────────────────────
    private void Start()
    {
        if (enableCsvLogging)
        {
            _csvDirPath = Path.Combine(Application.persistentDataPath, csvFolder);
            Directory.CreateDirectory(_csvDirPath);
        }

        OpenPort();
    }

    private void OnDestroy()
    {
        ClosePort();
        CloseCsvWriters();
    }

    private void OnApplicationQuit()
    {
        ClosePort();
        CloseCsvWriters();
    }

    private void Update()
    {
        bool gotNew = false;

        while (_lineQueue.TryDequeue(out string line))
        {
            _lineBuffer.Enqueue(line);
            if (_lineBuffer.Count > maxLines)
                _lineBuffer.Dequeue();

            TryParseDeviceLine(line);

            gotNew = true;
        }

        if (!gotNew) return;

        if (logText != null)
            logText.text = string.Join("\n", _lineBuffer);

        // Force the Content Size Fitter + layout to recalculate THIS frame,
        // before we touch the scroll position — otherwise ScrollRect reads
        // a stale (pre-resize) content height and the scrollbar/clamping
        // ends up one frame behind, which is what causes it to disappear
        // or stop short of the real bottom.
        if (scrollRect != null && scrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // ── Parsing ─────────────────────────────────────────────────────────────
    private void TryParseDeviceLine(string line)
    {
        Match m = DataLineRegex.Match(line);
        if (!m.Success) return; // not a data line (could be a status/log line) — ignore

        string id = m.Groups["id"].Value;

        var data = new DeviceData
        {
            Id        = id,
            Lat       = ParseFloat(m.Groups["lat"].Value),
            Lon       = ParseFloat(m.Groups["lon"].Value),
            Temp      = ParseFloat(m.Groups["temp"].Value),
            Press     = ParseFloat(m.Groups["press"].Value),
            Alt       = ParseFloat(m.Groups["alt"].Value),
            Sos       = m.Groups["sos"].Value == "1",
            Fall      = m.Groups["fall"].Value == "1",
            Timestamp = DateTime.Now
        };

        if (!_devices.TryGetValue(id, out DeviceRecord record))
        {
            record = new DeviceRecord { Id = id };
            _devices[id] = record;
        }

        record.Latest = data;
        record.History.Add(data); // full session history — used for graphs + map trail

        if (enableCsvLogging)
            WriteCsvRow(id, data);
    }

    private static float ParseFloat(string s)
    {
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
            ? result
            : 0f;
    }

    /// <summary>Latest reading only, e.g. GetDevice("T1")?.Temp</summary>
    public DeviceData GetDevice(string id)
    {
        return _devices.TryGetValue(id, out DeviceRecord record) ? record.Latest : null;
    }

    /// <summary>Full history for one device — use this for graphs / map trail.</summary>
    public List<DeviceData> GetHistory(string id)
    {
        return _devices.TryGetValue(id, out DeviceRecord record) ? record.History : null;
    }

    // ── CSV logging ─────────────────────────────────────────────────────────
    private void WriteCsvRow(string id, DeviceData data)
    {
        try
        {
            if (!_csvWriters.TryGetValue(id, out StreamWriter writer))
            {
                string path = Path.Combine(_csvDirPath, $"{id}.csv");
                bool isNewFile = !File.Exists(path);

                writer = new StreamWriter(path, append: true);
                _csvWriters[id] = writer;

                if (isNewFile)
                    writer.WriteLine("Timestamp,Lat,Lon,Temp,Press,Alt,SOS,FALL");
            }

            writer.WriteLine(string.Join(",",
                data.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                data.Lat.ToString(CultureInfo.InvariantCulture),
                data.Lon.ToString(CultureInfo.InvariantCulture),
                data.Temp.ToString(CultureInfo.InvariantCulture),
                data.Press.ToString(CultureInfo.InvariantCulture),
                data.Alt.ToString(CultureInfo.InvariantCulture),
                data.Sos ? "1" : "0",
                data.Fall ? "1" : "0"));

            writer.Flush(); // flush immediately — readings are infrequent (5-10s), cost is negligible
        }
        catch (Exception ex)
        {
            Enqueue($"[CSV error] {ex.Message}");
        }
    }

    private void CloseCsvWriters()
    {
        foreach (var writer in _csvWriters.Values)
        {
            writer.Flush();
            writer.Dispose();
        }
        _csvWriters.Clear();
    }

    // ── Port management ────────────────────────────────────────────────────
    private void OpenPort()
    {
        try
        {
            _port = new SerialPort(portName, baudRate)
            {
                ReadTimeout = readTimeoutMs,
                NewLine     = "\n"   // change to "\r\n" if ESP sends CR+LF
            };
            _port.Open();

            _cts        = new CancellationTokenSource();
            _readThread = new Thread(() => ReadLoop(_cts.Token))
            {
                IsBackground = true,
                Name         = "SerialReadThread"
            };
            _readThread.Start();

            Enqueue($"[Serial] Connected → {portName} @ {baudRate} baud");
        }
        catch (Exception ex)
        {
            Enqueue($"[Serial] Failed to open {portName}: {ex.Message}");
        }
    }

    private void ClosePort()
    {
        _cts?.Cancel();
        _readThread?.Join(500);

        if (_port is { IsOpen: true })
            _port.Close();

        _port?.Dispose();
        _port = null;
    }

    // ── Serial read loop (background thread) ──────────────────────────────
    private void ReadLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _port is { IsOpen: true })
        {
            try
            {
                string line = _port.ReadLine();
                if (!string.IsNullOrEmpty(line))
                    Enqueue(line.TrimEnd('\r', '\n'));
            }
            catch (TimeoutException)
            {
                // expected — ReadTimeout hit, just loop again
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Enqueue($"[Serial error] {ex.Message}");
                break;
            }
        }
    }

    private void Enqueue(string msg) => _lineQueue.Enqueue(msg);
}
