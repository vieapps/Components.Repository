#region Related components
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;

using System.Data;
using System.Data.Common;

using net.vieapps.Components.Caching;
using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Collection of methods for working with SQL database (like SQL Server, MySQL, Oracle, ...)
	/// </summary>
	public static class SqlHelper
	{

		#region Provider Factory
		/// <summary>
		/// Gets the database provider factory for working with SQL database
		/// </summary>
		/// <param name="dataSource">The object that presents related information of a data source in SQL database</param>
		/// <returns></returns>
		public static DbProviderFactory GetProviderFactory(DataSource dataSource)
		{
			var connectionStringSettings = dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL)
				? RepositoryMediator.GetConnectionStringSettings(dataSource)
				: null;
			return connectionStringSettings != null && !string.IsNullOrEmpty(connectionStringSettings.ProviderName)
				? DbProviderFactories.GetFactory(connectionStringSettings.ProviderName)
				: null;
		}
		#endregion

		#region Connection
		/// <summary>
		/// Gets the connection for working with SQL database
		/// </summary>
		/// <param name="existingConnection">The object that presents a database connection to copy settings from</param>
		/// <returns></returns>
		public static DbConnection GetConnection(DbConnection existingConnection)
		{
			if (existingConnection == null)
				return null;
			var connection = Activator.CreateInstance(existingConnection.GetType()) as DbConnection;
			connection.ConnectionString = existingConnection.ConnectionString;
			return connection;
		}

		/// <summary>
		/// Gets the connection for working with SQL database
		/// </summary>
		/// <param name="providerFactory">The object that presents information of a database provider factory</param>
		/// <param name="connectionString">The string that presents information to connect with SQL database</param>
		/// <returns></returns>
		public static DbConnection GetConnection(DbProviderFactory providerFactory, string connectionString)
		{
			if (providerFactory == null)
				return null;
			var connection = providerFactory.CreateConnection();
			if (!string.IsNullOrWhiteSpace(connectionString))
				connection.ConnectionString = connectionString;
			return connection;
		}

		/// <summary>
		/// Gets the connection for working with SQL database
		/// </summary>
		/// <param name="dataSource">The object that presents related information of a data source in SQL database</param>
		/// <param name="providerFactory">The object that presents information of a database provider factory</param>
		/// <returns></returns>
		public static DbConnection GetConnection(DataSource dataSource, DbProviderFactory providerFactory = null)
		{		
			var connectionStringSettings = dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL)
				? RepositoryMediator.GetConnectionStringSettings(dataSource)
				: null;
			return SqlHelper.GetConnection(providerFactory != null ? providerFactory : SqlHelper.GetProviderFactory(dataSource), connectionStringSettings != null ? connectionStringSettings.ConnectionString : null);
		}
		#endregion

		#region Command
		static DbCommand CreateCommand(this Tuple<string, List<DbParameter>> info, DbConnection connection)
		{
			var command = connection.CreateCommand();
			command.CommandText = info.Item1;
			info.Item2.ForEach(parameter =>
			{
				command.Parameters.Add(parameter);
			});
			return command;
		}

		static DbCommand CreateCommand(this Tuple<string, List<DbParameter>> info, DbProviderFactory providerFactory)
		{
			var command = providerFactory.CreateCommand();
			command.CommandText = info.Item1;
			info.Item2.ForEach(parameter =>
			{
				command.Parameters.Add(parameter);
			});
			return command;
		}

		static DbParameter CreateParameter(this KeyValuePair<string, object> info, DbProviderFactory providerFactory)
		{
			var parameter = providerFactory.CreateParameter();
			parameter.ParameterName = info.Key;
			parameter.Value = info.Value;
			parameter.DbType = info.Key.EndsWith("ID")
				? DbType.AnsiStringFixedLength
				:  SqlHelper.DbTypes[info.Value.GetType()];
			return parameter;
		}
		#endregion

		#region Helpers
		internal static Dictionary<Type, DbType> DbTypes = new Dictionary<Type, DbType>()
		{
			{ typeof(byte), DbType.Byte },
			{ typeof(sbyte), DbType.SByte },
			{ typeof(short), DbType.Int16 },
			{ typeof(ushort), DbType.UInt16 },
			{ typeof(int), DbType.Int32 },
			{ typeof(uint), DbType.UInt32 },
			{ typeof(long), DbType.Int64 },
			{ typeof(ulong), DbType.UInt64 },
			{ typeof(float), DbType.Single },
			{ typeof(double), DbType.Double },
			{ typeof(decimal), DbType.Decimal },
			{ typeof(bool), DbType.Boolean },
			{ typeof(string), DbType.String },
			{ typeof(char), DbType.StringFixedLength },
			{ typeof(Guid), DbType.Guid },
			{ typeof(DateTime), DbType.DateTime },
			{ typeof(DateTimeOffset), DbType.DateTimeOffset },
			{ typeof(byte[]), DbType.Binary },
			{ typeof(byte?), DbType.Byte },
			{ typeof(sbyte?), DbType.SByte },
			{ typeof(short?), DbType.Int16 },
			{ typeof(ushort?), DbType.UInt16 },
			{ typeof(int?), DbType.Int32 },
			{ typeof(uint?), DbType.UInt32 },
			{ typeof(long?), DbType.Int64 },
			{ typeof(ulong?), DbType.UInt64 },
			{ typeof(float?), DbType.Single },
			{ typeof(double?), DbType.Double },
			{ typeof(decimal?), DbType.Decimal },
			{ typeof(bool?), DbType.Boolean },
			{ typeof(char?), DbType.StringFixedLength },
			{ typeof(Guid?), DbType.Guid },
			{ typeof(DateTime?), DbType.DateTime },
			{ typeof(DateTimeOffset?), DbType.DateTimeOffset },
		};

		static DbType GetDbType(this ObjectService.AttributeInfo attribute)
		{
			return attribute.Type.IsDateTimeType() && attribute.IsDateTimeString
				? DbType.AnsiStringFixedLength
				: attribute.Type.IsStringType() && (attribute.Name.EndsWith("ID") || attribute.MaxLength.Equals(32))
					? DbType.AnsiStringFixedLength
					: SqlHelper.DbTypes[attribute.Type];
		}

		static T Copy<T>(this DbDataReader reader, Dictionary<string, ObjectService.AttributeInfo> attributes) where T : class
		{
			T @object = Activator.CreateInstance<T>();
			for (var index = 0; index < reader.FieldCount; index++)
			{
				var name = reader.GetName(index);
				if (@object is RepositoryBase)
					(@object as RepositoryBase).SetProperty(name, reader[index]);
				else if (attributes.ContainsKey(name))
					@object.SetAttributeValue(attributes[name], reader[index], true);
			}
			return @object;
		}
		#endregion

		#region Create
		static Tuple<string, List<DbParameter>> GenerateCreatingInfo<T>(this T @object, DbProviderFactory providerFactory) where T : class
		{
			string columns = "", values = "";
			var parameters = new List<DbParameter>();
			var definition = RepositoryMediator.GetEntityDefinition<T>();
			definition.Attributes.ForEach(attribute =>
			{
				columns += (string.IsNullOrEmpty(attribute.Column) ? attribute.Name : attribute.Column) + ",";
				values += "@" + attribute.Name + ",";

				var parameter = providerFactory.CreateParameter();
				parameter.ParameterName = "@" + attribute.Name;
				parameter.Value = @object.GetAttributeValue(attribute.Name);
				parameter.DbType = attribute.GetDbType();
				parameters.Add(parameter);
			});

			return new Tuple<string, List<DbParameter>>(
					"INSERT INTO " + definition.TableName + " (" + columns.Left(columns.Length - 1) + ") VALUES (" + values.Left(values.Length - 1) + ")", 
					parameters
				);
		}

		/// <summary>
		/// Creates new the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		public static void Create<T>(RepositoryContext context, DataSource dataSource, T @object) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot create new because the object is null");

			DbProviderFactory providerFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = context.GetSqlConnection(dataSource, providerFactory))
			{
				var command = @object.GenerateCreatingInfo(providerFactory).CreateCommand(connection);
				connection.Open();
				command.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Creates new the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task CreateAsync<T>(RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot create new because the object is null");

			DbProviderFactory providerFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = context.GetSqlConnection(dataSource, providerFactory))
			{
				var command = @object.GenerateCreatingInfo(providerFactory).CreateCommand(connection);
				await connection.OpenAsync(cancellationToken);
				await command.ExecuteNonQueryAsync(cancellationToken);
			}
		}
		#endregion

		#region Get
		static Tuple<string, List<DbParameter>> GenerateGettingInfo<T>(this string id, DbProviderFactory providerFactory, HashSet<string> included = null, HashSet<string> excluded = null) where T : class
		{
			var statement = "";
			var definition = RepositoryMediator.GetEntityDefinition<T>();
			definition.Attributes.ForEach(attribute =>
			{
				if  ((included == null || included.Contains(attribute.Name)) && (excluded == null || !excluded.Contains(attribute.Name)))
					statement += (string.IsNullOrEmpty(attribute.Column) ? attribute.Name : attribute.Column) + ",";
			});

			if (statement.Equals(""))
				return null;

			var info = Filters.Equals<T>(definition.PrimaryKey, id).GetSqlStatement();
			return new Tuple<string, List<DbParameter>>(
					"SELECT TOP 1 " + statement.Left(statement.Length - 1) + " FROM " + definition.TableName + " WHERE " + info.Item1,
					new List<DbParameter>() { info.Item2.First().CreateParameter(providerFactory) }
				);
		}

		/// <summary>
		/// Finds the first record that matched with the filter and construc an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort) where T : class
		{
			return default(T);
		}

		/// <summary>
		/// Gets the record and construct an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The string that presents identity</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, DataSource dataSource, string id) where T : class
		{
			T @object = default(T);
			if (string.IsNullOrEmpty(id))
				return @object;

			DbProviderFactory providerFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = context.GetSqlConnection(dataSource, providerFactory))
			{
				var command = id.GenerateGettingInfo<T>(providerFactory).CreateCommand(connection);
				connection.Open();
				using (var reader = command.ExecuteReader())
				{
					if (reader.Read())
						@object = reader.Copy<T>(context.EntityDefinition.Attributes.ToDictionary(a => a.Name));
				}
			}
			return @object;
		}

		/// <summary>
		/// Finds the first record that matched with the filter and construc an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<T>(default(T));
		}

		/// <summary>
		/// Gets the record and construct an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The string that presents identity</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			T @object = default(T);
			if (string.IsNullOrEmpty(id))
				return @object;

			DbProviderFactory providerFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = context.GetSqlConnection(dataSource, providerFactory))
			{
				var command = id.GenerateGettingInfo<T>(providerFactory).CreateCommand(connection);
				await connection.OpenAsync(cancellationToken);
				using (var reader = await command.ExecuteReaderAsync(cancellationToken))
				{
					if (await reader.ReadAsync(cancellationToken))
						@object = reader.Copy<T>(context.EntityDefinition.Attributes.ToDictionary(a => a.Name));
				}
			}
			return @object;
		}
		#endregion

		#region Replace
		/// <summary>
		/// Replaces the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		public static void Replace<T>(RepositoryContext context, DataSource dataSource, T @object) where T : class
		{

		}

		/// <summary>
		/// Replaces the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<T>(RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.CompletedTask;
		}
		#endregion

		#region Update
		/// <summary>
		/// Updates the record of an object (only update changed atributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="attributes">The collection of attributes for updating individually</param>
		public static void Update<T>(RepositoryContext context, DataSource dataSource, T @object, List<string> attributes) where T : class
		{

		}

		/// <summary>
		/// Updates the record of an object (only update changed atributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="attributes">The collection of attributes for updating individually</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<T>(RepositoryContext context, DataSource dataSource, T @object, List<string> attributes, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.CompletedTask;
		}
		#endregion

		#region Delete
		/// <summary>
		/// Delete the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The identity of the document of an object for deleting</param>
		public static void Delete<T>(RepositoryContext context, DataSource dataSource, string id) where T : class
		{
			
		}

		/// <summary>
		/// Delete the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The identity of the document of an object for deleting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync<T>(RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Delete the record of the objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter for deleting</param>
		public static void DeleteMany<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter) where T : class
		{

		}

		/// <summary>
		/// Delete the record of the objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter for deleting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteManyAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.CompletedTask;
		}
		#endregion

		#region Select
		/// <summary>
		/// Finds all the matched records and return the collection of <see cref="DataRow">DataRow</see> objects with limited attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include identity attribute only)</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <returns></returns>
		public static List<DataRow> Select<T>(RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber) where T : class
		{
			return new List<DataRow>();
		}

		/// <summary>
		/// Finds all the matched records and return the collection of <see cref="DataRow">DataRow</see> objects with limited attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include identity attribute only)</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<DataRow>> SelectAsync<T>(RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<List<DataRow>>(new List<DataRow>());
		}

		/// <summary>
		/// Finds all the matched documents and return the collection of identity attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <returns></returns>
		public static List<string> SelectIdentities<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber) where T : class
		{
			return SqlHelper.Select(context, dataSource, null, filter, sort, pageSize, pageNumber)
				.Select(data => data["ID"] as string)
				.ToList();
		}

		/// <summary>
		/// Finds all the matched records and return the collection of identity attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> SelectIdentitiesAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return (await SqlHelper.SelectAsync(context, dataSource, null, filter, sort, pageSize, pageNumber))
				.Select(data => data["ID"] as string)
				.ToList();
		}
		#endregion

		#region Find
		/// <summary>
		/// Finds the records and construct the collection of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <returns></returns>
		public static List<T> Find<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber) where T : class
		{
			return new List<T>();
		}

		/// <summary>
		/// Finds the records and construct the collection of objects that specified by identity
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="identities">The collection of identities for finding</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <returns></returns>
		public static List<T> Find<T>(RepositoryContext context, DataSource dataSource, List<string> identities, SortBy<T> sort = null) where T : class
		{
			if (identities == null || identities.Count < 1)
				return new List<T>();

			var filter = Filters.Or<T>();
			identities.ForEach(id =>
			{
				filter.Add(Filters.Equals<T>("ID", id));
			});

			return SqlHelper.Find(context, dataSource, filter, sort, 0, 1);
		}

		/// <summary>
		/// Finds the records and construct the collection of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<List<T>>(new List<T>());
		}

		/// <summary>
		/// Finds the records and construct the collection of objects that specified by identity
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="identities">The collection of identities for finding</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(RepositoryContext context, DataSource dataSource, List<string> identities, SortBy<T> sort = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (identities == null || identities.Count < 1)
				return Task.FromResult<List<T>>(new List<T>());

			var filter = Filters.Or<T>();
			identities.ForEach(id =>
			{
				filter.Add(Filters.Equals<T>("ID", id));
			});

			return SqlHelper.FindAsync(context, dataSource, filter, sort, 0, 1, cancellationToken);
		}
		#endregion

		#region Search
		/// <summary>
		/// Searchs the records and construct the collection of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filter</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <returns></returns>
		public static List<T> Search<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber) where T : class
		{
			return new List<T>();
		}

		/// <summary>
		/// Searchs the records and construct the collection of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filter</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<List<T>>(new List<T>());
		}

		/// <summary>
		/// Searchs all the matched records and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filter</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <returns></returns>
		public static List<string> SearchIdentities<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber) where T : class
		{
			return new List<string>();
		}

		/// <summary>
		/// Searchs all the matched records and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filter</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<string>> SearchIdentitiesAsync<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<List<string>>(new List<string>());
		}
		#endregion

		#region Count
		/// <summary>
		/// Counts the number of all matched records
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <returns></returns>
		public static long Count<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter) where T : class
		{
			return 0;
		}

		/// <summary>
		/// Counts the number of all matched records
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<long>(0);
		}

		/// <summary>
		/// Counts the number of all matched records
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <returns></returns>
		public static long Count<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter) where T : class
		{
			return 0;
		}

		/// <summary>
		/// Counts the number of all matched records
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountAsync<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<long>(0);
		}
		#endregion

	}
}