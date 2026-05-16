namespace Xcord.Api;

/// <summary>
/// Notify_* methods: notification subscription/dispatch.
///
/// Notifications are pushed to clients via the unified hub's "Notify_*"
/// SendAsync calls from <see cref="NotificationService"/>. There are no
/// inbound client-invoked Notify_* methods today; this partial exists so
/// the namespace has a clear home when client-driven notification
/// subscription is added.
/// </summary>
public partial class MainHub
{
}
