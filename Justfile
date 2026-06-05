set quiet

solution := "src/DotNetProjectSdk.slnx"
project := "src/src/DotNetProjectSdk/DotNetProjectSdk.csproj"
current_version := `node -p "require('./package.json').version"`
build_configuration := "Release"
artifacts_folder := "./artifacts"

[private]
default:
    @just --list

# Build and test with the specified configuration, defaulting to "Release"
build configuration=build_configuration version=current_version:
    dotnet build {{solution}} -c {{configuration}} -p:Version={{version}}

# Run tests with the specified configuration, defaulting to "Release"
test configuration=build_configuration:
    dotnet test {{solution}} -c {{configuration}}

# Create NuGet package for the project
pack publish_folder=artifacts_folder version=current_version:
    dotnet pack {{project}} -o {{publish_folder}} -p:PackageVersion={{version}} -p:Version={{version}}

# Display the current version from package.json
current_version:
    @echo {{current_version}}

# Open the solution in Visual Studio
vs:
    open {{solution}}
