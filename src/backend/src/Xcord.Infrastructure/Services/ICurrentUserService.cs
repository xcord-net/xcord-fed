using Xcord;

namespace Xcord.Infrastructure.Services;

public interface ICurrentUserService
{
    Result<long> GetCurrentUserId();
}
