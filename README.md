# ThreadedFetchDemo
Makes sure a fetch request is threaded and can run concurrently.

To run the demo do dotnet run in the terminal at the root of the project.

Call to the demo using an API testing service like postman and use http://localhost:5181/wikiApiFetch?maxTaskCount=5


maxTaskCount being defined will use a set link, if you don't define it at all or put something invalid in it will go through a list of links.