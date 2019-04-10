using GraphQL.Annotations.ToDo.Example.models;
using GraphQL.Annotations.TSql;
using GraphQL.Annotations.TSql.AspNetCore;
using GraphQL.Server;
using GraphQL.Server.Ui.Playground;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraphQL.Annotations.ToDo.Example
{
	public class Startup
	{
		private readonly IHostingEnvironment _environment;

		public Startup(IConfiguration configuration, IHostingEnvironment environment)
		{
			this._environment = environment;
			this.Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

			// In production, the React files will be served from this directory
			services.AddSpaStaticFiles(
				configuration => { configuration.RootPath = "ClientApp/build"; });

			services.AddSingleton<Query>();
			services.AddSingleton<Mutation>();
			services.AddSingleton<ToDoSchema>();
			services.AddSingleton<ISqlConnectionGetter, ConnectionGetter>();

			services.AddGraphQL(
				options =>
				{
					options.EnableMetrics = true;
					options.ExposeExceptions = this._environment.IsDevelopment();
				})
				.AddHttpContextUserContextBuilder();

			services.AddGraphQLTSql<Query, Mutation>();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}
			else
			{
				app.UseExceptionHandler("/Error");
				// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			app.UseStaticFiles();
			app.UseSpaStaticFiles();

			app.UseGraphQL<ToDoSchema>("/graphql");
			app.UseGraphQLPlayground(new GraphQLPlaygroundOptions());

			app.UseMvc(
				routes =>
				{
					routes.MapRoute(
						"default",
						"{controller}/{action=Index}/{id?}");
				});

			app.UseSpa(
				spa =>
				{
					spa.Options.SourcePath = "ClientApp";

					if (env.IsDevelopment())
					{
						spa.UseReactDevelopmentServer("start");
					}
				});
		}
	}
}
