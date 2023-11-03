# Liberex

## Build

```sh
cd Librex
docker build -t liberex .
```

## Run

```sh
docker run -it --rm -p:12345:80 -v:{path}:/app/data/library liberex
```