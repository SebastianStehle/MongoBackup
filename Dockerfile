FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
 
WORKDIR /MongoBackup

# Build Step 1: Install packages
COPY MongoBackup/MongoBackup.csproj .

RUN dotnet restore

# Build Step 2: Create publish artifact
COPY . .

RUN dotnet publish -c release -o /out/ 

FROM mcr.microsoft.com/dotnet/aspnet:8.0.0-bookworm-slim

# Install wget
RUN apt-get update \
 && apt-get install -y gnupg ca-certificates wget

RUN wget -qO - https://www.mongodb.org/static/pgp/server-4.2.asc | apt-key add -

RUN echo "deb http://repo.mongodb.org/apt/debian buster/mongodb-org/4.2 main" | tee /etc/apt/sources.list.d/mongodb-org-4.2.list

# Install mongodb binaries
RUN apt-get update --allow-unauthenticated \
 && apt-get install -y mongodb-org-tools

# Set the path to the mongodump
ENV MongoDB__DumpBinaryPath=/usr/bin/mongodump

WORKDIR /app/

COPY --from=builder /out/ .

CMD ["dotnet", "./MongoBackup.dll"]  