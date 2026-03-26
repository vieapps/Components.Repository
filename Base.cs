#region Related components
using System;
using System.Linq;
using System.Data;
using System.Dynamic;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MsgPack.Serialization;
using MongoDB.Driver;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using net.vieapps.Components.Utility;
using net.vieapps.Components.Security;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Presents the base of a repository entity in a repository
	/// </summary>
	public abstract class RepositoryBase : IPropertyChangedNotifier
	{
		/// <summary>
		/// Initialize a new instance
		/// </summary>
		public RepositoryBase()
			=> this.AssignPropertyChangedEventHandler();

		#region Mandatory properties
		/// <summary>
		/// Gets or sets the identity (the primary key)
		/// </summary>
		[BsonId(IdGenerator = typeof(IdentityGenerator))]
		[PrimaryKey(MaxLength = 32)]
		[FormControl(Hidden = true)]
		public virtual string ID { get; set; }

		/// <summary>
		/// Gets or sets the title
		/// </summary>
		[IgnoreIfNull, BsonIgnoreIfNull]
		[Property(MaxLength = 250, NotNull = true, NotEmpty = true)]
		[Sortable(IndexName = "Title")]
		[Searchable]
		public virtual string Title { get; set; }

		/// <summary>
		/// Gets the searching score for ordering the results
		/// </summary>
		[Ignore, JsonIgnore, XmlIgnore, BsonIgnoreIfNull, MessagePackIgnore]
		public virtual double? SearchScore { get; set; }

		/// <summary>
		/// Gets the name of the service that associates with this entity
		/// </summary>
		[Ignore, JsonIgnore, XmlIgnore, BsonIgnore, MessagePackIgnore]
		public virtual string ServiceName => RepositoryMediator.GetEntityDefinition(this.GetType())?.RepositoryDefinition?.ServiceName;

		/// <summary>
		/// Gets the name of the service's object that associates with this entity
		/// </summary>
		[Ignore, JsonIgnore, XmlIgnore, BsonIgnore, MessagePackIgnore]
		public virtual string ObjectName => RepositoryMediator.GetEntityDefinition(this.GetType())?.ObjectName ?? this.GetType().GetTypeName(true);
		#endregion

		#region IBusinessEntity properties
		/// <summary>
		/// Gets or sets the identity of the system that the object is belong to (means the system/organization at run-time )
		/// </summary>
		[IgnoreIfNull, JsonIgnore, XmlIgnore, BsonIgnoreIfNull]
		[Property(MaxLength = 32)]
		[Sortable(IndexName = "System")]
		[FormControl(Hidden = true)]
		public virtual string SystemID { get; set; }

		/// <summary>
		/// Gets or sets the identity of the repository that the object is belong to (means the business module at run-time)
		/// </summary>
		[IgnoreIfNull, JsonIgnore, XmlIgnore, BsonIgnoreIfNull]
		[Property(MaxLength = 32)]
		[Sortable(IndexName = "System")]
		[FormControl(Hidden = true)]
		public virtual string RepositoryID { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business repository entity that the object is belong to (means the business content-type at run-time)
		/// </summary>
		[IgnoreIfNull, JsonIgnore, XmlIgnore, BsonIgnoreIfNull]
		[Property(MaxLength = 32)]
		[Sortable(IndexName = "System")]
		[FormControl(Hidden = true)]
		public virtual string RepositoryEntityID { get; set; }

		/// <summary>
		/// Gets the object that marks as parent of this object
		/// </summary>
		[Ignore, JsonIgnore, XmlIgnore, BsonIgnore, MessagePackIgnore]
		public virtual RepositoryBase Parent { get; }

		/// <summary>
		/// Gets or sets the collection of extended properties
		/// </summary>
		[Ignore, JsonIgnore, XmlIgnore, BsonIgnoreIfNull, MessagePackIgnore]
		public virtual Dictionary<string, object> ExtendedProperties { get; set; }

		[Ignore, JsonIgnore, XmlIgnore, BsonIgnore]
		public byte[] MsgPackExtendedProperties
		{
			get => this.ExtendedProperties != null && this.ExtendedProperties.Count > 0 ? Caching.Helper.SerializeBson(this.ExtendedProperties) : Array.Empty<byte>();
			set => this.ExtendedProperties = value != null && value.Length > 0 ? Caching.Helper.DeserializeBson<Dictionary<string, object>>(value) : null;
		}

		/// <summary>
		/// Gets or sets the original privileges (means original working permissions)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, MessagePackIgnore]
		[AsJson]
		[FormControl(Excluded = true)]
		public virtual Privileges OriginalPrivileges { get; set; }

		[Ignore, JsonIgnore, XmlIgnore, BsonIgnore]
		public byte[] MsgPackOriginalPrivileges
		{
			get => this.OriginalPrivileges != null ? Caching.Helper.SerializeBson(this.OriginalPrivileges) : Array.Empty<byte>();
			set
			{
				this.OriginalPrivileges = value != null && value.Length > 0 ? Caching.Helper.DeserializeBson<Privileges>(value) : null;
				if (RepositoryMediator.IsTraceEnabled)
					Task.Run(async () =>
					{
						await Task.Delay(123).ConfigureAwait(false);
						RepositoryMediator.WriteLogs($"The object was deserialized [{this.GetType()}#{this.ID} => {this.Title}]");
					}).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// The privileges that are combined from original privileges and parent privileges
		/// </summary>
		[MessagePackIgnore]
		protected Privileges _workingPrivileges = null;

		/// <summary>
		/// Gets the working privileges (means the actual working permissions - that combined with parents' privileges)
		/// </summary>
		[Ignore, JsonIgnore, XmlIgnore, BsonIgnore, MessagePackIgnore]
		public virtual Privileges WorkingPrivileges
			=> this._workingPrivileges ?? (this._workingPrivileges = (this.OriginalPrivileges ?? new Privileges()).Combine(this.Parent?.WorkingPrivileges));
		#endregion

		#region Methods
		/// <summary>
		/// Gest or sets value of a property by name
		/// </summary>
		/// <param name="name">The string that presents the name of a property</param>
		/// <returns></returns>
		protected virtual object this[string name]
		{
			get { return this.GetProperty(name); }
			set { this.SetProperty(name, value); }
		}

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <returns></returns>
		public abstract object GetProperty(string name);

		/// <summary>
		/// Sets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="value">The value of the property</param>
		public abstract void SetProperty(string name, object value);

		/// <summary>
		/// Serializes this object to JSON
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (named with surfix '$Type')</param>
		/// <param name="onCompleted">The action to run on completed</param>
		/// <returns></returns>
		public abstract JObject ToJson(bool addTypeOfExtendedProperties, Action<JObject> onCompleted);

		/// <summary>
		/// Serializes this object to JSON
		/// </summary>
		/// <param name="onCompleted">The action to run on completed</param>
		/// <returns></returns>
		public virtual JObject ToJson(Action<JObject> onCompleted)
			=> this.ToJson(false, onCompleted);

		/// <summary>
		/// Serializes this object to JSON
		/// </summary>
		/// <returns></returns>
		public virtual JObject ToJson()
			=> this.ToJson(false, null);

		/// <summary>
		/// Parses the JSON and copy values into this object
		/// </summary>
		/// <param name="json">The JSON object that contains information</param>
		/// <param name="onCompleted">The action to run when complete</param>
		public abstract void ParseJson(JObject json, Action<JObject> onCompleted);

		/// <summary>
		/// Serializes this object to XML
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (attribute named '$type')</param>
		/// <param name="onCompleted">The action to run when complete</param>
		/// <returns></returns>
		public abstract XElement ToXml(bool addTypeOfExtendedProperties, Action<XElement> onCompleted);

		/// <summary>
		/// Serializes this object to XML
		/// </summary>
		/// <param name="onCompleted">The action to run when complete</param>
		/// <returns></returns>
		public virtual XElement ToXml(Action<XElement> onCompleted)
			=> this.ToXml(false, onCompleted);

		/// <summary>
		/// Serializes this object to XML
		/// </summary>
		/// <returns></returns>
		public virtual XElement ToXml()
			=> this.ToXml(false, null);

		/// <summary>
		/// Parses the XML and copy values into this object
		/// </summary>
		/// <param name="xml">The XML object that contains information</param>
		/// <param name="onCompleted">The action to run when complete</param>
		public abstract void ParseXml(XContainer xml, Action<XContainer> onCompleted);

		/// <summary>
		/// Serializes this object to ExpandoObject
		/// </summary>
		/// <param name="onCompleted">The action to run on completed</param>
		/// <returns></returns>
		public virtual ExpandoObject ToExpandoObject(Action<ExpandoObject> onCompleted)
			=> this.ToJson().ToExpandoObject(onCompleted);

		/// <summary>
		/// Converts this object to string (JSON format)
		/// </summary>
		/// <param name="formatting"></param>
		/// <returns></returns>
		public virtual string ToString(Formatting formatting)
			=> this.ToJson().ToString(formatting);

		/// <summary>
		/// Converts this object to string (JSON format)
		/// </summary>
		/// <returns></returns>
		public override string ToString()
			=> this.ToString(Formatting.None);
		#endregion

		#region On property changed
		public event PropertyChangedEventHandler PropertyChanged;

		public virtual void NotifyPropertyChanged([CallerMemberName] string name = "", object sender = null)
		{
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"A property was changed: {name} @ [{this.GetType()}#{this.ID} => {this.Title}]");
			this.PropertyChanged?.Invoke(sender ?? this, new PropertyChangedEventArgs(name));
		}

		/// <summary>
		/// Assigns the event handler to process a property changed
		/// </summary>
		protected virtual void AssignPropertyChangedEventHandler()
			=> this.PropertyChanged += (_, args) => this.ProcessPropertyChanged(args.PropertyName);

		public virtual void ProcessPropertyChanged(string name) { }
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents the base of a repository entity of a repository with helper methods to perform CRUD operations, count, find, and query (full-text search)
	/// </summary>
	[DebuggerDisplay("ID = {ID}, Type = {typeof(T).FullName}")]
	public abstract class RepositoryBase<T> : RepositoryBase where T : class
	{
		/// <summary>
		/// Initialize a new instance
		/// </summary>
		public RepositoryBase() : base() { }

		/// <summary>
		/// The number that presents the total of version contents
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnore]
		protected long _totalVersions = -1;

		/// <summary>
		/// Gets the total number of version contents
		/// </summary>
		[Ignore, JsonIgnore, XmlIgnore, BsonIgnore, MessagePackIgnore]
		public virtual long TotalVersions
		{
			set => this._totalVersions = value;
			get
			{
				if (this._totalVersions < 0)
					this.CountVersions();
				return this._totalVersions;
			}
		}

		#region [Public] Fill
		/// <summary>
		/// Fills data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="nullable">The nullable properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <param name="onError">The action to run when got any error</param>
		public T Fill(ExpandoObject data, HashSet<string> excluded, HashSet<string> nullable, Action<T> onCompleted = null, Action<Exception> onError = null)
		{
			// standard properties
			this.CopyFrom(data, excluded, nullable, null, onError).TrimAll().OriginalPrivileges = this.OriginalPrivileges?.Normalize();
			if (RepositoryMediator.IsTraceEnabled)
			{
				var attributes = new JObject();
				var excludedAttributes = new HashSet<string>(excluded ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);
				this.GetPublicAttributes(attribute => !attribute.IsStatic && attribute.CanWrite && !excludedAttributes.Contains(attribute.Name)).ForEach(attribute =>
				{
					if (data.TryGet(attribute.Name, out var expandoValue))
						try
						{
							var expandoJson = expandoValue?.ToJson();
							var objectJson = this.GetAttributeValue(attribute)?.ToJson();
							attributes[attribute.Name] = new JObject
							{
								{ "IsEquals", objectJson?.ToString() == expandoJson?.ToString() },
								{ "Source", expandoJson },
								{ "Filled", objectJson }
							};
						}
						catch
						{
							attributes[attribute.Name] = new JValue("(binary or unknown)");
						}
				});
				RepositoryMediator.WriteLogs($"Fill data into object [{typeof(T)}#{this.ID}] - Excluded: {excluded?.Join(", ") ?? "None"}\r\nComparing sheets:\r\n{attributes}");
			}

			// extended properties
			if (this is IBusinessEntity && !string.IsNullOrWhiteSpace(this.RepositoryEntityID) && RepositoryMediator.GetEntityDefinition<T>().BusinessRepositoryEntities.TryGetValue(this.RepositoryEntityID, out var repositoryEntity) && repositoryEntity?.ExtendedPropertyDefinitions != null)
			{
				this.ExtendedProperties = this.ExtendedProperties ?? new Dictionary<string, object>();
				var excludedAttributes = new HashSet<string>(excluded ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);
				repositoryEntity.ExtendedPropertyDefinitions.Where(definition => !excludedAttributes.Contains(definition.Name)).ForEach(definition =>
				{
					var value = data?.Get(definition.Name);
					if (value != null)
					{
						// validate string value
						if (value is string @string)
						{
							if (definition.Mode.Equals(ExtendedPropertyMode.IntegralNumber) || definition.Mode.Equals(ExtendedPropertyMode.FloatingPointNumber))
								value = @string.CastAs(definition.Type);
							else if (definition.Mode.Equals(ExtendedPropertyMode.YesNo))
								value = "true".IsEquals(@string);
							else if (definition.Mode.Equals(ExtendedPropertyMode.DateTime))
								value = DateTime.Parse(@string);
							else
							{
								var maxLength = 0;
								switch (definition.Mode)
								{
									case ExtendedPropertyMode.SmallText:
									case ExtendedPropertyMode.Select:
										maxLength = 250;
										break;

									case ExtendedPropertyMode.MediumText:
									case ExtendedPropertyMode.Lookup:
										maxLength = 4000;
										break;
								}
								value = maxLength > 0 && @string.Length > maxLength ? @string.Left(maxLength) : @string.Trim();
							}
						}

						// validate multiple values of select (array/list)
						else if (definition.Mode.Equals(ExtendedPropertyMode.Select) && value is IEnumerable<string> selectValues)
						{
							var maxLength = 250;
							@string = selectValues.Where(val => !string.IsNullOrWhiteSpace(val)).Join("#;");
							value = maxLength > 0 && @string.Length > maxLength ? @string.Left(maxLength) : @string.Trim();
						}

						// validate multiple values of lookup (array/list)
						else if (definition.Mode.Equals(ExtendedPropertyMode.Lookup) && value is IEnumerable<string> lookupValues)
						{
							var maxLength = 4000;
							@string = lookupValues.Where(val => !string.IsNullOrWhiteSpace(val)).Join("#;");
							value = maxLength > 0 && @string.Length > maxLength ? @string.Left(maxLength) : @string.Trim();
						}
					}

					// assign value
					this.ExtendedProperties[definition.Name] = value;
				});
			}

			// return object
			onCompleted?.Invoke(this as T);
			if (RepositoryMediator.IsTraceEnabled)
			{
				var json = new JObject();
				this.GetPublicAttributes(attribute => !attribute.IsStatic && attribute.CanWrite).ForEach(attribute =>
				{
					try
					{
						json[attribute.Name] = this.GetAttributeValue(attribute.Name)?.ToJson();
					}
					catch
					{
						json[attribute.Name] = new JValue("(binary or unknown)");
					}
				});
				RepositoryMediator.WriteLogs($"The object has been filled [{typeof(T)}#{this.ID}]\r\n{json}");
			}
			return this as T;
		}

		/// <summary>
		/// Fills data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		public T Fill(ExpandoObject data, HashSet<string> excluded = null, Action<T> onCompleted = null)
			=> this.Fill(data, excluded, null, onCompleted);

		/// <summary>
		/// Fills data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="nullable">The nullable properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <param name="onError">The action to run when got any error</param>
		public T Fill(ExpandoObject data, string excluded, string nullable, Action<T> onCompleted = null, Action<Exception> onError = null)
			=> this.Fill(data, excluded?.ToHashSet(), nullable?.ToHashSet(), onCompleted, onError);

		/// <summary>
		/// Fills data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		public T Fill(ExpandoObject data, string excluded, Action<T> onCompleted)
			=> this.Fill(data, excluded, null, onCompleted, null);

		/// <summary>
		/// Fills data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="onCompleted">The action to run when completed</param>
		public T Fill(ExpandoObject data, Action<T> onCompleted)
			=> this.Fill(data, "", "", onCompleted, null);

		/// <summary>
		/// Fills data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="nullable">The nullable properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		public T Fill(JToken data, HashSet<string> excluded, HashSet<string> nullable, Action<T> onCompleted = null, Action<Exception> onError = null)
			=> this.Fill(data?.ToExpandoObject(), excluded, nullable, onCompleted, onError);

		/// <summary>
		/// Fills data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		public T Fill(JToken data, HashSet<string> excluded = null, Action<T> onCompleted = null)
			=> this.Fill(data, excluded, null, onCompleted);

		/// <summary>
		/// Fills data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="nullable">The nullable properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <param name="onError">The action to run when got any error</param>
		public T Fill(JToken data, string excluded, string nullable, Action<T> onCompleted = null, Action<Exception> onError = null)
			=> this.Fill(data, excluded?.ToHashSet(), nullable?.ToHashSet(), onCompleted, onError);

		/// <summary>
		/// Fills data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		public T Fill(JToken data, string excluded, Action<T> onCompleted)
			=> this.Fill(data, excluded, null, onCompleted, null);

		/// <summary>
		/// Fills data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="onCompleted">The action to run when completed</param>
		public T Fill(JToken data, Action<T> onCompleted)
			=> this.Fill(data, "", "", onCompleted, null);
		#endregion

		#region [Static] Create new an instance
		/// <summary>
		/// Create new an instance and fill data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="nullable">The nullable properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <param name="onError">The action to run when got any error</param>
		/// <returns></returns>
		public static T CreateInstance(ExpandoObject data, HashSet<string> excluded, HashSet<string> nullable, Action<T> onCompleted = null, Action<Exception> onError = null)
			=> typeof(T).CreateInstance<RepositoryBase<T>>().Fill(data, excluded, nullable, onCompleted, onError);

		/// <summary>
		/// Create new an instance and fill data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <returns></returns>
		public static T CreateInstance(ExpandoObject data, HashSet<string> excluded = null, Action<T> onCompleted = null)
			=> typeof(T).CreateInstance<RepositoryBase<T>>().Fill(data, excluded, null, onCompleted);

		/// <summary>
		/// Create new an instance and fill data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="nullable">The nullable properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <param name="onError">The action to run when got any error</param>
		/// <returns></returns>
		public static T CreateInstance(ExpandoObject data, string excluded, string nullable, Action<T> onCompleted = null, Action<Exception> onError = null)
			=> typeof(T).CreateInstance<RepositoryBase<T>>().Fill(data, excluded?.ToHashSet(), nullable?.ToHashSet(), onCompleted, onError);

		/// <summary>
		/// Create new an instance and fill data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <returns></returns>
		public static T CreateInstance(ExpandoObject data, string excluded, Action<T> onCompleted)
			=> typeof(T).CreateInstance<RepositoryBase<T>>().Fill(data, excluded, null, onCompleted, null);

		/// <summary>
		/// Create new an instance and fill data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <returns></returns>
		public static T CreateInstance(ExpandoObject data, Action<T> onCompleted)
			=> typeof(T).CreateInstance<RepositoryBase<T>>().Fill(data, "", "", onCompleted, null);

		/// <summary>
		/// Create new an instance and fill data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="nullable">The nullable properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <param name="onError">The action to run when got any error</param>
		/// <returns></returns>
		public static T CreateInstance(JToken data, HashSet<string> excluded, HashSet<string> nullable, Action<T> onCompleted = null, Action<Exception> onError = null)
			=> typeof(T).CreateInstance<RepositoryBase<T>>().Fill(data, excluded, nullable, onCompleted, onError);

		/// <summary>
		/// Create new an instance and fill data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <returns></returns>
		public static T CreateInstance(JToken data, HashSet<string> excluded = null, Action<T> onCompleted = null)
			=> typeof(T).CreateInstance<RepositoryBase<T>>().Fill(data, excluded, null, onCompleted);

		/// <summary>
		/// Create new an instance and fill data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="nullable">The nullable properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <param name="onError">The action to run when got any error</param>
		/// <returns></returns>
		public static T CreateInstance(JToken data, string excluded, string nullable, Action<T> onCompleted = null, Action<Exception> onError = null)
			=> typeof(T).CreateInstance<RepositoryBase<T>>().Fill(data, excluded?.ToHashSet(), nullable?.ToHashSet(), onCompleted, onError);

		/// <summary>
		/// Create new an instance and fill data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="excluded">The excluded properties</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <returns></returns>
		public static T CreateInstance(JToken data, string excluded, Action<T> onCompleted)
			=> typeof(T).CreateInstance<RepositoryBase<T>>().Fill(data, excluded, null, onCompleted, null);

		/// <summary>
		/// Create new an instance and fill data into objects' properties
		/// </summary>
		/// <param name="data">The data to fill into this object</param>
		/// <param name="onCompleted">The action to run when completed</param>
		/// <returns></returns>
		public static T CreateInstance(JToken data, Action<T> onCompleted)
			=> typeof(T).CreateInstance<RepositoryBase<T>>().Fill(data, "", "", onCompleted, null);
		#endregion

		#region [Static] Create
		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create(RepositoryContext context, DataSource dataSource, T @object)
			=> RepositoryMediator.Create(context, dataSource, @object);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create(DataSource dataSource, T @object)
		{
			using (var context = new RepositoryContext())
				RepositoryBase<T>.Create(context, dataSource, @object);
		}

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create(RepositoryContext context, string aliasTypeName, T @object)
			=> RepositoryMediator.Create(context, aliasTypeName, @object);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create(string aliasTypeName, T @object)
			=> RepositoryMediator.Create(aliasTypeName, @object);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create(T @object)
			=> RepositoryBase<T>.Create("", @object);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task CreateAsync(RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CreateAsync(context, dataSource, @object, cancellationToken);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task CreateAsync(DataSource dataSource, T @object, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext())
				await RepositoryBase<T>.CreateAsync(context, dataSource, @object, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateAsync(RepositoryContext context, string aliasTypeName, T @object, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CreateAsync(context, aliasTypeName, @object, cancellationToken);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateAsync(string aliasTypeName, T @object, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CreateAsync(aliasTypeName, @object, cancellationToken);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <param name="object"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task CreateAsync(T @object, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CreateAsync("", @object, cancellationToken);
		#endregion

		#region [Protected] Create
		/// <summary>
		/// Creates new instance of this object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Create(RepositoryContext context, string aliasTypeName)
			=> RepositoryBase<T>.Create(context, aliasTypeName, this as T);

		/// <summary>
		/// Creates new instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Create(string aliasTypeName = null)
		{
			using (var context = new RepositoryContext())
				this.Create(context, aliasTypeName);
		}

		/// <summary>
		/// Creates new instance of this object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task CreateAsync(RepositoryContext context, string aliasTypeName, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CreateAsync(context, aliasTypeName, this as T, cancellationToken);

		/// <summary>
		/// Creates new instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual async Task CreateAsync(string aliasTypeName = null, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext())
				await this.CreateAsync(context, aliasTypeName, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region [Static] Create many
		/// <summary>
		/// Creates new collection of objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objects">The collection of objects to create new instance in the repository</param>
		public static void CreateMany(RepositoryContext context, DataSource dataSource, IEnumerable<T> objects)
			=> RepositoryMediator.CreateMany(context, dataSource, objects);

		/// <summary>
		/// Creates new collection of objects
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objects">The collection of objects to create new instance in the repository</param>
		public static void CreateMany(DataSource dataSource, IEnumerable<T> objects)
		{
			using (var context = new RepositoryContext())
				RepositoryBase<T>.CreateMany(context, dataSource, objects);
		}

		/// <summary>
		/// Creates new collection of objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="objects">The collection of objects to create new instance in the repository</param>
		public static void CreateMany(RepositoryContext context, string aliasTypeName, IEnumerable<T> objects)
			=> RepositoryMediator.CreateMany(context, aliasTypeName, objects);

		/// <summary>
		/// Creates new collection of objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="objects">The collection of objects to create new instance in the repository</param>
		public static void CreateMany(string aliasTypeName, IEnumerable<T> objects, bool useTransaction = false)
			=> RepositoryMediator.CreateMany(aliasTypeName, objects, useTransaction);

		/// <summary>
		/// Creates new collection of objects
		/// </summary>
		/// <param name="objects">The collection of objects to create new instance in the repository</param>
		/// <param name="useTransaction">true to use transaction while creating new many</param>
		public static void CreateMany(IEnumerable<T> objects, bool useTransaction = false)
			=> RepositoryBase<T>.CreateMany("", objects, useTransaction);

		/// <summary>
		/// Creates new collection of objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objects">The collection of objects to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task CreateManyAsync(RepositoryContext context, DataSource dataSource, IEnumerable<T> objects, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CreateManyAsync(context, dataSource, objects, cancellationToken);

		/// <summary>
		/// Creates new collection of objects
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objects">The collection of objects to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task CreateManyAsync(DataSource dataSource, IEnumerable<T> objects, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext())
				await RepositoryBase<T>.CreateManyAsync(context, dataSource, objects, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Creates new collection of objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="objects">The collection of objects to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateManyAsync(RepositoryContext context, string aliasTypeName, IEnumerable<T> objects, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CreateManyAsync(context, aliasTypeName, objects, cancellationToken);

		/// <summary>
		/// Creates new collection of objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="objects">The collection of objects to create new instance in the repository</param>
		/// <param name="useTransaction">true to use transaction while creating new many</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateManyAsync(string aliasTypeName, IEnumerable<T> objects, bool useTransaction, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CreateManyAsync(aliasTypeName, objects, useTransaction, cancellationToken);

		/// <summary>
		/// Creates new collection of objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="objects">The collection of objects to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateManyAsync(string aliasTypeName, IEnumerable<T> objects, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CreateManyAsync(aliasTypeName, objects, false, cancellationToken);

		/// <summary>
		/// Creates new collection of objects
		/// </summary>
		/// <param name="objects">The collection of objects to create new instance in the repository</param>
		/// <param name="useTransaction">true to use transaction while creating new many</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateManyAsync(IEnumerable<T> objects, bool useTransaction, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CreateManyAsync("", objects, useTransaction, cancellationToken);

		/// <summary>
		/// Creates new collection of objects
		/// </summary>
		/// <param name="objects">The collection of objects to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateManyAsync(IEnumerable<T> objects, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CreateManyAsync(objects, false, cancellationToken);
		#endregion

		#region [Static] Get
		/// <summary>
		/// Gets an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static T Get(RepositoryContext context, DataSource dataSource, string id, bool processCache = true, bool processSecondaryWhenNotFound = true)
			=> !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.Get<T>(context, dataSource, id, true, processCache, processSecondaryWhenNotFound)
				: null;

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static T Get(DataSource dataSource, string id, bool processCache = true, bool processSecondaryWhenNotFound = true)
		{
			using (var context = new RepositoryContext(false))
				return RepositoryBase<T>.Get(context, dataSource, id, processCache, processSecondaryWhenNotFound);
		}

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static T Get(RepositoryContext context, string aliasTypeName, string id, bool processCache = true, bool processSecondaryWhenNotFound = true)
			=> !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.Get<T>(context, aliasTypeName, id, true, processCache, processSecondaryWhenNotFound)
				: null;

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static T Get(string aliasTypeName, string id, bool processCache = true, bool processSecondaryWhenNotFound = true)
			=> !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.Get<T>(aliasTypeName, id, processCache, processSecondaryWhenNotFound)
				: null;

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static T Get(string id, bool processCache = true, bool processSecondaryWhenNotFound = true)
			=> RepositoryBase<T>.Get("", id, processCache, processSecondaryWhenNotFound);

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<T> GetAsync(RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default, bool processCache = true, bool processSecondaryWhenNotFound = true)
			=> !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.GetAsync<T>(context, dataSource, id, true, cancellationToken, processCache, processSecondaryWhenNotFound)
				: Task.FromResult<T>(null);

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static async Task<T> GetAsync(DataSource dataSource, string id, CancellationToken cancellationToken = default, bool processCache = true, bool processSecondaryWhenNotFound = true)
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryBase<T>.GetAsync(context, dataSource, id, cancellationToken, processCache, processSecondaryWhenNotFound).ConfigureAwait(false);
		}

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<T> GetAsync(RepositoryContext context, string aliasTypeName, string id, CancellationToken cancellationToken = default, bool processCache = true, bool processSecondaryWhenNotFound = true)
			=> !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.GetAsync<T>(context, aliasTypeName, id, true, cancellationToken, processCache, processSecondaryWhenNotFound)
				: Task.FromResult<T>(null);

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<T> GetAsync(string aliasTypeName, string id, CancellationToken cancellationToken = default, bool processCache = true, bool processSecondaryWhenNotFound = true)
			=> !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.GetAsync<T>(aliasTypeName, id, cancellationToken, processCache, processSecondaryWhenNotFound)
				: Task.FromResult<T>(null);

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<T> GetAsync(string id, bool processCache, CancellationToken cancellationToken = default, bool processSecondaryWhenNotFound = true)
			=> RepositoryBase<T>.GetAsync("", id, cancellationToken, processCache, processSecondaryWhenNotFound);

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<T> GetAsync(string id, CancellationToken cancellationToken = default, bool processCache = true, bool processSecondaryWhenNotFound = true)
			=> RepositoryBase<T>.GetAsync(id, processCache, cancellationToken, processSecondaryWhenNotFound);
		#endregion

		#region [Protected] Get
		/// <summary>
		/// Gets the instance of this object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Get(RepositoryContext context, string aliasTypeName)
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
			{
				var instance = RepositoryBase<T>.Get(context, aliasTypeName, this.ID);
				if (instance != null)
					this.CopyFrom(instance);
			}
		}

		/// <summary>
		/// Gets the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Get(string aliasTypeName = null)
		{
			using (var context = new RepositoryContext(false))
				this.Get(context, aliasTypeName);
		}

		/// <summary>
		/// Gets the instance of this object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual async Task GetAsync(RepositoryContext context, string aliasTypeName, CancellationToken cancellationToken = default)
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
			{
				var instance = await RepositoryBase<T>.GetAsync(context, aliasTypeName, this.ID, cancellationToken).ConfigureAwait(false);
				if (instance != null)
					this.CopyFrom(instance);
			}
		}

		/// <summary>
		/// Gets the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual async Task GetAsync(string aliasTypeName = null, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext(false))
				await this.GetAsync(context, aliasTypeName, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region [Static] Get first match
		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static T Get(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null)
			=> RepositoryMediator.Get(context, dataSource, filter, sort, businessRepositoryEntityID);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static T Get(DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null)
		{
			using (var context = new RepositoryContext(false))
				return RepositoryBase<T>.Get(context, dataSource, filter, sort, businessRepositoryEntityID);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static T Get(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null)
			=> RepositoryMediator.Get(context, aliasTypeName, filter, sort, businessRepositoryEntityID);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static T Get(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null)
			=> RepositoryMediator.Get(aliasTypeName, filter, sort, businessRepositoryEntityID);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static T Get(IFilterBy<T> filter, SortBy<T> sort, string businessRepositoryEntityID)
			=> RepositoryBase<T>.Get("", filter, sort, businessRepositoryEntityID);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static T Get(IFilterBy<T> filter, SortBy<T> sort = null)
			=> RepositoryBase<T>.Get(filter, sort, null);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<T> GetAsync(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.GetAsync(context, dataSource, filter, sort, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static async Task<T> GetAsync(DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryBase<T>.GetAsync(context, dataSource, filter, sort, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<T> GetAsync(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.GetAsync(context, aliasTypeName, filter, sort, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<T> GetAsync(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.GetAsync(aliasTypeName, filter, sort, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<T> GetAsync(IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.GetAsync("", filter, sort, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<T> GetAsync(IFilterBy<T> filter, SortBy<T> sort, CancellationToken cancellationToken)
			=> RepositoryBase<T>.GetAsync(filter, sort, null, cancellationToken);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<T> GetAsync(IFilterBy<T> filter, CancellationToken cancellationToken)
			=> RepositoryBase<T>.GetAsync(filter, null, cancellationToken);
		#endregion

		#region [Static] Replace
		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace(RepositoryContext context, DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null)
			=> RepositoryMediator.Replace(context, dataSource, @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace(RepositoryContext context, DataSource dataSource, T @object, string userID = null)
			=> RepositoryBase<T>.Replace(context, dataSource, @object, false, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace(DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null)
		{
			using (var context = new RepositoryContext())
				RepositoryBase<T>.Replace(context, dataSource, @object, dontCreateNewVersion, userID);
		}

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace(DataSource dataSource, T @object, string userID = null)
			=> RepositoryBase<T>.Replace(dataSource, @object, false, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace(RepositoryContext context, string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null)
			=> RepositoryMediator.Replace(context, aliasTypeName, @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace(RepositoryContext context, string aliasTypeName, T @object, string userID = null)
			=> RepositoryBase<T>.Replace(context, aliasTypeName, @object, false, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace(string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null)
			=> RepositoryMediator.Replace(aliasTypeName, @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace(string aliasTypeName, T @object, string userID = null)
			=> RepositoryBase<T>.Replace(aliasTypeName, @object, false, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace(T @object, bool dontCreateNewVersion, string userID = null)
			=> RepositoryBase<T>.Replace("", @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace(T @object, string userID = null)
			=> RepositoryBase<T>.Replace(@object, false, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync(RepositoryContext context, DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.ReplaceAsync(context, dataSource, @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync(RepositoryContext context, DataSource dataSource, T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.ReplaceAsync(context, dataSource, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task ReplaceAsync(DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext())
				await RepositoryBase<T>.ReplaceAsync(context, dataSource, @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync(DataSource dataSource, T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.ReplaceAsync(dataSource, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync(RepositoryContext context, string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.ReplaceAsync(context, aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync(RepositoryContext context, string aliasTypeName, T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.ReplaceAsync(context, aliasTypeName, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync(string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.ReplaceAsync(aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync(string aliasTypeName, T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.ReplaceAsync(aliasTypeName, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync(T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.ReplaceAsync("", @object, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync(T @object, bool dontCreateNewVersion, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.ReplaceAsync("", @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync(T @object, bool dontCreateNewVersion, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.ReplaceAsync(@object, dontCreateNewVersion, null, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync(T @object, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.ReplaceAsync(@object, false, cancellationToken);
		#endregion

		#region [Protected] Replace
		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		protected virtual void Replace(RepositoryContext context, string aliasTypeName, string userID = null)
			=> RepositoryBase<T>.Replace(context, aliasTypeName, this as T, userID);

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		protected virtual void Replace(bool dontCreateNewVersion, string userID = null)
			=> RepositoryBase<T>.Replace(this as T, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		protected virtual void Replace(string aliasTypeName = null, string userID = null)
		{
			using (var context = new RepositoryContext())
				this.Replace(context, aliasTypeName, userID);
		}

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task ReplaceAsync(RepositoryContext context, string aliasTypeName, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.ReplaceAsync(context, aliasTypeName, this as T, userID, cancellationToken);

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task ReplaceAsync(bool dontCreateNewVersion, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.ReplaceAsync(this as T, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task ReplaceAsync(bool dontCreateNewVersion, CancellationToken cancellationToken = default)
			=> this.ReplaceAsync(dontCreateNewVersion, null, cancellationToken);

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task ReplaceAsync(CancellationToken cancellationToken)
			=> this.ReplaceAsync(false, null, cancellationToken);

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual async Task ReplaceAsync(string aliasTypeName = null, string userID = null, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext())
				await this.ReplaceAsync(context, aliasTypeName, userID, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region [Static] Update
		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update(RepositoryContext context, DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null)
			=> RepositoryMediator.Update(context, dataSource, @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update(RepositoryContext context, DataSource dataSource, T @object, string userID = null)
			=> RepositoryBase<T>.Update(context, dataSource, @object, false, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update(DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null)
		{
			using (var context = new RepositoryContext())
				RepositoryBase<T>.Update(context, dataSource, @object, dontCreateNewVersion, userID);
		}

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update(DataSource dataSource, T @object, string userID = null)
			=> RepositoryBase<T>.Update(dataSource, @object, false, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update(RepositoryContext context, string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null)
			=> RepositoryMediator.Update(context, aliasTypeName, @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update(RepositoryContext context, string aliasTypeName, T @object, string userID = null)
			=> RepositoryBase<T>.Update(context, aliasTypeName, @object, false, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update(string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null)
			=> RepositoryMediator.Update(aliasTypeName, @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update(string aliasTypeName, T @object, string userID = null)
			=> RepositoryBase<T>.Update(aliasTypeName, @object, false, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update(T @object, string userID = null)
			=> RepositoryBase<T>.Update("", @object, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update(T @object, bool dontCreateNewVersion, string userID = null)
			=> RepositoryBase<T>.Update("", @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(RepositoryContext context, DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.UpdateAsync(context, dataSource, @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(RepositoryContext context, DataSource dataSource, T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.UpdateAsync(context, dataSource, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task UpdateAsync(DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext())
				await RepositoryBase<T>.UpdateAsync(context, dataSource, @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(DataSource dataSource, T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.UpdateAsync(dataSource, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(RepositoryContext context, string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.UpdateAsync(context, aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(RepositoryContext context, string aliasTypeName, T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.UpdateAsync(context, aliasTypeName, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(string aliasTypeName, T @object, bool dontCreateNewVersion, string userID, bool processCache, CancellationToken cancellationToken = default)
			=> RepositoryMediator.UpdateAsync(aliasTypeName, @object, dontCreateNewVersion, userID, processCache, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(string aliasTypeName, T @object, bool dontCreateNewVersion, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.UpdateAsync(aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(string aliasTypeName, T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.UpdateAsync(aliasTypeName, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(T @object, string userID, bool processCache, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.UpdateAsync("", @object, false, userID, processCache, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.UpdateAsync("", @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(T @object, bool dontCreateNewVersion, string userID, bool processCache, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.UpdateAsync("", @object, dontCreateNewVersion, userID, processCache, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(T @object, bool dontCreateNewVersion, string userID, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.UpdateAsync(@object, dontCreateNewVersion, userID, true, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(T @object, bool dontCreateNewVersion, bool processCache, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.UpdateAsync(@object, dontCreateNewVersion, null, processCache, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync(T @object, bool dontCreateNewVersion, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.UpdateAsync(@object, dontCreateNewVersion, true, cancellationToken);
		#endregion

		#region [Protected] Update
		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		protected virtual void Update(RepositoryContext context, string aliasTypeName, string userID = null)
			=> RepositoryBase<T>.Update(context, aliasTypeName, this as T, userID);

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		protected virtual void Update(bool dontCreateNewVersion, string userID = null)
			=> RepositoryBase<T>.Update(this as T, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		protected virtual void Update(string aliasTypeName = null, string userID = null)
		{
			using (var context = new RepositoryContext())
				this.Update(context, aliasTypeName, userID);
		}

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task UpdateAsync(RepositoryContext context, string aliasTypeName, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.UpdateAsync(context, aliasTypeName, this as T, userID, cancellationToken);

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task UpdateAsync(bool dontCreateNewVersion, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.UpdateAsync(this as T, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task UpdateAsync(bool dontCreateNewVersion, CancellationToken cancellationToken = default)
			=> this.UpdateAsync(dontCreateNewVersion, null, cancellationToken);

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task UpdateAsync(CancellationToken cancellationToken)
			=> this.UpdateAsync(false, cancellationToken);

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual async Task UpdateAsync(string aliasTypeName = null, string userID = null, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext())
				await this.UpdateAsync(context, aliasTypeName, userID, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region [Static] Create version
		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		public static VersionContent CreateVersion(RepositoryContext context, DataSource dataSource, T @object, string userID = null)
			=> RepositoryMediator.CreateVersion(context, dataSource, @object, userID);

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		public static VersionContent CreateVersion(RepositoryContext context, T @object, string userID = null)
			=> RepositoryMediator.CreateVersion(context, @object, userID);

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		public static VersionContent CreateVersion(T @object, string userID = null)
			=> RepositoryMediator.CreateVersion(@object, userID);

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task<VersionContent> CreateVersionAsync(RepositoryContext context, DataSource dataSource, T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CreateVersionAsync(context, dataSource, @object, userID, cancellationToken);

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task<VersionContent> CreateVersionAsync(RepositoryContext context, T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CreateVersionAsync(context, @object, userID, cancellationToken);

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task<VersionContent> CreateVersionAsync(T @object, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CreateVersionAsync(@object, userID, cancellationToken);
		#endregion

		#region [Protected] Create version
		/// <summary>
		/// Creates new version of this object
		/// </summary>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		protected virtual VersionContent CreateVersion(string userID = null)
			=> RepositoryBase<T>.CreateVersion(this as T, userID);

		/// <summary>
		/// Creates new version of this object
		/// </summary>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task<VersionContent> CreateVersionAsync(string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CreateVersionAsync(this as T, userID, cancellationToken);
		#endregion

		#region [Static] Rollback version
		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		public static T Rollback(RepositoryContext context, VersionContent version, string userID)
			=> RepositoryMediator.Rollback<T>(context, version, userID);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		public static T Rollback(VersionContent version, string userID)
			=> RepositoryMediator.Rollback<T>(version, userID);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		public static T Rollback(RepositoryContext context, string versionID, string userID)
			=> RepositoryMediator.Rollback<T>(context, versionID, userID);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		public static T Rollback(string versionID, string userID)
			=> RepositoryMediator.Rollback<T>(versionID, userID);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> RollbackAsync(RepositoryContext context, VersionContent version, string userID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.RollbackAsync<T>(context, version, userID, cancellationToken);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> RollbackAsync(VersionContent version, string userID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.RollbackAsync<T>(version, userID, cancellationToken);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> RollbackAsync(RepositoryContext context, string versionID, string userID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.RollbackAsync<T>(context, versionID, userID, cancellationToken);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> RollbackAsync(string versionID, string userID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.RollbackAsync<T>(versionID, userID, cancellationToken);
		#endregion

		#region [Static] Count versions
		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <returns></returns>
		public static long CountVersions(string objectID)
			=> RepositoryMediator.CountVersionContents(objectID);

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountVersionsAsync(string objectID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CountVersionContentsAsync(objectID, cancellationToken);
		#endregion

		#region [Protected] Count versions
		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <returns></returns>
		protected long CountVersions()
		{
			this._totalVersions = string.IsNullOrWhiteSpace(this.ID)
				? 0
				: RepositoryBase<T>.CountVersions(this.ID);
			return this._totalVersions;
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected async Task<long> CountVersionsAsync(CancellationToken cancellationToken = default)
		{
			this._totalVersions = string.IsNullOrWhiteSpace(this.ID)
				? 0
				: await RepositoryBase<T>.CountVersionsAsync(this.ID, cancellationToken).ConfigureAwait(false);
			return this._totalVersions;
		}
		#endregion

		#region [Static] Find versions
		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersions(string objectID)
			=> RepositoryMediator.FindVersionContents(objectID);

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<VersionContent>> FindVersionsAsync(string objectID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.FindVersionContentsAsync(objectID, cancellationToken);
		#endregion

		#region [Protected] Find versions
		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <returns></returns>
		protected List<VersionContent> FindVersions()
			=> string.IsNullOrWhiteSpace(this.ID)
				? null
				: RepositoryBase<T>.FindVersions(this.ID);

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected Task<List<VersionContent>> FindVersionsAsync(CancellationToken cancellationToken = default)
			=> string.IsNullOrWhiteSpace(this.ID)
				? Task.FromResult<List<VersionContent>>(null)
				: RepositoryBase<T>.FindVersionsAsync(this.ID, cancellationToken);
		#endregion

		#region [Static] Delete
		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static void Delete(RepositoryContext context, DataSource dataSource, string id, string userID = null)
			=> RepositoryMediator.Delete<T>(context, dataSource, id, userID);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static void Delete(DataSource dataSource, string id, string userID = null)
		{
			using (var context = new RepositoryContext())
				RepositoryBase<T>.Delete(context, dataSource, id, userID);
		}

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static void Delete(RepositoryContext context, string aliasTypeName, string id, string userID = null)
			=> RepositoryMediator.Delete<T>(context, aliasTypeName, id, userID);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static void Delete(string aliasTypeName, string id, string userID = null)
			=> RepositoryMediator.Delete<T>(aliasTypeName, id, userID);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static void Delete(string id, string userID = null)
			=> RepositoryBase<T>.Delete("", id, userID);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync(RepositoryContext context, DataSource dataSource, string id, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.DeleteAsync<T>(context, dataSource, id, userID, cancellationToken);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task DeleteAsync(DataSource dataSource, string id, string userID = null, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext())
				await RepositoryBase<T>.DeleteAsync(context, dataSource, id, userID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync(RepositoryContext context, string aliasTypeName, string id, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.DeleteAsync<T>(context, aliasTypeName, id, userID, cancellationToken);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync(string aliasTypeName, string id, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.DeleteAsync<T>(aliasTypeName, id, userID, cancellationToken);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync(string id, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.DeleteAsync("", id, userID, cancellationToken);
		#endregion

		#region [Protected] Delete
		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		protected virtual void Delete(RepositoryContext context, string aliasTypeName, string userID = null)
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
				RepositoryBase<T>.Delete(context, aliasTypeName, this.ID, userID);
		}

		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task DeleteAsync(RepositoryContext context, string aliasTypeName, string userID = null, CancellationToken cancellationToken = default)
			=> !string.IsNullOrWhiteSpace(this.ID)
				? RepositoryBase<T>.DeleteAsync(context, aliasTypeName, this.ID, userID, cancellationToken)
				: Task.FromException(new ArgumentException("The identity of the object is null or empty", nameof(this.ID)));
		#endregion

		#region [Static] Delete many
		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null)
			=> RepositoryMediator.DeleteMany(context, dataSource, filter, businessRepositoryEntityID);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany(DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null)
		{
			using (var context = new RepositoryContext())
				RepositoryBase<T>.DeleteMany(context, dataSource, filter, businessRepositoryEntityID);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID = null)
			=> RepositoryMediator.DeleteMany(context, aliasTypeName, filter, businessRepositoryEntityID);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany(string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID = null)
			=> RepositoryMediator.DeleteMany(aliasTypeName, filter, businessRepositoryEntityID);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany(IFilterBy<T> filter, string businessRepositoryEntityID = null)
			=> RepositoryBase<T>.DeleteMany("", filter, businessRepositoryEntityID);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteManyAsync(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.DeleteManyAsync(context, dataSource, filter, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task DeleteManyAsync(DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext())
				await RepositoryBase<T>.DeleteManyAsync(context, dataSource, filter, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteManyAsync(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.DeleteManyAsync(context, aliasTypeName, filter, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteManyAsync(string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.DeleteManyAsync(aliasTypeName, filter, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteManyAsync(IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.DeleteManyAsync("", filter, businessRepositoryEntityID, cancellationToken);
		#endregion

		#region [Static] Find
		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0)
			=> RepositoryMediator.Find(context, dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0)
		{
			using (var context = new RepositoryContext(false))
				return RepositoryBase<T>.Find(context, dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, bool processCache = true, string cacheKey = null, int cacheTime = 0)
			=> RepositoryMediator.Find(aliasTypeName, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, processCache, cacheKey, cacheTime);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0)
			=> RepositoryMediator.Find(aliasTypeName, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0)
			=> RepositoryBase<T>.Find("", filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, string cacheKey)
			=> RepositoryBase<T>.Find(filter, sort, pageSize, pageNumber, businessRepositoryEntityID, true, cacheKey, 0);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, bool processCache, string cacheKey)
			=> RepositoryMediator.Find("", filter, sort, pageSize, pageNumber, null, true, processCache, cacheKey, 0);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string cacheKey)
			=> RepositoryBase<T>.Find(filter, sort, pageSize, pageNumber, true, cacheKey);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(IFilterBy<T> filter, SortBy<T> sort, bool processCache, string cacheKey)
			=> RepositoryBase<T>.Find(filter, sort, 0, 1, processCache, cacheKey);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(IFilterBy<T> filter = null, SortBy<T> sort = null, string cacheKey = null)
			=> RepositoryBase<T>.Find(filter, sort, true, cacheKey);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default)
			=> RepositoryMediator.FindAsync(context, dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static async Task<List<T>> FindAsync(DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryBase<T>.FindAsync(context, dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, bool processCache, string cacheKey, int cacheTime, CancellationToken cancellationToken)
			=> RepositoryMediator.FindAsync(aliasTypeName, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, processCache, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey, int cacheTime, CancellationToken cancellationToken)
			=> RepositoryBase<T>.FindAsync(aliasTypeName, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, true, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, bool processCache, string cacheKey, int cacheTime, CancellationToken cancellationToken)
			=> RepositoryBase<T>.FindAsync("", filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, processCache, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey, int cacheTime, CancellationToken cancellationToken)
			=> RepositoryBase<T>.FindAsync(filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, true, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, bool processCache, string cacheKey, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.FindAsync(filter, sort, pageSize, pageNumber, businessRepositoryEntityID, true, processCache, cacheKey, 0, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, string cacheKey, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.FindAsync(filter, sort, pageSize, pageNumber, businessRepositoryEntityID, true, cacheKey, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, bool processCache, string cacheKey, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.FindAsync(filter, sort, pageSize, pageNumber, null, processCache, cacheKey, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string cacheKey, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.FindAsync(filter, sort, pageSize, pageNumber, true, cacheKey, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, bool processCache, string cacheKey, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.FindAsync(filter, sort, 0, 1, null, processCache, cacheKey, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter = null, SortBy<T> sort = null, string cacheKey = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.FindAsync(filter, sort, true, cacheKey, cancellationToken);
		#endregion

		#region [Static] Count
		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, bool processCache = true, string cacheKey = null, int cacheTime = 0)
			=> RepositoryMediator.Count(context, dataSource, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, processCache, cacheKey, cacheTime);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0)
			=> RepositoryBase<T>.Count(context, dataSource, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, true, cacheKey, cacheTime);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0)
		{
			using (var context = new RepositoryContext(false))
				return RepositoryBase<T>.Count(context, dataSource, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, bool processCache = true, string cacheKey = null, int cacheTime = 0)
			=> RepositoryMediator.Count(aliasTypeName, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, processCache, cacheKey, cacheTime);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0)
			=> RepositoryBase<T>.Count(aliasTypeName, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, true, cacheKey, cacheTime);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0)
			=> RepositoryBase<T>.Count("", filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(IFilterBy<T> filter, string businessRepositoryEntityID, string cacheKey = null)
			=> RepositoryBase<T>.Count(filter, businessRepositoryEntityID, true, cacheKey, 0);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, bool processCache, string cacheKey = null, int cacheTime = 0)
			=> RepositoryBase<T>.Count("", filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, processCache, cacheKey, cacheTime);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(IFilterBy<T> filter, string businessRepositoryEntityID, bool processCache, string cacheKey = null)
			=> RepositoryBase<T>.Count(filter, businessRepositoryEntityID, true, processCache, cacheKey);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(IFilterBy<T> filter, bool processCache, string cacheKey = null)
			=> RepositoryBase<T>.Count(filter, null, processCache, cacheKey);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(IFilterBy<T> filter = null, string cacheKey = null)
			=> RepositoryBase<T>.Count(filter, true, cacheKey);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, bool processCache = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CountAsync(context, dataSource, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, processCache, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CountAsync(context, dataSource, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, true, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static async Task<long> CountAsync(DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryBase<T>.CountAsync(context, dataSource, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, bool processCache = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CountAsync(aliasTypeName, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, processCache, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CountAsync(aliasTypeName, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, true, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CountAsync("", filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter, string businessRepositoryEntityID, string cacheKey = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CountAsync(filter, businessRepositoryEntityID, true, cacheKey, 0, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter, string businessRepositoryEntityID, bool autoAssociateWithMultipleParents, bool processCache = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CountAsync("", filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, processCache, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter, string businessRepositoryEntityID, bool processCache = true, string cacheKey = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CountAsync(filter, businessRepositoryEntityID, true, processCache, cacheKey, 0, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="processCache">true to process cache first</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter, bool processCache, string cacheKey, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CountAsync("", filter, null, true, processCache, cacheKey, 0, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter = null, string cacheKey = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CountAsync(filter, true, cacheKey, cancellationToken);
		#endregion

		#region [Static] Search
		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Search(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID)
			=> RepositoryMediator.Search(context, dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Search(DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID)
		{
			using (var context = new RepositoryContext(false))
				return RepositoryBase<T>.Search(context, dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Search(string aliasTypeName, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID)
			=> RepositoryMediator.Search(aliasTypeName, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Search(string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID)
			=> RepositoryBase<T>.Search("", query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> SearchAsync(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.SearchAsync(context, dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static async Task<List<T>> SearchAsync(DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryBase<T>.SearchAsync(context, dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> SearchAsync(string aliasTypeName, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.SearchAsync(aliasTypeName, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> SearchAsync(string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.SearchAsync("", query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> SearchAsync(string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.SearchAsync("", query, filter, sort, pageSize, pageNumber, null, cancellationToken);
		#endregion

		#region [Static] Count (searching)
		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessRepositoryEntityID)
			=> RepositoryMediator.Count(context, dataSource, query, filter, businessRepositoryEntityID);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(DataSource dataSource, string query, IFilterBy<T> filter, string businessRepositoryEntityID)
		{
			using (var context = new RepositoryContext(false))
				return RepositoryBase<T>.Count(context, dataSource, query, filter, businessRepositoryEntityID);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(string aliasTypeName, string query, IFilterBy<T> filter, string businessRepositoryEntityID)
			=> RepositoryMediator.Count(aliasTypeName, query, filter, businessRepositoryEntityID);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(string query, IFilterBy<T> filter = null, string businessRepositoryEntityID = null)
			=> RepositoryBase<T>.Count("", query, filter, businessRepositoryEntityID);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessRepositoryEntityID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CountAsync(context, dataSource, query, filter, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static async Task<long> CountAsync(DataSource dataSource, string query, IFilterBy<T> filter, string businessRepositoryEntityID, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryBase<T>.CountAsync(context, dataSource, query, filter, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(string aliasTypeName, string query, IFilterBy<T> filter, string businessRepositoryEntityID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CountAsync(aliasTypeName, query, filter, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(string query, IFilterBy<T> filter, string businessRepositoryEntityID, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CountAsync("", query, filter, businessRepositoryEntityID, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(string query, IFilterBy<T> filter = null, CancellationToken cancellationToken = default)
			=> RepositoryBase<T>.CountAsync("", query, filter, null, cancellationToken);
		#endregion

		#region [Static] Sync to other data sources
		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <param name="context">The context</param>
		/// <param name="object">The object</param>
		public static void Sync(RepositoryContext context, T @object)
			=> RepositoryMediator.Sync(context, @object);

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <param name="aliasTypeName">The alias type name</param>
		/// <param name="object">The object</param>
		public static void Sync(string aliasTypeName, T @object)
			=> RepositoryMediator.Sync(aliasTypeName, @object);

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <param name="context">The context</param>
		/// <param name="object">The object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task SyncAsync(RepositoryContext context, T @object, CancellationToken cancellationToken = default)
			=> RepositoryMediator.SyncAsync(context, @object, cancellationToken);

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <param name="aliasTypeName">The alias type name</param>
		/// <param name="object">The object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task SyncAsync(string aliasTypeName, T @object, CancellationToken cancellationToken = default)
			=> RepositoryMediator.SyncAsync(aliasTypeName, @object, cancellationToken);
		#endregion

		#region [Public] Generate form controls
		/// <summary>
		/// Generates the form controls of this type
		/// </summary>
		public static JToken GenerateFormControls()
			=> RepositoryMediator.GenerateFormControls<T>();
		#endregion

		#region [Public] Get/Set properties
		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="value">The value of the property</param>
		/// <returns>true if the property is getted; otherwise false;</returns>
		public virtual bool TryGetProperty(string name, out object value)
		{
			value = null;
			try
			{
				var attributes = this.GetPublicProperties(attr => attr.CanRead).ToDictionary(attr => attr.Name, StringComparer.OrdinalIgnoreCase);
				if (attributes.TryGetValue(name, out var attribute))
				{
					value = this.GetAttributeValue(attribute);
					return true;
				}
				else if (this is IBusinessEntity && this.ExtendedProperties != null && this.ExtendedProperties.TryGetValue(name, out var extValue))
				{
					value = extValue;
					return true;
				}
			}
			catch (Exception ex)
			{
				RepositoryMediator.WriteLogs($"Error occurred while getting value of a property [{name}] => {ex.Message}", ex);
			}
			return false;
		}

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <returns></returns>
		public override object GetProperty(string name)
			=> this.TryGetProperty(name, out object value) ? value : null;

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <typeparam name="TValue"></typeparam>
		/// <param name="name">The name of the property</param>
		/// <param name="value">The value of the property</param>
		/// <returns>true if the property is getted; otherwise false;</returns>
		public virtual bool TryGetProperty<TValue>(string name, out TValue value)
		{
			// get value & cast
			if (this.TryGetProperty(name, out object theValue))
			{
				value = theValue != null ? theValue.CastAs<TValue>() : default;
				return true;
			}

			// default
			value = default;
			return false;
		}

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <returns></returns>
		public virtual TValue GetProperty<TValue>(string name)
			=> this.TryGetProperty(name, out TValue value) ? value : default;

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <returns>true if the property is setted; otherwise false;</returns>
		public virtual bool TrySetProperty(string name, object value)
		{
			try
			{
				var attributes = this.GetPublicProperties(attr => attr.CanWrite).ToDictionary(attr => attr.Name);
				if (attributes.TryGetValue(name, out var attribute))
				{
					this.SetAttributeValue(attribute, value, true);
					return true;
				}
				else if (this is IBusinessEntity && this.ExtendedProperties != null)
				{
					this.ExtendedProperties[name] = value;
					this.NotifyPropertyChanged(name);
					return true;
				}
			}
			catch (Exception ex)
			{
				RepositoryMediator.WriteLogs($"Error occurred while setting value of a property [{name}] => {ex.Message}", ex);
			}
			return false;
		}

		/// <summary>
		/// Sets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="value">The value of the property</param>
		public override void SetProperty(string name, object value)
			=> this.TrySetProperty(name, value);
		#endregion

		#region [Public] JSON/XML conversions
		/// <summary>
		/// Serializes this object to JSON object
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (named with suffix '$type')</param>
		/// <param name="onCompleted">The action to run when complete</param>
		/// <returns></returns>
		public override JObject ToJson(bool addTypeOfExtendedProperties = false, Action<JObject> onCompleted = null)
		{
			// standard properties
			var json = (this as T).ToJson() as JObject;

			// extended properties
			if (this is IBusinessEntity && this.ExtendedProperties != null && !string.IsNullOrWhiteSpace(this.RepositoryEntityID) && RepositoryMediator.GetEntityDefinition<T>().BusinessRepositoryEntities.TryGetValue(this.RepositoryEntityID, out var repositoryEntity) && repositoryEntity != null && repositoryEntity.ExtendedPropertyDefinitions != null)
				repositoryEntity.ExtendedPropertyDefinitions.ForEach(definition =>
				{
					if (this.ExtendedProperties.TryGetValue(definition.Name, out var value) && value != null)
						value = value is string @string
							? (definition.Mode.Equals(ExtendedPropertyMode.Select) || definition.Mode.Equals(ExtendedPropertyMode.Lookup)) && @string.Contains(";")
								? @string.ToArray(@string.Contains("#;") ? "#;" : ";", true).ToJArray() as object
								: @string
							: value is RepositoryBase repositoryObject
								? repositoryObject.ToJson(addTypeOfExtendedProperties, null)
								: value.ToJson();
					json[definition.Name] = value?.ToJson();
					if (addTypeOfExtendedProperties && value != null)
					{
						var type = value.GetType();
						json[$"{definition.Name}$type"] = type.IsPrimitiveType() ? type.ToString() : type.GetTypeName();
					}
				});

			// privileges
			if (this.WorkingPrivileges != null && !this.WorkingPrivileges.IsInheritFromParent() && json["Privileges"] == null)
				json["Privileges"] = JObject.FromObject(this.WorkingPrivileges);

			if (this.OriginalPrivileges != null && !this.OriginalPrivileges.IsInheritFromParent() && json["OriginalPrivileges"] == null)
				json["OriginalPrivileges"] = JObject.FromObject(this.OriginalPrivileges);

			// system management properties
			if (!string.IsNullOrWhiteSpace(this.SystemID))
				json["SystemID"] = new JValue(this.SystemID);

			if (!string.IsNullOrWhiteSpace(this.RepositoryID))
				json["RepositoryID"] = new JValue(this.RepositoryID);

			if (!string.IsNullOrWhiteSpace(this.RepositoryEntityID))
				json["RepositoryEntityID"] = new JValue(this.RepositoryEntityID);

			onCompleted?.Invoke(json);
			return json;
		}

		/// <summary>
		/// Parses the JSON object and copy values into this object
		/// </summary>
		/// <param name="json">The JSON object that contains information</param>
		/// <param name="onCompleted">The action to run when complete</param>
		public override void ParseJson(JObject json, Action<JObject> onCompleted = null)
		{
			if (json != null)
			{
				this.CopyFrom(json);
				onCompleted?.Invoke(json);
			}
		}

		/// <summary>
		/// Serializes this object to XML object
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (attribute named '$type')</param>
		/// <param name="onCompleted">The action to run when complete</param>
		/// <returns></returns>
		public override XElement ToXml(bool addTypeOfExtendedProperties = false, Action<XElement> onCompleted = null)
			=> this.ToXml(addTypeOfExtendedProperties, "", onCompleted);

		/// <summary>
		/// Serializes this object to XML object
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (attribute named '$type')</param>
		/// <param name="cultureName">The culture name to format date-time and number, ex: vi-VN, en-US, ...</param>
		/// <param name="onCompleted">The action to run when complete</param>
		/// <returns></returns>
		public virtual XElement ToXml(bool addTypeOfExtendedProperties, string cultureName, Action<XElement> onCompleted = null)
			=> this.ToXml(addTypeOfExtendedProperties, string.IsNullOrWhiteSpace(cultureName) ? CultureInfo.GetCultureInfo("vi-VN") : CultureInfo.GetCultureInfo(cultureName) ?? CultureInfo.GetCultureInfo("vi-VN"), onCompleted);

		/// <summary>
		/// Serializes this object to XML object
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (attribute named '$type')</param>
		/// <param name="cultureInfo">The culture information to format date-time and number</param>
		/// <param name="onCompleted">The action to run when complete</param>
		/// <returns></returns>
		public virtual XElement ToXml(bool addTypeOfExtendedProperties, CultureInfo cultureInfo, Action<XElement> onCompleted = null)
		{
			// standard properties
			var xml = (this as T).ToXml();
			this.GetPublicAttributes(attribute => attribute.IsDateTimeType() || attribute.IsNumericType()).ForEach(attribute =>
			{
				var element = xml.Element(attribute.Name);
				if (attribute.IsDateTimeType())
					element.UpdateDateTime(cultureInfo);
				else
					element.UpdateNumber(attribute.IsFloatingPointType(), cultureInfo);
			});

			// extended properties
			if (this is IBusinessEntity && this.ExtendedProperties != null && !string.IsNullOrWhiteSpace(this.RepositoryEntityID) && RepositoryMediator.GetEntityDefinition<T>().BusinessRepositoryEntities.TryGetValue(this.RepositoryEntityID, out var repositoryEntity))
				repositoryEntity?.ExtendedPropertyDefinitions?.ForEach(definition =>
				{
					if (this.ExtendedProperties.TryGetValue(definition.Name, out var value) && value != null)
						value = value is string @string
							? (definition.Mode.Equals(ExtendedPropertyMode.Select) || definition.Mode.Equals(ExtendedPropertyMode.Lookup)) && @string.Contains(";")
								? @string.ToArray(@string.Contains("#;") ? "#;" : ";", true).Select(val => new XElement("Value", val)).ToArray() as object
								: @string
							: value is RepositoryBase repositoryObject
								? repositoryObject.ToXml(addTypeOfExtendedProperties, onCompleted)
								: value.IsClassType()
									? value.ToXml(onCompleted)
									: value;
					var element = new XElement(definition.Name, value);
					if (value != null)
					{
						var type = value.GetType();
						if (addTypeOfExtendedProperties)
							element.Add(new XAttribute("$type", type.IsPrimitiveType() ? type.ToString() : type.GetTypeName()));
						if (type.IsDateTimeType())
							element.UpdateDateTime(cultureInfo);
						else if (type.IsNumericType())
							element.UpdateNumber(type.IsFloatingPointType(), cultureInfo);
					}
					xml.Add(element);
				});

			/*
			// privileges
			if (this.WorkingPrivileges != null && !this.WorkingPrivileges.IsInheritFromParent() && xml.Element("Privileges") == null)
				xml.Add(new XElement("Privileges", this.WorkingPrivileges.ToXml()));

			if (this.OriginalPrivileges != null && !this.OriginalPrivileges.IsInheritFromParent() && xml.Element("OriginalPrivileges") == null)
				xml.Add(new XElement("OriginalPrivileges", this.OriginalPrivileges.ToXml()));
			*/

			// system management properties
			if (!string.IsNullOrWhiteSpace(this.SystemID))
				xml.Add(new XElement("SystemID", this.SystemID));

			if (!string.IsNullOrWhiteSpace(this.RepositoryID))
				xml.Add(new XElement("RepositoryID", this.RepositoryID));

			if (!string.IsNullOrWhiteSpace(this.RepositoryEntityID))
				xml.Add(new XElement("RepositoryEntityID", this.RepositoryEntityID));

			onCompleted?.Invoke(xml);
			return xml;
		}

		/// <summary>
		/// Parses the XML object and copy values into this object
		/// </summary>
		/// <param name="xml">The XML object that contains information</param>
		/// <param name="onCompleted">The action to run when complete</param>
		public override void ParseXml(XContainer xml, Action<XContainer> onCompleted = null)
		{
			if (xml != null)
			{
				this.CopyFrom(xml.FromXml<T>());
				onCompleted?.Invoke(xml);
			}
		}
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	#region Trash & Version
	public class TrashContent
	{
		public TrashContent() { }

		/// <summary>
		/// Gets or sets the identity
		/// </summary>
		[BsonId(IdGenerator = typeof(IdentityGenerator))]
		public string ID { get; set; }

		/// <summary>
		/// Gets or sets the title
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// Gets or sets the name of service that associates with
		/// </summary>
		[BsonIgnoreIfNull]
		public string ServiceName { get; set; }

		/// <summary>
		/// Gets or sets the identity of system that associates with
		/// </summary>
		[BsonIgnoreIfNull]
		public string SystemID { get; set; }

		/// <summary>
		/// Gets or sets the identity of business repository (mean business module) that associates with
		/// </summary>
		[BsonIgnoreIfNull]
		public string RepositoryID { get; set; }

		/// <summary>
		/// Gets or sets the identity of business repository entity (mean business content-type) that associates with
		/// </summary>
		[BsonIgnoreIfNull]
		public string RepositoryEntityID { get; set; }

		/// <summary>
		/// Gets or sets the create time
		/// </summary>
		[Sortable(IndexName = "Time", ExpireAfter = 2592000, Reverse = true)]
		public DateTime Created { get; set; }

		/// <summary>
		/// Gets or sets the identity of user
		/// </summary>
		public string CreatedID { get; set; }

		/// <summary>
		/// Gets the raw data of object (compressed bytes in Base64 string)
		/// </summary>
		public string Data { get; internal set; }

		protected object _Object = null;

		/// <summary>
		/// Gets the original object
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnore, MessagePackIgnore]
		public object Object
		{
			get => this._Object ?? (this._Object = string.IsNullOrWhiteSpace(this.Data) ? null : Caching.Helper.Deserialize(this.Data.Base64ToBytes().Decompress()));
			internal set
			{
				this._Object = value;
				this.Data = this._Object != null
					? Caching.Helper.Serialize(this._Object).Compress().ToBase64()
					: null;
			}
		}

		internal static long Count<T>(DataSource dataSource, string name, IFilterBy<T> filter) where T : class
		{
			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				return collection.CountDocuments(filter?.GetNoSqlStatement() ?? Builders<T>.Filter.Empty);
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					var (Statement, Parameters) = filter != null ? filter.GetSqlStatement() : (null, null);
					var command = connection.CreateCommand($"COUNT (ID) AS Total FROM T_Data_{name}" + (Statement != null ? " WHERE " + Statement : ""), Parameters?.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList());
					return command.ExecuteScalar().CastAs<long>();
				}
			}
			return 0;
		}

		internal static async Task<long> CountAsync<T>(DataSource dataSource, string name, IFilterBy<T> filter, CancellationToken cancellationToken = default) where T : class
		{
			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				return await collection.CountDocumentsAsync(filter?.GetNoSqlStatement() ?? Builders<T>.Filter.Empty, null, cancellationToken).ConfigureAwait(false);
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					var (Statement, Parameters) = filter != null ? filter.GetSqlStatement() : (null, null);
					var command = connection.CreateCommand($"COUNT (ID) AS Total FROM T_Data_{name}" + (Statement != null ? " WHERE " + Statement : ""), Parameters?.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList());
					return (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).CastAs<long>();
				}
			}
			return 0;
		}

		internal static List<T> Find<T>(DataSource dataSource, string name, IFilterBy<T> filter, SortBy<T> sort, int pageSize = 0, int pageNumber = 1) where T : class
		{
			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				return collection.Find(filter?.GetNoSqlStatement(), sort?.GetNoSqlStatement(), pageSize, pageNumber);
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var type = typeof(T);
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					var (Statement, Parameters) = filter != null ? filter.GetSqlStatement() : (null, null);
					var statement = $"SELECT * FROM T_Data_{name}{(Statement != null ? " WHERE " + Statement : "")}{(sort != null ? " ORDER BY " + sort.GetSqlStatement() : "")}";

					DataTable dataTable = null;
					if (pageSize == 0)
					{
						var command = connection.CreateCommand(statement, Parameters?.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList());
						using (var dataReader = command.ExecuteReader())
						{
							dataTable = dataReader.ToDataTable<T>();
						}
					}
					else
					{
						var dataSet = new DataSet();
						var dataAdapter = dbProviderFactory.CreateDataAdapter();
						dataAdapter.SelectCommand = connection.CreateCommand(statement, Parameters?.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList());
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, type.GetTypeName(true));
						dataTable = dataSet.Tables[0];
					}

					return dataTable.Rows
						.ToList()
						.Select(dataRow =>
						{
							var @object = type.CreateInstance<T>();
							for (var index = 0; index < dataRow.Table.Columns.Count; index++)
								try
								{
									@object.SetAttributeValue(dataRow.Table.Columns[index].ColumnName, dataRow[name]);
								}
								catch { }
							return @object;
						})
						.ToList();
				}
			}
			return null;
		}

		internal static async Task<List<T>> FindAsync<T>(DataSource dataSource, string name, IFilterBy<T> filter, SortBy<T> sort, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
		{
			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				return await collection.FindAsync(filter?.GetNoSqlStatement(), sort?.GetNoSqlStatement(), pageSize, pageNumber, null, cancellationToken).ConfigureAwait(false);
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var type = typeof(T);
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					var (Statement, Parameters) = filter != null ? filter.GetSqlStatement() : (null, null);
					var statement = $"SELECT * FROM T_Data_{name}{(Statement != null ? " WHERE " + Statement : "")}{(sort != null ? " ORDER BY " + sort.GetSqlStatement() : "")}";

					DataTable dataTable = null;
					if (pageSize == 0)
					{
						var command = connection.CreateCommand(statement, Parameters?.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList());
						using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
						{
							dataTable = await dataReader.ToDataTableAsync<T>(cancellationToken).ConfigureAwait(false);
						}
					}
					else
					{
						var dataSet = new DataSet();
						var dataAdapter = dbProviderFactory.CreateDataAdapter();
						dataAdapter.SelectCommand = connection.CreateCommand(statement, Parameters?.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList());
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, type.GetTypeName(true));
						dataTable = dataSet.Tables[0];
					}

					return dataTable.Rows
						.ToList()
						.Select(dataRow =>
						{
							var @object = type.CreateInstance<T>();
							for (var index = 0; index < dataRow.Table.Columns.Count; index++)
								try
								{
									@object.SetAttributeValue(dataRow.Table.Columns[index].ColumnName, dataRow[name]);
								}
								catch { }
							return @object;
						})
						.ToList();
				}
			}
			return null;
		}

		internal static T Create<T>(DataSource dataSource, string name, T @object) where T : class
		{
			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				collection.Create(@object);
				return @object;
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					var attributes = RepositoryMediator.GetPublicProperties<T>()
						.Where(attribute => attribute.Info.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Length < 0)
						.ToList();
					var command = connection.CreateCommand(
						$"INSERT INTO T_Data_{name} ({string.Join(", ", attributes.Select(attribute => attribute.Name))}) VALUES ({string.Join(", ", attributes.Select(attribute => "@" + attribute.Name))})",
						attributes.Select(attribute => dbProviderFactory.CreateParameter(attribute.Name, SqlHelper.DbTypes[attribute.Name.EndsWith("ID") ? typeof(char) : attribute.Info.GetType()], @object.GetAttributeValue(attribute))).ToList()
					);
					command.ExecuteNonQuery();
				}
				return @object;
			}
			return null;
		}

		internal static async Task<T> CreateAsync<T>(DataSource dataSource, string name, T @object, CancellationToken cancellationToken = default) where T : class
		{
			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				await collection.CreateAsync(@object, null, cancellationToken).ConfigureAwait(false);
				return @object;
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					var attributes = RepositoryMediator.GetPublicProperties<T>()
						.Where(attribute => attribute.Info.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Length < 0)
						.ToList();
					var command = connection.CreateCommand(
						$"INSERT INTO T_Data_{name} ({string.Join(", ", attributes.Select(attribute => attribute.Name))}) VALUES ({string.Join(", ", attributes.Select(attribute => "@" + attribute.Name))})",
						attributes.Select(attribute => dbProviderFactory.CreateParameter(attribute.Name, SqlHelper.DbTypes[attribute.Name.EndsWith("ID") ? typeof(char) : attribute.Info.GetType()], @object.GetAttributeValue(attribute))).ToList()
					);
					await command.ExecuteNonQueryAsync(cancellationToken);
				}
				return @object;
			}
			return null;
		}

		internal static void Delete<T>(DataSource dataSource, string name, IFilterBy<T> filter) where T : class
		{
			if (dataSource == null)
				throw new ArgumentNullException(nameof(dataSource), "Data source is invalid");

			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				collection.DeleteMany(filter?.GetNoSqlStatement() ?? Builders<T>.Filter.Empty);
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					var (Statement, Parameters) = filter != null ? filter.GetSqlStatement() : (null, null);
					var command = connection.CreateCommand(
						$"DELETE FROM T_Data_{name}{(Statement != null ? " WHERE " + Statement : "")}",
						Parameters?.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList()
					);
					command.ExecuteNonQuery();
				}
			}
		}

		internal static async Task DeleteAsync<T>(DataSource dataSource, string name, IFilterBy<T> filter, CancellationToken cancellationToken = default) where T : class
		{
			if (dataSource == null)
				throw new ArgumentNullException(nameof(dataSource), "Data source is invalid");

			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				await collection.DeleteManyAsync(filter?.GetNoSqlStatement() ?? Builders<T>.Filter.Empty, cancellationToken).ConfigureAwait(false);
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					var (Statement, Parameters) = filter != null ? filter.GetSqlStatement() : (null, null);
					var command = connection.CreateCommand(
						$"DELETE FROM T_Data_{name}{(Statement != null ? " WHERE " + Statement : "")}",
						Parameters?.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList()
					);
					await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
			}
		}

		internal static T GetByID<T>(DataSource dataSource, string name, string id) where T : class
		{
			if (dataSource == null)
				throw new ArgumentNullException(nameof(dataSource), "Data source is invalid");

			var @object = ObjectService.CreateInstance<T>();
			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				@object = collection.Get(Builders<T>.Filter.Eq("_id", id));
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					var command = connection.CreateCommand($"SELECT * T_Data_{name} WHERE ID=@ID", new[] { dbProviderFactory.CreateParameter("ID", DbType.StringFixedLength, id) }.ToList());
					using (var dataReader = command.ExecuteReader())
						@object = dataReader.Read() ? @object.Copy(dataReader, @object.GetPublicProperties().ToDictionary(attribute => attribute.Name, attribute => new AttributeInfo(attribute)), null) : null;
				}
			}
			return @object;
		}

		internal static async Task<T> GetByIDAsync<T>(DataSource dataSource, string name, string id, CancellationToken cancellationToken = default) where T : class
		{
			if (dataSource == null)
				throw new ArgumentNullException(nameof(dataSource), "Data source is invalid");

			var @object = ObjectService.CreateInstance<T>();
			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				@object = await collection.GetAsync(Builders<T>.Filter.Eq("_id", id), null, null, cancellationToken).ConfigureAwait(false);
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					var command = connection.CreateCommand($"SELECT * T_Data_{name} WHERE ID=@ID", new[] { dbProviderFactory.CreateParameter("ID", DbType.StringFixedLength, id) }.ToList());
					using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
						@object = await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false) ? @object.Copy(dataReader, @object.GetPublicProperties().ToDictionary(attribute => attribute.Name, attribute => new AttributeInfo(attribute)), null) : null;
				}
			}
			return @object;
		}

		internal static TrashContent Prepare<T>(T @object, Action<TrashContent> onCompleted = null) where T : class
		{
			var serviceName = @object.GetAttributeValue<string>("ServiceName");
			var systemID = @object.GetAttributeValue<string>("SystemID");
			var repositoryID = @object.GetAttributeValue<string>("RepositoryID");
			var repositoryEntityID = @object.GetAttributeValue<string>("RepositoryEntityID");
			var objectID = @object.GetEntityID();
			var title = @object.GetAttributeValue<string>("Title");

			if (string.IsNullOrWhiteSpace(title))
				title = typeof(T).GetTypeName(true) + "#" + objectID;

			var content = new TrashContent
			{
				ID = $"{typeof(T).GetTypeName()}#{objectID}".GenerateUUID(),
				Title = title,
				ServiceName = serviceName?.ToLower(),
				SystemID = systemID,
				RepositoryID = repositoryID,
				RepositoryEntityID = repositoryEntityID,
				Object = @object,
				Created = DateTime.Now,
				CreatedID = ""
			};
			onCompleted?.Invoke(content);
			return content;
		}
	}

	public class VersionContent : TrashContent
	{
		public VersionContent() { }

		/// <summary>
		/// Gets or sets the version number
		/// </summary>
		public int VersionNumber { get; set; }

		/// <summary>
		/// Gets or sets the identity of original object
		/// </summary>
		public string ObjectID { get; set; }

		internal static VersionContent Prepare<T>(T @object, Action<VersionContent> onCompleted = null) where T : class
		{
			var content = ObjectService.CreateInstance<VersionContent>().CopyFrom(TrashContent.Prepare(@object));
			content.ID = UtilityService.NewUUID;
			content.ObjectID = @object.GetEntityID();
			onCompleted?.Invoke(content);
			return content;
		}
	}
	#endregion

	//  --------------------------------------------------------------------------------------------

	#region Comparer of repository objects
	/// <summary>
	/// Presents the comparer to help repository objects work with LINQ
	/// </summary>
	public class RepositoryComparer<T> : IEqualityComparer<T> where T : class
	{
		/// <summary>
		/// Objects are equal if their identities are equal
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public virtual bool Equals(T x, T y)
			=> x != null && y != null && (ReferenceEquals(x, y) ? true : (x.GetEntityID() ?? "").IsEquals(y.GetEntityID()));

		/// <summary>
		/// If Equals() returns true for a pair of objects,  then GetHashCode() must return the same value for these objects
		/// </summary>
		/// <param name="object"></param>
		/// <returns></returns>
		public virtual int GetHashCode(T @object)
			=> @object == null ? -1 : (@object.GetEntityID() ?? "").GetHashCode();
	}
	#endregion

	//  --------------------------------------------------------------------------------------------

	#region Identity generator (for working with MongoDB)
	/// <summary>
	/// Generates identity as UUID (128 bits) for MongoDB documents
	/// </summary>
	public class IdentityGenerator : MongoDB.Bson.Serialization.IIdGenerator
	{
		public object GenerateId(object container, object document)
			=> UtilityService.NewUUID;

		public bool IsEmpty(object id)
			=> id == null || id.Equals(string.Empty);
	}
	#endregion

}