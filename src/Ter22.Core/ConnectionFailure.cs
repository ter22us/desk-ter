namespace Ter22.Core;

public enum ConnectionStage { Network, Relay, Tls, Approval, Displays }

public sealed class ConnectionFailureException(ConnectionStage stage, string message, Exception? inner = null)
    : IOException(message, inner)
{
    public ConnectionStage Stage { get; } = stage;
}

public enum RejectionReason { InvalidCode, Busy, Denied, ApprovalExpired }
public sealed record ConnectionRejection(RejectionReason Reason);
