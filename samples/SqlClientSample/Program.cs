using Microsoft.Data.SqlClient;

try
{
    var connectionString = args.Length > 0 ? args[1] : "Server=sqlprosample.database.windows.net;Database=sqlprosample;user=sqlproro;password=nh{Zd?*8ZU@Y}Bb#";
    using var connection = new SqlConnection(connectionString);
    connection.Open();
    using var command = new SqlCommand("Select @@version", connection);
    var result = command.ExecuteScalar();

    if (BinaryData.Empty.IsEmpty)
    {
        Console.WriteLine($"✅ {result}");
    }
    else
    {
        throw new InvalidOperationException("BinaryData.Empty.IsEmpty returned false");
    }
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"❌ {exception}");
    return 1;
}
