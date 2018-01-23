#region Related components
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Dynamic;
using System.Data;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MongoDB.Bson.Serialization.Attributes;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Definition of a respository
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Type.FullName}")]
	public class RepositoryDefinition
	{
		public RepositoryDefinition() { }

		#region Properties
		/// <summary>
		/// Gets the type of the class that responsibility to process data of the repository
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
		/// Gets the name of the data source for storing information of versioning contents
		/// </summary>
		public string VersionDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the name of the data source for storing information of trash contents
		/// </summary>
		public string TrashDataSourceName { get; internal set; }

		/// <summary>
		/// Gets that state that specified this repository is an alias of other repository
		/// </summary>
		public bool IsAlias { get; internal set; } = false;

		/// <summary>
		/// Gets that state that specified data of this repository is sync automatically between data sources
		/// </summary>
		public bool AutoSync { get; internal set; } = false;

		/// <summary>
		/// Gets the extra-settings of the repository
		/// </summary>
		public Dictionary<string, object> ExtraSettings { get; internal set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		#endregion

		#region Properties [Helpers]
		/// <summary>
		/// Gets the primary data-source
		/// </summary>
		public DataSource PrimaryDataSource
		{
			get
			{
				return RepositoryMediator.GetDataSource(this.PrimaryDataSourceName);
			}
		}

		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public DataSource SecondaryDataSource
		{
			get
			{
				return RepositoryMediator.GetDataSource(this.SecondaryDataSourceName);
			}
		}

		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public List<DataSource> SyncDataSources
		{
			get
			{
				return string.IsNullOrWhiteSpace(this.SyncDataSourceNames)
					? new List<DataSource>()
					: this.SyncDataSourceNames.ToList()
						.Distinct(StringComparer.OrdinalIgnoreCase)
						.Select(name => RepositoryMediator.GetDataSource(name))
						.Where(dataSource => dataSource != null)
						.ToList();
			}
		}

		/// <summary>
		/// Gets the data-source that use to store versioning contents
		/// </summary>
		public DataSource VersionDataSource
		{
			get
			{
				return RepositoryMediator.GetDataSource(this.VersionDataSourceName);
			}
		}

		/// <summary>
		/// Gets the data-source that use to store trash contents
		/// </summary>
		public DataSource TrashDataSource
		{
			get
			{
				return RepositoryMediator.GetDataSource(this.TrashDataSourceName);
			}
		}

		/// <summary>
		/// Gets the definitions of all entities
		/// </summary>
		public List<EntityDefinition> EntityDefinitions
		{
			get
			{
				return RepositoryMediator.EntityDefinitions
					.Where(item => item.Value.RepositoryType.Equals(this.Type))
					.Select(item => item.Value)
					.ToList();
			}
		}
		#endregion

		#region Properties [Module Definition]
		/// <summary>
		/// Gets the identity (when this object is defined as a module definition)
		/// </summary>
		public string ID { get; internal set; }

		/// <summary>
		/// Gets the path to folder that contains all UI files (when this object is defined as a module definition)
		/// </summary>
		public string Path { get; internal set; }

		/// <summary>
		/// Gets the title (when this object is defined as a module definition)
		/// </summary>
		public string Title { get; internal set; }

		/// <summary>
		/// Gets the description (when this object is defined as a module definition)
		/// </summary>
		public string Description { get; internal set; }

		/// <summary>
		/// Gets the name of the SQL table for storing extended properties, default is 'T_Data_Extended_Properties' (when this object is defined as a module definition)
		/// </summary>
		public string ExtendedPropertiesTableName { get; internal set; }

		/// <summary>
		/// Gets the collection of business repositories at run-time (means business modules at run-time)
		/// </summary>
		public Dictionary<string, IRepository> RuntimeRepositories { get; internal set; } = new Dictionary<string, IRepository>(StringComparer.OrdinalIgnoreCase);
		#endregion

		#region Register
		internal static void Register(Type type)
		{
			// check
			if (type == null || RepositoryMediator.RepositoryDefinitions.ContainsKey(type))
				return;

			// initialize
			var info = type.GetCustomAttributes(false)
				.Where(attribute => attribute is RepositoryAttribute)
				.ToList()[0] as RepositoryAttribute;

			var definition = new RepositoryDefinition()
			{
				Type = type,
				ID = !string.IsNullOrWhiteSpace(info.ID) ? info.ID : "",
				Path = !string.IsNullOrWhiteSpace(info.Path) ? info.Path : "",
				Title = !string.IsNullOrWhiteSpace(info.Title) ? info.Title : "",
				Description = !string.IsNullOrWhiteSpace(info.Description) ? info.Description : "",
				ExtendedPropertiesTableName = !string.IsNullOrWhiteSpace(info.ExtendedPropertiesTableName) ? info.ExtendedPropertiesTableName : "T_Data_Extended_Properties"
			};

			// update into collection
			RepositoryMediator.RepositoryDefinitions.Add(type, definition);
		}
		#endregion

		#region Update settings
		internal static void Update(JObject settings, Action<string, Exception> tracker = null)
		{
			// check
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["type"] == null)
				throw new ArgumentNullException("type", "[type] attribute of settings");

			// prepare
			var isAlias = settings["isAlias"] != null
				? "true".IsEquals((settings["isAlias"] as JValue).Value as string)
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

			var type = isAlias
				? targetType
				: Type.GetType((settings["type"] as JValue).Value as string);

			// no type is found
			if (type == null)
				return;

			// clone definition for new alias
			if (isAlias)
			{
				if (!RepositoryMediator.RepositoryDefinitions.ContainsKey(type))
					throw new ArgumentException("The target type named '" + type.GetTypeName() + "' is not available");

				RepositoryMediator.RepositoryDefinitions.Add(type, RepositoryMediator.RepositoryDefinitions[type].Clone());
				RepositoryMediator.RepositoryDefinitions[type].IsAlias = true;
				RepositoryMediator.RepositoryDefinitions[type].ExtraSettings = RepositoryMediator.RepositoryDefinitions[type].ExtraSettings;
			}

			// check existing
			else if (!RepositoryMediator.RepositoryDefinitions.ContainsKey(type))
				return;

			// update
			var data = settings["primaryDataSource"] != null
				? (settings["primaryDataSource"] as JValue).Value as string
				: null;
			if (string.IsNullOrEmpty(data))
				throw new ArgumentNullException("primaryDataSource", "[primaryDataSource] attribute of settings");
			else if (!RepositoryMediator.DataSources.ContainsKey(data))
				throw new ArgumentException("The data source named '" + data + "' is not available");
			RepositoryMediator.RepositoryDefinitions[type].PrimaryDataSourceName = data;

			data = settings["secondaryDataSource"] != null
				? (settings["secondaryDataSource"] as JValue).Value as string
				: null;
			RepositoryMediator.RepositoryDefinitions[type].SecondaryDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			RepositoryMediator.RepositoryDefinitions[type].SyncDataSourceNames = settings["syncDataSources"] != null
				? (settings["syncDataSources"] as JValue).Value as string
				: null;

			data = settings["versionDataSource"] != null
				? (settings["versionDataSource"] as JValue).Value as string
				: null;
			RepositoryMediator.RepositoryDefinitions[type].VersionDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			data = settings["trashDataSource"] != null
				? (settings["trashDataSource"] as JValue).Value as string
				: null;
			RepositoryMediator.RepositoryDefinitions[type].TrashDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			RepositoryMediator.RepositoryDefinitions[type].AutoSync = "true".IsEquals(settings["autoSync"] != null ? (settings["autoSync"] as JValue).Value as string : "false");

			tracker?.Invoke(
				$"Update settings of repository [{type.GetTypeName()}]:" + "\r\n" +
				$"- Primary data source: {(RepositoryMediator.RepositoryDefinitions[type].PrimaryDataSource != null ? $"{RepositoryMediator.RepositoryDefinitions[type].PrimaryDataSource.Name} ({RepositoryMediator.RepositoryDefinitions[type].PrimaryDataSource.Mode})"  : "None")}" + "\r\n" +
				$"- Secondary data source: {(RepositoryMediator.RepositoryDefinitions[type].SecondaryDataSource != null ? $"{RepositoryMediator.RepositoryDefinitions[type].SecondaryDataSource.Name} ({RepositoryMediator.RepositoryDefinitions[type].SecondaryDataSource.Mode})" : "None")}" + "\r\n" +
				$"- Sync data sources: {(RepositoryMediator.RepositoryDefinitions[type].SyncDataSources.Count > 0 ? RepositoryMediator.RepositoryDefinitions[type].SyncDataSources.Select(dataSource => $"{dataSource.Name} ({dataSource.Mode})").ToString(", ") : "None")}" + "\r\n" +
				$"- Version data source: {RepositoryMediator.RepositoryDefinitions[type].VersionDataSource?.Name ?? "None"}" + "\r\n" +
				$"- Trash data source: {RepositoryMediator.RepositoryDefinitions[type].TrashDataSource?.Name ?? "None"}" + "\r\n" +
				$"- Auto sync: {RepositoryMediator.RepositoryDefinitions[type].AutoSync}"
				, null);
		}
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Definition of an entity in a respository
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Type.FullName}")]
	public class EntityDefinition
	{
		public EntityDefinition() { }

		#region Properties
		/// <summary>
		/// Gets the type of the class that responsibility to process data of the repository entity
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
		/// Gets the name of the data source for storing information of versioning contents
		/// </summary>
		public string VersionDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the name of the data source for storing information of trash contents
		/// </summary>
		public string TrashDataSourceName { get; internal set; }

		/// <summary>
		/// Gets or sets the name of the table in SQL database
		/// </summary>
		public string TableName { get; internal set; }

		/// <summary>
		/// Gets or sets the name of the collection in NoSQL database
		/// </summary>
		public string CollectionName { get; internal set; }

		/// <summary>
		/// Gets or sets the state that specifies this entity is able to search using full-text method
		/// </summary>
		public bool Searchable { get; internal set; } = true;

		/// <summary>
		/// Gets the state to create new version when an entity object is updated
		/// </summary>
		public bool CreateNewVersionWhenUpdated { get; internal set; } = true;

		/// <summary>
		/// Gets that state that specified data of this repository entity is sync automatically between data sources
		/// </summary>
		public bool AutoSync { get; internal set; } = false;

		/// <summary>
		/// Gets extra settings of of the entity definition
		/// </summary>
		public Dictionary<string, object> ExtraSettings { get; internal set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		#endregion

		#region Properties [Helpers]
		/// <summary>
		/// Gets the caching object for processing with caching data of this entity
		/// </summary>
		public Caching.Cache Cache { get; internal set; }

		/// <summary>
		/// Gets the primary data-source
		/// </summary>
		public DataSource PrimaryDataSource
		{
			get
			{
				return RepositoryMediator.GetDataSource(this.PrimaryDataSourceName);
			}
		}

		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public DataSource SecondaryDataSource
		{
			get
			{
				return RepositoryMediator.GetDataSource(this.SecondaryDataSourceName);
			}
		}

		/// <summary>
		/// Gets the other data sources that are available for synchronizing
		/// </summary>
		public List<DataSource> SyncDataSources
		{
			get
			{
				return !string.IsNullOrWhiteSpace(this.SyncDataSourceNames)
					? this.SyncDataSourceNames.ToList()
						.Distinct(StringComparer.OrdinalIgnoreCase)
						.Select(name => RepositoryMediator.GetDataSource(name))
						.Where(dataSource => dataSource != null)
						.ToList()
					: null;
			}
		}

		/// <summary>
		/// Gets the data-source that use to store versioning contents
		/// </summary>
		public DataSource VersionDataSource
		{
			get
			{
				return RepositoryMediator.GetDataSource(this.VersionDataSourceName);
			}
		}

		/// <summary>
		/// Gets the data-source that use to store trash contents
		/// </summary>
		public DataSource TrashDataSource
		{
			get
			{
				return RepositoryMediator.GetDataSource(this.TrashDataSourceName);
			}
		}

		/// <summary>
		/// Gets the collection of all attributes (properties and fields)
		/// </summary>
		public List<AttributeInfo> Attributes { get; internal set; } = new List<AttributeInfo>();

		internal string PrimaryKey { get; set; }

		/// <summary>
		/// Gets the information of the primary key
		/// </summary>
		public AttributeInfo PrimaryKeyInfo
		{
			get
			{
				return this.Attributes.FirstOrDefault(attribute => attribute.Name.Equals(this.PrimaryKey));
			}
		}

		/// <summary>
		/// Gets the collection of sortable properties
		/// </summary>
		public List<string> SortableAttributes { get; internal set; } = new List<string>() { "ID" };

		/// <summary>
		/// Gets the type of the class that presents the repository of this repository entity
		/// </summary>
		public Type RepositoryType { get; internal set; }

		/// <summary>
		/// Gets the repository definition of this defintion
		/// </summary>
		public RepositoryDefinition RepositoryDefinition
		{
			get
			{
				return RepositoryMediator.GetRepositoryDefinition(this.RepositoryType);
			}
		}
		#endregion

		#region Properties [Content-Type Definition]
		/// <summary>
		/// Gets the identity (when this object is defined as a content-type definition)
		/// </summary>
		public string ID { get; internal set; }

		/// <summary>
		/// Gets the title (when this object is defined as a content-type definition)
		/// </summary>
		public string Title { get; internal set; }

		/// <summary>
		/// Gets the description (when this object is defined as a content-type definition)
		/// </summary>
		public string Description { get; internal set; }

		/// <summary>
		/// Gets the state that allow to use multiple instances, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool MultipleIntances { get; internal set; } = false;

		/// <summary>
		/// Gets the state that allow to extend this entity by extended properties, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool Extendable { get; internal set; } = false;

		/// <summary>
		/// Gets or sets the state that specifies this entity is able to index with global search module, default is true (when this object is defined as a content-type definition)
		/// </summary>
		public bool Indexable { get; internal set; } = true;

		/// <summary>
		/// Gets the type of parent entity definition (when this object is defined as a content-type definition)
		/// </summary>
		public Type ParentType { get; internal set; }

		/// <summary>
		/// Gets the name of the property that use to associate with parent object (when this object is defined as a content-type definition)
		/// </summary>
		public string ParentAssociatedProperty { get; internal set; }

		/// <summary>
		/// Gets the state that specifies this entity had multiple associates with parent object, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool MultipleParentAssociates { get; internal set; } = false;

		/// <summary>
		/// Gets the name of the property that use to store the information of multiple associates with parent, mus be List or HashSet (when this object is defined as a content-type definition)
		/// </summary>
		public string MultipleParentAssociatesProperty { get; internal set; }

		/// <summary>
		/// Gets the name of the SQL table that use to store the information of multiple associates with parent (when this object is defined as a content-type definition)
		/// </summary>
		public string MultipleParentAssociatesTable { get; internal set; }

		/// <summary>
		/// Gets the name of the column of SQL table that use to map the associate with parent (when this object is defined as a content-type definition)
		/// </summary>
		public string MultipleParentAssociatesMapColumn { get; internal set; }

		/// <summary>
		/// Gets the name of the column of SQL table that use to link the associate with this entity (when this object is defined as a content-type definition)
		/// </summary>
		public string MultipleParentAssociatesLinkColumn { get; internal set; }

		/// <summary>
		/// Gets the name of the property to use as short-name (when this object is defined as a content-type definition)
		/// </summary>
		public string ShortnameProperty { get; internal set; }

		/// <summary>
		/// Gets the type of a class that use to generate navigator menu (when this object is defined as a content-type definition)
		/// </summary>
		public Type NavigatorType { get; internal set; }

		/// <summary>
		/// Gets the collection of business entities at run-time (means business conten-types at run-time)
		/// </summary>
		public Dictionary<string, IRepositoryEntity> RuntimeEntities { get; internal set; } = new Dictionary<string, IRepositoryEntity>(StringComparer.OrdinalIgnoreCase);
		#endregion

		#region Register
		internal static void Register(Type type)
		{
			// check existed
			if (type == null || RepositoryMediator.EntityDefinitions.ContainsKey(type))
				return;

			// check table/collection name
			var info = type.GetCustomAttributes(false).FirstOrDefault(attribute => attribute is EntityAttribute) as EntityAttribute;
			if (string.IsNullOrWhiteSpace(info.TableName) && string.IsNullOrWhiteSpace(info.CollectionName))
				throw new ArgumentException("The type [" + type.ToString() + "] must have name of SQL table or NoSQL collection");

			// initialize
			var definition = new EntityDefinition()
			{
				Type = type,
				TableName = info.TableName,
				CollectionName = info.CollectionName,
				Searchable = info.Searchable,
				ID = !string.IsNullOrWhiteSpace(info.ID) ? info.ID : "",
				Title = !string.IsNullOrWhiteSpace(info.Title) ? info.Title : "",
				Description = !string.IsNullOrWhiteSpace(info.Description) ? info.Description : "",
				MultipleIntances = info.MultipleIntances,
				Extendable = info.Extendable,
				Indexable = info.Indexable,
				ParentType = info.ParentType,
				CreateNewVersionWhenUpdated = info.CreateNewVersionWhenUpdated,
				NavigatorType = info.NavigatorType
			};

			// cache
			if (info.CacheClass != null && !string.IsNullOrWhiteSpace(info.CacheName))
			{
				var cache = info.CacheClass.GetStaticObject(info.CacheName);
				definition.Cache = cache != null && cache is Caching.Cache
					? cache as Caching.Cache
					: null;
			}

			// parent (repository)
			var parent = type.BaseType;
			var grandparent = parent.BaseType;
			while (grandparent.BaseType != null && grandparent.BaseType != typeof(object) && grandparent.BaseType != typeof(RepositoryBase))
			{
				parent = parent.BaseType;
				grandparent = parent.BaseType;
			}
			var typename = parent.GetTypeName();
			definition.RepositoryType = Type.GetType(typename.Left(typename.IndexOf("[")) + typename.Substring(typename.IndexOf("]") + 2));

			// public properties
			var numberOfKeys = 0;
			var properties = ObjectService.GetProperties(type);
			properties.Where(attr => !attr.IsIgnored()).ForEach(attr =>
			{
				// create
				var attribute = new AttributeInfo(attr);

				// primary key
				var attributes = attribute.Info.GetCustomAttributes(typeof(PrimaryKeyAttribute), true);
				if (attributes.Length > 0)
				{
					attribute.Column = (attributes[0] as PrimaryKeyAttribute).Column;
					attribute.NotNull = attribute.NotEmpty = true;
					attribute.MaxLength = (attributes[0] as PrimaryKeyAttribute).MaxLength;
					attribute.IsCLOB = false;

					definition.PrimaryKey = attribute.Name;
					numberOfKeys += attributes.Length;
				}

				// property
				attributes = attribute.Info.GetCustomAttributes(typeof(PropertyAttribute), true);
				if (attributes.Length > 0)
				{
					attribute.Column = (attributes[0] as PropertyAttribute).Column;
					attribute.NotNull = (attributes[0] as PropertyAttribute).NotNull;
					attribute.NotEmpty = (attributes[0] as PropertyAttribute).NotEmpty;
					attribute.MaxLength = (attributes[0] as PropertyAttribute).MaxLength;
					attribute.IsCLOB = (attributes[0] as PropertyAttribute).IsCLOB;
				}

				// sortable
				if (attribute.Info.GetCustomAttributes(typeof(SortableAttribute), true).Length > 0)
					definition.SortableAttributes.Add(attribute.Name);

				// update
				definition.Attributes.Add(attribute);
			});

			// check primary key
			if (numberOfKeys == 0)
				throw new ArgumentException("The type [" + type.ToString() + "] has no primary-key");
			else if (numberOfKeys > 1)
				throw new ArgumentException("The type [" + type.ToString() + "] has multiple primary-keys");

			// fields
			ObjectService.GetFields(type)
				.Where(attr => !attr.IsIgnored())
				.ForEach(attr =>
				{
					// create
					var attribute = new AttributeInfo(attr);

					// update info
					var attributes = attribute.Info.GetCustomAttributes(typeof(FieldAttribute), false);
					if (attributes.Length > 0)
					{
						attribute.Column = (attributes[0] as FieldAttribute).Column;
						attribute.NotNull = (attributes[0] as FieldAttribute).NotNull;
						attribute.NotEmpty = (attributes[0] as FieldAttribute).NotEmpty;
						attribute.MaxLength = (attributes[0] as FieldAttribute).MaxLength;
						attribute.IsCLOB = (attributes[0] as FieldAttribute).IsCLOB;
						definition.Attributes.Add(attribute);
					}
				});

			// parent (entity)
			var parentEntity = definition.ParentType != null
				? definition.ParentType.CreateInstance()
				: null;

			if (parentEntity != null)
			{
				var property = string.IsNullOrWhiteSpace(info.ParentAssociatedProperty)
					? null
					: properties.FirstOrDefault(p => p.Name.Equals(info.ParentAssociatedProperty));
				definition.ParentAssociatedProperty = property != null
					? info.ParentAssociatedProperty
					: "";

				if (!string.IsNullOrWhiteSpace(definition.ParentAssociatedProperty))
				{
					definition.MultipleParentAssociates = info.MultipleParentAssociates;

					property = string.IsNullOrWhiteSpace(info.MultipleParentAssociatesProperty)
						? null
						: properties.FirstOrDefault(p => p.Name.Equals(info.MultipleParentAssociatesProperty));
					definition.MultipleParentAssociatesProperty = property != null && property.Type.IsGenericListOrHashSet()
						? info.MultipleParentAssociatesProperty
						: "";

					definition.MultipleParentAssociatesTable = !string.IsNullOrWhiteSpace(info.MultipleParentAssociatesTable)
						? info.MultipleParentAssociatesTable
						: "";

					if (!string.IsNullOrWhiteSpace(definition.MultipleParentAssociatesTable))
					{
						definition.MultipleParentAssociatesMapColumn = !string.IsNullOrWhiteSpace(info.MultipleParentAssociatesMapColumn)
							? info.MultipleParentAssociatesMapColumn
							: "";

						definition.MultipleParentAssociatesLinkColumn = !string.IsNullOrWhiteSpace(info.MultipleParentAssociatesLinkColumn)
							? info.MultipleParentAssociatesLinkColumn
							: "";
					}
				}
			}
			else
				definition.ParentType = null;

			// shortname
			definition.ShortnameProperty = !string.IsNullOrWhiteSpace(info.ShortnameProperty) && properties.FirstOrDefault(p => p.Name.Equals(info.ShortnameProperty)) != null
				? info.ShortnameProperty
				: "";

			// navigator
			definition.NavigatorType = definition.NavigatorType != null && definition.NavigatorType.CreateInstance() != null
				? definition.NavigatorType
				: null;

			// update into collection
			RepositoryMediator.EntityDefinitions.Add(type, definition);
		}
		#endregion

		#region Update settings
		internal static void Update(JObject settings, Action<string, Exception> tracker = null)
		{
			// check
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["type"] == null)
				throw new ArgumentNullException("type", "[type] attribute of settings");

			// prepare
			var type = Type.GetType((settings["type"] as JValue).Value as string);
			if (type == null || !RepositoryMediator.EntityDefinitions.ContainsKey(type))
				return;

			// update
			var data = settings["primaryDataSource"] != null
				? (settings["primaryDataSource"] as JValue).Value as string
				: null;
			RepositoryMediator.EntityDefinitions[type].PrimaryDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			data = settings["secondaryDataSource"] != null
				? (settings["secondaryDataSource"] as JValue).Value as string
				: null;
			RepositoryMediator.EntityDefinitions[type].SecondaryDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			RepositoryMediator.EntityDefinitions[type].SyncDataSourceNames = settings["syncDataSources"] != null
				? (settings["syncDataSources"] as JValue).Value as string
				: null;

			data = settings["versionDataSource"] != null
				? (settings["versionDataSource"] as JValue).Value as string
				: null;
			RepositoryMediator.EntityDefinitions[type].VersionDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			data = settings["trashDataSource"] != null
				? (settings["trashDataSource"] as JValue).Value as string
				: null;
			RepositoryMediator.EntityDefinitions[type].TrashDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			// individual caching storage
			if (settings["cacheRegion"] != null)
			{
				var cacheRegion = (settings["cacheRegion"] as JValue).Value as string;
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
				var cacheProvider = settings["cacheProvider"] != null
					? (settings["cacheProvider"] as JValue).Value as string
					: null;
				RepositoryMediator.EntityDefinitions[type].Cache = new Caching.Cache(cacheRegion, cacheExpirationTime, cacheActiveSynchronize, cacheProvider);
			}

			RepositoryMediator.EntityDefinitions[type].AutoSync = settings["autoSync"] != null
				? "true".IsEquals((settings["autoSync"] as JValue).Value as string)
				: RepositoryMediator.EntityDefinitions[type].RepositoryDefinition.AutoSync;

			tracker?.Invoke(
				$"Update settings of repository entity [{type.GetTypeName()}]:" + "\r\n" +
				$"- Primary data source: {(RepositoryMediator.EntityDefinitions[type].PrimaryDataSource != null ? $"{RepositoryMediator.EntityDefinitions[type].PrimaryDataSource.Name} ({RepositoryMediator.EntityDefinitions[type].PrimaryDataSource.Mode})" : "None")}" + "\r\n" +
				$"- Secondary data source: {(RepositoryMediator.EntityDefinitions[type].SecondaryDataSource != null ? $"{RepositoryMediator.EntityDefinitions[type].SecondaryDataSource.Name} ({RepositoryMediator.EntityDefinitions[type].SecondaryDataSource.Mode})" : "None")}" + "\r\n" +
				$"- Sync data sources: {(RepositoryMediator.EntityDefinitions[type].SyncDataSources != null && RepositoryMediator.EntityDefinitions[type].SyncDataSources.Count > 0 ? RepositoryMediator.EntityDefinitions[type].SyncDataSources.Select(dataSource => $"{dataSource.Name} ({dataSource.Mode})").ToString(", ") : "None")}" + "\r\n" +
				$"- Version data source: {RepositoryMediator.EntityDefinitions[type].VersionDataSource?.Name ?? "None"}" + "\r\n" +
				$"- Trash data source: {RepositoryMediator.EntityDefinitions[type].TrashDataSource?.Name ?? "None"}" + "\r\n" +
				$"- Auto sync: {RepositoryMediator.EntityDefinitions[type].AutoSync}"
				, null);
		}

		/// <summary>
		/// Sets the cache storage of an entity definition
		/// </summary>
		/// <param name="type">The type that presents information of an entity definition</param>
		/// <param name="cache">The cache storage</param>
		public void SetCacheStorage(Type type, Caching.Cache cache)
		{
			if (type != null && RepositoryMediator.EntityDefinitions.ContainsKey(type))
				RepositoryMediator.EntityDefinitions[type].Cache = cache;
		}
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents information of an attribute of an entity
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Name}")]
	public class AttributeInfo : ObjectService.AttributeInfo
	{
		public AttributeInfo() : this(null) { }

		public AttributeInfo(ObjectService.AttributeInfo derived) : base(derived?.Name, derived?.Info) { }

		#region Properties
		public bool NotNull { get; internal set; } = false;

		public bool NotEmpty { get; internal set; } = false;

		public string Column { get; internal set; }

		public int MaxLength { get; internal set; } = 0;

		public bool IsCLOB { get; internal set; } = false;
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Information of a definition of extended property in a respository 
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Name}, Mode = {Mode}")]
	public sealed class ExtendedPropertyDefinition
	{
		public ExtendedPropertyDefinition(JObject json = null)
		{
			this.Name = "";
			this.Mode = ExtendedPropertyMode.SmallText;
			this.Column = "";
			this.DefaultValue = "";
			this.DefaultValueFormula = "";
			if (json != null)
				this.CopyFrom(json);
		}

		#region Properties
		/// <summary>
		/// Gets or sets the name
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the mode
		/// </summary>
		public ExtendedPropertyMode Mode { get; set; }

		/// <summary>
		/// Gets or sets the name of column for storing data (when repository mode is SQL)
		/// </summary>
		public string Column { get; set; }

		/// <summary>
		/// Gets or sets the default value
		/// </summary>
		public string DefaultValue { get; set; }

		/// <summary>
		/// Gets or sets the formula for computing default value
		/// </summary>
		public string DefaultValueFormula { get; set; }
		#endregion

		#region Properties [Helper]
		/// <summary>
		/// Gets the type of this property
		/// </summary>
		[JsonIgnore, BsonIgnore, XmlIgnore]
		internal Type Type
		{
			get
			{
				switch (this.Mode)
				{
					case ExtendedPropertyMode.YesNo:
					case ExtendedPropertyMode.Number:
						return typeof(Int32);

					case ExtendedPropertyMode.Decimal:
						return typeof(Decimal);

					case ExtendedPropertyMode.DateTime:
						return typeof(DateTime);
				}
				return typeof(String);
			}
		}

		/// <summary>
		/// Gets the database-type of this property
		/// </summary>
		[JsonIgnore, BsonIgnore, XmlIgnore]
		internal DbType DbType
		{
			get
			{
				switch (this.Mode)
				{
					case ExtendedPropertyMode.YesNo:
					case ExtendedPropertyMode.Number:
						return typeof(Int32).GetDbType();


					case ExtendedPropertyMode.Decimal:
						return typeof(Decimal).GetDbType();

					case ExtendedPropertyMode.DateTime:
						return DbType.AnsiString;
				}
				return typeof(String).GetDbType();
			}
		}
		#endregion

		#region Parse XML (backward compatible)
		/*
		public void Parse(XElement element)
		{
			try
			{
				var node = element.Element(XName.Get("Mode"));
				var mode = node != null ? node.Value : "";

				node = element.Element(XName.Get("Name"));
				this.Name = node != null ? node.Value : this.Name;

				node = element.Element(XName.Get("Column"));
				this.Column = node != null ? node.Value : this.Column;

				node = element.Element(XName.Get("DefaultValue"));
				this.DefaultValue = node != null ? node.Value : this.DefaultValue;

				node = element.Element(XName.Get("Label"));
				this.UI.Label = node != null ? node.Value : this.UI.Label;

				node = element.Element(XName.Get("Description"));
				this.UI.Description = node != null ? node.Value : this.UI.Description;

				node = element.Element(XName.Get("Required"));
				this.UI.Required = node != null ? node.Value.IsEquals("true") : this.UI.Required;

				node = element.Element(XName.Get("MaxLength"));
				this.UI.MaxLength = node != null ? node.Value.CastType<int>() : this.UI.MaxLength;

				switch (mode)
				{
					// text
					case "0":
					case "1":
						this.Mode = CustomPropertyMode.Text;
						this.UI.AllowMultiple = mode.Equals("1");
						break;

					// text (large)
					case "2":
						this.Mode = CustomPropertyMode.LargeText;
						this.UI.UseAsTextEditor = true;
						node = element.Element(XName.Get("TextEditorWidth"));
						this.UI.TextEditorWidth = node != null ? node.Value : this.UI.TextEditorWidth;
						node = element.Element(XName.Get("TextEditorHeight"));
						this.UI.TextEditorHeight = node != null ? node.Value : this.UI.TextEditorHeight;
						break;

					// yes/no
					case "3":
						this.Mode = CustomPropertyMode.YesNo;
						this.DefaultValue = "Yes";
						this.UI.PredefinedValues = "Yes\nNo";
						break;

					// choice
					case "4":
					case "5":
						this.Mode = CustomPropertyMode.Choice;
						this.UI.AllowMultiple = mode.Equals("5");
						node = element.Element(XName.Get("ChoiceStyle"));
						if (mode.Equals("4"))
							this.UI.Format = node != null && node.Value.Equals("0") ? "Radio" : "Select";
						else
							this.UI.Format = node != null && node.Value.Equals("0") ? "Checkbox" : "Listbox";
						node = element.Element(XName.Get("ChoiceValues"));
						this.UI.PredefinedValues = node != null ? node.Value : this.UI.PredefinedValues;
						break;

					// date-time
					case "6":
						this.Mode = CustomPropertyMode.DateTime;
						node = element.Element(XName.Get("DateTimeValueFormat"));
						this.UI.Format = node != null ? node.Value : "dd/MM/yyyy";
						break;

					// number (integer)
					case "7":
					case "8":
						this.Mode = CustomPropertyMode.Number;
						node = element.Element(XName.Get("NumberMinValue"));
						this.UI.MinValue = node != null && node.Value.CastType<int>() > -1 ? node.Value.CastType<int>().ToString() : this.UI.MinValue;
						node = element.Element(XName.Get("NumberMaxValue"));
						this.UI.MaxValue = node != null && node.Value.CastType<int>() > -1 ? node.Value.CastType<int>().ToString() : this.UI.MaxValue;
						node = element.Element(XName.Get("DisplayFormat"));
						this.UI.Format = node != null ? node.Value : this.UI.Format;
						break;

					// number (decimal)
					case "9":
						this.Mode = CustomPropertyMode.Decimal;
						node = element.Element(XName.Get("DisplayFormat"));
						this.UI.Format = node != null ? node.Value : this.UI.Format;
						break;

					// hyper-link
					case "10":
						this.Mode = CustomPropertyMode.HyperLink;
						break;

					// look-up
					case "11":
						this.Mode = CustomPropertyMode.Lookup;
						node = element.Element(XName.Get("LookupModuleId"));
						this.UI.LookupRepositoryID = node != null ? node.Value : this.UI.LookupRepositoryID;
						node = element.Element(XName.Get("LookupContentTypeId"));
						this.UI.LookupEntityID = node != null ? node.Value : this.UI.LookupEntityID;
						node = element.Element(XName.Get("LookupColumn"));
						this.UI.LookupProperty = node != null ? node.Value : this.UI.LookupProperty;
						node = element.Element(XName.Get("LookupMultiple"));
						this.UI.AllowMultiple = node != null ? node.Value.IsEquals("true") : this.UI.AllowMultiple;
						break;

					// user
					case "12":
						this.Mode = CustomPropertyMode.User;
						node = element.Element(XName.Get("LookupMultiple"));
						this.UI.AllowMultiple = node != null ? node.Value.IsEquals("true") : this.UI.AllowMultiple;
						break;

					default:
						this.Mode = CustomPropertyMode.Text;
						this.UI.MaxLength = 250;
						this.UI.UseAsTextEditor = true;
						break;
				}
			}
			catch { }
		}
		*/
		#endregion

		#region Name validation
		static HashSet<string> ReservedWords = "ExtendedProperties,Add,External,Procedure,All,Fetch,Public,Alter,File,RaisError,And,FillFactor,Read,Any,For,ReadText,As,Foreign,ReConfigure,Asc,FreeText,References,Authorization,FreeTextTable,Replication,Backup,From,Restore,Begin,Full,Restrict,Between,Function,Return,Break,Goto,Revert,Browse,Grant,Revoke,Bulk,Group,Right,By,Having,Rollback,Cascade,Holdlock,Rowcount,Case,RowGuidCol,Check,Identity,Insert,Rule,Checkpoint,Identitycol,Save,Close,If,Schema,Clustered,In,SecurityAudit,Coalesce,Index,Select,Collate,Inner,SemanticKeyPhraseTable,Column,SemanticSimilarityDetailsTable,Commit,Intersect,SemanticSimilarityTable,Compute,Into,Session,User,Constraint,Is,Set,Contains,Join,Setuser,ContainsTable,Key,Shutdown,Continue,Kill,Some,Convert,Left,Statistics,Create,Like,System,Cross,Lineno,Table,Current,Load,TableSample,Current_Date,Current_Time,Current_Timestamp,Merge,TextSize,National,Then,NoCheck,To,Current_User,NonClustered,Top,Cursor,Not,Tran,Database,Null,Transaction,Dbcc,NullIf,Trigger,Deallocate,Of,Truncate,Declare,Off,Try_Convert,Default,Offsets,Tsequal,Delete,On,Union,Deny,Open,Unique,Desc,OpenDataSource,Unpivot,Disk,Openquery,Update,Distinct,OpenRowset,UpdateText,Distributed,OpenXml,Use,Double,Option,User,Drop,Or,Values,Dump,Order,Varying,Else,Outer,View,End,Over,Waitfor,Errlvl,Percent,When,Escape,Pivot,Where,Except,Plan,While,Exec,Precision,With,Execute,Primary,Exists,Print,WriteText,Exit,Proc".ToLower().ToHashSet();

		/// <summary>
		/// Validates the name of a custom property definition
		/// </summary>
		/// <param name="name">The string that presents name of a custom property</param>
		/// <remarks>An exception will be thrown if the name is invalid</remarks>
		public static void Validate(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentNullException("name", "The name is null or empty");

			var validName = name.GetANSIUri().Replace("-", "");
			if (!validName.Equals(name))
				throw new InformationInvalidException("The name is contains one or more invalid characters (like space, -, +, ...)");

			if (name.ToUpper()[0] < 'A' || name.ToUpper()[0] > 'Z')
				throw new InformationInvalidException("The name must starts with a letter");

			if (ExtendedPropertyDefinition.ReservedWords.Contains(name.ToLower()))
				throw new InformationInvalidException("The name is system reserved word");
		}

		/// <summary>
		/// Validates the name of a custom property definition
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name">The string that presents name of a custom property</param>
		/// <remarks>An exception will be thrown if the name is invalid</remarks>
		public static void Validate<T>(string name) where T : class
		{
			ExtendedPropertyDefinition.Validate(name);
			var attributes = ObjectService.GetProperties(RepositoryMediator.GetEntityDefinition<T>().Type).ToDictionary(info => info.Name.ToLower(), info => info.Name);
			if (attributes.ContainsKey(name.ToLower()))
				throw new InformationInvalidException("The name is already used");
		}
		#endregion

		#region Helper methods
		public object GetDefaultValue()
		{
			return null;
		}

		public override string ToString()
		{
			return this.ToJson().ToString(Newtonsoft.Json.Formatting.None);
		}
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// UI definition for working with a custom property in a respository 
	/// </summary>
	[Serializable]
	public sealed class ExtendedUIDefinition
	{
		public ExtendedUIDefinition(JObject json = null)
		{
			this.Controls = new List<ExtendedUIControlDefinition>();
			this.EditXslt = "";
			this.ListXslt = "";
			this.ViewXslt = "";
			if (json != null)
				this.CopyFrom(json);
		}

		#region Properties
		public List<ExtendedUIControlDefinition> Controls { get; set; }

		public string EditXslt { get; set; }

		public string ListXslt { get; set; }

		public string ViewXslt { get; set; }
		#endregion

		public override string ToString()
		{
			return this.ToJson().ToString(Newtonsoft.Json.Formatting.None);
		}

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// UI definition of a custom property in a respository 
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Name}")]
	public sealed class ExtendedUIControlDefinition
	{
		public ExtendedUIControlDefinition(JObject json = null)
		{
			this.Name = "";
			this.Label = "";
			this.PlaceHolder = "";
			this.Description = "";
			this.CssClass = "";
			this.CssMinWidth = "";
			this.CssMinHeight = "";
			this.MaxLength = 0;
			this.MinValue = "";
			this.MaxValue = "";
			this.UseAsTextEditor = false;
			this.TextEditorWidth = "";
			this.TextEditorHeight = "";
			this.Format = "";
			this.PredefinedValues = "";
			this.LookupRepositoryID = "";
			this.LookupEntityID = "";
			this.LookupProperty = "";
			this.Shown = true;
			this.Required = false;
			this.AllowMultiple = false;
			if (json != null)
				this.CopyFrom(json);
		}

		#region Properties
		/// <summary>
		/// Gets or sets the name
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the label
		/// </summary>
		public string Label { get; set; }

		/// <summary>
		/// Gets or sets the place-holder
		/// </summary>
		public string PlaceHolder { get; set; }

		/// <summary>
		/// Gets or sets the description
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Gets or sets the CSS class name
		/// </summary>
		public string CssClass { get; set; }

		/// <summary>
		/// Gets or sets the CSS min-width
		/// </summary>
		public string CssMinWidth { get; set; }

		/// <summary>
		/// Gets or sets the CSS min-height
		/// </summary>
		public string CssMinHeight { get; set; }

		/// <summary>
		/// Gets or sets the max-length of input control or max-length of text (when the property is text)
		/// </summary>
		public int MaxLength { get; set; }

		/// <summary>
		/// Gets or sets the state that mark this property is use text-editor (when the property is large text)
		/// </summary>
		public bool UseAsTextEditor { get; set; }

		/// <summary>
		/// Gets or sets the width of text-editor (when the property is large text and marks to use text editor)
		/// </summary>
		public string TextEditorWidth { get; set; }

		/// <summary>
		/// Gets or sets the height of text-editor (when the property is large text and marks to use text editor)
		/// </summary>
		public string TextEditorHeight { get; set; }

		/// <summary>
		/// Gets or sets the minimum value (when the property is number or date-time)
		/// </summary>
		public string MinValue { get; set; }

		/// <summary>
		/// Gets or sets the maximum value (when the property is number or date-time)
		/// </summary>
		public string MaxValue { get; set; }

		/// <summary>
		/// Gets or sets the displaying format (when the property is number/date-time or choice)
		/// </summary>
		public string Format { get; set; }

		/// <summary>
		/// Gets or sets the pre-defined values (when the property is choice) - seperated by new line (LF)
		/// </summary>
		public string PredefinedValues { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business repository (when the property is lookup)
		/// </summary>
		public string LookupRepositoryID { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business entity (when the property is lookup)
		/// </summary>
		public string LookupEntityID { get; set; }

		/// <summary>
		/// Gets or sets the name of business entity's property for displaying (when the property is lookup)
		/// </summary>
		public string LookupProperty { get; set; }

		/// <summary>
		/// Gets or sets the state that mark this property is shown for inputing
		/// </summary>
		public bool Shown { get; set; }

		/// <summary>
		/// Gets or sets the state that mark this property is required
		/// </summary>
		public bool Required { get; set; }

		/// <summary>
		/// Gets or sets the state that allow multiple values (when the property is text, choice or lookup)
		/// </summary>
		public bool AllowMultiple { get; set; }
		#endregion

		public override string ToString()
		{
			return this.ToJson().ToString(Newtonsoft.Json.Formatting.None);
		}
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Information of a data source
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Name}, Mode = {Mode}")]
	public class DataSource
	{
		public DataSource() { }

		#region Properties
		/// <summary>
		/// Gets the name of the data source
		/// </summary>
		public string Name { get; internal set; }

		/// <summary>
		/// Gets the working mode
		/// </summary>
		public Repository.RepositoryMode Mode { get; internal set; }

		/// <summary>
		/// Gets the name of the connection string (for working with database server)
		/// </summary>
		public string ConnectionStringName { get; internal set; }

		/// <summary>
		/// Gets or sets the connection string (for working with database server)
		/// </summary>
		public string ConnectionString { get; set; }

		/// <summary>
		/// Gets the name of the database (for working with database server)
		/// </summary>
		public string DatabaseName { get; internal set; }
		#endregion

		#region Helper methods
		internal static DataSource FromJson(JObject settings)
		{
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["name"] == null)
				throw new ArgumentNullException("name", "[name] attribute of settings");
			else if (settings["mode"] == null)
				throw new ArgumentNullException("mode", "[mode] attribute of settings");

			// initialize
			var dataSource = new DataSource()
			{
				Name = (settings["name"] as JValue).Value as string,
				Mode = ((settings["mode"] as JValue).Value as string).ToEnum<RepositoryMode>()
			};

			// name of connection string
			if (settings["connectionStringName"] == null)
				throw new ArgumentNullException("connectionStringName", "[connectionStringName] attribute of settings");
			dataSource.ConnectionStringName = (settings["connectionStringName"] as JValue).Value as string;

			// connection string
			dataSource.ConnectionString = settings["connectionString"] != null
				? (settings["connectionString"] as JValue).Value as string
				: null;

			// name of database
			if (settings["databaseName"] != null)
				dataSource.DatabaseName = (settings["databaseName"] as JValue).Value as string;
			else if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
				throw new ArgumentNullException("databaseName", "[databaseName] attribute of settings");

			return dataSource;
		}
		#endregion

	}

}