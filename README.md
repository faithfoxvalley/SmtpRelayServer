# SmtpRelayServer
 
An SMTP server for sending messages via Microsoft Exchange

## Setup
Use of docker is recommended, but the server will run without docker if you build and compile it.

```yaml
services:
  smtp-relay:
    image: ghcr.io/faithfoxvalley/smtp-relay
    volumes:
      - smtp-relay:/data
    ports:
      - 25:25
    restart: unless-stopped
```

Configuration is done in the /data/config.toml file: 
```toml
[exchange]
client_id = ""
tenant_id = ""
client_secret = ""
[smtp]
host_name = "localhost"
email_domain_filter = ["example.com"] # Restrict the domain of the email addresses (empty for no filter)
connection_subnet_filter = ["192.168.1.1/24", "172.0.0.0/8"] # Restrict the ip of the smtp client (empty for no filter)
certificate = "" # Specify a certificate file here for tls/ssl
certificate_key = ""
certificate_password = ""
[[user_account]]
username = "user"
password = "smtp_password"
exchange_email = "email@example.com"
```

