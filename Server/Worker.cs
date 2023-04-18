using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Models;
using Server.Domain.Interfaces;

namespace Server;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly MessageBroker _messageBroker;
    private readonly IEnumerable<IMessageSubscriber> _serviceWorkers;
    private List<IDisposable> _channelSubscriptions;
    
    public Worker(ILogger<Worker> logger, 
        IConfiguration configuration,
        IEnumerable<IMessageSubscriber> serviceWorkers, MessageBroker messageBroker)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceWorkers = serviceWorkers;
        _messageBroker = messageBroker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = _configuration.GetValue<int>("TcpServer:Port");
        _logger.LogInformation("Starting TCP Server on port {Port}", port);
        
        var listener = new TcpListener(System.Net.IPAddress.Any, port);
        listener.Start();
        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(stoppingToken);
            _ = HandleClientAsync(client, stoppingToken);
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _channelSubscriptions = new List<IDisposable>();
        foreach (var worker in _serviceWorkers)
        {
            var channelReader = _messageBroker.Subscribe(worker.SubscriberIds[0], worker.SubscriberIds);
            _channelSubscriptions.Add(
                worker.ListenToChannelAsync(channelReader)
                    .ContinueWith(_ => channelReader.Completion, cancellationToken));
        }
        return base.StartAsync(cancellationToken);
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Client connected from {RemoteEndpoint}", client.Client.RemoteEndPoint);

        using (client)
        {
            var pipe = new Pipe();
            var writing = FillPipeAsync(client, pipe.Writer, cancellationToken);
            var reading = ReadPipeAsync(pipe.Reader, cancellationToken);

            await Task.WhenAll(reading, writing);
        }

        _logger.LogInformation("Client disconnected from {RemoteEndpoint}", client.Client.RemoteEndPoint);
    }
    
    private async Task FillPipeAsync(TcpClient client, PipeWriter writer, CancellationToken cancellationToken)
    {
        const int minimumBufferSize = 512;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var memory = writer.GetMemory(minimumBufferSize);
                var bytesRead = await client.GetStream().ReadAsync(memory, cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                writer.Advance(bytesRead);
                var result = await writer.FlushAsync(cancellationToken);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reading from client");
        }
        finally
        {
            await writer.CompleteAsync();
        }
    }
    
    private async Task ReadPipeAsync(PipeReader reader, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                // Process the received data
                var jsonString = GetStringFromBuffer(buffer);
                try
                {
                    var message = JsonSerializer.Deserialize<Message>(jsonString);
                    _logger.LogInformation("Received Message: Id = {ID}, Name = {NAME}", message.Id, message.Name);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error while deserializing received JSON data");
                }

                // Mark the buffer as consumed
                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing received data");
        }
        finally
        {
            reader.Complete();
        }
    }
    
    private static string GetStringFromBuffer(in ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsSingleSegment)
        {
            return RemoveBomIfPresent(Encoding.UTF8.GetString(buffer.First.Span));
        }

        var sb = new StringBuilder();
        foreach (var segment in buffer)
        {
            sb.Append(Encoding.UTF8.GetString(segment.Span));
        }
        return RemoveBomIfPresent(sb.ToString());
    }

    private static string RemoveBomIfPresent(string text)
    {
        if (!string.IsNullOrEmpty(text) && text[0] == '\uFEFF')
        {
            return text.Substring(1);
        }
        return text;
    }
}
