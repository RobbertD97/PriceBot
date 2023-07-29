dotnet publish -c release

-> Start Docker

docker build -t pricebot-image -f Dockerfile .
docker create --name pricebot-bcc pricebot-image

-> Run the Docker container
docker ps

docker commit pricebot-bcc pricebot-image
docker save -o pricebot-bcc.tar pricebot-image


-> Import .tar file from Container Manager -> Image
-> Create Volume mapping: /docker/appdata/pricebot-bcc/urls-to-track.json -> /App/urls-to-track.json