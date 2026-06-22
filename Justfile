set quiet

solution := "src/DotNetProjectSdk.slnx"
current_version := `node -p "require('./package.json').version"`
build_configuration := "Release"
artifacts_folder := "./artifacts"
default_test_filter:= "/*/*/*/*/"

[private]
default:
    just --list

# Build and test with the specified configuration, defaulting to "Release"
build solutionOrProject=solution configuration=build_configuration:
    dotnet build {{ solutionOrProject }} -c {{ configuration }}

# Run tests with the specified configuration, defaulting to "Release"
test solutionOrProject=solution configuration=build_configuration filter=default_test_filter:
    dotnet test {{ solutionOrProject }} -c {{ configuration }} --treenode-filter {{ filter }}

# Run tests with the specified configuration, defaulting to "Release"
restore solutionOrProject=solution:
    dotnet restore {{ solutionOrProject }}

# Create NuGet package for the project
pack solutionOrProject=solution publish_folder=artifacts_folder:
    dotnet pack {{ solutionOrProject }} -o {{ publish_folder }}

# Displays the current version from package.json
current_version:
    echo "Current version is {{ BLUE }}{{ current_version }}{{ NORMAL }}"

# Check code formatting using CSharpier
lint-check:
    dotnet csharpier check .

# Fix code formatting issues using CSharpier
lint-fix:
    dotnet csharpier format .

# Open the solution in Visual Studio/ Registered application
vs:
    open {{solution}}
