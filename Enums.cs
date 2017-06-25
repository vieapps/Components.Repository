using System;

namespace net.vieapps.Components.Repository
{
	[Serializable]
	public enum RepositoryModes
	{
		/// <summary>
		/// Specifies to use SQL database (SQL Server, MySQL, Oracle, ...)
		/// </summary>
		SQL,

		/// <summary>
		/// Specifies to use NoSQL database (MongoDB)
		/// </summary>
		NoSQL,
	}

	[Serializable]
	public enum RepositoryOperations
	{
		/// <summary>
		/// Create/Add new object
		/// </summary>
		Create,
		/// <summary>
		/// Get/Retrieve/Fetch an object
		/// </summary>
		Get,
		/// <summary>
		/// Update/Save an object (or piece of data of an instance)
		/// </summary>
		Update,
		/// <summary>
		/// Delete/Remove an object
		/// </summary>
		Delete,
		/// <summary>
		/// Query objects (search/count)
		/// </summary>
		Query
	}

	[Serializable]
	public enum GroupOperators
	{
		/// <summary>
		/// Group all filter-by expression with AND operator
		/// </summary>
		And = 0,

		/// <summary>
		/// Group all filter-by expression with OR operator
		/// </summary>
		Or = 1,
	}

	[Serializable]
	public enum CompareOperators
	{
		/// <summary>
		/// Equals operator (==)
		/// </summary>
		Equals = 0,

		/// <summary>
		/// Not equals operator (!=)
		/// </summary>
		NotEquals = 1,

		/// <summary>
		/// Less than operator (&lt;)
		/// </summary>
		LessThan = 2,

		/// <summary>
		/// Less than or equals operator (&lt;=)
		/// </summary>
		LessThanOrEquals = 3,

		/// <summary>
		/// Greater operator (&gt;)
		/// </summary>
		Greater = 4,

		/// <summary>
		/// Greater or equals operator (&gt;=)
		/// </summary>
		GreaterOrEquals = 5,

		/// <summary>
		/// Contains operator (LIKE in SQL, RegEx in NoSQL)
		/// </summary>
		Contains = 6,

		/// <summary>
		/// Starts with operator (means starts with sub-string)
		/// </summary>
		StartsWith = 7,

		/// <summary>
		/// Ends with operator (means ends with sub-string)
		/// </summary>
		EndsWith = 8,

		/// <summary>
		/// Is null operator (IS NULL in SQL)
		/// </summary>
		IsNull = 9,

		/// <summary>
		/// Is not null operator (IS NOT NULL in SQL)
		/// </summary>
		IsNotNull = 10,

		/// <summary>
		/// Is empty operator (=='')
		/// </summary>
		IsEmpty = 11,

		/// <summary>
		/// Is not empty operator (!='')
		/// </summary>
		IsNotEmpty = 12,
	}

	[Serializable]
	public enum SortModes
	{
		/// <summary>
		/// Ascending sort
		/// </summary>
		Ascending = 0,

		/// <summary>
		/// Descending sort
		/// </summary>
		Descending = 1
	}
}