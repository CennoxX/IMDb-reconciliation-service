# .NET Core reconciliation service
OpenRefine reconciliation service using the JSON-LD data of IMDb

.NET Core implementation of an OpenRefine reconciliation service that takes IMDb-IDs and blindly verifies them to themself. The Reconciliation Service API provides the Service Metadata, returns the title or name and the url, so that reconciled IMDb-IDs are clickable links. Further it is possible to extend the data by using the JSON-LD data of IMDb with the Data Extension API. The Preview API, Property Proposal API and Suggest API (for properties) are also implemented.

The service is easy adaptable for other websites using JSON-LD.

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
* Select a column containing IMDb-IDs > Reconcile > Start Reconciling…
* Add the following reconciliation service URL: http://localhost:5000/imdb-reconcile/api
* Click "Start Reconciling"

To extend data:
* Select a column containing IMDb-IDs > Edit Columns > Add columns from reconciled values…
* Choose one or multiple of "Suggested Properties"
* Click on "OK"

-----------
For more info, see https://github.com/OpenRefine/OpenRefine/wiki/Reconciliation-Service-API.
