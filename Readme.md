# Backup your MongoDB database to Google Cloud Storage

## Environment Variables

* **MONGODB__URI**: The uri to your mongo server. Default value: `mongodb://localhost:27017`
* **MONGODB__DUMPBINARYPATH**: The path to the mongodump binary. Part of the docker container.
* **BACKUP__FILENAME**: The file name of your backup. Default value: `"backup-{0:yyyy-MM-dd-hh-mm-ss}.gzip"`
* **GOOGLESTORAGE__BUCKETNAME**: The name of your bucket.

## Docker

> docker run -e "MONGODB__URI=<MY_MONGODB_SERVER>" -e "GOOGLESTORAGE__BUCKETNAME=<MY_BUCKET> sebastianstehle/mongodb-backup:latest"

## Kubernetes

Use the example configs in the k8 folder. Not that you have to create a service account with permissions to the cloud storage.

https://cloud.google.com/kubernetes-engine/docs/tutorials/authenticating-to-cloud-platform#step_3_create_service_account_credentials