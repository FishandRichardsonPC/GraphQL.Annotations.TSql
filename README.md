# GraphQL.Annotations.TSql
[![Build Status](https://dev.azure.com/fishandrichardson-oss/GraphQL.Annotations.TSql/_apis/build/status/FishandRichardsonPC.GraphQL.Annotations.TSql?branchName=master)](https://dev.azure.com/fishandrichardson-oss/GraphQL.Annotations.TSql/_build/latest?definitionId=1&branchName=master)
[![SemVer](https://img.shields.io/nuget/v/GraphQL.Annotations.TSql.svg)](https://semver.org)
[![Nuget](https://img.shields.io/nuget/dt/GraphQL.Annotations.TSql.svg)](https://www.nuget.org/packages/GraphQL.Annotations.TSql)


C# Library for adding objects which map to TSQL Tables to a graphql schema, Intended to be used with the GraphQL 
package on nuget. A sample project is included with the db schema required to run it. The client side code is based on
the react ui from [haikyuu/graphql-todo-list](https://github.com/haikyuu/graphql-todo-list/tree/master/client/src) 

# Usage
If you get confused with these instructions refer to the `GraphQL.Annotations.ToDo.Example` project 

## Setup
1. Add the GraphQL.Annotations.TSql nuget package
2. Create your schema, query, and mutation objects and add them to DI
3. Add `IServiceProvider` as a constructor parameter to both your Query and Mutation types
4. Add `this.AddGraphQLTSqlMutationFields<Mutation>(serviceProvider);` inside the constructor of the mutation class
5. Add `this.AddGraphQLTSqlQueryFields<Query>(serviceProvider);` inside the constructor of the query class.
6. Add `services.AddGraphQLTSql<Query, Mutation>();` inside your Startup.ConfigureServices method
7. Create a class implementing `ISqlConnectionGetter` and add it to DI using the interface. This class will be used to 
    create all connections, the return value of the `GetConnection` method will be automatically disposed
8. If you are using the GraphQL.Server package with Asp.Net
    1. Add the `GraphQL.Annotations.TSql.AspNetCore` package
    2. Add `.AddHttpContextUserContextBuilder()` to the end of your `services.AddGraphQL` call

## Creating objects
This library is based on the [GraphQL.Annotations](https://github.com/dlukez/graphql-dotnet-annotations) library and uses
similar syntax
### For query only objects
This is the simpler case and will only add the object to your query type
1. Inherit from `SqlFieldResolver<T>` filling in the class name as the type argument
3. Create properties and annotate them using SqlGraphQLField. Each field must be nullable otherwise you will run into 
    runtime errors
    * You should not start any of your fields with an `_` all system fields will start with one and using them will 
        potentially result in naming conflicts
2. Implement the `Table`, `DefaultOrder`, and `PrimaryProperty` properties
    * `Table` is the table or view that this object is bound to
    * `DefaultOrder` is the property which will be used for ordering by default. Use the property name, not the sql 
        field name
    * `PrimaryProperty` is the field which contains the primary key. Use the property name, not the sql field name. This
        MUST return a unique value per row, it is advised that this is also indexed
### For mutable objects
These are a little more involved documentation is in the [wiki](https://github.com/FishandRichardsonPC/GraphQL.Annotations.TSql/wiki/Mutable-Objects)
### Querying objects
The documentation for querying should be in the resulting graphql schema. Some of the potential problems and things to watch out for are in the [wiki](https://github.com/FishandRichardsonPC/GraphQL.Annotations.TSql/wiki/Querying)

Additional features can be found in the [wiki](https://github.com/FishandRichardsonPC/GraphQL.Annotations.TSql/wiki).

# Releasing
All Pull Requests should be available as a prerelease on nuget.org. To create an official release create a release in github
with the new version number, after the build completes it will be uploaded to nuget.org