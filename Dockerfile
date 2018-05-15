FROM microsoft/dotnet:2.1-sdk-alpine as builder  
 
WORKDIR /MongoBackup

# Build Step 1: Install packages
COPY MongoBackup/MongoBackup.csproj .

RUN dotnet restore

# Build Step 2: Create publish artifact
COPY . .

RUN dotnet publish -c release -o /out/ 

FROM microsoft/dotnet:2.1-runtime-alpine

# Install mongodb binaries
RUN apk add --no-cache mongodb-tools

# Set the path to the mongodump
ENV MongoDB__DumpBinaryPath /usr/bin/mongodump

WORKDIR /app/

COPY --from=builder /out/ .

CMD ["dotnet", "./MongoBackup.dll"]  