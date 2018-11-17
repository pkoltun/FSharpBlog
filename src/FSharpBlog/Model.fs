module FSharpBlog.Model

[<CLIMutable>]
type Post =
    {
        Id : int
        PageContent : string
        Title : string
        UrlPath : string
    }

[<CLIMutable>]
type PostActionResultModel =
    {
        Success: bool
        PostId: int
        Error: string 
    }
