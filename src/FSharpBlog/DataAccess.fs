module FSharpBlog.DataAccess

open FSharpBlog.Model

type GetPageData = string -> Post Option

type AddPost = Post -> PostActionResultModel