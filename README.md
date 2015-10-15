# AkkaNotifications
Uses Redis' Pub/Sub system to generate events and Akka.Net to act on them.

Install Redis from Chocolatey for this to work
```
choco install redis-64
```

From a terminal, to enter the redis command line interface
```
redis-cli
```

Now you can publish messages from the terminal that our program will act on,

```
publish funky "Hello, World" (publish channel message)
```
