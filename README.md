# Liberex

### Build

```sh
docker build -t liberex .
```

### Run

以下命令会持久化数据库和配置文件，同时在 `/data` 目录挂载EPUB文件

```sh
docker run -it --rm -p 12345:80 -v {app_dir}:/app/data -v {epub_dir}:/data liberex
```

For dev

```sh
docker run -it --rm -p 12345:80 -v {app_dir}:/app/data -v {epub_dir}:/data -e ASPNETCORE_ENVIRONMENT=Development liberex
```
