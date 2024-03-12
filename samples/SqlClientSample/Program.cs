using System.Diagnostics;
using Microsoft.Data.SqlClient;

try
{
    var connectionString = args.Length > 0 && !args[^1].StartsWith("--") ? args[^1] : "Server=sqlprosample.database.windows.net;Database=sqlprosample;user=sqlproro;password=nh{Zd?*8ZU@Y}Bb#";
    await using var dataSource = SqlClientFactory.Instance.CreateDataSource(connectionString);
    await using var command = dataSource.CreateCommand("Select @@version");
    var result = await command.ExecuteScalarAsync();

    if (BinaryData.Empty.IsEmpty)
    {
        Console.WriteLine($"✅ {result}");
    }
    else
    {
        throw new UnreachableException();
    }
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"❌ {exception}");
    return 1;
}
