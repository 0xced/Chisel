using Microsoft.Data.SqlClient;

/*
 * This program demonstrates that an SQL connection can be successfully established when authenticating with a user and a password.
 *
 * Passing the --azure argument changes the authentication method to [managed identity][1] which actually requires the removed Azure package.
 * Since the package was transitively removed with <ChiselPackage Include="Azure.Identity" /> a `FileNotFoundException` will be thrown (when not using the Microsoft.Identity.Client stub)
 * > Could not load file or assembly 'Azure.Core, Version=1.35.0.0, Culture=neutral, PublicKeyToken=92742159e12e44c8'
 *
 * [1]: https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/azure-active-directory-authentication#using-managed-identity-authentication
 */

try
{
    var builder = new SqlConnectionStringBuilder { DataSource = "sqlprosample.database.windows.net", InitialCatalog = "sqlprosample" };
    if (args.Contains("--azure"))
    {
        builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
    }
    else
    {
        builder.UserID = "sqlproro";
        builder.Password = "nh{Zd?*8ZU@Y}Bb#";
    }

    using var connection = new SqlConnection(builder.ConnectionString);
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
