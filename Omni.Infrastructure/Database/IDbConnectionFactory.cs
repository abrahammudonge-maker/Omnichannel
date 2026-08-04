using System.Data;

namespace Omni.Infrastructure.Database;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
