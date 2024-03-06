using DotNet.Testcontainers.Configurations;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;

TestcontainersSettings.Logger = new DockerLogger();
await using var mongoContainer = new MongoDbBuilder().Build();
try
{
    await mongoContainer.StartAsync();
    var mongoSettings = MongoClientSettings.FromConnectionString(mongoContainer.GetConnectionString());
    if (args.Contains("--aws"))
    {
        mongoSettings.Credential = new MongoCredential("MONGODB-AWS", new MongoExternalIdentity("username"), new PasswordEvidence("password"));
    }
    var mongoClient = new MongoClient(mongoSettings);
    var database = mongoClient.GetDatabase("admin");
    var buildInfo = await database.RunCommandAsync(new BsonDocumentCommand<BsonDocument>(new BsonDocument { ["buildInfo"] = true }));
    Console.WriteLine($"✅ {buildInfo}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"❌ {exception}");
    return 1;
}

internal class DockerLogger : ILogger
{
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Console.WriteLine($"🐳 {formatter(state, exception)}");
    public bool IsEnabled(LogLevel logLevel) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}