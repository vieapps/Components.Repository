# Components.Repository
Tiny ORM-ish component to help POCO objects can work with both NoSQL and SQL in the same time

Features:
- Work with NoSQL database (MongoDB) and SQL database (Microsoft SQL, MySQL, PostgreSQL, Oracle, and ODBC)
- Attributes/Columns are mapped like the way of ActiveRecord
- Integrated with caching component (Components.Caching) to reduce all I/O round trips
- Built-in serializations with JSON and XML
- Async supported