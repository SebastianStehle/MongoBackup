# Backup your MongoDB database to Google Cloud Storage

## Parameters

* **MONGODB__URI**: The uri to your mongo server. Default value: `mongodb://localhost:27017`
* **MONGODB__DUMPBINARYPATH**: The path to the mongodump binary. Part of the docker container.
* **BACKUP__FILENAME**: The file name of your backup. Default value: `"backup-{0:yyyy-MM-dd-hh-mm-ss}.gzip"`
* **GOOGLESTORAGE__BUCKETNAME**: The name of your bucket.