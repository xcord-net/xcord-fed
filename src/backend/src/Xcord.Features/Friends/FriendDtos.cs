using Xcord.Entities;

namespace Xcord.Features.Friends;

public sealed record FriendshipDto(
    long Id,
    long SenderId,
    string SenderUsername,
    string SenderDisplayName,
    string? SenderAvatarUrl,
    long ReceiverId,
    string ReceiverUsername,
    string ReceiverDisplayName,
    string? ReceiverAvatarUrl,
    FriendshipStatus Status,
    DateTimeOffset CreatedAt
);

public sealed record SendFriendRequestRequest(
    long UserId
);
