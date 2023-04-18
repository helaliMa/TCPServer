using System.Threading.Channels;
using Server.configuration;
using Server.Domain.Entities;

namespace Server.Domain.Interfaces;

public interface IMessageSubscriber
{
    IReadOnlyList<int> SubscriberIds { get; }
    string ServiceType { get; }
    Task ListenToChannelAsync(ChannelReader<PriorityMessage> channelReader);
}