#!/bin/sh
dotnet tool restore
dotnet paket restore
dotnet publish ./FBlazorShop.Web/FBlazorShop.Web.fsproj /p:Configuration=Release /p:Platform=x64 /p:DefineConstants=WASM
cd ./FBlazorShop.Web/bin/x64/Release/netcoreapp3.1/publish/
dotnet FBlazorShop.Web.dll
