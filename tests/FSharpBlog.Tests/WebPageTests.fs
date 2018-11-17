module Tests

open System
open System.IO
open System.Net
open System.Net.Http
open Xunit
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection


// ---------------------------------
// Helper functions (extend as you need)
// ---------------------------------

let createHost( configureDataServices ) =
    WebHostBuilder()
        .UseContentRoot(Directory.GetCurrentDirectory())
        .Configure(Action<IApplicationBuilder> FSharpBlog.App.configureApp)
        .ConfigureServices(Action<IServiceCollection> FSharpBlog.App.configureServices)
        .ConfigureServices(Action<IServiceCollection> configureDataServices)


let runTask task =
    task
    |> Async.AwaitTask
    |> Async.RunSynchronously

///This method must be added after the runTask function implementation
let readContent (response : HttpResponseMessage) =
    let stream = response.Content.ReadAsStreamAsync() |> runTask
    let doc =  HtmlAgilityPack.HtmlDocument(); 
    doc.Load stream |> ignore
    let contentElement = doc.GetElementbyId "content"
    if contentElement = null then failwith "Content element not found"
    contentElement.InnerText


let httpGet (path : string) (client : HttpClient) =
    path
    |> client.GetAsync
    |> runTask

let httpPost (path : string) (data: obj) (client : HttpClient) =
    let jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(data)
    use httpContent = new System.Net.Http.StringContent(jsonData, Text.Encoding.UTF8, "application/json")
    client.PostAsync(path, httpContent)
    |> runTask

let isStatus (code : HttpStatusCode) (response : HttpResponseMessage) =
    Assert.Equal(code, response.StatusCode)
    response

let ensureSuccess (response : HttpResponseMessage) =
    if not response.IsSuccessStatusCode
    then response.Content.ReadAsStringAsync() |> runTask |> failwithf "%A"
    else response

let readText (response : HttpResponseMessage) =
    response.Content.ReadAsStringAsync()
    |> runTask

let shouldEqual (expected:'a) (actual:'a) =
    Assert.Equal<'a>(expected, actual)

let shouldContain (expected : string) (actual : string) =
    Assert.True(actual.Contains expected)

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``Route /myPostPath returns content of  post with path myPostPath`` () =
   
    let configureDataServices(services:IServiceCollection) =
      // Create function which will get page data for given function
      let getPageData path =
        { 
            FSharpBlog.Model.Post.Id = 0
            FSharpBlog.Model.Post.PageContent = sprintf "The content on my post for path:%s" path
            FSharpBlog.Model.Post.Title = null
            FSharpBlog.Model.Post.UrlPath = null
        } |> Some
      // Inject our function into Asp.Net services
      services.AddSingleton<FSharpBlog.DataAccess.GetPageData>( getPageData  ) |> ignore
    
    let host = createHost( configureDataServices )
    use server = new TestServer(host)
    use client = server.CreateClient()

    client
    |> httpGet "/myPostPath"
    |> ensureSuccess
    |> readContent
    |> shouldEqual "The content on my post for path:myPostPath"

[<Fact>]
let ``Route for not extisting post returns 404`` () =
   
    let configureDataServices(services:IServiceCollection) =
      // Create function which will get page data for given function
      let getPageData path =
        None
      // Inject our function into Asp.Net services
      services.AddSingleton<FSharpBlog.DataAccess.GetPageData>( getPageData  ) |> ignore
    
    let host = createHost( configureDataServices )
    use server = new TestServer(host)
    use client = server.CreateClient()

    client
    |> httpGet "/notExistingUrl"
    |> isStatus HttpStatusCode.NotFound
    |> readText
    |> shouldEqual "Post Not Found"
     
[<Fact>]
let ``Route /addPost saves new blog post`` () =
    // Here we will store data which was saved by web app
    let mutable savedData = None        

    let configureDataServices(services:IServiceCollection) =
      // Create function which will save post
      let addPost postData =
        savedData <- Some postData
        {
            FSharpBlog.Model.PostActionResultModel.Success = true
            FSharpBlog.Model.PostActionResultModel.Error = null 
            FSharpBlog.Model.PostActionResultModel.PostId = 123 
        }
      // Inject our function into Asp.Net services
      services.AddSingleton<FSharpBlog.DataAccess.AddPost>( addPost  ) |> ignore 
    let host = createHost( configureDataServices )
    
    use server = new TestServer( host )
    use client = server.CreateClient()
    let newData = 
       { 
            FSharpBlog.Model.Post.Id = 0
            FSharpBlog.Model.Post.PageContent = "My new post"
            FSharpBlog.Model.Post.Title = "New title"
            FSharpBlog.Model.Post.UrlPath = "New url path"
       }
    client
    |> httpPost "addPost" newData
    |> ensureSuccess
    |> readText
    |> Newtonsoft.Json.JsonConvert.DeserializeObject<FSharpBlog.Model.PostActionResultModel>
    |> shouldEqual 
        {
            FSharpBlog.Model.PostActionResultModel.Success = true
            FSharpBlog.Model.PostActionResultModel.Error = null 
            FSharpBlog.Model.PostActionResultModel.PostId = 123 
        }
    shouldEqual newData savedData.Value


[<Fact>]
let ``Route /addPost returns error message if post can't be saved`` () =
    let configureDataServices(services:IServiceCollection) =
      // Create function which will save post
      let addPost postData =
        {
            FSharpBlog.Model.PostActionResultModel.Success = false
            FSharpBlog.Model.PostActionResultModel.Error = "Not saved" 
            FSharpBlog.Model.PostActionResultModel.PostId = 0 
        }
      // Inject our function into Asp.Net services
      services.AddSingleton<FSharpBlog.DataAccess.AddPost>( addPost  ) |> ignore 
    let host = createHost( configureDataServices )
    
    use server = new TestServer( host )
    use client = server.CreateClient()
    let newData = 
       { 
            FSharpBlog.Model.Post.Id = 0
            FSharpBlog.Model.Post.PageContent = "My new post"
            FSharpBlog.Model.Post.Title = "New title"
            FSharpBlog.Model.Post.UrlPath = "New url path"
       }
    client
    |> httpPost "addPost" newData
    |> ensureSuccess
    |> readText
    |> Newtonsoft.Json.JsonConvert.DeserializeObject<FSharpBlog.Model.PostActionResultModel>
    |> shouldEqual 
        {
            FSharpBlog.Model.PostActionResultModel.Success = false
            FSharpBlog.Model.PostActionResultModel.Error = "Not saved" 
            FSharpBlog.Model.PostActionResultModel.PostId = 0 
        }
    

[<Fact>]
let ``Route /addPost returns view`` () =
   
    let configureDataServices(services:IServiceCollection) =
      ()
    
    let host = createHost( configureDataServices )
    use server = new TestServer(host)
    use client = server.CreateClient()

    client
    |> httpGet "/addPost"
    |> ensureSuccess