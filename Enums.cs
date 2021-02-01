namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Presents the working mode of a repository
	/// </summary>
	public enum RepositoryMode
	{
		/// <summary>
		/// Specifies to use SQL database (SQLServer, MySQL, PostgreSQL)
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
		/// Query objects (find/search/count)
		/// </summary>
		Query
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents the operator of a group of comparing expressions
	/// </summary>
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
	public enum ExtendedPropertyMode
	{
		/// <summary>
		/// Small Unicode text (max length: 250)
		/// </summary>
		SmallText,

		/// <summary>
		/// Medium Unicode text (max length: 4000)
		/// </summary>
		MediumText,

		/// <summary>
		/// Large Unicode text (CLOB)
		/// </summary>
		LargeText,

		/// <summary>
		/// Yes/No (boolean)
		/// </summary>
		YesNo,

		/// <summary>
		/// Date Time
		/// </summary>
		DateTime,

		/// <summary>
		/// Integral number (long)
		/// </summary>
		IntegralNumber,

		/// <summary>
		/// Floating point number (decimal)
		/// </summary>
		FloatingPointNumber,

		/// <summary>
		/// Select (one or more) from the pre-defined values (stored as small-text, multiple values are separated by hashtag and semicolon - [#;])
		/// </summary>
		Select,

		/// <summary>
		/// Lookup identities (stored as medium-text, multiple values are separated comma [,])
		/// </summary>
		Lookup
	}
}