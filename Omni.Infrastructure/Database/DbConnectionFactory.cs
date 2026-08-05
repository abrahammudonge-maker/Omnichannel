using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Omni.Infrastructure.Database;

public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? @"Server=localhost\SQLEXPRESS;Database=omnichannel;Trusted_Connection=True;Encrypt=False;";
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
