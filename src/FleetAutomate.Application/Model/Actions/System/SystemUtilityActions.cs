using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Directory = System.IO.Directory;
using FormsClipboard = System.Windows.Forms.Clipboard;
using Path = System.IO.Path;
using SoundPlayer = System.Media.SoundPlayer;
using Thread = System.Threading.Thread;

namespace FleetAutomate.Model.Actions.System;

[DataContract]
public sealed class IfProcessExistsAction : ActionBase
{
    private string _processName = string.Empty;

    public override string Name => "If Process Exists";
    protected override string DefaultDescription => "Check if process is running";

    [DataMember]
    public string ProcessName
    {
        get => _processName;
        set => SetProperty(ref _processName, value);
    }

    [IgnoreDataMember]
    public bool Exists { get; private set; }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Exists = Process.GetProcessesByName(NormalizeProcessName(ProcessName)).Length > 0;
        return Task.CompletedTask;
    }

    private static string NormalizeProcessName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            throw new InvalidOperationException("ProcessName cannot be empty.");
        }

        return Path.GetFileNameWithoutExtension(processName);
    }
}

[DataContract]
public sealed class KillProcessAction : ActionBase
{
    private string _processName = string.Empty;
    private bool _succeedIfNotFound = true;

    public override string Name => "Kill Process";
    protected override string DefaultDescription => "Terminate a process";

    [DataMember]
    public string ProcessName
    {
        get => _processName;
        set => SetProperty(ref _processName, value);
    }

    [DataMember]
    public bool SucceedIfNotFound
    {
        get => _succeedIfNotFound;
        set => SetProperty(ref _succeedIfNotFound, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ProcessName));
        if (processes.Length == 0 && !SucceedIfNotFound)
        {
            throw new InvalidOperationException($"Process '{ProcessName}' was not found.");
        }

        foreach (var process in processes)
        {
            using (process)
            {
                if (process.Id == global::System.Environment.ProcessId)
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
            }
        }

        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class GetScreenshotAction : ActionBase
{
    private string _filePath = string.Empty;

    public override string Name => "Get Screenshot";
    protected override string DefaultDescription => "Capture screen to file";

    [DataMember]
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new InvalidOperationException("FilePath cannot be empty.");
        }

        var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1, 1);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, global::System.Drawing.Point.Empty, bounds.Size);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath)) ?? global::System.Environment.CurrentDirectory);
        bitmap.Save(FilePath, ImageFormat.Png);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class SetClipboardAction : ActionBase
{
    private string _text = string.Empty;

    public override string Name => "Set Clipboard";
    protected override string DefaultDescription => "Set clipboard text";

    [DataMember]
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            FormsClipboard.SetText(Text ?? string.Empty);
            return Task.CompletedTask;
        }

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                FormsClipboard.SetText(Text ?? string.Empty);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            throw failure;
        }

        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class PlaySoundAction : ActionBase
{
    private string _filePath = string.Empty;

    public override string Name => "Play Sound";
    protected override string DefaultDescription => "Play audio file";

    [DataMember]
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        using var player = new SoundPlayer(FilePath);
        player.PlaySync();
        return Task.CompletedTask;
    }
}
