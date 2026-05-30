using System.Text.Json;
using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public sealed class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string RootDirectory { get; }

    public SessionStore(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenSteps",
            "Sessions");
    }

    public async Task<string> SaveAsync(RecordingSession session, CancellationToken cancellationToken = default)
    {
        var sessionDirectory = GetSessionDirectory(session);
        var imagesDirectory = Path.Combine(sessionDirectory, "images");
        Directory.CreateDirectory(imagesDirectory);

        session.OutputDirectory = sessionDirectory;
        for (var i = 0; i < session.Steps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = session.Steps[i];
            step.Index = i + 1;

            if (!string.IsNullOrWhiteSpace(step.ScreenshotPath) && File.Exists(step.ScreenshotPath))
            {
                var destination = Path.Combine(imagesDirectory, $"step-{step.Index:000}.png");
                if (!Path.GetFullPath(step.ScreenshotPath).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(step.ScreenshotPath, destination, overwrite: true);
                }

                step.ScreenshotPath = destination;
                step.ScreenshotCaptured = true;
            }
        }

        var jsonPath = Path.Combine(sessionDirectory, "session.json");
        await using var stream = File.Create(jsonPath);
        await JsonSerializer.SerializeAsync(stream, session, JsonOptions, cancellationToken);
        return jsonPath;
    }

    public async Task<RecordingSession?> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(RootDirectory))
        {
            return null;
        }

        var latest = Directory
            .EnumerateFiles(RootDirectory, "session.json", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();

        return latest is null ? null : await LoadAsync(latest.FullName, cancellationToken);
    }

    public async Task<RecordingSession?> LoadAsync(string sessionJsonPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sessionJsonPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(sessionJsonPath);
        var session = await JsonSerializer.DeserializeAsync<RecordingSession>(stream, JsonOptions, cancellationToken);
        if (session is not null)
        {
            session.OutputDirectory = Path.GetDirectoryName(sessionJsonPath) ?? session.OutputDirectory;
        }

        return session;
    }

    private string GetSessionDirectory(RecordingSession session)
    {
        return Path.Combine(RootDirectory, session.Id.ToString("N"));
    }
}
