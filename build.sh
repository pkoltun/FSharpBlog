if [ ! -e "paket.lock" ]
then
    exec mono .paket/paket.exe install
fi
dotnet restore src/FSharpBlog
dotnet build src/FSharpBlog

dotnet restore tests/FSharpBlog.Tests
dotnet build tests/FSharpBlog.Tests
dotnet test tests/FSharpBlog.Tests
