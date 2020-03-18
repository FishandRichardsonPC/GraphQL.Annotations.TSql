using System.Data.SqlClient;
using GraphQL.Annotations.TSql;
using GraphQL.Types;
using Microsoft.Extensions.Configuration;

namespace GraphQL.Annotations.ToDo.Example
{
	public class ConnectionGetter: ISqlConnectionGetter
	{
		private readonly IConfiguration _configuration;

		public ConnectionGetter(IConfiguration configuration)
		{
			this._configuration = configuration;
		}

		public SqlConnection GetConnection(IResolveFieldContext context)
		{
			return new SqlConnection(this._configuration.GetConnectionString("Database"));
		}
	}
}
