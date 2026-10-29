---
title: "Configuration-backed credentials"
permalink: /configuration-credentials/
nav_exclude: true
---

# Configuration-backed credentials

Configuration, environment variables, and secret vaults are ways to supply the database account's username and password. They do not change how the database authenticates that account.

See [Username and password]({{ '/username-password-authentication/' | relative_url }}#library) for Library setup, including `ConfigurationUsernamePasswordCredentialSource`, and its [database/OS support table]({{ '/username-password-authentication/' | relative_url }}#database-and-operating-system-support). An application's configuration provider can obtain values from a vault using that vault's own workload identity and permissions.

For terminal prompts, OS credential stores, or environment-backed CLI connections, use the [CLI instructions]({{ '/username-password-authentication/' | relative_url }}#cli) on the same method page.
