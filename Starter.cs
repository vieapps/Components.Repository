#region Related components
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Xml;
using System.Linq;
using System.Reflection;

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
			RepositoryStarter.Initialize(selector != null ? selector() : AppDomain.CurrentDomain.GetAssemblies());
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