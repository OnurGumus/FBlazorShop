dotnet tool restore
dotnet paket restore
dotnet build /p:DefineConstants=WASM
dotnet run --no-build  -p FBlazorShop.Web\FBlazorShop.Web.fsproj /p:Platform=x64
