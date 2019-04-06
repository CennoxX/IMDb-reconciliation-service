# imdb-reconcile
OpenRefine reconciliation service

.NET Core implementation of an OpenRefine reconciliation service that just returns the given name/id with a base url prepended, so that reconciled IMDb-IDs are clickable links.

At the moment all it does, is blindly verifying any IMDb-ID to itself, and provide the following metadata:

    metadata = {
      "name": "IMDb (en)",
      "defaultTypes": [{"id": "/imdb/Title", "name": "Title"}],
      "view": { "url" : "https://www.imdb.org/title/{{id}}" } 
    }

Requirements
------------
Requires [.NET Core 2.2 SDK](https://www.microsoft.com/net/download/all)

Starting the server
-------------------

To start the service:
```
dotnet run IMDbWebApi
```

To use in OpenRefine:
* Select a column containing IMDb-IDs > Reconcile > Start Reconciling...
* Add the following reconciliation service URL: http://localhost:5000/imdb-reconcile/api
* Click "Start Reconciling"

Inspiration
-----------
This was adapted from https://github.com/dergachev/redmine-reconcile

For more info, see https://github.com/OpenRefine/OpenRefine/wiki/Reconciliation-Service-API.
