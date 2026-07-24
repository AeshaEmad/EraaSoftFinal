# AeroFly secure deployment checklist

## Rotate exposed credentials before running the application

The old database password, Gmail app password, and Stripe test secret were committed
in earlier Git history. Removing them from current files does not revoke them.

1. Reset the database user's password and restrict its network access.
2. Revoke the exposed Gmail app password and create a new app password.
3. Roll the exposed Stripe test secret key.
4. Replace any publish-profile credentials that were used.
5. If the repository will become public, purge secrets from Git history with
   `git filter-repo`, coordinate a force-push, and require collaborators to re-clone.

Store new values in deployment environment variables. For local development:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."
dotnet user-secrets set "EmailSettings:SenderEmail" "..."
dotnet user-secrets set "EmailSettings:SenderPassword" "..."
dotnet user-secrets set "Stripe:PublishableKey" "..."
dotnet user-secrets set "Stripe:SecretKey" "..."
dotnet user-secrets set "Stripe:WebhookSecret" "..."
```

## First SuperAdmin

No fixed administrator is created. On an empty database, temporarily configure
`Security__BootstrapAdminEmail` and a 14+ character
`Security__BootstrapAdminPassword` containing upper/lowercase letters, a number,
and a symbol. Start once, sign in, and change the password when prompted. Then
delete both bootstrap variables.

## Stripe webhook

Configure Stripe to send these events to `POST /api/stripe/webhook`:

- `payment_intent.succeeded`
- `payment_intent.payment_failed`
- `refund.updated`

Set the endpoint signing secret as `Stripe__WebhookSecret`.
