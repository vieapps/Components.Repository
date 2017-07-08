﻿using System;

namespace net.vieapps.Components.Repository
{
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

	[Serializable]
	public enum ExtendedPropertyMode
	{
		/// <summary>
		/// Unicode text
		/// </summary>
		Text,

		/// <summary>
		/// Large (CLOB) unicode text
		/// </summary>
		LargeText,

		/// <summary>
		/// Yes/No (boolean)
		/// </summary>
		YesNo,

		/// <summary>
		/// Choice from the pre-defined values
		/// </summary>
		Choice,

		/// <summary>
		/// Date Time
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
		/// Hyper-link (http://)
		/// </summary>
		HyperLink,

		/// <summary>
		/// Lookup (contains comma seperated value of identites)
		/// </summary>
		Lookup,

		/// <summary>
		/// User information (contains comma seperated value of identites)
		/// </summary>
		User
	}

}