# Liberex

### Run

以下命令会持久化数据库和配置文件，同时在 `/data` 目录挂载EPUB文件

```sh
docker run -it --rm -p 25511:80 -v {app_dir}:/app/data -v {epub_dir}:/data wuyu8512/liberex:dev
```

For Web dev

```sh
docker run -it --rm -p 25511:80 -v {app_dir}:/app/data -v {epub_dir}:/data -e ASPNETCORE_ENVIRONMENT=Development wuyu8512/liberex:dev
```
