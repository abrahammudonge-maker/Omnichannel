namespace Omni.Domain.Entities;

public sealed class ConversationTag
{
    public Guid ConversationId { get; set; }
    public Guid TagId { get; set; }
}
