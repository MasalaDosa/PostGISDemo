A simple demo demonstating a PostGIS spatial search

To run launch a docker container running PostGIS:
docker run -d --name postgis -e POSTGRES_DB=test -e POSTGRES_PASSWORD=secret -e POSTGRES_USER=postgres -p 5432:5432 postgis/postgis:16-3.4