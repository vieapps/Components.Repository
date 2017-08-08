#region Related components
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
					.ForEach(type =>
					{
						RepositoryDefinition.Register(type);
					});

				// entities
				assembly.GetTypes()
					.Where(type => type.IsDefined(typeof(EntityAttribute), false))
					.ForEach(type =>
					{
						EntityDefinition.Register(type);
					});
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
		/// <param name="updateConfigFromDisc">true to update other settings from configuration file on the disc</param>
		public static void Initialize(IEnumerable<Assembly> assemblies, bool updateConfigFromDisc = true)
		{
#if DEBUG
			RepositoryMediator.WriteLogs("Start to initialize repositories & entities [" + assemblies.Select(a => a.FullName.Left(a.FullName.IndexOf(","))).ToString(", ") + "]");
#endif

			// initialize & register all types
			assemblies.ForEach(assembly =>
			{
				RepositoryStarter.Initialize(assembly);
			});

			// read configuration and update
			var config = ConfigurationManager.GetSection("net.vieapps.repositories") as ConfigurationSectionHandler;
			if (config != null)
			{
				// update settings of data sources
				foreach (XmlNode dataSourceNode in config._section.SelectNodes("dataSources/dataSource"))
				{
					var dataSource = DataSource.FromJson(config.GetSettings(dataSourceNode));
					if (!RepositoryMediator.DataSources.ContainsKey(dataSource.Name))
						RepositoryMediator.DataSources.Add(dataSource.Name, dataSource);
				}

				// update settings of repositories
				foreach (XmlNode repositoryNode in config._section.SelectNodes("repository"))
				{
					// update repository
					RepositoryDefinition.Update(config.GetSettings(repositoryNode));

					// update repository entities
					foreach (XmlNode repositoryEntityNode in repositoryNode.SelectNodes("entity"))
						EntityDefinition.Update(config.GetSettings(repositoryEntityNode));
				}

				// schemas & indexes
				if (config._section.Attributes["ensureSchemas"] != null && config._section.Attributes["ensureSchemas"].Value.IsEquals("true"))
					Task.Run(async () =>
					{
						try
						{
							await RepositoryStarter.EnsureSqlSchemasAsync();
						}
						catch { }
					}).ConfigureAwait(false);

				if (config._section.Attributes["ensureIndexes"] != null && config._section.Attributes["ensureIndexes"].Value.IsEquals("true"))
					Task.Run(async () =>
					{
						try
						{
							await RepositoryStarter.EnsureNoSqlIndexesAsync();
						}
						catch { }
					}).ConfigureAwait(false);
			}
			else if (updateConfigFromDisc)
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
						&& !n.Name.IsStartsWith("Newtonsoft") && !n.Name.IsStartsWith("MongoDB") && !n.Name.IsStartsWith("WampSharp")
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
			await Task.Delay(0);
		}
	}

	//  --------------------------------------------------------------------------------------------

	public class ConfigurationSectionHandler : IConfigurationSectionHandler
	{
		internal XmlNode _section = null;

		public object Create(object parent, object configContext, XmlNode section)
		{
			this._section = section;
			return this;
		}

		internal JObject GetSettings(XmlNode node)
		{
			var settings = new JObject();
			foreach (XmlAttribute attribute in node.Attributes)
				settings.Add(new JProperty(attribute.Name, attribute.Value));
			return settings;
		}
	}

}