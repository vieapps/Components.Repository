#region Related components
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Xml;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Dynamic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Information of a data source in the respository 
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Name}, Mode = {Mode}")]
	public class RepositoryDataSource
	{
		public RepositoryDataSource() {}

		#region Properties
		/// <summary>
		/// Gets or sets the name of the data source
		/// </summary>
		public string Name { get; set; }
		/// <summary>
		/// Gets or sets the working mode
		/// </summary>
		public Repository.RepositoryModes Mode { get; set; }
		/// <summary>
		/// Gets or sets the name of the connection string (for working with database)
		/// </summary>
		public string ConnectionStringName { get; set; }
		/// <summary>
		/// Gets or sets the name of the database (for working with database)
		/// </summary>
		public string DatabaseName { get; set; }
		/// <summary>
		/// Gets or sets end-point of CRUD operations (for working with REST)
		/// </summary>
		public string CRUDEndPoint { get; set; }
		/// <summary>
		/// Gets or sets end-point of search operations (for working with REST)
		/// </summary>
		public string SearchEndPoint { get; set; }
		/// <summary>
		/// Gets or sets name of JSON Web Token parameter (for working with REST)
		/// </summary>
		public string TokenName { get; set; }
		/// <summary>
		/// Gets or sets place (Header or QueryString) of JSON Web Token parameter (for working with REST)
		/// </summary>
		public string TokenPlace { get; set; }
		/// <summary>
		/// Gets or sets URI of topic (for working with RPC using WAMP)
		/// </summary>
		public string TopicURI { get; set; }
		#endregion

		#region Parse
		internal static RepositoryDataSource FromJson(JObject settings)
		{
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["name"] == null)
				throw new ArgumentNullException("name", "[name] attribute of settings");
			else if (settings["mode"] == null)
				throw new ArgumentNullException("mode", "[mode] attribute of settings");

			// initialize
			var dataSource = new RepositoryDataSource()
			{
				Name = (settings["name"] as JValue).Value as string,
				Mode = (RepositoryModes)Enum.Parse(typeof(RepositoryModes), (settings["mode"] as JValue).Value as string)
			};

			// name of connection string (SQL and NoSQL)
			if (dataSource.Mode.Equals(RepositoryModes.SQL) || dataSource.Mode.Equals(RepositoryModes.NoSQL))
			{
				if (settings["connectionStringName"] == null)
					throw new ArgumentNullException("connectionStringName", "[connectionStringName] attribute of settings");
				dataSource.ConnectionStringName = (settings["connectionStringName"] as JValue).Value as string;
			}

			// name of database (NoSQL)
			if (dataSource.Mode.Equals(RepositoryModes.NoSQL))
			{
				if (settings["databaseName"] == null)
					throw new ArgumentNullException("databaseName", "[databaseName] attribute of settings");
				dataSource.DatabaseName = (settings["databaseName"] as JValue).Value as string;
			}

			return dataSource;
		}
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Definition of a respository (means a module definition)
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Type.FullName}")]
	public class RepositoryDefinition
	{
		public RepositoryDefinition()
		{
			this.IsAlias = false;
			this.ExtraData = new ExpandoObject();
		}

		#region Properties
		/// <summary>
		/// Gets or sets type of the object for processing
		/// </summary>
		public Type Type { get; set; }
		/// <summary>
		/// Gets the name of the primary data source
		/// </summary>
		public string PrimaryDataSourceName { get; internal set; }
		/// <summary>
		/// Gets the name of the secondary data source
		/// </summary>
		public string SecondaryDataSourceName { get; internal set; }
		/// <summary>
		/// Gets the names of the all data-sources that available for sync
		/// </summary>
		public string SyncDataSourceNames { get; internal set; }
		/// <summary>
		/// Gets the identity of this module definition
		/// </summary>
		public string ID { get; internal set; }
		/// <summary>
		/// Gets the title of this module definition
		/// </summary>
		public string Title { get; internal set; }
		/// <summary>
		/// Gets the description of this module definition
		/// </summary>
		public string Description { get; internal set; }
		/// <summary>
		/// Gets state that specified this is alias of other repository (means alias of other module)
		/// </summary>
		public bool IsAlias { get; internal set; }
		/// <summary>
		/// Gets extra data of the definition
		/// </summary>
		public ExpandoObject ExtraData { get; internal set; }
		/// <summary>
		/// Gets the primary data-source
		/// </summary>
		public RepositoryDataSource PrimaryDataSource
		{
			get
			{
				return !string.IsNullOrWhiteSpace(this.PrimaryDataSourceName) && RepositoryMediator.RepositoryDataSources.ContainsKey(this.PrimaryDataSourceName)
					? RepositoryMediator.RepositoryDataSources[this.PrimaryDataSourceName]
					: null;
			}
		}
		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public RepositoryDataSource SecondaryDataSource
		{
			get
			{
				return !string.IsNullOrWhiteSpace(this.SecondaryDataSourceName) && RepositoryMediator.RepositoryDataSources.ContainsKey(this.SecondaryDataSourceName)
					? RepositoryMediator.RepositoryDataSources[this.SecondaryDataSourceName]
					: null;
			}
		}
		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public List<RepositoryDataSource> SyncDataSources
		{
			get
			{
				if (string.IsNullOrWhiteSpace(this.SyncDataSourceNames))
					return new List<RepositoryDataSource>();
				else
				{
					HashSet<string> dataSources = this.SyncDataSourceNames.ToHashSet(',');
					return RepositoryMediator.RepositoryDataSources
						.Where(item => dataSources.Contains(item.Key))
						.Select(item => item.Value)
						.ToList();
				}
			}
		}
		/// <summary>
		/// Gets the definitions of all entities
		/// </summary>
		public List<RepositoryEntityDefinition> EntityDefinitions
		{
			get
			{
				return RepositoryMediator.RepositoryEntityDefinitions
					.Where(item => item.Value.ParentName.Equals(this.Type.GetTypeName()))
					.Select(item => item.Value)
					.ToList();
			}
		}
		#endregion

		#region Register
		internal static void Register(Type type)
		{
			// check existed
			if (RepositoryMediator.RepositoryDefinitions.ContainsKey(type.GetTypeName()))
				return;

			// check table/collection name
			var info = type.GetCustomAttributes(false)
				.Where(attribute => attribute is RepositoryAttribute)
				.ToList()[0] as RepositoryAttribute;

			// initialize
			var definition = new RepositoryDefinition()
			{
				Type = type,
				ID = !string.IsNullOrWhiteSpace(info.ID) ? info.ID : "",
				Title = !string.IsNullOrWhiteSpace(info.Title) ? info.Title : "",
				Description = !string.IsNullOrWhiteSpace(info.Description) ? info.Description : "",
			};

			// update into collection
			RepositoryMediator.RepositoryDefinitions.Add(type.GetTypeName(), definition);
		}
		#endregion

		#region Update settings
		internal static void Update(JObject settings)
		{
			// check settings
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["type"] == null)
				throw new ArgumentNullException("type", "[type] attribute of settings");

			var isAlias = settings["isAlias"] != null
				? ((settings["isAlias"] as JValue).Value as string).IsEquals("true")
				: false;

			Type targetType = null;
			if (isAlias)
			{
				var target = settings["target"] != null
					? (settings["target"] as JValue).Value as string
					: null;

				if (string.IsNullOrWhiteSpace(target))
					isAlias = false;
				else
				{
					targetType = Type.GetType(target);
					if (targetType == null)
						isAlias = false;
				}
			}

			// check type
			var type = isAlias
				? targetType
				: Type.GetType((settings["type"] as JValue).Value as string);
			if (type == null)
				return;

			// get type name
			var typeName = isAlias
				? (settings["type"] as JValue).Value as string
				: type.GetTypeName();

			// clone definition for new alias
			if (isAlias && !RepositoryMediator.RepositoryDefinitions.ContainsKey(typeName))
			{
				if (!RepositoryMediator.RepositoryDefinitions.ContainsKey(type.GetTypeName()))
					throw new ArgumentException("The target type named '" + type.GetTypeName() + "' is not available");

				RepositoryMediator.RepositoryDefinitions.Add(typeName, RepositoryMediator.RepositoryDefinitions[type.GetTypeName()].Clone());
				RepositoryMediator.RepositoryDefinitions[typeName].IsAlias = true;
				RepositoryMediator.RepositoryDefinitions[typeName].ExtraData = RepositoryMediator.RepositoryDefinitions[type.GetTypeName()].ExtraData;
			}

			// check existing
			if (!RepositoryMediator.RepositoryDefinitions.ContainsKey(typeName))
				return;

			// update
			var data = settings["primaryDataSource"] != null
				? (settings["primaryDataSource"] as JValue).Value as string
				: null;
			if (string.IsNullOrEmpty(data))
				throw new ArgumentNullException("primaryDataSource", "[primaryDataSource] attribute of settings");
			else if (!RepositoryMediator.RepositoryDataSources.ContainsKey(data))
				throw new ArgumentException("The data source named '" + data + "' is not available");
			RepositoryMediator.RepositoryDefinitions[typeName].PrimaryDataSourceName = data;

			data = settings["secondaryDataSource"] != null
				? (settings["secondaryDataSource"] as JValue).Value as string
				: null;
			if (!string.IsNullOrEmpty(data) && !RepositoryMediator.RepositoryDataSources.ContainsKey(data))
				data = null;
			RepositoryMediator.RepositoryDefinitions[typeName].SecondaryDataSourceName = data;

			data = settings["syncDataSources"] != null
				? (settings["syncDataSources"] as JValue).Value as string
				: null;
			if (!string.IsNullOrEmpty(data))
			{
				if (data.Equals(","))
					data = null;
				else
				{
					var names = data.ToArray(',', true, true);
					data = "";
					names.ForEach(name =>
					{
						data += RepositoryMediator.RepositoryDataSources.ContainsKey(name)
							? (!data.Equals("") ? "," : "") + name
							: "";
					});
				}
			}
			RepositoryMediator.RepositoryDefinitions[typeName].SyncDataSourceNames = data;
		}
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Definition of a respository entity (means a content-type definition)
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Type.FullName}")]
	public class RepositoryEntityDefinition
	{
		public RepositoryEntityDefinition()
		{
			this.Attributes = new List<ObjectService.AttributeInfo>();
			this.SortableAttributes = new List<string>() { "ID" };
			this.ExtraData = new ExpandoObject();
		}

		#region Properties
		/// <summary>
		/// Gets the type of the object for processing
		/// </summary>
		public Type Type { get; internal set; }
		/// <summary>
		/// Gets the name of the primary data source
		/// </summary>
		public string PrimaryDataSourceName { get; internal set; }
		/// <summary>
		/// Gets the name of the secondary data source
		/// </summary>
		public string SecondaryDataSourceName { get; internal set; }
		/// <summary>
		/// Gets the names of the all data-sources that available for sync
		/// </summary>
		public string SyncDataSourceNames { get; internal set; }
		/// <summary>
		/// Gets or sets the name of the table in SQL database
		/// </summary>
		public string TableName { get; internal set; }
		/// <summary>
		/// Gets or sets the name of the collection in NoSQL database
		/// </summary>
		public string CollectionName { get; internal set; }
		/// <summary>
		/// Gets or sets the identity of this content-type definition
		/// </summary>
		public string ID { get; internal set; }
		/// <summary>
		/// Gets or sets the title (means name) of this content-type definition
		/// </summary>
		public string Title { get; internal set; }
		/// <summary>
		/// Gets or sets the description of this content-type definition
		/// </summary>
		public string Description { get; internal set; }
		/// <summary>
		/// Gets the type of a static class that contains information of the cache storage for processing caching data
		/// </summary>
		public Type CacheStorageType { get; internal set; }
		/// <summary>
		/// Gets the name of the object in the static class that contains information of the cache storage for processing caching data
		/// </summary>
		public string CacheStorageName { get; internal set; }
		/// <summary>
		/// Gets the cache storage object for processing caching data of this entity
		/// </summary>
		public Caching.CacheManager CacheStorage { get; internal set; }
		/// <summary>
		/// Gets extra data of the definition
		/// </summary>
		public ExpandoObject ExtraData { get; internal set; }
		/// <summary>
		/// Gets the primary data-source
		/// </summary>
		public RepositoryDataSource PrimaryDataSource
		{
			get
			{
				return !string.IsNullOrWhiteSpace(this.PrimaryDataSourceName) && RepositoryMediator.RepositoryDataSources.ContainsKey(this.PrimaryDataSourceName)
					? RepositoryMediator.RepositoryDataSources[this.PrimaryDataSourceName]
					: null;
			}
		}
		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public RepositoryDataSource SecondaryDataSource
		{
			get
			{
				return !string.IsNullOrWhiteSpace(this.SecondaryDataSourceName) && RepositoryMediator.RepositoryDataSources.ContainsKey(this.SecondaryDataSourceName)
					? RepositoryMediator.RepositoryDataSources[this.SecondaryDataSourceName]
					: null;
			}
		}
		/// <summary>
		/// Gets the other data sources that are available for synchronizing
		/// </summary>
		public List<RepositoryDataSource> SyncDataSources
		{
			get
			{
				if (string.IsNullOrWhiteSpace(this.SyncDataSourceNames))
					return new List<RepositoryDataSource>();
				else
				{
					HashSet<string> dataSources = this.SyncDataSourceNames.ToHashSet();
					return RepositoryMediator.RepositoryDataSources
						.Where(item => dataSources.Contains(item.Key))
						.Select(item => item.Value)
						.ToList();
				}
			}
		}
		/// <summary>
		/// Gets the collection of all attributes (properties and fields)
		/// </summary>
		public List<ObjectService.AttributeInfo> Attributes { get; internal set; }
		internal string PrimaryKey { get; set; }
		public ObjectService.AttributeInfo PrimaryKeyInfo
		{
			get
			{
				return this.Attributes.FirstOrDefault(attribute => attribute.Name.Equals(this.PrimaryKey));
			}
		}
		/// <summary>
		/// Gets the collection of sortable properties
		/// </summary>
		public List<string> SortableAttributes { get; internal set; }
		internal string ParentName { get; set; }
		/// <summary>
		/// Gets the definitions of the parent (repository)
		/// </summary>
		public RepositoryDefinition Parent
		{
			get
			{
				return RepositoryMediator.GetRepositoryDefinition(this.ParentName);
			}
		}
		#endregion

		#region Register
		internal static void Register(Type type)
		{
			// check existed
			if (RepositoryMediator.RepositoryEntityDefinitions.ContainsKey(type.GetTypeName()))
				return;

			// check table/collection name
			var info = type.GetCustomAttributes(false).FirstOrDefault(attribute => attribute is RepositoryEntityAttribute) as RepositoryEntityAttribute;
			if (string.IsNullOrWhiteSpace(info.TableName) && string.IsNullOrWhiteSpace(info.CollectionName))
				throw new ArgumentException("The type [" + type.ToString() + "] must have name of SQL table or NoSQL collection");

			// initialize
			var definition = new RepositoryEntityDefinition()
			{
				Type = type,
				TableName = info.TableName,
				CollectionName = info.CollectionName,
				ID = !string.IsNullOrWhiteSpace(info.ID) ? info.ID : "",
				Title = !string.IsNullOrWhiteSpace(info.Title) ? info.Title : "",
				Description = !string.IsNullOrWhiteSpace(info.Description) ? info.Description : "",
				CacheStorageType = info.CacheStorageType,
				CacheStorageName = info.CacheStorageName
			};

			// set cache storage
			if (definition.CacheStorageType != null && !string.IsNullOrWhiteSpace(definition.CacheStorageName))
			{
				var cacheStorage = definition.CacheStorageType.GetStaticObject(definition.CacheStorageName);
				definition.CacheStorage = cacheStorage != null && cacheStorage is Caching.CacheManager
					? cacheStorage as Caching.CacheManager
					: null;
			}

			// parent (module definition)
			var parent = type.BaseType;
			var grandparent = parent.BaseType;
			while (grandparent.BaseType != null && grandparent.BaseType != typeof(object) && grandparent.BaseType != typeof(RepositoryBase))
			{
				parent = parent.BaseType;
				grandparent = parent.BaseType;
			}
			definition.ParentName = parent.GetTypeName();
			definition.ParentName = definition.ParentName.Left(definition.ParentName.IndexOf("[")) + definition.ParentName.Substring(definition.ParentName.IndexOf("]") + 2);

			// public properties
			var numberOfKeys = 0;
			ObjectService.GetProperties(type).ForEach(attribute =>
			{
				if (!attribute.IsIgnored())
				{
					// primary key
					var attributes = attribute.Info.GetCustomAttributes(typeof(PrimaryKeyAttribute), true);
					if (attributes.Length > 0)
					{
						attribute.Column = (attributes[0] as PrimaryKeyAttribute).Column;
						attribute.NotNull = true;
						attribute.MaxLength = (attributes[0] as PrimaryKeyAttribute).MaxLength;
						attribute.IsCLOB = false;
						attribute.IsDateTimeString = false;

						definition.PrimaryKey = attribute.Name;
						numberOfKeys += attributes.Length;
					}

					// property
					attributes = attribute.Info.GetCustomAttributes(typeof(PropertyAttribute), true);
					if (attributes.Length > 0)
					{
						attribute.Column = (attributes[0] as PropertyAttribute).Column;
						attribute.NotNull = (attributes[0] as PropertyAttribute).NotNull;
						attribute.MaxLength = (attributes[0] as PropertyAttribute).MaxLength;
						attribute.IsCLOB = (attributes[0] as PropertyAttribute).IsCLOB;
					}

					// date-time string
					attribute.IsDateTimeString = attribute.Info.GetCustomAttributes(typeof(DateTimeStringAttribute), true).Length > 0;

					// sortable
					attributes = attribute.Info.GetCustomAttributes(typeof(SortableAttribute), true);
					if (attributes.Length > 0)
						definition.SortableAttributes.Add(attribute.Name);

					// update
					definition.Attributes.Add(attribute);
				}
			});

			// check primary key
			if (numberOfKeys == 0)
				throw new ArgumentException("The type [" + type.ToString() + "] has no primary-key");
			else if (numberOfKeys > 1)
				throw new ArgumentException("The type [" + type.ToString() + "] has multiple primary-keys");

			// private attributes (fields)
			ObjectService.GetFields(type).ForEach(attribute =>
			{
				var attributes = attribute.Info.GetCustomAttributes(typeof(FieldAttribute), false);
				if (attributes.Length > 0)
				{
					attribute.Column = (attributes[0] as FieldAttribute).Column;
					attribute.NotNull = (attributes[0] as FieldAttribute).NotNull;
					attribute.MaxLength = (attributes[0] as FieldAttribute).MaxLength;
					attribute.IsCLOB = (attributes[0] as FieldAttribute).IsCLOB;
					definition.Attributes.Add(attribute);
				}
			});

			// update into collection
			RepositoryMediator.RepositoryEntityDefinitions.Add(type.GetTypeName(), definition);
		}
		#endregion

		#region Update settings
		internal static void Update(JObject settings)
		{
			// check settings
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["type"] == null)
				throw new ArgumentNullException("type", "[type] attribute of settings");

			// check type
			var type = Type.GetType((settings["type"] as JValue).Value as string);
			if (type == null)
				return;

			var typeName = type.GetTypeName();
			if (!RepositoryMediator.RepositoryEntityDefinitions.ContainsKey(typeName))
				return;

			// update
			var data = settings["primaryDataSource"] != null
				? (settings["primaryDataSource"] as JValue).Value as string
				: null;
			if (!string.IsNullOrEmpty(data) && !RepositoryMediator.RepositoryDataSources.ContainsKey(data))
				data = null;
			RepositoryMediator.RepositoryEntityDefinitions[typeName].PrimaryDataSourceName = data;

			data = settings["secondaryDataSource"] != null
				? (settings["secondaryDataSource"] as JValue).Value as string
				: null;
			if (!string.IsNullOrEmpty(data) && !RepositoryMediator.RepositoryDataSources.ContainsKey(data))
				data = null;
			RepositoryMediator.RepositoryEntityDefinitions[typeName].SecondaryDataSourceName = data;

			data = settings["syncDataSources"] != null
				? (settings["syncDataSources"] as JValue).Value as string
				: null;
			if (!string.IsNullOrEmpty(data))
			{
				if (data.Equals(","))
					data = null;
				else
				{
					var names = data.ToArray(',', true, true);
					data = "";
					names.ForEach(name =>
					{
						data += RepositoryMediator.RepositoryDataSources.ContainsKey(name)
							? (!data.Equals("") ? "," : "") + name
							: "";
					});
				}
			}
			RepositoryMediator.RepositoryEntityDefinitions[typeName].SyncDataSourceNames = data;

			// individual cache storage
			if (settings["cacheRegion"] != null)
			{
				var cacheRegion = (settings["cacheRegion"] as JValue).Value as string;
				var cacheExpirationType = settings["cacheExpirationType"] != null
					? (settings["cacheExpirationType"] as JValue).Value as string
					: "Sliding";
				var cacheExpirationTime = 30;
				if (settings["cacheExpirationTime"] != null)
					try
					{
						cacheExpirationTime = Convert.ToInt32((settings["cacheExpirationTime"] as JValue).Value);
						if (cacheExpirationTime < 0)
							cacheExpirationTime = 30;
					}
					catch { }
				var cacheActiveSynchronize = settings["cacheActiveSynchronize"] != null
					? ((settings["cacheActiveSynchronize"] as JValue).Value as string).IsEquals("true")
					: false;
				RepositoryMediator.RepositoryEntityDefinitions[typeName].CacheStorage = new Caching.CacheManager(cacheRegion, cacheExpirationType.IsEquals("absolute") ? "Absolute" : "Sliding", cacheExpirationTime, cacheActiveSynchronize);
			}
		}

		/// <summary>
		/// Sets the cache storage of an entity definition
		/// </summary>
		/// <param name="type">The type that presents information of an entity definition</param>
		/// <param name="cacheStorage">The cache storage</param>
		public void SetCacheStorage(Type type, Caching.CacheManager cacheStorage)
		{
			if (type != null && RepositoryMediator.RepositoryEntityDefinitions.ContainsKey(type.GetTypeName()))
				RepositoryMediator.RepositoryEntityDefinitions[type.GetTypeName()].CacheStorage = cacheStorage;
		}
		#endregion

	}

}