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
					.Where(type => type.IsDefined(typeof(RepositoryEntityAttribute), false))
					.ForEach(type =>
					{
						RepositoryEntityDefinition.Register(type);
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
		public static void Initialize(IEnumerable<Assembly> assemblies)
		{
			assemblies.ForEach(assembly =>
			{
				RepositoryStarter.Initialize(assembly);
			});
		}

		/// <summary>
		/// Initializes all types of all assemblies in the current app domain (so please call this method one time only)
		/// </summary>
		public static void Initialize()
		{
			// initialize & register all types
			AppDomain.CurrentDomain.GetAssemblies().ForEach(assembly =>
			{
				RepositoryStarter.Initialize(assembly);
			});

			// read configuration and update
			var configurationHandler = ConfigurationManager.GetSection("net.vieapps.repositories") as ConfigurationSectionHandler;
			if (configurationHandler == null)
				throw new ConfigurationErrorsException("Cannot find the configuration section named 'net.vieapps.repositories' in the configuration file");

			// update settings of data sources
			foreach (XmlNode dataSourceNode in configurationHandler._section.SelectNodes("dataSources/dataSource"))
			{
				RepositoryDataSource dataSource = RepositoryDataSource.FromJson(configurationHandler.GetSettings(dataSourceNode));
				RepositoryMediator.RepositoryDataSources.Add(dataSource.Name, dataSource);
			}

			// update settings of repositories
			foreach (XmlNode repositoryNode in configurationHandler._section.SelectNodes("repository"))
			{
				// update repository
				RepositoryDefinition.Update(configurationHandler.GetSettings(repositoryNode));

				// update repository entities
				foreach (XmlNode repositoryEntityNode in repositoryNode.SelectNodes("entity"))
					RepositoryEntityDefinition.Update(configurationHandler.GetSettings(repositoryEntityNode));
			}
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