#region Related components
using System;
using System.Collections.Generic;
using System.Diagnostics;

using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json.Linq;
#endregion

namespace net.vieapps.Components.Repository
{
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
				Mode = (RepositoryMode)Enum.Parse(typeof(RepositoryMode), (settings["mode"] as JValue).Value as string)
			};

			// name of connection string (SQL and NoSQL)
			if (dataSource.Mode.Equals(RepositoryMode.SQL) || dataSource.Mode.Equals(RepositoryMode.NoSQL))
			{
				if (settings["connectionStringName"] == null)
					throw new ArgumentNullException("connectionStringName", "[connectionStringName] attribute of settings");
				dataSource.ConnectionStringName = (settings["connectionStringName"] as JValue).Value as string;
			}

			// name of database (NoSQL)
			if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
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
	/// Access permissions of a resource (means working permissions of a run-time entity)
	/// </summary>
	[Serializable]
	public class AccessPermissions
	{

		public AccessPermissions()
		{
			this.DownloadableRoles = new HashSet<string>();
			this.DownloadableUsers = new HashSet<string>();
			this.ViewableRoles = new HashSet<string>();
			this.ViewableUsers = new HashSet<string>();
			this.ContributiveRoles = new HashSet<string>();
			this.ContributiveUsers = new HashSet<string>();
			this.EditableRoles = new HashSet<string>();
			this.EditableUsers = new HashSet<string>();
			this.ModerateRoles = new HashSet<string>();
			this.ModerateUsers = new HashSet<string>();
			this.AdministrativeRoles = new HashSet<string>();
			this.AdministrativeUsers = new HashSet<string>();
		}

		#region Properties
		/// <summary>
		/// Gets or sets the collection of identity of working roles that able to download files/attachments of the published resources
		/// </summary>
		public HashSet<string> DownloadableRoles { get; set; }

		/// <summary>
		/// Gets or sets the collection of identity of users that able to download files/attachments of the published resources
		/// </summary>
		public HashSet<string> DownloadableUsers { get; set; }

		/// <summary>
		/// Gets or sets the collection of identity of working roles that able to view the details (means read-only on published resources)
		/// </summary>
		public HashSet<string> ViewableRoles { get; set; }

		/// <summary>
		/// Gets or sets the collection of identity of users that able to view the details (means read-only on published resources)
		/// </summary>
		public HashSet<string> ViewableUsers { get; set; }

		/// <summary>
		/// Gets or sets the collection of identity of working roles that able to contribute (means create new and view the published/their own resources)
		/// </summary>
		public HashSet<string> ContributiveRoles { get; set; }

		/// <summary>
		/// Gets or sets the collection of identity of users that able to contribute (means create new and view the published/their own resources)
		/// </summary>
		public HashSet<string> ContributiveUsers { get; set; }

		/// <summary>
		/// Gets or sets the collection of identity of working roles that able to edit (means create new and re-update the published resources)
		/// </summary>
		public HashSet<string> EditableRoles { get; set; }

		/// <summary>
		/// Gets or sets the collection of identity of users that able to edit (means create new and re-update the published resources)
		/// </summary>
		public HashSet<string> EditableUsers { get; set; }

		/// <summary>
		/// Gets or sets the collection of identity of working roles that able to moderate (means moderate all kinds of resources)
		/// </summary>
		public HashSet<string> ModerateRoles { get; set; }

		/// <summary>
		/// Gets or sets the collection of identity of users that able to moderate (means moderate all kinds of resources)
		/// </summary>
		public HashSet<string> ModerateUsers { get; set; }

		/// <summary>
		/// Gets or sets the collection of identity of working roles that able to manage (means full access)
		/// </summary>
		public HashSet<string> AdministrativeRoles { get; set; }

		/// <summary>
		/// Gets or sets the collection of identity of users that able to manage (means full access)
		/// </summary>
		public HashSet<string> AdministrativeUsers { get; set; }
		#endregion

		#region Helper methods
		internal static bool IsEmpty(HashSet<string> roles, HashSet<string> users)
		{
			return (roles == null || roles.Count < 1) && (users == null || users.Count < 1);
		}

		internal static bool IsNotEmpty(HashSet<string> roles, HashSet<string> users)
		{
			return (roles != null && roles.Count > 0) || (users != null && users.Count > 0);
		}
		#endregion

	}
}