﻿#region Related components
using System;
using System.Linq;
using System.Data;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Xml.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

using net.vieapps.Components.Utility;
using net.vieapps.Components.Security;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Base class of an entity in the a repository
	/// </summary>
	[Serializable]
	public abstract class RepositoryBase
	{
		/// <summary>
		/// Initialize a new instance
		/// </summary>
		public RepositoryBase() { }

		#region Properties
		/// <summary>
		/// Gets or sets the object identity (primary-key)
		/// </summary>
		[BsonId(IdGenerator = typeof(IdentityGenerator)), PrimaryKey(MaxLength = 32)]
		public virtual string ID { get; set; } = "";

		/// <summary>
		/// Gets the score while searching
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, Ignore]
		public virtual double? SearchScore { get; set; } = null;
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
		/// <param name="onPreCompleted">The action to run on pre-completed</param>
		/// <returns></returns>
		public abstract JObject ToJson(bool addTypeOfExtendedProperties, Action<JObject> onPreCompleted);

		/// <summary>
		/// Serializes this object to JSON
		/// </summary>
		/// <returns></returns>
		public virtual JObject ToJson() => this.ToJson(false, null);

		/// <summary>
		/// Parses the JSON and copy values into this object
		/// </summary>
		/// <param name="json">The JSON object that contains information</param>
		/// <param name="onPreCompleted">The action to run on pre-completed</param>
		public abstract void ParseJson(JObject json, Action<JObject> onPreCompleted);

		/// <summary>
		/// Serializes this object to XML
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (attribute named '$type')</param>
		/// <param name="onPreCompleted">The action to run on pre-completed</param>
		/// <returns></returns>
		public abstract XElement ToXml(bool addTypeOfExtendedProperties, Action<XElement> onPreCompleted);

		/// <summary>
		/// Serializes this object to XML
		/// </summary>
		/// <returns></returns>
		public virtual XElement ToXml() => this.ToXml(false, null);

		/// <summary>
		/// Parses the XML and copy values into this object
		/// </summary>
		/// <param name="xml">The XML object that contains information</param>
		/// <param name="onPreCompleted">The action to run on pre-completed</param>
		public abstract void ParseXml(XContainer xml, Action<XContainer> onPreCompleted);
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Base class of an entity of a repository with helper methods to perform CRUD operations, count, find, and query (full-text search)
	/// </summary>
	[Serializable, DebuggerDisplay("ID = {ID}, Type = {typeof(T).FullName}")]
	public abstract class RepositoryBase<T> : RepositoryBase where T : class
	{
		/// <summary>
		/// Initialize a new instance
		/// </summary>
		public RepositoryBase() : base() { }

		#region [Static] Create
		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create<TEntity>(RepositoryContext context, DataSource dataSource, TEntity @object) where TEntity : class
			=> RepositoryMediator.Create(context, dataSource, @object);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create<TEntity>(DataSource dataSource, TEntity @object) where TEntity : class
		{
			using (var context = new RepositoryContext())
			{
				RepositoryBase<T>.Create<TEntity>(context, dataSource, @object);
			}
		}

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object) where TEntity : class
			=> RepositoryMediator.Create(context, aliasTypeName, @object);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create<TEntity>(string aliasTypeName, TEntity @object) where TEntity : class
			=> RepositoryMediator.Create(aliasTypeName, @object);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create<TEntity>(TEntity @object) where TEntity : class
			=> RepositoryBase<T>.Create<TEntity>("", @object);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task CreateAsync<TEntity>(RepositoryContext context, DataSource dataSource, TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.CreateAsync(context, dataSource, @object, cancellationToken);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task CreateAsync<TEntity>(DataSource dataSource, TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryBase<T>.CreateAsync<TEntity>(context, dataSource, @object, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateAsync<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.CreateAsync(context, aliasTypeName, @object, cancellationToken);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateAsync<TEntity>(string aliasTypeName, TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.CreateAsync(aliasTypeName, @object, cancellationToken);

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateAsync<TEntity>(TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.CreateAsync<TEntity>("", @object, cancellationToken);
		#endregion

		#region [Protected] Create
		/// <summary>
		/// Creates new instance of this object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Create(RepositoryContext context, string aliasTypeName)
			=> RepositoryBase<T>.Create<T>(context, aliasTypeName, this as T);

		/// <summary>
		/// Creates new instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Create(string aliasTypeName = null)
		{
			using (var context = new RepositoryContext())
			{
				this.Create(context, aliasTypeName);
			}
		}

		/// <summary>
		/// Creates new instance of this object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task CreateAsync(RepositoryContext context, string aliasTypeName, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.CreateAsync<T>(context, aliasTypeName, this as T, cancellationToken);

		/// <summary>
		/// Creates new instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual async Task CreateAsync(string aliasTypeName = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			using (var context = new RepositoryContext())
			{
				await this.CreateAsync(context, aliasTypeName, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region [Static] Get
		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static TEntity Get<TEntity>(RepositoryContext context, DataSource dataSource, string id, bool processSecondaryWhenNotFound = true) where TEntity : class
			=> !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.Get<TEntity>(context, dataSource, id, true, true, processSecondaryWhenNotFound)
				: null;

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static TEntity Get<TEntity>(DataSource dataSource, string id, bool processSecondaryWhenNotFound = true) where TEntity : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryBase<T>.Get<TEntity>(context, dataSource, id, processSecondaryWhenNotFound);
			}
		}

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static TEntity Get<TEntity>(RepositoryContext context, string aliasTypeName, string id, bool processSecondaryWhenNotFound = true) where TEntity : class
			=> !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.Get<TEntity>(context, aliasTypeName, id, true, true, processSecondaryWhenNotFound)
				: null;

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static TEntity Get<TEntity>(string aliasTypeName, string id, bool processSecondaryWhenNotFound = true) where TEntity : class
			=> !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.Get<TEntity>(aliasTypeName, id, processSecondaryWhenNotFound)
				: null;

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static TEntity Get<TEntity>(string id, bool processSecondaryWhenNotFound = true) where TEntity : class
			=> RepositoryBase<T>.Get<TEntity>("", id, processSecondaryWhenNotFound);

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<TEntity> GetAsync<TEntity>(RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default(CancellationToken), bool processSecondaryWhenNotFound = true) where TEntity : class
			=> !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.GetAsync<TEntity>(context, dataSource, id, true, cancellationToken, true, processSecondaryWhenNotFound)
				: Task.FromResult<TEntity>(null);

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static async Task<TEntity> GetAsync<TEntity>(DataSource dataSource, string id, CancellationToken cancellationToken = default(CancellationToken), bool processSecondaryWhenNotFound = true) where TEntity : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryBase<T>.GetAsync<TEntity>(context, dataSource, id, cancellationToken, processSecondaryWhenNotFound).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<TEntity> GetAsync<TEntity>(RepositoryContext context, string aliasTypeName, string id, CancellationToken cancellationToken = default(CancellationToken), bool processSecondaryWhenNotFound = true) where TEntity : class
			=> !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.GetAsync<TEntity>(context, aliasTypeName, id, true, cancellationToken, true, processSecondaryWhenNotFound)
				: Task.FromResult<TEntity>(null);

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<TEntity> GetAsync<TEntity>(string aliasTypeName, string id, CancellationToken cancellationToken = default(CancellationToken), bool processSecondaryWhenNotFound = true) where TEntity : class
			=> !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.GetAsync<TEntity>(aliasTypeName, id, cancellationToken, processSecondaryWhenNotFound)
				: Task.FromResult<TEntity>(null);

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<TEntity> GetAsync<TEntity>(string id, CancellationToken cancellationToken = default(CancellationToken), bool processSecondaryWhenNotFound = true) where TEntity : class
			=> RepositoryBase<T>.GetAsync<TEntity>("", id, cancellationToken, processSecondaryWhenNotFound);
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
				var instance = RepositoryBase<T>.Get<T>(context, aliasTypeName, this.ID);
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
			{
				this.Get(context, aliasTypeName);
			}
		}

		/// <summary>
		/// Gets the instance of this object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual async Task GetAsync(RepositoryContext context, string aliasTypeName, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
			{
				var instance = await RepositoryBase<T>.GetAsync<T>(context, aliasTypeName, this.ID, cancellationToken).ConfigureAwait(false);
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
		protected virtual async Task GetAsync(string aliasTypeName = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			using (var context = new RepositoryContext(false))
			{
				await this.GetAsync(context, aliasTypeName, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region [Static] Get first match
		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static TEntity Get<TEntity>(RepositoryContext context, DataSource dataSource, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null) where TEntity : class
			=> RepositoryMediator.Get<TEntity>(context, dataSource, filter, sort, businessEntityID);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static TEntity Get<TEntity>(DataSource dataSource, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null) where TEntity : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryBase<T>.Get<TEntity>(context, dataSource, filter, sort, businessEntityID);
			}
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static TEntity Get<TEntity>(RepositoryContext context, string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null) where TEntity : class
			=> RepositoryMediator.Get<TEntity>(context, aliasTypeName, filter, sort, businessEntityID);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static TEntity Get<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null) where TEntity : class
			=> RepositoryMediator.Get<TEntity>(aliasTypeName, filter, sort, businessEntityID);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static TEntity Get<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null) where TEntity : class
			=> RepositoryBase<T>.Get<TEntity>("", filter, sort, businessEntityID);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static T Get(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null)
			=> RepositoryBase<T>.Get<T>(aliasTypeName, filter, sort, businessEntityID);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static T Get(IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null)
			=> RepositoryBase<T>.Get("", filter, sort, businessEntityID);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<TEntity> GetAsync<TEntity>(RepositoryContext context, DataSource dataSource, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.GetAsync<TEntity>(context, dataSource, filter, sort, businessEntityID, cancellationToken);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static async Task<TEntity> GetAsync<TEntity>(DataSource dataSource, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryBase<T>.GetAsync<TEntity>(context, dataSource, filter, sort, businessEntityID, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<TEntity> GetAsync<TEntity>(RepositoryContext context, string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.GetAsync<TEntity>(context, aliasTypeName, filter, sort, businessEntityID, cancellationToken);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<TEntity> GetAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.GetAsync<TEntity>(aliasTypeName, filter, sort, businessEntityID, cancellationToken);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<TEntity> GetAsync<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.GetAsync<TEntity>("", filter, sort, businessEntityID, cancellationToken);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<T> GetAsync(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.GetAsync<T>(aliasTypeName, filter, sort, businessEntityID, cancellationToken);

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<T> GetAsync(IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.GetAsync("", filter, sort, businessEntityID, cancellationToken);
		#endregion

		#region [Static] Replace
		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace<TEntity>(RepositoryContext context, DataSource dataSource, TEntity @object, bool dontCreateNewVersion = false, string userID = null) where TEntity : class
			=> RepositoryMediator.Replace(context, dataSource, @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace<TEntity>(RepositoryContext context, DataSource dataSource, TEntity @object, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Replace<TEntity>(context, dataSource, @object, false, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace<TEntity>(DataSource dataSource, TEntity @object, bool dontCreateNewVersion = false, string userID = null) where TEntity : class
		{
			using (var context = new RepositoryContext())
			{
				RepositoryBase<T>.Replace<TEntity>(context, dataSource, @object, dontCreateNewVersion, userID);
			}
		}

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace<TEntity>(DataSource dataSource, TEntity @object, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Replace<TEntity>(dataSource, @object, false, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object, bool dontCreateNewVersion = false, string userID = null) where TEntity : class
			=> RepositoryMediator.Replace(context, aliasTypeName, @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Replace<TEntity>(context, aliasTypeName, @object, false, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace<TEntity>(string aliasTypeName, TEntity @object, bool dontCreateNewVersion = false, string userID = null) where TEntity : class
			=> RepositoryMediator.Replace(aliasTypeName, @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace<TEntity>(string aliasTypeName, TEntity @object, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Replace<TEntity>(aliasTypeName, @object, false, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace<TEntity>(TEntity @object, bool dontCreateNewVersion, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Replace<TEntity>("", @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace<TEntity>(TEntity @object, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Replace<TEntity>(@object, false, userID);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(RepositoryContext context, DataSource dataSource, TEntity @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.ReplaceAsync(context, dataSource, @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(RepositoryContext context, DataSource dataSource, TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.ReplaceAsync<TEntity>(context, dataSource, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task ReplaceAsync<TEntity>(DataSource dataSource, TEntity @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryBase<T>.ReplaceAsync<TEntity>(context, dataSource, @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(DataSource dataSource, TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.ReplaceAsync<TEntity>(dataSource, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.ReplaceAsync(context, aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.ReplaceAsync<TEntity>(context, aliasTypeName, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(string aliasTypeName, TEntity @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.ReplaceAsync(aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(string aliasTypeName, TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.ReplaceAsync<TEntity>(aliasTypeName, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.ReplaceAsync<TEntity>("", @object, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(TEntity @object, bool dontCreateNewVersion, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.ReplaceAsync<TEntity>("", @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(TEntity @object, bool dontCreateNewVersion, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.ReplaceAsync<TEntity>(@object, dontCreateNewVersion, null, cancellationToken);

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.ReplaceAsync<TEntity>(@object, false, cancellationToken);
		#endregion

		#region [Protected] Replace
		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		protected virtual void Replace(RepositoryContext context, string aliasTypeName, string userID = null)
			=> RepositoryBase<T>.Replace<T>(context, aliasTypeName, this as T, userID);

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		protected virtual void Replace(bool dontCreateNewVersion, string userID = null)
			=> RepositoryBase<T>.Replace<T>(this as T, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		protected virtual void Replace(string aliasTypeName = null, string userID = null)
		{
			using (var context = new RepositoryContext())
			{
				this.Replace(context, aliasTypeName, userID);
			}
		}

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task ReplaceAsync(RepositoryContext context, string aliasTypeName, string userID = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.ReplaceAsync<T>(context, aliasTypeName, this as T, userID, cancellationToken);

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task ReplaceAsync(bool dontCreateNewVersion, string userID = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.ReplaceAsync<T>(this as T, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task ReplaceAsync(bool dontCreateNewVersion, CancellationToken cancellationToken = default(CancellationToken))
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
		protected virtual async Task ReplaceAsync(string aliasTypeName = null, string userID = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			using (var context = new RepositoryContext())
			{
				await this.ReplaceAsync(context, aliasTypeName, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region [Static] Update
		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update<TEntity>(RepositoryContext context, DataSource dataSource, TEntity @object, bool dontCreateNewVersion = false, string userID = null) where TEntity : class
			=> RepositoryMediator.Update(context, dataSource, @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update<TEntity>(RepositoryContext context, DataSource dataSource, TEntity @object, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Update<TEntity>(context, dataSource, @object, false, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update<TEntity>(DataSource dataSource, TEntity @object, bool dontCreateNewVersion = false, string userID = null) where TEntity : class
		{
			using (var context = new RepositoryContext())
			{
				RepositoryBase<T>.Update<TEntity>(context, dataSource, @object, dontCreateNewVersion, userID);
			}
		}

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update<TEntity>(DataSource dataSource, TEntity @object, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Update<TEntity>(dataSource, @object, false, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object, bool dontCreateNewVersion = false, string userID = null) where TEntity : class
			=> RepositoryMediator.Update(context, aliasTypeName, @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Update<TEntity>(context, aliasTypeName, @object, false, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update<TEntity>(string aliasTypeName, TEntity @object, bool dontCreateNewVersion = false, string userID = null) where TEntity : class
			=> RepositoryMediator.Update(aliasTypeName, @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update<TEntity>(string aliasTypeName, TEntity @object, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Update<TEntity>(aliasTypeName, @object, false, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update<TEntity>(TEntity @object, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Update<TEntity>("", @object, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update<TEntity>(TEntity @object, bool dontCreateNewVersion, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Update<TEntity>("", @object, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(RepositoryContext context, DataSource dataSource, TEntity @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.UpdateAsync(context, dataSource, @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(RepositoryContext context, DataSource dataSource, TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.UpdateAsync<TEntity>(context, dataSource, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task UpdateAsync<TEntity>(DataSource dataSource, TEntity @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryBase<T>.UpdateAsync<TEntity>(context, dataSource, @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(DataSource dataSource, TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.UpdateAsync<TEntity>(dataSource, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.UpdateAsync(context, aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.UpdateAsync<TEntity>(context, aliasTypeName, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(string aliasTypeName, TEntity @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.UpdateAsync(aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(string aliasTypeName, TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.UpdateAsync<TEntity>(aliasTypeName, @object, false, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.UpdateAsync<TEntity>("", @object, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(TEntity @object, bool dontCreateNewVersion, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.UpdateAsync<TEntity>("", @object, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(TEntity @object, bool dontCreateNewVersion, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.UpdateAsync<TEntity>(@object, dontCreateNewVersion, null, cancellationToken);
		#endregion

		#region [Protected] Update
		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		protected virtual void Update(RepositoryContext context, string aliasTypeName, string userID = null)
			=> RepositoryBase<T>.Update<T>(context, aliasTypeName, this as T, userID);

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		protected virtual void Update(bool dontCreateNewVersion, string userID = null)
			=> RepositoryBase<T>.Update<T>(this as T, dontCreateNewVersion, userID);

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		protected virtual void Update(string aliasTypeName = null, string userID = null)
		{
			using (var context = new RepositoryContext())
			{
				this.Update(context, aliasTypeName, userID);
			}
		}

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task UpdateAsync(RepositoryContext context, string aliasTypeName, string userID = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.UpdateAsync<T>(context, aliasTypeName, this as T, userID, cancellationToken);

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task UpdateAsync(bool dontCreateNewVersion, string userID = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.UpdateAsync<T>(this as T, dontCreateNewVersion, userID, cancellationToken);

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task UpdateAsync(bool dontCreateNewVersion, CancellationToken cancellationToken = default(CancellationToken))
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
		protected virtual async Task UpdateAsync(string aliasTypeName = null, string userID = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			using (var context = new RepositoryContext())
			{
				await this.UpdateAsync(context, aliasTypeName, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region [Static] Create version
		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		public static VersionContent CreateVersion<TEntity>(RepositoryContext context, DataSource dataSource, TEntity @object, string userID = null) where TEntity : class
			=> RepositoryMediator.CreateVersion(context, dataSource, @object, userID);

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		public static VersionContent CreateVersion<TEntity>(RepositoryContext context, TEntity @object, string userID = null) where TEntity : class
			=> RepositoryMediator.CreateVersion(context, @object, userID);

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		public static VersionContent CreateVersion<TEntity>(TEntity @object, string userID = null) where TEntity : class
			=> RepositoryMediator.CreateVersion(@object, userID);

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task<VersionContent> CreateVersionAsync<TEntity>(RepositoryContext context, DataSource dataSource, TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.CreateVersionAsync(context, dataSource, @object, userID, cancellationToken);

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task<VersionContent> CreateVersionAsync<TEntity>(RepositoryContext context, TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.CreateVersionAsync(context, @object, userID, cancellationToken);

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task<VersionContent> CreateVersionAsync<TEntity>(TEntity @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.CreateVersionAsync(@object, userID, cancellationToken);
		#endregion

		#region [Protected] Create version
		/// <summary>
		/// Creates new version of this object
		/// </summary>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		protected virtual VersionContent CreateVersion(string userID = null)
			=> RepositoryBase<T>.CreateVersion<T>(this as T, userID);

		/// <summary>
		/// Creates new version of this object
		/// </summary>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		protected virtual Task<VersionContent> CreateVersionAsync(string userID = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.CreateVersionAsync<T>(this as T, userID, cancellationToken);
		#endregion

		#region [Static] Rollback version
		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		public static TEntity Rollback<TEntity>(RepositoryContext context, VersionContent version, string userID) where TEntity : class
			=> RepositoryMediator.Rollback<TEntity>(context, version, userID);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		public static TEntity Rollback<TEntity>(VersionContent version, string userID) where TEntity : class
			=> RepositoryMediator.Rollback<TEntity>(version, userID);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		public static TEntity Rollback<TEntity>(RepositoryContext context, string versionID, string userID) where TEntity : class
			=> RepositoryMediator.Rollback<TEntity>(context, versionID, userID);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		public static TEntity Rollback<TEntity>(string versionID, string userID) where TEntity : class
			=> RepositoryMediator.Rollback<TEntity>(versionID, userID);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<TEntity> RollbackAsync<TEntity>(RepositoryContext context, VersionContent version, string userID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.RollbackAsync<TEntity>(context, version, userID, cancellationToken);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<TEntity> RollbackAsync<TEntity>(VersionContent version, string userID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.RollbackAsync<TEntity>(version, userID, cancellationToken);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<TEntity> RollbackAsync<TEntity>(RepositoryContext context, string versionID, string userID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.RollbackAsync<TEntity>(context, versionID, userID, cancellationToken);

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<TEntity> RollbackAsync<TEntity>(string versionID, string userID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.RollbackAsync<TEntity>(versionID, userID, cancellationToken);
		#endregion

		#region [Protected] Rollback version
		/// <summary>
		/// Rollbacks this object from a version content
		/// </summary>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		protected virtual T Rollback(VersionContent version, string userID)
			=> RepositoryBase<T>.Rollback<T>(version, userID);

		/// <summary>
		/// Rollbacks this object from a version content
		/// </summary>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		protected virtual T Rollback(string versionID, string userID)
			=> RepositoryBase<T>.Rollback<T>(versionID, userID);

		/// <summary>
		/// Rollbacks this object from a version content
		/// </summary>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task<T> RollbackAsync(VersionContent version, string userID, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.RollbackAsync<T>(version, userID, cancellationToken);

		/// <summary>
		/// Rollbacks this object from a version content
		/// </summary>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task<T> RollbackAsync(string versionID, string userID, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.RollbackAsync<T>(versionID, userID, cancellationToken);
		#endregion

		#region [Static] Count versions
		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <returns></returns>
		public static long CountVersions<TEntity>(string objectID) where TEntity : class
			=> RepositoryMediator.CountVersionContents<TEntity>(objectID);

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountVersionsAsync<TEntity>(string objectID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.CountVersionContentsAsync<TEntity>(objectID, cancellationToken);
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
				: RepositoryBase<T>.CountVersions<T>(this.ID);
			return this._totalVersions;
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected async Task<long> CountVersionsAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			this._totalVersions = string.IsNullOrWhiteSpace(this.ID)
				? 0
				: await RepositoryBase<T>.CountVersionsAsync<T>(this.ID, cancellationToken).ConfigureAwait(false);
			return this._totalVersions;
		}
		#endregion

		#region [Static] Find versions
		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersions<TEntity>(string objectID) where TEntity : class
			=> RepositoryMediator.FindVersionContents<TEntity>(objectID);

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<VersionContent>> FindVersionsAsync<TEntity>(string objectID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.FindVersionContentsAsync<TEntity>(objectID, cancellationToken);
		#endregion

		#region [Protected] Find versions
		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <returns></returns>
		protected List<VersionContent> FindVersions()
			=> string.IsNullOrWhiteSpace(this.ID)
				? null
				: RepositoryBase<T>.FindVersions<T>(this.ID);

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected Task<List<VersionContent>> FindVersionsAsync(CancellationToken cancellationToken = default(CancellationToken))
			=> string.IsNullOrWhiteSpace(this.ID)
				? Task.FromResult<List<VersionContent>>(null)
				: RepositoryBase<T>.FindVersionsAsync<T>(this.ID, cancellationToken);
		#endregion

		#region [Static] Delete
		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static void Delete<TEntity>(RepositoryContext context, DataSource dataSource, string id, string userID = null) where TEntity : class
			=> RepositoryMediator.Delete<TEntity>(context, dataSource, id, userID);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static void Delete<TEntity>(DataSource dataSource, string id, string userID = null) where TEntity : class
		{
			using (var context = new RepositoryContext())
			{
				RepositoryBase<T>.Delete<TEntity>(context, dataSource, id, userID);
			}
		}

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static void Delete<TEntity>(RepositoryContext context, string aliasTypeName, string id, string userID = null) where TEntity : class
			=> RepositoryMediator.Delete<TEntity>(context, aliasTypeName, id, userID);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static void Delete<TEntity>(string aliasTypeName, string id, string userID = null) where TEntity : class
			=> RepositoryMediator.Delete<TEntity>(aliasTypeName, id, userID);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static void Delete<TEntity>(string id, string userID = null) where TEntity : class
			=> RepositoryBase<T>.Delete<TEntity>("", id, userID);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync<TEntity>(RepositoryContext context, DataSource dataSource, string id, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.DeleteAsync<TEntity>(context, dataSource, id, userID, cancellationToken);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task DeleteAsync<TEntity>(DataSource dataSource, string id, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryBase<T>.DeleteAsync<TEntity>(context, dataSource, id, userID, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync<TEntity>(RepositoryContext context, string aliasTypeName, string id, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.DeleteAsync<TEntity>(context, aliasTypeName, id, userID, cancellationToken);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync<TEntity>(string aliasTypeName, string id, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.DeleteAsync<TEntity>(aliasTypeName, id, userID, cancellationToken);

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync<TEntity>(string id, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.DeleteAsync<TEntity>("", id, userID, cancellationToken);
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
				RepositoryBase<T>.Delete<T>(context, aliasTypeName, this.ID, userID);
		}

		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		protected virtual void Delete(string aliasTypeName = null, string userID = null)
		{
			using (var context = new RepositoryContext())
			{
				this.Delete(context, aliasTypeName, userID);
			}
		}

		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task DeleteAsync(RepositoryContext context, string aliasTypeName, string userID = null, CancellationToken cancellationToken = default(CancellationToken))
			=> !string.IsNullOrWhiteSpace(this.ID)
				? RepositoryBase<T>.DeleteAsync<T>(context, aliasTypeName, this.ID, userID, cancellationToken)
				: Task.FromException(new ArgumentException("The identity of the object is null or empty", nameof(this.ID)));

		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual async Task DeleteAsync(string aliasTypeName = null, string userID = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			using (var context = new RepositoryContext())
			{
				await this.DeleteAsync(context, aliasTypeName, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region [Static] Delete many
		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<TEntity>(RepositoryContext context, DataSource dataSource, IFilterBy<TEntity> filter, string businessEntityID = null) where TEntity : class
			=> RepositoryMediator.DeleteMany(context, dataSource, filter, businessEntityID);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<TEntity>(DataSource dataSource, IFilterBy<TEntity> filter, string businessEntityID = null) where TEntity : class
		{
			using (var context = new RepositoryContext())
			{
				RepositoryBase<T>.DeleteMany<TEntity>(context, dataSource, filter, businessEntityID);
			}
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<TEntity>(RepositoryContext context, string aliasTypeName, IFilterBy<TEntity> filter, string businessEntityID = null) where TEntity : class
			=> RepositoryMediator.DeleteMany(context, aliasTypeName, filter, businessEntityID);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, string businessEntityID = null) where TEntity : class
			=> RepositoryMediator.DeleteMany(aliasTypeName, filter, businessEntityID);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<TEntity>(IFilterBy<TEntity> filter, string businessEntityID = null) where TEntity : class
			=> RepositoryBase<T>.DeleteMany<TEntity>("", filter, businessEntityID);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		protected static void DeleteMany(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null)
			=> RepositoryBase<T>.DeleteMany<T>(context, aliasTypeName, filter, businessEntityID);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		protected static void DeleteMany(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null)
			=> RepositoryBase<T>.DeleteMany<T>(aliasTypeName, filter, businessEntityID);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		protected static void DeleteMany(IFilterBy<T> filter, string businessEntityID = null)
			=> RepositoryBase<T>.DeleteMany("", filter, businessEntityID);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteManyAsync<TEntity>(RepositoryContext context, DataSource dataSource, IFilterBy<TEntity> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.DeleteManyAsync(context, dataSource, filter, businessEntityID, cancellationToken);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task DeleteManyAsync<TEntity>(DataSource dataSource, IFilterBy<TEntity> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryBase<T>.DeleteManyAsync<TEntity>(context, dataSource, filter, businessEntityID, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteManyAsync<TEntity>(RepositoryContext context, string aliasTypeName, IFilterBy<TEntity> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.DeleteManyAsync(context, aliasTypeName, filter, businessEntityID, cancellationToken);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteManyAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.DeleteManyAsync(aliasTypeName, filter, businessEntityID, cancellationToken);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteManyAsync<TEntity>(IFilterBy<TEntity> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.DeleteManyAsync<TEntity>("", filter, businessEntityID, cancellationToken);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected static Task DeleteManyAsync(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.DeleteManyAsync<T>(context, aliasTypeName, filter, businessEntityID, cancellationToken);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected static Task DeleteManyAsync(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.DeleteManyAsync<T>(aliasTypeName, filter, businessEntityID, cancellationToken);

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected static Task DeleteManyAsync(IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.DeleteManyAsync("", filter, businessEntityID, cancellationToken);
		#endregion

		#region [Static] Find
		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Find<TEntity>(RepositoryContext context, DataSource dataSource, IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0) where TEntity : class
			=> RepositoryMediator.Find(context, dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Find<TEntity>(DataSource dataSource, IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0) where TEntity : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryBase<T>.Find<TEntity>(context, dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
			}
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Find<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0) where TEntity : class
			=> RepositoryMediator.Find(aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Find<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0) where TEntity : class
			=> RepositoryBase<T>.Find<TEntity>("", filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0)
			=> RepositoryBase<T>.Find<T>(aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0)
			=> RepositoryBase<T>.Find("", filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID, string cacheKey)
			=> RepositoryBase<T>.Find(filter, sort, pageSize, pageNumber, businessEntityID, true, cacheKey, 0);

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
			=> RepositoryBase<T>.Find(filter, sort, pageSize, pageNumber, null, cacheKey);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> FindAsync<TEntity>(RepositoryContext context, DataSource dataSource, IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.FindAsync(context, dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static async Task<List<TEntity>> FindAsync<TEntity>(DataSource dataSource, IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryBase<T>.FindAsync<TEntity>(context, dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> FindAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.FindAsync(aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> FindAsync<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.FindAsync<TEntity>("", filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.FindAsync<T>(aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.FindAsync("", filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID, string cacheKey, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.FindAsync(filter, sort, pageSize, pageNumber, businessEntityID, true, cacheKey, 0, cancellationToken);

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
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string cacheKey, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.FindAsync(filter, sort, pageSize, pageNumber, null, cacheKey, cancellationToken);
		#endregion

		#region [Static] Count
		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count<TEntity>(RepositoryContext context, DataSource dataSource, IFilterBy<TEntity> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0) where TEntity : class
			=> RepositoryMediator.Count(context, dataSource, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count<TEntity>(DataSource dataSource, IFilterBy<TEntity> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0) where TEntity : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryBase<T>.Count<TEntity>(context, dataSource, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
			}
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0) where TEntity : class
			=> RepositoryMediator.Count(aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count<TEntity>(IFilterBy<TEntity> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0) where TEntity : class
			=> RepositoryBase<T>.Count<TEntity>("", filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count<TEntity>(IFilterBy<TEntity> filter, string businessEntityID, string cacheKey = null) where TEntity : class
			=> RepositoryBase<T>.Count<TEntity>(filter, businessEntityID, true, cacheKey, 0);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(string aliasTypeName, IFilterBy<T> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0)
			=> RepositoryBase<T>.Count<T>(aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(IFilterBy<T> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0)
			=> RepositoryBase<T>.Count<T>(filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(IFilterBy<T> filter = null, string businessEntityID = null, string cacheKey = null)
		{
			return RepositoryBase<T>.Count(filter, businessEntityID, true, cacheKey);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync<TEntity>(RepositoryContext context, DataSource dataSource, IFilterBy<TEntity> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.CountAsync(context, dataSource, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static async Task<long> CountAsync<TEntity>(DataSource dataSource, IFilterBy<TEntity> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryBase<T>.CountAsync<TEntity>(context, dataSource, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.CountAsync(aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync<TEntity>(IFilterBy<TEntity> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.CountAsync<TEntity>("", filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync<TEntity>(IFilterBy<TEntity> filter, string businessEntityID, string cacheKey = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.CountAsync<TEntity>(filter, businessEntityID, true, cacheKey, 0, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(string aliasTypeName, IFilterBy<T> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.CountAsync<T>(aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.CountAsync("", filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter, string businessEntityID, string cacheKey = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.CountAsync(filter, businessEntityID, true, cacheKey, 0, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter = null, string cacheKey = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.CountAsync(filter, null, cacheKey, cancellationToken);
		#endregion

		#region [Static] Search
		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Search<TEntity>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber, string businessEntityID) where TEntity : class
			=> RepositoryMediator.Search(context, dataSource, query, filter, pageSize, pageNumber, businessEntityID);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Search<TEntity>(DataSource dataSource, string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber, string businessEntityID) where TEntity : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryBase<T>.Search<TEntity>(context, dataSource, query, filter, pageSize, pageNumber, businessEntityID);
			}
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Search<TEntity>(string aliasTypeName, string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber, string businessEntityID) where TEntity : class
			=> RepositoryMediator.Search(aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Search<TEntity>(string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber, string businessEntityID) where TEntity : class
			=> RepositoryBase<T>.Search<TEntity>("", query, filter, pageSize, pageNumber, businessEntityID);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Search(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID)
			=> RepositoryBase<T>.Search<T>(aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Search(string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null)
			=> RepositoryBase<T>.Search("", query, filter, pageSize, pageNumber, businessEntityID);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> SearchAsync<TEntity>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.SearchAsync(context, dataSource, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static async Task<List<TEntity>> SearchAsync<TEntity>(DataSource dataSource, string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryBase<T>.SearchAsync<TEntity>(context, dataSource, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> SearchAsync<TEntity>(string aliasTypeName, string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.SearchAsync(aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> SearchAsync<TEntity>(string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.SearchAsync<TEntity>("", query, filter, pageSize, pageNumber, businessEntityID, cancellationToken);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> SearchAsync(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.SearchAsync<T>(aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> SearchAsync(string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.SearchAsync("", query, filter, pageSize, pageNumber, businessEntityID, cancellationToken);

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> SearchAsync(string query, IFilterBy<T> filter, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.SearchAsync("", query, filter, pageSize, pageNumber, null, cancellationToken);
		#endregion

		#region [Static] Count by search query
		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountByQuery<TEntity>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<TEntity> filter, string businessEntityID) where TEntity : class
			=> RepositoryMediator.Count(context, dataSource, query, filter, businessEntityID);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountByQuery<TEntity>(DataSource dataSource, string query, IFilterBy<TEntity> filter, string businessEntityID) where TEntity : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryBase<T>.CountByQuery<TEntity>(context, dataSource, query, filter, businessEntityID);
			}
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountByQuery<TEntity>(string aliasTypeName, string query, IFilterBy<TEntity> filter, string businessEntityID) where TEntity : class
			=> RepositoryMediator.Count(aliasTypeName, query, filter, businessEntityID);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountByQuery<TEntity>(string query, IFilterBy<TEntity> filter = null, string businessEntityID = null) where TEntity : class
			=> RepositoryBase<T>.CountByQuery<TEntity>("", query, filter, businessEntityID);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountByQuery(string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID)
			=> RepositoryBase<T>.CountByQuery<T>(aliasTypeName, query, filter, businessEntityID);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountByQuery(string query, IFilterBy<T> filter = null, string businessEntityID = null)
			=> RepositoryBase<T>.CountByQuery("", query, filter, businessEntityID);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync<TEntity>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<TEntity> filter, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.CountAsync(context, dataSource, query, filter, businessEntityID, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static async Task<long> CountByQueryAsync<TEntity>(DataSource dataSource, string query, IFilterBy<TEntity> filter, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryBase<T>.CountByQueryAsync<TEntity>(context, dataSource, query, filter, businessEntityID, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync<TEntity>(string aliasTypeName, string query, IFilterBy<TEntity> filter, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.CountAsync(aliasTypeName, query, filter, businessEntityID, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync<TEntity>(string query, IFilterBy<TEntity> filter, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.CountByQueryAsync<TEntity>("", query, filter, businessEntityID, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync<TEntity>(string query, IFilterBy<TEntity> filter = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryBase<T>.CountByQueryAsync<TEntity>("", query, filter, null, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync(string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.CountByQueryAsync<T>(aliasTypeName, query, filter, businessEntityID, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync(string query, IFilterBy<T> filter, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.CountByQueryAsync("", query, filter, businessEntityID, cancellationToken);

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync(string query, IFilterBy<T> filter = null, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryBase<T>.CountByQueryAsync(query, filter, null, cancellationToken);
		#endregion

		#region [Static] Sync to other data sources
		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The context</param>
		/// <param name="object">The object</param>
		public static void Sync<TEntity>(RepositoryContext context, TEntity @object) where TEntity : class
			=> RepositoryMediator.Sync(context, @object);

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The alias type name</param>
		/// <param name="object">The object</param>
		public static void Sync<TEntity>(string aliasTypeName, TEntity @object) where TEntity : class
			=> RepositoryMediator.Sync(aliasTypeName, @object);

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">The context</param>
		/// <param name="object">The object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task SyncAsync<TEntity>(RepositoryContext context, TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.SyncAsync(context, @object, cancellationToken);

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The alias type name</param>
		/// <param name="object">The object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task SyncAsync<TEntity>(string aliasTypeName, TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
			=> RepositoryMediator.SyncAsync(aliasTypeName, @object, cancellationToken);
		#endregion

		#region [Protected] Sync to other data sources
		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <param name="context">The context</param>
		/// <param name="object">The object</param>
		protected void Sync(RepositoryContext context, T @object)
			=> RepositoryMediator.Sync(context, @object);

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <param name="aliasTypeName">The alias type name</param>
		/// <param name="object">The object</param>
		protected void Sync(string aliasTypeName, T @object)
			=> RepositoryMediator.Sync(aliasTypeName, @object);

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <param name="context">The context</param>
		/// <param name="object">The object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected Task SyncAsync(RepositoryContext context, T @object, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryMediator.SyncAsync(context, @object, cancellationToken);

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <param name="aliasTypeName">The alias type name</param>
		/// <param name="object">The object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected Task SyncAsync(string aliasTypeName, T @object, CancellationToken cancellationToken = default(CancellationToken))
			=> RepositoryMediator.SyncAsync(aliasTypeName, @object, cancellationToken);
		#endregion

		#region [Public] Generate form controls
		/// <summary>
		/// Generates the form controls of this type
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		public static JToken GenerateFormControls<TEntity>() where TEntity : class => RepositoryMediator.GenerateFormControls<TEntity>();

		/// <summary>
		/// Generates the form controls of this type
		/// </summary>
		protected virtual JToken GenerateFormControls() => RepositoryMediator.GenerateFormControls<T>();
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
			var attributes = this.GetProperties().ToDictionary(attribute => attribute.Name);
			try
			{
				if (attributes.ContainsKey(name))
				{
					value = (this as T).GetAttributeValue(name);
					return true;
				}
				else if (this is IBusinessEntity && (this as IBusinessEntity).ExtendedProperties != null && (this as IBusinessEntity).ExtendedProperties.ContainsKey(name))
				{
					value = (this as IBusinessEntity).ExtendedProperties[name];
					return true;
				}
				else
					return false;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <returns></returns>
		public override object GetProperty(string name)
		{
			return this.TryGetProperty(name, out object value)
				? value
				: null;
		}

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
				value = theValue != null ? theValue.CastAs<TValue>() : default(TValue);
				return true;
			}

			// default
			value = default(TValue);
			return false;
		}

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <returns></returns>
		public virtual TValue GetProperty<TValue>(string name)
			=> this.TryGetProperty(name, out TValue value)
				? value
				: default(TValue);

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <returns>true if the property is setted; otherwise false;</returns>
		public virtual bool TrySetProperty(string name, object value)
		{
			var attributes = this.GetProperties().ToDictionary(attribute => attribute.Name);
			try
			{
				if (attributes.ContainsKey(name))
				{
					this.SetAttributeValue(attributes[name], value, true);
					return true;
				}
				else if (this is IBusinessEntity && (this as IBusinessEntity).ExtendedProperties != null)
				{
					(this as IBusinessEntity).ExtendedProperties[name] = value;
					return true;
				}
				else
					return false;
			}
			catch
			{
				return false;
			}
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
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (named with surfix '$Type')</param>
		/// <param name="onPreCompleted">The action to run on pre-completed</param>
		/// <returns></returns>
		public override JObject ToJson(bool addTypeOfExtendedProperties = false, Action<JObject> onPreCompleted = null)
		{
			// serialize the original object
			var json = (this as T).ToJson(null) as JObject;

			// serialize privileges & extended properties
			if (this is IBusinessEntity)
			{
				// working privileges
				if (json["Privileges"] == null && (this as IBusinessEntity).WorkingPrivileges != null)
					json.Add("Privileges", JObject.FromObject(this.OriginalPrivileges));

				// extended properties
				if ((this as IBusinessEntity).ExtendedProperties != null && (this as IBusinessEntity).ExtendedProperties.Count > 0)
					(this as IBusinessEntity).ExtendedProperties.ForEach(property =>
					{
						var type = property.Value?.GetType();

						if (addTypeOfExtendedProperties && type != null)
							json.Add(property.Key + "$Type", type.IsPrimitiveType() ? type.ToString() : type.GetTypeName());

						var value = type == null || type.IsPrimitiveType()
							? property.Value
							: property.Value is RepositoryBase
								? (property.Value as RepositoryBase).ToJson(addTypeOfExtendedProperties, null)
								: property.Value.ToJson(null);

						json.Add(new JProperty(property.Key, value));
					});
			}

			// run the handler on pre-completed
			onPreCompleted?.Invoke(json);

			// return the JSON
			return json;
		}

		/// <summary>
		/// Parses the JSON object and copy values into this object
		/// </summary>
		/// <param name="json">The JSON object that contains information</param>
		/// <param name="onPreCompleted">The action to run on pre-completed</param>
		public override void ParseJson(JObject json, Action<JObject> onPreCompleted = null)
		{
			if (json != null)
			{
				this.CopyFrom(json);
				onPreCompleted?.Invoke(json);
			}
		}

		/// <summary>
		/// Serializes this object to XML object
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (attribute named '$type')</param>
		/// <param name="onPreCompleted">The action to run on pre-completed</param>
		/// <returns></returns>
		public override XElement ToXml(bool addTypeOfExtendedProperties = false, Action<XElement> onPreCompleted = null)
		{
			// serialize the original object
			var xml = (this as T).ToXml(null);

			// serialize privileges & extended properties
			if (this is IBusinessEntity)
			{
				// working privileges				
				if (xml.Elements().FirstOrDefault(e => e.Name.Equals("Privileges")) == null && (this as IBusinessEntity).WorkingPrivileges != null)
					xml.Add(new XElement("Privileges", (this as IBusinessEntity).WorkingPrivileges.ToXml()));

				// extended properties
				if ((this as IBusinessEntity).ExtendedProperties != null && (this as IBusinessEntity).ExtendedProperties.Count > 0)
					(this as IBusinessEntity).ExtendedProperties.ForEach(property =>
					{
						var type = property.Value?.GetType();

						var value = type == null || type.IsPrimitiveType()
							? property.Value
							: property.Value is RepositoryBase
								? (property.Value as RepositoryBase).ToXml(addTypeOfExtendedProperties, null)
								: property.Value.ToXml();

						var element = addTypeOfExtendedProperties && type != null
							? new XElement(XName.Get(property.Key), new XAttribute("$type", type.IsPrimitiveType() ? type.ToString() : type.GetTypeName()), value)
							: new XElement(XName.Get(property.Key), value);

						xml.Add(element);
					});
			}

			// run the handler
			onPreCompleted?.Invoke(xml);

			// return the XML
			return xml;
		}

		/// <summary>
		/// Parses the XML object and copy values into this object
		/// </summary>
		/// <param name="xml">The XML object that contains information</param>
		/// <param name="onPreCompleted">The action to run on pre-completed</param>
		public override void ParseXml(XContainer xml, Action<XContainer> onPreCompleted = null)
		{
			if (xml != null)
			{
				this.CopyFrom(xml.FromXml<T>());
				onPreCompleted?.Invoke(xml);
			}
		}
		#endregion

		#region [Public] IBussineEntitys' properties & methods
		/// <summary>
		/// Gets or sets the title
		/// </summary>
		[BsonIgnoreIfNull, Property(MaxLength = 250), IgnoreIfNull, Sortable, Searchable]
		public virtual string Title { get; set; } = null;

		/// <summary>
		/// Gets the name of service that associates with this repository
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnore, Ignore, FormControl(Excluded = true)]
		public virtual string ServiceName { get; }

		/// <summary>
		/// Gets or sets the identity of the business system that the object is belong to (means the run-time system)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, Property(MaxLength = 32), IgnoreIfNull, Sortable(IndexName = "System"), FormControl(Excluded = true)]
		public virtual string SystemID { get; set; } = null;

		/// <summary>
		/// Gets or sets the identity of the business repository that the object is belong to (means the run-time business module)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, Property(MaxLength = 32), IgnoreIfNull, Sortable(IndexName = "System"), FormControl(Excluded = true)]
		public virtual string RepositoryID { get; set; } = null;

		/// <summary>
		/// Gets or sets the identity of the business entity that the object is belong to (means the run-time business content-type)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, Property(MaxLength = 32), IgnoreIfNull, Sortable(IndexName = "System"), FormControl(Excluded = true)]
		public virtual string EntityID { get; set; } = null;

		/// <summary>
		/// Gets or sets the collection of extended properties
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, Ignore]
		public virtual Dictionary<string, object> ExtendedProperties { get; set; } = null;

		/// <summary>
		/// Gets the business entity that marks as parent of this object
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnore, Ignore]
		public virtual IBusinessEntity Parent { get; }

		/// <summary>
		/// Gets or sets the original privileges (means original working permissions)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, AsJson, FormControl(Excluded = true)]
		public virtual Privileges OriginalPrivileges { get; set; } = null;

		/// <summary>
		/// The privileges that are combined from original privileges and parent privileges
		/// </summary>
		[NonSerialized]
		protected Privileges Privileges = null;

		/// <summary>
		/// Gets the actual privileges (mean the combined privileges)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnore, Ignore]
		public virtual Privileges WorkingPrivileges => this.Privileges ?? (this.Privileges = this.OriginalPrivileges.Combine(this.Parent?.WorkingPrivileges));

		long _totalVersions = -1;

		/// <summary>
		/// Gets the total number of version contents
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnore, Ignore]
		public virtual long TotalVersions
		{
			get
			{
				if (this._totalVersions < 0)
					this.CountVersions();
				return this._totalVersions;
			}
		}
		#endregion
		
	}

	//  --------------------------------------------------------------------------------------------

	#region Trash & Version
	[Serializable]
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
		public string EntityID { get; set; }

		/// <summary>
		/// Gets or sets the create time
		/// </summary>
		public DateTime Created { get; set; }

		/// <summary>
		/// Gets or sets the identity of user
		/// </summary>
		public string CreatedID { get; set; }

		/// <summary>
		/// Gets the raw data of object (compressed bytes in Base64 string)
		/// </summary>
		public string Data { get; internal set; }

		object _Object = null;

		/// <summary>
		/// Gets the original object
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnore]
		public object Object
		{
			get => this._Object ?? (this._Object = !string.IsNullOrWhiteSpace(this.Data) ? Caching.Helper.Deserialize(this.Data.Base64ToBytes().Decompress()) : null);
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
				return collection.CountDocuments(filter != null ? filter.GetNoSqlStatement() : Builders<T>.Filter.Empty);
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					var info = filter?.GetSqlStatement();
					var command = connection.CreateCommand($"COUNT (ID) AS Total FROM T_Data_{name}" + (info != null ? " WHERE " + info.Item1 : ""), info?.Item2.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList());
					return command.ExecuteScalar().CastAs<long>();
				}
			}
			else
				return 0;
		}

		internal static async Task<long> CountAsync<T>(DataSource dataSource, string name, IFilterBy<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				return await collection.CountDocumentsAsync(filter != null ? filter.GetNoSqlStatement() : Builders<T>.Filter.Empty, null, cancellationToken).ConfigureAwait(false);
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					var info = filter?.GetSqlStatement();
					var command = connection.CreateCommand($"COUNT (ID) AS Total FROM T_Data_{name}" + (info != null ? " WHERE " + info.Item1 : ""), info?.Item2.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList());
					return (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).CastAs<long>();
				}
			}
			else
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
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					var info = filter?.GetSqlStatement();
					var statement = $"SELECT * FROM T_Data_{name}"
						+ (info != null ? " WHERE " + info.Item1 : "")
						+ (sort != null ? " ORDER BY " + sort.GetSqlStatement() : "");

					DataTable dataTable = null;
					if (pageSize == 0)
					{
						var command = connection.CreateCommand(statement, info?.Item2.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList());
						using (var dataReader = command.ExecuteReader())
						{
							dataTable = dataReader.ToDataTable<T>();
						}
					}
					else
					{
						var dataSet = new DataSet();
						var dataAdapter = dbProviderFactory.CreateDataAdapter();
						dataAdapter.SelectCommand = connection.CreateCommand(statement, info?.Item2.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList());
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
						dataTable = dataSet.Tables[0];
					}

					return dataTable.Rows
						.ToList()
						.Select(dataRow =>
						{
							var @object = ObjectService.CreateInstance<T>();
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
			else
				return null;
		}

		internal static async Task<List<T>> FindAsync<T>(DataSource dataSource, string name, IFilterBy<T> filter, SortBy<T> sort, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				return await collection.FindAsync(filter?.GetNoSqlStatement(), sort?.GetNoSqlStatement(), pageSize, pageNumber, null, cancellationToken).ConfigureAwait(false);
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					var info = filter?.GetSqlStatement();
					var statement = $"SELECT * FROM T_Data_{name}"
						+ (info != null ? " WHERE " + info.Item1 : "")
						+ (sort != null ? " ORDER BY " + sort.GetSqlStatement() : "");

					DataTable dataTable = null;
					if (pageSize == 0)
					{
						var command = connection.CreateCommand(statement, info?.Item2.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList());
						using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
						{
							dataTable = await dataReader.ToDataTableAsync<T>(cancellationToken).ConfigureAwait(false);
						}
					}
					else
					{
						var dataSet = new DataSet();
						var dataAdapter = dbProviderFactory.CreateDataAdapter();
						dataAdapter.SelectCommand = connection.CreateCommand(statement, info?.Item2.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList());
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
						dataTable = dataSet.Tables[0];
					}

					return dataTable.Rows
						.ToList()
						.Select(dataRow =>
						{
							var @object = ObjectService.CreateInstance<T>();
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
			else
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
					var attributes = ObjectService.GetProperties(typeof(T))
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

		internal static async Task<T> CreateAsync<T>(DataSource dataSource, string name, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
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
					var attributes = ObjectService.GetProperties(typeof(T))
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
				collection.DeleteMany(filter != null ? filter.GetNoSqlStatement() : Builders<T>.Filter.Empty);
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					var info = filter?.GetSqlStatement();
					var command = connection.CreateCommand(
						$"DELETE FROM T_Data_{name}" + (info != null ? " WHERE " + info.Item1 : ""),
						info?.Item2.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList()
					);
					command.ExecuteNonQuery();
				}
			}
		}

		internal static async Task DeleteAsync<T>(DataSource dataSource, string name, IFilterBy<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (dataSource == null)
				throw new ArgumentNullException(nameof(dataSource), "Data source is invalid");

			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				var collection = NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, name);
				await collection.DeleteManyAsync(filter != null ? filter.GetNoSqlStatement() : Builders<T>.Filter.Empty, cancellationToken).ConfigureAwait(false);
			}
			else if (dataSource.Mode.Equals(RepositoryMode.SQL))
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					var info = filter?.GetSqlStatement();
					var command = connection.CreateCommand(
						$"DELETE FROM T_Data_{name}" + (info != null ? " WHERE " + info.Item1 : ""),
						info?.Item2.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList()
					);
					await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
			}
		}

		internal static TrashContent Prepare<T>(T @object, Action<TrashContent> onPreCompleted = null) where T : class
		{
			string serviceName = null, systemID = null, repositoryID = null, entityID = null, objectID = null, title = null;

			if (@object is IBusinessEntity)
			{
				serviceName = (@object as IBusinessEntity).ServiceName;
				systemID = (@object as IBusinessEntity).SystemID;
				repositoryID = (@object as IBusinessEntity).RepositoryID;
				entityID = (@object as IBusinessEntity).EntityID;
				objectID = (@object as IBusinessEntity).ID;
				title = (@object as IBusinessEntity).Title;
			}
			else if (@object is IRepositoryEntity)
			{
				serviceName = (@object as IRepositoryEntity).ServiceName;
				systemID = (@object as IRepositoryEntity).SystemID;
				repositoryID = (@object as IRepositoryEntity).RepositoryID;
				objectID = (@object as IRepositoryEntity).ID;
				title = (@object as IRepositoryEntity).Title;
			}
			else if (@object is IRepository)
			{
				serviceName = (@object as IRepository).ServiceName;
				systemID = (@object as IRepository).SystemID;
				objectID = (@object as IRepository).ID;
				title = (@object as IRepository).Title;
			}
			else
			{
				serviceName = @object.GetAttributeValue("ServiceName") as string;
				systemID = @object.GetAttributeValue("SystemID") as string;
				repositoryID = @object.GetAttributeValue("RepositoryID") as string;
				entityID = @object.GetAttributeValue("EntityID") as string;
				objectID = @object.GetEntityID();
				title = @object.GetAttributeValue("Title") as string;
			}

			if (string.IsNullOrWhiteSpace(title))
				title = typeof(T).GetTypeName(true) + "#" + objectID;

			var content = new TrashContent
			{
				ID = (typeof(T).GetTypeName() + "#" + objectID).GetMD5(),
				Title = title,
				ServiceName = serviceName,
				SystemID = systemID,
				RepositoryID = repositoryID,
				EntityID = entityID,
				Object = @object,
				Created = DateTime.Now,
				CreatedID = ""
			};
			onPreCompleted?.Invoke(content);
			return content;
		}
	}

	[Serializable]
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

		internal static new VersionContent Prepare<T>(T @object, Action<VersionContent> onPreCompleted = null) where T : class
		{
			var content = ObjectService.CreateInstance<VersionContent>();
			content.CopyFrom(TrashContent.Prepare(@object));
			content.ID = UtilityService.NewUUID;
			content.ObjectID = @object.GetEntityID();
			onPreCompleted?.Invoke(content);
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
			=> x == null || y == null
				? false
				: object.ReferenceEquals(x, y)
					? true
					: (x.GetEntityID() ?? "").IsEquals(y.GetEntityID());

		/// <summary>
		/// If Equals() returns true for a pair of objects,  then GetHashCode() must return the same value for these objects
		/// </summary>
		/// <param name="object"></param>
		/// <returns></returns>
		public virtual int GetHashCode(T @object)
			=> @object == null
				? -1
				: (@object.GetEntityID() ?? "").GetHashCode();
	}
	#endregion

	//  --------------------------------------------------------------------------------------------

	#region Identity generator (for working with MongoDB)
	/// <summary>
	/// Generates identity as UUID (128 bits) for MongoDB documents
	/// </summary>
	public class IdentityGenerator : MongoDB.Bson.Serialization.IIdGenerator
	{
		public object GenerateId(object container, object document) => Guid.NewGuid().ToString("N").ToLower();

		public bool IsEmpty(object id) => id == null || id.Equals(string.Empty);
	}
	#endregion

}