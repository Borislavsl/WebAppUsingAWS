# WebAppUsingAWS
Simple Web App in ASP .NET Core 3.1 MVC using S3, SQS, and SNS AWS
There are 3 actions for Simple Storage Service se:
  - creating an S3 bucket,
  - put two objects into it (first - only key name is specified, second - a text file) and list the bucket after that - key names and sizes of the objects
  - read the second object - title, content type and the content itself - the text from the file

There is an action for Simple Queue Service: Create an SQS queue, send two messages to the queue, receive the messages, and delete them.

There is an action for Simple Notification Service: Create an SNS topic with attributes, send a subscription invitation to an email, wait for confirmation, and publish a notification.
