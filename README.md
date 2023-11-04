# Liberex

## Build

```sh
docker build -t liberex ./Liberex
```

## Run

```sh
docker run -it --rm -p 12345:80 -v {path}:/data liberex
```

## Run for dev

```sh
docker run -it --rm -p 12345:80 -v {path}:/data -e ASPNETCORE_ENVIRONMENT=Development liberex
```