namespace Server.configuration;

public class ServiceConfiguration
{
    public string ServiceType  { get; set; }
    public IReadOnlyList<int> SubscriberIds  { get; set; }
}