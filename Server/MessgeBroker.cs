using System.Threading.Channels;

namespace Server;

using Models;
using Domain.Entities;

public class MessageBroker
{
    private readonly Dictionary<int, List<Channel<PriorityMessage>>> _subscriberChannels;
    private readonly ILogger<MessageBroker> _logger;
    private readonly int _channelCapacity;
    private readonly bool _preserveOrder;

    public MessageBroker(ILogger<MessageBroker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _subscriberChannels = new Dictionary<int, List<Channel<PriorityMessage>>>();

        _channelCapacity = configuration.GetValue<int>("Channel:Capacity");
        _preserveOrder = configuration.GetValue<bool>("Channel:PreserveOrder");
    }

    public ChannelReader<PriorityMessage> Subscribe(int messageId, IReadOnlyList<int> subscriberIds)
    {
        if (!_subscriberChannels.ContainsKey(messageId))
        {
            _subscriberChannels[messageId] = new List<Channel<PriorityMessage>>();
        }

        var options = new BoundedChannelOptions(_channelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = !_preserveOrder,
            SingleWriter = false
        };

        var channel = Channel.CreateBounded<PriorityMessage>(options);
        _subscriberChannels[messageId].Add(channel);
        _logger.LogInformation("Subscriber {SubscriberIds} subscribed to message with ID {MessageId}",
            string.Join(",", subscriberIds), messageId);

        return channel.Reader;
    }

    public async Task PublishAsync(IMessage message, int priority = 0, CancellationToken cancellationToken = default)
    {
        if (_subscriberChannels.TryGetValue(message.Id, out var channelList))
        {
            _logger.LogInformation("Publishing message with ID {MessageId}", message.Id);

            foreach (var channel in channelList)
            {
                await channel.Writer.WriteAsync(new PriorityMessage { Message = message, Priority = priority },
                    cancellationToken);
            }
        }
        else
        {
            _logger.LogWarning("No subscribers found for message with ID {MessageId}", message.Id);
        }
    }
}