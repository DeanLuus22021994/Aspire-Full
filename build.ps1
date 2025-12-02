#!/usr/bin/env pwsh
# Aspire-Full Build Script

dotnet nuget locals all --clear
dotnet clean
dotnet restore
dotnet format
dotnet build
dotnet test
dotnet pack
dotnet publish
dotnet workload list
dotnet run
dotnet watch
