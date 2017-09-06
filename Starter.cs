﻿#region Related components
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Xml;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	public static class RepositoryStarter
	{
		/// <summary>
		/// Initializes all types of the assembly
		/// </summary>
		/// <param name="assembly">The assembly</param>
		public static void Initialize(Assembly assembly)
		{
			try
			{
				// repositories
				assembly.GetTypes()
					.Where(type => type.IsDefined(typeof(RepositoryAttribute), false))
					.ForEach(type => RepositoryDefinition.Register(type));

				// entities
				assembly.GetTypes()
					.Where(type => type.IsDefined(typeof(EntityAttribute), false))
					.ForEach(type => EntityDefinition.Register(type));
			}
			catch (ReflectionTypeLoadException ex)
			{
				var gotFileNotFoundException = false;
				foreach (var exception in ex.LoaderExceptions)
					if (exception is System.IO.FileNotFoundException)
					{
						gotFileNotFoundException = true;
						break;
					}

				if (!gotFileNotFoundException)
					throw ex;
			}
			catch (Exception)
			{
				throw;
			}
		}

		/// <summary>
		/// Initializes all types in assemblies
		/// </summary>
		/// <param name="assemblies">The collection of assemblies</param>
		/// <param name="updateFromConfigurationFile">true to update other settings from configuration file on the disc</param>
		public static void Initialize(IEnumerable<Assembly> assemblies, bool updateFromConfigurationFile = true)
		{
#if DEBUG
			RepositoryMediator.WriteLogs("Start to initialize repositories & entities [" + assemblies.Select(a => a.FullName.Left(a.FullName.IndexOf(","))).ToString(", ") + "]");
#endif

			// initialize & register all types
			assemblies.ForEach(assembly => RepositoryStarter.Initialize(assembly));

			// read configuration and update
			var config = ConfigurationManager.GetSection("net.vieapps.repositories") as AppConfigurationSectionHandler;
			if (config != null)
			{
				// update settings of data sources
				if (config.Section.SelectNodes("dataSources/dataSource") is XmlNodeList dataSourceNodes)
					foreach (XmlNode dataSourceNode in dataSourceNodes)
					{
						var dataSource = DataSource.FromJson(config.GetJson(dataSourceNode));
						if (!RepositoryMediator.DataSources.ContainsKey(dataSource.Name))
							RepositoryMediator.DataSources.Add(dataSource.Name, dataSource);
					}

				// update settings of repositories
				if (config.Section.SelectNodes("repository") is XmlNodeList repositoryNodes)
					foreach (XmlNode repositoryNode in repositoryNodes)
					{
						// update repository
						RepositoryDefinition.Update(config.GetJson(repositoryNode));

						// update repository entities
						if (repositoryNode.SelectNodes("entity") is XmlNodeList entityNodes)
							foreach (XmlNode repositoryEntityNode in entityNodes)
								EntityDefinition.Update(config.GetJson(repositoryEntityNode));
					}

				// schemas (SQL)
				if ("true".IsEquals(config.Section.Attributes["ensureSchemas"]?.Value))
					Task.Run(async () =>
					{
						try
						{
							await RepositoryStarter.EnsureSqlSchemasAsync();
						}
						catch { }
					}).ConfigureAwait(false);

				// indexes (NoSQL)
				if ("true".IsEquals(config.Section.Attributes["ensureIndexes"]?.Value))
					Task.Run(async () =>
					{
						try
						{
							await RepositoryStarter.EnsureNoSqlIndexesAsync();
						}
						catch { }
					}).ConfigureAwait(false);
			}
			else if (updateFromConfigurationFile)
				throw new ConfigurationErrorsException("Cannot find the configuration section named 'net.vieapps.repositories' in the configuration file");
		}

		/// <summary>
		/// Initializes all types of all assemblies of the current app
		/// </summary>
		/// <param name="selector">The function to select assemblies to initialize</param>
		public static void Initialize(Func<IEnumerable<Assembly>> selector = null)
		{
			RepositoryStarter.Initialize(selector != null
				? selector()
				: (new List<Assembly>() { Assembly.GetCallingAssembly() }).Concat(Assembly.GetCallingAssembly()
					.GetReferencedAssemblies()
					.Where(n => !n.Name.IsStartsWith("MsCorLib") && !n.Name.IsStartsWith("Microsoft") && !n.Name.IsStartsWith("System")
						&& !n.Name.IsStartsWith("Newtonsoft") && !n.Name.IsStartsWith("MongoDB")
						&& !n.Name.IsStartsWith("WampSharp") && !n.Name.IsStartsWith("VIEApps.Components.")
					)
					.Select(n => Assembly.Load(n))
				)
			);
		}

		static async Task EnsureSqlSchemasAsync()
		{
			foreach (var info in RepositoryMediator.EntityDefinitions)
			{
				var definition = info.Value;

				var dataSource = RepositoryMediator.GetPrimaryDataSource(null, definition);
				if (dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL))
					try
					{
						await definition.EnsureSchemasAsync(dataSource);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs("Cannot ensure schemas of SQL [" + definition.Type.GetTypeName(true) + "]", ex);
					}

				dataSource = RepositoryMediator.GetSecondaryDataSource(null, definition);
				if (dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL))
					try
					{
						await definition.EnsureSchemasAsync(dataSource);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs("Cannot ensure schemas of SQL [" + definition.Type.GetTypeName(true) + "]", ex);
					}
			}
		}

		static async Task EnsureNoSqlIndexesAsync()
		{
			foreach (var info in RepositoryMediator.EntityDefinitions)
			{
				var definition = info.Value;

				var dataSource = RepositoryMediator.GetPrimaryDataSource(null, definition);
				if (dataSource != null && dataSource.Mode.Equals(RepositoryMode.NoSQL))
					try
					{
						await definition.EnsureIndexesAsync(dataSource);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs("Cannot ensure indexes of NoSQL [" + definition.Type.GetTypeName(true) + "]", ex);
					}

				dataSource = RepositoryMediator.GetSecondaryDataSource(null, definition);
				if (dataSource != null && dataSource.Mode.Equals(RepositoryMode.NoSQL))
					try
					{
						await definition.EnsureIndexesAsync(dataSource);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs("Cannot ensure indexes of NoSQL [" + definition.Type.GetTypeName(true) + "]", ex);
					}
			}
		}
	}
}