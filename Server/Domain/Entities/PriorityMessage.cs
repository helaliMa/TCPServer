using Models;

namespace Server.Domain.Entities;

public class PriorityMessage
{
    public IMessage Message { get; set; } = default!;
    public int Priority { get; set; }
}