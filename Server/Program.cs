using Serilog;
using Server;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    Log.Information("Starting Server");
    CreateHostBuilder(args).Build().Run();
}
catch (Exception e)
{
    Log.Fatal("ArcelorMittal server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .UseWindowsService()
        .UseSerilog()
        .ConfigureServices((hostContext, services) =>
        {
            services.AddHostedService<Worker>();
        });