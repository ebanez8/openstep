using OpenSteps.Core.Models;

namespace OpenSteps.Core.Services;

public interface ISettingsService
{
    Task<AppSettings> LoadAsync();

    Task SaveAsync(AppSettings settings);
}
