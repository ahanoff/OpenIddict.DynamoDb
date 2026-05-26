VERSION ?= 0.1.0-beta1
NUGET_SOURCE ?= https://api.nuget.org/v3/index.json
NUGET_API_KEY ?=

.PHONY: build test pack publish clean

build:
	dotnet build

test:
	dotnet test

pack: clean
	dotnet pack src/OpenIddict.DynamoDb/OpenIddict.DynamoDb.csproj -c Release -p:Version=$(VERSION) -o dist

publish: pack
	dotnet nuget push dist/*.nupkg --api-key $(NUGET_API_KEY) --source $(NUGET_SOURCE)

clean:
	dotnet clean
	rm -rf dist
