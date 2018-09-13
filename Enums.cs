using System;

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Presents the working mode of a repository
	/// </summary>
	[Serializable]
	public enum RepositoryMode
	{
		/// <summary>
		/// Specifies to use SQL database (SQL Server, MySQL, Oracle, ...)
		/// </summary>
		SQL,

		/// <summary>
		/// Specifies to use NoSQL database (MongoDB)
		/// </summary>
		NoSQL
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents the operation of a repository's context
	/// </summary>
	[Serializable]
	public enum RepositoryOperation
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

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents the operator of a group of comparing expressions
	/// </summary>
	[Serializable]
	public enum GroupOperator
	{
		/// <summary>
		/// Group all filter-by expression with AND operator
		/// </summary>
		And,

		/// <summary>
		/// Group all filter-by expression with OR operator
		/// </summary>
		Or
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents the operation of a comparing expression
	/// </summary>
	[Serializable]
	public enum CompareOperator
	{
		/// <summary>
		/// Equals operator (==)
		/// </summary>
		Equals,

		/// <summary>
		/// Not equals operator (!=)
		/// </summary>
		NotEquals,

		/// <summary>
		/// Less than operator (&lt;)
		/// </summary>
		LessThan,

		/// <summary>
		/// Less than or equals operator (&lt;=)
		/// </summary>
		LessThanOrEquals,

		/// <summary>
		/// Greater operator (&gt;)
		/// </summary>
		Greater,

		/// <summary>
		/// Greater or equals operator (&gt;=)
		/// </summary>
		GreaterOrEquals,

		/// <summary>
		/// Contains operator (LIKE in SQL, RegEx in NoSQL)
		/// </summary>
		Contains,

		/// <summary>
		/// Starts with operator (means starts with sub-string)
		/// </summary>
		StartsWith,

		/// <summary>
		/// Ends with operator (means ends with sub-string)
		/// </summary>
		EndsWith,

		/// <summary>
		/// Is null operator (IS NULL in SQL)
		/// </summary>
		IsNull,

		/// <summary>
		/// Is not null operator (IS NOT NULL in SQL)
		/// </summary>
		IsNotNull,

		/// <summary>
		/// Is empty operator (=='')
		/// </summary>
		IsEmpty,

		/// <summary>
		/// Is not empty operator (!='')
		/// </summary>
		IsNotEmpty
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents the sorting mode of a sorting expression
	/// </summary>
	[Serializable]
	public enum SortMode
	{
		/// <summary>
		/// Ascending sort
		/// </summary>
		Ascending,

		/// <summary>
		/// Descending sort
		/// </summary>
		Descending
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents the data mode of an extended property
	/// </summary>
	[Serializable]
	public enum ExtendedPropertyMode
	{
		/// <summary>
		/// Small Unicode text - max length = 250
		/// </summary>
		SmallText,

		/// <summary>
		/// Medium Unicode text - max length = 4000
		/// </summary>
		MediumText,

		/// <summary>
		/// Large (CLOB) unicode text
		/// </summary>
		LargeText,

		/// <summary>
		/// Yes/No (boolean)
		/// </summary>
		YesNo,

		/// <summary>
		/// Select one (or more as multiple) from the pre-defined values - stored as small-text, multiple values are separated by hashtag and comma (#;)
		/// </summary>
		Select,

		/// <summary>
		/// Date Time - stored as string (max length = 19)
		/// </summary>
		DateTime,

		/// <summary>
		/// Integer number
		/// </summary>
		Number,

		/// <summary>
		/// Decimal number
		/// </summary>
		Decimal,

		/// <summary>
		/// Lookup identities - stored as small-text, multiple values are separated comma (;)
		/// </summary>
		Lookup,

		/// <summary>
		/// User identities - stored as small-text, multiple values are separated comma (;)
		/// </summary>
		User
	}
}