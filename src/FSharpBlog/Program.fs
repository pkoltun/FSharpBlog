module FSharpBlog.App

open Microsoft.AspNetCore.Http
open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe

// ---------------------------------
// Models
// ---------------------------------

type Message =
    {
        Text : string
    }

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open GiraffeViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "FSharpBlog" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "https://cdnjs.cloudflare.com/ajax/libs/bulma/0.7.2/css/bulma.min.css" ]

                script [ _src "https://use.fontawesome.com/releases/v5.3.1/js/all.js" ][]
            ]
            body [] [
                section [ _class "section"] [
                    div[ _class "container" ][
                        h1 [ _class "title"] [ encodedText "FSharpBlog" ]
                        div[] content
                    ]
                ]
            ]
            
        ]

    

    let post (model : Model.Post ) =
     [  
       div [ _id "content" ] [ RawText model.PageContent ]
     ] |> layout

    let addPost =
     let fieldWithLabel name inputControl =
        div[ _class "field" ][
            label[ _class "label"] [ rawText name ]
            div[ _class "control"][
               inputControl ]
        ]
     
     let myStringInput name = 
        fieldWithLabel name (input [ _class "input"; _name name; _type "text" ])
    
     let myTextAreaInput name = 
        fieldWithLabel name (textarea [ _class "textarea"; _name name ][])
     
     [     
        form [ _action "/addPost"; _method "POST" ] 
         [
             myTextAreaInput "PageContent"
             myStringInput "Id"
             myStringInput "Title"
             myStringInput "UrlPath"
             input[ _type "submit"]
         ]     

     ] |> layout

// ---------------------------------
// Web app
// ---------------------------------

let postHandler (postPath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
                let getPageData = ctx.GetService<DataAccess.GetPageData>()
                let model     = getPageData postPath
                match model with
                | Some m ->
                    let view = Views.post m
                    return! htmlView view next ctx
                | None -> return! RequestErrors.notFound( text "Post Not Found" ) next ctx
        }

let addPostHandler (postModel : Model.Post) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
                let addPost = ctx.GetService<DataAccess.AddPost>()
                let result = addPost postModel                               
                return! json result next ctx 
        }

let postViewHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
                 let view = Views.addPost
                 return! htmlView view next ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/addPost" >=> postViewHandler
                routef "/%s" postHandler
            ]
        POST >=> 
            choose [
                route "/addPost" >=> bindModel<Model.Post>(None) addPostHandler 
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins("http://localhost:8080")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IHostingEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler errorHandler)
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureDataServices(services:IServiceCollection) =
      // Create the in memory repository with one post already setup
      let firstPost = 
        { 
            FSharpBlog.Model.Post.Id = 1
            FSharpBlog.Model.Post.PageContent = "The content on my first post"
            FSharpBlog.Model.Post.Title = null
            FSharpBlog.Model.Post.UrlPath = "FirstTest"
        }
      let simplePostRepository = new System.Collections.Generic.Dictionary<string, FSharpBlog.Model.Post>()
      
      let tryFindElement path =
        let success, element =  simplePostRepository.TryGetValue path
        match success with  
        |true -> Some element
        |false -> None
      // The TryFind method is our data access method
      services.AddSingleton<FSharpBlog.DataAccess.GetPageData>( tryFindElement  ) |> ignore

      let addPost (postData:FSharpBlog.Model.Post) =
        simplePostRepository.Add (postData.UrlPath,postData) |> ignore
        {
            FSharpBlog.Model.PostActionResultModel.Success = true
            FSharpBlog.Model.PostActionResultModel.Error = null
            FSharpBlog.Model.PostActionResultModel.PostId = postData.Id
        }
      
      addPost firstPost |> ignore
      services.AddSingleton<FSharpBlog.DataAccess.AddPost>( addPost  ) |> ignore 

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore
    configureDataServices(services) |> ignore

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

[<EntryPoint>]
let main _ =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    WebHostBuilder()
        .UseKestrel()
        .UseContentRoot(contentRoot)
        .UseIISIntegration()
        .UseWebRoot(webRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0