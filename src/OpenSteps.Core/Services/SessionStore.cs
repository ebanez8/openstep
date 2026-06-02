using System.Text.Json;
using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public sealed class SessionStore : ISessionStorageService
{
    private const int CurrentSchemaVersion = 1;

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

    public async Task SaveSessionAsync(RecordingSession session, CancellationToken cancellationToken = default)
    {
        await SaveAsync(session, cancellationToken);
    }

    public async Task<string> SaveAsync(RecordingSession session, CancellationToken cancellationToken = default)
    {
        var sessionDirectory = GetSessionDirectory(session.Id);
        var imagesDirectory = Path.Combine(sessionDirectory, "images");
        Directory.CreateDirectory(imagesDirectory);

        session.SchemaVersion = CurrentSchemaVersion;
        session.SessionDirectory = sessionDirectory;
        session.OutputDirectory = sessionDirectory;
        session.UpdatedAt = DateTimeOffset.Now;

        for (var i = 0; i < session.Steps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = session.Steps[i];
            step.Index = i + 1;
            PersistStepImage(step, imagesDirectory, edited: false);
            PersistStepImage(step, imagesDirectory, edited: true);
        }

        var jsonPath = GetSessionJsonPath(session.Id);
        await using var stream = File.Create(jsonPath);
        await JsonSerializer.SerializeAsync(stream, session, JsonOptions, cancellationToken);
        return jsonPath;
    }

    public async Task<RecordingSession?> LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await LoadAsync(GetSessionJsonPath(sessionId), cancellationToken);
    }

    public async Task<RecordingSession?> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await ListSessionsAsync(cancellationToken);
        var latest = sessions.FirstOrDefault();
        return latest is null ? null : await LoadSessionAsync(latest.Id, cancellationToken);
    }

    public async Task<RecordingSession?> LoadAsync(string sessionJsonPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sessionJsonPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(sessionJsonPath, cancellationToken);
            var session = JsonSerializer.Deserialize<RecordingSession>(json, JsonOptions);
            if (session is null || session.SchemaVersion > CurrentSchemaVersion)
            {
                return null;
            }

            ApplyLegacyImagePaths(session, json);
            ResolveRuntimePaths(session, Path.GetDirectoryName(sessionJsonPath) ?? string.Empty);
            return session;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(RootDirectory))
        {
            return [];
        }

        var summaries = new List<SessionSummary>();
        foreach (var sessionJson in Directory.EnumerateFiles(RootDirectory, "session.json", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var session = await LoadAsync(sessionJson, cancellationToken);
            if (session is null)
            {
                continue;
            }

            summaries.Add(new SessionSummary
            {
                Id = session.Id,
                Title = session.Title,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt,
                StepCount = session.Steps.Count,
                ThumbnailPath = session.Steps.Select(step => step.EffectiveScreenshotPath).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            });
        }

        return summaries.OrderByDescending(summary => summary.UpdatedAt).ToList();
    }

    public Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var directory = GetSessionDirectory(sessionId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        return Task.CompletedTask;
    }

    public async Task RenameSessionAsync(Guid sessionId, string newTitle, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return;
        }

        session.Title = string.IsNullOrWhiteSpace(newTitle) ? "Untitled OpenSteps Guide" : newTitle.Trim();
        await SaveSessionAsync(session, cancellationToken);
    }

    private void PersistStepImage(RecordedStep step, string imagesDirectory, bool edited)
    {
        var sourcePath = edited ? step.EditedScreenshotPath : step.ScreenshotPath;
        var relativePath = edited ? step.EditedScreenshotRelativePath : step.ScreenshotRelativePath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            if (edited)
            {
                step.EditedScreenshotRelativePath = null;
                step.EditedScreenshotPath = null;
            }

            return;
        }

        var fileName = Path.GetFileName(sourcePath);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            fileName = edited ? $"step-{step.Index:000}-redacted.png" : $"step-{step.Index:000}.png";
            relativePath = Path.Combine("images", fileName).Replace('\\', '/');
        }

        var destination = Path.Combine(imagesDirectory, Path.GetFileName(relativePath));
        if (!Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, destination, overwrite: true);
        }

        if (edited)
        {
            step.EditedScreenshotRelativePath = relativePath;
            step.EditedScreenshotPath = destination;
        }
        else
        {
            step.ScreenshotRelativePath = relativePath;
            step.ScreenshotPath = destination;
            step.ScreenshotCaptured = true;
        }
    }

    private static void ResolveRuntimePaths(RecordingSession session, string sessionDirectory)
    {
        session.SessionDirectory = sessionDirectory;
        session.OutputDirectory = sessionDirectory;
        foreach (var step in session.Steps)
        {
            step.ScreenshotPath = ResolvePath(sessionDirectory, step.ScreenshotRelativePath, step.ScreenshotPath);
            step.EditedScreenshotPath = ResolvePath(sessionDirectory, step.EditedScreenshotRelativePath, step.EditedScreenshotPath);
        }
    }

    private static void ApplyLegacyImagePaths(RecordingSession session, string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("steps", out var stepsElement) || stepsElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var count = Math.Min(session.Steps.Count, stepsElement.GetArrayLength());
            for (var i = 0; i < count; i++)
            {
                var stepElement = stepsElement[i];
                var step = session.Steps[i];
                if (string.IsNullOrWhiteSpace(step.ScreenshotRelativePath)
                    && stepElement.TryGetProperty("screenshotPath", out var screenshotPath)
                    && screenshotPath.ValueKind == JsonValueKind.String)
                {
                    step.ScreenshotPath = screenshotPath.GetString();
                }

                if (string.IsNullOrWhiteSpace(step.EditedScreenshotRelativePath)
                    && stepElement.TryGetProperty("editedScreenshotPath", out var editedScreenshotPath)
                    && editedScreenshotPath.ValueKind == JsonValueKind.String)
                {
                    step.EditedScreenshotPath = editedScreenshotPath.GetString();
                }
            }
        }
        catch (JsonException)
        {
        }
    }

    private static string? ResolvePath(string sessionDirectory, string? relativePath, string? fallbackPath)
    {
        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            return Path.GetFullPath(Path.Combine(sessionDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        return fallbackPath;
    }

    private string GetSessionJsonPath(Guid sessionId)
    {
        return Path.Combine(GetSessionDirectory(sessionId), "session.json");
    }

    private string GetSessionDirectory(Guid sessionId)
    {
        return Path.Combine(RootDirectory, sessionId.ToString("N"));
    }
}
