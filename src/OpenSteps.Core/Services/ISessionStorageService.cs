using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public interface ISessionStorageService
{
    Task SaveSessionAsync(RecordingSession session, CancellationToken cancellationToken = default);

    Task<RecordingSession?> LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(CancellationToken cancellationToken = default);

    Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task RenameSessionAsync(Guid sessionId, string newTitle, CancellationToken cancellationToken = default);
}
