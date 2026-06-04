set shell := ["pwsh", "-NoLogo", "-NoProfile", "-Command"]

solution := "DotNetProjectSdk.slnx"
project := "src\\DotNetProjectSdk\\DotNetProjectSdk.csproj"
current_version := `node -p "require('./package.json').version"`

default:
    @just --list

build:
    dotnet build {{solution}}

test:
    dotnet test {{solution}}

pack version=current_version:
    dotnet pack {{project}} -o .\\artifacts -p:PackageVersion={{version}}