# Docker Sample Commands

## Build the app

``` bash
docker build -t smi -f Dockerfile .
```

## Run the app

The docker run command creates and runs the container as a single command. This command eliminates the need to run docker create and then docker start. You can also set this command to automatically delete the container when the container stops by adding --rm

``` bash
docker run -it --rm smi
```

Run with a parameter:

``` bash
# the id of the user assigned managed identity
docker run -it --rm smi AzureStorageAccountEndpoint="https://llgh114stdev.blob.core.windows.net/" UseManagedIdentityResourceAccess="true" UserAssignedManagedIdentityClientId="c601bb86-0b2d-4c20-88af-453d99175c48"

#my id
docker run -it --rm smi AzureStorageAccountEndpoint="https://llgh114stdev.blob.core.windows.net/" UseManagedIdentityResourceAccess="false" UserAssignedManagedIdentityClientId="af35198e-8dc7-4a2e-a41e-b2ba79bebd51"
```

## Docker Auth Notes from Piotr

Docker won't work with user assigned identity when you run locally - it has no good way to get the token

Workarounds:

- Run docker from full Visual Studio, and with right dependencies (something like visual studio containers nuget package) - it may work
- Do multi-layer build like I do in DOW, where for local runs I install azure CLI in the container
- There's another way where you can provide certain env variables, but it's little hacky - it was - something with docker compose
- You can create a service principal and inject client id/secret into the container

## View list of images

``` bash
docker images 
```

## View current usage

``` bash
docker stats
```

## Create a new container (that is stopped)

``` bash
docker create --name smi-container smi
```

## To see a list of all containers

``` bash
docker ps -a
```

## Brig's Developer Notes

``` bash
# Update the file:  ./app/Assistant.Hub.Api/appsettings.Development.json
# load .env vars
[ ! -f .env ] || export $(grep -v '^#' .env | xargs)
# or this version allows variable substitution and quoted long values
[ -f .env ] && while IFS= read -r line; do [[ $line =~ ^[^#]*= ]] && eval "export $line"; done < .env

# Build Docker image
project_root=$(git rev-parse --show-toplevel)
dockerfile_root="${project_root}/app/Assistant.Hub.Api"
dockerfile_path="${dockerfile_root}/Dockerfile"
image_name="ai.doc.eval.dotnet_app"
docker build --build-arg "BUILD_CONFIGURATION=Development" -t "${image_name}.dev" -f "${dockerfile_path}" "${dockerfile_root}"

# Run container locally
docker run -p 8080:8080 -p 8081:8081 -p 32771:32771 "${image_name}.dev"

# Interactive shell
docker run -it --entrypoint /bin/bash -p 8080:8080 -p 8081:8081 -p 32771:32771 "${image_name}.dev"
# Start service in container
$ dotnet Assistant.Hub.Api.dll

# Connect to running image
docker exec -it $(docker ps --filter "ancestor=${image_name}.dev"  -q ) /bin/bash

# Test endpoint
curl -X POST -v http://localhost:8080/api/chat/weather \
     -H "X-Api-key: $DOTNET_APP_API_KEY" \
     -H "Content-Type: application/json" \
     -d '[{ "user": "What is the forecast for Mankato MN" }]'
```

## Connect to a running container to see the output and peek at the output stream

``` bash
docker attach --sig-proxy=false smi-container
```

## Start the container and show only containers that are running

``` bash
docker start smi-container
docker ps
```

## Stop the container

``` bash
docker stop smi-container
```

## Delete the container and check for existence

``` bash
docker ps -a
docker rm smi-container
docker ps -a
```

## Delete images you no longer want

 You can delete any images that you no longer want on your machine.  Delete the image created by your Dockerfile and then delete the .NET image the Dockerfile was based on. You can use the IMAGE ID or the REPOSITORY:TAG formatted string.

``` bash
  docker rmi smi:latest
  docker rmi mcr.microsoft.com/dotnet/aspnet:9.0
```
