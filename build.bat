IF NOT EXIST paket.lock (
    START /WAIT .paket/paket.exe install
)
dotnet restore src/FSharpBlog
dotnet build src/FSharpBlog

dotnet restore tests/FSharpBlog.Tests
dotnet build tests/FSharpBlog.Tests
dotnet test tests/FSharpBlog.Tests
