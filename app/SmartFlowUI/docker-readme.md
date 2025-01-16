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
docker run -it --rm smi AzureStorageAccountEndpoint="https://xxxxxx.blob.core.windows.net/"
```

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
