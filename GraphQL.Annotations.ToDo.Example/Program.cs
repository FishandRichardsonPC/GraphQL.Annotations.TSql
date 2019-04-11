using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace GraphQL.Annotations.ToDo.Example
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Program.CreateWebHostBuilder(args).Build().Run();
		}

		public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
			WebHost.CreateDefaultBuilder(args)
				.UseStartup<Startup>();
	}
}
