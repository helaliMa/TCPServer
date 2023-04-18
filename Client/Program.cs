// See https://aka.ms/new-console-template for more information

using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Models;

var serverIpAddress = "127.0.0.1"; // Change this to your server's IP address if needed
var serverPort = 12345;

using var client = new TcpClient();
await client.ConnectAsync(serverIpAddress, serverPort);

Console.WriteLine("Connected to server at {0}:{1}", serverIpAddress, serverPort);

using var stream = client.GetStream();
using var streamWriter = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
using var streamReader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

// Send a message to the server
var message = new Message
{
    Id = 1,
    Name = "this is my first Message "
};
var jsonString = JsonSerializer.Serialize(message);
await streamWriter.WriteLineAsync(jsonString);
await streamWriter.FlushAsync();

// Read the server's response
var serverResponse = await streamReader.ReadLineAsync();
Console.WriteLine("Server response: {0}", serverResponse);

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
