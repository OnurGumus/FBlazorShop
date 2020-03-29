dotnet tool restore
dotnet paket restore
dotnet publish ./FBlazorShop.Web/FBlazorShop.Web.fsproj /p:Configuration=Release /p:Platform=x64
cd ./FBlazorShop.Web/bin/x64/Release/netcoreapp3.1/publish/
dotnet FBlazorShop.Web.dll
