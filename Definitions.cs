#region Related components
using System;
using System.Data;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using System.Dynamic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using MongoDB.Bson.Serialization.Attributes;
using net.vieapps.Components.Utility;
using net.vieapps.Components.Caching;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Presents a respository definition (means a module definition)
	/// </summary>
	[DebuggerDisplay("Name = {Type?.FullName}")]
	public class RepositoryDefinition
	{
		public RepositoryDefinition()
			: this(null) { }

		public RepositoryDefinition(Type type)
			=> this.Type = type;

		#region Properties
		/// <summary>
		/// Gets the type of the class that responsibility to process data of the repository
		/// </summary>
		public Type Type { get; }

		/// <summary>
		/// Gets the name of the primary data source
		/// </summary>
		public string PrimaryDataSourceName { get; private set; }

		/// <summary>
		/// Gets the primary data-source
		/// </summary>
		public DataSource PrimaryDataSource => RepositoryMediator.GetDataSource(this.PrimaryDataSourceName);

		/// <summary>
		/// Gets the name of the secondary data source
		/// </summary>
		public string SecondaryDataSourceName { get; private set; }

		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public DataSource SecondaryDataSource => RepositoryMediator.GetDataSource(this.SecondaryDataSourceName);

		/// <summary>
		/// Gets the names of the all data-sources that available for sync
		/// </summary>
		public string SyncDataSourceNames { get; private set; }

		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public List<DataSource> SyncDataSources
			=> string.IsNullOrWhiteSpace(this.SyncDataSourceNames)
				? new List<DataSource>()
				: this.SyncDataSourceNames.ToList()
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.Select(name => RepositoryMediator.GetDataSource(name))
					.Where(dataSource => dataSource != null)
					.ToList();

		/// <summary>
		/// Gets the name of the data source for storing information of versioning contents
		/// </summary>
		public string VersionDataSourceName { get; private set; }

		/// <summary>
		/// Gets the data-source that use to store versioning contents
		/// </summary>
		public DataSource VersionDataSource => RepositoryMediator.GetDataSource(this.VersionDataSourceName);

		/// <summary>
		/// Gets the name of the data source for storing information of trash contents
		/// </summary>
		public string TrashDataSourceName { get; private set; }

		/// <summary>
		/// Gets the data-source that use to store trash contents
		/// </summary>
		public DataSource TrashDataSource => RepositoryMediator.GetDataSource(this.TrashDataSourceName);

		/// <summary>
		/// Gets that state that specified this repository is an alias of other repository
		/// </summary>
		public bool IsAlias { get; private set; } = false;

		/// <summary>
		/// Gets that state that specified data of this repository is sync automatically between data sources
		/// </summary>
		public bool AutoSync { get; private set; } = false;

		/// <summary>
		/// Gets the extra information of the repository definition
		/// </summary>
		public Dictionary<string, object> Extras { get; private set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Gets the definitions of all entities
		/// </summary>
		public List<EntityDefinition> EntityDefinitions => RepositoryMediator.EntityDefinitions.Where(kvp => kvp.Value.RepositoryDefinitionType.Equals(this.Type)).Select(kvp => kvp.Value).ToList();
		#endregion

		#region Properties [Module Definition]
		/// <summary>
		/// Gets the name of the service that associates with the repository (when this object is defined as a module definition)
		/// </summary>
		public string ServiceName => this.Type?.GetCustomAttribute<RepositoryAttribute>(false)?.ServiceName;

		/// <summary>
		/// Gets the identity (when this object is defined as a module definition)
		/// </summary>
		public string ID => this.Type?.GetCustomAttribute<RepositoryAttribute>(false)?.ID;

		/// <summary>
		/// Gets the title (when this object is defined as a module definition)
		/// </summary>
		public string Title => this.Type?.GetCustomAttribute<RepositoryAttribute>(false)?.Title;

		/// <summary>
		/// Gets the description (when this object is defined as a module definition)
		/// </summary>
		public string Description => this.Type?.GetCustomAttribute<RepositoryAttribute>(false)?.Description;

		/// <summary>
		/// Gets or Sets the name of the icon for working with user interfaces (when this object is defined as a module definition)
		/// </summary>
		public string Icon => this.Type?.GetCustomAttribute<RepositoryAttribute>(false)?.Icon;

		/// <summary>
		/// Gets or Sets the name of the directory that contains all files for working with user interfaces (when this object is defined as a module definition - will be placed in directory named '/themes/modules/', the value of 'ServiceName' will be used if no value was provided)
		/// </summary>
		public string Directory => this.Type?.GetCustomAttribute<RepositoryAttribute>(false)?.Directory;

		/// <summary>
		/// Gets the name of the SQL table for storing extended properties, default is 'T_Data_Extended_Properties' (when this object is defined as a module definition)
		/// </summary>
		public string ExtendedPropertiesTableName
		{
			get
			{
				var tableName = this.Type?.GetCustomAttribute<RepositoryAttribute>(false)?.ExtendedPropertiesTableName;
				return string.IsNullOrWhiteSpace(tableName) ? "T_Data_Extended_Properties" : tableName.Trim();
			}
		}

		/// <summary>
		/// Gets the collection of business repositories (means business modules at run-time)
		/// </summary>
		public ConcurrentDictionary<string, IBusinessRepository> BusinessRepositories { get; } = new ConcurrentDictionary<string, IBusinessRepository>(StringComparer.OrdinalIgnoreCase);
		#endregion

		#region Register & Update settings
		internal static void Register(Type type, Action<string, Exception> tracker = null)
		{
			// check
			if (type == null || RepositoryMediator.RepositoryDefinitions.ContainsKey(type) || type.GetCustomAttribute<RepositoryAttribute>(false) == null)
				return;

			// initialize & register
			var definition = new RepositoryDefinition(type);
			if (RepositoryMediator.RepositoryDefinitions.TryAdd(type, definition))
			{
				tracker?.Invoke($"The repository definition was registered [{definition.Type.GetTypeName()}{(string.IsNullOrWhiteSpace(definition.Title) ? "" : $" => {definition.Title}")}]", null);
				if (tracker == null && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"The repository definition was registered [{definition.Type.GetTypeName()}{(string.IsNullOrWhiteSpace(definition.Title) ? "" : $" => {definition.Title}")}]");
			}
		}

		internal static void Update(JObject settings, Action<string, Exception> tracker = null)
		{
			// check
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["type"] == null)
				throw new ArgumentNullException("type", "[type] attribute of settings");

			// prepare
			Type aliasOf = null;
			var isAlias = settings["isAlias"] != null && "true".IsEquals((settings["isAlias"] as JValue).Value as string);
			if (isAlias)
			{
				var targetType = settings["target"] != null
					? (settings["target"] as JValue).Value as string
					: null;

				if (string.IsNullOrWhiteSpace(targetType))
					isAlias = false;
				else
				{
					aliasOf = Enyim.Caching.AssemblyLoader.GetType(targetType);
					if (aliasOf == null)
						isAlias = false;
				}
			}

			var typeStr = (settings["type"] as JValue).Value as string;
			var type = Enyim.Caching.AssemblyLoader.GetType(typeStr);
			if (type == null)
			{
				tracker?.Invoke($"The type of repository definition is not found [{typeStr}]", null);
				if (tracker == null && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"The type of repository definition is not found [{typeStr}]");
				return;
			}

			// clone definition for new alias
			if (isAlias)
			{
				if (!RepositoryMediator.RepositoryDefinitions.TryGetValue(aliasOf, out var targetDef))
					throw new InformationInvalidException($"The target type named '{aliasOf.GetTypeName()}' is not available");
				RepositoryMediator.RepositoryDefinitions.Add(type, RepositoryMediator.RepositoryDefinitions[aliasOf].Clone(cloneDef =>
				{
					cloneDef.IsAlias = true;
					cloneDef.Extras = targetDef.Extras.Clone();
				}));
			}

			// get definition
			var definition = RepositoryMediator.GetRepositoryDefinition(type);
			if (definition == null)
			{
				tracker?.Invoke($"The repository definition was not registered [{type}]", null);
				if (tracker == null && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"The repository definition was not registered [{type}]");
				return;
			}

			// update
			var data = settings["primaryDataSource"] != null
				? (settings["primaryDataSource"] as JValue).Value as string
				: null;
			if (string.IsNullOrEmpty(data))
				throw new ArgumentNullException("primaryDataSource", "[primaryDataSource] attribute of settings");
			else if (!RepositoryMediator.DataSources.ContainsKey(data))
				throw new InformationInvalidException($"The data source named '{data}' is not available");
			definition.PrimaryDataSourceName = data;

			data = settings["secondaryDataSource"] != null
				? (settings["secondaryDataSource"] as JValue).Value as string
				: null;
			definition.SecondaryDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			definition.SyncDataSourceNames = settings["syncDataSources"] != null
				? (settings["syncDataSources"] as JValue).Value as string
				: null;

			data = settings["versionDataSource"] != null
				? (settings["versionDataSource"] as JValue).Value as string
				: null;
			definition.VersionDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			data = settings["trashDataSource"] != null
				? (settings["trashDataSource"] as JValue).Value as string
				: null;
			definition.TrashDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			definition.AutoSync = "true".IsEquals(settings["autoSync"] != null ? (settings["autoSync"] as JValue).Value as string : "false");

			tracker?.Invoke(
				$"Settings of a repository was updated [{definition.Type.GetTypeName()}{(string.IsNullOrWhiteSpace(definition.Title) ? "" : $" => {definition.Title}")}]" + "\r\n" +
				$"- Primary data source: {(definition.PrimaryDataSource != null ? $"{definition.PrimaryDataSource.Name} ({definition.PrimaryDataSource.Mode})" : "None")}" + "\r\n" +
				$"- Secondary data source: {(definition.SecondaryDataSource != null ? $"{definition.SecondaryDataSource.Name} ({definition.SecondaryDataSource.Mode})" : "None")}" + "\r\n" +
				$"- Sync data sources: {(definition.SyncDataSources.Count > 0 ? definition.SyncDataSources.Select(dataSource => $"{dataSource.Name} ({dataSource.Mode})").ToString(", ") : "None")}" + "\r\n" +
				$"- Version data source: {definition.VersionDataSource?.Name ?? "(None)"}" + "\r\n" +
				$"- Trash data source: {definition.TrashDataSource?.Name ?? "(None)"}" + "\r\n" +
				$"- Auto sync: {definition.AutoSync}"
			, null);
		}
		#endregion

		#region Register/Unregister business repositories
		/// <summary>
		/// Registers a business repository (means a business module at run-time)
		/// </summary>
		/// <param name="businessRepository"></param>
		public bool Register(IBusinessRepository businessRepository)
		{
			if (businessRepository != null && !string.IsNullOrWhiteSpace(businessRepository.ID))
			{
				var existed = this.BusinessRepositories.ContainsKey(businessRepository.ID);
				this.BusinessRepositories[businessRepository.ID] = businessRepository;
				if (!existed && RepositoryMediator.IsTraceEnabled)
					RepositoryMediator.WriteLogs($"A business repository (a run-time business module) was registered\r\n{businessRepository.ToJson()}");
				return true;
			}
			return false;
		}

		/// <summary>
		/// Unregisters a business repository (means a business module at run-time)
		/// </summary>
		/// <param name="businessRepositoryID"></param>
		public bool Unregister(string businessRepositoryID)
			=> !string.IsNullOrWhiteSpace(businessRepositoryID) && this.BusinessRepositories.Remove(businessRepositoryID);

		/// <summary>
		/// Unregisters a business repository (means a business module at run-time)
		/// </summary>
		/// <param name="businessRepository"></param>
		public bool Unregister(IBusinessRepository businessRepository)
		{
			var success = this.Unregister(businessRepository?.ID);
			if (success && RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"A business repository (a run-time business module) was uregistered\r\n{businessRepository?.ToJson()}");
			return success;
		}
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a repository entity definition (means a content-type definition)
	/// </summary>
	[DebuggerDisplay("Name = {Type?.FullName}")]
	public class EntityDefinition
	{
		public EntityDefinition()
			: this(null) { }

		public EntityDefinition(Type type)
			=> this.Type = type;

		#region Properties
		/// <summary>
		/// Gets the type of the class that responsibility to process data of the repository entity
		/// </summary>
		public Type Type { get; }

		/// <summary>
		/// Gets the name of the primary data source
		/// </summary>
		public string PrimaryDataSourceName { get; private set; }

		/// <summary>
		/// Gets the primary data-source
		/// </summary>
		public DataSource PrimaryDataSource => RepositoryMediator.GetDataSource(this.PrimaryDataSourceName);

		/// <summary>
		/// Gets the name of the secondary data source
		/// </summary>
		public string SecondaryDataSourceName { get; private set; }

		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public DataSource SecondaryDataSource => RepositoryMediator.GetDataSource(this.SecondaryDataSourceName);

		/// <summary>
		/// Gets the names of the all data-sources that available for sync
		/// </summary>
		public string SyncDataSourceNames { get; private set; }

		/// <summary>
		/// Gets the other data sources that are available for synchronizing
		/// </summary>
		public List<DataSource> SyncDataSources => !string.IsNullOrWhiteSpace(this.SyncDataSourceNames)
			? this.SyncDataSourceNames.ToList()
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Select(name => RepositoryMediator.GetDataSource(name))
				.Where(dataSource => dataSource != null)
				.ToList()
			: null;

		/// <summary>
		/// Gets the name of the data source for storing information of versioning contents
		/// </summary>
		public string VersionDataSourceName { get; private set; }

		/// <summary>
		/// Gets the data-source that use to store versioning contents
		/// </summary>
		public DataSource VersionDataSource => RepositoryMediator.GetDataSource(this.VersionDataSourceName);

		/// <summary>
		/// Gets the name of the data source for storing information of trash contents
		/// </summary>
		public string TrashDataSourceName { get; private set; }

		/// <summary>
		/// Gets the data-source that use to store trash contents
		/// </summary>
		public DataSource TrashDataSource => RepositoryMediator.GetDataSource(this.TrashDataSourceName);

		/// <summary>
		/// Gets or Sets the name of the table in SQL database
		/// </summary>
		public string TableName => this.Type?.GetCustomAttribute<EntityAttribute>(false)?.TableName;

		/// <summary>
		/// Gets or Sets the name of the collection in NoSQL database
		/// </summary>
		public string CollectionName => this.Type?.GetCustomAttribute<EntityAttribute>(false)?.CollectionName;

		/// <summary>
		/// Gets or Sets the state that specifies this entity is able to search using full-text method
		/// </summary>
		public bool Searchable
		{
			get
			{
				var info = this.Type?.GetCustomAttribute<EntityAttribute>(false);
				return info != null && info.Searchable;
			}
		}

		/// <summary>
		/// Gets the state to create new version when a repository entity object is updated
		/// </summary>
		public bool CreateNewVersionWhenUpdated
		{
			get
			{
				var info = this.Type?.GetCustomAttribute<EntityAttribute>(false);
				return info != null && info.CreateNewVersionWhenUpdated;
			}
		}

		/// <summary>
		/// Gets that state that specified data of this repository entity is sync automatically between data sources
		/// </summary>
		public bool AutoSync { get; private set; } = false;

		/// <summary>
		/// Gets the extra information of the repository entity definition
		/// </summary>
		public Dictionary<string, object> Extras { get; private set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Gets the collection of all available attributes (properties and fields)
		/// </summary>
		public List<AttributeInfo> Attributes { get; } = new List<AttributeInfo>();

		/// <summary>
		/// Gets the collection of all available attributes (properties and fields) for working with forms
		/// </summary>
		public List<AttributeInfo> FormAttributes { get; } = new List<AttributeInfo>();

		/// <summary>
		/// Gets the name of primary key property
		/// </summary>
		internal string PrimaryKey { get; set; }

		/// <summary>
		/// Gets the information of the primary key
		/// </summary>
		public AttributeInfo PrimaryKeyInfo => this.Attributes.FirstOrDefault(attribute => attribute.Name.Equals(this.PrimaryKey));

		/// <summary>
		/// Gets the collection of sortable properties
		/// </summary>
		public List<string> SortableAttributes { get; } = new List<string> { "ID" };

		/// <summary>
		/// Gets the caching object for processing with caching data of this entity
		/// </summary>
		public ICache Cache { get; private set; }

		/// <summary>
		/// Gets the type of the type that presents the repository definition of this repository entity definition
		/// </summary>
		public Type RepositoryDefinitionType { get; private set; }

		/// <summary>
		/// Gets the repository definition of this repository entity definition
		/// </summary>
		public RepositoryDefinition RepositoryDefinition => this.RepositoryDefinitionType?.GetRepositoryDefinition();
		#endregion

		#region Properties [Content-Type Definition]
		/// <summary>
		/// Gets the name of the service's object that associates with the entity (when this object is defined as a content-type definition)
		/// </summary>
		public string ObjectName => this.Type?.GetCustomAttribute<EntityAttribute>(false)?.ObjectName ?? this.Type?.GetTypeName(true);

		/// <summary>
		/// Gets the name prefix of the service's object that associates with the entity (when this object is defined as a content-type definition)
		/// </summary>
		public string ObjectNamePrefix => this.Type?.GetCustomAttribute<EntityAttribute>(false)?.ObjectNamePrefix;

		/// <summary>
		/// Gets the name suffix of the service's object that associates with the entity (when this object is defined as a content-type definition)
		/// </summary>
		public string ObjectNameSuffix => this.Type?.GetCustomAttribute<EntityAttribute>(false)?.ObjectNameSuffix;

		/// <summary>
		/// Gets the identity (when this object is defined as a content-type definition)
		/// </summary>
		public string ID => this.Type?.GetCustomAttribute<EntityAttribute>(false)?.ID;

		/// <summary>
		/// Gets the title (when this object is defined as a content-type definition)
		/// </summary>
		public string Title => this.Type?.GetCustomAttribute<EntityAttribute>(false)?.Title;

		/// <summary>
		/// Gets the description (when this object is defined as a content-type definition)
		/// </summary>
		public string Description => this.Type?.GetCustomAttribute<EntityAttribute>(false)?.Description;

		/// <summary>
		/// Gets or Sets the name of the icon for working with user interfaces (when this object is defined as a content-type definition)
		/// </summary>
		public string Icon => this.Type?.GetCustomAttribute<EntityAttribute>(false)?.Icon;

		/// <summary>
		/// Gets the state that allow to use multiple instances, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool MultipleIntances
		{
			get
			{
				var info = this.Type?.GetCustomAttribute<EntityAttribute>(false);
				return info != null && info.MultipleIntances;
			}
		}

		/// <summary>
		/// Gets or Sets the state that specifies this entity is able to index with global search module, default is true (when this object is defined as a content-type definition)
		/// </summary>
		public bool Indexable
		{
			get
			{
				var info = this.Type?.GetCustomAttribute<EntityAttribute>(false);
				return info != null && info.Indexable;
			}
		}

		/// <summary>
		/// Gets the state that allow to extend this entity by extended properties, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool Extendable
		{
			get
			{
				var info = this.Type?.GetCustomAttribute<EntityAttribute>(false);
				return info != null && info.Extendable;
			}
		}

		/// <summary>
		/// Gets the state to specify that entity got some instances of portlet
		/// </summary>
		public bool Portlets
		{
			get
			{
				var info = this.Type?.GetCustomAttribute<EntityAttribute>(false);
				return info != null &&!string.IsNullOrWhiteSpace(info.ID) && info.Portlets;
			}
		}

		/// <summary>
		/// Gets the type of parent entity definition (when this object is defined as a content-type definition)
		/// </summary>
		public Type ParentType => this.GetParentMappingAttribute()?.GetCustomAttribute<ParentMappingAttribute>()?.Type;

		/// <summary>
		/// Gets the collection of business repository entities (means business conten-types at run-time)
		/// </summary>
		public ConcurrentDictionary<string, IBusinessRepositoryEntity> BusinessRepositoryEntities { get; } = new ConcurrentDictionary<string, IBusinessRepositoryEntity>(StringComparer.OrdinalIgnoreCase);
		#endregion

		#region Register & Update settings
		internal static void Register(Type type, Action<string, Exception> tracker = null)
		{
			// check
			if (type == null || RepositoryMediator.EntityDefinitions.ContainsKey(type) || type.GetCustomAttribute<EntityAttribute>(false) == null)
				return;

			// initialize & verify the name of table/collection
			var definition = new EntityDefinition(type);
			if (string.IsNullOrWhiteSpace(definition.TableName) && string.IsNullOrWhiteSpace(definition.CollectionName))
				throw new InformationRequiredException($"The type [{type}] must have name of SQL table or NoSQL collection");

			// verify the mappings
			var properties = type.GetPublicAttributes();

			foreach (var attribute in properties.Where(attribute => attribute.IsMappings()))
			{
				if (!attribute.IsGenericListOrHashSet() || !attribute.GetGenericTypeArguments().First().Equals(typeof(string)))
					throw new InformationInvalidException($"The attribute [{attribute.Name}] must be list or hash-set of string for using as mapping");
			}

			var parentMappings = properties.Where(attribute => attribute.IsParentMapping()).ToList();
			if (parentMappings.Count > 0)
			{
				if (parentMappings.Count > 1)
					throw new InformationInvalidException($"The type [{type}] got multiple mappings with parent entity definition");
				else if (parentMappings.First().Type == null)
					throw new InformationRequiredException($"The attribute [{parentMappings.First().Name}] must have the type of parent entity definition");
			}
			else if (properties.Any(attribute => attribute.IsMultipleParentMappings()))
				throw new InformationInvalidException($"The type [{type}] got multiple parent mappings but got no information of the parent entity definition");

			// verify the alias
			var aliasProperties = properties.Where(attribute => attribute.IsAlias()).ToList();
			if (aliasProperties.Count > 0)
			{
				if (aliasProperties.Count > 1)
					throw new InformationInvalidException($"The type [{type}] got multiple alias properties");
				else if (!typeof(IAliasEntity).IsAssignableFrom(type))
					throw new InformationRequiredException($"The type [{type}] got alias property but not implement the 'IAliasEntity' interface");
				var aliasInfo = aliasProperties.First().GetCustomAttribute<AliasAttribute>();
				if (!string.IsNullOrWhiteSpace(aliasInfo.Properties))
				{
					var aliasProps = aliasInfo.Properties.ToHashSet(",", true);
					var missingProps = aliasProps.Except(properties.Where(attribute => aliasProps.Contains(attribute.Name)).Select(attribute => attribute.Name), StringComparer.OrdinalIgnoreCase).ToList();
					if (missingProps.Count > 0)
						throw new InformationInvalidException($"The properties to make the alias combination of the type [{type}] are invalid [missing: {missingProps.Join(", ")}]");
				}
			}

			// public properties
			var numberOfPrimaryKeys = 0;
			foreach (var property in properties)
			{
				// by-pass if ignore and not defined as a form control
				if (property.IsIgnored() && !property.IsFormControl())
					continue;

				// create
				var attribute = new AttributeInfo(property);

				// primary key
				if (!property.IsIgnored() && attribute.GetCustomAttribute<PrimaryKeyAttribute>() is PrimaryKeyAttribute primaryKeyInfo)
				{
					attribute.Column = primaryKeyInfo.Column;
					attribute.NotNull = true;
					attribute.NotEmpty = true;
					if (primaryKeyInfo.MaxLength > 0)
						attribute.MaxLength = primaryKeyInfo.MaxLength;

					definition.PrimaryKey = attribute.Name;
					numberOfPrimaryKeys += 1;
				}

				// property
				if (attribute.GetCustomAttribute<PropertyAttribute>() is PropertyAttribute propertyInfo)
				{
					attribute.Column = propertyInfo.Column;
					attribute.NotNull = propertyInfo.NotNull;
					if (attribute.IsStringType())
					{
						if (propertyInfo.NotEmpty)
							attribute.NotEmpty = true;
						if (propertyInfo.IsCLOB)
							attribute.IsCLOB = true;
						if (propertyInfo.MinLength > 0)
							attribute.MinLength = propertyInfo.MinLength;
						if (!propertyInfo.IsCLOB)
							attribute.MaxLength = propertyInfo.MaxLength > 0 && propertyInfo.MaxLength < 4000
								? propertyInfo.MaxLength
								: attribute.IsParentMapping() || attribute.Name.EndsWith("ID") ? 32 : 4000;
					}
					else
					{
						if (!string.IsNullOrWhiteSpace(propertyInfo.MinValue))
							attribute.MinValue = propertyInfo.MinValue;
						if (!string.IsNullOrWhiteSpace(propertyInfo.MaxValue))
							attribute.MaxValue = propertyInfo.MaxValue;
					}
				}

				// update definitions
				definition.FormAttributes.Add(attribute);
				if (!property.IsIgnored())
				{
					definition.Attributes.Add(attribute);
					if (attribute.IsSortable())
						definition.SortableAttributes.Add(attribute.Name);
				}
			};

			// check primary key
			if (numberOfPrimaryKeys < 1)
				throw new InformationRequiredException($"The type [{type}] got no primary-key");
			else if (numberOfPrimaryKeys > 1)
				throw new InformationInvalidException($"The type [{type}] got multiple primary-keys");

			// private attributes
			type.GetPrivateAttributes().Where(field => !field.IsIgnored()).ForEach(field =>
			{
				// create
				var attribute = new AttributeInfo(field);

				// update info
				if (attribute.GetCustomAttribute<FieldAttribute>() is FieldAttribute fieldInfo)
				{
					attribute.Column = fieldInfo.Column;
					attribute.NotNull = fieldInfo.NotNull;
					if (attribute.IsStringType())
					{
						if (fieldInfo.NotEmpty)
							attribute.NotEmpty = true;
						if (fieldInfo.IsCLOB)
							attribute.IsCLOB = true;
						if (fieldInfo.MinLength > 0)
							attribute.MinLength = fieldInfo.MinLength;
						if (!fieldInfo.IsCLOB)
							attribute.MaxLength = fieldInfo.MaxLength > 0 && fieldInfo.MaxLength < 4000
								? fieldInfo.MaxLength
								: attribute.IsParentMapping() || attribute.Name.EndsWith("ID") ? 32 : 4000;
					}
					else
					{
						if (!string.IsNullOrWhiteSpace(fieldInfo.MinValue))
							attribute.MinValue = fieldInfo.MinValue;
						if (!string.IsNullOrWhiteSpace(fieldInfo.MaxValue))
							attribute.MaxValue = fieldInfo.MaxValue;
					}

					// update definitions
					definition.Attributes.Add(attribute);
				}
			});

			// cache
			var info = type.GetCustomAttribute<EntityAttribute>(false);
			if (info.CacheClass != null && !string.IsNullOrWhiteSpace(info.CacheName))
			{
				var cache = info.CacheClass.GetStaticObject(info.CacheName);
				definition.Cache = cache != null && cache is ICache ? cache as ICache : null;
			}

			// type of repository definition
			var rootType = typeof(object);
			var baseType = typeof(RepositoryBase);
			var parentType = type.BaseType;
			var grandParentType = parentType.BaseType;
			while (grandParentType.BaseType != null && grandParentType.BaseType != rootType && grandParentType.BaseType != baseType)
			{
				parentType = parentType.BaseType;
				grandParentType = parentType.BaseType;
			}
			var repositoryDefinitionTypeName = parentType.GetTypeName();
			definition.RepositoryDefinitionType = Type.GetType(repositoryDefinitionTypeName.Left(repositoryDefinitionTypeName.IndexOf("[")) + repositoryDefinitionTypeName.Substring(repositoryDefinitionTypeName.IndexOf("]") + 2));

			// update into collection
			if (RepositoryMediator.EntityDefinitions.TryAdd(type, definition))
			{
				var log = $"The repository entity definition was registered [{definition.Type.GetTypeName()}{(string.IsNullOrWhiteSpace(definition.Title) ? "" : $" => {definition.Title}")}]" + "\r\n" +
				$"- Attributes: {definition.Attributes.Select(attribute => $"{attribute.Name} ({attribute.Type.GetTypeName(true)})").Join(", ")}\r\n" +
				$"- Parent: {definition.ParentType?.GetTypeName() ?? "None"}";
				tracker?.Invoke(log, null);
				if (tracker == null && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(log);
			}
		}

		internal static void Update(JObject settings, Action<string, Exception> tracker = null)
		{
			// check
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["type"] == null)
				throw new ArgumentNullException("type", "[type] attribute of settings");

			// prepare
			var definition = RepositoryMediator.GetEntityDefinition(Type.GetType((settings["type"] as JValue).Value as string));
			if (definition == null)
				return;

			// update
			var data = settings["primaryDataSource"] != null
				? (settings["primaryDataSource"] as JValue).Value as string
				: null;
			definition.PrimaryDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			data = settings["secondaryDataSource"] != null
				? (settings["secondaryDataSource"] as JValue).Value as string
				: null;
			definition.SecondaryDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			definition.SyncDataSourceNames = settings["syncDataSources"] != null
				? (settings["syncDataSources"] as JValue).Value as string
				: null;

			data = settings["versionDataSource"] != null
				? (settings["versionDataSource"] as JValue).Value as string
				: null;
			definition.VersionDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			data = settings["trashDataSource"] != null
				? (settings["trashDataSource"] as JValue).Value as string
				: null;
			definition.TrashDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
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
				var cacheActiveSynchronize = settings["cacheActiveSynchronize"] != null && ((settings["cacheActiveSynchronize"] as JValue).Value as string).IsEquals("true");
				var cacheProvider = settings["cacheProvider"] != null
					? (settings["cacheProvider"] as JValue).Value as string
					: null;
				definition.Cache = new Cache(cacheRegion, cacheExpirationTime, cacheActiveSynchronize, cacheProvider);
			}

			definition.AutoSync = settings["autoSync"] != null
				? "true".IsEquals((settings["autoSync"] as JValue).Value as string)
				: definition.RepositoryDefinition.AutoSync;

			tracker?.Invoke(
				$"Settings of a repository entity was updated [{definition.Type.GetTypeName()}{(string.IsNullOrWhiteSpace(definition.Title) ? "" : $" => {definition.Title}")}]" + "\r\n" +
				$"- Primary data source: {(definition.PrimaryDataSource != null ? $"{definition.PrimaryDataSource.Name} ({definition.PrimaryDataSource.Mode})" : "None")}" + "\r\n" +
				$"- Secondary data source: {(definition.SecondaryDataSource != null ? $"{definition.SecondaryDataSource.Name} ({definition.SecondaryDataSource.Mode})" : "None")}" + "\r\n" +
				$"- Sync data sources: {(definition.SyncDataSources != null && definition.SyncDataSources.Count > 0 ? definition.SyncDataSources.Select(dataSource => $"{dataSource.Name} ({dataSource.Mode})").ToString(", ") : "None")}" + "\r\n" +
				$"- Version data source: {definition.VersionDataSource?.Name ?? "(None)"}" + "\r\n" +
				$"- Trash data source: {definition.TrashDataSource?.Name ?? "(None)"}" + "\r\n" +
				$"- Auto sync: {definition.AutoSync}"
			, null);
		}

		/// <summary>
		/// Sets the cache storage of a repository entity definition
		/// </summary>
		/// <param name="type">The type that presents information of a repository entity definition</param>
		/// <param name="cache">The cache storage</param>
		public void SetCacheStorage(Type type, Cache cache)
		{
			if (type != null && RepositoryMediator.EntityDefinitions.ContainsKey(type))
				RepositoryMediator.EntityDefinitions[type].Cache = cache;
		}
		#endregion

		#region Register/Unregister business repository entities
		/// <summary>
		/// Registers a business repository entity (means a business content-type at run-time)
		/// </summary>
		/// <param name="businessRepositoryEntity"></param>
		public bool Register(IBusinessRepositoryEntity businessRepositoryEntity)
		{
			if (businessRepositoryEntity != null && !string.IsNullOrWhiteSpace(businessRepositoryEntity.ID))
			{
				var existed = this.BusinessRepositoryEntities.ContainsKey(businessRepositoryEntity.ID);
				this.BusinessRepositoryEntities[businessRepositoryEntity.ID] = businessRepositoryEntity;
				if (!existed && RepositoryMediator.IsTraceEnabled)
					RepositoryMediator.WriteLogs($"A business repository entity (a run-time business content-type) was registered\r\n{businessRepositoryEntity.ToJson()}");
				return true;
			}
			return false;
		}

		/// <summary>
		/// Unregisters a business repository entity (means a business content-type at run-time)
		/// </summary>
		/// <param name="businessRepositoryEntityID"></param>
		public bool Unregister(string businessRepositoryEntityID)
			=> !string.IsNullOrWhiteSpace(businessRepositoryEntityID) && this.BusinessRepositoryEntities.Remove(businessRepositoryEntityID);

		/// <summary>
		/// Unregisters a business repository entity (means a business content-type at run-time)
		/// </summary>
		/// <param name="businessRepositoryEntity"></param>
		public bool Unregister(IBusinessRepositoryEntity businessRepositoryEntity)
		{
			var success = this.Unregister(businessRepositoryEntity?.ID);
			if (success && RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"A business repository entity (a run-time business content-type) was uregistered\r\n{businessRepositoryEntity?.ToJson()}");
			return success;
		}
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents information of an attribute of a repository entity
	/// </summary>
	[DebuggerDisplay("Name = {Name}")]
	public class AttributeInfo : ObjectService.AttributeInfo
	{
		public AttributeInfo() : this(null) { }

		public AttributeInfo(ObjectService.AttributeInfo derived) : base(derived?.Info) { }

		public string Column { get; internal set; }

		public bool NotNull { get; internal set; } = false;

		public bool? NotEmpty { get; internal set; }

		public bool? IsCLOB { get; internal set; }

		public int? MinLength { get; internal set; }

		public int? MaxLength { get; internal set; }

		public string MinValue { get; internal set; }

		public string MaxValue { get; internal set; }
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a definition of an extended property of an entity in a respository 
	/// </summary>
	[DebuggerDisplay("Name = {Name}, Mode = {Mode}")]
	public sealed class ExtendedPropertyDefinition
	{
		public ExtendedPropertyDefinition() { }

		public ExtendedPropertyDefinition(JObject data = null)
			=> this.CopyFrom(data ?? new JObject());

		public ExtendedPropertyDefinition(ExpandoObject data = null)
			=> this.CopyFrom(data ?? new ExpandoObject());

		#region Properties
		/// <summary>
		/// Gets or Sets the name
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Gets or Sets the mode
		/// </summary>
		[JsonConverter(typeof(StringEnumConverter)), BsonRepresentation(MongoDB.Bson.BsonType.String)]
		public ExtendedPropertyMode Mode { get; set; } = ExtendedPropertyMode.SmallText;

		/// <summary>
		/// Gets or Sets the name of column for storing data (when repository mode is SQL)
		/// </summary>
		public string Column { get; set; }

		/// <summary>
		/// Gets or Sets the default value
		/// </summary>
		public string DefaultValue { get; set; }

		/// <summary>
		/// Gets or Sets the formula for computing default value
		/// </summary>
		public string DefaultValueFormula { get; set; }
		#endregion

		#region Properties [Helper]
		/// <summary>
		/// Gets the runtime-type of this property
		/// </summary>
		[JsonIgnore, BsonIgnore, XmlIgnore]
		public Type Type
		{
			get
			{
				switch (this.Mode)
				{
					case ExtendedPropertyMode.YesNo:
						return typeof(bool);

					case ExtendedPropertyMode.IntegralNumber:
						return typeof(long);

					case ExtendedPropertyMode.FloatingPointNumber:
						return typeof(decimal);

					case ExtendedPropertyMode.DateTime:
						return typeof(DateTime);

					default:
						return typeof(string);
				}
			}
		}

		/// <summary>
		/// Gets the database-type of this property
		/// </summary>
		[JsonIgnore, BsonIgnore, XmlIgnore]
		public DbType DbType => this.Type.GetDbType();
		#endregion

		#region Validations
		/// <summary>
		/// Gets the collection of reserved words (means the excluded attributes)
		/// </summary>
		public static HashSet<string> ReservedWords { get; } = "ExtendedProperties,Add,External,Procedure,All,Fetch,Public,Alter,File,RaisError,And,FillFactor,Read,Any,For,ReadText,As,Foreign,ReConfigure,Asc,FreeText,References,Authorization,FreeTextTable,Replication,Backup,From,Restore,Begin,Full,Restrict,Between,Function,Return,Break,Goto,Revert,Browse,Grant,Revoke,Bulk,Group,Right,By,Having,Rollback,Cascade,Holdlock,Rowcount,Case,RowGuidCol,Check,Identity,Insert,Rule,Checkpoint,Identitycol,Save,Close,If,Schema,Clustered,In,SecurityAudit,Coalesce,Index,Select,Collate,Inner,SemanticKeyPhraseTable,Column,SemanticSimilarityDetailsTable,Commit,Intersect,SemanticSimilarityTable,Compute,Into,Session,User,Constraint,Is,Set,Contains,Join,Setuser,ContainsTable,Key,Shutdown,Continue,Kill,Some,Convert,Left,Statistics,Create,Like,System,Cross,Lineno,Table,Current,Load,TableSample,Current_Date,Current_Time,Current_Timestamp,Merge,TextSize,National,Then,NoCheck,To,Current_User,NonClustered,Top,Cursor,Not,Tran,Database,Null,Transaction,Dbcc,NullIf,Trigger,Deallocate,Of,Truncate,Declare,Off,Try_Convert,Default,Offsets,Tsequal,Delete,On,Union,Deny,Open,Unique,Desc,OpenDataSource,Unpivot,Disk,Openquery,Update,Distinct,OpenRowset,UpdateText,Distributed,OpenXml,Use,Double,Option,User,Drop,Or,Values,Dump,Order,Varying,Else,Outer,View,End,Over,Waitfor,Errlvl,Percent,When,Escape,Pivot,Where,Except,Plan,While,Exec,Precision,With,Execute,Primary,Exists,Print,WriteText,Exit,Proc".ToLower().ToHashSet();

		/// <summary>
		/// Validates the name of a extended property definition
		/// </summary>
		/// <param name="name">The string that presents name of a extended property</param>
		/// <remarks>An exception will be thrown if the name is invalid</remarks>
		public static void Validate(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentNullException("name", "The name is null or empty");

			if (!name.IsEquals(name.GetANSIUri().Replace("-", "")))
				throw new InformationInvalidException("The name is contains one or more invalid characters (like space, -, +, ...)");

			if (name.ToUpper()[0] < 'A' || name.ToUpper()[0] > 'Z')
				throw new InformationInvalidException("The name must starts with a letter");

			if (ExtendedPropertyDefinition.ReservedWords.Contains(name.ToLower()))
				throw new InformationInvalidException("The name is system reserved word");
		}
		#endregion

		#region Helper methods
		public object GetDefaultValue()
			=> this.DefaultValue?.CastAs(this.Type);

		public override string ToString()
			=> this.ToJson().ToString(Formatting.None);
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a data source
	/// </summary>
	[DebuggerDisplay("Name = {Name}, Mode = {Mode}")]
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
		public RepositoryMode Mode { get; internal set; }

		/// <summary>
		/// Gets the name of the connection string (for working with database server)
		/// </summary>
		public string ConnectionStringName { get; internal set; }

		/// <summary>
		/// Gets or Sets the connection string (for working with database server)
		/// </summary>
		public string ConnectionString { get; set; }

		/// <summary>
		/// Gets the name of the database (for working with database server)
		/// </summary>
		public string DatabaseName { get; internal set; }

		/// <summary>
		/// Gets the name of the database provider (for working with SQL database server)
		/// </summary>
		public string ProviderName { get; internal set; }
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
			dataSource.ConnectionString = string.IsNullOrWhiteSpace(dataSource.ConnectionString) ? null : dataSource.ConnectionString.Trim();

			// name of database
			if (settings["databaseName"] != null)
				dataSource.DatabaseName = (settings["databaseName"] as JValue).Value as string;
			else if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
				throw new ArgumentNullException("databaseName", "[databaseName] attribute of settings");

			if (settings["providerName"] != null)
				dataSource.ProviderName = (settings["providerName"] as JValue).Value as string;
			else if (dataSource.Mode.Equals(RepositoryMode.SQL) && !string.IsNullOrWhiteSpace(dataSource.ConnectionStringName))
				dataSource.ProviderName = RepositoryMediator.GetConnectionStringSettings(dataSource)?.ProviderName;

			return dataSource;
		}
		#endregion

	}
}